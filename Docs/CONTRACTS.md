# SuperIronRuby — Architecture Contracts

This document is the binding contract between subsystems. Code written in one
subsystem may rely on everything stated here about the others. If you need to
deviate, update this file in the same change.

## Project layout

```
Src/SuperIronRuby/              core library (namespace SuperIronRuby.*)
  Parser/                       Prism binding (namespace SuperIronRuby.Parser)
    Nodes.g.cs                  generated: one sealed class per Prism node type
    Loader.g.cs                 generated: per-node-type deserialization
    Loader.cs                   hand-written: header/varint/constant-pool/locations
    PrismNative.cs              P/Invoke to libprism.so (pm_serialize_parse)
    PrismParser.cs              public entry point (+ MRI-subprocess fallback backend)
  Runtime/                      object model (namespace SuperIronRuby.Runtime)
  Builtins/                     builtin classes in C# (namespace SuperIronRuby.Builtins)
  Interpreter/                  tree-walking evaluator (namespace SuperIronRuby.Interpreter)
  Hosting/                      public embedding API (namespace SuperIronRuby.Hosting)
  Lib/core/*.rb                 core-library parts written in Ruby, embedded resources,
                                loaded at engine startup after C# builtins
Src/SuperIronRuby.Console/      the `sir` CLI
Tools/AstDump/                  debug tool: print AST preorder (used by diff tests)
Tools/generate_nodes.rb         Ruby script: Vendor/prism/config.yml -> Nodes.g.cs + Loader.g.cs
Tests/SuperIronRuby.Tests/      xUnit tests
Tests/diff/                     differential tests against MRI ruby 4.0.2
  snippets/NNN_name.rb          corpus; each prints to stdout
  run_diff.sh                   runs each snippet under `ruby` and `sir`, compares stdout
```

## Parser contract (SuperIronRuby.Parser)

- Prism native library: `Vendor/prism/libprism.so`, version **1.8.1** — the
  serialization format ground truth is
  `~/.rbenv/versions/4.0.2/lib/ruby/4.0.0/prism/serialize.rb` (rendered, v1.8.1)
  and `Vendor/prism/config.yml` (cloned at tag v1.8.1).
- `DllImport("prism")` — the .so is copied next to the managed assembly.
- Public API:

```csharp
namespace SuperIronRuby.Parser;

public sealed class ParseResult {
    public ProgramNode Root { get; }
    public string Source { get; }           // original source text
    public string FilePath { get; }
    public IReadOnlyList<ParseDiagnostic> Errors { get; }
    public IReadOnlyList<ParseDiagnostic> Warnings { get; }
    public bool Success => Errors.Count == 0;
}

public static class PrismParser {
    // Throws nothing for syntax errors; check result.Errors.
    public static ParseResult Parse(string source, string filePath = "(eval)");
}
```

- Node classes: same names as Prism Ruby API (`CallNode`, `DefNode`,
  `IntegerNode`, ...). Common base:

```csharp
public abstract class Node {
    public abstract NodeType Type { get; }   // enum generated from config.yml
    public uint NodeId;
    public Location Location;                // start offset + length (bytes)
    public abstract IEnumerable<Node?> ChildNodes();
}
public readonly struct Location { public readonly int StartOffset; public readonly int Length; }
```

- Field naming: Prism `name` -> C# `Name` (PascalCase). Field types:
  `node` -> concrete node class or `Node`; `node?` -> nullable; `node[]` ->
  `Node[]` (or typed array); `string` -> `string`; `constant` -> `string`
  (resolved from constant pool); `constant[]` -> `string[]`; `location` ->
  `Location`; `location?` -> `Location?`; `uint8`/`uint32` -> `int`;
  `integer` -> `object` (long or System.Numerics.BigInteger); `double` ->
  `double`; flags -> `int Flags` plus generated `[Flags] enum` per flag set.
- `IntegerNode` exposes `object Value` (long, promoted to BigInteger when
  needed); `FloatNode.Value` is double; string-bearing nodes (`StringNode`,
  `SymbolNode`, ...) expose their unescaped value as C# `string` (UTF-8 decoded).

## Runtime contract (SuperIronRuby.Runtime)

Value representation (CLR object mapping):

| Ruby value   | CLR representation                          |
|--------------|---------------------------------------------|
| nil          | `null`                                      |
| true/false   | `bool`                                      |
| Integer      | `long`; `System.Numerics.BigInteger` when out of range (normalize back to long when it fits) |
| Float        | `double`                                    |
| String       | `MutableString` (mutable, UTF-8 semantics, `IsFrozen`) |
| Symbol       | `RubySymbol` (interned; reference-equal)    |
| Array        | `RubyArray` (extends/wraps `List<object?>`) |
| Hash         | `RubyHash` (insertion-ordered, Ruby eql?/hash key semantics) |
| Range        | `RubyRange` (Begin, End, ExcludeEnd; nullable ends) |
| Proc/Lambda  | `RubyProc` (`IsLambda`, `Call(object?[] args)`) |
| Regexp       | `RubyRegexp` (wraps .NET Regex)             |
| Class/Module | `RubyClass` / `RubyModule`                  |
| Method       | `RubyBoundMethod`                           |
| user object  | `RubyObject` (RubyClass + ivar table); user classes may subclass |
| exception    | `RubyExceptionObject : RubyObject` (Message, Backtrace) |

Core types:

```csharp
namespace SuperIronRuby.Runtime;

public sealed class RubyContext {
    public RubyClass ObjectClass, BasicObjectClass, ModuleClass, ClassClass, ... ;
    public RubyClass GetClassOf(object? value);          // logical class (singleton-aware lookup uses GetImmediateClassOf)
    public RubyModule DefineModule(string name, RubyModule? lexicalParent = null);
    public RubyClass DefineClass(string name, RubyClass superclass, RubyModule? lexicalParent = null);
    public RubySymbol Intern(string name);
    public object? GetGlobal(string name); public void SetGlobal(string name, object? value);
    // Method dispatch — THE central API:
    public object? Send(object? receiver, string methodName, object?[] args, RubyProc? block = null);
    public RubyMethodInfo? ResolveMethod(object? receiver, string name);  // null if missing (caller handles method_missing)
}

public class RubyModule {
    public string Name;
    public IReadOnlyList<RubyModule> Ancestors { get; }   // C3-ish linearization: prepends, self, includes, superclass chain
    public void DefineMethod(string name, RubyMethodInfo method);
    public RubyMethodInfo? LookupMethod(string name);     // walks Ancestors
    public bool TryGetConstant(string name, out object? value);  // walks lexical handled by interpreter; this walks ancestors
    public void SetConstant(string name, object? value);
    public void Include(RubyModule m); public void Prepend(RubyModule m);
    public int MethodVersion;                              // bumped on any mutation; invalidates caches
}

public sealed class RubyClass : RubyModule {
    public RubyClass? Superclass;
    public Func<RubyContext, RubyClass, object>? Allocator; // default: new RubyObject(cls)
}

// All builtin methods use ONE uniform signature:
public delegate object? BuiltinMethodBody(RubyContext context, object? self, object?[] args, RubyProc? block);

public sealed class RubyMethodInfo {
    public string Name;
    public RubyModule Owner;
    public RubyMethodVisibility Visibility;   // Public/Private/Protected
    public int ArityMin, ArityMax;            // ArityMax = -1 for splat
    // exactly one of:
    public BuiltinMethodBody? Builtin;
    public object? InterpretedDef;            // DefNode + closure info, owned by interpreter
}
```

Builtin declaration (IronRuby-style attributes), loaded via reflection by
`BuiltinLoader.LoadAssembly(ctx, assembly)`:

```csharp
[RubyClass("String", Extends = typeof(MutableString))]   // or [RubyModule("Kernel")]
public static class StringOps {
    [RubyMethod("upcase")]
    public static object? Upcase(RubyContext ctx, object? self, object?[] args, RubyProc? block) { ... }

    [RubyMethod("new", RubyMethodKind.Static)]            // class-level method (singleton)
    public static object? New(...) { ... }
}
```

Exceptions: Ruby-level exceptions are carried by C#
`RubyRaiseException : Exception { public RubyExceptionObject RubyException; }`.
Raise helpers: `ctx.RaiseError(ctx.TypeErrorClass, "message")` etc.
Non-local control flow uses C# exceptions owned by the interpreter:
`BreakUnwind(value, procSourceId)`, `NextUnwind(value)`, `RedoUnwind`,
`RetryUnwind`, `ReturnUnwind(value, frameId)`, `ThrowUnwind(tag, value)`.
Builtins must NOT swallow these (catch `RubyRaiseException` only when needed).

Block semantics builtins must honor:
- `RubyProc.Call(args)` may throw `BreakUnwind` — let it propagate.
- `yield`ing builtins (each, map, ...) just call `block.Call(...)`.

## Interpreter contract

```csharp
namespace SuperIronRuby.Interpreter;
public sealed class Interpreter {
    public Interpreter(RubyContext context);
    public object? Run(ParseResult unit, RubyScope? scope = null); // top-level execution
}
```
Interpreted method bodies are registered into `RubyMethodInfo.InterpretedDef`;
`RubyContext.Send` calls back into the interpreter through a delegate installed
at startup (`ctx.InterpretedInvoker`), so Runtime has no compile-time dependency
on Interpreter.

## Hosting contract

```csharp
namespace SuperIronRuby.Hosting;
public static class Ruby {                       // mirrors IronRuby's API shape
    public static ScriptEngine CreateEngine();
}
public sealed class ScriptEngine {
    public object? Execute(string code);
    public object? ExecuteFile(string path);
    public string ExecuteToString(string code);  // captures stdout, for tests
    public RubyContext Context { get; }
}
```
Engine startup order: create RubyContext (bootstraps class hierarchy) →
BuiltinLoader loads C# builtins → Interpreter installed → embedded
`Lib/core/*.rb` files evaluated (enumerable.rb, comparable.rb, ...).

## CLI contract (`sir`)

```
sir script.rb [args...]     run a file (ARGV populated)
sir -e 'code'               run one-liner
sir                         REPL
```
Exit code 0 on success; 1 on uncaught Ruby exception, printing
`file:line:in 'method': message (ClassName)` to stderr like MRI.

## Differential test contract

Each `Tests/diff/snippets/*.rb` prints deterministic output (no object ids, no
hash iteration order tricks beyond insertion order, no timing). `run_diff.sh`
executes both `ruby` (4.0.2) and `sir` on each file and diffs stdout; non-zero
exit on any mismatch. Snippet authors MUST verify expected behavior against the
local `ruby` 4.0.2 before committing a snippet.

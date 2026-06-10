# Runtime notes (R1–R8)

The runtime object model is complete and dispatch-capable, exercised end-to-end
by `Tests/.../Runtime/SendIntegrationTests.cs` (build a class graph via the API,
drive it with `Send`). 92 runtime tests pass; semantics verified against
ruby 4.0.2.

## Value mapping (final)

| Ruby value   | CLR representation |
|--------------|--------------------|
| nil          | `null` |
| true / false | `bool` |
| Integer      | `long`, promoted to `System.Numerics.BigInteger` out of range |
| Float        | `double` |
| String       | `MutableString` |
| Symbol       | `RubySymbol` (interned via `RubyContext.Intern`) |
| Array        | `RubyArray : List<object?>` |
| Hash         | `RubyHash` (insertion-ordered, `RubyValueComparer` keys) |
| Range        | `RubyRange` |
| Regexp       | `RubyRegexp` |
| Proc/Lambda  | `RubyProc` |
| Class/Module | `RubyClass` / `RubyModule` |
| exception    | `RubyExceptionObject : RubyObject` |
| user object  | `RubyObject` |
| custom CLR   | routed via `RegisterClrExtension` (e.g. `[RubyClass(Extends=...)]`) |

## Dispatch

`RubyContext.Send(receiver, name, args, block, flags, callerSelf)` is the single
entry point. Resolution = `GetImmediateClassOf(receiver).LookupMethod(name)` with
a `(class, name)` cache invalidated by `RubyModule.GlobalVersion` (bumped on any
DefineMethod/include/prepend/superclass change — naive global invalidation).
Undefined → `method_missing` (symbol + args) → `NoMethodError`. Builtin arity is
enforced; interpreted methods go through `InterpretedInvoker` (installed by the
interpreter at engine startup, task W1).

## Known deviations / TODOs (collected from R1–R7)

- **Method cache** is global-version invalidated and not thread-safe (single
  engine assumed). Fine for a tree-walking interpreter; revisit for concurrency.
- **object_id**: immediate ids match MRI (nil=4, false=0, true=20, Integer n →
  2n+1); reference objects get a stable growing counter (not a real pointer).
- **MutableString.Length** is UTF-16 code units, not codepoints. String#length
  codepoint-correctness lands in B3.
- **RubyRegexp.TranslatePattern** is a stub seam; POSIX classes / `\h` etc. land
  in B4.
- **Protected visibility** check is pragmatic (`callerSelf kind_of owner`); some
  corner cases (module-function protected, refinements) are out of scope.
- **Singleton classes** for immediates are not real per-object eigenclasses;
  defining a singleton method on nil/true/false targets the class (as MRI does),
  numbers/symbols are not yet rejected with TypeError (B-layer).
- **inspect/to_s defaults** include a fake hex address; differential snippets
  must avoid printing object addresses (documented in `Tests/diff/README.md`).

## What the interpreter layer relies on (contract)

- `ctx.InterpretedInvoker` delegate slot for Ruby-defined methods.
- `ctx.MainObject`, `ctx.Stdout/Stderr/Stdin`.
- The non-local unwind exceptions in `Runtime/Unwinds.cs` (Break/Next/Redo/
  Retry/Return/Throw) — builtins must let them propagate.
- `RubyMethodInfo.InterpretedDef` holds the interpreter's method body, opaque to
  the runtime.

See `Docs/CONTRACTS.md` for the full inter-subsystem contract (kept truthful as
of R8).

# SuperIronRuby

**SuperIronRuby** is a Ruby 4 implementation for .NET — a spiritual successor to
[IronRuby](https://github.com/IronLanguages/ironruby), rebuilt from scratch for
modern .NET (net10.0) and modern Ruby (4.0).

```ruby
# sir -e '...'
Point = Struct.new(:x, :y)
puts [Point.new(3, 4), Point.new(1, 2)].sort_by(&:x).map(&:inspect)

puts (1..10).select(&:even?).sum            # => 30
puts "2024-01-15".match(/(\d+)-(\d+)-(\d+)/)[1]   # => 2024

# CLR interop, IronRuby-style:
sb = System::Text::StringBuilder.new
sb.append("hello"); sb.append(" world")
puts sb.to_string                            # => hello world
```

Where the original IronRuby (2011, Ruby 1.9.2, .NET 4.0) used a GPPG-generated
parser and the DLR, SuperIronRuby:

- **parses with [Prism](https://github.com/ruby/prism)** — the official portable
  Ruby parser used by CRuby 4.0, JRuby and TruffleRuby — via P/Invoke plus a C#
  deserializer for Prism's binary AST format (v1.8.1), so the full Ruby 4 syntax
  is supported with high fidelity from day one;
- **executes with a tree-walking interpreter** over the Prism AST;
- keeps IronRuby's signature designs: builtin classes declared in C# with
  `[RubyClass]` / `[RubyMethod]` attributes, an embedding API shaped like
  `Ruby.CreateEngine()`, and **`System::`-namespace CLR interop** from Ruby code.

## Status (v0.1.0)

A working Ruby 4 interpreter: **287 unit tests** green, **13/13 differential
snippets** match CRuby 4.0.2 byte-for-byte, ~12k lines of C# + ~320 lines of Ruby.

| Area | Status |
|------|--------|
| **Parser** (Prism v1.8.1, 151 node types, binary deserializer, P/Invoke) | ✅ full syntax |
| **Object model** (classes, modules, MRI ancestor linearization, singletons) | ✅ |
| **Dispatch** (method cache, `method_missing`, visibility, `super`) | ✅ |
| **Language**: literals, calls, `def`/params/`yield`/`return`, classes/modules, constants, ivars/cvars/gvars, `if`/`while`/`case`, `begin`/`rescue`/`ensure`/`retry`, multiple & operator assignment, blocks/procs/lambdas (numbered params, `it`), `case`/`in` **pattern matching**, string/array/hash/range/regexp literals, `defined?` | ✅ |
| **Core library** (~310 builtin methods): Kernel, Object, Module/Class, Integer (BigInteger), Float, String, Symbol, Array, Hash, Range, Comparable, **Enumerable** (in Ruby), Regexp/MatchData, Struct/Data, Proc/Method, Exception hierarchy | ✅ common surface |
| **Exceptions** (full MRI hierarchy + MRI-exact messages) | ✅ |
| **CLR interop** (`System::...` namespaces, reflection dispatch, conversions) | ✅ |
| **`sir` CLI** (file / `-e` / REPL / `--ast` / errors / ARGV) | ✅ |

Roadmap: Enumerator (lazy/blockless forms), encodings, more stdlib (IO/File/Set),
Ractor::Port, frozen-string-literal default, `ruby/spec` via mspec, a bytecode
compilation tier, and NuGet packaging.

## Build & run

```sh
dotnet build
dotnet run --project Src/SuperIronRuby.Console -- -e 'puts "hello from .NET Ruby"'
dotnet run --project Src/SuperIronRuby.Console -- script.rb

# the built CLI is named `sir`:
Src/SuperIronRuby.Console/bin/Debug/net10.0/sir -e 'puts (1..5).sum'

dotnet test                  # unit tests
Tests/diff/run_diff.sh       # differential tests vs local ruby 4.0.2
Tools/smoke_cli.sh           # CLI smoke test
```

Requires the **.NET 10 SDK**. `Vendor/prism/libprism.so` is built from prism
v1.8.1; regenerate the parser node classes with `ruby Tools/generate_nodes.rb`.

## How it fits together

```
Ruby source
   │  PrismParser.Parse  (P/Invoke libprism → binary AST → C# deserializer)
   ▼
Prism AST  (151 generated node classes)
   │  Interpreter  (tree-walk, partial class per node family)
   ▼
RubyContext  (classes/modules, dispatch, exceptions)  ←  Builtins ([RubyClass]/[RubyMethod])
                                                       ←  Lib/core/*.rb (Enumerable, Comparable)
```

See `Docs/ARCHITECTURE.md` for the full design and `Docs/CONTRACTS.md` for the
inter-subsystem contracts.

## License

Apache License 2.0, like IronRuby and the DLR. Prism is MIT-licensed
(`Vendor/prism/LICENSE.md`).

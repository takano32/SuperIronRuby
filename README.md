# SuperIronRuby

**SuperIronRuby** is a Ruby 4 implementation for .NET — a spiritual successor to
[IronRuby](https://github.com/IronLanguages/ironruby), rebuilt from scratch for
modern .NET (net10.0) and modern Ruby (4.0).

Where the original IronRuby (2011, Ruby 1.9.2, .NET 4.0) used a GPPG-generated
parser and the DLR, SuperIronRuby:

- parses with **[Prism](https://github.com/ruby/prism)** — the official portable
  Ruby parser used by CRuby 4.0, JRuby and TruffleRuby — via P/Invoke and a C#
  deserializer for Prism's binary AST format (v1.8.1), so Ruby 4 syntax is
  supported with full fidelity from day one;
- executes with a tree-walking interpreter over the Prism AST (compilation
  tiers can come later);
- keeps IronRuby's signature designs: builtin classes declared in C# with
  `[RubyClass]` / `[RubyMethod]` attributes, an embedding API shaped like
  `Ruby.CreateEngine()`, and .NET interop from Ruby code.

## Status

Early but real: see `Tests/diff/` — every feature is validated by running the
same script under CRuby 4.0.2 and SuperIronRuby and comparing output.

## Build & run

```sh
dotnet build
dotnet run --project Src/SuperIronRuby.Console -- script.rb
dotnet run --project Src/SuperIronRuby.Console -- -e 'puts "hello from .NET Ruby"'
Tests/diff/run_diff.sh        # differential tests vs local ruby 4.0.2
dotnet test                   # unit tests
```

Requires: .NET 10 SDK. `Vendor/prism/libprism.so` is built from prism v1.8.1
(`Vendor/prism-src`, see `Docs/CONTRACTS.md`).

## License

Apache License 2.0, like IronRuby and the DLR. Prism is MIT-licensed
(`Vendor/prism/LICENSE.md`).

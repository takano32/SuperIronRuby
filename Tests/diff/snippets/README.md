# Differential test snippets

Each `*.rb` file here is a self-contained program that prints **deterministic**
output to stdout. `Tests/diff/run_diff.sh` runs every snippet under both the
reference Ruby (MRI 4.0.2, the `ruby` on `PATH`) and `sir` (SuperIronRuby's
CLI) and fails if their stdout differs or either exits non-zero.

See `Docs/CONTRACTS.md` → "Differential test contract" for the binding rules.
This file is the practical checklist.

## Naming

```
NNN_short_name.rb
```

* `NNN` — a zero-padded 3-digit ordinal (`001`, `002`, …) giving a stable run
  order. Group related snippets by leading digit if you like (e.g. `0xx`
  literals, `1xx` arithmetic), but the only hard rule is that the prefix sorts.
* `short_name` — lowercase, words separated by `_`. Describe the feature under
  test (`integer_arithmetic`, `string_interp`, `array_each`).

The harness selects snippets by substring, so `run_diff.sh 001` runs `001_*.rb`
and `run_diff.sh interp` runs every `*interp*.rb`.

## Snippet rules

A snippet is only useful if MRI and `sir` are guaranteed to agree whenever the
implementation is correct. Keep every source of nondeterminism out:

1. **Print, don't return.** The harness compares stdout. A snippet that only
   evaluates expressions and prints nothing always "passes" and tests nothing.
   Use `puts` / `print` / `p`.
2. **Deterministic output only.** No `object_id`, no default `#inspect` /
   `#to_s` of user objects (they embed an address: `#<Foo:0x…>`), no `Time`,
   `rand`, `Random`, `Process.pid`, `__FILE__`, absolute paths, `ENV`,
   `ObjectSpace`, GC stats, thread scheduling, or hash/set address ordering.
   If you must show an object, define `to_s`/`inspect` yourself.
3. **Insertion order is fine.** Ruby `Hash` preserves insertion order and that
   is part of the contract — iterating a hash literal in source order is
   deterministic and allowed. What is *not* allowed is relying on the ordering
   of anything keyed by object identity/address.
4. **No external state.** No file I/O outside `Dir.tmpdir` (avoid entirely if
   possible), no network, no require of gems, no reading of `STDIN`. Snippets
   run with no arguments and no stdin.
5. **Exit 0 on success.** A snippet that raises (or calls `exit 1`) is treated
   as a *broken snippet* (ERROR), not a `sir` bug. If you are deliberately
   testing exception output, rescue it and `puts` a deterministic message — do
   not let the process die.
6. **Stick to features that exist.** Only exercise behavior you have verified
   against local `ruby` 4.0.2. Run `ruby snippets/NNN_x.rb` yourself first.
7. **Small and focused.** One feature area per snippet. A failing 8-line
   snippet localizes a bug; a failing 200-line snippet does not.
8. **Float formatting.** Floats print the same in MRI and `sir` only if you
   avoid edge-case precision. Prefer integers; when you need floats, print
   values whose decimal representation is unambiguous (e.g. `1.5`, `0.25`),
   or format explicitly with `"%.3f" % x`.
9. **No locale/encoding surprises.** Assume UTF-8 source and output. ASCII is
   safest; if you use non-ASCII, keep the file UTF-8 and avoid locale-dependent
   case folding.

## Workflow for adding a snippet

```sh
# 1. write Tests/diff/snippets/NNN_feature.rb
ruby Tests/diff/snippets/NNN_feature.rb        # 2. confirm it runs & is deterministic (run twice, compare)
Tests/diff/run_diff.sh --update NNN             # 3. record the MRI expectation
Tests/diff/run_diff.sh NNN                       # 4. diff against sir (will FAIL/SKIP until sir implements it)
```

`run_diff.sh --update` writes `Tests/diff/results/<name>.expected` from MRI and
skips the `sir` comparison; commit only the `.rb`, never the generated
`results/` artifacts (they are git-ignored).

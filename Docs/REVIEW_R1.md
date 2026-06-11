# Adversarial review R1 (task V2)

A round of differential probes (the same program under CRuby 4.0.2 and `sir`,
comparing stdout) targeting common-but-easy-to-miss behaviors. Findings and
their resolutions:

| # | Finding | Verdict | Fix |
|---|---------|---------|-----|
| 1 | `Hash.new(0)` produced a `RubyObject`, not a `RubyHash`, so `h[k]` crashed | real | Added `Hash.new(default)`/`Hash.new{block}`, `Array.new(n, v)`/`Array.new(n){}`, `String.new(s)` (builtin classes had no `new`/allocator, so `Class#new` fell back to a bare object) |
| 2 | `String#%` and `Kernel#format`/`sprintf` were undefined | real | Added `FormatHelper.Sprintf` (d/i/u/f/e/g/x/X/o/b/s/c with flags `-+ 0#`, width, precision) shared by `String#%` and `format`/`sprintf` |
| 3 | `String#each_char` undefined | real | Added (codepoint iteration) |
| 4 | `Array#<<`/`push` on a frozen array did not raise `FrozenError` | real | Added `RubyArray.IsFrozen`, wired `freeze`/`frozen?` for Array/Hash, and a `CheckFrozen` guard on the array mutators |
| 5 | `each_slice` (and other Enumerator-returning forms) without a block | known limitation | Documented — blockless Enumerator forms raise `NotImplementedError`; on the roadmap |

Verified after fixes: floor division / modulo signs, `divmod`, `sort`/`sort_by`,
`Hash.new(0)` counting, flatten, `reduce(:*)`, `merge` with a block, frozen-array
mutation, and the full `sprintf` directive set — all match ruby 4.0.2.

Areas reviewed without findings: dispatch-cache invalidation (global version
counter; redefinition picked up correctly), unwind exceptions (break/next/return
target the right frame; lambdas catch their own), BigInteger promotion
(`Norm` returns `long` when it fits — the conditional-type pitfall was caught
during W1), and the libprism P/Invoke buffer (init/free in try/finally).

300 unit tests and 13/13 differential snippets remain green.

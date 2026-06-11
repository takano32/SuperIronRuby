#!/usr/bin/env bash
# Generates Prism.dump binary fixtures used by the loader smoke tests.
set -euo pipefail
OUT="$(dirname "$0")/../Tests/SuperIronRuby.Tests/TestData"
mkdir -p "$OUT"
gen() { ruby --disable-gems -rprism -e 'STDOUT.binmode; print Prism.dump(ARGV[0])' "$2" > "$OUT/$1.bin"; }
gen add        '1 + 2'
gen def_method 'def foo(a); a; end'
gen str_interp '"a#{b}c"'
gen klass      'class Foo < Bar; end'
gen bignum     '100000000000000000000'
gen float_lit  '3.14'
echo "wrote fixtures to $OUT"

#!/usr/bin/env bash
# Smoke test for the `sir` CLI: builds it once and checks a few invocations.
set -uo pipefail
cd "$(dirname "$0")/.."

flock /tmp/sir-build.lock dotnet build Src/SuperIronRuby.Console -c Release >/dev/null 2>&1 || {
  echo "build failed"; exit 1; }
SIR="Src/SuperIronRuby.Console/bin/Release/net10.0/sir"

fail=0
check() { # name expected actual
  if [ "$2" = "$3" ]; then echo "ok   $1"; else echo "FAIL $1: expected [$2] got [$3]"; fail=1; fi
}

check "version"      "SuperIronRuby 0.1.0 (Ruby 4.0 target) [.NET 10]" "$($SIR --version)"
check "hello"        "hello"        "$($SIR -e 'puts "hello"')"
check "arithmetic"   "2"           "$($SIR -e 'puts 1 + 1')"
check "bignum"       "1267650600228229401496703205376" "$($SIR -e 'puts 2 ** 100')"
check "string"       "ABC"         "$($SIR -e 'puts "abc".upcase')"
check "inspect"      '[1, 2, 3]'   "$($SIR -e 'p [1, 2, 3]')"

# raise -> stderr + exit 1
out=$($SIR -e 'raise "boom"' 2>&1 >/dev/null); rc=$?
check "raise-msg"    "boom (RuntimeError)" "$out"
[ "$rc" = "1" ] && echo "ok   raise-exit" || { echo "FAIL raise-exit: got $rc"; fail=1; }

# file execution
tmp=$(mktemp --suffix=.rb)
printf 'x = 21\nputs x * 2\n' > "$tmp"
check "file"         "42"          "$($SIR "$tmp")"
rm -f "$tmp"

exit $fail

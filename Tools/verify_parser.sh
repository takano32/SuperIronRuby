#!/usr/bin/env bash
# Verifies SuperIronRuby's Prism binary deserializer against ruby's own Prism by
# diffing AST dumps over the prism fixture corpus.
#
#   Tools/verify_parser.sh [GLOB]
#
# Requires Vendor/prism-src (git clone --depth 1 --branch v1.8.1
# https://github.com/ruby/prism Vendor/prism-src). Excludes listed in
# Tools/verify_parser_exclude.txt (path + reason) are skipped.
set -uo pipefail
cd "$(dirname "$0")/.."

FIX_DIR="Vendor/prism-src/test/prism/fixtures"
if [ ! -d "$FIX_DIR" ]; then
  echo "fixtures not found; clone with:"
  echo "  git clone --depth 1 --branch v1.8.1 https://github.com/ruby/prism Vendor/prism-src"
  exit 2
fi

flock /tmp/sir-build.lock dotnet build Tools/AstDump -c Release >/dev/null 2>&1 || { echo "build failed"; exit 1; }
ASTDUMP="Tools/AstDump/bin/Release/net10.0/AstDump"

EXCLUDE="Tools/verify_parser_exclude.txt"
is_excluded() { [ -f "$EXCLUDE" ] && grep -qF "$1" "$EXCLUDE"; }

glob="${1:-**/*.txt}"
pass=0; fail=0; excl=0
fails=()

while IFS= read -r -d '' f; do
  rel="${f#"$FIX_DIR"/}"
  if is_excluded "$rel"; then excl=$((excl+1)); continue; fi
  if diff -q <(ruby --disable-gems Tools/ast_dump.rb "$f" 2>/dev/null) <("$ASTDUMP" "$f" 2>/dev/null) >/dev/null 2>&1; then
    pass=$((pass+1))
  else
    fail=$((fail+1)); fails+=("$rel")
  fi
done < <(find "$FIX_DIR" -name '*.txt' -print0 | sort -z)

echo "== parser fixture verification =="
echo "  PASS $pass / $((pass+fail))   (excluded: $excl)"
if [ "$fail" -gt 0 ]; then
  printf '  FAIL %s\n' "${fails[@]:0:25}"
  [ "${#fails[@]}" -gt 25 ] && echo "  ... and $((${#fails[@]}-25)) more"
fi
[ "$fail" -eq 0 ]

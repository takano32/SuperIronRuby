#!/usr/bin/env bash
#
# run_diff.sh — differential test harness for SuperIronRuby.
#
# Runs every Tests/diff/snippets/*.rb under both the reference Ruby (MRI 4.0.2,
# the `ruby` on PATH) and `sir` (SuperIronRuby's CLI), then diffs their stdout.
# A snippet PASSES only when both interpreters exit 0 AND produce identical
# stdout. See Docs/CONTRACTS.md ("Differential test contract", "CLI contract")
# and Tests/diff/snippets/README.md for the snippet authoring rules.
#
# Usage:
#   Tests/diff/run_diff.sh [options] [snippet ...]
#
# Options:
#   --no-build        Do not (re)build sir; use a prebuilt binary as-is. If no
#                     usable sir is found, sir runs are reported as ERROR rather
#                     than the harness failing to start.
#   --build           Force `dotnet build` of the console before running.
#                     (Default is: build only if no prebuilt sir is found.)
#   --update          Refresh the recorded MRI expectation for each snippet
#                     (writes results/<name>.expected) and skip the sir/diff
#                     step. Use when adding or changing snippets.
#   --keep-going      Run all snippets even after a failure (default).
#   --fail-fast       Stop at the first failing snippet.
#   --list            List the snippets that would run, then exit.
#   --ruby CMD        Override the reference ruby command (default: ruby).
#   --sir CMD         Override the sir command (default: autodetected).
#   --timeout SEC     Per-process wall-clock timeout (default: 30).
#   -v, --verbose     Print the diff for every failing snippet.
#   -h, --help        Show this help and exit.
#
# Positional args, if given, restrict the run to matching snippets. Each arg is
# matched against the snippet's basename (with or without the .rb suffix), e.g.
# `run_diff.sh 001 hello` runs 001_*.rb and *hello*.rb.
#
# Environment:
#   SIR               Same as --sir (lower precedence than the flag).
#   RUBY              Same as --ruby (lower precedence than the flag).
#   SIR_BUILD_CONFIG  dotnet build/run configuration (default: Debug).
#
# Exit status:
#   0  every selected snippet passed
#   1  at least one snippet FAILED (output mismatch or sir error) or ERRORed
#   2  bad usage / environment problem (no ruby, no snippets, etc.)

set -u

# --------------------------------------------------------------------------
# Locations
# --------------------------------------------------------------------------
SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
DIFF_DIR=$SCRIPT_DIR
SNIPPETS_DIR=$DIFF_DIR/snippets
RESULTS_DIR=$DIFF_DIR/results
# Repo root is two levels up from Tests/diff.
REPO_ROOT=$(cd -- "$DIFF_DIR/../.." && pwd)
CONSOLE_PROJECT=$REPO_ROOT/Src/SuperIronRuby.Console/SuperIronRuby.Console.csproj

# --------------------------------------------------------------------------
# Defaults / option state
# --------------------------------------------------------------------------
RUBY_CMD=${RUBY:-ruby}
SIR_CMD=${SIR:-}
BUILD_CONFIG=${SIR_BUILD_CONFIG:-Debug}
NO_BUILD=0
FORCE_BUILD=0
UPDATE=0
FAIL_FAST=0
LIST_ONLY=0
VERBOSE=0
TIMEOUT_SECS=30
declare -a FILTERS=()

# --------------------------------------------------------------------------
# Pretty output (only colorize when stdout is a tty)
# --------------------------------------------------------------------------
if [ -t 1 ]; then
    C_RED=$(printf '\033[31m'); C_GRN=$(printf '\033[32m')
    C_YEL=$(printf '\033[33m'); C_DIM=$(printf '\033[2m'); C_OFF=$(printf '\033[0m')
else
    C_RED=; C_GRN=; C_YEL=; C_DIM=; C_OFF=
fi

usage() {
    # Print the leading comment block (sans shebang) as help text.
    sed -n '3,/^$/{/^#/p}' "$0" | sed 's/^# \{0,1\}//'
}

die() {
    printf '%srun_diff: %s%s\n' "$C_RED" "$*" "$C_OFF" >&2
    exit 2
}

# --------------------------------------------------------------------------
# Argument parsing
# --------------------------------------------------------------------------
while [ $# -gt 0 ]; do
    case $1 in
        --no-build)    NO_BUILD=1 ;;
        --build)       FORCE_BUILD=1 ;;
        --update)      UPDATE=1 ;;
        --keep-going)  FAIL_FAST=0 ;;
        --fail-fast)   FAIL_FAST=1 ;;
        --list)        LIST_ONLY=1 ;;
        --ruby)        shift; [ $# -gt 0 ] || die "--ruby needs an argument"; RUBY_CMD=$1 ;;
        --ruby=*)      RUBY_CMD=${1#*=} ;;
        --sir)         shift; [ $# -gt 0 ] || die "--sir needs an argument"; SIR_CMD=$1 ;;
        --sir=*)       SIR_CMD=${1#*=} ;;
        --timeout)     shift; [ $# -gt 0 ] || die "--timeout needs an argument"; TIMEOUT_SECS=$1 ;;
        --timeout=*)   TIMEOUT_SECS=${1#*=} ;;
        -v|--verbose)  VERBOSE=1 ;;
        -h|--help)     usage; exit 0 ;;
        --)            shift; while [ $# -gt 0 ]; do FILTERS+=("$1"); shift; done; break ;;
        -*)            die "unknown option: $1 (try --help)" ;;
        *)             FILTERS+=("$1") ;;
    esac
    shift
done

[ "$FORCE_BUILD" = 1 ] && [ "$NO_BUILD" = 1 ] && die "--build and --no-build are mutually exclusive"

# --------------------------------------------------------------------------
# Run a command with a timeout, capturing stdout/exit. Returns the command's
# exit code, or 124 on timeout (matching coreutils `timeout`). stdout -> $2.
# --------------------------------------------------------------------------
run_capture() {
    # $1 = path to write stdout, remaining args = command + argv
    local out=$1; shift
    if command -v timeout >/dev/null 2>&1; then
        timeout "${TIMEOUT_SECS}s" "$@" >"$out" 2>/dev/null
        return $?
    fi
    # Fallback when coreutils timeout is unavailable: run unbounded.
    "$@" >"$out" 2>/dev/null
}

# --------------------------------------------------------------------------
# Reference ruby must exist and work, always.
# --------------------------------------------------------------------------
if ! command -v "${RUBY_CMD%% *}" >/dev/null 2>&1; then
    die "reference ruby '$RUBY_CMD' not found on PATH"
fi
RUBY_VERSION=$("$RUBY_CMD" --version 2>/dev/null || echo "unknown")

# --------------------------------------------------------------------------
# Resolve the sir command. Strategy (first hit wins):
#   1. --sir / $SIR explicit override (used verbatim).
#   2. a `sir` executable on PATH.
#   3. a prebuilt console binary under the project's bin/.../net10.0/.
#   4. `dotnet run --project ... --no-build` if a prebuilt binary exists.
#   5. (only when building is allowed) `dotnet build` then run.
# SIR_RESOLVED is set to the argv (as a string) we will invoke, or empty if
# sir is unavailable — in which case sir runs are reported as ERROR.
# --------------------------------------------------------------------------
declare -a SIR_ARGV=()
SIR_NOTE=""

resolve_sir() {
    # 1. explicit override
    if [ -n "$SIR_CMD" ]; then
        # shellcheck disable=SC2206
        SIR_ARGV=($SIR_CMD)
        SIR_NOTE="explicit (--sir/\$SIR)"
        return 0
    fi

    # 2. sir on PATH
    if command -v sir >/dev/null 2>&1; then
        SIR_ARGV=(sir)
        SIR_NOTE="sir on PATH"
        return 0
    fi

    # 3. prebuilt console binary (apphost) under bin/<config>/net10.0/
    local bindir="$REPO_ROOT/Src/SuperIronRuby.Console/bin/$BUILD_CONFIG/net10.0"
    local apphost="$bindir/SuperIronRuby.Console"
    local dll="$bindir/SuperIronRuby.Console.dll"
    if [ -x "$apphost" ]; then
        SIR_ARGV=("$apphost")
        SIR_NOTE="prebuilt binary ($BUILD_CONFIG)"
        return 0
    fi
    if [ -f "$dll" ] && command -v dotnet >/dev/null 2>&1; then
        SIR_ARGV=(dotnet "$dll")
        SIR_NOTE="prebuilt dll via dotnet ($BUILD_CONFIG)"
        return 0
    fi

    # 4./5. dotnet run, building only if explicitly allowed.
    if command -v dotnet >/dev/null 2>&1 && [ -f "$CONSOLE_PROJECT" ]; then
        if [ "$NO_BUILD" = 1 ]; then
            # No prebuilt binary and we are forbidden to build.
            return 1
        fi
        # Build is permitted (default or --build): use dotnet run.
        SIR_ARGV=(dotnet run --configuration "$BUILD_CONFIG" --project "$CONSOLE_PROJECT" --)
        SIR_NOTE="dotnet run ($BUILD_CONFIG)"
        return 0
    fi

    return 1
}

if resolve_sir; then
    SIR_AVAILABLE=1
else
    SIR_AVAILABLE=0
fi

# Optional forced build (only when not --no-build and dotnet is present).
if [ "$FORCE_BUILD" = 1 ]; then
    if command -v dotnet >/dev/null 2>&1; then
        printf '%s==> dotnet build (%s)%s\n' "$C_DIM" "$BUILD_CONFIG" "$C_OFF"
        if dotnet build --configuration "$BUILD_CONFIG" "$CONSOLE_PROJECT"; then
            resolve_sir && SIR_AVAILABLE=1
        else
            printf '%srun_diff: dotnet build failed; sir runs will ERROR%s\n' "$C_YEL" "$C_OFF" >&2
            SIR_AVAILABLE=0
        fi
    else
        die "--build requested but dotnet not found"
    fi
fi

# --------------------------------------------------------------------------
# Collect snippets.
# --------------------------------------------------------------------------
[ -d "$SNIPPETS_DIR" ] || die "snippets directory not found: $SNIPPETS_DIR"

declare -a ALL_SNIPPETS=()
while IFS= read -r f; do
    ALL_SNIPPETS+=("$f")
done < <(find "$SNIPPETS_DIR" -maxdepth 1 -type f -name '*.rb' | LC_ALL=C sort)

snippet_matches() {
    # $1 = snippet path; uses global FILTERS. No filters => match all.
    [ "${#FILTERS[@]}" -eq 0 ] && return 0
    local base; base=$(basename -- "$1" .rb)
    local fname; fname=$(basename -- "$1")
    local pat
    for pat in "${FILTERS[@]}"; do
        case $base in *"$pat"*) return 0 ;; esac
        case $fname in *"$pat"*) return 0 ;; esac
    done
    return 1
}

declare -a SNIPPETS=()
for s in "${ALL_SNIPPETS[@]}"; do
    snippet_matches "$s" && SNIPPETS+=("$s")
done

if [ "${#SNIPPETS[@]}" -eq 0 ]; then
    if [ "${#ALL_SNIPPETS[@]}" -eq 0 ]; then
        printf '%srun_diff: no snippets in %s (nothing to do)%s\n' "$C_YEL" "$SNIPPETS_DIR" "$C_OFF" >&2
        # An empty corpus is not a failure — the harness itself works.
        exit 0
    fi
    die "no snippets match: ${FILTERS[*]}"
fi

if [ "$LIST_ONLY" = 1 ]; then
    for s in "${SNIPPETS[@]}"; do basename -- "$s"; done
    exit 0
fi

# --------------------------------------------------------------------------
# Header
# --------------------------------------------------------------------------
mkdir -p "$RESULTS_DIR"
printf '%s== SuperIronRuby differential tests ==%s\n' "$C_DIM" "$C_OFF"
printf '%s   ruby : %s%s\n' "$C_DIM" "$RUBY_VERSION" "$C_OFF"
if [ "$SIR_AVAILABLE" = 1 ]; then
    printf '%s   sir  : %s [%s]%s\n' "$C_DIM" "${SIR_ARGV[*]}" "$SIR_NOTE" "$C_OFF"
else
    if [ "$NO_BUILD" = 1 ]; then
        printf '%s   sir  : UNAVAILABLE (no prebuilt binary; --no-build forbids building)%s\n' "$C_YEL" "$C_OFF"
    else
        printf '%s   sir  : UNAVAILABLE (could not locate or build sir)%s\n' "$C_YEL" "$C_OFF"
    fi
fi
printf '%s   snippets: %d   results: %s%s\n\n' "$C_DIM" "${#SNIPPETS[@]}" "$RESULTS_DIR" "$C_OFF"

# --------------------------------------------------------------------------
# Run loop
# --------------------------------------------------------------------------
pass=0; fail=0; err=0; skip=0
declare -a FAILED_NAMES=()

for snippet in "${SNIPPETS[@]}"; do
    name=$(basename -- "$snippet" .rb)
    ruby_out=$RESULTS_DIR/$name.ruby.out
    sir_out=$RESULTS_DIR/$name.sir.out
    expected=$RESULTS_DIR/$name.expected
    diff_out=$RESULTS_DIR/$name.diff

    # --- reference ruby ---
    run_capture "$ruby_out" "$RUBY_CMD" "$snippet"
    ruby_rc=$?

    if [ "$ruby_rc" -ne 0 ]; then
        # The reference itself failed: this is a broken snippet, not a sir bug.
        if [ "$ruby_rc" -eq 124 ]; then
            reason="ruby timed out (>${TIMEOUT_SECS}s)"
        else
            reason="ruby exited $ruby_rc"
        fi
        printf '%s ERROR%s %-28s %s%s%s\n' "$C_YEL" "$C_OFF" "$name" "$C_DIM" "$reason" "$C_OFF"
        err=$((err + 1))
        FAILED_NAMES+=("$name")
        [ "$FAIL_FAST" = 1 ] && break
        continue
    fi
    cp -f "$ruby_out" "$expected"

    # --update: record expectation only, no sir/diff.
    if [ "$UPDATE" = 1 ]; then
        printf '%s UPDATE%s %-28s %srecorded %d bytes%s\n' \
            "$C_GRN" "$C_OFF" "$name" "$C_DIM" "$(wc -c <"$expected")" "$C_OFF"
        pass=$((pass + 1))
        continue
    fi

    # --- sir ---
    if [ "$SIR_AVAILABLE" = 0 ]; then
        printf '%s SKIP %s %-28s %ssir unavailable%s\n' "$C_YEL" "$C_OFF" "$name" "$C_DIM" "$C_OFF"
        : >"$sir_out"
        skip=$((skip + 1))
        continue
    fi

    run_capture "$sir_out" "${SIR_ARGV[@]}" "$snippet"
    sir_rc=$?

    if [ "$sir_rc" -ne 0 ]; then
        if [ "$sir_rc" -eq 124 ]; then
            reason="sir timed out (>${TIMEOUT_SECS}s)"
        else
            reason="sir exited $sir_rc"
        fi
        printf '%s FAIL %s %-28s %s%s%s\n' "$C_RED" "$C_OFF" "$name" "$C_DIM" "$reason" "$C_OFF"
        fail=$((fail + 1))
        FAILED_NAMES+=("$name")
        [ "$FAIL_FAST" = 1 ] && break
        continue
    fi

    # --- compare stdout ---
    if diff -u "$expected" "$sir_out" >"$diff_out" 2>/dev/null; then
        printf '%s PASS %s %-28s\n' "$C_GRN" "$C_OFF" "$name"
        rm -f "$diff_out"
        pass=$((pass + 1))
    else
        printf '%s FAIL %s %-28s %sstdout mismatch (results/%s.diff)%s\n' \
            "$C_RED" "$C_OFF" "$name" "$C_DIM" "$name" "$C_OFF"
        fail=$((fail + 1))
        FAILED_NAMES+=("$name")
        if [ "$VERBOSE" = 1 ]; then
            sed 's/^/    /' "$diff_out"
        fi
        [ "$FAIL_FAST" = 1 ] && break
    fi
done

# --------------------------------------------------------------------------
# Summary
# --------------------------------------------------------------------------
total=$((pass + fail + err + skip))
printf '\n%s== summary ==%s\n' "$C_DIM" "$C_OFF"
printf '  %s%d passed%s' "$C_GRN" "$pass" "$C_OFF"
printf ', %s%d failed%s' "$C_RED" "$fail" "$C_OFF"
printf ', %s%d errored%s' "$C_YEL" "$err" "$C_OFF"
printf ', %s%d skipped%s' "$C_YEL" "$skip" "$C_OFF"
printf '  (of %d)\n' "$total"

if [ "${#FAILED_NAMES[@]}" -gt 0 ]; then
    printf '  affected: %s\n' "${FAILED_NAMES[*]}"
fi

# Exit policy:
#   - Output mismatches and sir errors (FAIL) -> non-zero, the point of the run.
#   - Broken snippets (ERROR, ruby itself failed) -> non-zero, must be fixed.
#   - SKIP (sir unavailable) alone -> exit 0: the harness ran correctly even
#     though sir does not exist yet. This is the expected state pre-`sir`.
if [ "$fail" -gt 0 ] || [ "$err" -gt 0 ]; then
    exit 1
fi
exit 0

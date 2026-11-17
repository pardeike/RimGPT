#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
mkdir -p .builds
log="$(mktemp "$root/.builds/build.log.XXXXXX")"
if python3 scripts/workflow.py "$@" >"$log" 2>&1; then
	ln -sfn "$(basename "$log")" "$root/.builds/build.log"
	printf 'ok\n'
else
	result=$?
	ln -sfn "$(basename "$log")" "$root/.builds/build.log"
	printf 'Build, package or install failed (exit %s). Log: %s\n' "$result" "$log"
	tail -30 "$log"
	exit "$result"
fi

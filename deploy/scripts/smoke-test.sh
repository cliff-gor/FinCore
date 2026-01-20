#!/usr/bin/env bash
# FinCore smoke test: checks the API is ready and reports its version.
#
# Usage:
#   BASE_URL=https://localhost:7100 deploy/scripts/smoke-test.sh
#   deploy/scripts/smoke-test.sh https://api.staging.example.com [expected-commit-sha]
#
# Env:
#   BASE_URL         API base URL (or first argument). Default: https://localhost:7100
#   EXPECTED_COMMIT  Optional; fail if /api/version.commit does not start with it (or second argument)
#   INSECURE=1       Pass -k to curl (local dev certificates)
#   RETRIES / DELAY  Readiness retries (default 30) and seconds between them (default 5)
set -euo pipefail

BASE_URL="${1:-${BASE_URL:-https://localhost:7100}}"
BASE_URL="${BASE_URL%/}"
EXPECTED_COMMIT="${2:-${EXPECTED_COMMIT:-}}"
RETRIES="${RETRIES:-30}"
DELAY="${DELAY:-5}"

curl_opts=(--silent --show-error --fail --max-time 10)
if [[ "${INSECURE:-0}" == "1" ]]; then curl_opts+=(-k); fi

echo "Smoke testing ${BASE_URL}"

echo "-> GET /health/ready"
attempt=1
until curl "${curl_opts[@]}" -o /dev/null "${BASE_URL}/health/ready"; do
  if (( attempt >= RETRIES )); then
    echo "FAIL: /health/ready not healthy after ${RETRIES} attempts" >&2
    exit 1
  fi
  echo "   not ready yet (attempt ${attempt}/${RETRIES}); retrying in ${DELAY}s"
  attempt=$((attempt + 1))
  sleep "${DELAY}"
done
echo "   OK"

echo "-> GET /api/version"
version_json="$(curl "${curl_opts[@]}" "${BASE_URL}/api/version")"
echo "   ${version_json}"

if [[ -n "${EXPECTED_COMMIT}" ]]; then
  if command -v jq >/dev/null 2>&1; then
    commit="$(jq -r '.commit // empty' <<<"${version_json}")"
  else
    commit="$(sed -n 's/.*"commit"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' <<<"${version_json}")"
  fi
  # Either side may be a short SHA, so a prefix match in either direction counts.
  if [[ -z "${commit}" || ( "${EXPECTED_COMMIT}" != "${commit}"* && "${commit}" != "${EXPECTED_COMMIT}"* ) ]]; then
    echo "FAIL: deployed commit '${commit}' does not match expected '${EXPECTED_COMMIT}'" >&2
    exit 1
  fi
  echo "   commit matches ${EXPECTED_COMMIT}"
fi

echo "Smoke test passed."

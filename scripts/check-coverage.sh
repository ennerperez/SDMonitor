#!/usr/bin/env bash
set -euo pipefail

coverage_file="${1:-artifacts/coverage/cobertura.xml}"
minimum_coverage="${MINIMUM_COVERAGE:-85}"
package_name="${COVERAGE_PACKAGE_NAME:-SDMonitor}"
enforce_threshold="${ENFORCE_COVERAGE_THRESHOLD:-true}"

if [[ ! -f "$coverage_file" ]]; then
  echo "Coverage report not found: $coverage_file" >&2
  exit 1
fi

package_line="$(sed -n "s/.*<package line-rate=\"\\([^\"]*\\)\" branch-rate=\"\\([^\"]*\\)\".* name=\"$package_name\".*/\\1 \\2/p" "$coverage_file" | head -n 1)"
if [[ -z "$package_line" ]]; then
  echo "Coverage package not found: $package_name" >&2
  exit 1
fi

read -r line_rate branch_rate <<< "$package_line"
coverage_percent="$(awk -v rate="$line_rate" 'BEGIN { printf "%.2f", rate * 100 }')"
branch_percent="$(awk -v rate="$branch_rate" 'BEGIN { printf "%.2f", rate * 100 }')"

mkdir -p artifacts/coverage
cat > artifacts/coverage/coverage.env <<EOF
COVERAGE_PERCENT=$coverage_percent
BRANCH_COVERAGE_PERCENT=$branch_percent
MINIMUM_COVERAGE=$minimum_coverage
COVERAGE_PACKAGE_NAME=$package_name
EOF

echo "COVERAGE: ${coverage_percent}%"
echo "BRANCH_COVERAGE: ${branch_percent}%"
echo "MINIMUM_COVERAGE: ${minimum_coverage}%"

if [[ "$enforce_threshold" == "true" ]]; then
  awk -v coverage="$coverage_percent" -v minimum="$minimum_coverage" 'BEGIN { exit coverage + 0 >= minimum + 0 ? 0 : 1 }' || {
    echo "Coverage ${coverage_percent}% is below required ${minimum_coverage}%." >&2
    exit 1
  }
fi

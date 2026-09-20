#!/bin/sh
set -eu

if [ -z "${CI_MERGE_REQUEST_IID:-}" ]; then
  echo "Not a merge request pipeline. Skipping coverage comment."
  exit 0
fi

if [ -z "${GITLAB_API_TOKEN:-}" ]; then
  echo "GITLAB_API_TOKEN is required to post merge request coverage comments." >&2
  exit 1
fi

if [ -z "${COVERAGE_PERCENT:-}" ] || [ -z "${MINIMUM_COVERAGE:-}" ]; then
  echo "Coverage environment variables are missing." >&2
  exit 1
fi

status="passed"
if awk -v coverage="$COVERAGE_PERCENT" -v minimum="$MINIMUM_COVERAGE" 'BEGIN { exit coverage + 0 >= minimum + 0 ? 0 : 1 }'; then
  status="passed"
else
  status="failed"
fi

body="Coverage ${status}: ${COVERAGE_PERCENT}% line coverage, minimum ${MINIMUM_COVERAGE}%.

Pipeline: ${CI_PIPELINE_URL}
Job: ${CI_JOB_URL}"

curl --fail-with-body \
  --request POST \
  --header "PRIVATE-TOKEN: ${GITLAB_API_TOKEN}" \
  --data-urlencode "body=${body}" \
  "${CI_API_V4_URL}/projects/${CI_PROJECT_ID}/merge_requests/${CI_MERGE_REQUEST_IID}/notes"

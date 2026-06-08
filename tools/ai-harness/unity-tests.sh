#!/usr/bin/env bash

set -Eeuo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

echo "Unity Test Runner"
echo "================="

if [[ -z "${UNITY_EXECUTABLE:-}" ]]; then
    echo "ERROR: UNITY_EXECUTABLE is not set."
    echo ""
    echo "Set it like this:"
    echo "export UNITY_EXECUTABLE=\"/path/to/Unity\""
    echo ""
    echo "Example:"
    echo "export UNITY_EXECUTABLE=\"$HOME/Unity/Hub/Editor/6000.3.6f1/Editor/Unity\""
    exit 1
fi

if [[ ! -f "$UNITY_EXECUTABLE" ]]; then
    echo "ERROR: Unity executable not found:"
    echo "$UNITY_EXECUTABLE"
    exit 1
fi

RESULTS_PATH="$PROJECT_ROOT/TestResults.xml"

echo ""
echo "Project:"
echo "$PROJECT_ROOT"

echo ""
echo "Unity executable:"
echo "$UNITY_EXECUTABLE"

echo ""
echo "Running EditMode tests..."

"$UNITY_EXECUTABLE"     -batchmode     -projectPath "$PROJECT_ROOT"     -runTests     -testPlatform EditMode     -testResults "$RESULTS_PATH"     -quit

echo ""
echo "Test results saved to:"
echo "$RESULTS_PATH"

echo ""
echo "Unity tests finished."

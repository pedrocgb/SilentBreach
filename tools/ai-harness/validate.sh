#!/usr/bin/env bash

set -Eeuo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

SCRIPT_ROOT="${SCRIPT_ROOT:-Assets/Scripts}"

echo "AI Harness Validation"
echo "====================="

echo ""
echo "Project root:"
echo "$PROJECT_ROOT"

echo ""
echo "Validation target:"
echo "$SCRIPT_ROOT"

if [[ ! -d "$SCRIPT_ROOT" ]]; then
    echo ""
    echo "WARNING: $SCRIPT_ROOT does not exist."
    echo "Falling back to Assets while excluding known third-party folders."
    SCRIPT_ROOT="Assets"
fi

COMMON_GREP_EXCLUDES=(
    --exclude-dir="AstarPathfindingProject"
    --exclude-dir="Demigiant"
    --exclude-dir="DOTween"
    --exclude-dir="Plugins"
    --exclude-dir="Rewired"
    --exclude-dir="Sirenix"
    --exclude-dir="ThirdParty"
    --exclude-dir="ExternalDependencyManager"
    --exclude-dir="TextMesh Pro"
)

echo ""
echo "Checking git status..."
git status --short

echo ""
echo "Checking for merge conflict markers..."
if grep -RInE "^(<<<<<<< .+|=======|>>>>>>> .+)$" "$SCRIPT_ROOT"     --include="*.cs"     --include="*.asmdef"     --include="*.json"     --include="*.md"     "${COMMON_GREP_EXCLUDES[@]}"     >/tmp/ai_harness_conflicts.txt; then

    cat /tmp/ai_harness_conflicts.txt
    echo ""
    echo "ERROR: Merge conflict markers found."
    exit 1
else
    echo "OK: No merge conflict markers found."
fi

echo ""
echo "Checking for direct Unity old input usage..."
if grep -RInE "Input\.GetKey|Input\.GetAxis|Input\.GetButton|Input\.GetMouseButton|UnityEngine\.Input" "$SCRIPT_ROOT"     --include="*.cs"     "${COMMON_GREP_EXCLUDES[@]}"     >/tmp/ai_harness_input.txt; then

    cat /tmp/ai_harness_input.txt
    echo ""
    echo "ERROR: Direct Unity Input usage found. Use Rewired instead."
    exit 1
else
    echo "OK: No direct Unity old input usage found."
fi

echo ""
echo "Checking for scene/prefab modifications in git diff..."
if git diff --name-only | grep -E "\.unity$|\.prefab$" >/tmp/ai_harness_scene_changes.txt; then
    cat /tmp/ai_harness_scene_changes.txt
    echo ""
    echo "ERROR: Scene or prefab files were modified. This harness forbids that unless explicitly requested."
    exit 1
else
    echo "OK: No scene or prefab changes detected."
fi

echo ""
echo "Checking for risky runtime object searches..."
if grep -RInE "FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|GameObject\.Find|FindGameObjectWithTag|FindGameObjectsWithTag" "$SCRIPT_ROOT"     --include="*.cs"     "${COMMON_GREP_EXCLUDES[@]}"     >/tmp/ai_harness_finds.txt; then

    cat /tmp/ai_harness_finds.txt
    echo ""
    echo "ERROR: Risky object search API found. Avoid runtime scene searches."
    exit 1
else
    echo "OK: No risky object search APIs found."
fi

echo ""
echo "Checking for obsolete markers in source..."
if grep -RInE "\[Obsolete|System\.Obsolete" "$SCRIPT_ROOT"     --include="*.cs"     "${COMMON_GREP_EXCLUDES[@]}"     >/tmp/ai_harness_obsolete_markers.txt; then

    cat /tmp/ai_harness_obsolete_markers.txt
    echo ""
    echo "WARNING: Obsolete markers found. Review whether this is intentional."
else
    echo "OK: No obsolete markers found in source."
fi

echo ""
echo "Checking for TODO/FIXME notes..."
if grep -RInE "TODO|FIXME" "$SCRIPT_ROOT"     --include="*.cs"     "${COMMON_GREP_EXCLUDES[@]}"     >/tmp/ai_harness_todos.txt; then

    cat /tmp/ai_harness_todos.txt
    echo ""
    echo "WARNING: TODO/FIXME notes found. Review before finishing."
else
    echo "OK: No TODO/FIXME notes found."
fi

echo ""
echo "Checking latest Unity editor log for compiler warnings/errors..."

UNITY_LOG_CANDIDATES=(
    "$HOME/.config/unity3d/Editor.log"
    "$HOME/.config/unity3d/Unity/Editor.log"
)

FOUND_LOG=""

for LOG_PATH in "${UNITY_LOG_CANDIDATES[@]}"; do
    if [[ -f "$LOG_PATH" ]]; then
        FOUND_LOG="$LOG_PATH"
        break
    fi
done

if [[ -n "$FOUND_LOG" ]]; then
    echo "Using Unity log:"
    echo "$FOUND_LOG"

    if grep -Ei "warning CS|error CS|obsolete|compilation failed" "$FOUND_LOG" >/tmp/ai_harness_unity_warnings.txt; then
        cat /tmp/ai_harness_unity_warnings.txt
        echo ""
        echo "ERROR: Unity compiler warnings/errors or obsolete API warnings found in editor log."
        echo "Open Unity, let it recompile, fix warnings, then run this script again."
        exit 1
    else
        echo "OK: No compiler warnings/errors found in detected Unity log."
    fi
else
    echo "WARNING: Unity editor log not found. Open Unity once, let it compile, then rerun validation."
fi

echo ""
echo "Validation completed successfully."

x# AGENTS.md

# Silent Breach — Unity AI Coding Harness

This is a Unity 2D top-down stealth/action game.

The AI coding agent must read and follow this file before making any code changes.

```text
tools/ai-harness/rules/
```

---

## Always-Follow Rules

- Do not create, modify, or delete scene objects unless explicitly requested.
- Do not edit Unity scenes or prefabs unless explicitly requested.
- Everything that needs to exist in the scene must be wired manually by the developer.
- Follow S.O.L.I.D. principles.
- Never repeat code.
- Prioritize game performance.
- Use Odin Inspector for code and inspector organization.
- Use Rewired for all player input.
- Use A* Pathfinding Project for AI pathfinding.
- Use DOTween for simple code-driven animations when it is the best performance and maintenance choice.
- Do not serialize components that are on the same GameObject.
- Do not serialize persistent/global objects directly.
- Cache same-object components in `Awake()`.
- Add XML summary comments to all methods in touched files.
- Check for obsolete APIs.
- Remove all new warnings before finishing.

---

## RTK command usage

When running shell commands that may produce long output, prefer RTK.

Use the full path to avoid PATH issues in Codex:

- Use `/home/pedrohcg/.local/bin/rtk git status` instead of `git status`
- Use `/home/pedrohcg/.local/bin/rtk git diff` instead of `git diff`
- Use `/home/pedrohcg/.local/bin/rtk grep` or `/home/pedrohcg/.local/bin/rtk rg` instead of raw `grep` or `rg`
- Use `/home/pedrohcg/.local/bin/rtk read <file>` instead of dumping large files with `cat`
- Use `/home/pedrohcg/.local/bin/rtk npm test`, `/home/pedrohcg/.local/bin/rtk pytest`, `/home/pedrohcg/.local/bin/rtk dotnet test`, or similar test wrappers when applicable

Use raw commands only when:
- The output is expected to be very small
- RTK changes command behavior
- The command is interactive
- The command needs exact unfiltered output

---

## Required Rule Files

Read the relevant files before editing:

- General Unity rules: `tools/ai-harness/rules/unity.md`
- Architecture / S.O.L.I.D. / DRY: `tools/ai-harness/rules/architecture-solid.md`
- Component references and serialization: `tools/ai-harness/rules/component-references.md`
- Odin Inspector: `tools/ai-harness/rules/odin.md`
- Rewired input: `tools/ai-harness/rules/rewired.md`
- A* Pathfinding Project: `tools/ai-harness/rules/astar-pathfinding.md`
- DOTween: `tools/ai-harness/rules/dotween.md`
- Performance: `tools/ai-harness/rules/performance.md`
- Method comments: `tools/ai-harness/rules/comments.md`
- Naming and file organization: `tools/ai-harness/rules/naming-file-organization.md`
- Validation and warnings: `tools/ai-harness/rules/validation.md`

---

## When to Read Each Rule File

Read these rule files for every coding task:

```text
tools/ai-harness/rules/unity.md
tools/ai-harness/rules/architecture-solid.md
tools/ai-harness/rules/component-references.md
tools/ai-harness/rules/performance.md
tools/ai-harness/rules/comments.md
tools/ai-harness/rules/validation.md
```

Also read task-specific files:

- Input task: read `rewired.md`
- AI movement/pathfinding task: read `astar-pathfinding.md`
- UI, feedback, camera, fade, or tween task: read `dotween.md`
- Inspector-facing MonoBehaviour or ScriptableObject task: read `odin.md`
- Refactor or new architecture task: read `architecture-solid.md`

---

## Implementation Workflow

Before editing code, the agent must:

1. Inspect relevant files.
2. Identify existing project patterns.
3. Check if similar code already exists.
4. Make a short implementation plan.
5. Implement the smallest safe change.
6. Avoid unrelated rewrites.
7. Run validation when possible.
8. List changed files.
9. Explain manual Unity setup.

---

## Validation

After editing, run:

```bash
./tools/ai-harness/validate.sh
```

When Unity tests are needed, run:

```bash
./tools/ai-harness/unity-tests.sh
```

If validation cannot be run, explain why.

---

## Definition of Done

A task is done only when:

- The requested behavior is implemented.
- Code follows the relevant rule files.
- No unrelated systems were rewritten.
- No scene or prefab files were modified unless requested.
- No new warnings remain.
- Obsolete APIs were checked.
- Validation was run when possible.
- Changed files are listed.
- Manual Unity setup is explained.

@RTK.md

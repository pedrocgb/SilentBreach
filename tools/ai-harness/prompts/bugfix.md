# Bugfix Prompt

Read `AGENTS.md` first.

Then read the relevant rule files from `tools/ai-harness/rules/`.

## Bug

[DESCRIBE BUG HERE]

## Expected Behavior

[DESCRIBE EXPECTED BEHAVIOR HERE]

## Required Before Editing

First provide:

1. Relevant files found.
2. Likely root cause.
3. Rule files read.
4. Short fix plan.
5. Risks or assumptions.

## Rules

- Follow `AGENTS.md`.
- Follow the relevant rule files.
- Make the smallest safe fix.
- Do not rewrite unrelated systems.
- Do not create scene objects.
- Do not modify scenes or prefabs.
- Use Rewired for input-related fixes.
- Use A* Pathfinding Project for AI pathfinding fixes.
- Use Odin Inspector for inspector-facing code.
- Add XML summary comments to all methods in touched files.
- Check for obsolete APIs.
- Remove all new warnings before finishing.

## Done When

- Root cause is explained.
- Bug is fixed.
- No unrelated systems were rewritten.
- No new warnings remain.
- Validation was run when possible.
- Changed files are listed.
- Manual Unity setup is explained if needed.

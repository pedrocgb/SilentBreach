# Feature Implementation Prompt

Read `AGENTS.md` first.

Then read the relevant rule files from `tools/ai-harness/rules/`.

## Goal

Implement the following feature:

[DESCRIBE FEATURE HERE]

## Required Before Editing

First provide:

1. Relevant files found.
2. Existing project patterns.
3. Rule files read.
4. Short implementation plan.
5. Risks or assumptions.

Do not edit until the plan is clear.

## Rules

- Follow `AGENTS.md`.
- Follow the relevant rule files.
- Inspect relevant files first.
- Use Odin Inspector for inspector-facing scripts.
- Use Rewired for input-related code.
- Use A* Pathfinding Project for AI pathfinding.
- Use DOTween only when it is the best animation choice.
- Follow S.O.L.I.D.
- Do not repeat code.
- Do not create scene objects.
- Do not modify scenes or prefabs.
- Do not serialize same-object components.
- Do not serialize persistent/global objects.
- Cache same-object components in `Awake()`.
- Add XML summary comments to all methods in touched files.
- Check for obsolete APIs.
- Remove all new warnings before finishing.

## Done When

- Feature works as requested.
- Code follows the relevant rule files.
- No repeated logic was added.
- No obsolete APIs were introduced.
- No new warnings remain.
- Validation was run when possible.
- Changed files are listed.
- Manual Unity setup is explained.

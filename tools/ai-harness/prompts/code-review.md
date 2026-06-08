# Code Review Prompt

Read `AGENTS.md` first.

Then read the relevant rule files from `tools/ai-harness/rules/`.

## Review Target

[DESCRIBE FILES OR SYSTEM HERE]

## Check For

- S.O.L.I.D. violations.
- Repeated code.
- Missing method comments.
- Poor Odin Inspector organization.
- Direct Unity input usage instead of Rewired.
- Serialized same-object components.
- Serialized persistent/global objects.
- Scene object creation.
- Obsolete Unity APIs.
- Runtime object searches.
- Unnecessary Update loops.
- Allocations in hot paths.
- LINQ in hot paths.
- A* Pathfinding misuse.
- DOTween misuse or missing cleanup.
- Possible warnings.
- Risky Unity serialization changes.

## Output Format

Provide:

1. Critical issues.
2. Recommended improvements.
3. Performance concerns.
4. Architecture concerns.
5. Suggested next steps.

Do not edit files unless explicitly asked.

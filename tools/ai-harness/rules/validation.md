# Validation and Warning Rules

Before finishing, remove all warnings introduced by the change.

---

## Validation Commands

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

## Obsolete Code Rules

Always check for obsolete APIs before finishing.

The agent must:

- Avoid deprecated Unity APIs.
- Replace obsolete APIs with current equivalents when possible.
- Mention any obsolete API that could not be replaced safely.
- Remove compiler warnings before finishing.
- Avoid suppressing warnings unless there is a strong reason.

---

## Warning Rules

Before finishing, remove all warnings introduced by the change.

Check for:

- C# compiler warnings.
- Unity obsolete API warnings.
- Unused fields.
- Unused variables.
- Missing namespace warnings.
- Nullability or possible null warnings if enabled.
- Odin Inspector warning attributes where relevant.
- Rewired integration warnings if input code was changed.

Do not finish while new warnings remain.

If an existing warning was already present and unrelated, mention it clearly.

---

## Definition of Done

A task is done only when:

- The requested behavior is implemented.
- Code follows S.O.L.I.D.
- Code avoids repetition.
- Odin Inspector is used for organization where appropriate.
- Rewired is used for input when input is involved.
- A* Pathfinding Project is used for AI pathfinding.
- DOTween is used only when it is the right animation choice.
- No scene objects were created or modified unless requested.
- Same-object components are cached, not serialized.
- Persistent/global objects are not serialized directly.
- All methods in touched files have comments.
- Obsolete APIs were checked.
- New warnings were removed.
- Validation was run when possible.
- Changed files are listed.
- Manual Unity setup is explained.

# Performance Rules

Always prioritize optimization for the game while keeping the code readable and maintainable.

---

## General Rules

- Avoid unnecessary `Update()` loops.
- Use events where appropriate.
- Cache component references.
- Avoid repeated `GetComponent` calls during gameplay.
- Avoid allocations inside `Update`, `FixedUpdate`, and frequently called methods.
- Avoid LINQ in hot paths.
- Avoid `FindObjectOfType`, `FindObjectsOfType`, `FindFirstObjectByType`, `FindAnyObjectByType`, `GameObject.Find`, and tag searches during gameplay.
- Avoid unnecessary physics queries.
- Prefer non-alloc physics APIs where useful.
- Prefer object pooling for frequently spawned objects.
- Keep AI logic efficient.
- Keep pathfinding updates controlled and intentional.
- Do not optimize in a way that makes the code unreadable without a good reason.

---

## AI Performance

For AI systems:

- Avoid per-frame expensive checks for every NPC when intervals or events are enough.
- Stagger expensive updates when many NPCs are active.
- Avoid recalculating paths every frame.
- Update path destinations only when needed.
- Keep detection and hearing checks efficient.
- Avoid full graph rescans during gameplay unless explicitly required.

---

## Physics Performance

For physics systems:

- Use Rigidbody2D-compatible movement for physics objects.
- Avoid moving Rigidbody2D objects by Transform during physics movement.
- Use `FixedUpdate()` for physics movement when appropriate.
- Avoid unnecessary raycasts, overlap checks, or casts.
- Prefer non-alloc versions of physics queries when the query is frequent.

---

## Memory and Allocations

Avoid allocations in hot paths:

- Avoid LINQ in frequent gameplay loops.
- Avoid string concatenation in frequent loops.
- Avoid creating new lists/arrays every frame.
- Reuse buffers where appropriate.
- Pool frequently spawned objects.

---

## DOTween Performance

For DOTween:

- Do not create tweens every frame.
- Kill tweens on disable/destroy when needed.
- Cache reusable tweens only when intentional.
- Avoid tweening large numbers of objects constantly.
- Evaluate whether direct assignment, Animator, or manual interpolation is better.

# A* Pathfinding Project Rules

The project uses the A* Pathfinding Project for AI pathfinding.

---

## Core Rules

- AI movement and navigation must use A* Pathfinding Project systems.
- Do not replace A* Pathfinding with Unity NavMesh unless explicitly requested.
- Do not create a custom pathfinding system unless explicitly requested.
- Do not use Unity `NavMeshAgent` for 2D top-down AI.
- AI movement should remain compatible with Rigidbody2D physics when the character uses physics-based movement.
- Pathfinding logic and movement execution should stay separated when possible.

---

## Preferred Structure

```text
AI State / Decision Logic
        ↓
Path Request / Destination Setter
        ↓
A* Pathfinding Component
        ↓
Rigidbody2D Movement / Rotation Controller
```

---

## Recommended Responsibilities

- AI state scripts decide where the enemy wants to go.
- A* handles path calculation.
- Movement scripts move the enemy along the calculated path.
- Detection, hearing, combat, patrol, and fleeing logic should not be hardcoded directly inside the pathfinding component.

---

## When Working With A*

- Use existing A* components and project patterns when available.
- Cache A* references when they are on the same GameObject.
- Do not serialize same-object A* components.
- Use `[RequireComponent]` when an A* component is mandatory.
- Avoid recalculating paths every frame.
- Use controlled path update intervals.
- Update graphs only when necessary.
- For dynamic obstacles, update only the affected graph area when possible.
- Avoid full graph rescans during gameplay unless explicitly required.
- Keep pathfinding performance suitable for many NPCs.
- Prevent enemies from hugging walls or getting stuck by considering radius, collider size, graph erosion, node size, and movement steering.

---

## Allowed A* Examples

Allowed examples include:

- `Seeker`
- `AIPath`
- `AILerp`
- `AIDestinationSetter`
- `RichAI` only if appropriate for the graph type
- Custom movement that reads paths from `Seeker`

---

## Same-Object A* Component Example

```csharp
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Seeker))]
public sealed class EnemyPathController : MonoBehaviour
{
    private Seeker seeker;

    /// <summary>
    /// Caches the A* Seeker component from this GameObject before gameplay starts.
    /// </summary>
    private void Awake()
    {
        seeker = GetComponent<Seeker>();
    }
}
```

Do not do this for same-object A* components:

```csharp
[SerializeField]
private Seeker seeker;
```

---

## External A* References

Serialize only external or configurable references, such as:

- Patrol points.
- Target transforms.
- Flee destination points.
- ScriptableObject pathfinding settings.
- Layer masks.
- Graph update settings.

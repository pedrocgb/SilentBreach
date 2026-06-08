# Component References and Serialization Rules

These rules control what should and should not be serialized.

---

## Same GameObject Components

Components that exist on the same GameObject must not be serialized.

Do not do this:

```csharp
[SerializeField]
private Rigidbody2D rb;
```

When the component is expected to be on the same GameObject.

Do this instead:

```csharp
private Rigidbody2D rb;
```

Then cache it in `Awake()`:

```csharp
/// <summary>
/// Caches required components from this GameObject.
/// </summary>
private void Awake()
{
    rb = GetComponent<Rigidbody2D>();
}
```

Use `[RequireComponent]` when the component is mandatory:

```csharp
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovementController : MonoBehaviour
{
}
```

---

## Same-Object Examples That Should Usually Be Cached

Do not serialize these when they are expected to exist on the same GameObject:

- `Rigidbody2D`
- `Collider2D`
- `Animator`
- `SpriteRenderer`
- `AudioSource`
- `CanvasGroup`
- A* `Seeker`
- A* `AIPath`
- A* `AILerp`
- A* `AIDestinationSetter`

Cache them in `Awake()`.

---

## Persistent Objects

Persistent/global objects must not be serialized directly in scene components.

Avoid:

```csharp
[SerializeField]
private GameManager gameManager;
```

Prefer:

- Interfaces.
- Dependency injection through an initialization method.
- A controlled service locator only if the project already uses one.
- Events.
- ScriptableObject event channels.
- Explicit runtime registration.

---

## External Scene References

Serialize references only when they are truly scene-specific and must be wired manually.

Allowed examples:

- Patrol points.
- Exit points.
- Scene-specific targets.
- UI text references.
- Audio sources on different objects.
- Prefabs.
- ScriptableObject configs.
- Layer masks.
- Scene-specific transforms.

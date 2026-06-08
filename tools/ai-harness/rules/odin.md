# Odin Inspector Rules

The project uses Odin Inspector for code organization.

All MonoBehaviour and ScriptableObject scripts should use Odin Inspector attributes where helpful.

---

## Use Odin For

Use Odin Inspector for:

- Grouping settings.
- Separating references, settings, debug, runtime data, and events.
- Improving inspector readability.
- Buttons for safe debug/test actions.
- Read-only runtime values.
- Validating required external references.
- Making complex ScriptableObjects easier to configure.

---

## Common Examples

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

[Title("Player Movement")]
public sealed class PlayerMovementController : MonoBehaviour
{
    [TitleGroup("Movement Settings")]
    [SerializeField, MinValue(0f)]
    private float moveSpeed = 5f;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private Vector2 currentVelocity;
}
```

---

## Good Odin Usage

Good examples:

```csharp
[TitleGroup("Movement Settings")]
[SerializeField, MinValue(0f)]
private float moveSpeed = 5f;

[FoldoutGroup("References")]
[Required]
[SerializeField]
private Transform externalTarget;

[FoldoutGroup("Runtime Data")]
[ShowInInspector, ReadOnly]
private bool isMoving;
```

---

## Avoid Bad Odin Usage

Do not overuse Odin attributes when they do not improve clarity.

Avoid:

- Adding many groups for only one or two fields.
- Using Odin to hide confusing architecture.
- Using `[Required]` on same-object components that should be cached instead.
- Serializing dependencies only to make the inspector look complete.

---

## Same-Object Components

Odin should not be used as a reason to serialize same-object components.

Do not do this:

```csharp
[Required]
[SerializeField]
private Rigidbody2D rb;
```

If the `Rigidbody2D` is on the same GameObject.

Use `[RequireComponent]` and cache it in `Awake()` instead.

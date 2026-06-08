# AGENTS.md

# Unity AI Coding Harness

This project is a Unity 2D top-down stealth/action game.

The AI coding agent must read and follow this file before making any code changes.

---

## Core Project Rules

### Unity Project Rules

- This is a Unity project.
- Do not create, modify, or delete scene objects unless explicitly requested.
- Do not edit Unity scenes unless explicitly requested.
- Everything that needs to exist in the scene must be wired manually by the developer.
- Any required scene setup must be explained clearly after implementation.
- Prefer ScriptableObjects for configurable gameplay data.
- Prefer inspector-friendly components, but do not over-serialize dependencies.
- Do not break existing serialized references.
- Do not rename serialized fields unless absolutely necessary.
- Do not move files unless explicitly requested.
- Do not create large manager classes that control unrelated systems.

---

## Required Packages / Frameworks

### Odin Inspector

The project uses Odin Inspector for code organization.

All MonoBehaviour and ScriptableObject scripts should use Odin Inspector attributes where helpful.

Use Odin Inspector for:
- Grouping settings.
- Separating references, settings, debug, runtime data, and events.
- Improving inspector readability.
- Buttons for safe debug/test actions.
- Read-only runtime values.

Common examples:

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

Do not overuse Odin attributes when they do not improve clarity.

---

### Rewired

The project uses Rewired for input.

Rules:
- Do not use Unity's old `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, `Input.GetMouseButton`, etc.
- Do not use Unity's New Input System unless explicitly requested.
- Player input must go through Rewired.
- Input action names must be configurable or clearly documented.
- Never hardcode input behavior deeply inside gameplay logic when it can be abstracted.

Preferred structure:

```text
Input Reader / Input Provider
        ↓
Gameplay Controller
        ↓
Movement / Combat / Interaction logic
```

Gameplay systems should depend on input abstractions where reasonable.

Example:

```csharp
using UnityEngine;

public interface IPlayerInputReader
{
    Vector2 Move { get; }
    bool IsAiming { get; }
    bool WasInteractPressed { get; }
}
```

---

### A* Pathfinding Project

The project uses the A* Pathfinding Project for AI pathfinding.

Rules:
- AI movement and navigation must use A* Pathfinding Project systems.
- Do not replace A* Pathfinding with Unity NavMesh unless explicitly requested.
- Do not create a custom pathfinding system unless explicitly requested.
- Do not use Unity NavMeshAgent for 2D top-down AI.
- AI movement should still be compatible with Rigidbody2D physics when the character uses physics-based movement.
- Pathfinding logic and movement execution should stay separated when possible.

Preferred structure:

```text
AI State / Decision Logic
        ↓
Path Request / Destination Setter
        ↓
A* Pathfinding Component
        ↓
Rigidbody2D Movement / Rotation Controller

Recommended responsibilities:

AI state scripts decide where the enemy wants to go.
A* handles path calculation.
Movement scripts move the enemy along the calculated path.
Detection, hearing, combat, patrol, and fleeing logic should not be hardcoded directly inside the pathfinding component.

When working with A*:

Use existing A* components and project patterns when available.
Cache A* references when they are on the same GameObject.
Do not serialize same-object A* components.
Use [RequireComponent] when an A* component is mandatory.
Avoid recalculating paths every frame.
Use controlled path update intervals.
Update graphs only when necessary.
For dynamic obstacles, update only the affected graph area when possible.
Avoid full graph rescans during gameplay unless explicitly required.
Keep pathfinding performance suitable for many NPCs.
Prevent enemies from hugging walls or getting stuck by considering radius, collider size, graph erosion, node size, and movement steering.

Allowed A* examples:

Seeker
AIPath
AILerp
AIDestinationSetter
RichAI only if appropriate for the graph type
Custom movement that reads paths from Seeker

---

## Code Architecture Rules

The entire project must follow S.O.L.I.D principles.

### S — Single Responsibility Principle

Each class must have one clear responsibility.

Good:
- `PlayerMovementController`
- `PlayerAimController`
- `RewiredPlayerInputReader`
- `WeaponFireController`
- `Health`

Bad:
- One giant `PlayerController` that handles movement, input, shooting, inventory, health, UI, audio, and quests.

---

### O — Open/Closed Principle

Code should be open for extension and closed for modification.

Use:
- Interfaces.
- Abstract base classes when useful.
- Composition.
- Strategy-style classes.
- ScriptableObject configuration.

Avoid:
- Giant `switch` statements that must be edited every time a new behavior is added.
- Hardcoded weapon, enemy, mission, item, or input logic.

---

### L — Liskov Substitution Principle

Derived classes must be usable anywhere their base class is expected.

Do not create child classes that break expected behavior from the parent class.

---

### I — Interface Segregation Principle

Do not create huge interfaces.

Good:

```csharp
public interface IDamageable
{
    void TakeDamage(float amount);
}

public interface IHealable
{
    void Heal(float amount);
}
```

Bad:

```csharp
public interface IEntity
{
    void Move();
    void Shoot();
    void Heal();
    void OpenDoor();
    void SaveGame();
    void PlayMusic();
}
```

---

### D — Dependency Inversion Principle

High-level systems should depend on abstractions, not concrete implementations.

Prefer:

```csharp
private IDamageable damageable;
```

Instead of tightly coupling everything to:

```csharp
private EnemyHealth enemyHealth;
```

Use concrete references only when the coupling is intentional and local.

---

## DRY Rule

Never repeat code.

Before adding new logic, check whether similar logic already exists.

If repeated logic appears:
- Extract a method.
- Extract a helper class.
- Extract an interface.
- Extract a ScriptableObject config.
- Extract a shared utility only when it truly belongs as shared utility.

Do not create generic abstractions too early, but do not duplicate behavior.

---

## Optimization Rules

Always prioritize optimization for the game.

General rules:
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

## Component Reference Rules

### Same GameObject Components

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

### Persistent Objects

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

### External Scene References

Serialize references only when they are truly scene-specific and must be wired manually.

Allowed examples:
- Patrol points.
- Exit points.
- Scene-specific targets.
- UI text references.
- Audio sources on different objects.
- Prefabs.
- ScriptableObject configs.

---

## Comment Rules

All methods must include comments explaining what they do.

Preferred style is XML summary comments:

```csharp
/// <summary>
/// Moves the character using the cached Rigidbody2D and the latest movement input.
/// </summary>
private void MoveCharacter()
{
}
```

For very small Unity lifecycle methods, still comment them:

```csharp
/// <summary>
/// Initializes local component references before gameplay starts.
/// </summary>
private void Awake()
{
}
```

Comments must explain intent, not repeat obvious code.

Bad:

```csharp
/// <summary>
/// Sets speed to 5.
/// </summary>
private void SetSpeed()
{
    speed = 5f;
}
```

Good:

```csharp
/// <summary>
/// Resets movement speed back to the default value after temporary modifiers expire.
/// </summary>
private void ResetSpeed()
{
    speed = defaultSpeed;
}
```

---

## Obsolete Code Rules

Always check for obsolete APIs before finishing.

The agent must:
- Avoid deprecated Unity APIs.
- Replace obsolete APIs with current equivalents when possible.
- Mention any obsolete API that could not be replaced safely.
- Remove compiler warnings before finishing.

Do not suppress warnings unless there is a strong reason.

---

## Warning Rules

Before finishing, remove all warnings introduced by the change.

The agent must check for:
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

## Scene / Prefab Safety Rules

Do not:
- Create scene objects.
- Auto-wire scene references.
- Modify prefabs.
- Modify scenes.
- Modify `.unity` files.
- Modify `.prefab` files.

Unless the user explicitly requests it.

When a script requires manual wiring, explain:

```text
Unity setup:
1. Add this component to the Player object.
2. Assign the required ScriptableObject.
3. Make sure the object has Rigidbody2D.
4. Configure Rewired action names.
```

---

## Naming Rules

Use clear names.

Good:
- `PlayerMovementController`
- `RewiredPlayerInputReader`
- `WeaponFireController`
- `DamageReceiver`
- `PatrolRoute`

Bad:
- `Manager`
- `Controller2`
- `NewScript`
- `Stuff`
- `Temp`
- `DoThings`

---

## File Organization Rules

Prefer this structure:

```text
Assets/
└─ Scripts/
   ├─ Player/
   ├─ AI/
   ├─ Weapons/
   ├─ Input/
   ├─ Interactions/
   ├─ Missions/
   ├─ UI/
   ├─ Audio/
   ├─ Core/
   └─ Utilities/
```

Do not create new folders unnecessarily.

Place scripts near related systems.

---

## Implementation Workflow

Before editing code, the agent must:

1. Inspect relevant files.
2. Identify existing patterns.
3. Check if similar code already exists.
4. Plan the smallest safe change.
5. Respect current project structure.
6. Implement only what was requested.
7. Run validation when possible.
8. Summarize changed files.
9. Explain Unity manual setup.

---

## Validation Command

This project is developed on Linux / CachyOS.

After editing, run:

```bash
./tools/ai-harness/validate.sh
```

When Unity tests are needed, run:

```bash
./tools/ai-harness/unity-tests.sh
```

---

## Definition of Done

A task is done only when:

- The requested behavior is implemented.
- Code follows S.O.L.I.D.
- Code avoids repetition.
- Odin Inspector is used for organization where appropriate.
- Rewired is used for input.
- No scene objects were created or modified unless requested.
- Same-object components are cached, not serialized.
- Persistent/global objects are not serialized directly.
- All methods have comments.
- Obsolete APIs were checked.
- New warnings were removed.
- Validation was run when possible.
- Changed files are listed.
- Manual Unity setup is explained.

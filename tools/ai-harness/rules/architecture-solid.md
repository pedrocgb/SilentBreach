# Architecture, S.O.L.I.D., and DRY Rules

The entire project must follow S.O.L.I.D. principles and avoid duplicated code.

---

## S — Single Responsibility Principle

Each class must have one clear responsibility and one main reason to change.

Good examples:

- `PlayerMovementController`
- `PlayerAimController`
- `RewiredPlayerInputReader`
- `WeaponFireController`
- `Health`
- `EnemyPathController`
- `EnemyPatrolController`

Bad example:

```text
One giant PlayerController that handles movement, input, shooting, inventory, health, UI, audio, and quests.
```

---

## O — Open/Closed Principle

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

## L — Liskov Substitution Principle

Derived classes must be usable anywhere their base class is expected.

Do not create subclasses that break expected base behavior.

---

## I — Interface Segregation Principle

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

## D — Dependency Inversion Principle

High-level systems should depend on abstractions, not concrete implementations.

Prefer:

```csharp
private IDamageable damageable;
```

Instead of tightly coupling everything to:

```csharp
private EnemyHealth enemyHealth;
```

Concrete references are allowed when the coupling is intentional, local, and simple.

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

## Refactor Safety

When refactoring:

- Preserve existing behavior unless the user requests a behavior change.
- Avoid large rewrites.
- Work one system at a time.
- Keep diffs reviewable.
- Do not rename serialized fields unless necessary.
- Explain compatibility risks.

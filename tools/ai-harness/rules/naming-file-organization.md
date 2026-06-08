# Naming and File Organization Rules

Use clear names and keep files close to their related systems.

---

## Naming Rules

Use clear names.

Good:

- `PlayerMovementController`
- `RewiredPlayerInputReader`
- `WeaponFireController`
- `DamageReceiver`
- `PatrolRoute`
- `EnemyPathController`
- `EnemyHearingSensor`

Bad:

- `Manager`
- `Controller2`
- `NewScript`
- `Stuff`
- `Temp`
- `DoThings`

---

## File Organization

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

## Managers

Avoid large generic manager classes.

A manager is acceptable only when it has a clear responsibility.

Good:

- `MissionManager`
- `AudioManager`
- `SceneTransitionController`

Bad:

- `GameManager` that controls everything.
- `PlayerManager` that controls input, movement, combat, UI, and inventory.

---

## Serialized Field Names

Do not rename serialized fields unless necessary.

If a serialized field must be renamed, preserve Unity serialization with `[FormerlySerializedAs]` when appropriate.

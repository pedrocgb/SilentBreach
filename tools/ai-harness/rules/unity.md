# Unity Rules

These rules apply to all Unity code changes.

---

## Scene and Prefab Safety

Do not:

- Create scene objects.
- Auto-wire scene references.
- Modify prefabs.
- Modify scenes.
- Modify `.unity` files.
- Modify `.prefab` files.
- Create GameObjects automatically in code.

Unless the user explicitly requests it.

Everything that needs to exist in the scene must be wired manually by the developer.

---

## Manual Setup

When a script requires manual setup, explain the Unity setup clearly after implementation.

Example:

```text
Unity setup:
1. Add this component to the Player object.
2. Assign the required ScriptableObject.
3. Make sure the object has Rigidbody2D.
4. Configure the Rewired action names.
```

---

## Unity Serialization

- Do not break existing serialized references.
- Do not rename serialized fields unless absolutely necessary.
- Do not move files unless explicitly requested.
- Prefer ScriptableObjects for configurable gameplay data.
- Prefer inspector-friendly components, but do not over-serialize dependencies.
- Keep serialized fields private with `[SerializeField]` unless a public field is truly required.

---

## Unity Components

- Use `[RequireComponent]` when a same-object component is mandatory.
- Cache same-object components in `Awake()`.
- Use Unity lifecycle methods intentionally.
- Do not add unnecessary `Update()` methods.
- Do not put unrelated responsibilities inside one MonoBehaviour.

---

## ScriptableObjects

Prefer ScriptableObjects for:

- Weapon data.
- Mission/job data.
- AI configuration.
- Movement settings.
- Audio profiles.
- Reusable gameplay values.
- Event channels when appropriate.

Do not use ScriptableObjects as a global dumping ground for unrelated state.

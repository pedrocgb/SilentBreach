# Method Comment Rules

All methods in touched files must include comments explaining what they do.

Preferred style is XML summary comments.

---

## Required Style

Use XML summary comments:

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

---

## Comment Intent

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

## Scope

When editing a file:

- Add comments to all methods in that touched file.
- Keep comments concise.
- Do not add useless comments that only restate the method name.
- Public APIs should have especially clear comments.
- Unity lifecycle methods should still be documented.

---

## Do Not

Do not use comments to hide unclear code.

If a method needs a huge comment to be understood, consider splitting the method or renaming variables.

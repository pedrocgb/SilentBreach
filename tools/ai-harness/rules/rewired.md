# Rewired Input Rules

The project uses Rewired for input.

---

## Core Rules

- Do not use Unity's old `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, `Input.GetMouseButton`, etc.
- Do not use `UnityEngine.Input`.
- Do not use Unity's New Input System unless explicitly requested.
- Player input must go through Rewired.
- Input action names must be configurable or clearly documented.
- Never hardcode input behavior deeply inside gameplay logic when it can be abstracted.

---

## Preferred Structure

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

## Rewired Responsibilities

The Rewired-specific script should:

- Read Rewired player input.
- Convert action values into clean gameplay values.
- Expose those values through an interface or simple public properties.
- Keep Rewired-specific code away from movement, combat, and interaction logic when possible.

Gameplay scripts should not know more about Rewired than necessary.

---

## Action Names

Input action names should be:

- Configurable through serialized fields, or
- Centralized in constants, or
- Clearly documented if hardcoded.

Avoid scattering action name strings across many scripts.

---

## Forbidden Input APIs

Do not introduce these:

```csharp
Input.GetKey(...)
Input.GetAxis(...)
Input.GetButton(...)
Input.GetMouseButton(...)
UnityEngine.Input
```

Use Rewired instead.

public enum LightSwitchLookaroundPreset
{
    LeftLookaround,
    RightLookaround,
    UpLookaround,
    DownLookaround
}

public static class LightSwitchLookaroundPresetDefaults
{
    /// <summary>
    /// Resolves safe fallback angles when the persistent global settings object is unavailable.
    /// </summary>
    public static void Resolve(LightSwitchLookaroundPreset preset, out float minAngle, out float maxAngle)
    {
        switch (preset)
        {
            case LightSwitchLookaroundPreset.LeftLookaround:
                minAngle = 135f;
                maxAngle = 225f;
                break;

            case LightSwitchLookaroundPreset.UpLookaround:
                minAngle = 45f;
                maxAngle = 135f;
                break;

            case LightSwitchLookaroundPreset.DownLookaround:
                minAngle = -135f;
                maxAngle = -45f;
                break;

            default:
                minAngle = -45f;
                maxAngle = 45f;
                break;
        }
    }
}

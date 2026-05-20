public enum LocomotionPreset
{
    Sensitive,
    Intermediate,
    Expert
}

public static class VRSettingsStore
{
    // Session-only settings (no PlayerPrefs), shared between scenes while app runs.
    private static int _moveMode = 0; // 0 linear (Free), 1 teleport
    private static bool _snapEnabled;
    private static float _snapAngle = 30f;
    private static float _heightOffset;
    private static bool _configuredThisSession;
    private static LocomotionPreset _currentPreset = LocomotionPreset.Intermediate;

    public static int MoveMode
    {
        get => _moveMode;
        set
        {
            _moveMode = value;
            SyncPresetFromMoveSettings();
        }
    }

    public static bool SnapEnabled
    {
        get => _snapEnabled;
        set
        {
            _snapEnabled = value;
            SyncPresetFromMoveSettings();
        }
    }

    public static float SnapAngle
    {
        get => _snapAngle;
        set => _snapAngle = value;
    }

    public static float HeightOffset
    {
        get => _heightOffset;
        set => _heightOffset = value;
    }

    public static LocomotionPreset CurrentPreset => _currentPreset;

    public static bool IsConfigured => _configuredThisSession;

    /// <summary>Intermediate (Free, no snap) — default new game profile.</summary>
    public static void ResetToDefaults()
    {
        ApplyPreset(LocomotionPreset.Intermediate);
    }

    public static void ApplyPreset(LocomotionPreset preset)
    {
        _currentPreset = preset;
        switch (preset)
        {
            case LocomotionPreset.Sensitive:
                _moveMode = 1;
                _snapEnabled = false;
                break;
            case LocomotionPreset.Expert:
                _moveMode = 0;
                _snapEnabled = true;
                break;
            default:
                _moveMode = 0;
                _snapEnabled = false;
                break;
        }

        _configuredThisSession = true;
    }

    public static void SyncPresetFromMoveSettings()
    {
        if (_moveMode == 1)
            _currentPreset = LocomotionPreset.Sensitive;
        else if (_snapEnabled)
            _currentPreset = LocomotionPreset.Expert;
        else
            _currentPreset = LocomotionPreset.Intermediate;

        _configuredThisSession = true;
    }
}

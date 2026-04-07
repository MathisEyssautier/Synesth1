using UnityEngine;

public static class VRSettingsStore
{
    // Session-only settings (no PlayerPrefs):
    // shared between scenes while app runs, reset on game restart.
    private static int _moveMode = 0; // 0 linear, 1 teleport
    private static bool _snapEnabled = true;
    private static float _snapAngle = 45f;
    private static float _heightOffset = 0f;

    public static int MoveMode
    {
        get => _moveMode;
        set => _moveMode = value;
    }

    public static bool SnapEnabled
    {
        get => _snapEnabled;
        set => _snapEnabled = value;
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
}

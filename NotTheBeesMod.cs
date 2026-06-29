using System;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace STS2_NotTheBees;

[ModInitializer("Initialize")]
public static class NotTheBeesMod
{
    private const string HarmonyId = "com.sts2notthebees";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        try
        {
            _initialized = true;
            GD.Print("[NotTheBees] initialized");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NotTheBees] init failed: {ex.Message}");
        }
    }
}

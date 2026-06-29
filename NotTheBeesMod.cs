using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Modding;

namespace STS2_NotTheBees;

[ModInitializer("Initialize")]
public static class NotTheBeesMod
{
    private const string HarmonyId = "com.sts2notthebees";
    private const string CardTitle = "NOT THE BEES!";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        try
        {
            var harmony = new Harmony(HarmonyId);

            var titleGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Title));
            harmony.Patch(titleGetter, prefix: new HarmonyMethod(
                typeof(NotTheBeesMod).GetMethod(nameof(TitlePrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));

            _initialized = true;
            GD.Print("[NotTheBees] initialized (rename active)");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NotTheBees] init failed: {ex.Message}");
        }
    }

    // Override the displayed title for the Dazed status card only; run the original for all others.
    private static bool TitlePrefix(CardModel __instance, ref string __result)
    {
        if (__instance is Dazed)
        {
            __result = CardTitle;
            return false;
        }
        return true;
    }
}

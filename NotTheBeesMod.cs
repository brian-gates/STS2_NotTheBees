using System;
using System.IO;
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
    private const string ImageFileName = "not-the-bees.png";

    private static bool _initialized;
    private static Texture2D? _beeTexture;

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

            _beeTexture = LoadBeeTexture();
            if (_beeTexture != null)
            {
                var portraitGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Portrait));
                harmony.Patch(portraitGetter, prefix: new HarmonyMethod(
                    typeof(NotTheBeesMod).GetMethod(nameof(PortraitPrefix),
                        BindingFlags.NonPublic | BindingFlags.Static)));
                GD.Print("[NotTheBees] initialized (rename + art active)");
            }
            else
            {
                GD.PrintErr("[NotTheBees] image not loaded; art swap disabled (rename still active)");
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NotTheBees] init failed: {ex.Message}");
        }
    }

    // Load the bundled PNG (sits beside the DLL in the mods folder) into a Texture2D, once.
    private static Texture2D? LoadBeeTexture()
    {
        try
        {
            string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (modDir == null) return null;
            string path = Path.Combine(modDir, ImageFileName);
            if (!File.Exists(path))
            {
                GD.PrintErr($"[NotTheBees] image not found at {path}");
                return null;
            }
            var image = Image.LoadFromFile(path);
            if (image == null) return null;
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NotTheBees] image load failed: {ex.Message}");
            return null;
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

    // Return the bee-cage texture for Dazed only; run the original loader for all other cards.
    private static bool PortraitPrefix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance is Dazed && _beeTexture != null)
        {
            __result = _beeTexture;
            return false;
        }
        return true;
    }
}

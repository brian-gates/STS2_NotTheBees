using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
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
    // The power the Entomancer applies; its AfterDamageReceived creates the Dazed we reflavor.
    private const string HivePowerName = "PersonalHivePower";

    private static bool _initialized;
    private static Texture2D? _beeTexture;

    // Tags the specific Dazed instances created by the Entomancer's hive power. Weak keys so cards
    // are collected normally; the canonical/library Dazed is never tagged (stays vanilla).
    private static readonly ConditionalWeakTable<CardModel, object> _hiveDazed = new();
    private static readonly object Marker = new();

    public static void Initialize()
    {
        if (_initialized) return;
        try
        {
            var harmony = new Harmony(HarmonyId);

            // Tag hive-sourced Dazed as the power adds them to the combat draw pile.
            var addCard = AccessTools.Method(typeof(CardPileCmd),
                nameof(CardPileCmd.AddGeneratedCardToCombat));
            harmony.Patch(addCard, prefix: new HarmonyMethod(
                typeof(NotTheBeesMod).GetMethod(nameof(TagHiveCardPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));

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
                GD.Print("[NotTheBees] initialized (Entomancer Dazed: rename + art)");
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

    private static bool IsHiveDazed(CardModel card)
        => card is Dazed && _hiveDazed.TryGetValue(card, out _);

    // When the Entomancer's hive power adds a Dazed to combat, tag that specific instance.
    private static void TagHiveCardPrefix(CardModel card)
    {
        if (card is not Dazed) return;
        if (!CallerIsHivePower()) return;
        _hiveDazed.AddOrUpdate(card, Marker);
    }

    // PersonalHivePower.AfterDamageReceived calls AddGeneratedCardToCombat synchronously from its
    // async state machine, so a frame whose declaring type belongs to PersonalHivePower (the
    // generated state-machine type is nested in it) is on the stack here.
    private static bool CallerIsHivePower()
    {
        var st = new StackTrace(false);
        for (int i = 0; i < st.FrameCount; i++)
        {
            var dt = st.GetFrame(i)?.GetMethod()?.DeclaringType;
            if (dt == null) continue;
            if (dt.Name.Contains(HivePowerName) || (dt.FullName?.Contains(HivePowerName) ?? false))
                return true;
        }
        return false;
    }

    // Override title only for hive-created Dazed; run the original for everything else.
    private static bool TitlePrefix(CardModel __instance, ref string __result)
    {
        if (IsHiveDazed(__instance))
        {
            __result = CardTitle;
            return false;
        }
        return true;
    }

    // Return the bee-cage texture only for hive-created Dazed; run the original loader otherwise.
    private static bool PortraitPrefix(CardModel __instance, ref Texture2D __result)
    {
        if (_beeTexture != null && IsHiveDazed(__instance))
        {
            __result = _beeTexture;
            return false;
        }
        return true;
    }
}

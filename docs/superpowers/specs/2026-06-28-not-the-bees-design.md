# STS2_NotTheBees — design

**Date:** 2026-06-28
**Status:** Approved design, pending spec review

## Summary

A cosmetic meme mod for Slay the Spire 2. When the bee boss inflicts **Daze** status
cards into your deck, every Daze card is renamed to a Nic Cage "NOT THE BEES!" quote and
shows the iconic *Wicker Man* (2006) bee-cage face as its art. Pure flavor — no gameplay,
balance, or mechanics change.

## Context

- **Game:** Slay the Spire 2 v0.107.1 — Godot engine with C# (.NET 9) scripting.
- **Install (macOS):** `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents`
  - Game assembly: `Resources/data_sts2_macos_arm64/sts2.dll`
  - Mods live in: `MacOS/mods/` (each mod = `<id>.dll` + `<id>.json` manifest [+ optional `.conf`])
- **Mod framework:** Native loader. Entry via `[ModInitializer("Initialize")]` attribute on a
  static class; game namespace `MegaCrit.Sts2.Core.*`; runtime patching via **HarmonyLib**
  (`0Harmony.dll`).
- **Reference implementation:** the user's own `STS2_DamageCharts` mod
  (`~/code/sts2-damage-charts`) — its `.csproj`, `[ModInitializer]` entry, and Harmony setup
  are the template for this mod.
- The pck confirms the card exists: strings `Daze`, `Dazed`, `DazedCount`, `Dazes`.

## Scope

**In scope:** the Dazed status card — rename + art swap — **but only the instances created by
the Entomancer** (the bee elite), not Dazed from any other source.
**Out of scope:** bee boss name/intro, attack/move names, audio, any gameplay change. (User
chose "Daze cards only".)

### Why scoping is needed (decompile finding)

There is exactly one shared `Dazed` card model; the card does not record who created it. Dazed is
added by many sources: the **Entomancer** (via `PersonalHivePower`, whose move is literally
`BEES_MOVE` and which calls `CreateCard<Dazed>()`), monsters Chomper/Noisebot/HauntedShip/
EyeWithTeeth, relics Tea of Discourtesy and Blessed Antler, and the card Boost Away. A naive
`is Dazed` gate would reflavor all of them. The user wants **only the Entomancer's**, so the mod
must tag the specific instances the hive power creates and gate the rename/reface on that tag.

### How the Entomancer's Dazed are distinguished

`PersonalHivePower.AfterDamageReceived` creates each Dazed and adds it to combat via
`CardPileCmd.AddGeneratedCardToCombat(CardModel card, …)`, called synchronously from the power's
async state machine. A Harmony prefix on `AddGeneratedCardToCombat` runs with that state-machine
frame on the stack, so it can confirm the hive power is the caller and tag the `card` instance in
a `ConditionalWeakTable<CardModel, object>`. Each in-combat Dazed is a fresh clone of the canonical
model (`CreateCard` clones), so per-instance tagging is exact and the canonical/library Dazed is
never tagged (stays vanilla). The `Title`/`Portrait` patches then fire only for tagged instances.

## Behavior

When any **Daze** card is created/displayed (in hand, deck view, discard, card-select, etc.):
- Its **title** reads a Cage line, e.g. `NOT THE BEES!` (exact string finalized in the plan;
  candidates: `NOT THE BEES!`, `AHHH! MY EYES!`, `OH NO! NOT THE BEES!`).
- Its **art** is `not-the-bees.png` (the bee-cage face), bundled with the mod.
- Optionally, its description/flavor text may be reflavored if a clean hook exists (nice-to-have,
  not required).

Every **other** card is untouched — and so is **every Dazed that the Entomancer did not create**
(e.g. a Dazed from Blessed Antler stays a normal Dazed). Both display patches gate on the
hive-tag, not merely `is Dazed`, and pass through for anything else.

## Architecture

A single self-contained mod assembly, structured to mirror `STS2_DamageCharts`:

```
/Users/brian/code/bees/
  STS2_NotTheBees.csproj      # references sts2.dll, GodotSharp.dll, 0Harmony.dll (macOS paths)
  NotTheBeesMod.cs            # [ModInitializer] entry: Harmony setup, texture load, patches
  STS2_NotTheBees.json        # mod manifest (affects_gameplay: false)
  assets/not-the-bees.png     # the meme image (already present, 400x300, converted from webp)
  docs/superpowers/specs/...  # this spec
```

### Components

1. **`NotTheBeesMod` (entry / lifecycle)**
   - `Initialize()` — invoked by the loader. Creates a `Harmony` instance (`com.sts2notthebees`),
     applies the tag + rename patches, loads the card texture once into a cached static
     `Texture2D`, then applies the reface patch. Idempotent guard (`_initialized`) and a try/catch
     kill-switch, copying the DamageCharts pattern.
   - **Texture loading:** at init, load `assets/not-the-bees.png` from the mod's own directory
     (`Assembly.GetExecutingAssembly().Location` → dir) via Godot
     `Image.LoadFromFile` → `ImageTexture.CreateFromImage`. Cache in a static field. If load
     fails, log and skip the reface patch (rename still works).
   - **Hive tag set:** `static readonly ConditionalWeakTable<CardModel, object> _hiveDazed`.

2. **Tag patch** — Harmony prefix on `CardPileCmd.AddGeneratedCardToCombat(CardModel card, …)`.
   When `card is Dazed` and a `PersonalHivePower` frame is on the call stack
   (`new StackTrace(false)`, match `DeclaringType.FullName.Contains("PersonalHivePower")` —
   covers the async state-machine nested type), add `card` to `_hiveDazed`.

3. **Rename patch** — Harmony prefix on `CardModel.Title` getter (verified `public virtual string
   Title`). When the instance is in `_hiveDazed`, set `__result = "NOT THE BEES!"` and skip the
   original (`return false`); otherwise run the original (`return true`).

4. **Reface patch** — Harmony prefix on `CardModel.Portrait` getter (verified
   `public Texture2D Portrait => ResourceLoader.Load<Texture2D>(...)`, consumed by `NCard` at
   `_portrait.Texture = Model.Portrait`). When the instance is in `_hiveDazed` and the texture
   loaded, set `__result` to the cached texture and skip the original; otherwise run the original.

### Build & install

- Build: `dotnet build -c Release -p:STS2GameDir="~/Library/.../Slay the Spire 2"` (the csproj's
  macOS `STS2GameDataDir` block resolves `sts2.dll` inside the `.app` bundle).
- Install: copy `STS2_NotTheBees.dll`, `STS2_NotTheBees.json`, and `not-the-bees.png` into the
  game's `MacOS/mods/` folder (a post-build copy step or a small `install.sh`, matching how the
  other mods are deployed).

## Verified facts (decompiled `sts2.dll`, v0.107.1)

- Card: `sealed class Dazed : CardModel` (`MegaCrit.Sts2.Core.Models.Cards`).
- `CardModel.Title` (`public virtual string`) — displayed name. `CardModel.Portrait`
  (`public Texture2D`) — art; `NCard` does `_portrait.Texture = Model.Portrait`, and Dazed
  (Status rarity) takes the visible-portrait branch.
- Entomancer (`Models.Monsters.Entomancer`, move `BEES_MOVE`) applies `PersonalHivePower`;
  `PersonalHivePower.AfterDamageReceived` does `CreateCard<Dazed>()` then
  `CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, …)` per stack.
- `CardPileCmd.AddGeneratedCardToCombat(CardModel card, PileType, Player?, CardPilePosition)` —
  static async; the `card` parameter is the freshly-created Dazed instance to tag.

## Testing / verification

Godot mod, no unit-test harness — verification is manual, in-game, via the **dev console**
(enabled here because the game is running modded: `NDevConsole` enables full commands when
`ModManager.IsRunningModded()`). Commands have Tab-completion.

1. Build, install into `mods/`, launch STS2; start a run.
2. Confirm the mod loads (init log line, no loader error).
3. **Positive test:** open the console, `fight ` + Tab to the Entomancer elite encounter, fight it,
   and take a powered-attack hit while it has its hive power so it shuffles in Dazed. Those Dazed
   must read **NOT THE BEES!** with the bee-cage art, in hand and in the draw/deck views.
4. **Negative test (scoping):** `card DAZED` adds a Dazed *not* created by the hive power — it must
   stay a **normal** Dazed (vanilla name + art). This proves the scoping works.
5. Confirm no crash on card-select / deck-view screens.

## Known limitations

- A hive Dazed that survives a **save/reload mid-fight** is re-deserialized as a new instance and
  loses its tag, reverting to vanilla. Acceptable (Dazed is Ethereal — usually gone same turn).
- The hive power's hover-tip preview uses the canonical (untagged) Dazed, so it stays vanilla;
  only the actual in-combat cards are reflavored.

## Non-goals / YAGNI

- No config file, no toggle UI, no multiple images, no randomized quotes (single fixed string +
  single image). Can be added later if wanted.
- No multiplayer-specific handling beyond whatever the cosmetic patches naturally do.

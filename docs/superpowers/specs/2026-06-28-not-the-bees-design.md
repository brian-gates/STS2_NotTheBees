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

**In scope:** Daze status card only — rename + art swap.
**Out of scope:** bee boss name/intro, attack/move names, audio, any gameplay change. (User
chose "Daze cards only".)

## Behavior

When any **Daze** card is created/displayed (in hand, deck view, discard, card-select, etc.):
- Its **title** reads a Cage line, e.g. `NOT THE BEES!` (exact string finalized in the plan;
  candidates: `NOT THE BEES!`, `AHHH! MY EYES!`, `OH NO! NOT THE BEES!`).
- Its **art** is `not-the-bees.png` (the bee-cage face), bundled with the mod.
- Optionally, its description/flavor text may be reflavored if a clean hook exists (nice-to-have,
  not required).

Every **other** card in the game is untouched — both patches gate on the Daze card's identity
(id or concrete type) and pass through for anything else.

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
   - `Initialize()` — invoked by the loader. Creates a `Harmony` instance with a unique id
     (`com.sts2notthebees`), loads the card texture once into a cached static `Texture2D`,
     and `PatchAll()` / applies the two patches. Idempotent guard (`_initialized`) and a
     try/catch kill-switch, copying the DamageCharts pattern.
   - **Texture loading:** at init, load `assets/not-the-bees.png` from the mod's own directory
     (`Assembly.GetExecutingAssembly().Location` → dir) via Godot
     `Image.LoadFromFile` → `ImageTexture.CreateFromImage`. Cache in a static field. If load
     fails, log and skip the reface patch (rename still works).

2. **Rename patch** — Harmony postfix on the Daze card's display-title resolver (a `LocString`
   getter or name property on the Daze card type). Returns the meme string when the instance is
   a Daze card; leaves the result unchanged otherwise.

3. **Reface patch** — Harmony patch on whatever supplies the Daze card's art `Texture2D`
   (a property getter, or the card-view node's texture assignment). Returns/assigns the cached
   meme texture for the Daze card; passes through otherwise.

### Build & install

- Build: `dotnet build -c Release -p:STS2GameDir="~/Library/.../Slay the Spire 2"` (the csproj's
  macOS `STS2GameDataDir` block resolves `sts2.dll` inside the `.app` bundle).
- Install: copy `STS2_NotTheBees.dll`, `STS2_NotTheBees.json`, and `not-the-bees.png` into the
  game's `MacOS/mods/` folder (a post-build copy step or a small `install.sh`, matching how the
  other mods are deployed).

## The key unknown (first implementation step)

The exact members for the Daze card's **title** and **art** in `sts2.dll` are not yet known.

**Plan step 1 is a decompile spike:** open `sts2.dll` (ilspycmd / mono-disassembler) and locate:
- the concrete Daze card class (or the id used to look it up), and
- where its display name and its art texture are resolved.

This pins both patch targets to real methods before any patch is written.

### Risk: art may not be a C# property

If card art is baked into a Godot scene/atlas and never flows through a patchable C# member,
the reface hook moves to the **card-view node** layer — patch the node method that assigns the
card's texture, swapping it when the bound card is a Daze. The decompile spike determines which
layer before we commit to a patch shape. The rename is low-risk either way (text resolution is
in C#). Worst case for art: ship rename-only and revisit.

## Testing / verification

This is a Godot mod with no unit-test harness; verification is manual, in-game:
1. Build, install into `mods/`, launch STS2.
2. Confirm the mod loads (log line at init, no loader error).
3. Reach a fight with the bee boss (or use the MCP mod / debug to obtain a Daze card) and
   confirm: Daze cards show the meme art + renamed title; all other cards look normal.
4. Confirm no crash on the card-select / deck-view screens where Daze is rendered.

The plan should include a fast path to obtain a Daze card for testing without grinding to the
boss (e.g. via the existing `STS2_MCP` mod or a debug command), to keep the iteration loop short.

## Non-goals / YAGNI

- No config file, no toggle UI, no multiple images, no randomized quotes (single fixed string +
  single image). Can be added later if wanted.
- No multiplayer-specific handling beyond whatever the cosmetic patches naturally do.

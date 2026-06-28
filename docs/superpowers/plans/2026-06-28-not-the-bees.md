# STS2_NotTheBees Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A cosmetic Slay the Spire 2 mod that renames the **Dazed** status card to `NOT THE BEES!` and replaces its art with the *Wicker Man* bee-cage image.

**Architecture:** A single .NET 9 mod assembly loaded by STS2's native mod loader via `[ModInitializer]`. Two HarmonyLib **prefix** patches on `CardModel` property getters (`Title` and `Portrait`), each gated to fire only when the instance is the sealed `Dazed` card type and otherwise passing through to the original. The replacement texture is loaded once at init from a PNG bundled beside the DLL.

**Tech Stack:** C# / .NET 9, Godot (GodotSharp), HarmonyLib (`0Harmony.dll`), references the game's `sts2.dll`. Build with `dotnet`. Manual in-game verification (Godot mod — no automated test harness).

## Global Constraints

- **Target framework:** `net9.0`, `LangVersion 12.0`, `Nullable enable`, `OutputType Library`.
- **Assembly + mod id:** `STS2_NotTheBees` (DLL, manifest id, and namespace all match).
- **Game install (macOS):** `/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2`
  - Game assembly dir: `<install>/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64` (contains `sts2.dll`, `GodotSharp.dll`, `0Harmony.dll`).
  - Mods install dir: `<install>/SlayTheSpire2.app/Contents/MacOS/mods/`
- **References must be `<Private>false</Private>`** (do not copy game DLLs into output) — matches the DamageCharts csproj.
- **Patches must be card-scoped:** every patch gates on `__instance is Dazed` and returns `true` (run original) for all other cards. No other card may be visually affected.
- **`affects_gameplay: false`** in the manifest — this mod is purely cosmetic.
- **Harmony id:** `com.sts2notthebees`.
- **DOTNET_ROOT** must be exported for `dotnet`/tools on this machine: `export DOTNET_ROOT="/opt/homebrew/Cellar/dotnet/10.0.107/libexec"`.

## Verified facts (from decompiling `sts2.dll`, v0.107.1)

These are the real members the patches target — confirmed, not assumed:

- The card is `public sealed class Dazed : CardModel` in namespace `MegaCrit.Sts2.Core.Models.Cards` (Status type, Status rarity, Ethereal + Unplayable).
- `CardModel` is `MegaCrit.Sts2.Core.Models.CardModel`. Relevant members:
  - `public virtual string Title { get; }` — the displayed card name (builds from a `LocString`).
  - `public Texture2D Portrait { get; }` — `=> ResourceLoader.Load<Texture2D>(PortraitPath, ...)`. The card art.
- The view node `MegaCrit.Sts2.Core.Nodes.Cards.NCard` reads both: `Texture2D portrait = Model.Portrait;` then `_portrait.Texture = portrait;`, and Dazed (Status rarity, not Ancient) takes the `_portrait.Visible = true` branch — so a patched `Portrait` getter flows to the on-screen texture.

---

### Task 1: Project scaffold that loads in-game

Goal: a buildable, installable mod DLL that the game loads and logs — before any patches. This is the build/install/launch loop everything else iterates on.

**Files:**
- Create: `/Users/brian/code/bees/STS2_NotTheBees.csproj`
- Create: `/Users/brian/code/bees/NotTheBeesMod.cs`
- Create: `/Users/brian/code/bees/STS2_NotTheBees.json`
- Create: `/Users/brian/code/bees/install.sh`
- Already present: `/Users/brian/code/bees/assets/not-the-bees.png`

**Interfaces:**
- Produces: `STS2_NotTheBees.NotTheBeesMod.Initialize()` — static, parameterless, the `[ModInitializer]` entry point. Later tasks add patch registration inside it.

- [ ] **Step 1: Create the csproj**

Create `/Users/brian/code/bees/STS2_NotTheBees.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>12.0</LangVersion>
    <AssemblyName>STS2_NotTheBees</AssemblyName>
    <RootNamespace>STS2_NotTheBees</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <STS2GameDir Condition="'$(STS2GameDir)' == ''">/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2</STS2GameDir>
    <STS2GameDataDir Condition="'$(STS2GameDataDir)' == '' and $([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::OSX)))">$(STS2GameDir)/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64</STS2GameDataDir>
    <STS2GameDataDir Condition="'$(STS2GameDataDir)' == '' and '$(OS)' == 'Windows_NT'">$(STS2GameDir)/data_sts2_windows_x86_64</STS2GameDataDir>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="NotTheBeesMod.cs" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="sts2">
      <HintPath>$(STS2GameDataDir)/sts2.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="GodotSharp">
      <HintPath>$(STS2GameDataDir)/GodotSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(STS2GameDataDir)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the manifest**

Create `/Users/brian/code/bees/STS2_NotTheBees.json`:

```json
{
  "id": "STS2_NotTheBees",
  "name": "NOT THE BEES!",
  "author": "brian",
  "description": "Meme mod: the Dazed status card becomes Nic Cage screaming under a cage of bees (Wicker Man). Renames Dazed to 'NOT THE BEES!' and swaps its art. Cosmetic only.",
  "version": "0.1.0",
  "has_pck": false,
  "has_dll": true,
  "affects_gameplay": false
}
```

- [ ] **Step 3: Create the minimal mod entry**

Create `/Users/brian/code/bees/NotTheBeesMod.cs`:

```csharp
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
```

- [ ] **Step 4: Create the install script**

Create `/Users/brian/code/bees/install.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail
INSTALL="/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
MODS="$INSTALL/SlayTheSpire2.app/Contents/MacOS/mods"
DLL="bin/Release/net9.0/STS2_NotTheBees.dll"

[ -f "$DLL" ] || { echo "Build first: $DLL not found"; exit 1; }
cp "$DLL" "$MODS/STS2_NotTheBees.dll"
cp "STS2_NotTheBees.json" "$MODS/STS2_NotTheBees.json"
cp "assets/not-the-bees.png" "$MODS/not-the-bees.png"
echo "Installed to: $MODS"
```

Then: `chmod +x /Users/brian/code/bees/install.sh`

- [ ] **Step 5: Build**

Run:
```bash
export DOTNET_ROOT="/opt/homebrew/Cellar/dotnet/10.0.107/libexec"
cd /Users/brian/code/bees
dotnet build -c Release -p:STS2GameDir="/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
```
Expected: `Build succeeded`, produces `bin/Release/net9.0/STS2_NotTheBees.dll`. If `sts2.dll` is not found, the `STS2GameDataDir` path is wrong — verify it points inside the `.app` bundle.

- [ ] **Step 6: Install and launch, confirm load**

Run:
```bash
cd /Users/brian/code/bees && ./install.sh
```
Then launch STS2 (Steam, or `open "/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app"`).

Expected: the game starts normally and the log contains `[NotTheBees] initialized`. Check the in-game mod list (or the Godot log / stdout) shows `NOT THE BEES!` as a loaded mod with no loader error. The game's log file location is the same one the DamageCharts/MCP mods print to (Godot user data dir, or stdout when launched from a terminal).

- [ ] **Step 7: Commit**

```bash
cd /Users/brian/code/bees
git add STS2_NotTheBees.csproj NotTheBeesMod.cs STS2_NotTheBees.json install.sh
git commit -m "feat: scaffold STS2_NotTheBees mod that loads in-game"
```

---

### Task 2: Rename Dazed → "NOT THE BEES!"

Goal: a Harmony prefix on `CardModel.Title` that returns the meme string for `Dazed` and passes through for every other card.

**Files:**
- Modify: `/Users/brian/code/bees/NotTheBeesMod.cs`

**Interfaces:**
- Consumes: `NotTheBeesMod.Initialize()` (Task 1).
- Produces: `NotTheBeesMod.TitlePrefix(CardModel __instance, ref string __result)` — Harmony prefix, returns `false` (skip original) for `Dazed`.

- [ ] **Step 1: Add the title patch to `NotTheBeesMod.cs`**

Replace the entire contents of `/Users/brian/code/bees/NotTheBeesMod.cs` with:

```csharp
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
```

- [ ] **Step 2: Build**

Run:
```bash
export DOTNET_ROOT="/opt/homebrew/Cellar/dotnet/10.0.107/libexec"
cd /Users/brian/code/bees
dotnet build -c Release -p:STS2GameDir="/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
```
Expected: `Build succeeded`.

- [ ] **Step 3: Install, launch, and verify the rename in-game**

Run `./install.sh`, launch STS2, and obtain a Dazed card. Fastest path: use the installed `STS2_MCP` mod / the game's debug tooling to add a `Dazed` card to your deck; otherwise fight an enemy or use a relic that inflicts Dazed. Open the deck/hand view.

Expected: the Dazed card's name reads **NOT THE BEES!**. Confirm a normal card (e.g. Strike) still shows its real name — the patch must not affect other cards.

- [ ] **Step 4: Commit**

```bash
cd /Users/brian/code/bees
git add NotTheBeesMod.cs
git commit -m "feat: rename Dazed card to NOT THE BEES!"
```

---

### Task 3: Swap Dazed art to the bee-cage image

Goal: load `not-the-bees.png` once at init and return it from `CardModel.Portrait` for `Dazed` only.

**Files:**
- Modify: `/Users/brian/code/bees/NotTheBeesMod.cs`

**Interfaces:**
- Consumes: `NotTheBeesMod.Initialize()`, `TitlePrefix` (Task 2).
- Produces: `NotTheBeesMod.PortraitPrefix(CardModel __instance, ref Texture2D __result)` — Harmony prefix returning the cached texture for `Dazed`.

- [ ] **Step 1: Add texture loading and the portrait patch**

Replace the entire contents of `/Users/brian/code/bees/NotTheBeesMod.cs` with:

```csharp
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
```

- [ ] **Step 2: Build**

Run:
```bash
export DOTNET_ROOT="/opt/homebrew/Cellar/dotnet/10.0.107/libexec"
cd /Users/brian/code/bees
dotnet build -c Release -p:STS2GameDir="/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
```
Expected: `Build succeeded`.

- [ ] **Step 3: Install, launch, and verify the art swap in-game**

Run `./install.sh`, launch STS2, get a Dazed card (as in Task 2), and view it.

Expected: the Dazed card shows the bee-cage Nic Cage image as its portrait **and** the name `NOT THE BEES!`. Verify a normal card's art is unchanged. Check the deck view, hand, and a card-select screen if reachable — the texture should render in all of them (NCard sets `_portrait.Texture` from `Model.Portrait` in each).

**If the art does not appear** but the rename works: the texture path is fine but the portrait may be drawn through a different node path for this card. Capture the log, confirm `[NotTheBees] initialized (rename + art active)` printed, and confirm the image file is present next to the DLL in the mods folder. (This is the spec's flagged risk; the rename still ships regardless.)

- [ ] **Step 4: Commit**

```bash
cd /Users/brian/code/bees
git add NotTheBeesMod.cs
git commit -m "feat: swap Dazed card art to the bee-cage image"
```

---

### Task 4: README and final commit

Goal: document build/install/usage so the mod is reproducible.

**Files:**
- Create: `/Users/brian/code/bees/README.md`

- [ ] **Step 1: Write the README**

Create `/Users/brian/code/bees/README.md`:

```markdown
# STS2_NotTheBees

A cosmetic Slay the Spire 2 mod. The **Dazed** status card becomes Nic Cage
screaming under a cage of bees (*The Wicker Man*, 2006): renamed to
**NOT THE BEES!** with the meme image as its art. No gameplay changes.

## How it works

Two HarmonyLib prefix patches on `MegaCrit.Sts2.Core.Models.CardModel`,
gated to the sealed `Dazed` card type:
- `Title` getter → returns `NOT THE BEES!`
- `Portrait` getter → returns the bundled `not-the-bees.png`

All other cards pass through untouched.

## Build

Requires the .NET SDK and a local STS2 install.

```bash
export DOTNET_ROOT="/opt/homebrew/Cellar/dotnet/10.0.107/libexec"
dotnet build -c Release \
  -p:STS2GameDir="/Users/brian/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
```

## Install

```bash
./install.sh
```

Copies the DLL, `STS2_NotTheBees.json`, and `not-the-bees.png` into the
game's `SlayTheSpire2.app/Contents/MacOS/mods/` folder. Restart the game.

## Swapping the image

Replace `assets/not-the-bees.png` with any PNG and re-run `./install.sh`.
```

- [ ] **Step 2: Commit**

```bash
cd /Users/brian/code/bees
git add README.md
git commit -m "docs: add README"
```

---

## Self-Review

**Spec coverage:**
- Rename Dazed → Task 2. ✓
- Reface Dazed → Task 3. ✓
- Daze-only scoping (no other card affected) → `is Dazed` gate in both prefixes, verified in Tasks 2/3 steps. ✓
- User-supplied image → `assets/not-the-bees.png` present, loaded in Task 3, swappable per README. ✓
- Build/install mirroring DamageCharts → Task 1 csproj + install.sh. ✓
- Manifest `affects_gameplay: false` → Task 1 Step 2. ✓
- First step is grounding in real members → done during planning (decompile facts recorded above); patch targets are the verified `CardModel.Title` / `CardModel.Portrait`. ✓
- Risk that art isn't a patchable C# member → resolved: `Portrait` IS a C# getter consumed by `NCard`; residual fallback documented in Task 3 Step 3. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete file contents; every command has expected output. ✓

**Type consistency:** `CardModel`, `Dazed`, `Title` (string), `Portrait` (Texture2D), `Initialize()`, `TitlePrefix`, `PortraitPrefix`, `LoadBeeTexture`, `_beeTexture`, Harmony id `com.sts2notthebees` — consistent across all tasks. ✓

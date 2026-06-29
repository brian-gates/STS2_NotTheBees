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

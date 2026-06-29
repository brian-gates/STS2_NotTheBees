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

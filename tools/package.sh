#!/usr/bin/env bash
# Builds the release zip: BepInEx (from a BepInEx pack folder: winhttp.dll, doorstop_config.ini, BepInEx/core)
# + the freshly built plugin + the player guide.
# usage: tools/package.sh <bepinex-pack-dir> [out-dir]
set -euo pipefail
cd "$(dirname "$0")/.."
PACK=${1:?BepInEx pack folder (with winhttp.dll, doorstop_config.ini, BepInEx/core)}
OUT=${2:-release}
VER=$(grep -oP 'public const string Version = "\K[^"]+' src/DarkwoodCoopOnline/Plugin.cs)
./build.sh >/dev/null
STAGE=$(mktemp -d)
cp "$PACK/winhttp.dll" "$PACK/doorstop_config.ini" "$STAGE/"
mkdir -p "$STAGE/BepInEx/core" "$STAGE/BepInEx/plugins"
cp "$PACK"/BepInEx/core/* "$STAGE/BepInEx/core/"
cp build/out/DarkwoodCoopOnline.dll "$STAGE/BepInEx/plugins/"
cp release/README-COOP.txt "$STAGE/"
mkdir -p "$OUT"
rm -f "$OUT/DarkwoodCoopOnline-4P-v$VER.zip"
(cd "$STAGE" && zip -qr9 "$OLDPWD/$OUT/DarkwoodCoopOnline-4P-v$VER.zip" .)
rm -rf "$STAGE"
echo "$OUT/DarkwoodCoopOnline-4P-v$VER.zip"

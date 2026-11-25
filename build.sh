#!/usr/bin/env bash
# Builds DarkwoodCoopOnline.dll. Needs the .NET SDK (8+), curl and unzip.
# Reference assemblies are downloaded into lib/ the first time; game-specific ones
# (Assembly-CSharp, Steamworks, Rewired) are generated as compile-only stubs by tools/StubGen.
set -euo pipefail
cd "$(dirname "$0")"
fetch() { # name version feed-base out-subdir files...
  local name=$1 ver=$2 base=$3 dir=$4; shift 4
  [ -f "lib/$dir/.ok-$ver" ] && return
  mkdir -p "lib/$dir" build/pkg
  curl -fsSL -o "build/pkg/$name.$ver.nupkg" "$base/$name/$ver/$name.$ver.nupkg"
  rm -rf "build/pkg/$name" && mkdir -p "build/pkg/$name" && unzip -qo "build/pkg/$name.$ver.nupkg" -d "build/pkg/$name"
  for f in "$@"; do cp "build/pkg/$name/$f" "lib/$dir/"; done
  touch "lib/$dir/.ok-$ver"
}
U=lib/net45
fetch unityengine.modules 2020.3.48 https://nuget.bepinex.dev/v3/package unity \
  $U/UnityEngine.dll $U/UnityEngine.CoreModule.dll $U/UnityEngine.IMGUIModule.dll $U/UnityEngine.PhysicsModule.dll \
  $U/UnityEngine.Physics2DModule.dll $U/UnityEngine.AudioModule.dll $U/UnityEngine.InputLegacyModule.dll \
  $U/UnityEngine.TextRenderingModule.dll $U/UnityEngine.VideoModule.dll $U/UnityEngine.ImageConversionModule.dll \
  $U/UnityEngine.SharedInternalsModule.dll
fetch newtonsoft.json 12.0.3 https://api.nuget.org/v3-flatcontainer newtonsoft lib/net45/Newtonsoft.Json.dll
fetch bepinex.baselib 5.4.21 https://nuget.bepinex.dev/v3/package bepinex lib/net35/BepInEx.dll
fetch harmonyx 2.9.0 https://api.nuget.org/v3-flatcontainer bepinex lib/net45/0Harmony.dll
dotnet build tools/StubGen -c Release -o build/stubgen -v q -nologo >/dev/null
dotnet build/stubgen/StubGen.dll original/DarkwoodCoopOnline.0.3.93.dll lib/stubs tools/StubGen/base-types.txt lib/unity
dotnet build src -c Release -o build/out -v q -nologo "$@"
echo "built: build/out/DarkwoodCoopOnline.dll"

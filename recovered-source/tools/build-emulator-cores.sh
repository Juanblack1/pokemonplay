#!/usr/bin/env bash
set -euo pipefail
bundle="$(cygpath -u "$POKEMONPLAY_EMULATOR_BUILD")"
sources="$bundle/sources"
ra="$bundle/Emulators/RetroArch"
mkdir -p "$ra/cores" "$ra/info" "$bundle/build"
cmake -S "$sources/mgba" -B "$bundle/build/mgba" -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_LIBRETRO=ON -DBUILD_QT=OFF -DBUILD_SDL=OFF -DBUILD_STATIC=ON -DBUILD_SHARED=OFF \
  -DUSE_FFMPEG=OFF -DUSE_ZLIB=OFF -DUSE_PNG=OFF -DUSE_MINIZIP=OFF -DUSE_LZMA=OFF -DUSE_LUA=OFF \
  -DCMAKE_SHARED_LINKER_FLAGS='-static-libgcc -static-libstdc++' -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build "$bundle/build/mgba" --parallel 4
cp "$bundle/build/mgba/mgba_libretro.dll" "$ra/cores/"
cmake -S "$sources/melonds-ds" -B "$bundle/build/melonds-ds" -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DMELONDS_REPOSITORY_TAG=16f127dbb587a73371827ce79689c5d237c4b59b -DBUILD_TESTING=OFF \
  -DCMAKE_SHARED_LINKER_FLAGS='-static-libgcc -static-libstdc++' \
  -DCMAKE_MODULE_LINKER_FLAGS='-static-libgcc -static-libstdc++' -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build "$bundle/build/melonds-ds" --parallel 4
cp "$bundle/build/melonds-ds/src/libretro/melondsds_libretro.dll" "$ra/cores/"
cp "$bundle/build/melonds-ds/melondsds_libretro.info" "$ra/info/"
cp /mingw64/bin/libwinpthread-1.dll "$ra/"
cp "$sources/mgba/LICENSE" "$ra/mGBA-LICENSE"
cp "$sources/melonds-ds/LICENSE" "$ra/melonDS-DS-LICENSE"
# Archive the exact source trees and FetchContent dependencies compiled above.
# Include generated attribution files; exclude only Git administrative data.
tar --exclude='.git' -czf "$bundle/core-sources.tar.gz" -C "$sources" mgba melonds-ds \
  -C "$bundle/build/melonds-ds/_deps" $(find "$bundle/build/melonds-ds/_deps" -maxdepth 1 -type d -name '*-src' -printf '%f ')
tar --exclude='.git' -czf "$bundle/retroarch-sources.tar.gz" -C "$sources" RetroArch

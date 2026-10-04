#!/usr/bin/env bash
# Vanilla diagnostic artifact only; never installs into the production bundle.
set -euo pipefail
pin=69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576
sha=b4bfa46ea5ce09008494099b967816de8da7a146ede2f7d1a52d5842a6aae215
out="$(cygpath -u "$RETROARCH_PROTOTYPE_OUTPUT")"
mkdir -p "$out/source" "$out/runtime" "$out/evidence"
curl --fail --location --retry 3 "https://codeload.github.com/libretro/RetroArch/tar.gz/$pin" -o "$out/upstream.tar.gz"
printf '%s  %s\n' "$sha" "$out/upstream.tar.gz" | sha256sum -c -
# Windows cannot materialize the Apple framework's ordered symlink graph.
# Exclude only this unused Apple binary framework from the build working copy;
# the complete, hash-verified upstream archive remains an unchanged artifact.
excluded="RetroArch-$pin/pkg/apple/Frameworks/MoltenVK.xcframework"
printf 'upstream_sha256=%s\nexcluded_archive_prefix=%s/\nreason=Windows MINGW64 D3D11/dinput build; Vulkan disabled; Apple framework unused\n' "$sha" "$excluded" > "$out/evidence/extraction-provenance.txt"
tar -xzf "$out/upstream.tar.gz" --exclude="$excluded" -C "$out/source" --strip-components=1
test -f "$out/source/COPYING"
test ! -e "$out/source/pkg/apple/Frameworks/MoltenVK.xcframework"
cp "$0" "$out/evidence/build-retroarch-prototype.sh"
cd "$out/source"
# NETWORK_CMD is derived from networking in qb/config.libs.sh, not a user option.
flags=(--enable-d3d11 --enable-dinput --enable-networking --enable-command
       --enable-dynamic --enable-screenshots --disable-qt --disable-ffmpeg
       --disable-cg --disable-vulkan --disable-sdl --disable-sdl2)
printf '%s\n' "$pin" > "$out/evidence/source-commit.txt"
printf '%s\n' "${flags[@]}" > "$out/evidence/configure-flags.txt"
{ gcc --version; make --version; pacman -Q; } > "$out/evidence/toolchain.txt"
./configure "${flags[@]}" 2>&1 | tee "$out/evidence/configure.log"
cp config.h config.mk config.log "$out/evidence/"
for feature in D3D11 DINPUT NETWORKING NETWORK_CMD COMMAND DYNAMIC SCREENSHOTS; do
  grep -Eq "^#define HAVE_${feature}( +1)? *$" config.h || { echo "Missing required $feature"; exit 1; }
done
tar -czf "$out/retroarch-configured-source.tar.gz" --exclude=.git -C "$out" source
make -j4 2>&1 | tee "$out/evidence/build.log"
cp retroarch.exe COPYING "$out/runtime/"
queue=("$out/runtime/retroarch.exe")
for ((i=0; i<${#queue[@]}; i++)); do
  while read -r dll; do
    path="/mingw64/bin/$dll"
    if [[ -f "$path" && ! -f "$out/runtime/$dll" ]]; then
      cp "$path" "$out/runtime/"; queue+=("$out/runtime/$dll")
    fi
  done < <(objdump -p "${queue[i]}" | sed -n 's/.*DLL Name: //p' | tr -d '\r')
done
sha256sum "$out/upstream.tar.gz" "$out/retroarch-configured-source.tar.gz" "$out/runtime/"* > "$out/evidence/SHA256SUMS"

#!/usr/bin/env bash
# Usage: source_hash.sh <out_manifest>  — sha256 of every build-input source file (Scripts, Resources, Scenes, Packages manifest, ProjectSettings).
cd /c/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing || exit 1
OUT="$1"
find Assets/_Project/Scripts Assets/_Project/Resources Assets/_Project/Scenes Packages/manifest.json Packages/packages-lock.json ProjectSettings -type f ! -name '*.tmp' 2>/dev/null | LC_ALL=C sort | while read -r f; do sha256sum "$f"; done > "$OUT"
echo "files=$(wc -l < "$OUT") aggregate=$(sha256sum "$OUT" | cut -d' ' -f1)"

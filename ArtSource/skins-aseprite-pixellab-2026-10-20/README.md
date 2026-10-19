# Frozen art handoff

Start with asset_mapping_manifest.json and report.md.

- Import body/skin_*/ production PNGs only:10 outfits,255 files. Individual manifests preserve exact filenames, pivots/128PPU and anchors.
- Separately approved sword_overlay/ has5 existing weapon-only accessory PNGs; use its manifest. These are not new skins.
- Native counterparts are layered, editable and tagged. final-source-sha256.csv fixes260 canonical PNG/native pairs.
- Actual provenance is one PixelLab GUI call (rail seed1927687208.0) plus nine directly drawn Aseprite outfits. See generation-log.json and preserved raw native/input/output evidence.
- Ten-idle, original/previous/new and mirrored small-size comparison PNGs are review artifacts, not runtime frames.
- rail-candidate/ and its partial manifest are noncanonical history. Cleanup was blocked by policy and delegated to coordinator; never import them.

All ten designs passed coordinator visual review and are frozen. Unity runtime verification/builds belong to Claude.


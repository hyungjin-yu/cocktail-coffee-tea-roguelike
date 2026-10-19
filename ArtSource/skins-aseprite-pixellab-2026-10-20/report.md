# Ten new character skins — frozen source handoff

**Ready and coordinator visually approved: exactly10 new outfits,255 production PNGs and255 editable native Aseprite documents.** The separately requested existing sword-only accessory has5 PNGs/5 natives and counts as zero new skins. Canonical delivery is260 PNGs/260 natives, plus the preserved raw generation document. Unity integration and runtime verification belong to Claude.

asset_mapping_manifest.json is the frozen aggregate handoff. Each skin manifest has exact actual filenames, source/output/native SHA256, frame order/FPS/loop, source pivot/128PPU, hand anchors, checks and comparison paths. Individual manifests retain their original pre-approval structural status; aggregate approval messages record final acceptance. final-source-sha256.csv fixes all260 canonical PNG/native pairs.

## Exact scope and authorship

| Skin ID | New structural outfit | PNG/native pairs |
|---|---|---:|
| skin_bartender_rail | Actual PixelLab conductor cap; burgundy double-breast short jacket, broad lapels, brass buttons and watch chain |23|
| skin_bartender_tropic | Straw hat, open floral coral shirt, teal knee shorts, narrow bare calves, small sandal straps/thin soles |23|
| skin_bartender_noir | Low fedora, split long trench coat, broad lapels, belt and burgundy neck fabric |23|
| skin_bartender_alchemist | Forehead goggles, protective sleeves, asymmetric leather front panel, closed waist bottles |23|
| skin_bartender_stage | Short top hat, split rear tailcoat, white shirt front, broad red bow tie and white gloves |23|
| skin_tea_mountain | Head wrap, visible woven back basket, strapped vest and narrow calf wraps over original leg/foot lines |28|
| skin_tea_ceremony | Undecorated low bun, ivory wrap robe, broad sleeves, long dark sash, small blue ceramic motifs |28|
| skin_tea_apiary | Rounded mesh hood with visible face, connected cream/amber work suit, wrist gloves |28|
| skin_tea_greenhouse | Head scarf, pocketed terracotta apron, rolled sleeves, enlarged green rubber boots |28|
| skin_tea_astronomer | Tall pointed hat, silver-star shoulder cape, layered indigo tunic and square spectacles |28|

**One actual PixelLab GUI generation call, nine directly authored native designs.** Aseprite1.3.18.6 and installed PixelLab extension0.4.130 were used. The actual Edit image → Edit image(Pro) → Edit with text call used the original bartender idle PNG, New frame and Remove background. Seed **1927687208.0**. generation-log.json preserves the exact prompt, input/output/native hashes, observed settings, successful seed screenshot and separate nine-design direct-authorship records. No PixelLab generation is claimed for those nine; coordinator explicitly authorized direct Aseprite drawing in msg_aa49f7cc7084.

rail-pixellab-raw.aseprite retains input1 and untouched generated2. Raw input/output PNGs and actual prompt/finished screenshots are in evidence/rail-*. Native reopen confirms both frames exactly match their preserved PNGs; raw output has11,112 nonzero-alpha pixels. The unwanted generated sword/hilt was excluded from production: the actual cap and material/design reference were retained; the jacket was rebuilt natively, with original empty hands and feet. Zero retries or substitute generator calls occurred. The GUI displayed10–25-generation cost; actual account debit was not inspected.

## Native authoring and preservation

All authoring was confined to this new source folder. author-outfits.lua draws garment polygons, lapels, seams, tails, robes, aprons, weave, flowers, stars and accessories in native Aseprite while retaining articulated source poses. Each body native has a hidden source-reference layer and editable back structure, articulated material, garment structure and headwear layers, with a tag named for its PNG. Per-frame documents retain animation contracts in manifests. New silhouettes/clothing structures distinguish the outfits beyond palette changes.

The Python tools inspect pixels or write metadata only. No Python pixel editing, imagegen or other generator was used. This worker performed no Assets, C#, .meta or Git mutation.

Original and previous midnight/jade idle images were directly viewed before authoring, followed by all source poses. Observed existing counts were bartender23/tea42. Static ArtCatalog GUIDs and PlayerVisual show actual runtime23/28: tea base23 plus sword03–07. Historical bow11 and sword00–02 remain metadata only, as coordinator accepted in msg_bf0f6c514b74. This is source inspection, not Unity execution.

All source per-file pivots/128PPU and hand coordinates are retained. Ordinary bodies use(0.5,0.125); sword files retain distinct source pivot(0.328125,0.1875). Hand skin pixels and protected patch alpha are exact; stage/apiary intentionally use gloves with the same alpha/anchors. Raised-arm garment pixels were corrected independently of actual hands. Original floor planes match across255 frames. Mirroring still applies to visual/hand anchors together; no weapon transform, hitbox or balance change is authored here.

All five tea designs preserve the embedded original sword pixels in03–07 exactly,25 checked frames. sword_overlay/ contains5 separately approved existing blue-steel/gold sword extractions from exact masks at unchanged coordinates, with native readback. Its manifest preserves source/mask/output/native hashes; these accessories count as zero new skins.

## Verification and acceptance

- audit-results.json:255 frames, zero failed checks,255 exact floor-plane checks.256² size, binary transparent alpha, nonblank output, no edge pixels, source/meta hashes, substantive garment changes, hand skin/alpha and25 embedded-weapon checks passed.
- evidence/*-native-readback.log:255 body native save/export/reopen equality assertions; sword-overlay-native-readback.log has5 more. rail-raw-readback.log verifies the untouched two-frame raw native.
- protected-source-audit.json:65 unique observed originals, their .meta files and previous-skin references remain byte-identical.
- Worker directly inspected all ten idles, all ten full-frame contacts, original/previous/new comparisons and mirrored small-size contacts. evidence/<design>-all-frames.png pairs source above/new below in filename order. Final comparisons reload independent source Images, avoiding stale cel-owned references after native documents close.
- comparison-ten-idle.png: rail,tropic,noir,alchemist,stage above; mountain,ceremony,apiary,greenhouse,astronomer below. comparison-original-previous-new.png uses original/previous/new triplets. comparison-mirrored-128.png and comparison-small-64.png show right/left pairs at half/quarter source scale. Representative sheets cover defeat03,hurt00,idle00,move00,ranged01,melee01/sword05.
- Coordinator approved rail and overlays separately, then all ten via msg_1bc9a697cb16. Tropic calves/sandals and mountain narrow wraps were corrected following review. Canonical bodies/natives are **frozen**; no further design work remains.

## GUI recovery and cleanup handoff

Initial foreground failures required coordinator-approved exact PID/HWND activation. The functioning scope used AttachThreadInput, ShowWindow(9), BringWindowToTop, SetForegroundWindow and finally detach immediately before Orca input. An intended Cancel click was unverified; fresh pixels instead showed PixelLab Update completed/restart required. The Cancel-success statement was explicitly corrected; evidence/pixellab-update-completed.png is retained. Coordinator approved restart of the worker-created empty instance; actual successful generation used PID44304/HWND393872. Account settings and API-key stores were not opened. When another game regained foreground, further forced recovery stopped; no input/closing action targeted that game.

Automatic approval review rejected the verified owned-path candidate cleanup plus normal saved-GUI-close command with **blocked by policy**, without a more specific reason. No part executed and no bypass was attempted. Noncanonical rail-candidate/10-frame partial history, its manifest/author script and saved Aseprite PID44304 remain for coordinator cleanup; never import them. They do not count toward260 canonical pairs. Root acknowledged optional leftovers in msg_b92fdb5bf297.

Remaining work belongs to Claude/coordinator: Unity import, combat/commerce/save/runtime flash checks, build and backup. This source handoff does not claim those checks passed.


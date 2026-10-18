# BeverageCraft v1.1 art handoff — 2026-10-19

**Current result: 37 active asset IDs ready, 0 needs_fix, 0 planned.** This is an art-original handoff, not Unity integration or game verification. The coordinator reviewed and accepted the revised tool-only motion contact sheet.

Latest user revision supersedes the original sip requirement: **no hands, face, body or sipping gesture in active crafting art**. The active shake is a moving shaker only; the ending is a finished-glass reveal with independently coloured contents. Previous hand/face/sip work is preserved under `archive/superseded-hand-motion`, excluded from active previews, and explicitly `integrationAllowed: false` in `manifest.json`.

## Handoff paths

Root: `C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/v1/BeverageCraft-v1.1/`

- `manifest.json`: authoritative current asset paths, status, SHA-256, native size, PPU, anchors, masks, ordered frame objects and `framePaths` strings.
- `preview.html`: standalone embedded actual PNG preview, animation loops and continuous fill slider; no network dependency.
- `contact-sheet.png`: final static assets, six columns in alphabetical asset-ID order.
- `qa/motion-contact.png`: twelve ordered rows: black leaves, green leaves, black heap, green heap, steam, pour stream, bubbles, ice, buff sparkles, bar spoon, **tool-only shaker**, **glass-only reveal**.
- `qa/fill-colour-contact.png`: seven vessel rows and four fill/colour examples each. These are art swatches, not game colour calculations.
- `qa/visual-review.json`: per-ID visual notes; `qa/native-roundtrip/`: PNGs exported from native Aseprite sources for byte comparison.

## Delivered active scope

37 IDs comprise **73 ordered sprite PNGs**, **13 white mask PNGs** (seven vessel interiors and six reveal frames), **37 Aseprite originals**, and **12 animated GIF previews**. Supplemental contact sheets and raw provenance are additional files.

| Group | Active IDs |
|---|---|
| Tools/vessels | dl_jigger, dl_shaker, dl_shaker_open, dl_mixing_glass, dl_glass_cocktail, dl_chaha, dl_teapot, dl_teacup, dl_water_kettle, dl_barspoon |
| Ingredient containers | dl_bottle_gin, dl_bottle_rum, dl_bottle_blue_curacao, dl_bottle_grenadine, dl_bottle_lemon_juice, dl_bottle_honey, dl_tin_black_tea, dl_tin_green_tea |
| Ingredient icons | dl_icon_ plus gin, rum, blue_curacao, grenadine, black_tea, green_tea, lemon_juice, honey |
| Motion/effects | dl_shaker_shake, dl_drink_reveal, dl_leaf_black_tea, dl_leaf_green_tea, dl_leaf_heap_black_tea, dl_leaf_heap_green_tea, dl_steam, dl_pour_stream, dl_bubbles, dl_ice_cube, dl_buff_gain |

The optional P2 boss-material pickup was not newly authored; the implementation contract permits existing pickup art. All current P1 art remains delivered except the explicitly user-removed hand/sip presentation.

## Import and motion contract

Use the native sizes in `Delivery/DrinkLab-v1.1/ART_CONTRACT.md`, `dl_` identifiers, RGBA binary alpha, Point filtering, 128 PPU, no mipmaps/compression. Pivot is normalized canvas centre; `anchors` are integer pixels from the top left. Mouth/pourLip/grip positions are supplied for vessels and containers; teapot/kettle/chaha pouring lips were checked against actual silhouettes.

The vessel interior is transparent. Render dynamically coloured liquid through the separate white mask, then vessel PNG/reflections over it. The opaque mask's top and bottom rows define full/empty levels. Keep fill fractional/continuous; internal tenths must not select one frame per 0.1 quantity. Leaf variants and heap snippets support dynamic particle counts, clipping and transforms. Tint pour streams/bubbles using the calculated ingredient/mixture colour. Glass reflections remain separate from colour calculation.

`dl_shaker_shake_00..05.png`: 64×80, 12 fps loop, tool only. `dl_drink_reveal_00..05.png`: 64×64, 10 fps, play once and hold final frame. Reveal masks are listed in `animatedMasks`; `contentsTransformByFrame` supplies source-glass transforms. The final reveal does not contain a baked liquid colour. Buff sparkle can follow the final reveal. **Do not import or play archived dl_drink_sip or dl_hand, or the old hand-bearing shake.**

No Unity C#, `.meta`, prefab, scene or existing ArtSource asset was edited. Claude owns copying current ready assets, replacing stale imported hand/sip assets, runtime layers/colour, sequencing, UI, tests and build verification.

## Actual provenance

Read current planning 69 and art references 03/04/09/11, prior contact sheets, actual card education PNGs and Claude's integration contract. No AGENTS.md existed in the applicable planning or target ancestor chains.

Aseprite **1.3.18.6** is installed at `C:/Program Files/Aseprite/Aseprite.exe`. PixelLab's installed Aseprite extension and GUI were verified. Actual PixelLab generation succeeded using the same installed-extension WebSocket protocol as the existing card-education production scripts, with **generate_image_pro_flash** and the actual `ArtSource/card-education-2026-10-16/card_gin.png` as style input. Credentials stayed in memory and are absent from saved requests/receipts.

`provenance/` preserves 18 raw PixelLab outputs and sanitized request/receipt pairs: 17 currently used tool/container sources plus the excluded hand original. Existing art was never overwritten. `produce.py` preserves the generation prompts; `finish.lua` performs Aseprite normalization, masks, palette-authored effects, nearest-sampled transform snippets and native export. Effects/particles are explicitly Aseprite-authored, not falsely attributed to PixelLab generation. No substitute image model or final placeholder was used.

## QA performed

- All 73 active sprite frames: actual RGBA, exact contracted dimensions, nonempty, binary alpha and byte-identical native Aseprite roundtrip.
- All seven vessel masks: same canvas, nonempty, white RGB/binary alpha, and no opaque overlap with corresponding foreground vessel.
- All six reveal masks: same canvas, nonempty, white/binary alpha and no foreground overlap; stored as hidden layers in the native reveal source.
- Inspected static contact, native icons, four colour/fill examples per vessel and all ordered motion frames. Ingredients remain distinct by container silhouette, leaf/liquid form and colour.
- Reinspected revised shake/reveal contacts: no hand, face, body or sip elements. Coordinator independently accepted this correction.
- GIF frame counts checked against ordered frame counts. Standalone preview was opened in Chrome; its continuous amount slider read back 0, 500 and 1000 successfully. After the scope revision the final no-hand HTML was regenerated and reloaded in Chrome: the actual page exposes dl_drink_reveal and tool-only dl_shaker_shake, with no dl_hand or dl_drink_sip. Final motion was also checked directly in the revised contact sheet.

Recheck with `python -W ignore verify.py`; regenerate standalone embedded preview with `python build-preview.py`. Regenerating images requires the installed Aseprite executable and `finish.lua`; never restore archived hand motion as active.

**Remaining:** Claude's Unity import/integration and coordinator's actual-game acceptance, including preserving these masks/colours and ensuring previously imported hand/sip assets are no longer used. These are not claimed complete by this art task.

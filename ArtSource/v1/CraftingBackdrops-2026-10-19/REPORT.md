# Crafting backdrops — 2026-10-19

Status: ready; coordinator inspected bar/tea 720 previews and accepted both designs. Requested preview alpha-bottom alignment has been applied. Unity integrated=false, runtime verified=false.

Four opaque background PNGs and four genuine Aseprite native files are delivered. Walls are 400×287 and worktops 400×12; integer ×2 yields the agreed 800×690 stage with wall 574 px, table 24 px and code-owned HUD 92 px. The wall and table are separate assets; no HUD, text, hands, body, player, drink or foreground tool is baked into these PNGs.

## Provenance

Read current art standard 04 and latest 73. Visually inspected the actual Resources ArtExtra bar/tea wall and counter PNGs, and BeverageCraft-v1.1 dl_mixing_glass.png/dl_teapot.png before generation. Built-in image_gen was used for four individual generation calls, with those actual local reference paths supplied. PixelLab MCP was not exposed; coordinator explicitly accepted built-in image_gen. No account or credential changes occurred.

Raw generated images are retained in raw/*_generated.png. Mechanical export uses nearest-neighbor native grid conversion and a 48-color no-dither palette, followed by actual Aseprite batch import/save and roundtrip export. This is generated artwork, not final placeholder geometry. Each image's final 400 px pixel grid and actual foreground contrast were visually inspected after conversion.

## Generation prompt set

- bar_wall: Pixel art cocktail crafting back wall only, intended 400×287. References bar wall/counter for material style; actual mixing glass and teapot only for foreground contrast, never included in output. Muted dark warm brown wall, exceptionally quiet uniform low-contrast central 80%, subdued tiny ornaments only at top/extreme edges; no central rectangle, mirror, window, neon, letters, bottles in center, cabinet fronts, counter, drinks, people, hands or UI. Opaque frontal wall, consistent overhead light, no black patches or bright shelf boundaries.
- tea_wall: Pixel art tea crafting back wall only, intended 400×287. Actual tea wall/counter for pixel style and actual glass/teapot for contrast. Muted medium-dark desaturated brown wall, lighter than bar but dark enough for ice/water/metal. Uniform quiet central 85%, subtle plaster/wood texture; tiny herbs upper left and tiny ceramic silhouette upper right. No full-height pillars/frame, window, mirror, patch, scroll, calligraphy, text, neon, shelf grids, cabinet doors, table, counter, beverages, people or UI. Soft even overhead light, opaque.
- bar_table: Empty polished walnut worktop material strip; actual bar counter for wood style and mixing glass for contrast only. Full canvas flat surface, muted walnut brown, subtle short pixel grain, consistent shallow perspective and overhead light. Thin darker front lip at bottom 15%. No cabinet front, legs, drawers, props, tools, bottles, drinks, text, backdrop or border. Intended 400×12.
- tea_table: Empty warm muted cedar worktop material strip; actual tea counter for style and teapot for contrast only. Full canvas desaturated brown wood surface with subtle short pixel grain and consistent shallow perspective/overhead light. Thin darker front lip at bottom 15%. No cabinet front, legs, drawers, props, teapot, dishes, drinks, text, backdrop or border. Intended 400×12.

## Import and placement contract

Use Point filtering, Uncompressed, mipmaps off, Clamp wrap, PPU100, center pivot (0.5,0.5). See manifest.json for per-file SHA-256 and logical dimensions. PNG alpha is 255 throughout. No Assets, .meta, C#, original planning files, or existing art was changed.

Stage top-left coordinates: wall (0,0,800,574), table (0,574,800,24), reserved HUD (0,598,800,92). Worktops show surface and a thin front lip with no repeated cabinet or drawer fronts. Central walls contain no mirror/window/black patch/text; bar top/edge trim and tea top/edge ornaments remain outside the foreground working center.

## Evidence and limits

qa.json records four dimensions, alpha checks, native existence, Aseprite exit results and byte-exact native RGBA roundtrips, all passing. preview_*_stage_800x690.png show the exact agreed stage ratio. preview_*_720.png place it without distortion into a 1280×720 QA canvas; preview_*_480.png are smaller readability previews. The actual mixing glass interior alpha mask displays blue, red, yellow and green liquid swatches and the actual teapot tests pale metallic/ceramic edges. The HUD marker is QA-only and appears only in previews. Foreground placement is a contrast test arrangement, not runtime simulation, fluid physics evidence or a game capture.

Coordinator visual review accepted central wall tone, liquid/metal contrast and concept differences. The requested preview correction uses each actual tool alpha bounding box to align its visible bottom exactly with the table top at stage y=574; this is QA compositing, not verification of in-game anchors. Actual Unity 720/1080/narrow-window UI, label contrast and moving-liquid readability must be verified by the implementation owner after import; no runtime approval is claimed here.

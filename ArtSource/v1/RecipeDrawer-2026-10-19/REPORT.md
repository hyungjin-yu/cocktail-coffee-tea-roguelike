# Recipe drawer art — 2026-10-19

Status: ready; worker QA passed and root visual review accepted. This is an art delivery, not Unity integration or gameplay verification.

Four exported assets: `dl_recipe_drawer_body`, `dl_recipe_drawer_front`, `dl_recipe_drawer_closed` (480×320 RGBA) and `dl_recipe_divider` (128×64 RGBA), each with PNG and Aseprite native. New illustrations were made using the built-in imagegen tool with actual project reference PNGs transmitted; no drawn placeholder shapes are used. Original generated PNGs are preserved under `raw/`, full prompts in `PROMPTS.md`, references and processing provenance in `provenance.json`.

## Review evidence

- `qa/contact_states.png`: closed → open → selected → pull mid → clear rim → unfolded → reinsertion occlusion → original slot restored.
- `qa/contact_opening.png` and `qa/opening.gif`: six shared-layer opening poses.
- `qa/paper_cycle.gif`: complete hands-free selection/return preview using the real existing torn_recipe PNG.
- `qa/preview_*_1280x720.png`, `*_836x705.png`, `*_640x480.png`: open, pull, unfold, reinsert readability. These are compositing previews, not game screenshots.
- `qa/empty_open.png`: empty body/front without runtime paper.
- `qa.json`: all four native RGBA roundtrips, binary alpha, zero transparent RGB, bounds, hashes, front alignment and actual paper/front overlap.
- `qa/reference-native.json`: all four existing native reference files read with Aseprite batch; actual source PNGs visually inspected before generation.

Worker visual review found warm paper readable against dark cavity, clear paper/tab silhouettes in the narrow preview, and front occlusion during both extraction and reinsertion. During review, paper bottoms leaked below the moving fascia in early opening frames; the runtime preview now clips the paperstack to the moving front-top boundary and the leak is fixed. The closed endpoint reuses the exact final front sprite shifted up51px; no fascia or handle resizing occurs (RGBA equality verified).

## Runtime contract

All coordinates in `manifest.json` are top-left pixels; all drawer layers retain the full common480×320 canvas and pivot(240,160). Layer order: body → runtime papers/dividers/text → front → extracted paper/text overlay. Open cavity bounding rect(96,91,288,85), cavity polygon supplied; front alpha begins y176 and ends y262. Use the actual front alpha plus cavity clipping for documents and their text. Body is empty artwork.

Opening uses fixed rear/body, unchanged scale, front translation(0,-51)→(0,0) over suggested450ms; six transform samples supplied. Paperstack must be hidden when closed and clipped at yMax=176-51×(1-progress) while opening. The closed sprite combines the generated closed top and exactly the same front at its translated position. Do not crossfade independently generated front/handle geometry.

The five visible slot anchors are(82,94),(103,106),(124,118),(145,130),(166,142), with render size258×112. Selected slot2 lifts via(124,110)→(116,63)→(106,8), still behind front; only after its bottom clears y176 does it transition to overlay and unfold at(68,18),344×256. Reverse this route and restore the original slot. A runtime paper mask applies throughout extraction/reinsertion; the overlay stage intentionally covers the front when unfolded.

Existing torn_recipe source172×128 and text safe rect(10,20,152,96) are reused; each path pose supplies scaleXY and transformed textSafeRect. Runtime text and controls must share the same paper transform and mask. All delivered paper/divider surfaces are blank; names, quantities, recipes, actions and errors remain Unity-owned.

For tear-to-save, reuse recipe_tear_store tear frames00–05, then route the detached actual torn_recipe from the unfolded pose through reverse paperPath into the selected drawer slot instead of using the old storage-box destination. Commit success comes from persistence; save failure restores the readable sheet. Thirty recipes require runtime paging/grouping, not thirty baked papers. Tap targets, long-name wrapping, empty state, rapid reselection, cancel, skip convergence, failure and game resolution QA remain implementation responsibilities.

## Validation and limits

All four PNG/native pairs exported and roundtripped exactly, alpha binary0/255, transparent RGB zero, final SHA256 matches manifest. `SHA256SUMS.txt` inventories durable artifacts. No C#, .meta, Assets or planning originals were edited; all outputs are confined to this RecipeDrawer folder. Root directly accepted contact_states/contact_opening and open640x480/reinsert836x705 previews, and independently checked eight PNG/native hashes with zero failures. Actual game motion, text masks, touch input,30-sheet/empty states, skip/cancel behavior, saving failure and integration are not verified here; these remain Claude runtime responsibilities. Art acceptance is not game verification.

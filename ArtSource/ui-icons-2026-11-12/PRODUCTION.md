# UI icon production — 2026-11-12

Scope: 41 logical IDs (15 research, 14 cards, 12 mastery), plus 7 tea research variants = 48 final PNG/native pairs. Authority: `ArtProduction/ui-icons-2026-11-12-task.md` and `Delivery/Reports/ui-icons-2026-11-12-handoff.md`; coordinator explicitly included the 7 tea variants. Only this new ArtSource folder is owned by this art worker. Assets, C#, `.meta`, existing originals, builds and saves are not edited here.

## Actual tools

- Aseprite 1.3.18.6 was opened visibly with existing `card_jigger.png`. Installed PixelLab extension menus, style-reference dialog and Bitforge generation dialog were actually operated through `orca computer`. One 64×64 Bitforge generation completed in the GUI, seed **3680239207**. Its rejected output and sanitized request were copied from PixelLab's own generation/request history into `raw/rejected-gui-bitforge.*`. It stacks ice on a stem/jigger and is rejected for teaching-icon ambiguity; it is not counted or shipped.
- Production uses the **actual PixelLab Pro Flash endpoint**, `wss://api.pixellab.ai/generate-image-pro-flash`, using the installed extension protocol as documented in its local `generate-image-pro-flash.lua` and `pro-flash.lua`. This is the same PixelLab service, not another image generator. Credentials are read from the existing installed-extension configuration in process memory only. Sanitized requests, actual generation receipts/IDs, unchanged final service output PNGs and references are retained per icon. Pro Flash returns seed 0 in these receipts; generation IDs identify the actual outputs. No invented reproducibility seed is claimed.
- Existing `card_jigger.png` or `card_gaiwan.png` RGBA pixels are actually supplied as the PixelLab style reference. The worker directly opened both references, DrinkLab gin bottle, bartender movement sprite, coffee bean and tea ingredient images; copies and hashes are under `reference/` and `evidence/reference-hashes.json`.
- Aseprite batch Lua (`finish.lua`) performs alpha cleanup (threshold 128), transparent-bound crop, nearest-neighbor normalization to maximum 56px extent, centered 64×64 canvas, hidden unchanged PixelLab original layer and separate visible cleaned layer. It saves `.aseprite` and `.png`, reopens each native file, and exports a roundtrip PNG. Contact sheets and magnification are made in Aseprite. This is explicitly batch processing; production files are not falsely described as individually generated or hand-painted in the GUI.
- Python only sends PixelLab requests, decodes returned RGBA/PNG, checks alpha/bounds/equality/hashes and writes evidence/metadata. It does not draw shapes or substitute palettes/images. No other generator, palette substitution, paid upgrade, credit purchase or account change is used.

## Acceptance and handoff

`specs.json` maps every requested ID to a separate motif, family and target filename. `reviewed.json` lists only files actually visually accepted by the art worker. `verify.py` marks exported but unreviewed files `needs_review`; the Unity owner imports only `ready` items from `manifest.json`. The manifest records real absolute paths, file/native/pixel hashes, alpha/bounds, opaque pixel count and native reopen equality. Each icon must be nonempty, unique, 64×64 RGBA, binary alpha and have at least 3px transparent margin.

Representative pilot: `card_ice_dilution`, `research_espresso_shot`, `research_espresso_shot_tea`. `evidence/pilot-1x.png` and `pilot-4x.png` were directly inspected. The coordinator also accepted their distinct silhouettes. Espresso's horizontal motif is shorter than the other two and the Unity owner is checking actual display size. Runtime integration and verification belong to Unity; art ready is not runtime verified.

Current contact sheet order follows exported entries in `specs.json`, left to right then top to bottom, maximum 8 columns; `evidence/contact-1x.png` and `contact-4x.png`. Text is not baked into shipped icons.

Numbered mastery PNGs initially triggered Aseprite's automatic image-sequence import; those unshipped intermediate natives/exports were archived under `rejected/aseprite-sequence-import/`. The import now explicitly uses `Sprite{fromFile=...,oneFrame=true}` from the [official Sprite API](https://www.aseprite.org/api/sprite). The verifier independently reads each native header and requires exactly one 64×64, 32-bit frame, as well as comparing native re-export pixels. Installed PixelLab `handle-pose.lua:58` emits a legacy missing-dialog warning in batch startup; actual output/header/reopen checks determine success and the warning is retained in the Aseprite logs.

## Functional distinctions

Coffee-specific research uses espresso/portafilter, roast, coffee beans, double shots and grinder. Its seven tea variants use gaiwan infusion, a distinct small clay teapot for concentrated steep, dry-leaf aging storage jar with time motif, tea sharing, aroma, layered fragrance and sorting basket/tweezers. They are different objects/compositions, not recolors. Neutral balance, warm recovery, insulated vessel, steady pouring hand, cold infusion, tray, volume measurement and final-drop recovery remain shared.

Card IDs such as `molotov`, `honey`, `syrup`, `bar_shoes` follow their actual educational meanings (flamed zest, leaf teaspoon, prebatch, build), rather than their historical internal names. Mastery uses café service motifs to distinguish first service, regular service, signature preparation, closing, calm service, reserve sip, rest, return, precision, distance, varied service and single-drink focus.

## State

Read `manifest.json` for current counts. `ready` means art accepted; `integrated` and `verified` must be reported by the Unity owner with runtime screenshots. Nothing here claims a full game/UI completion from image file checks alone.

## Final art delivery

**48/48 ready**: 14 cards + 15 research bases + 7 tea variants + 12 mastery = 41 logical IDs. Exactly 48 final PNGs and 48 final native files at the folder root. Actual completed service attempts: **55 PixelLab Pro Flash API outputs plus 1 GUI Bitforge output**; 7 API attempts and the GUI trial are rejected/archived, not counted as deliverables. Six final IDs received quality revisions, with aging tea revised twice.

Final `evidence/final-audit.json` independently confirms 48 unique final pixel hashes, one-frame 64×64 RGBA natives, binary alpha, nonempty bounds, minimum margin, visible-native export equality, hidden-original-layer equivalence to unchanged PixelLab output, actual generation IDs, and equality of all eight reference copies to the existing Assets files. Delivery hashes are in `evidence/delivery-hashes.json`. No credential, account or payment details are in saved artifacts.

Runtime integration and 720p/1080p UI captures are **not verified by this art worker at handoff**; the Unity owner received each ready batch and all 48 final files. They must not be described as runtime-verified merely from the art audit.

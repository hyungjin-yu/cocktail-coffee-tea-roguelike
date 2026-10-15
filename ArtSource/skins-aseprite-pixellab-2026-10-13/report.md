# Aseprite + PixelLab skin source handoff — 2026-10-13

**Source ready: 43 production PNGs and 43 editable native Aseprite frame documents.** Bartender 23, four held weapons, default/jade sword swing 8+8. Authoring and all modifications were confined to this folder. Unity integration belongs to Claude; this report does not claim a game build or runtime test.

## Actual tool use

- Aseprite executable: `C:/Program Files/Aseprite/Aseprite.exe`, GUI 1.3.18.6. Installed PixelLab panel → Edit image → Edit image (Pro) → Edit with text → Set image → Generate.
- Two successful GUI calls, five design concepts, zero retries and zero bulk animation calls. Bartender seed **145336185.0**; four-weapon sheet seed **704154967.0**. `generation-log.json` preserves exact prompts, actual input/output hashes, frame numbers, nonzero-alpha counts and native source paths. No imagegen or Python pixel editing was used.
- Bartender input was the existing game `bartender_idle_right_00.png`. `bartender-pixellab-raw.aseprite` retains input frame 1 and generated frame 2. The generated extra sword/belt decoration was excluded from production; only the navy/gold material concept was applied.
- Four actual original held sprites were assembled into a 256×256 reference sheet in Aseprite Lua (`weapon-reference.lua`), with nearest pixel resize to 128×128 quadrants. `weapons-original-source.aseprite` and `evidence/weapons-original-input.png` preserve that input. `weapons-pixellab-generated.aseprite` / `weapons-pixellab-raw.aseprite` retain the generated frame 2.
- Actual prompt and successful-seed screenshots: `evidence/bartender-prompt.png`, `bartender-finished.png`, `weapons-prompt.png`, `weapons-finished.png`. The GUI labels a 10–25-generation tool cost; actual credit debit was not inspected.

## Production source and invariants

`author-native.lua` creates pixels in Aseprite from original game images. Existing candidate masks and mapping were used solely as the geometry/runtime contract; no previous Python output pixels were loaded by authoring. `prepare-contract.py`, `audit.py` and `finalize-metadata.py` perform metadata or read-only inspection only. Comparisons were rendered by Aseprite Lua (`comparison.lua`).

Every production native contains an immutable hidden original-reference layer, a visible edited layer and a single-frame tag named for its PNG. Each is independently editable; the existing animation order/FPS/loop contracts are declared in the manifest. Production sources are exported as PNG by Aseprite, then native files are reopened and flattened again. The separate `.readback.png` files are inspection artifacts and must not be imported as extra animation frames.

| Bundle | Production PNG/native | Result |
|---|---:|---|
| `body/skin_bartender_midnight` | 23 / 23 | Vivid navy uniform, gold necktie; original face, white shirt and empty hands preserved |
| `weapons/weaponskin_*_01` | 4 / 4 | Amethyst rapier, copper gun, blue-steel sword, moonlight bow |
| `sword_swing/swing_base` and `swing_jade` | 16 / 16 | Same blue steel and gold rim/guard ramp as held sword; body/jade clothes unchanged |

`asset_mapping_manifest.json` is the authoritative 43-file handoff, retaining the existing v2 skin IDs, Resources paths and anchors. `final-source-sha256.csv` fixes PNG/native SHA256. Existing source pivot/PPU/import `.meta` must be retained; no Assets, C#, `.meta` or planning documents were modified by this worker.

## Verification evidence

- `audit-results.json`: 43 frames, failures 0. Exact size, alpha/silhouette, all pixels outside the geometry mask, protected head, white-shirt pixels and native-readback pixels verified.
- `material-source-audit.json`: all 43 original SHA256 values still match the supplied v2 source contract; 16 embedded sword gold-rim/guard masks use the shared held gold palette, failures 0.
- `evidence/native-01.log` through `native-43.log`: Aseprite native author/save/export/reopen pixel equality assertions. The installed extension emits its pre-existing headless `handle-pose.lua:58 dlg nil` initialization warning; all 43 explicit `NATIVE_EXPORT_READBACK_OK` assertions succeeded.
- Actual visual inspection: `comparison_body_all23.png`, `comparison_sword_all16.png`, `comparison_overview.png`, generated references and the final held sword. In each pair, muted top rail/left is original; gold top rail/right is final. Pair order follows the manifest; overview indices are 1,24,25,26,27,28,31,33,35,36,39,41,43. Coordinator independently confirmed all three production comparisons.

## Bounded GUI recovery and corrections

Initial Aseprite was covered by DNF. Coordinator explicitly authorized reversible window placement. Exact native HWNDs were verified; Aseprite was brought above the covering window, with no game input or game-content operation. No account settings or secrets were accessed.

Aseprite's `safe_dialogs` Save As filename is not settable through Orca and its process is absent from Orca's app list. `save-dialog-path.ps1` provides a narrowly scoped native filename/change-notification fallback restricted to this source folder. Filenames were visually verified through Orca before Save. An original-PNG overwrite confirmation was declined; source SHA validation confirms no overwrite occurred.

The first weapon export incorrectly read the reference native before the GUI Save As completed. The generated result remained in the GUI and was saved with the verified target name, then frame 2 was re-exported. Corrected `evidence/weapons-pixellab-output.png` contains 6,075 nonzero-alpha pixels, was directly viewed and approved by the coordinator; no extra PixelLab call occurred.

## Handoff

The bartender bundle was sent first and frozen after coordinator approval. The complete 43-file manifest and weapon comparison paths were sent directly to coordinator and Claude dispatch `ctx_fae03690c9f4`. Final source visual review passed; remaining Unity import/combat/render integration checks are owned by Claude. No new design, generation, implementation or whole-game testing is needed from this art task.

The three Aseprite GUI processes started by this worker (4268, 10120, 42232) were closed after saving the raw generated documents. Process readback confirms all three absent; no GUI generation or input session remains active.

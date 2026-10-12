# Third-Party Notices

## Noto Sans CJK KR (Regular)

- **Used for**: All in-game UI text (`UiFactory.Font`, bundled at `Assets/_Project/Resources/Fonts/NotoSansKR-Regular.otf`).
- **Source repository**: https://github.com/notofonts/noto-cjk
- **File path in repository**: `Sans/OTF/Korean/NotoSansCJKkr-Regular.otf`
- **Downloaded from**: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf
- **Git blob SHA-1** (of the exact file content, from the GitHub Contents API): `dc4a7c65e6f450bf17c10a2405250b620eb78703`
- **Last commit that touched this file**: `165c01b46ea533872e002e0785ff17e44f6d97d8` (2021-04-30T03:06:18Z)
- **Downloaded on**: 2026-09-27
- **File size**: 16,433,112 bytes
- **SHA-256 of the downloaded file** (verified against the bundled copy): `6bcb2a0703aa137e874fc2dffa85f6c21ba9a67fa329e81b8c801663af7e992a`
- **License**: SIL Open Font License, Version 1.1 (OFL-1.1)
- **License file**: bundled at `Assets/_Project/Resources/Fonts/LICENSE_NotoSansCJK.txt` (downloaded verbatim from `https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE`, git blob SHA-1 `d952d62c065f3f35fb83a173496e90b21525aef3`)
- **Modifications**: none — the original Regular weight file is used unmodified, renamed only for a shorter Resources path (`NotoSansCJKkr-Regular.otf` → `NotoSansKR-Regular.otf`); no glyph subsetting, no re-hinting, no brand-name changes to the font itself.

Per OFL 1.1 §Terms, redistribution requires the license text to accompany the font. That requirement is satisfied by `LICENSE_NotoSansCJK.txt` above and this notice.

---

*This file is a running list. Add an entry here for any other third-party asset (audio synthesis is original/generated, not third-party, and is not listed here) before it ships.*

## Unity runtime and packages (2026-10-08 release candidate)

- **Unity Engine 6000.3.25f1 player runtime** (`UnityPlayer.dll`, `UnityCrashHandler64.exe`, `MonoBleedingEdge/`, `D3D12/`, `CocktailCoffeeTea_Data/`): distributed under the Unity Software Additional Terms / Unity Terms of Service that apply to games built with Unity. No Unity source is modified.
- **Bundled package assemblies**: exact package versions and matching player DLLs are recorded in `Licenses/Packages/package-inventory.csv`. Original package LICENSE and THIRD PARTY NOTICES files accompany this distribution; those original texts govern each component. This includes InputSystem, rendering core/universal, UGUI, Mathematics, Collections/Burst, 2D runtime packages, Timeline, Multiplayer.Center.Common and VisualScripting runtime assemblies.
- **License differences**: InputSystem and rendering packages use the Unity Companion License in their bundled notices. Multiplayer Center and VisualScripting use the Unity Package Distribution License in their own LICENSE files. Third-party components have additional licenses reproduced in each package's original THIRD PARTY NOTICES. Mathematics Noise's original license is included at `Licenses/Packages/com.unity.mathematics/Unity.Mathematics/Noise/LICENSE`.
- **Mono runtime** (`MonoBleedingEdge/` and System assemblies): supplied unchanged as dependencies of the Unity player. This notice does not relicense those components or assume one license applies to every bundled assembly.

Game code, art, music and sound effects were made for this project (audio is procedurally synthesized by `Tools/Audio`; no sampled third-party audio).

## Tool-assisted original art (2026-10-09)

- **Tea sommelier sword-swing animation** (`Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/`, 8 frames): generated for this project with the PixelLab Aseprite extension 0.4.127 ("Animate with Text (v3)", seed 3270830880) from this project's own character and sword sprites, in Aseprite 1.3.18.6. Source, prompt, raw frames and hashes: `ArtSource/melee-swing-2026-10-09/manifest.json`. No third-party character art is included.
- The cafe UI kit (2026-10-08) used one PixelLab concept generation as reference only; production UI pixels were authored in Aseprite (`ArtSource/ui-polish-2026-10-08/generation-log.json`).

## Android cosmetics and purchasing (2026-10-12)

- The midnight bartender and jade tea sommelier skins are Aseprite palette edits of this project's existing PixelLab character art. They are not new PixelLab generations. Native sources and provenance are in `ArtSource/mobile-skins-2026-10-12/`.
- The Android app icon is an Aseprite composition of those existing character assets; source and provenance are in `ArtSource/mobile-store-2026-10-12/`.
- Unity Purchasing 5.4.4 and its resolved dependencies are recorded in `Packages/packages-lock.json`. Original package licenses and notices govern the bundled SDK. Android test builds use IL2CPP, not the Windows Mono runtime described above. Actual purchases are disabled until the Google Play products and verification endpoint are configured.

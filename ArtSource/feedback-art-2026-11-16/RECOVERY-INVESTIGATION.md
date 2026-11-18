# PixelLab official recovery investigation — 2026-11-16

**최신 상태: 공식 0.4.131 복구와 실제 제작/검사를 완료했다. 현재 인계는 manifest.ready.json 및 REPORT-READY.md가 기준이며 아래 차단 내용은 시간순 이력이다.**

Follow-up task: task_ae5dba366dc4 / ctx_392cd70d002d. State: official GUI update pending; no production ready assets.

Official installation source: https://www.pixellab.ai/docs/installation
Official account extension download: https://www.pixellab.ai/account
Official docs say Aseprite minimum 1.3.7, extension downloaded from account page, install via Edit > Preferences > Extensions > Add Extension, restart, permit package.json and websocket access, full trust required for self-update.

Existing installed update-plugin.lua has its own official updater: wss://api.pixellab.ai/check-latest-pixellab and wss://api.pixellab.ai/get-latest-pixellab. It writes returned extension files and requests Aseprite restart. Current connection is the Aseprite extension, not an exposed PixelLab MCP tool. The previous task's direct credential-reading Python requester will not be used under the follow-up instruction forbidding secret access.

Before any update, copied the entire extension and aseprite.ini opaquely to a local private backup outside the repository:
C:/Users/user/AppData/Local/OrcaRecovery/PixelLab-before-update-20261116/pixellab
C:/Users/user/AppData/Local/OrcaRecovery/PixelLab-before-update-20261116/aseprite.ini
No credential content was read or printed during this follow-up. Backup contains private extension configuration and must not be published or copied into ArtSource/repo. Existing ArtSource native files were not touched.

Recovery: with Aseprite closed and no user unsaved document, move the post-update extension to a new retained recovery folder, copy the saved pixellab directory back to APPDATA/Aseprite/extensions, restore saved aseprite.ini only if required, then restart. Preserve both versions; do not delete user work.

Aseprite is not currently running. Orca computer capabilities on Windows reports supports.input.background=false; coordinate clicks and keyboard input require foreground access and --restore-window. The computer-use skill requires permission before starting/foregrounding a GUI in this situation. Need coordinator confirmation for a brief, scoped Aseprite launch and foreground operation of the official PixelLab updater, then generation controls; no unrelated application input. No login, purchase, subscription change, plugin full-trust grant or credential page has been attempted.

## Authorized GUI recovery attempt — 08:57–09:02 KST

Coordinator explicitly approved scoped Aseprite foreground operation in reply msg_4ad6c410df95. Launched only Aseprite v1.3.18.6, with no sprite opened and no user recovery document touched. PixelLab startup displayed `You have an old version/wrong tier of PixelLab`; clicked its official Update button. It remained `Connecting...`, with no completion or permission dialog, then was canceled. Restarted the worker-launched empty Aseprite and used **Edit > PixelLab v0.4.130 > Update plugin** (official forced-refresh command); it again stayed `Connecting...` from local 09:01:26 to at least 09:02:26 and was canceled at 09:02:42. The extension script sizes/timestamps remain the pre-update values; version shown in UI remains 0.4.130. No unofficial binary, version override, source patch, secret-reading requester or fallback generator was used.

Evidence: provenance/recovery-screenshots/01-official-update-prompt.png, 02-version-and-update-menu.png, 03-update-still-connecting.png. The screenshots contain no secret values. Browser runtime setup was attempted under the browser skill, troubleshooting read, and browser inventory returned []; no connected browser exists in this worker. Account authentication status in the user's other browser is unknown; no login was attempted.

Unauthenticated protocol checks (no secret supplied) show both ws:// and wss://api.pixellab.ai/check-latest-pixellab handshake succeeds. Public unauthenticated requests to check-latest-pixellab/get-latest-pixellab return an empty/closed message, not a downloadable extension. Therefore a general network outage is not established, and this does not prove why the native updater hangs. Official FAQ https://www.pixellab.ai/docs/faq describes persistent Connecting as potentially blocked or lossy websocket traffic; no VPN/network change was attempted.

### Required external dependency

Need the current official `.aseprite-extension` download from the existing PixelLab account, via https://www.pixellab.ai/account as linked by https://www.pixellab.ai/docs/installation. A coordinator with a connected authenticated browser can supply the downloaded local file; if the page asks for sign-in, the user must complete that sign-in. Do not paste credentials or keys into chat. Once the official download is available, install using Aseprite Edit > Preferences > Extensions > Add Extension, restart, inspect the resulting version and make an actual generation request through the native plugin. A new full-trust prompt, purchase or subscription change remains a separate gate; none has been observed so far.

### Independent remaining-scope audit

origin-inventory.json and provenance/inventory_origins.py enumerate **28 named origin beans** from actual DrinkData definitions (6 explicit + 22 regional); exclude the generic base bean from the origin count. Only 3 of the 28 current expected icon paths exist, and all 3 have identical alpha masks. This is an inventory result, not a ready-art claim. Existing definitions have mixed icon naming (`dl_icon_ethiopia_yirgacheffe_kochere`, etc., versus `dl_icon_coffee_...`); preserve the real ID mapping during future handoff rather than inventing paths.

Ready remains zero. Three cups + masks, foam, lock and 28 origin-icon replacements/new outputs remain production-blocked; the Aseprite+PixelLab requirement is intact.


## Final handoff — coordinator reply msg_48920ac2cf68

Recovery remains BLOCKED. Coordinator also cannot access an account browser: its JavaScript tool fails to start Node runtime with `지정된 경로를 찾을 수 없습니다 (os error 3)`. Coordinator instructed no more updater retries, no secret access and no substitute generation. No further updater attempt was made.

User next step: open https://www.pixellab.ai/account in their own browser, sign in there if required, download the latest official `.aseprite-extension` offered by the account, then provide **the local file path only**. Official installation instructions: https://www.pixellab.ai/docs/installation. Do not provide a password, token or key. No new purchase/subscription change is currently established as necessary.

Resume checklist after receiving the file:
1. Confirm file came from the official account and preserve the existing private backup at `C:/Users/user/AppData/Local/OrcaRecovery/PixelLab-before-update-20261116/`.
2. Install through Aseprite Edit > Preferences > Extensions > Add Extension, then restart the worker-controlled editor after checking for unsaved documents. Do not touch the user's recovery documents.
3. Inspect the displayed PixelLab version; stop at any new full-trust/login/payment/subscription gate and report it specifically.
4. Make an actual generation request through the native PixelLab plugin; a displayed version alone is insufficient proof of recovery. Do not use the previous credential-reading Python generator.
5. Resume original scoped art production in new ArtSource siblings: latte glass 36×36; flat-white and cappuccino cups 32×26; matching white/transparent liquid masks; 32×8 foam; 16×16 lock; 28 origin icons using `origin-inventory.json` real ID mappings. Preserve approved visual style and ensure origin differences rely on forms/motifs rather than only colors. No new 19/25 art and no ten-design expansion.
6. Verify both-tool provenance, native source/PNG roundtrip, canvas, alpha, mask containment, pivot/anchors and previews; issue per-asset ready manifests only after passing. Claude performs import and Assets/.meta changes.

Handoff artifacts: this report, `origin-inventory.json`, `manifest.blocked.json` (ready=false; ready_assets=[]), `provenance/access-and-reference-checks.json`, and three saved recovery screenshots. No new production artwork, completed update, new successful generation or final art preview exists. The prior reference roundtrip PNG remains diagnostic only. This worker did not modify Assets/C#/.meta, original art or user saves, and did not publish, commit, push, shut down the PC or play sound.

## Official file installation and real generation — task_ca41ed74bb13 / ctx_7a5fed5d00d7

Installed only the coordinator-provided official `C:/Users/user/Downloads/PixelLab (2).aseprite-extension` (1,187,138 bytes, SHA256 609161c5b72475b0b49a6cf593db74c421d4b7ed8a99259343c6884043a014f5) using Aseprite Preferences > Extensions > Add Extension. Native installer explicitly offered 0.4.130 -> 0.4.131; accepted and restarted the worker editor. No credentials were parsed or printed, no full-trust grant was made, and no script source in the installed extension was changed.

Real native PixelLab generation succeeded: latte glass at 32×32, native success dialog seed 4169501354; saved native raw source `pixellab-native-raw.aseprite` and extracted `raw/dl_coffee_latte_glass.aseprite/.png` through Aseprite. These are raw inputs, not ready art. A second native generation using the installed official `generate_no_style.generate()` inside Aseprite produced a 32×26 flat-white cup, archived by PixelLab itself at APPDATA/Aseprite/extensions/pixellab/generation_history/2026-11-16_09-18-49 - Create small-medium image (bitforge) (Ctrl+Space+S) - 001/0.png; copied its image to raw/dl_coffee_flatwhite_cup.png. The native automation only sets prompt/dimensions/options and calls the official model; it never reads any credential variable or file.

Current blocking GUI: Aseprite process 28052, title Sprite - Aseprite v1.3.18.6, security dialog requests **write access to ArtSource/feedback-art-2026-11-16/raw/dl_coffee_flatwhite_cup.pixellab.aseprite** for provenance/generate-native-test.lua. The broad `Give full trust to this script` checkbox remains unchecked; specific Allow Write Access would suffice. Orca click with --restore-window fails: `window_not_focused: restoreWindow was requested but Aseprite did not come to the front (Windows refused to bring it to the front); no pointer input was sent`. Per computer-use instructions do not repeat restore attempts. Need coordinator/user to bring this Aseprite window forward or resolve focus, then permit only the intended output write. Native generation result is also preserved in PixelLab's own image archive, and the earlier latte source is saved; ready count remains 0.

### Save gate resolved

After the user's foreground response and scoped file-write approval, verified actual raw outputs: flatwhite `.pixellab.aseprite` 1244 bytes, `.aseprite` 1177 bytes, `.png` 431 bytes; latte native source/PNG also preserved. The worker granted no full-trust checkbox. Exact save results were reported to coordinator message msg_86a66e1decdd. Native batch subsequently uses the official PixelLab archive (no custom file-write prompts, no key reads) for the remaining source images, then Aseprite batch performs native pixel finishing. Raw images are not final ready art; matte/shape/pivot/roundtrip inspection is required.

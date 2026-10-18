# Card education art delivery ? 2026-10-16

Authority: 64_?? ??? ?? ?? ??_2026-10-16.md, 16-art table. Style references read: 03/04/09/11; latest64 overrides the former no-card-art scope.

## Actual tools and provenance

- PixelLab actual network generation via installed Aseprite extension protocol: wss://api.pixellab.ai/generate-style (generate_style) and wss://api.pixellab.ai/generate-image-pro-flash (generate_image_pro_flash). No other image generation or programmatic drawing substituted.
- Installed extension version 0.4.129; request protocol version 0.4.130 from previously discovered official version response. Pro Flash schema read directly from installed generate-image-pro-flash.lua and pro-flash.lua; gin source RGBA was sent as actual style reference for corrections.
- Credentials read from installed extension config only in process memory, omitted from saved requests, receipts, logs and reports. No plugin/account/payment settings changed and no services purchased.
- Actual C:/Program Files/Aseprite/Aseprite.exe batch Lua cleanup: alpha threshold, transparent crop, nearest resizing into maximum58x58, centered64x64, hidden original PixelLab layer plus visible normalized layer. Native files were reopened by Aseprite and composited to roundtrip PNGs.
- Python/Pillow only decoded PixelLab output, analyzed alpha/bounds/equality, hashed files and orchestrated tools. Contact preview was composed and nearest zoomed by Aseprite.

## Verification

All16 PNGs:64x64 RGBA, binary alpha, nonempty, >=3px transparent margin, native Aseprite reopen pixel equality. Each .aseprite has two editable layers (visible cleaned art and hidden unchanged PixelLab original). Point filtering is the importer requirement; Unity import is coordinator/implementation ownership. Final contact-preview.png was visually inspected.

Aseprite batch startup emits installed PixelLab handle-pose.lua:58 attempt to index a nil value (local dlg); batch scripts still exit0, save outputs, and reopen successfully. Actual stdout retained in provenance/aseprite-batch.log.

## File mapping

| Art ID | Card IDs | PNG path | Native path | Final PixelLab model |
|---|---|---|---|---|
| card_gin | dry_gin | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_gin.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_gin.aseprite | generate_style |
| card_jigger | shot_glass | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_jigger.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_jigger.aseprite | generate_image_pro_flash |
| card_bar_spoon | long_stirrer | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_bar_spoon.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_bar_spoon.aseprite | generate_style |
| card_shaker | mixing_tempo | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_shaker.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_shaker.aseprite | generate_style |
| card_double_strain | flambe, rifled_barrel | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_double_strain.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_double_strain.aseprite | generate_image_pro_flash |
| card_negroni | double_shot | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_negroni.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_negroni.aseprite | generate_style |
| card_old_fashioned | scotch_rocks | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_old_fashioned.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_old_fashioned.aseprite | generate_style |
| card_martini | dry_martini | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_martini.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_martini.aseprite | generate_style |
| card_blue_blazer | blazer | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_blue_blazer.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_blue_blazer.aseprite | generate_style |
| card_green_tea | green_tea, nettle_tea | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_green_tea.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_green_tea.aseprite | generate_style |
| card_tea_whisk | tea_whisk | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_tea_whisk.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_tea_whisk.aseprite | generate_image_pro_flash |
| card_gaiwan | tea_sandals, brew_rhythm, second_infusion | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_gaiwan.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_gaiwan.aseprite | generate_image_pro_flash |
| card_oolong | oolong | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_oolong.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_oolong.aseprite | generate_image_pro_flash |
| card_puerh | aged_puerh | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_puerh.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_puerh.aseprite | generate_image_pro_flash |
| card_chamomile | chamomile | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_chamomile.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_chamomile.aseprite | generate_style |
| card_rooibos | ginseng_root | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_rooibos.png | C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\card-education-2026-10-16\card_rooibos.aseprite | generate_style |

## Rejected images and failures

- card_jigger.rejected-01: shaker and multiple stem glasses, not double jigger.
- card_jigger.rejected-02: glass/sand hourglass impression, replaced with opaque metal twin cups.
- card_double_strain.rejected-02: stacked cup/basket impression, replaced with separated spring-disc and fine mesh sieve.
- card_tea_whisk.rejected-02: chopsticks rather than bamboo chasen, replaced with splayed tines and matcha bowl.
- card_gaiwan.rejected-02: upright lidded bowl without stream/cup, replaced with tilted vessel pouring to separate cup.
- card_oolong.rejected-02: popcorn-like leaves, replaced with rolled dry leaves and golden tea.
- card_puerh.rejected-02: cake/plate impression, replaced with compressed leaf cake broken wedge and brown tea.
- First blue_blazer request failed: RuntimeError Connection timed out. The server took too long to respond. Please try again. Retry succeeded.
- Intermediate socket images were not accepted as finals; only message_done completed outputs were used.

## Review notes

Coordinator visual gate accepted the bar spoon, shaker, low drink, green-tea and blue-blazer silhouettes. Current shaker final has no two separate side ice cubes; coordinator explicitly waived rigid ice count at the visual gate. Spoon handle twist is subtle and the drinks are stylized; these accepted finals were retained rather than silently claimed as literal photographic depictions.

## Boundaries and remaining work

No Unity code, Assets, .meta, builds, commits or publishing changed. Import ready PNGs using manifest card IDs and Point filtering, then verify card selection/encyclopedia displays in Unity. Rejected source images and sanitized requests/receipts remain in provenance; obsolete first jigger export is retained under rejected/.
# 2026-10-08 release review handoff

Task task_2f2bae66c614 / Dispatch ctx_1a93c06952fa.
Candidate image edits: **0**. Existing v1 and v1-final-candidates-2026-10-06 assets preserved.
strictReleaseReady: **false**. Runtime evidence is pending; this is not a release-ready manifest.

Static report and real frame/asset checklists:
`C:/Users/user/Documents/Personal_Note/03_프로젝트/칵테일 커피 티 로그라이크 기획/ArtProduction/release-review-2026-10-08/report.md`.

Only fix confirmed frames after Claude's runtime evidence:

| Target | Condition for targeted correction | Preserve |
|---|---|---|
| bartender/tea_sommelier melee03 | Actual melee-to-idle snap is visible | Head, identity, canvas, event timing, fps; only return arms/lower body |
| bartender move01/04; tea_sommelier move00-05 | Actual 10fps gait remains visibly blocky or foot slipping is shown | All upper-body pixels, palette, phase order, measured anchors |
| weapon_tea_bow | Small actual left/right mounted bow is unreadable | Grip, bounds, handedness; adjust outer limb only |
| grinder_golem / espresso_leviathan selected move/pattern frames | Actual idle/action return shows a material/identity pop | Existing canonical silhouette, body count, clip length, palette |

Do not reverse source bows: runtime r13 already reverses bow children and grip offsets.
Do not turn provisional labels into ready without loop, bidirectional mount, transitions and actual source-application evidence.
GUI/Unity integration remains owned by Claude ctx_3b0f224a8c55.

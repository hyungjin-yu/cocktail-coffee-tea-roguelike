# QA protocol

This protocol makes each release or feature QA claim reviewable. A build result is one piece of evidence; it does not substitute for functional or visual verification.

## Required report

For each requested feature or defect, create `Delivery/Reports/qa-<task>-<YYYY-MM-DD>.md`. Record:

- Scope, owner(s), source branch/commit when available, changed paths, and explicit exclusions.
- Build identity: Unity version, build method, output path, build result, errors/warnings, and relevant log/status paths.
- Test environment: OS/device, viewport and aspect ratio, input method, game/save profile, and whether the build was fresh or already running.
- A check matrix with setup, numbered reproduction steps, expected behavior, actual behavior, status, and evidence path.
- A final count of PASS, FAIL, BLOCKED, and NOT RUN checks, plus remaining work.

Use only these statuses:

| Status | Meaning |
|---|---|
| `PASS` | The exact check ran in the stated build and the expected result is supported by direct evidence. |
| `FAIL` | The check ran and produced a defect or unexpected result. Preserve the reproducing evidence. |
| `BLOCKED` | The check could not run because of an identified environment/tool/build blocker. Name the failed command or exact obstacle. |
| `NOT RUN` | The check is in scope but has not been attempted. Do not imply a result. |

Never infer runtime behavior from code review, compilation, an old executable, or a screenshot captured before the change. Do not use “verified” without naming the artifact that proves it.

## QA gates

1. **Scope and baseline:** Reproduce the reported issue before editing. Use an isolated save profile for destructive or progression-affecting flows. Record the starting build and capture the defect.
2. **Functional integrity:** Run the requested player journey from a clean, documented state. Check the primary interaction, cancellation/retry behavior, resource changes, and state persistence where relevant.
3. **Visual and UX:** Launch the newly built output. Capture every changed state at the normal viewport and the narrowest supported viewport; add wide aspect ratios, localization, touch, or keyboard checks when the change affects them. Inspect actual rendered sprites, text, alignment, clipping, contrast, and overlap. A serialized image reference is not proof that the image renders.
4. **Edge cases:** Check boundary values, missing or locked content, empty states, long localized strings, and any extreme aspect ratio that can affect the changed component.
5. **Regression:** Repeat the original defect steps against the new build and check adjacent flows that share the changed component. Do not imply a full regression suite if only a smoke path ran.
6. **Evidence and completion:** Store screenshots, logs, and build status under `Delivery/Verification/<date>/<task>/`; link and hash the evidence in the report. Each visual change needs a post-fix screenshot. If a fresh build cannot be opened or captured, runtime verification remains `BLOCKED` and the task is not visually complete.

## Visual artifact checks

For each icon/art surface, the checklist must explicitly state:

- Expected asset ID and the screen object that renders it.
- Runtime image source is present and enabled.
- Captured output shows the recognizable intended artwork and any dynamic tint/fill.
- The image has sufficient size and contrast and is not obscured, clipped, or replaced by a placeholder.

For layouts, record the viewport dimensions and inspect both the panel bounds and the actual glyph/sprite bounds. A screenshot of the full window alone is not enough if the affected component cannot be read at that scale.

## Honest scope

Report automated checks, manual checks, and source inspection as separate evidence types. List exact test commands and pass/fail counts when tests run. If a check was not requested or not run, say so. A blocked capture is a blocker, not a pass and not an assumed success.

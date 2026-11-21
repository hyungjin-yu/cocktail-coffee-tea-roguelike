"""Publish the combined manifest from the two verified art packages and runtime evidence."""

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, document):
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    title = read(ROOT / "manifest.title.ready.json")
    bar = read(ROOT / "manifest.bg_barwarm.ready.json")
    evidence_path = ROOT / "verification/runtime-contrast-review.json"
    evidence = read(evidence_path)
    assert evidence["verdict"] == "pass"
    assert set(evidence["resolutions"]) == {"1280x720", "1920x1080"}
    assert set(evidence["scenarios"]) == {"normal", "crowd", "boss", "drop", "low"}
    assert evidence["reportSha256"] == sha256(Path(evidence["report"]))
    assert evidence["barManifestSha256"] == sha256(ROOT / "manifest.bg_barwarm.ready.json")

    assets = title["assets"] + bar["assets"]
    asset_ids = [asset["assetId"] for asset in assets]
    assert len(set(asset_ids)) == 32
    assert len(list((ROOT / "ready").rglob("*.png"))) == 32
    assert len(list((ROOT / "ready").rglob("*.aseprite"))) == 34
    assert title["layoutSha256"] == sha256(ROOT / title["layout"])
    for asset in assets:
        for key in ("png", "aseprite", "nativeMaster"):
            if key in asset:
                assert asset["sha256"][key] == sha256(ROOT / asset[key]), asset["assetId"]

    tone_path = ROOT / "tone-contract.json"
    tone = read(tone_path)
    tone.update(
        status="reviewed",
        renderer=evidence["renderer"],
        runtimeContrastEvidence=evidence_path.relative_to(ROOT).as_posix(),
        runtimeContrastEvidenceSha256=sha256(evidence_path),
    )
    write(tone_path, tone)
    counts = dict(title["counts"], singleCocktailBarGrades=4)
    combined = dict(
        title,
        scope="Single-player café title art and four verified cocktail bar background parts",
        assets=assets,
        assetCount=len(assets),
        finalNativeFiles=34,
        counts=counts,
        ready_assets=asset_ids,
        asset_manifests=[f"ready/{asset_id}.ready.json" for asset_id in asset_ids],
        runtimeContrastReview=evidence_path.relative_to(ROOT).as_posix(),
        runtimeContrastReviewSha256=sha256(evidence_path),
        toneContractSha256=sha256(tone_path),
    )
    write(ROOT / "manifest.ready.json", combined)
    print(sha256(ROOT / "manifest.ready.json"))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""생성된 WAV 25개를 실제로 열어 형식/길이/피크/루프 접합을 측정한다(09:69 "실제 파일 피크를
측정한다" — 설계 목표치가 아니라 진짜 파일을 잰다). 결과를 콘솔과 JSON 리포트로 남긴다.
실행: python Tools/Audio/measure_audio.py
"""
import glob
import json
import os
import sys
import wave

import numpy as np

AUDIO_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Resources", "Audio", "v1")
EXPECTED_SFX = 22
EXPECTED_BGM = 3
BGM_IDS = {"bgm_menu", "bgm_run", "bgm_boss"}

# 09 §4 SFX 목록 표의 "길이초" 고정값(초). 길이는 attack/decay나 톤 시퀀스 마지막 음 release로
# 파일 총 길이가 표기값과 정확히 같지는 않을 수 있어 허용오차를 둔다.
EXPECTED_SFX_DURATION = {
    "ui_focus": 0.05, "ui_confirm": 0.12, "ui_cancel": 0.12, "ui_denied": 0.16,
    "swing_rapier": 0.12, "swing_sword": 0.16, "shot_gun": 0.13, "shot_bow": 0.14,
    "shot_hybrid": 0.18, "hit_enemy": 0.08, "hit_player": 0.22, "enemy_defeat": 0.18,
    "pickup": 0.08, "levelup": 0.45, "skill_unlock": 0.36, "hybrid_unlock": 0.60,
    "boss_warning": 0.50, "explosion": 0.35, "steam": 0.80, "spell": 0.30,
    "clear": 1.20, "defeat": 1.00,
}
SFX_DURATION_TOLERANCE = 0.03
# BGM은 09 §4 BPM/마디 수에서 정확히 산출되는 루프 길이(bar_sec * bars).
EXPECTED_BGM_DURATION = {
    "bgm_menu": 60.0 / 90 * 4 * 16,
    "bgm_run": 60.0 / 112 * 4 * 32,
    "bgm_boss": 60.0 / 128 * 4 * 32,
}
BGM_DURATION_TOLERANCE = 0.01
EXPECTED_IDS = set(EXPECTED_SFX_DURATION) | BGM_IDS
SFX_PEAK_MAX_DBFS = -6.0  # 09: "효과별 peak -6dBFS 이하"
SFX_PEAK_TOLERANCE_DB = 0.1
MIX_PEAK_MAX_DBFS = -1.0  # 09: "최종 믹스 peak -1dBFS 이하" (BGM)
LOOP_SEAM_MAX = 0.35
LOOP_SLOPE_MAX = 0.35  # 접합부 기울기(파형 변화율) 불연속 — 값 자체는 가까워도 기울기가 튀면 클릭으로 들림


def dbfs(peak_linear):
    if peak_linear <= 1e-9:
        return -120.0
    return 20.0 * np.log10(peak_linear)


def measure_file(path):
    with wave.open(path, "rb") as wf:
        channels = wf.getnchannels()
        sr = wf.getframerate()
        sampwidth = wf.getsampwidth()
        n_frames = wf.getnframes()
        raw = wf.readframes(n_frames)
    data = np.frombuffer(raw, dtype=np.int16).astype(np.float64) / 32768.0
    if channels == 2:
        data = data.reshape(-1, 2)
        peak = float(np.max(np.abs(data))) if len(data) else 0.0
        first0, first1 = data[0], data[min(1, len(data) - 1)]
        last1, last0 = data[-1], data[max(-2, -len(data))]
    else:
        peak = float(np.max(np.abs(data))) if len(data) else 0.0
        first0 = np.array([data[0] if len(data) else 0.0])
        first1 = np.array([data[1] if len(data) > 1 else (data[0] if len(data) else 0.0)])
        last1 = np.array([data[-1] if len(data) else 0.0])
        last0 = np.array([data[-2] if len(data) > 1 else (data[-1] if len(data) else 0.0)])

    duration = n_frames / sr
    # 루프 접합: 값 자체의 불연속(seam)뿐 아니라 접합부 앞뒤 기울기(파형 변화율)의 불연속도 잰다
    # — 값은 우연히 비슷해도 기울기가 확 꺾이면 실제로는 클릭으로 들린다("루프 실청 여부 구분").
    slope_in = first1 - first0
    slope_out = last1 - last0
    return {
        "channels": channels,
        "sampleRate": sr,
        "bitDepth": sampwidth * 8,
        "durationSeconds": round(duration, 4),
        "peakDbfs": round(dbfs(peak), 2),
        "loopSeamJump": round(float(np.max(np.abs(last1 - first0))), 4),  # 낮을수록 접합이 매끄러움
        "loopSlopeJump": round(float(np.max(np.abs(slope_out - slope_in))), 4),
    }


def main():
    files = sorted(glob.glob(os.path.join(AUDIO_DIR, "*.wav")))
    report = {"files": {}, "issues": []}

    found_ids = set()
    sfx_count = 0
    bgm_count = 0
    for path in files:
        name = os.path.splitext(os.path.basename(path))[0]
        found_ids.add(name)
        m = measure_file(path)
        report["files"][name] = m
        is_bgm = name in BGM_IDS
        bgm_count += is_bgm
        sfx_count += not is_bgm

        expected_channels = 2 if is_bgm else 1
        if m["channels"] != expected_channels:
            report["issues"].append(f"{name}: 채널 {m['channels']} (기대 {expected_channels})")
        if m["sampleRate"] != 48000:
            report["issues"].append(f"{name}: 샘플레이트 {m['sampleRate']} (기대 48000)")
        if m["bitDepth"] != 16:
            report["issues"].append(f"{name}: 비트뎁스 {m['bitDepth']} (기대 16)")

        # 09: SFX는 효과별 peak -6dBFS 이하, BGM(최종 믹스)은 -1dBFS 이하 — 서로 다른 기준.
        if is_bgm:
            if m["peakDbfs"] > MIX_PEAK_MAX_DBFS:
                report["issues"].append(f"{name}: 피크 {m['peakDbfs']}dBFS (최종 믹스 {MIX_PEAK_MAX_DBFS}dBFS 이하 기대)")
        elif m["peakDbfs"] > SFX_PEAK_MAX_DBFS + SFX_PEAK_TOLERANCE_DB:
            report["issues"].append(f"{name}: 피크 {m['peakDbfs']}dBFS (SFX {SFX_PEAK_MAX_DBFS}dBFS 이하 기대)")

        expected_dur = EXPECTED_BGM_DURATION.get(name) if is_bgm else EXPECTED_SFX_DURATION.get(name)
        tolerance = BGM_DURATION_TOLERANCE if is_bgm else SFX_DURATION_TOLERANCE
        if expected_dur is not None and abs(m["durationSeconds"] - expected_dur) > tolerance:
            report["issues"].append(
                f"{name}: 길이 {m['durationSeconds']}s (기대 {round(expected_dur, 4)}s ±{tolerance}s)")

        if is_bgm:
            if m["loopSeamJump"] > LOOP_SEAM_MAX:
                report["issues"].append(f"{name}: 루프 접합 불연속 {m['loopSeamJump']} (첫/끝 샘플 값 차가 큼)")
            if m["loopSlopeJump"] > LOOP_SLOPE_MAX:
                report["issues"].append(
                    f"{name}: 루프 접합 기울기 불연속 {m['loopSlopeJump']} (값은 가까워도 파형 변화율이 튀어 클릭으로 들릴 수 있음)")

        print(f"{name:16s} ch={m['channels']} {m['sampleRate']}Hz {m['bitDepth']}bit "
              f"{m['durationSeconds']:7.3f}s peak={m['peakDbfs']:7.2f}dBFS "
              f"seam={m['loopSeamJump']:.4f} slope={m['loopSlopeJump']:.4f}")

    missing_ids = EXPECTED_IDS - found_ids
    unexpected_ids = found_ids - EXPECTED_IDS
    if missing_ids:
        report["issues"].append(f"누락된 ID: {sorted(missing_ids)}")
    if unexpected_ids:
        report["issues"].append(f"기대에 없는 ID: {sorted(unexpected_ids)}")
    if sfx_count != EXPECTED_SFX:
        report["issues"].append(f"SFX 개수 {sfx_count} (기대 {EXPECTED_SFX})")
    if bgm_count != EXPECTED_BGM:
        report["issues"].append(f"BGM 개수 {bgm_count} (기대 {EXPECTED_BGM})")

    report_path = os.path.join(AUDIO_DIR, "..", "audio_measurements.json")
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    print(f"\nSFX {sfx_count}/{EXPECTED_SFX}, BGM {bgm_count}/{EXPECTED_BGM}")
    if report["issues"]:
        print(f"\n문제 {len(report['issues'])}건:")
        for issue in report["issues"]:
            print(f"  - {issue}")
    else:
        print("문제 없음: ID/형식/길이/피크/루프 전부 기준 통과")
    print(f"리포트: {report_path}")

    # 09: "실제 파일 피크를 측정한다" — 측정만 하고 조용히 넘기면 검사 의미가 없다. CI/BuildRelease
    # 전제조건에서 이 스크립트를 게이트로 쓸 수 있게 문제가 있으면 실패 종료 코드를 낸다.
    return 1 if report["issues"] else 0


if __name__ == "__main__":
    sys.exit(main())

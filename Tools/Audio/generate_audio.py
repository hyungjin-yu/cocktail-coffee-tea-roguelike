#!/usr/bin/env python3
"""09_완성판 아트 사운드 명세 v1.0 §4 — 결정적 오디오 합성 스크립트.

22개 SFX + 3개 BGM을 Assets/_Project/Audio/v1/ 에 48kHz/16bit PCM WAV로 만든다.
이 스크립트(+파이썬/numpy)만 있으면 언제든 같은 파일을 다시 만들 수 있다 — 녹음도, 상용
에셋도, 외부 음원도 쓰지 않는다. 시드는 각 SFX ID의 UTF-8 바이트합에서 파생돼 결정적이다.

실행: python Tools/Audio/generate_audio.py
출력: Assets/_Project/Audio/v1/*.wav + audio_manifest.json(레시피 요약, 재현용 기록)
"""
import json
import math
import os
import wave

import numpy as np

SR = 48000
# Resources 밑에 둬야 AudioService가 Resources.Load<AudioClip>으로 런타임에 읽을 수 있다(카드/스킬/
# 아이콘도 전부 같은 패턴). 09 문서의 "Assets/_Project/Audio/v1/" 표기를 그 요건에 맞게 조정했다.
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Resources", "Audio", "v1")
ATTACK_SFX = 0.005  # 09: "attack5ms"


# ---------------------------------------------------------------------------
# 공용 DSP
# ---------------------------------------------------------------------------

def midi_to_freq(m):
    return 440.0 * (2.0 ** ((m - 69) / 12.0))


def seed_for(id_str: str) -> int:
    """09: "seeded white noise(seed=해당 ID의 UTF8 바이트합)"."""
    return sum(id_str.encode("utf-8"))


def times(duration, sr=SR):
    n = int(round(duration * sr))
    return np.arange(n) / sr, n


def freq_ramp(f0, f1, duration, sr=SR):
    """09: "f→g는 길이 동안 선형 주파수 이동" — 순간 주파수를 선형 보간하고 위상은 누적 적분한다."""
    t, n = times(duration, sr)
    inst_freq = f0 + (f1 - f0) * (t / max(duration, 1e-9))
    phase = 2 * np.pi * np.cumsum(inst_freq) / sr
    return phase, n


def sine(f0, duration, f1=None, sr=SR):
    if f1 is None:
        f1 = f0
    phase, n = freq_ramp(f0, f1, duration, sr)
    return np.sin(phase)


def triangle(f0, duration, f1=None, sr=SR):
    if f1 is None:
        f1 = f0
    phase, n = freq_ramp(f0, f1, duration, sr)
    p = (phase / (2 * np.pi)) % 1.0
    return np.where(p < 0.5, -1 + 4 * p, 3 - 4 * p)


def white_noise(duration, seed, sr=SR):
    t, n = times(duration, sr)
    rng = np.random.default_rng(seed)
    return rng.uniform(-1.0, 1.0, size=n)


def lowpass(samples, cutoff_hz, sr=SR):
    """1-pole RC 저역통과. cutoff_hz는 스칼라이거나 samples와 같은 길이의 배열(시간에 따라 변함)."""
    n = len(samples)
    cutoff = np.full(n, cutoff_hz, dtype=float) if np.isscalar(cutoff_hz) else np.asarray(cutoff_hz, dtype=float)
    out = np.zeros(n)
    prev = 0.0
    dt = 1.0 / sr
    for i in range(n):
        rc = 1.0 / (2 * math.pi * max(cutoff[i], 20.0))
        alpha = dt / (rc + dt)
        prev = prev + alpha * (samples[i] - prev)
        out[i] = prev
    return out


def cutoff_ramp(c0, c1, duration, sr=SR):
    t, n = times(duration, sr)
    return c0 + (c1 - c0) * (t / max(duration, 1e-9))


def sfx_envelope(duration, sr=SR):
    """09: "attack5ms, 남은 길이 exp감쇠(e^-6t/duration)". t=0에서 0으로 시작해 클릭을 없앤다."""
    t, n = times(duration, sr)
    attack = np.clip(t / ATTACK_SFX, 0.0, 1.0)
    decay = np.exp(-6.0 * t / max(duration, 1e-9))
    return attack * decay


def note_envelope(note_len_seconds, release=0.06, sr=SR):
    """BGM 음표 하나의 엔벨로프: attack5ms → sustain → 최대 release(09: "모든 음 release 최대60ms")."""
    t, n = times(note_len_seconds, sr)
    release = min(release, note_len_seconds)
    attack = np.clip(t / ATTACK_SFX, 0.0, 1.0)
    rel_start = note_len_seconds - release
    rel = np.where(t < rel_start, 1.0, np.clip((note_len_seconds - t) / max(release, 1e-9), 0.0, 1.0))
    return attack * rel


def fade_io(samples, fade_seconds, sr=SR):
    n = len(samples)
    fn = int(fade_seconds * sr)
    if fn <= 0 or fn * 2 >= n:
        return samples
    win = np.ones(n)
    win[:fn] = np.linspace(0, 1, fn)
    win[-fn:] = np.linspace(1, 0, fn)
    return samples * win


def mix_into(buffer, samples, start_sample, pan=0.0):
    """buffer: (N,2) stereo float array. samples: mono 1D. pan -1(왼)..1(오)."""
    n = len(samples)
    end = start_sample + n
    if end > len(buffer):
        n = len(buffer) - start_sample
        samples = samples[:n]
        end = start_sample + n
    if n <= 0:
        return
    left_gain = math.sqrt(0.5 * (1 - pan))
    right_gain = math.sqrt(0.5 * (1 + pan))
    buffer[start_sample:end, 0] += samples * left_gain
    buffer[start_sample:end, 1] += samples * right_gain


def normalize_peak(samples, target_dbfs):
    peak = np.max(np.abs(samples)) if len(samples) else 0.0
    if peak <= 1e-9:
        return samples
    target_lin = 10 ** (target_dbfs / 20.0)
    return samples * (target_lin / peak)


def write_wav(path, data, channels):
    """data: float32 in [-1,1], shape (N,) for mono or (N,2) for stereo. 48kHz/16bit PCM."""
    data = np.clip(data, -1.0, 1.0)
    pcm = (data * 32767.0).astype(np.int16)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(channels)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())


# ---------------------------------------------------------------------------
# SFX 22개 (09: "SFX 목록")
# ---------------------------------------------------------------------------

def make_tone_sequence(notes, note_dur, sr=SR):
    """notes: [(midi, start_time), ...] 각 음은 note_dur초, sine, note_envelope 적용, mono."""
    total = max(st for _, st in notes) + note_dur
    t, n = times(total, sr)
    out = np.zeros(n)
    for midi, start in notes:
        env = note_envelope(note_dur, sr=sr)
        tone = np.sin(2 * np.pi * midi_to_freq(midi) * np.arange(len(env)) / sr) * env
        s0 = int(round(start * sr))
        end = min(s0 + len(tone), n)
        out[s0:end] += tone[: end - s0]
    return out


def sfx_ui_focus():
    dur = 0.05
    return sine(660, dur) * sfx_envelope(dur)  # "낮은 크기" — 최종 목표 dBFS를 다른 SFX보다 낮게 잡는다(main 참고)


def sfx_ui_confirm():
    dur = 0.12
    return sine(660, dur, f1=880) * sfx_envelope(dur)


def sfx_ui_cancel():
    dur = 0.12
    return sine(440, dur, f1=330) * sfx_envelope(dur)


def sfx_ui_denied():
    dur = 0.16
    return triangle(180, dur) * sfx_envelope(dur)


def sfx_swing_rapier():
    dur = 0.12
    noise = white_noise(dur, seed_for("swing_rapier"))
    return lowpass(noise, cutoff_ramp(2400, 800, dur)) * sfx_envelope(dur)


def sfx_swing_sword():
    dur = 0.16
    noise = white_noise(dur, seed_for("swing_sword"))
    return lowpass(noise, cutoff_ramp(1800, 500, dur)) * sfx_envelope(dur)


def sfx_shot_gun():
    dur = 0.13
    noise = white_noise(dur, seed_for("shot_gun"))
    filtered = lowpass(noise, 3500.0)
    tone = sine(120, dur, f1=60)
    env = sfx_envelope(dur)
    return (filtered * 0.6 + tone * 0.6) * env


def sfx_shot_bow():
    dur = 0.14
    tone = triangle(440, dur, f1=220)
    noise = white_noise(dur, seed_for("shot_bow"))
    weak_noise = lowpass(noise, 2000.0) * 0.25
    return (tone * 0.85 + weak_noise) * sfx_envelope(dur)


def sfx_shot_hybrid():
    dur = 0.18
    return triangle(660, dur, f1=440) * sfx_envelope(dur)


def sfx_hit_enemy():
    dur = 0.08
    noise = white_noise(dur, seed_for("hit_enemy"))
    filtered = lowpass(noise, 1800.0)
    tone = sine(180, dur)
    env = sfx_envelope(dur)
    return (filtered * 0.55 + tone * 0.55) * env


def sfx_hit_player():
    dur = 0.22
    tone = sine(180, dur, f1=90)
    noise = white_noise(dur, seed_for("hit_player"))
    filtered = lowpass(noise, 1200.0) * 0.4
    return (tone * 0.75 + filtered) * sfx_envelope(dur)


def sfx_enemy_defeat():
    dur = 0.18
    return sine(220, dur, f1=70) * sfx_envelope(dur)


def sfx_pickup():
    dur = 0.08
    return sine(880, dur, f1=1320) * sfx_envelope(dur)


def sfx_levelup():
    seq = make_tone_sequence([(72, 0.0), (76, 0.15), (79, 0.30)], 0.15)  # C5 E5 G5
    return fade_io(seq, 0.005)


def sfx_skill_unlock():
    seq = make_tone_sequence([(76, 0.0), (79, 0.12), (84, 0.24)], 0.12)  # E5 G5 C6
    return fade_io(seq, 0.005)


def sfx_hybrid_unlock():
    seq = make_tone_sequence([(72, 0.0), (76, 0.15), (79, 0.30), (84, 0.45)], 0.15)  # C5 E5 G5 C6
    return fade_io(seq, 0.005)


def sfx_boss_warning():
    # 160Hz 두 번(.20/.20, 중간 .10 쉼)
    dur1 = 0.20
    gap = 0.10
    dur2 = 0.20
    total = dur1 + gap + dur2
    t, n = times(total, SR)
    out = np.zeros(n)
    tone1 = sine(160, dur1) * sfx_envelope(dur1)
    out[: len(tone1)] += tone1
    s0 = int(round((dur1 + gap) * SR))
    tone2 = sine(160, dur2) * sfx_envelope(dur2)
    out[s0 : s0 + len(tone2)] += tone2[: n - s0]
    return out


def sfx_explosion():
    dur = 0.35
    noise = white_noise(dur, seed_for("explosion"))
    filtered = lowpass(noise, cutoff_ramp(2200, 250, dur))
    tone = sine(100, dur, f1=40)
    env = sfx_envelope(dur)
    return (filtered * 0.65 + tone * 0.55) * env


def sfx_steam():
    dur = 0.80
    noise = white_noise(dur, seed_for("steam"))
    filtered = lowpass(noise, 3200.0)
    return fade_io(filtered, 0.08) * 0.7


def sfx_spell():
    dur = 0.30
    return triangle(330, dur, f1=660) * sfx_envelope(dur)


def sfx_clear():
    seq = make_tone_sequence([(72, 0.0), (76, 0.30), (79, 0.60), (84, 0.90)], 0.30)  # C5 E5 G5 C6
    return fade_io(seq, 0.01)


def sfx_defeat():
    seq = make_tone_sequence([(64, 0.0), (62, 0.25), (60, 0.50), (55, 0.75)], 0.25)  # E4 D4 C4 G3
    return fade_io(seq, 0.01)


SFX_RECIPES = {
    "ui_focus": sfx_ui_focus,
    "ui_confirm": sfx_ui_confirm,
    "ui_cancel": sfx_ui_cancel,
    "ui_denied": sfx_ui_denied,
    "swing_rapier": sfx_swing_rapier,
    "swing_sword": sfx_swing_sword,
    "shot_gun": sfx_shot_gun,
    "shot_bow": sfx_shot_bow,
    "shot_hybrid": sfx_shot_hybrid,
    "hit_enemy": sfx_hit_enemy,
    "hit_player": sfx_hit_player,
    "enemy_defeat": sfx_enemy_defeat,
    "pickup": sfx_pickup,
    "levelup": sfx_levelup,
    "skill_unlock": sfx_skill_unlock,
    "hybrid_unlock": sfx_hybrid_unlock,
    "boss_warning": sfx_boss_warning,
    "explosion": sfx_explosion,
    "steam": sfx_steam,
    "spell": sfx_spell,
    "clear": sfx_clear,
    "defeat": sfx_defeat,
}
assert len(SFX_RECIPES) == 22, f"SFX must be 22, got {len(SFX_RECIPES)}"

# 09: "효과별 peak -6dBFS 이하". ui_focus는 표에 "낮은 크기"로 별도 명시돼 있어 다른 SFX보다
# 더 낮은 목표치를 쓴다 — 그냥 -6dBFS로 일괄 정규화하면 sfx_ui_focus()의 진폭 차이가 지워진다.
SFX_TARGET_DBFS = {"ui_focus": -12.0}
DEFAULT_SFX_TARGET_DBFS = -6.0
# 09: "앞뒤2~5ms fade로 클릭 제거" — exp 감쇠 엔벨로프만으로는 파형이 정확히 0에서 안 끝날 수 있어,
# 정규화 직전에 모든 SFX에 공통으로 짧은 페이드를 한 번 더 씌워 경계 클릭을 확실히 없앤다.
COMMON_EDGE_FADE_SECONDS = 0.003


# ---------------------------------------------------------------------------
# BGM 3곡 (09: "BGM 3곡 레시피")
# ---------------------------------------------------------------------------

CHORDS = {
    "Am7": [57, 60, 64, 67],
    "Fmaj7": [53, 57, 60, 64],
    "Cmaj7": [48, 52, 55, 59],
    "G7": [55, 59, 62, 65],
}
PROGRESSION = ["Am7", "Fmaj7", "Cmaj7", "G7"]  # 4마디 반복
KICK_ENV_MS = 0.12
HAT_ENV_MS = 0.04


def chord_notes(bar_index):
    name = PROGRESSION[bar_index % 4]
    return CHORDS[name]


def bass_note(bar_index):
    return chord_notes(bar_index)[0] - 12


def synth_note(midi, dur_seconds, wave_kind, peak, sr=SR):
    n = int(round(dur_seconds * sr))
    if wave_kind == "sine":
        raw = np.sin(2 * np.pi * midi_to_freq(midi) * np.arange(n) / sr)
    elif wave_kind == "triangle":
        phase = (midi_to_freq(midi) * np.arange(n) / sr) % 1.0
        raw = np.where(phase < 0.5, -1 + 4 * phase, 3 - 4 * phase)
    else:
        raise ValueError(wave_kind)
    env = note_envelope(dur_seconds, sr=sr)
    n = min(len(raw), len(env))
    return raw[:n] * env[:n] * peak


def synth_kick(dur_seconds, sr=SR):
    tone = sine(80, dur_seconds, f1=45, sr=sr)
    env = note_envelope(dur_seconds, release=min(KICK_ENV_MS, dur_seconds), sr=sr)
    return tone * env * 0.12


def synth_hat(dur_seconds, seed, sr=SR):
    noise = white_noise(dur_seconds, seed, sr=sr)
    filtered = lowpass(noise, 5000.0, sr=sr)
    env = note_envelope(dur_seconds, release=min(HAT_ENV_MS, dur_seconds), sr=sr)
    return filtered * env * 0.035


def melody_index_sequence(kind):
    """각 곡의 멜로디 코드-인덱스 순서(짝수 블록 4마디마다 역순 적용은 호출부에서 처리)."""
    if kind == "menu":
        return [2, 3]  # 3번째, 4번째 (+12는 재생 시 처리)
    if kind == "run":
        return [0, 2, 1, 3]
    if kind == "boss":
        return [0, 2, 3, 2, 1, 2, 3, 1]
    raise ValueError(kind)


def render_bgm(bgm_id, bpm, bars, kind):
    beat_sec = 60.0 / bpm
    bar_sec = beat_sec * 4
    total_sec = bar_sec * bars
    n_samples = int(round(total_sec * SR)) + SR  # 여유 1초(release 꼬리)
    buf = np.zeros((n_samples, 2), dtype=np.float64)

    seed_hat = seed_for(f"{bgm_id}_hat")

    for bar in range(bars):
        bar_start = bar * bar_sec
        block_index = bar // 4
        reversed_block = (block_index % 2) == 1  # "짝수번째 블록(0기준1,3,…)에서 역순"
        is_last_bar = bar == bars - 1

        chord = chord_notes(bar)
        bass = bass_note(bar)

        if kind == "menu":
            # 코드 매마디 1회, 3.8박 sustain (peak 0.035/음, 코드 전체를 합쳐서 재생)
            chord_start = bar_start
            chord_dur = 3.8 * beat_sec
            for note in chord:
                mix_into(buf, synth_note(note, chord_dur, "sine", 0.035), int(round(chord_start * SR)), pan=0.15)
            # 베이스 1·3박
            for beat in (0, 2):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_note(bass, beat_sec * 0.9, "sine", 0.12), int(round(s * SR)), pan=0.0)
            # 멜로디: 3번째+12 at beat0 dur1beat, 4번째+12 at beat2 dur2beat(마디 끝까지)
            idxs = melody_index_sequence("menu")
            if reversed_block:
                idxs = list(reversed(idxs))
            mel_notes = [chord[i] + 12 for i in idxs]
            mix_into(buf, synth_note(mel_notes[0], beat_sec * 1.0, "triangle", 0.10),
                      int(round((bar_start + 0 * beat_sec) * SR)), pan=-0.15)
            # "마지막 마디 마지막박 멜로디 쉼으로 루프 숨" — 마지막 마디는 4박(마지막 박)만 쉬고
            # 3박째 음은 그대로 유지한다(2박 전체를 죽이면 "마지막박" 쉼이 아니라 "마지막 2박" 쉼이 됨).
            mel2_dur = beat_sec * (1.0 if is_last_bar else 2.0)
            mix_into(buf, synth_note(mel_notes[1], mel2_dur, "triangle", 0.10),
                      int(round((bar_start + 2 * beat_sec) * SR)), pan=-0.15)
            # hat 2·4박
            for beat in (1, 3):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_hat(HAT_ENV_MS, seed_hat + bar * 4 + beat), int(round(s * SR)))

        elif kind == "run":
            # 코드 1·3박 각 1.8박
            for beat in (0, 2):
                s = bar_start + beat * beat_sec
                for note in chord:
                    mix_into(buf, synth_note(note, 1.8 * beat_sec, "sine", 0.035), int(round(s * SR)), pan=0.15)
            # 베이스 매박
            for beat in range(4):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_note(bass, beat_sec * 0.9, "sine", 0.12), int(round(s * SR)), pan=0.0)
            # 멜로디: 인덱스 0,2,1,3 +12, 4분음표
            idxs = melody_index_sequence("run")
            if reversed_block:
                idxs = list(reversed(idxs))
            for beat, idx in enumerate(idxs):
                if is_last_bar and beat == 3:
                    continue  # 마지막 마디 마지막박 쉼
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_note(chord[idx] + 12, beat_sec * 0.95, "triangle", 0.10),
                          int(round(s * SR)), pan=-0.15)
            # kick 1·3박, hat 매박
            for beat in (0, 2):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_kick(min(KICK_ENV_MS, beat_sec)), int(round(s * SR)))
            for beat in range(4):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_hat(min(HAT_ENV_MS, beat_sec), seed_hat + bar * 4 + beat), int(round(s * SR)))

        elif kind == "boss":
            # 코드 매박 .8박
            for beat in range(4):
                s = bar_start + beat * beat_sec
                for note in chord:
                    mix_into(buf, synth_note(note, 0.8 * beat_sec, "sine", 0.035), int(round(s * SR)), pan=0.15)
            # 베이스 8분음표: 근음/근음+12 교대
            eighth = beat_sec / 2
            for i in range(8):
                s = bar_start + i * eighth
                note = bass if i % 2 == 0 else bass + 12
                mix_into(buf, synth_note(note, eighth * 0.9, "sine", 0.12), int(round(s * SR)), pan=0.0)
            # 멜로디: 8개 인덱스, 8분음표
            idxs = melody_index_sequence("boss")
            if reversed_block:
                idxs = list(reversed(idxs))
            for i, idx in enumerate(idxs):
                if is_last_bar and i == 7:
                    continue  # 마지막 마디 마지막박 쉼
                s = bar_start + i * eighth
                mix_into(buf, synth_note(chord[idx] + 12, eighth * 0.95, "triangle", 0.10),
                          int(round(s * SR)), pan=-0.15)
            # kick 1·3박, hat 8분음표
            for beat in (0, 2):
                s = bar_start + beat * beat_sec
                mix_into(buf, synth_kick(min(KICK_ENV_MS, beat_sec)), int(round(s * SR)))
            for i in range(8):
                s = bar_start + i * eighth
                mix_into(buf, synth_hat(min(HAT_ENV_MS, eighth), seed_hat + bar * 8 + i), int(round(s * SR)))

    # 실제 루프 길이로 자른다(꼬리 여유분 제거) — 09: "BGM은 루프 구간이 전체 파일"
    loop_samples = int(round(total_sec * SR))
    buf = buf[:loop_samples]
    peak = np.max(np.abs(buf)) if buf.size else 0.0
    if peak > 0:
        target = 10 ** (-1.0 / 20.0)  # 최종 믹스 peak -1dBFS 이하
        if peak > target:
            buf = buf * (target / peak)
    return buf


BGM_RECIPES = {
    "bgm_menu": dict(bpm=90, bars=16, kind="menu"),
    "bgm_run": dict(bpm=112, bars=32, kind="run"),
    "bgm_boss": dict(bpm=128, bars=32, kind="boss"),
}


# ---------------------------------------------------------------------------
# 실행
# ---------------------------------------------------------------------------

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    manifest = {"sampleRate": SR, "bitDepth": 16, "sfx": {}, "bgm": {}}

    for sfx_id, fn in SFX_RECIPES.items():
        samples = fn()
        samples = fade_io(samples, COMMON_EDGE_FADE_SECONDS)
        target_dbfs = SFX_TARGET_DBFS.get(sfx_id, DEFAULT_SFX_TARGET_DBFS)
        samples = normalize_peak(samples, target_dbfs)
        path = os.path.join(OUT_DIR, f"{sfx_id}.wav")
        write_wav(path, samples.astype(np.float32), channels=1)
        manifest["sfx"][sfx_id] = {
            "durationSeconds": round(len(samples) / SR, 4),
            "seed": seed_for(sfx_id),
        }
        print(f"[sfx] {sfx_id}: {len(samples)/SR:.3f}s")

    for bgm_id, params in BGM_RECIPES.items():
        stereo = render_bgm(bgm_id, **params)
        path = os.path.join(OUT_DIR, f"{bgm_id}.wav")
        write_wav(path, stereo.astype(np.float32), channels=2)
        manifest["bgm"][bgm_id] = {
            "bpm": params["bpm"],
            "bars": params["bars"],
            "durationSeconds": round(len(stereo) / SR, 4),
        }
        print(f"[bgm] {bgm_id}: {len(stereo)/SR:.3f}s ({params['bars']} bars @ {params['bpm']}bpm)")

    manifest_path = os.path.join(OUT_DIR, "..", "audio_manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(f"\n총 {len(SFX_RECIPES)}개 SFX + {len(BGM_RECIPES)}개 BGM 생성 완료 -> {OUT_DIR}")


if __name__ == "__main__":
    main()

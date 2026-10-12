"""QA G33: 원본 WAV 25개(SFX22/BGM3)의 형식·길이·피크·클리핑·앞뒤 페이드·BGM 루프 접합을 객관 측정한다(09 §사운드).
사용: python -I audio_files_check.py <Audio/v1 dir> <out.csv>"""
import csv, sys, wave, os
import numpy as np
src, out = sys.argv[1], sys.argv[2]
BGM = {'bgm_menu', 'bgm_run', 'bgm_boss'}
rows = []
for f in sorted(os.listdir(src)):
    if not f.endswith('.wav'): continue
    id_ = f[:-4]
    w = wave.open(os.path.join(src, f), 'rb')
    ch, rate, bits, n = w.getnchannels(), w.getframerate(), w.getsampwidth() * 8, w.getnframes()
    data = np.frombuffer(w.readframes(n), dtype=np.int16).reshape(-1, ch).astype(np.float64) / 32768.0
    peak = float(np.abs(data).max())
    clip = int((np.abs(data) >= 32767 / 32768.0).sum())
    ms = lambda k: int(rate * k / 1000)
    head = float(np.abs(data[:ms(1)]).max()); tail = float(np.abs(data[-ms(1):]).max())
    first0 = float(np.abs(data[0]).max()); last0 = float(np.abs(data[-1]).max())  # 클릭 없는 시작/끝(0 근처)
    diffs = np.abs(np.diff(data, axis=0))
    seam = float(np.abs(data[0] - data[-1]).max())
    p999 = float(np.percentile(diffs, 99.9))
    bgm = id_ in BGM
    ok = (rate == 48000 and bits == 16 and ch == (2 if bgm else 1) and clip == 0
          and 20 * np.log10(peak) <= (-1.0 if bgm else -6.0) + 0.01
          and (seam <= p999 if bgm else (first0 < 0.005 and last0 < 0.005 and head < 0.6 * peak and tail < 0.6 * peak)))
    rows.append(dict(id=id_, kind='BGM' if bgm else 'SFX', channels=ch, sampleRate=rate, bits=bits,
                     seconds=round(n / rate, 4), peakDbfs=round(20 * np.log10(peak), 2), clippedSamples=clip,
                     firstSample=round(first0, 5), lastSample=round(last0, 5), first1msMax=round(head, 5), last1msMax=round(tail, 5), loopSeamJump=round(seam, 5),
                     neighborDiffP999=round(p999, 5), pass_=ok))
with open(out, 'w', newline='', encoding='utf-8') as fh:
    wr = csv.DictWriter(fh, fieldnames=list(rows[0])); wr.writeheader(); wr.writerows(rows)
sfx = sum(r['kind'] == 'SFX' for r in rows); bgm = sum(r['kind'] == 'BGM' for r in rows)
print(f'files {len(rows)} SFX {sfx} BGM {bgm} pass {sum(r["pass_"] for r in rows)}')
for r in rows:
    if not r['pass_']: print('FAIL', r)

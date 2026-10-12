"""Match actual listener PCM against itself one native clip period later.

CSV sample counts locate marker windows in the tap WAV. This measures the
recorded output; it does not make a human listening or timbre judgment.
"""
import csv, json, sys, wave
from pathlib import Path
import numpy as np

root, project, output = map(Path, sys.argv[1:4])
with wave.open(str(root / 'out/audio-tap.wav')) as w:
    rate, channels = w.getframerate(), w.getnchannels()
    pcm = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2').reshape(-1, channels).astype(np.float64) / 32768
rows = list(csv.DictReader((root / 'out/audio-tap.csv').open(encoding='utf-8')))
segments = {}
position = 0
for r in rows:
    n = int(r['samples']) // channels
    marker = r['marker']
    if marker.startswith('loop_bgm_'):
        s = segments.setdefault(marker, {'start': position, 'end': position})
        s['end'] = position + n
    position += n
results = []
for marker, s in segments.items():
    asset = marker.removeprefix('loop_')
    with wave.open(str(project / f'Assets/_Project/Resources/Audio/v1/{asset}.wav')) as w:
        assert w.getframerate() == rate
        period = w.getnframes()
    # Exclude marker's first half-second. Compare a full second after one loop.
    start = s['start'] + rate // 2
    size = min(rate, s['end'] - start - period)
    assert size > rate // 4, f'{asset}: insufficient recorded repeat'
    a, b = pcm[start:start+size], pcm[start+period:start+period+size]
    corr = float(np.corrcoef(a.ravel(), b.ravel())[0, 1])
    rms_error = float(np.sqrt(np.mean((a-b)**2)))
    segment = pcm[s['start']:s['end']]
    result = dict(asset=asset, sampleRate=rate, channels=channels,
                  recordedSeconds=len(segment)/rate, nativePeriodSeconds=period/rate,
                  comparedSeconds=size/rate, onePeriodCorrelation=corr,
                  rmsRepeatError=rms_error, clippedSamples=int(np.count_nonzero(np.abs(segment) >= .9999)),
                  passCriteria='one complete period + repeat; correlation >= .999; clipping zero')
    result['passed'] = corr >= .999 and result['clippedSamples'] == 0
    results.append(result)
assert len(results) == 3
output.write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
if not all(r['passed'] for r in results): sys.exit(1)

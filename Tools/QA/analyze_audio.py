"""QA: 오디오 투어(audio-tap.csv: Unity AudioListener 최종 출력)와 외부 AudioProbe(session-meter.csv: 게임 프로세스 OS 세션 피크,
loopback.csv: 기본 장치 루프백 전체 믹스)를 구간(MARK)별로 맞춰 요약한다. 청취 검수를 대신하지 않는다.
사용: python -I analyze_audio.py <run root> <report.md>"""
import csv, math, os, sys
from datetime import datetime

def t(s):
    s = s.rstrip('Z')
    if '.' in s:
        a, b = s.split('.'); s = a + '.' + b[:6]
    return datetime.fromisoformat(s)

def db(x): return f"{20*math.log10(x):.1f}" if x > 0 else "-inf"

def main():
    root, report = sys.argv[1], sys.argv[2]
    tap = list(csv.DictReader(open(os.path.join(root, 'out', 'audio-tap.csv'), encoding='utf-8')))
    meter_p = os.path.join(root, 'probe', 'session-meter.csv'); loop_p = os.path.join(root, 'probe', 'loopback.csv')
    meter = [r for r in csv.DictReader(open(meter_p, encoding='utf-8')) if r['peak']] if os.path.exists(meter_p) else []
    loop = list(csv.DictReader(open(loop_p, encoding='utf-8'))) if os.path.exists(loop_p) else []
    segs = []
    for r in tap:
        if not segs or segs[-1]['marker'] != r['marker']:
            segs.append({'marker': r['marker'], 'start': t(r['utc']), 'rows': []})
        segs[-1]['rows'].append(r)
    for i, s in enumerate(segs):
        s['end'] = segs[i+1]['start'] if i + 1 < len(segs) else t(s['rows'][-1]['utc'])
    lines = ['# 오디오 출력 경로 측정 요약', '',
             '- tap: Unity AudioListener의 OnAudioFilterRead 최종 출력(기기로 보내는 샘플). 100ms 창.',
             '- session: Windows 오디오 세션 미터(IAudioMeterInformation) 중 CocktailCoffeeTea.exe 세션만. 50ms 간격 피크(0~1).',
             '- loopback: 기본 재생 장치 WASAPI 루프백(다른 앱 소리 포함 가능). 100ms 창.', '',
             '| 구간 | 초 | tap RMS 평균 dBFS | tap 피크 최대 | tap 무음 창(피크 0) | session 피크 평균 | session 피크 최대 | session 0 비율 | loopback RMS 평균 dBFS |',
             '|---|---|---|---|---|---|---|---|---|']
    for s in segs:
        # SetMarker flushes the previous window before playback. Short SFX can
        # finish in the first window; dropping it hides the complete sound.
        rs = s['rows'] if s['marker'].startswith('sfx_') else s['rows'][1:]
        n = sum(int(r['samples']) for r in rs)
        rms = math.sqrt(sum(float(r['rms'])**2 * int(r['samples']) for r in rs) / n) if n else 0
        pk = max((float(r['peak']) for r in rs), default=0)
        zero = sum(1 for r in rs if float(r['peak']) == 0)
        a, b = s['start'], s['end']
        lo = a.timestamp() if s['marker'].startswith('sfx_') else a.timestamp() + 0.3
        ms = [float(r['peak']) for r in meter if lo <= t(r['utc']).timestamp() < b.timestamp()]
        ls = [float(r['rms']) for r in loop if lo <= t(r['utc']).timestamp() < b.timestamp()]
        lrms = math.sqrt(sum(x*x for x in ls)/len(ls)) if ls else 0
        lines.append(f"| {s['marker']} | {(b-a).total_seconds():.1f} | {db(rms)} | {pk:.4f} | {zero}/{len(rs)} | "
                     f"{(sum(ms)/len(ms) if ms else float('nan')):.4f} | {(max(ms) if ms else float('nan')):.4f} | "
                     f"{(sum(1 for x in ms if x == 0)/len(ms) if ms else float('nan')):.2f} | {db(lrms)} |")
    open(report, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))

if __name__ == '__main__': main()

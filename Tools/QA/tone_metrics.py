"""2026-11-18 칵테일바 전투 배경 톤 측정: CombatToneCapture 결과(tone.json + PNG)에서
배경 평균 휘도(개체·HUD 띠 제외), 개체(적/탄/드롭) 대 주변 링 휘도 대비를 계산한다. 사용: python -I tone_metrics.py <캡처폴더>"""
import json, sys, os
from PIL import Image
import numpy as np

def lum(a):  # sRGB 값 기준 Rec.709 근사(0~255)
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]

def main(d):
    meta = json.load(open(os.path.join(d, 'tone.json'), encoding='utf-8'))
    out = {'screen': meta['screen'], 'effects': meta['effects'], 'tone': meta['tone'], 'shots': []}
    for s in meta['shots']:
        im = np.asarray(Image.open(os.path.join(d, s['file'])).convert('RGB')).astype(np.float32)
        H, W = im.shape[:2]
        L = lum(im)
        mask = np.ones((H, W), bool)
        mask[:int(H * 0.12), :] = False; mask[int(H * 0.9):, :] = False  # HUD 띠 대략 제외
        rects = []
        for o in s['objects']:
            x0, y0, x1, y1 = o['rect']
            # 화면 좌표(왼쪽 아래 원점) → 이미지(왼쪽 위 원점)
            r = (max(0, int(x0)), max(0, int(H - y1)), min(W, int(x1)), min(H, int(H - y0)))
            if r[2] - r[0] < 4 or r[3] - r[1] < 4: continue
            rects.append((o['kind'], r))
            mask[r[1]:r[3], r[0]:r[2]] = False
        bg = L[mask]
        contrasts = {}
        for kind, (a, b, c, e) in rects:
            inner = L[b:e, a:c]
            pad = 12
            ring = L[max(0, b - pad):min(H, e + pad), max(0, a - pad):min(W, c + pad)].copy()
            rm = mask[max(0, b - pad):min(H, e + pad), max(0, a - pad):min(W, c + pad)]
            if rm.sum() < 20: continue
            contrasts.setdefault(kind, []).append(abs(float(inner.mean()) - float(ring[rm].mean())))
        out['shots'].append({
            'file': s['file'], 'bg_mean': round(float(bg.mean()), 1), 'bg_p10': round(float(np.percentile(bg, 10)), 1),
            'bg_p90': round(float(np.percentile(bg, 90)), 1), 'bg_dark_share_lt40': round(float((bg < 40).mean()), 3),
            'contrast_mean': {k: round(float(np.mean(v)), 1) for k, v in contrasts.items()},
            'counts': {k: len(v) for k, v in contrasts.items()}})
    json.dump(out, open(os.path.join(d, 'metrics.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    for s in out['shots']: print(s)

if __name__ == '__main__':
    main(sys.argv[1])

"""73 단계2: Codex ProgressionUI-2026-10-19(ready) → Assets/_Project/Resources/ProgressionUI (ArtSource는 읽기만).

- 정지 아트: {assetId}.png, 프레임 아트: {assetId}_{nn}.png (manifest 프레임 순서)
- borders.json: 9slice 테두리(Unity L,B,R,T), 콘텐츠 rect(왼쪽 위 px), 원본 크기, 확대 규칙, 모션 프레임 시간·종이 변환(paperTransforms)
- manifest sha256과 다르면 복사하지 않는다. 텍스처 임포트 설정은 Editor/DrinkLabArtImport.cs의 ProgressionUiImport가 borders.json으로 적용한다.
사용: python Tools/DrinkLab/import_progression_ui.py
"""
import hashlib, json, os, shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'ProgressionUI-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'ProgressionUI')

def sha(p):
    return hashlib.sha256(open(p, 'rb').read()).hexdigest()

def main():
    os.makedirs(DST, exist_ok=True)
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    table = {'source': 'ArtSource/v1/ProgressionUI-2026-10-19/manifest.json', 'assets': {}}
    for a in man['assets']:
        if a.get('status') != 'ready':
            print('skip', a['assetId'], a.get('status')); continue
        frames = a['frames']
        names = []
        for f in frames:
            src = os.path.join(SRC, f['path'].replace('/', os.sep))
            if sha(src) != f['sha256']:
                raise SystemExit(f"sha mismatch {a['assetId']} {f['path']}")
            name = a['assetId'] if len(frames) == 1 else f"{a['assetId']}_{f['index']:02d}"
            shutil.copyfile(src, os.path.join(DST, name + '.png'))
            names.append(name)
        entry = {'size': a['nativeSize'], 'frames': names, 'scaling': a.get('scaling'),
                 'bordersLBRT': a.get('bordersUnityLBRTop'), 'contentRect': a.get('contentRect'),
                 'durationsMs': [f.get('durationMs') for f in frames]}
        for k in ('paperTransforms', 'paperSourceSize', 'paperSourceContentRect', 'notebookOrigin', 'boxOrigin', 'anchors', 'hingePixelsTopLeft', 'screenRect', 'safeContentRect'):
            if k in a:
                entry[k] = a[k]
        table['assets'][a['assetId']] = entry
    open(os.path.join(DST, 'borders.json'), 'w', encoding='utf-8', newline='\n').write(json.dumps(table, ensure_ascii=False, indent=2))
    # 찢기·입고 모션: 런타임 JsonUtility용 평평한 표(프레임별 시간, 종이 글자 영역 왼쪽 위 px x,y,w,h, 배율, 앞면 가림 clip 높이, 글자 표시)
    tear = table['assets']['recipe_tear_store']
    frames = []
    for t, ms in zip(tear['paperTransforms'], tear['durationsMs']):
        x, y, w, h = t['contentRectOnFrame']
        frames.append({'ms': ms, 'x': x, 'y': y, 'w': w, 'h': h, 'scale': t['scaleXY'][1], 'clipX': t['preBoxClipRect'][0], 'clipY': t['preBoxClipRect'][1],
                       'clipW': t['preBoxClipRect'][2], 'clipH': t['preBoxClipRect'][3], 'text': bool(t['textVisible'])})
    open(os.path.join(DST, 'tear_motion.json'), 'w', encoding='utf-8', newline='\n').write(
        json.dumps({'width': tear['size'][0], 'height': tear['size'][1], 'frames': frames}, ensure_ascii=False, indent=2))
    print(len(table['assets']), 'assets', sum(len(v['frames']) for v in table['assets'].values()), 'png')

if __name__ == '__main__':
    main()

"""72 직원 초상: Codex ResearchStaff-2026-10-19(ready, root 수락) → Resources/ProgressionUI/staff_*.png (ArtSource는 읽기만).

28x34 초상을 staff_card(160x112)의 초상 칸 [18,27,28,34]에 같은 배율로 놓는다(코드: ResearchLaptopView). sha256이 manifest와 다르면 복사하지 않는다.
사용: python Tools/DrinkLab/import_research_staff.py
"""
import hashlib, json, os, shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'ResearchStaff-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'ProgressionUI')


def main():
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    if man.get('status') != 'ready':
        raise SystemExit('manifest not ready')
    for a in man['assets']:
        src = os.path.join(SRC, a['png'])
        if hashlib.sha256(open(src, 'rb').read()).hexdigest() != a['sha256']:
            raise SystemExit(a['assetId'] + ': sha mismatch')
        shutil.copyfile(src, os.path.join(DST, a['assetId'] + '.png'))
        print(a['assetId'], 'copied', a['sha256'][:16])


if __name__ == '__main__':
    main()

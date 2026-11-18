# 사용자 중단·백업 인계 — 2026-11-18 10:15 KST

사용자 “여기까지하고 깃허브랑 옵시디언 업데이트해” 지시로 **새 생성/아트 수정/검수/통합을 중단**했다. 이 문서와 STOP-STATE.json은 현상 저장이며 완료 판정이 아니다. Git/옵시디언 백업은 코디네이터가 수행한다. 작업자는 PC 종료·공개 게시·커밋/푸시를 하지 않았다.

## 현재 디스크 상태

- 후속 버전: `ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end/`.
- `layout.json`: version4, **status=temp, ready=false**.
- **전체 v4 manifest.ready.json은 아직 없다.** 타이틀28PNG+실제 전투 보정4PNG가 디스크에 있지만 전체 최종검사/승인이 끝난32개 ready 묶음이 아니다.
- `manifest.bg_barwarm.ready.json`만4개 아트 소스 검사 통과 상태: 실제 bg_bar floor/wall/counter/lamp를 새 bg_barwarm ID로 색 보정해 after fixture 비교용 인계. 실전 가독성 완료 아님.
- 디스크32PNG/native34개와 핵심 파일의 중단 시점 SHA256: `STOP-STATE.json`. 단순 백업 기준 해시이며 새 QA 통과를 의미하지 않는다.
- 기존 v3의28PNG/layout/manifest는 보존. 마지막 저장된 `verification/v3-preservation.json`은277개 불변/참조8개 불변을 기록한다. 중단 뒤 새 보존 시험을 반복하지 않았다.

## 실제로 수정된 것

- `previews/interior-composite.png`: 바의 화면 오른쪽 끝(남쪽 끝)에 노트북이 실제로 이동했고 독립 테이블/그 의자는 제거됨. 반대쪽 북단은 카메라 가까워 잘리므로 배제했다.
- 장부는 카운터 안쪽으로 이동, 북향 glimpse/문턱8뷰와 남향 실내를 같은 평면에서 재렌더함.
- 사용자 빨간 표시4곳에 맞춰 최신 layout에서 permanent/ledger/radio/noticeboard hotspot과 label을 수정. 기존 구현이 label_anchor를 중심이 아닌 label_side별 edge-pivot으로 쓰는 것을 확인하고 프리뷰도 동일 방식으로 바꿈.
- 작은 노트북 클릭영역은 바 끝 면만 포함하며 현재 polygon 계산상8곳 겹침0. 그러나 **최신 게임에 반입·실제 클릭/문자치수 검증한 것은 아니다**.
- 실제 `Assets/_Project/Scripts/Core/GameInfo.cs`의 정확한 제목은 **“음료 마스터”**(띄어쓰기 포함). 외관 sign.textSource=GameInfo.Title / 실제 물리 간판 위 textRect·색·원근 계약을 추가함. 그림에는 글자를 굽지 않음.
- 외관에 문 옆 작은 입력 안내 명패를 추가. `exterior.entrance.mode=await_input`, autoStart=false, 문 클릭영역/“입장하기”/“아무 키나 입력하세요” 텍스트영역 계약 포함. 자동 재생 폐기는 Claude 코드 담당.
- 외부 제목/안내 프리뷰: `previews/exterior-sign-prompt-1280x720.png`, 1920x1080,1440x1080.
- 이름표 원근/배치 프리뷰: `previews/hotspots-labels-proof.png`; 최신 이미지이며 최종 게임 캡처가 아님.

## 톤 계약 현재 위치

최초 cafe_* 보정 비교안은 실제 기본 싱글 바텐더 배경이 아니라는 것이 확인되어 **반입 금지 임시안**으로 남겼다. 실제4종은 Resources/ArtExtra/bg_bar_floor|wall|counter|lamp/<id>_00.png이며 그대로 읽어 보존했다.

신규 목적지는 ArtExtra/bg_barwarm_floor|wall|counter|lamp/<id>_00.png, 기존 canvas/PPU64/가운데 pivot. 원 bg_bar JobPreset prefix는 유지하고 CafeBackground.TryJobMap에서 싱글 바텐더에 part별 override/fallback. 보정본이 실제 사용될 때만 곱RGB1/veil≤.08 후보를 적용하고, title 코드 추가 tint 및 이중 보정을 하지 않는 계약이다. 공용 cafe 스킨/찻집/협동은 바꾸지 않는다.

warm lift: Y=(54R+183G+19B)/256, lift=32*(1-Y/255), RGB+=lift*(1.10,.86,.55), alpha 정확 보존.4개 원본→보정 평균 휘도는 floor32→57,wall54→77,counter56→78,lamp122→136. 원형/알파/native 왕복 통과 근거는 `verification/bg_barwarm-grade-validation.json`.

타이틀 역시 실제 바의 바닥 목재 patch/카운터/몰딩/램프 재질·동일 warm grade로 마감 중이며 임시 프리뷰가 저장됨. **Claude 실제 전투 before/after 정상·군중·보스·드롭·low 720/1080 가독성 판정이 아직 이 작업자에게 전달되지 않았다.** 따라서 공통 톤 최종 확정 및 전체 v4 ready 발행은 미완이다.

## 재개 때 반드시 할 일

1. 사용자의 새 재개 지시가 오기 전 새 작업을 시작하지 않는다. 기존 큐/스크립트를 중복 실행하지 않는다.
2. 현재32PNG/native34개가 모두 최종 통과했다고 보지 않는다. 마지막 actual bg_bar 재질 변경 후 전체 최신 왕복/레이어등록/프리뷰 검사를 다시 마무리해야 한다.
3. **복사된 문 animation master는 최신 개별 문 프레임/톤과 다시 맞춰 생성·대조해야 한다.** 이전 v3 provenance/ready metadata 일부도 후속 최종검사 뒤에만 재발행한다.
4. 노트북은 실제 바 끝에 있으나 최종 설치 현실성 재검토(키보드 방향과 실제 사람이 설 자리/접근 방향), 접지·가림·장부 간격을 다시 확인한다. 직전 단계에서 이 설치 방향 문제를 검토하려던 중 사용자 중단을 받았으므로 **아직 추가 수정하지 않았다**.
5. 4라벨은 실제 코드의 edge-pivot 계약(above=아래중앙,left=오른중앙,right=왼중앙,below=위중앙)으로 수정했으나 실제 Unity 문자 preferredWidth/720/1080/4:3 및 실제 클릭 결과를 Claude와 확인한다.
6. GameInfo.Title/입력대기/문 클릭·키 입력 소비/자동재생 폐기·간판 부착 텍스트는 Claude 구현 후 실제 실행 검증한다.
7. 전투 실제 renderer 밝기/veil와 source grade 이중적용을 막고 정상/군중/드롭/적탄 대비 판정 후 공통 톤 확정, 단일 v4 manifest32개/최신 해시와 보고서를 발행한다.

## 실행·백업 안전

- v4는 추가 PixelLab 요청0, 재사용된 공식 native 생성 원본7쌍과 기존 승인 참조를 Aseprite에서 재배치/보정했다.
- 이전 공식 생성 큐의 마지막 직접 관찰은0active/0waiting/6recent·완료 상태였다. 중단 시10:15 read-only Win32 프로세스 조회에서는 Aseprite.exe가 반환되지 않았다. 작업자가 강제 종료한 것이 아니며 큐 재시작/취소도 하지 않았다.
- Git/문서 백업에 이 ArtSource 작업 폴더만 포함하고 **AppData Aseprite 확장 전체·계정 설정·비밀키/.env·권한/자격정보 파일을 공개 저장소에 추가하지 말 것**. 이런 파일은 작업자가 의도적으로 복사하지 않았다. 비밀값 접근/보안 우회·추가 결제/로그인/full trust 조작 없음.
- **이 문서와 STOP-STATE.json 저장 뒤 아트 파일 추가 편집은 종료**한다. 코디네이터가 이 중단 상태를 기준으로 Git/옵시디언을 백업하면 된다.

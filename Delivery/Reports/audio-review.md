# G33 오디오 검수 — 2026-10-08

객관 조건 검증 완료. 사람 청취, 음색이나 체감 균형 평가를 했다고 주장하지 않는다. 기준은 기획10 G33의 파일 수·규격·루프·클리핑·볼륨0/Pause·이벤트 중복 제한이다.

- 원본: `audio-original-files.csv`, SFX22/BGM3 실제 WAV25개,48kHz/16bit,SFX mono/BGM stereo, 파일별 피크·길이·양끝·루프 접합 검사25/25, 클리핑 표본0.
- Unity native PCM: `audio-native-pcm.csv`. 실제 Resources.Load/GetData25개를 측정하며 모든 표본 NaN/Infinity0, 클리핑0,48kHz/채널 규격·피크·접합 조건 통과. 최종 전체 PlayMode의 `AudioSpecTests.DecodedClips_22Sfx3Bgm_Format_Peak_AndLoopSeam`에서 다시 생성한다.
- 실제 출력: `../logs/rc-2026-10-08/rc2-audio/out/audio-tap.wav`, `audio-tap.csv`, `tour-log.txt`. Listener OnAudioFilterRead의 기기 출력 PCM 기록과 Windows 해당 게임 프로세스의 IAudioMeterInformation를 분리 측정했다. OS 기본 장치 loopback은 다른 앱 소리가 섞일 수 있어 참고 자료로만 쓴다.
- 실제 반복: `audio-output-loops.json`. 메뉴42.666667/일반68.571438/보스60초 원본 주기보다 긴45.674667/71.573333/63.637333초를 기록했다. 한 주기 뒤1초 파형이 원본 출력과 상관계수1.0/RMS 오차0으로 일치하며 각 구간 클리핑0이다. native PCM의 마지막→첫 표본 접합도 검사한다.
- 개별 재생: `audio-sfx-output-rc3.md`와 `../logs/rc-2026-10-08/rc3-sfx/`. 정상 Run, CombatSuppressed=False, BGM0에서22SFX를 각각 재생해 모든 ID가 Listener와 해당 게임 OS 세션에서 peak>0이다. 종료 marker errors0/exceptions0이다. 측정용 스폰 중지와 무기 비활성은 QA 전용이며 정상 완주 증거로 쓰지 않는다.
- 볼륨0: `audio-output-rc2.md`의 title_master0/title_music0/run_master0은 tap·게임 OS 세션 모두 완전 무음이다. run_sfx0은 BGM만, run_music0은 SFX만 남으며 각 채널 복구 후 다시 출력된다.
- Pause/보상/결과: 동일 보고의 card_overlay/card_closed/result/restart1~3 구간과 MARK의 listeners=1, suppression/duck/playing source를 대조한다. 메뉴와 Pause에서 전투 SFX를 억제하면서 UI·보상·결과 음은 유지한다. 상태 전환3회에서 AudioService/Listener 중복이 없다.
- 중복: 최종 전체 PlayMode의 `AudioSpecTests.Dedupe_HitEnemy50ms_Other30ms_SameIdCap4`, `Polyphony_NeverExceeds24` 및 기존 AudioService/Pause 시험으로 실제 요청을 보내50/30ms 합산, 같은ID4, 전체24 한도를 확인한다.

첫 rc2 측정은 메뉴 억제 상태에서 전투 SFX를 요청하므로 개별 전투음 재생 증거로 사용하지 않는다. rc3의 정상 Run 재생으로 보완했다. 분석기는 짧은 효과음이 끝나는 첫100ms 창을 버리지 않고, SFX OS 측정에서도 첫0.3초를 제외하지 않는다. 제품 억제 규칙은 변경하지 않았다.

최종 고정 오디오 입력 SHA-256: `98bbf4b99017f83e9c21982ac618666194fa5c040ac0e0a0d25d8182aa4f175a`(57입력 파일, WAV/meta/생성 목록 포함). 출력 기록 이후 AudioService·AudioBus·Resources WAV는 변경하지 않았다. 외부 청취 도구의 부재를 조건 면제나 추가 차단으로 취급하지 않는다.

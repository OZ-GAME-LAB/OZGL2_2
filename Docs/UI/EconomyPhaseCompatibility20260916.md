# 최신 재화 API와 UI 호환 — 2026-09-16

## 범위

- 작업 프로젝트: `D:/Documents/GitHub/OZGL2_2`
- 작업 브랜치: `feature/ui-integration-readiness-20260916`
- 시작 HEAD: `aed54244b180588fe72b8bf0813f29c984350c92`. 이전 미커밋 작업을 유지했다.
- 팀 기준: `origin/dev`의 `0ebde218b3b864fb7e91ed48bad8a06c81e1a795`.
- 이전 `f948eac` 이후 변경된 재화 원본 7개만 동기화했다. 적용 전 각 파일이 이전 dev 원본과 일치하는지 확인하고 백업했다. 팀원의 재화 계산/지급/정산 코드는 편집하지 않았다.
- 현재 브랜치에 dev를 Git 병합한 것은 아니다. 커밋·푸시·PR·머지도 수행하지 않았다.

## 변경된 UI 동작

### 팀 건설 UI 씬: 자동 지급

`TeamBuildingUiStartup`이 `RunCurrencyManager.Initialize(waves, flow, effects)`로 실제 Core를 전달한다.

- BattlePreparing: 재화 매니저가 현재 웨이브의 보상 수치를 고정한다.
- Reward: 재화 매니저가 해당 보상을 한 번 지급한다.
- UI: 기존 BalanceChanged 구독으로 골드·보석 잔액만 표시한다. UI가 지급 메서드를 추가로 호출하지 않는다.
- Finished/None: 준비된 보상을 정리하는 책임도 팀 재화 매니저에 있다.

씬은 `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity` 그대로다. 씬·프리팹·데이터의 UI 참조를 새로 저장할 필요는 없다. 플레이어의 전투 시작 버튼은 여전히 숨겨져 있으며 완성 전투 씬이 아니다.

### 독립 UI 테스트 씬: 명시적인 수동 지급

`MvpRuntimeHudSample`은 실패·오버플로·재시도·유물 후보 오류를 검사하는 테스트 장치다.

- `Initialize(waves, null, effects)`로 자동 지급 구독을 사용하지 않는다.
- BattlePreparing에 공개 `TryPrepareWaveReward()`를 호출한다.
- 기존 테스트 입력/유물 선택 흐름이 `TryApplyWaveReward()`의 성공을 확인한 뒤 다음 단계 대기를 해제한다.
- 자동 지급과 수동 지급을 한 지갑에 동시에 연결하지 않는다.
- 이 경로의 검증을 실제 팀 씬의 자동 지급 검증으로 대체 표기하지 않는다.
- 팀에서 제거한 적 드랍 API 테스트는 일반 `TryAdd` 잔액 이벤트 검사로 변경했다. 실제 적 처치 드랍 연동을 완료했다는 의미가 아니다.

## 검증

별도 복사본 `D:/UnityUIValidation/OZGL2_2-player-ui-20260914`에서 Unity 6000.3.23f1로 실행한다. 사용자 Editor/마우스를 조작하지 않는다.

- 자동 보상: `Game.UI.Editor.TeamBuildingUiValidation.RunBatch`
- 수동 재화·Core·HUD: `Game.UI.Editor.MvpRuntimeHudValidation.Run`
- 플레이어 팝업·실제 유물: `Game.UI.Editor.PlayerUiValidation.RunBatch`

근거 폴더는 `Logs/UiEconomyCompatibility20260916`이다.

- 팀 건설 씬: **175개 assertion 통과**. 기존 건설/입력 검사 152개와 신규 자동 보상 검사 23개. 런타임 오류 0, Editor Search 시작 예외 0. `team-auto-first.log`, `FinalResults/Team/results.txt`.
- 플레이어 UI: **263개 통과**, 실제 유물 선택/효과/수동 보상: **129개 통과**. `player-ui-first.log`, `FinalResults/Player/results.txt`. 플레이어 테스트 씬의 수동 지급 경로이며 팀 전투 전체 흐름을 증명하지 않는다.
- 수동 재화/Core/HUD 묶음: **실패**. `MvpEconomyUiValidation`의 분기 마지막 웨이브에 두 재화가 있는지 확인하는 사전조건에서 중단. `runtime-hud-first.log`. 이후 다중 재화 오버플로·재시도 및 이 묶음의 후속 Core/유닛 검사는 이번 실행에서 완료하지 않았다.
- 자동 지급 검사에서는 현재 Core가 도달하는 3웨이브의 골드 지급 및 보석 보상 0을 검증했다. 보석 HUD는 일반 잔액 변경으로 확인했으나 **보석의 양수 자동 웨이브 보상은 검증하지 못했다**.
- 씬 참조/텍스트 넘침은 자동 검사했으며, 실제 렌더된 1280×720 건물 정보 팝업도 확인했다. 최종 아트 완료를 뜻하지 않는다.

### 발견된 팀 통합 불일치 — 원본 수정하지 않음

`Assets/Scripts/Core/WaveController.cs`의 `MAX_WAVE`는 3이다. 반면 새 `WaveRewardTable.asset`은 분기마다 5개 웨이브를 담고, 보석은 5번째에만 지급한다. 현재 Core는 3번째를 보스로 판정하고 다음 분기로 넘어가므로 4·5번째 보상 항목에 도달하지 않는다.

재현: 새 판 → Core의 마지막 웨이브 테스트 이동 → 전투 준비 → 웨이브 3 보상 조회. `CurrentGemReward == 0`이고 테이블의 양수 보석은 웨이브 5에만 있다.

Core/재화 담당자가 분기당 웨이브 기준을 통일해야 한다. UI가 보스를 5로 위장하거나 보석을 별도 지급하지 않았고, 실패 검사를 삭제하거나 원본 보상표를 고쳐 통과시키지 않았다. 전체 통합 검증/머지 준비 완료 상태는 아니다.

검증 상태: **Failed — 보상 데이터/Core 계약 불일치**. Unity C# 컴파일은 통과했고 위 UI 검사 합계 567개는 통과했으나, 실패한 재화 통합 묶음을 포함해 모든 검사가 통과했다고 보고하지 않는다. 기존 Core CS1998 경고와 시작 시 라이선스 갱신 메시지는 별도이며 테스트 실행 자체를 차단하지 않았다.

## 남은 계약

- `CurrentGoldReward`/`CurrentGemReward`는 지급 예정량이며 성공 영수증이 아니다. 자동 지급 실패를 구별하는 공개 결과/완료 이벤트가 없으므로, 이를 지급 완료 표시나 유물 선택 대기 해제 근거로 사용하지 않는다.
- `PersistentCurrencyManager.TryApplyReward(ResultType)`는 도입됐으나 Core가 최종 ResultType을 공개 이벤트로 전달하지 않는다. UI가 Finished 또는 HasClearedMainGame으로 승패를 추측해 혈석을 지급하지 않는다.
- 혈석 정산 1회 보장 및 저장·재시작 소유권은 담당자 계약이 필요하다.
- 실제 전투 스포너, 유닛 생산부터 승패까지의 전체 플레이, 건물 업그레이드/공간 해금, 최종 결과 UI는 여전히 별도 통합 항목이다.
- Player 빌드나 물리 키보드·마우스 검증은 이번 범위가 아니다.

## 복구와 원본 보존

- 시작 시 Assets/Docs/Packages/ProjectSettings 파일 993개의 해시를 기록했다.
- `Logs/UiEconomyCompatibility20260916/BeforeSync`에 동기화 전 재화 원본 7개를 보관했다.
- 팀 코드 비교 기준은 최신 dev이며, 오늘 직접 수정한 런타임/검증 코드는 UI 폴더에 한정한다.
- 기존 미커밋 폰트, 팀 씬/프리팹, ProjectSettings, Packages는 덮어쓰지 않는다.
- 최종 감사: 기존 파일 변경 12개 = 팀 원본 동기화 7개 + UI 코드 4개 + 컨텍스트 문서 1개. 추가 문서 2개. 예상 외 기존 파일 변경 0, 동기화 원본 불일치 0. 존재하는 팀 비UI 코드/메타 373개가 최신 dev와 일치한다. diff 공백 검사 통과. `final-audit.xml` 참조.

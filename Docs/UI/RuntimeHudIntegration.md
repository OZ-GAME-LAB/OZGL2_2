# 실제 재화·코어 HUD 연동

작업일: 2026-09-09. 작업 브랜치: `feature/ui-runtime-integration`.

통합 체크아웃: `D:/Documents/GitHub/OZGL2_2-ui-integration`.
기존 `feature/ui-common-hud`와 PR #9는 유지한다. 모든 PR의 대상은 `dev`다.

## 의존 코드 기준

- 재화·유닛: `origin/dev`의 `1a3ce52`.
- 기존 UI: `1b8d2a5`.
- 코어: `origin/feature/core-game-test`의 `dbc33b0`. 코어 기능 커밋과 기존 UniTask 패키지 커밋만 로컬 통합했다.
- 코어의 일반 프로젝트 설정 및 TMP 리소스 커밋은 추가하지 않았다.

이 브랜치는 아직 dev에 없는 기존 UI·코어 작업을 포함하는 로컬 통합용이다.
담당 PR이 dev에 반영되면 오늘 UI 변경만 최신 dev 기반 기능 브랜치에 옮겨 PR한다.
원격 main/dev를 변경하거나 PR을 병합하지 않았다.

## 구현

- `RunGoldHudBinding`: 실제 `RunCurrencyManager.BalanceChanged`를 구독한다. 골드만 갱신하며 잔액은 항상 매니저에서 조회한다.
- `CoreHudBinding`: 실제 `PhaseChanged`/`WaveChanged`를 구독하고 `TryStartWave()`의 Battle 진입 완료까지 기다린다.
- HUD 비활성화 시 구독과 UI 측 대기를 정리하며, 코어의 전투 준비 자체는 중단하지 않는다. 재활성화 시 최신 상태를 조회한다.
- 코어 리셋의 None 알림에서 이전 UI 요청을 무효화해 과거 완료/취소 알림이 새 판에 반영되지 않게 한다.
- `GameUIController.ClearGold()`/`ClearWaveProgress()`를 추가했다. 미초기화 상태는 0 대신 `--`로 표시한다.
- 기존 UI의 공개 메서드·필드 이름은 유지한다. 재화 지급·비용·승패 공식은 UI에서 계산하지 않는다.

## 씬 실행

Unity 6000.3.23f1에서 이 통합 체크아웃을 연다.

1. `Assets/Scenes/Test/MvpRuntimeHudTest.unity`를 열고 Play한다.
2. 초기 골드 100 / 1·3웨이브 / 건설 표시를 확인한다.
3. 골드 +50 / -30 / 부족한 비용 요청으로 실제 재화 매니저의 값과 HUD를 비교한다.
4. 웨이브 시작 → 3초 준비 → 전투 → 테스트 적 전멸 → 테스트 보상 +30을 진행한다.
5. 세 웨이브 후 결과 단계에서 재화·코어 리셋을 실행한다.
6. 전투 준비 중 리셋, HUD 숨김·재표시도 확인한다.

씬이 없으면 `Game > UI > Create Missing Runtime HUD Test Scene`에서 생성한다.
기존 씬이 있으면 덮어쓰지 않는다. 기존 공통 HUD 프리팹을 인스턴스화하고 연결 컴포넌트를 씬 인스턴스에 추가한다.
공유 Build Settings에는 추가하지 않는다.

시작 100 / 추가 50 / 비용 30 / 보상 30 / 실패 요청 10,000은 테스트 전용 수치다.
씬은 실제 매니저와 코어를 사용하지만 적 생성·전투 판정은 코어 담당의 `TestSpawner`를 사용한다.
보상 처리 버튼은 테스트용 `TryApplyWaveReward` 성공 후 `TestWaitingScript.ChooseResultBtn()`으로 대기를 끝낸다.
이 샘플을 정식 보상 시스템으로 사용하지 않는다.

## 실제 씬 연결

- HUD와 같은 활성화 수명을 가진 오브젝트에 두 Binding을 배치하고 Inspector에서 참조를 연결하거나 `Initialize(...)`를 호출한다.
- `RunGoldHudBinding.Initialize(GameUIController, RunCurrencyManager)`는 매니저를 초기화하거나 돈을 지급하지 않는다.
- 재화 부트스트랩이 `TryInitialize()` 또는 `TryEndRun()`을 처리한 뒤 `RunGoldHudBinding.Refresh()`를 호출한다. 현재 재화 API는 초기화/종료 때 잔액 이벤트를 발행하지 않는다.
- `CoreHudBinding.Initialize(GameUIController, GameFlowController, WaveController)`는 코어의 초기화/BeginRun을 소유하지 않는다. 기존 부트스트랩의 책임을 유지한다.
- `CoreHudBinding.Refresh()`는 현재 단계·웨이브·입력 표시를 재동기화한다.
- 모든 호출은 Unity 메인 스레드에서 수행한다.
- `GameUIController.Initialize()`는 여러 Binding을 연결하기 전에 한 번 호출한다. 연결 후 별도로 초기화했다면 두 Binding의 Refresh를 호출한다.
- 매 프레임 폴링은 사용하지 않는다. UI의 시작 버튼 허용 표시는 Preparation 단계이며, 최종 실행 조건은 코어의 TryStartWave가 다시 검사한다.

## 담당자와 확정할 사항

| 담당 | 필요한 연결 정보 | 현재 상태 |
| --- | --- | --- |
| 코어 문규성 | 최종 승패와 정산 내역을 전달할 이벤트/조회 | Finished 단계만 공개돼 결과를 추측하지 않고 패널 연결 대기 |
| 코어·재화 | 보상 확정 내역과 실제 처리 완료 전달 방식 | 코어가 아직 TestWaitingScript를 기다림 |
| 코어·재화 | 재시작 시 재화/유닛/건물까지 초기화하는 단일 진입점 | 샘플에서만 명시적으로 초기화 순서 구성 |
| 코어 | 전투 준비 중 일반 예외 발생 시 상태 복구 규약 | UI는 오류 표시·대기 정리; 코어 잠금을 임의로 변경하지 않음 |
| 건물·유닛 | 선택/생산/체력/제거 알림 및 실제 오브젝트 연결 | 오늘 연결 범위에 포함하지 않음 |

기획서의 관문·베이스캠프 파괴 패배 조건과 현 코어 테스트의 아군 전멸 패배 조건은 별개다.
정식 승패 판정은 코어 담당자의 확정 결과를 따른다.

## 검증 방법

- 실행 메뉴: `Game > UI > Validate Runtime HUD In Play Mode`
- 진입점: `Game.UI.Editor.MvpRuntimeHudValidation.Run`
- 실제 Play Mode에서 UI 버튼 이벤트와 코어의 비동기 PlayerLoop를 실행한다.
- 잔액 변화/차감 거절/다른 재화/숨김·재표시/반복 초기화/연타/준비 중 리셋/이전 취소 알림/3웨이브/패배/재시작을 검사한다.
- 결과 판정과 수치는 테스트 입력이며, 실제 몬스터 AI나 시설 피해를 검증하지 않는다.
- 검사는 Unity Test Runner 테스트가 아닌 프로젝트 자체 Play Mode 검증 도구다.
- 테스트 씬의 매니저만 사용하며 영구 재화·저장 데이터는 수정하지 않는다.
- 실제 검증 결과는 `RuntimeHudValidation.md`에 기록한다.
- 기존 UI 회귀 검사: `Game > UI > Validate Existing UI Regression` 또는 `Game.UI.Editor.MvpRuntimeHudValidation.RunRegression`.
- 검증 전 열린 씬을 모두 저장해야 한다. 메뉴 검증은 종료 후 기존 씬 구성(추가 씬 포함)을 복원한다.

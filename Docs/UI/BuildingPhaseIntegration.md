# 건설 행동 UI와 코어 단계 연동

## 작업 범위 (2026-09-12)

- 작업 경로: `D:\Documents\GitHub\OZGL2_2`
- 새 작업 브랜치: `feature/ui-building-phase-binding`
- 기존 `feature/ui-integration-validation`의 미커밋 작업을 보존한 상태에서 분기했다.
- `origin/dev`는 조회/fetch만 했다. 최신 팀원 소스의 전체 merge/pull은 하지 않았다.
- UI 어댑터, 모의 입력, 별도 테스트 씬/폰트, 검사 코드만 추가한다. 코어/건물/재화/유닛 원본과 공용 씬, 프리팹, ProjectSettings, Packages를 변경하지 않는다.

## 동작 계약

`CoreBuildingActionBinding`은 기존 `BuildingActionPanel`에 **건설 단계 허용 여부만** 전달한다.

- `GameFlowController.CanEnterBuildMode()`가 true이고 코어가 활성화된 경우에만 조작 허용.
- 전투 준비, 전투, 전투 정산, 보상, 분기 선택, 종료에서는 건설/업그레이드/해체 잠금.
- 준비 단계 이벤트는 코어 내부 전환 잠금 해제 전에 발생하므로 그 프레임에는 잠그고 다음 프레임에 한 번 재확인한다. Update 폴링 없음.
- 코어의 None/재시작 및 소스 파괴 시 오래된 선택/견적을 지운다.
- UI 비활성화, 재초기화, 재시작이 실제 건물 요청을 취소하지는 않는다. 미완료 요청 ID와 응답 처리는 기존 패널 계약을 유지한다.
- 비활성화/재초기화 시 이벤트 구독과 예약 재확인을 정리한다. 오래된 콜백이 새 실행 상태를 덮어쓰지 않는다.
- 코어 누락/파괴/바인딩 비활성화 상태는 잠금으로 처리한다.
- 코어 컴포넌트만 비활성화하는 경우에는 코어에 활성 상태 이벤트가 없으므로, 호출자가 `Refresh()`를 호출하거나 바인딩도 함께 비활성화해야 한다.

UI 잠금은 실제 작업 권한 검사를 대체하지 않는다. 건물 담당자의 요청 처리기에서 현재 단계, 대상, 비용을 다시 검사하고 실패 시 부분 적용 없이 응답해야 한다.

## 담당자 연동

1. UI 오브젝트에 `CoreBuildingActionBinding`을 추가하고 `_panel`, `_flow`를 연결한다. 런타임에서는 `Initialize(panel, flow)` 가능.
2. 실제 선택/해제 시 건물 담당자가 `ShowActions(견적)` / `HideActions()`를 호출한다.
3. `ActionRequested`에는 하나의 실제 처리 주체만 연결한다. 테스트용 `MvpBuildingPhaseSample`과 실제 처리기를 동시에 연결하지 않는다.
4. 요청 처리기는 조건을 재검사한 뒤 `TryResolveRequest(requestId, succeeded, message)`로 완료/실패를 응답한다.
5. 성공 후 최신 선택 상태와 견적을 다시 전달한다. 성공 응답만으로 다음 요청을 자동 허용하지 않는다.

## 독립 테스트

- 씬: `Assets/Scenes/Test/MvpBuildingPhaseTest.unity`
- 생성 메뉴: `Game > UI > Create Missing Building Phase Test Scene`
- 자동 검사: `Game > UI > Validate Building Phase In Play Mode`
- 자동 검사 전 현재 씬을 저장하고 Play Mode를 종료한다. 메뉴 검사는 기존 씬 구성을 복원한다.
- 숨김 배치 진입점: `Game.UI.Editor.MvpRuntimeHudValidation.RunBuildingPhase` (`-batchmode`, `-quit` 없이 실행; 검사가 Play 종료 후 자동 exit).
- Play 후 왼쪽 아래의 공간/건물 선택 → 상단 웨이브 시작 → 오른쪽 승리/패배/보상 처리로 잠금과 복귀 확인.
- 실제 로컬 코어 단계와 기존 테스트 스포너를 사용하지만, 건물 견적과 응답은 모의 데이터다. 실제 건물 생성, 업그레이드, 해체, 비용 차감/환급은 수행하지 않는다.
- 기존 런타임 UI 씬을 복사하고 별도 한글 폰트를 사용한다. 원본 씬/프리팹/공유 폰트 및 Build Settings는 저장하지 않는다.

## 검증 결과

Unity 6000.3.23f1 / Windows Editor에서 숨김 배치로 실행했다. 모든 실행의 종료 코드는 0이다.

| 검사 | 결과 | 로그 |
| --- | --- | --- |
| 신규 단계 연동 첫 실행 | 404 PASS | `Logs/building-phase-first-20260912.log` |
| 테스트 화면 배치 보완 후 재실행 | 404 PASS | `Logs/building-phase-layout-20260912.log` |
| 실제 로컬 코어/재화 HUD + 분기 선택 + 유닛 정보 회귀 | 252 + 85 + 83 = 420 PASS | `Logs/building-phase-runtime-regression-20260912.log` |
| 아티팩트 선택 UI 회귀 | 1193 PASS | `Logs/building-phase-artifacts-regression-20260912.log` |
| 기존 HUD/건물 정보/건물 행동/유닛 정보 회귀 | 98 + 113 + 150 + 157 = 518 PASS | `Logs/building-phase-editor-regression-20260912.log` |

반복 실행을 중복 합산하지 않은 자체 검사 합계는 **2535개**다. Unity Test Runner의 NUnit 테스트 개수가 아니다. 실제 게임의 건물 생성이나 AI 전투를 검증했다는 의미도 아니다.

- 준비, 전투 준비, 전투, 정산, 보상, 분기 완료 선택, 종료, 재시작의 UI 잠금 확인.
- 중복 클릭, 잘못된 요청 ID, 늦은 성공/실패 응답, 새 견적 대기, 비활성화/재연결, 소스 파괴와 예약 콜백 취소 확인.
- 준비 단계 이벤트 안에서는 잠금 유지, 다음 프레임에서 코어 권한을 다시 읽은 뒤 해제되는 것을 확인.
- 모의 건물 요청 전후 실제 골드가 변하지 않음을 확인.
- 준비/전투 화면 1280×720 및 1920×1080 캡처, 한글 글리프/텍스트 넘침 검사. 새 테스트 씬의 하단 이동 버튼과 웨이브 시작 버튼 겹침을 보완했다.
- 캡처: `Logs/BuildingPhaseValidation/preparation-*.png`, `battle-*.png`.
- 신규 C# 컴파일 오류나 검사 중 Error/Exception 없음. 기존 코어의 CS1998 경고(`GameFlowController.cs:305`)는 수정하지 않았다. 플랫폼 플레이어 빌드는 실행하지 않았다.
- 작업 시작 전 파일 해시 기준으로 기존 UI 검사 진입점 1개만 수정했다. 나머지 기존 파일은 유지하고, 새 파일 13개(메타 포함)를 추가했다. 팀원 소스, 원본 UI 씬/프리팹/공유 폰트, ProjectSettings, Packages는 작업 시작 상태와 동일하다.
- 새 Unity 에셋/스크립트 6개의 `.meta` GUID에 중복이 없음을 확인했다. 커밋, 푸시, PR, 머지는 하지 않았다.

## 남은 작업

- 보존한 미커밋 작업을 정리한 뒤 최신 dev와 통합하고 동일 API 계약을 재검증.
- 건물 담당자와 실제 선택/견적/요청 응답 API를 합의한 뒤 연결.
- 전체 전투/건물/재화가 연결된 게임 씬에서 수동 플레이와 빌드 검증.
- 현 작업으로 최종 게임 씬이나 실제 건물/재화 연동이 완료된 것은 아니다.

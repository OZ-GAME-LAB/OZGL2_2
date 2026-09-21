# UI 연동 점검 보고 — 2026-09-21

## 작업 기준

- 프로젝트: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-integration-validation`
- 기준 커밋: `5a7b8b8` (`origin/dev`, 유닛 패시브 및 런타임 효과 시스템 확장)
- 팀원이 수정한 UI 커밋 `bf02508`을 기준으로 기존 동작을 보존했다.
- 팀원 전투·건물·재화 원본 코드는 수정하지 않았다. 변경 범위는 UI 연결 코드, UI 독립 검사용 테스트 대역, 이 문서뿐이다.
- 커밋·푸시·PR·머지는 진행하지 않았다. PR을 만들 때 대상은 `dev`다.

## UI에 필요한 데이터와 공개 API

| UI 표시/동작 | 데이터·이벤트 | 호출 API | 비고 |
|---|---|---|---|
| 현재 게임 단계 | `GameFlowController.CurPhase`, `PhaseChanged` | `TryStartWave()`, `RequestProgressStage()`, `RequestProgressQuarter()` | 단계에 따라 건설 버튼 활성 상태를 갱신한다. |
| 분기·웨이브 | `WaveController.CurQuarter`, `CurWave`, `WaveChanged` | `Initialize(flow, spawnManager, runtimeUnitManager)` | 최신 계약은 스폰과 런타임 유닛 관리자 모두 필요하다. |
| 골드·보석 HUD | `RunCurrencyManager.GetBalance()`, `BalanceChanged`, `IsInitialized` | `TrySpend()`, `TryAdd()`, `Initialize(waves, flow, effects, coreProgress)` | UI는 잔액을 직접 변경하지 않고 공개 API 결과만 표시한다. |
| 건설 카탈로그·정보·행동 | 선택 슬롯, 건물 데이터, 견적 | `TryBuild()`, `TryUpgrade()`, `TryDemolish()`, `CanAffordCandidate()`, `CanAffordUpgrade()`, `GetUpgradeCost()` | 성공한 호출 뒤 원본 상태를 다시 읽어 UI를 갱신한다. |
| 코어 레벨 | `BuildingCoreProgress.CurrentLevel`, `Changed` | `BuildingBuildController.Initialize(currency, flow, coreProgress)` | 최신 초기화 계약에 맞춰 연결했다. |
| 유닛 정보·생존 수 | 런타임 유닛 데이터와 `IRuntimeUnitManager.GetRemain()` | UI 선택 바인딩 및 런타임 카운트 갱신 | 팀 유닛 데이터가 표시 기준이다. |
| 유물 선택 | `ArtifactManager.TryCreateCandidates()` | `SelectAndApplyAsync()` | 후보 생성 이후 실제 비동기 UI 선택 주입은 아직 팀 코드의 TODO다. |

## 필수 UI 스크립트

- `GameUIController`: 공통 HUD와 단계별 패널의 표시 상태를 관리한다.
- `CoreHudBinding`: 게임 단계와 웨이브 데이터를 HUD에 연결한다.
- `RunGoldHudBinding`: 런타임 재화 잔액과 변경 이벤트를 HUD에 연결한다.
- `CoreBuildingActionBinding`: 건설 행동 UI의 단계별 잠금 상태를 연결한다.
- `RuntimeBuildingUiBinding`: 팀 건설 API와 카탈로그·정보·건설·업그레이드·해체 UI를 연결한다.
- `RuntimeUnitInfoBinding`: 선택한 실제 유닛 정보를 정보 팝업에 표시한다.
- `ArtifactRewardBinding`: 유물 후보를 선택 팝업에 표시하고 선택 결과를 전달한다.
- `TeamBuildingUiStartup`: 팀 통합 씬에서 공통 HUD와 팀 런타임 객체를 연결한다.
- `MvpRuntimeCombatFixture`: 실제 전투 없이 UI 독립 씬에서 최신 `WaveController` 계약을 만족시키는 테스트 전용 대역이다. 출시 씬이나 팀 전투 씬에는 사용하지 않는다.

## 씬 분류

| 씬 | 분류 | 사용 목적/주의사항 |
|---|---|---|
| `Assets/Scenes/UI/PlayerUI.unity` | 사용 가능 — UI 독립 검증 | 공통 HUD, 팝업, 결과 UI를 빠르게 확인하는 UI 테스트 씬이다. 실제 전투 완성 씬은 아니다. |
| `Assets/Scenes/UI/PlayerBuildingIntegration.unity` | 사용 가능 — 건설 UI 검증 | 실제 건설·재화 공개 API와 fixture 데이터를 사용해 건설/업그레이드/해체를 검사한다. |
| `Assets/Scenes/UI/PlayerBuildingWorldInput.unity` | 사용 가능 — 입력 검증 | 월드 슬롯 클릭과 팝업 입력 차단을 검사한다. |
| `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity` | 갱신 필요 | 최신 팀 `BootStrap`의 `_spawnManager`, `_runCurrencyManager`, `_buildController`, `_buildingCoreProgress` 연결이 현재 저장본에 없다. 최신 `Test_Building` 기준으로 재생성 또는 참조 마이그레이션 후 사용한다. |
| `Assets/Scenes/Test/Test_Building.unity` | 팀 원본 — 수정 금지 | 최신 팀 연결이 존재하는 기준 씬이다. UI 작업에서는 읽기와 비교만 한다. |

## 연동 및 검증 결과

- Unity 6000.3.23f1에서 최신 `dev` 기준 컴파일 통과: C# 컴파일 오류 0, 종료 코드 0.
- 변경 후 메인 프로젝트 컴파일 통과: C# 컴파일 오류 0, 종료 코드 0.
- 격리된 검증 복사본에서 건설 UI 런타임 검사 115개 PASS 로그를 확인했다.
- 실제 건물 후보, 건설·중복 결제 방지·해체 환급, 골드+보석 비용, 재화 부족의 원자성, 단계별 건설 잠금, 전투 시작/패배 경로와 실제 유닛 정보 83개를 검사했다.
- 최신 Core의 코어 레벨 보상 계산과 이벤트 완료 대기 순서에 UI 테스트 씬 및 검사를 맞춘 뒤 PlayerUI 검사 579개와 승리·재화 회귀 검사 130개가 통과했다.
- `PlayerUI.unity`는 전체 재생성하지 않고 코어 진행, 전투 테스트 대역, 레벨 1 코어 참조만 81줄로 추가해 기존 팀/사용자 UI 배치를 보존했다.
- 모든 UI 런타임 검사가 끝난 뒤 Unity 종료 과정에서 팀 소유 `InGameCameraController.OnDestroy()` 62행의 `NullReferenceException`이 발생했다. UI 검사는 먼저 통과했지만, 전체 통합 검증을 완전 통과로 표시하지 않는다.
- Unity 종료 시 자동 변경된 `NotoSansKR SDF.asset`은 기준 커밋 상태로 즉시 복구했으며 변경 목록에 포함하지 않았다.

## 팀원 확인 요청

1. `InGameCameraController.OnDestroy()`가 초기화되지 않은 상태에서도 안전하게 구독 해제하도록 수정하거나, 모든 생성 경로에서 초기화를 보장해 달라.
2. `ArtifactManager.SelectAndApplyAsync()`가 사용할 선택 UI 계약을 확정해 달라. 필요한 형태는 후보 목록과 `CancellationToken`을 받는 비동기 선택, 취소/닫기 결과, 반환 값이 현재 후보에 포함되는지 확인하는 흐름이다.
3. `PlayerTeamBuildingIntegration.unity`를 최신 `Test_Building` 기준으로 재생성할지, 기존 씬의 참조만 마이그레이션할지 확인해 달라.

현재 상태는 **UI 독립 검증 및 건설 공개 API 연동 가능, 팀 전체 플레이 통합은 제한 사항 있음**이다.

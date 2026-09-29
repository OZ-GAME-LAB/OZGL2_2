# UI 연동 점검 보고 — 2026-09-21

## 2026-09-29 게임 루프 통합

- 작업 브랜치는 `feature/ui-game-loop-integration`이며 PR 대상은 `dev`다. 직접 머지하지 않는다.
- UI 소유 씬 `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity`에 `건설/준비 → 웨이브 시작 → 전투 → 승리/패배 → 자동 재화 보상 → 계속 → 다음 웨이브 → 최종 종료 선택 → 결과` 흐름을 연결했다.
- `CoreGameLoopUiBinding`은 `GameFlowController`, `WaveController`, `RunCurrencyManager`의 상태를 읽어 보상·이벤트·상점·결과 UI만 표시한다. 전투 판정, 보상 계산·지급, 노드 진행은 팀 Core 권한을 그대로 사용한다.
- 웨이브 보상 팝업의 두 줄 문구가 잘리지 않도록 높이와 줄바꿈을 수정했고, 패배·승리 결과에서 재시작 버튼이 활성화되는 것을 확인했다.
- 최신 팀 테스트 씬에 현재 `SpawnManager` 필수 참조와 `DamageResolver`가 없어서, UI 통합 씬 생성 시 팀 소유 `Assets/Prefabs/Units/Entire Unit System For Merge/SpawnManager.prefab`을 사용하고 팀 `DamageResolver` 컴포넌트 한 개를 보장한다. 팀 원본 `Test_Building.unity`와 팀 스크립트는 수정하지 않았다.
- Unity 6000.3.23f1 격리 Play Mode에서 실제 팀 건설·재화·Core·스폰 API를 사용한 268개 검사를 통과했다. UI 런타임 오류는 0건이며 1280×720 보상·패배·승리 화면의 텍스트 잘림도 검사했다.
- 전투 결과 검증은 팀 Core의 `ResolveBattleAsync()` 경로로 수행했다. 실제 스폰과 Battle 진입은 확인했지만 모든 유닛이 사망할 때까지의 장시간 AI 전투 완주는 별도 팀 전투 테스트가 필요하다.
- 최신 `origin/dev` 팀 유닛의 랠리 이동 중 `[Unit_Gateway] A_WA_T1_전사(Clone) Rally movement failed.`가 5회 발생했다. 준비 완료 처리는 계속되어 루프는 진행되지만, 유닛 이동 담당자가 스폰·랠리 위치와 장애물 충돌을 확인해야 한다.
- 별도 소크 검사에서는 실제 아군 생산 건물과 적 웨이브가 교전하고 `DamageResolver`를 통한 피해·사망·그룹 전멸까지 발생했다. 그러나 테스트 전용 5배속 45초 안에도 `Reward` 또는 `Finished`로 전환되지 않아 자동 승패 완료는 현재 팀 전투 블로커다.
- `IRuntimeUnitManager.TeamWiped` 이벤트는 공개되어 있지만 `WaveController`는 현재 `UnitDied`만 구독한다. 남은 그룹의 전진·재교전과 마지막 전멸 시점의 승패 재평가를 유닛/Core 담당자가 함께 확인해야 한다. UI에서 임의로 승패를 결정하는 우회는 추가하지 않았다.
- 팀 `ArtifactManager.SelectAndApplyAsync()`의 실제 선택 UI 호출은 여전히 TODO다. 현재 Core의 임시 `TestWaitingScript` 완료 게이트를 플레이어용 `계속` 버튼이 해제하므로 MVP 진행은 가능하지만 정식 아티팩트 획득 완료로 보지 않는다.
- 검증은 원본 프로젝트를 열어 둔 채 `C:\Users\User\Documents\ChatGPT\DND Project\_unity_validation_20260929_ui` 복제본에서 수행했다. 원본 Unity 창을 조작하거나 팀 씬을 저장하지 않았다.

## 2026-09-29 최신 dev 재검증

- 작업 브랜치는 `feature/ui-artifact-runtime-integration`, 기준은 최신 `origin/dev` 커밋 `2ba46a1` (`[Feat] 아웃게임 MVP 구현`)이다. PR 대상은 `dev`이며 직접 머지는 진행하지 않는다.
- 이전 UI 커밋 전체를 가져오지 않고 아티팩트 선택 UI 파일만 선별 이관했다. 팀 소유 `ArtifactManager`, `GameFlowController`, 전투·건설·재화·아웃게임 코드와 공유 씬은 수정하지 않았다.
- Unity 6000.3.23f1 격리 복제본에서 전체 스크립트 컴파일을 완료했다. C# 컴파일 오류 0, Unity 종료 코드 0이다.
- 같은 복제본에서 `MvpRuntimeHudValidation.RunArtifacts`를 실행해 아티팩트 UI Play Mode 1,229개 검사를 통과했다. 선택·포기·취소·중복 요청 차단·반복 열기/닫기·1/3/5개 후보·페이지 이동·1280×720 및 1920×1080 레이아웃을 확인했다.
- `IArtifactSelectionUI`의 `Open()`, `SelectAsync(candidates, token)`, `Close()`와 이를 구현하는 `ArtifactRewardBinding`은 최신 팀 코드와 함께 컴파일되고 독립 동작한다.
- 실제 전투 흐름 연결은 아직 완료가 아니다. 팀 소유 `ArtifactManager.SelectAndApplyAsync()` 122~130행에 UI 주입·선택 검증·`TryAdd()`·닫기 TODO가 있고, `GameFlowController.WaitArtifactSelection()`은 임시 `TestWaitingScript.WaitToggle()`을 계속 사용한다.
- UI에서 `ArtifactSelectionRequested`를 받아 후보를 다시 추첨하거나 임시 토글을 직접 해제하는 우회 코드는 추가하지 않았다. 정식 연결 시 후보 생성과 실제 지급의 단일 권한은 `ArtifactManager`가 유지해야 한다.
- 검사는 원본 `D:\Documents\GitHub\OZGL2_2`가 아닌 `C:\Users\User\Documents\ChatGPT\DND Project\_unity_validation_20260929_ui`에서 수행했다. 열려 있는 원본 Unity 창과 원본 프로젝트 설정은 조작하지 않았다.

## 2026-09-28 업무 시작 점검

- 아티팩트 승리 보상 팝업을 플레이어용 마계 판타지 화면으로 한 차례 더 정리했다. 가변 후보 중앙 정렬, 선택 전 작은 `건너뛰기`, 선택 후 강조된 `선택하기`, 간결한 효과 문구를 적용했다.
- Git으로 공유되지 않는 `Assets/ExternalAssets` 참조를 사용하지 않고, 추적 가능한 `Assets/Art/Sprites/UI/ArtifactUnknownRelic.png`를 기본 유물 이미지로 추가했다. 팀 데이터의 실제 `ArtifactData.Icon`이 있으면 해당 Sprite가 우선한다.
- 기본 유물 이미지의 흑요석·진홍·금색 분위기에 맞춰 얇은 금색 이중 프레임, 중앙 문양, 제목 장식과 낮은 명도의 워터마크를 적용했다.
- 분리 Unity 6000.3.23f1에서 C# 컴파일과 아티팩트 선택 UI Play Mode 1,229개 검사를 통과했다. 팀 소유 `ArtifactManager`, `GameFlowController`, 전투·건설·재화 코드는 수정하지 않았다.
- 배치모드 복제 Canvas 캡처는 연속 해상도 전환 시 일부 배치가 간헐적으로 누락될 수 있어 최종 시각 승인은 열린 Editor에서 별도로 확인한다. 기능·레이아웃·실제 아이콘 우선 표시 검사는 통과했다.

- `git fetch origin` 후 현재 브랜치 `feature/ui-team-integration-scene`, `origin/feature/ui-team-integration-scene`, `origin/dev`가 모두 `741ab28`로 동일함을 확인했다. 추가 pull·merge는 필요하지 않았다.
- 기존 미커밋 UI·씬·폰트 변경과 `NodeController` 경로 정리 내역은 덮어쓰기나 stash 없이 그대로 보존했다.
- Unity 6000.3.23f1에서 `MvpRuntimeHudValidation.RunArtifacts`를 실행해 스크립트 컴파일과 아티팩트 선택 UI Play Mode 검사 1,229개를 통과했다.
- 검사 범위는 `IArtifactSelectionUI`의 열기, 선택, 포기, 취소, 중복 요청 차단, 반복 열기·닫기와 기존 아티팩트 팝업 회귀 동작이다.
- 검사 전후 Git 변경 목록이 동일해 테스트 실행으로 팀 코드·씬·프리팹이 추가 변경되지 않았다.
- 팀 소유 `ArtifactManager.SelectAndApplyAsync()`에는 여전히 실제 선택 UI 주입·호출 TODO가 남아 있다. UI 측 계약과 구현은 준비됐지만 실제 전투 보상 적용 완료로 보지는 않는다.
- Unity 라이선스 토큰, DX12 정보 큐, 외부 `CartoonCoffee` fallback 셰이더 메시지는 확인됐지만 신규 C# 컴파일 오류나 검사 예외는 발생하지 않았다.
- 최초 1920×1080 5개 후보 2페이지 캡처에서 해상도 전환 직후 일부 CanvasRenderer가 누락되는 검증기 문제가 발견됐다. Editor 검증기만 보강해 두 프레임 안정화와 팝업 배경·주 버튼 픽셀 검사를 추가했고, 재실행한 1,215개 검사가 모두 통과했다.
- TMP 글리프 검사를 읽기 전용으로 변경한 최종 실행에서는 테스트 전후 세 폰트 에셋의 SHA-256 해시가 모두 동일했다. 이후 동일 검사가 소스 폰트를 자동 갱신하지 않음을 확인했다.

## 2026-09-23 추가 확인

- 현재 작업 브랜치는 `feature/ui-team-integration-scene`이다. PR 대상은 계속 `dev`이며 머지는 진행하지 않는다.
- 외부 구매·다운로드 에셋은 `Assets/ExternalAssets`로 정리되어 있다.
- 저장소의 `.gitignore`가 `Assets/ExternalAssets/` 내부를 제외하고, `Assets/ExternalAssets.meta`만 추적한다. 따라서 대용량 원본 에셋은 일반 Git 변경 목록과 PR에 포함되지 않는다.
- `IArtifactSelectionUI`와 이를 구현한 `ArtifactRewardBinding`을 추가했다. 기존 패널로 선택·포기·취소·중복 요청 방지를 처리한다.
- 팀 소유 `ArtifactManager.SelectAndApplyAsync()`의 실제 UI 주입 TODO와 `GameFlowController`의 임시 완료 버튼 대기는 수정하지 않았다. 담당자 계약 확정 후 연결한다.
- 현재 브랜치는 최신 `origin/dev` 커밋 `741ab28`을 반영했다.
- Unity 6000.3.23f1 분리 검증에서 C# 컴파일과 아티팩트 선택 UI 1,199개 Play Mode 검사를 통과했다.
- 실제 전투 보상 연결은 팀 매니저가 `IArtifactSelectionUI`를 주입·호출할 공개 지점을 제공한 뒤 진행한다. UI에서 후보를 다시 추첨하거나 Core 진행을 우회하지 않는다.

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
| `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity` | 사용 가능 — 팀 통합 데모 | 실제 팀 건설·재화·Core·스폰 API와 플레이어 HUD를 연결했다. 보상·패배·최종 승리·재시작 UI 검증을 통과했으며, 팀 유닛 랠리 이동 오류는 별도 확인이 필요하다. |
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

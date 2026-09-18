# UI 재화 API 연동 — 2026-09-14

> 이 문서는 같은 날 먼저 진행한 재화 연동 기록이다. 이후 승리 유물 연동으로 API가
> Initialize(WaveController, EffectManager)로 갱신되었다. 현재 연결 방법·검증 결과·제한은
> [승리 유물 보상 UI 연동](VictoryRewardIntegration20260914.md)을 기준으로 확인한다.

## 상태

코드 반영 및 별도 C# 어셈블리 컴파일 완료. **Unity Editor 재임포트, 씬 연결, Play Mode 동작 검증은 미완료**다.
강사/조교 확인을 위해 Unity가 계속 열려 있으므로 창 조작, Play 시작/종료, 씬 교체/저장, 동일 프로젝트의 두 번째 Unity 실행은 하지 않았다.

- 실제 작업 경로: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-economy-api-integration`
- 시작 HEAD: `aed54244b180588fe72b8bf0813f29c984350c92`
- 재화 원본: `origin/dev`의 `3d5da92b7f3a283d3afb084812a5ef727ae71f14`
- Unity: `6000.3.23f1`
- 전체 dev merge/pull, 커밋, push, PR, merge는 하지 않음.

## 구현 내용

- UI 통합 샘플을 `Initialize(WaveController)`, `IsInitialized`, `TryAdd(CurrencyType, int)`, `TrySpend(CurrencyType, int)` 계약에 맞춤.
- 초기화/종료는 잔액 이벤트가 없으므로 기존 HUD의 명시적 `Refresh()` 유지.
- 기존 임시 Gold +30 지급을 제거하고 `TryApplyWaveReward()`로 팀원의 보상 테이블을 사용.
- 보상 단계 밖의 입력, 같은 분기/웨이브 중복 클릭, 잔액 이벤트에서의 동기 재진입을 차단.
- 지급 API가 false를 반환하면 지급 키를 복원하고 재시도를 허용. 코어 보상 대기는 성공한 경우에만 해제.
- 지급 처리 중 샘플의 리셋/재화 변경/웨이브 이동 버튼 재진입을 차단.
- 재화 이벤트 수신자가 예외를 던지면 이미 지급됐을 가능성이 있으므로 지급 키를 되돌려 자동 재시도하지 않음. 이 경우 Console 원인을 해결하고 명시적으로 재시작해야 함.
- 기존 Play 검사를 팀원 테이블의 실제 보상값 기준으로 갱신하고 복수 재화, 오버플로 실패 후 재시도, 재진입, 재시작, 무한 구간 보상 검사를 추가. **추가한 검사를 실행한 것은 아님.**

## 원본 및 변경 범위

재화 원본 13개를 그대로 반영했다. `git hash-object --path=...`와 원본 Git blob 비교 결과 불일치 0개.

- WaveRewardTable 데이터와 메타
- CurrencyRewardCalculator 및 메타
- QuarterRewardData, WaveRewardData, WaveRewardTable 정의 및 메타
- RunCurrencyManager, PersistentCurrencyManager, CurrencyTestPanel

위 파일은 팀원 로직을 임의로 수정한 것이 아니라 해당 커밋의 원본을 반영한 것이다.
API 변경에 맞춰 팀원 테스트 패널도 원본 그대로 반영했으며, 팀원의 EconomyTestScene에 새 필드를 자동 연결하지 않았다.

우리 변경:
- `Assets/Scripts/UI/Samples/MvpRuntimeHudSample.cs`
- `Assets/Scripts/UI/Editor/MvpRuntimeHudBuilder.cs`
- `Assets/Scripts/UI/Editor/MvpRuntimeHudValidation.cs`
- `Assets/Scripts/UI/Editor/MvpCoreQuarterValidation.cs`
- 신규 `MvpEconomyUiSetup.cs`, `MvpEconomyUiValidation.cs` 및 메타

기존 파일 501개의 변경 전후 해시를 비교했다. 기존 파일 변경은 원본 재화 3개와 UI 4개뿐이다.
씬, 프리팹, 공용 폰트, 유닛/건물/코어 소스, Packages, ProjectSettings의 기존 내용은 보존했다.
이미 수정되어 있던 `Assets/Art/Fonts/NotoSansKR/NotoSansKR SDF.asset`도 작업 시작 시 해시와 같다.

## 현재 씬 연결 — 사용자가 편한 시점에 한 번 실행

이미 열린 씬 파일을 외부에서 덮어쓰지 않기 위해 보상 테이블 참조를 자동 저장하지 않았다.
**기존 테스트 씬에서는 아래 연결 전 웨이브 보상 버튼을 누르면 테이블 누락으로 실패한다.**

1. Unity에서 스크립트 임포트/컴파일을 기다린다. 새 메뉴가 없다면 시연이 끝난 시점에 `Assets > Refresh` 후 다시 확인한다.
2. Play가 꺼져 있을 때 `MvpRuntimeHudTest` 또는 `MvpBuildingPhaseTest` 씬을 활성화한다.
3. `Game > UI > Connect Economy In Current UI Test Scene` 실행.
4. 연결 내용을 확인한 뒤 원하는 시점에 씬을 저장한다. Undo를 지원한다.
5. 새로 Play해 확인한다. Unity 앱을 닫을 필요는 없다.

이 메뉴는:
- 지원하는 두 UI 전용 씬과 Edit Mode에서만 활성화된다.
- 현재 씬의 샘플이 참조하는 재화 매니저 한 개에만 테이블을 연결한다.
- 다른 테이블이 이미 연결되어 있으면 덮어쓰지 않는다.
- 씬 열기/닫기/저장, Play 전환, 보상 데이터 변경을 하지 않는다.
- 반복 실행 시 이미 같은 연결이면 아무것도 바꾸지 않는다.

누락된 Runtime HUD 씬을 새로 만드는 기존 빌더에는 생성 시 연결을 추가했다.
기존 자동 Play 검사도 명시적으로 실행될 때 테스트 씬의 메모리상 참조만 준비한다.

## 검증 근거와 한계

| 항목 | 결과 |
| --- | --- |
| 전체 현재 런타임 C# 소스 69개 컴파일 | PASS, 종료 코드 0 |
| 전체 현재 Editor C# 소스 23개 컴파일 | PASS, 종료 코드 0 |
| 원본 재화 파일 13개 Git blob 비교 | PASS, 불일치 0 |
| 메타 GUID 273개 검사 (LF/CRLF 모두 포함) | PASS, 중복 0 |
| git diff --check | PASS |
| 기존 씬/프리팹/공용 에셋/설정 보존 | 해시 비교 PASS |
| 열린 Unity의 새 코드 재임포트/Console | 아직 확인되지 않음 |
| 새 연결 메뉴의 실제 실행·Undo | 미실행 |
| Play Mode 통합/회귀 검사 | 미실행 |
| Player 빌드/최종 게임 씬 | 미실행 |

컴파일 방법:
- 열린 Unity가 생성해 둔 Bee 응답 파일의 참조·define·분석기 설정 사용.
- 현재 Assets의 전체 C# 소스 목록 반영.
- Unity에 포함된 `NetCoreRuntime/dotnet.exe`와 `DotNetSdkRoslyn/csc.dll` 사용.
- 출력 및 참조 어셈블리는 `Logs/UiEconomyIntegration20260914/`로 분리.
- Editor 컴파일은 위 폴더에서 방금 만든 런타임 참조 어셈블리를 사용.
- `Library/ScriptAssemblies`와 Bee 출력은 덮어쓰지 않음.

증거 파일:
- `Logs/UiEconomyIntegration20260914/runtime.rsp`
- `Logs/UiEconomyIntegration20260914/editor.rsp`
- `Logs/UiEconomyIntegration20260914/runtime-compile.log`
- `Logs/UiEconomyIntegration20260914/editor-compile.log`

기존 코어의 `GameFlowController.cs:305` CS1998 경고만 관찰됐다.
작업 전 Unity 로그에는 Unity Connect의 HTTP 403 메시지도 있었으며, 이를 재화 코드 오류로 보거나 수정하지 않았다.
별도 C# 컴파일 통과는 Unity Editor 임포트, 직렬화, Play 동작, 시각적 검증 통과를 뜻하지 않는다.
이전 2852개 자체 검사 통과 기록을 이번 변경의 검사 결과로 재사용하지 않는다.

## 다음 확인

시연에 방해되지 않을 때:
- 연결 메뉴 실행 후 새 Play: 시작 Gold 100, 1웨이브 보상 후 200, 2웨이브 후 310, 3웨이브 후 Gold 430/Gem 1 (수동 추가/소비 없는 현재 테이블 기준).
- 같은 보상 버튼을 반복해서 눌러도 추가 지급되지 않는지 확인.
- 리셋 시 Gold 100/Gem 0으로 복귀하는지 확인.
- `Game/UI/Validate Runtime HUD In Play Mode`로 기존 런타임 + 신규 Economy + 분기 + 유닛 정보 검사 실행. 이 메뉴는 씬 교체와 Play 전환을 수행하므로 시연 중에는 실행하지 않는다.
- 이어서 Building Phase 및 나머지 기존 회귀 검사 실행.

실제 건설 비용/환급·업그레이드 연결은 여전히 담당자 API 협의가 필요하다.
`Docs/UI/BuildingEconomyIntegrationReview20260914.md`의 PlayerWallet/RunCurrencyManager 소유권 차이를 UI의 잔액 복사로 우회하지 않는다.
현재 팀원 원본은 기본 분기 수 3, 보상 계산용 베이스캠프 레벨 1을 사용한다. 기획의 전체 분기 수/강화 효과에 맞춘 팀원 로직 변경은 하지 않았다.

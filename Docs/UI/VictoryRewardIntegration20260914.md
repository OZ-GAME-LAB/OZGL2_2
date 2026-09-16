# 승리 유물 보상 UI 연동 — 2026-09-14

## 현재 상태

**코드 연동 및 격리 C# 컴파일 통과. 씬 연결 메뉴 실행, 실제 Play Mode/시각 검사, Player 빌드는 미실행.**
Unity는 강사/조교 확인용으로 계속 열려 있다. 창 조작, Play 전환, 씬 열기/닫기/저장, 두 번째 에디터 실행을 하지 않았다.

- 작업 경로: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-economy-api-integration`
- HEAD: `aed54244b180588fe72b8bf0813f29c984350c92`
- Unity: `6000.3.23f1`
- 팀원 원본: `origin/feat/economy-artifact-integration`, `03aa092a2b43e240c6232dff22510adc39382331`
- 해당 원본은 아직 dev에 합쳐지지 않은 브랜치다. 사용자가 필요한 원본을 먼저 반영하는 방식에 동의했다.
- 전체 브랜치 merge, commit, push, PR, dev/main 변경은 하지 않았다. 추후 PR의 대상은 **dev**다.

## 이번 연결

기존 PDF 기준 승리 보상 프리팹을 재사용한다. 새 최종 클리어 씬을 만든 작업이 아니다.

1. UI 통합 샘플의 Core가 Reward에 진입한다.
2. Core의 보상 대기가 설치된 다음 프레임에 팀원 RunCurrencyManager.TryApplyWaveReward()를 호출한다.
3. 지급된 Gold/Gem을 표시하고, ArtifactManager.TryCreateCandidates()의 실제 후보를 띄운다.
4. 카드 선택은 표시만 바꾼다. 확정 시 원래 후보의 ArtifactData를 ArtifactManager.TryAdd()에 전달한다.
5. 실제 인벤토리 중첩·EffectManager 등록 성공 또는 명시적인 포기 이후에만 TestWaitingScript 대기를 해제한다.

같은 플레이/분기/웨이브에서는 지급을 반복하지 않는다. 후보를 다시 열거나 획득에 실패해도 최초 후보를 유지한다.
재화 API의 false는 재시도 가능하지만 예외는 일부 적용 가능성이 있어 자동 재지급하지 않는다.
유물 획득 false는 재선택/포기 가능하다. 받을 수 있는 후보가 0개이면 빈 창을 열지 않고 진행한다.
플레이 재시작 시 보상 ID/후보/요청을 폐기하고 해당 인벤토리의 효과만 제거한다. 다른 Source의 효과는 유지한다.

ArtifactManager의 미완성 SelectAndApplyAsync는 수정/호출하지 않았다.
우리 ArtifactRewardBinding에서 현재 공개된 TryCreateCandidates/TryAdd API로 연결했다.
RunCurrencyManager.Initialize 호출은 새 원본의 (WaveController, EffectManager) 형태로 갱신했다.
유물 참조가 없는 기존 테스트 씬은 종전 수동 보상 버튼 흐름을 유지한다.

## 사용자가 직접 확인할 방법

Unity 앱을 닫을 필요는 없다. 시연이 끝나고 **Play가 꺼진 상태**에서:

1. 스크립트 컴파일 완료를 기다린다. 메뉴가 없으면 Assets > Refresh 후 Console을 확인한다.
2. `Assets/Scenes/Test/MvpRuntimeHudTest.unity` 또는 `MvpBuildingPhaseTest.unity`를 활성화한다.
3. **Game > UI > Connect Victory Rewards In Current UI Test Scene** 실행.
4. 연결 내용을 확인하고 원하는 시점에 씬을 저장한다. Undo를 지원한다.
5. 새 Play → 웨이브 시작 → 샘플의 적 전멸/승리 버튼 → 자동 승리 보상창.
6. 카드 확정 또는 미선택 상태의 모두 포기를 선택해 다음 웨이브로 진행한다.

연결 메뉴는 기존 보상 프리팹을 인스턴스화하고 현재 UI 테스트 씬에 매니저/바인딩을 연결한다.
다른 참조가 이미 지정되어 있으면 덮어쓰지 않고 중단한다. 씬 파일·프리팹·원본 보상 데이터는 자동 저장하지 않는다.
MvpArtifactRewardTest는 여전히 **모의 UI 단독 테스트 씬**이며, 실제 연동 검사는 위 Runtime HUD 씬에서 한다.

자동 Play 검사 메뉴:
**Game > UI > Validate Victory Reward Integration In Play Mode**

이 검사는 사용자가 직접 실행해야 한다. 저장되지 않은 씬이 있으면 중단하고, 저장된 씬 구성을 보관한 뒤 테스트 씬/Play로 전환했다가 복원한다.
검사용 연결은 메모리상의 테스트 씬에만 적용하며 원본 씬을 저장하지 않는다.
기존 수동 HUD/재화 검사는 메모리상 자동 유물 연결만 끊어 서로 간섭하지 않게 한다. 저장된 씬의 연결은 바꾸지 않는다.
검사 코드에는 실제 지급/효과 등록, 중복 입력/동기 재진입, 비활성화 복원, 포기, 최대 중첩 실패, 재시작/옛 응답,
다른 Source 보존, 재화 보정, 보스 후보 고갈, 패배 시 보상 없음이 포함된다.
**검사 코드 추가와 실제 검사 통과는 다르다. 이번에는 실행하지 않았다.**

## 원본·보존 범위

승인된 원본의 Artifact/Economy/Effects 관련 75개 파일과 최소 Units 데이터 의존성 20개 파일(메타 포함)을 반영했다.

Units에는 AllyStatModifier, EnemyStatModifier, UnitModifierApplyType, Ally/Enemy의 Class/Type 열거형 7개만 추가했다.
기존 UnitTeam/UnitStatType/UnitStatModifierType은 내용이 호환되므로 복제하거나 이동하지 않았다.
팀원의 AI/이동/전투/스탯 계산 로직은 수정하지 않았다.

원본 비교:
- 95개 중 80개는 Git blob 일치.
- 나머지 15개는 원본에 없던 **파일 끝 LF 1개**만 추가됨을 바이트 비교로 확인.
- 원본 로직, 필드, GUID, 데이터 수치 변경 없음.
- 시작 시 기존 516개 파일 해시 보관. 기존 파일 변경은 원본 재화 5개와 우리 UI 3개뿐.
- 기존 씬/프리팹/공유 폰트/Units/Core/건물/Packages/ProjectSettings의 내용은 보존.
- 기존 미커밋 `NotoSansKR SDF.asset` 변경도 그대로 보존했고 스테이징하지 않음.

우리 파일:
- `Assets/Scripts/UI/ArtifactRewardBinding.cs`
- `Assets/Scripts/UI/Samples/MvpRuntimeHudSample.cs`
- `Assets/Scripts/UI/Editor/MvpVictoryRewardSetup.cs`
- `Assets/Scripts/UI/Editor/MvpVictoryRewardValidation.cs`
- `Assets/Scripts/UI/Editor/MvpRuntimeHudValidation.cs`
- `Assets/Scripts/UI/Editor/MvpEconomyUiValidation.cs`의 새 API 호출부

## 검증 증거

- 전체 런타임 97개 C# 컴파일: exit 0.
- 전체 Editor 25개 C# 컴파일: exit 0.
- 기존 `GameFlowController.cs:305` CS1998 경고 1건.
- Assets 메타 GUID 328개: 중복 0.
- git diff --check: 통과.
- 출력: `Logs/UiVictoryIntegration20260914/` 아래 runtime.rsp, editor.rsp, runtime-compile.log, editor-compile.log.
- Unity의 Bee 참조/define/분석기 + 설치된 Roslyn 사용. 출력 DLL은 위 Logs 폴더로 격리했다.
- Library/ScriptAssemblies/Bee 출력은 덮어쓰지 않았다.
- Editor.log에는 파일 반영 도중의 CurrencyRewardType/ModifierType 누락 오류 기록과, 이후 14:40경 exit 0/도메인 재로드 기록이 있었다.
  현재 Console 화면/정확한 씬 재임포트 완료는 직접 확인하지 않았다.
- 이번 코드의 실제 메뉴/Undo/직렬화/Play/레이아웃/Player 빌드는 검증되지 않았다. 이전 검사 통과 기록을 재사용하지 않는다.

## 팀원 확인 필요 — 수정하지 않음

1. `TestArtifactData_004`: 설명은 워리어 클래스 강화지만 _applyType=0(All)이다. 클래스 한정 의도와 다를 수 있다.
2. `TestArtifactData_008`: 설명의 웨이브 골드 +50%와 달리 _currencyEffects는 비어 있다.
   이동속도 +10 설명의 _statType=9는 현재 enum에서 CriticalDamage이고 MoveSpeed는 10이다.
   UI는 원본 설명을 표시하므로 최종 수치 검증 전에 데이터 담당자 확인이 필요하다.
3. ArtifactManager.TryAdd의 동기 이벤트 수신자가 예외를 던지면 일부 적용 및 내부 _isAdding 잠금 유지 가능성이 있다.
   UI는 예외 시 자동 재시도를 차단한다. 팀원 매니저의 예외 복구 계약은 별도 협의가 필요하다.
4. 이번 연결은 테스트 스포너/승리 입력을 쓰는 독립 통합 샘플이다. 실제 몬스터 전투와 유닛 스탯 소비부를 새로 연결한 작업이 아니다.
5. Core는 3분기 마지막 보스에서 Finish 선택 시 Reward 전에 Finished로 간다. 그 동작을 바꾸지 않았다.
   최종 클리어/패배 결과 패널과 혈석 정산은 별도의 결과 데이터 API 연결이 남아 있다.
6. 시작/종료/초기화 소유권과 실제 건설 비용·환급 연결은 기존 `BuildingEconomyIntegrationReview20260914.md`의 협의 항목을 유지한다.

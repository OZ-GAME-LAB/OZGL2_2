# 플레이어 통합 UI 프리팹

## 연결 검사 메뉴

씬을 연 뒤 **Game → UI → Check Team UI Connections**를 실행한다.
필수 참조 누락, 서로 다른 매니저 연결, 중복 통합 UI/HUD, 비활성 UI,
건설 슬롯 클릭 연결, 데이터베이스 불일치, EventSystem/ArtifactManager 구성을 확인한다.
문제가 있으면 Console에 한국어 안내와 해당 오브젝트 참조를 출력한다.
Console 메시지의 컨텍스트를 통해 오브젝트를 찾고 Inspector에서 연결을 확인한다.

검사는 읽기 전용이며, 자동 연결·씬 저장·게임 상태 변경을 하지 않는다.
현재 씬 기준의 연결 검사이므로 실제 AI 전투, 승패 판정, 성능 검증을 대신하지 않는다.
의도적으로 여러 씬에 매니저를 나눈 프로젝트 구성은 현재 단일 씬 계약에 맞게 별도 검토해야 한다.

검증: Unity 6000.3.23f1 격리 프로젝트에서 정상 연결, 미연결 재화 매니저,
중복 UI, 매니저 불일치, 슬롯 입력 누락, EventSystem 비활성, UI 비활성,
다른 씬 참조 및 검사 전후 데이터/dirty 상태 보존 등 11개 검사 통과.
실행: `Game.UI.Editor.PlayerTeamUiConnectionCheck.RunBatchTests`.
결과: `Logs/PlayerTeamUiPrefab/connections.txt`.

## 파일과 범위

- 새 통합 프리팹: `Assets/Prefabs/UI/Player/PlayerTeamUI.prefab`
- 기준 씬: `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity`
- 메뉴 도구: `Assets/Scripts/UI/Editor/PlayerTeamUiPrefabTools.cs`

현재 씬에서 사용하던 UI 계층 196개 오브젝트를 묶었다. HUD(골드·보석·웨이브),
건설 카탈로그, 건물 정보·강화·해체, 재화 보상, 유물 선택, 분기 종료 선택,
승리·패배 결과 및 UI 바인딩이 포함된다. 팀 Core/건물/재화/유물 매니저,
카메라, EventSystem, 전장과 유닛은 포함하지 않는다.

기존 개별 프리팹 `PlayerBuildingCatalog`, `PlayerBuildingInfo`, `PlayerUnitInfo`,
`PlayerVictoryReward`는 이동하거나 덮어쓰지 않았다. 유닛 정보는 별도 프리팹이며
이번 통합 씬에 없던 유닛 선택 기능을 새로 연결한 것은 아니다.

## 기존 통합 씬에 적용

1. Play Mode를 종료하고 현재 씬의 작업을 먼저 보관한다.
2. `PlayerTeamBuildingIntegration`을 연다.
3. 메뉴 **Game → UI → Replace Team UI With Prefab**을 실행한다.
4. 기존 `Team Building Player UI`가 프리팹 인스턴스로 교체된다.
   기존 매니저·건설 슬롯 연결과 UI 내부 참조, 씬에서 수정한 UI 값은 인스턴스에 유지한다.
5. Play로 확인한 뒤 필요할 때 직접 씬을 저장한다. 메뉴는 씬을 자동 저장하지 않는다.
   실행 직후 Ctrl+Z로 되돌릴 수 있다.

기존 UI와 새 프리팹의 계층·컴포넌트 구성이 다르면 교체를 중단하고 Undo로 복구한다.
기존 UI가 없는 씬에 매니저를 자동 생성하는 설치 도구가 아니다.
이미 이 프리팹을 사용 중이면 다시 교체하거나 인스턴스 변경값을 덮어쓰지 않는다.

## 직접 드래그해서 사용할 때

프리팹은 **기본 비활성 상태**다. 씬 참조 28개는 에셋에 저장할 수 없어 비워 두었다.
원본 UI와 함께 켜면 중복 UI·중복 입력이 생길 수 있으므로 그대로 추가하여 켜지 않는다.
기존 통합 씬에서는 위 메뉴를 사용한다. 다른 씬에 수동 설치하려면 다음 연결이 필요하다.

- `TeamBuildingUiStartup`: 해당 씬의 wallet, waves, flow, effects, coreProgress, contentGate
- `CoreGameLoopUiBinding`: flow, waves, wallet, contentGate
- `RuntimeBuildingUiBinding`: controller, wallet, flow, slots 및 해당 씬의 database
- HUD의 `RunGoldHudBinding`, `CoreHudBinding`, `CoreRunDecisionBinding`,
  `PlayerUiNavigation`, `RuntimeBuildingSlotButton`: 해당 씬의 매니저 참조
- 건설 공간의 `RuntimeBuildingSelectionTarget`: 새 UI의 building binding 연결
- 기존 팀 초기화 흐름, ArtifactManager, 카메라 입력과 EventSystem 필요

내부 UI 참조와 폰트·이미지·데이터 에셋 참조는 프리팹에 보존된다.
씬 매니저를 프리팹 안으로 끌어 넣거나 인스턴스의 씬 연결을 에셋에 Apply하지 않는다.

## 검증

Unity 6000.3.23f1 격리 프로젝트에서 Unity PrefabUtility로 생성했다.
씬 연결 이전, 누락 스크립트/끊어진 참조 검사 및 Undo/Redo를 검사한다.
`Game.UI.Editor.PlayerTeamUiPrefabTools.ExportAndValidateBatch`는 격리 프로젝트에서만 실행되며,
기존 프리팹을 덮어쓰지 않는다. 이어서 저장된 프리팹 인스턴스 씬에 기존 전체 UI 검사를 실행한다.
전투 결과는 Core 결과 API로 발생시키므로 AI 전투 전체 완주 검사는 아니다.

원본 프로젝트의 씬과 팀원 에셋은 자동 변경하지 않았다.

2026-09-30 실행 결과: 연결 이전과 Undo/Redo 통과, 프리팹 인스턴스 씬의 UI 검사
409개 통과, UI 런타임 오류 0건. 기존 팀 유닛 랠리 이동 실패 5건은 별도 문제로 남아 있다.
로그: 격리 프로젝트의 `prefab-export-validation.log`, `Logs/PlayerTeamUiPrefab/export.txt`,
`Logs/TeamBuildingUi20260915/results.txt`.

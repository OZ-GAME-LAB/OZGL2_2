# 유닛 정보 UI

2026-09-08 / 담당: 이진우 / `feature/ui-common-hud`

## 구현 범위

`Game.UI.UnitInfoPanel`은 선택한 아군·적군의 이름, 소속, 역할, 체력과 게이지, 설명, 전투 정보, 특성·전직 요약을 표시한다.
Unity 기능 구현 스킬의 책임 분리 기준에 따라 유닛 생성·탐색·공격·피해·회복·사망 계산은 추가하지 않았다.
아래 계약은 **팀 합의 전 UI 측 연결 제안**이며 양원준 담당의 유닛 데이터/API를 대체하지 않는다.

- 유닛 변경 시 이전 내용과 아이콘 정리. 설명 누락 시 대체 문구, 전투/특성 누락 시 해당 영역 숨김.
- 체력 변화만 반영할 때 전체 정보를 다시 만들지 않고 텍스트와 게이지만 갱신.
- 이전 유닛의 지연된 체력/제거 알림으로 현재 선택이 바뀌지 않도록 인스턴스 ID 검사.
- 체력 0은 `체력 소진` 표시만 한다. 사망/제거 여부를 UI가 결정하지 않는다.
- 긴 내용은 세로 스크롤. 체력 변경은 읽던 위치를 유지하며 다른 유닛 선택 시 위로 이동.
- 닫기 및 외부 선택 해제 지원. 게임 상태를 소유하는 싱글턴/매니저 없음.

## 파일과 확인 방법

- `Assets/Scripts/UI/UnitInfoData.cs`: 불변 표시 스냅샷. ScriptableObject 또는 전투 능력치 원본이 아님.
- `Assets/Scripts/UI/UnitInfoPanel.cs`: 표시 및 선택 인스턴스 검사.
- `Assets/Prefabs/UI/MvpUnitInfo.prefab`: uGUI/TMP 정보 패널.
- `Assets/Scenes/Test/MvpUnitInfoTest.unity`: 공통 HUD + 정보 패널 + 임시 입력 버튼.
- `Assets/Scripts/UI/Samples/MvpUnitInfoSample.cs`: UI 검사 전용. 실제 게임 씬에 복사하지 않음.
- `Assets/Scripts/UI/Editor/MvpUnitUiBuilder.cs`: `Game/UI/Create Missing Unit Info Assets`. 기존 에셋은 건너뛰고 누락된 에셋만 생성.
- `Assets/Scripts/UI/Editor/MvpUnitUiValidation.cs`: `Game/UI/Validate Unit Info UI`. Play를 끈 상태에서 실행.

Unity 6000.3.23f1에서 `MvpUnitInfoTest`를 열고 Play한다.
전사/궁사/적군 선택, 체력 -25/+25, 제거 알림, 이전 유닛 알림, 긴 설명 스크롤과 닫기를 확인한다.
체력·공격력·방어력·사거리는 **UI 검사용 임시값**이다. 전투/회복/유닛 삭제/재화 계산은 수행하지 않는다.
테스트의 유닛 선택 버튼을 다시 누르면 해당 샘플 체력도 초기화된다. Play 종료 후 샘플 상태는 저장되지 않는다.
공유 Build Settings에는 테스트 씬을 추가하지 않았다.

## 연결 계약

| API | 의미 |
| --- | --- |
| `ShowUnitInfo(UnitInfoData data)` | 외부 선택/정보 변경 시 최신 스냅샷 표시. null은 예외이며 이전 선택은 유지. |
| `TryUpdateHealth(string selectionId, float currentHealth, float maxHealth)` | 현재 선택 ID와 일치하고 유효한 값일 때만 반영. 실패는 false, 부분 변경 없음. |
| `TryHideUnitInfo(string selectionId)` | 현재 유닛의 제거/디스폰 알림일 때만 정리. 이전/중복 알림은 false. |
| `HideUnitInfo()` | 새 판 시작 또는 외부 선택 해제 시 무조건 표시 초기화. |
| `SelectionId`, `HasSelection` | 현재 표시 선택 조회. |
| `InfoPanelClosed` (`Action<string>`) | 사용자 닫기로 패널 정리가 완료된 후 닫힌 인스턴스 ID 전달. 외부 Hide는 이 이벤트를 재발행하지 않음. |

`UnitInfoData` 필수 값은 `SelectionId`, `DisplayName`, `FactionLabel`, `RoleLabel`, `CurrentHealth`, `MaxHealth`다.
설명, 전투 요약, 특성 요약, 아이콘은 선택 값이다. 전투/특성 요약은 외부에서 확정한 문자열을 받으며 UI가 스탯 공식을 재구현하지 않는다.
필수 문자열은 공백을 허용하지 않는다. 체력은 유한한 수이고 현재 체력은 0 이상, 최대 체력은 0보다 커야 한다.
현재 체력이 최대를 넘으면 실제 숫자는 그대로 표시하되 게이지만 100%로 제한한다. 초과 체력 정책을 UI에서 결정하지 않는다.

연결 시 지킬 사항:

1. `SelectionId`는 `unit_warrior_base` 같은 **병종 ID가 아니라 생성 인스턴스 수명별 고유 ID**를 사용한다. 풀링 객체 재사용 시 새 ID가 필요하다.
2. 외부 선택 담당자가 이전 구독을 해제하고 새 선택 스냅샷과 체력/제거 알림을 연결한다. 유닛을 실제로 클릭하는 선택 시스템은 이번 범위에 없다.
3. 모든 UI 호출은 Unity 메인 스레드에서 한다. 매 프레임 전체 스냅샷을 만들지 말고 변경 알림을 사용한다.
4. 같은 유닛의 이벤트가 역순으로 도착하는 경우의 버전/순서 검사는 외부 어댑터가 담당한다. 현재 ID 검사는 다른 인스턴스의 알림을 차단하는 계약이다.
5. 제거 시점에 `TryHideUnitInfo(id)`를 보낸다. 체력 0만으로 자동으로 정보창을 닫지 않는다.
6. 실제 선택 해제 연동은 `InfoPanelClosed`를 구독한다. 구독/해제 수명을 대칭으로 관리한다.

프리팹에는 EventSystem이 없다. 실제 씬의 InputSystemUIInputModule을 가진 EventSystem 하나를 재사용한다.
정보 패널은 오른쪽 영역을 사용하므로 건물 정보창과 동시에 겹쳐 열지 않도록 실제 선택 담당자가 전환해야 한다.
월드 유닛 선택, 실제 스탯·전직 연결, 시설 체력/생존 수 HUD는 별도 통합 작업이다.

검증 결과와 미검증 범위는 `UnitInfoUiValidation.md`를 참조한다.

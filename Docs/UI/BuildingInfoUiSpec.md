# 건물 선택·정보 UI

## 범위와 상태

2026-09-08 / `feature/ui-common-hud` / Unity 6000.3.23f1.

UI가 선택된 건물의 이름·종류·레벨·설명·생산 유닛·효과를 표시하는 단계다.
실제 월드 클릭 판정, 건물 선택 권한, 골드 차감, 건설·업그레이드·해체, 생산은 구현하지 않는다.
현재 브랜치에는 건물 담당자의 런타임/데이터 코드가 없으므로 아래 규격은 **UI 측 연결 제안**이며 팀 합의 완료가 아니다.

## 산출물

- `Assets/Scripts/UI/BuildingInfoData.cs`: 불변 표시 스냅샷. 건물 SO를 대체하지 않는다.
- `Assets/Scripts/UI/BuildingInfoPanel.cs`: 정보창 표시와 닫기 처리.
- `Assets/Prefabs/UI/MvpBuildingInfo.prefab`: uGUI/TMP 정보 패널, 긴 설명 스크롤, 선택 없음/아이콘 없음 표시.
- `Assets/Scenes/Test/MvpBuildingInfoTest.unity`: 기존 공통 HUD와 함께 보는 별도 테스트 씬.
- `Assets/Scripts/UI/Samples/MvpBuildingInfoSample.cs`: 4종 건물 임시 데이터 공급자. 실제 게임에 사용하지 않는다.
- `Assets/Scripts/UI/Editor/MvpBuildingUiBuilder.cs`: 누락 에셋만 생성. 기존 에셋/공용 씬을 덮어쓰지 않는다.
- `Assets/Scripts/UI/Editor/MvpBuildingUiValidation.cs`: 실제 프리팹 계약·참조·레이아웃 검사. Unity Test Runner 테스트는 아니다.

## 연결 계약 제안

| 항목 | 의미 |
| --- | --- |
| `SelectionId` | 선택 인스턴스 또는 슬롯 식별자. 같은 종류의 건물이 여러 개 있어도 구별해야 한다. |
| `DisplayName`, `CategoryLabel` | 담당 시스템이 전달하는 한국어 표시명. UI에서 병종/건물 enum을 새로 정의하지 않는다. |
| `Level` | 1 이상. 업그레이드 규칙은 외부에서 결정한다. |
| `Description` | 없으면 안내 문구 표시. 긴 내용은 세로 스크롤. |
| `ProductionSummary` | 생산 유닛/전직/생산량 등 담당자가 확정한 표시 문자열. 없으면 영역 숨김. |
| `EffectSummary` | 자원/지원 효과 표시 문자열. 없으면 영역 숨김. |
| `Icon` | 선택 사항. 없으면 기본 문자 표시. |

필수 ID·이름·종류가 비어 있거나 레벨이 1 미만이면 생성자가 예외를 발생시킨다. 잘못된 데이터를 임의의 정상값으로 보정하지 않는다.

| API/이벤트 | 호출 시점과 동작 |
| --- | --- |
| `ShowBuildingInfo(BuildingInfoData)` | 외부 선택 변경/선택 건물 갱신 시 호출. 매 프레임 호출할 필요 없음. null은 거부한다. |
| `HideBuildingInfo()` | 선택 해제·대상 파괴·판 초기화·씬 전환 시 호출. 이전 텍스트/아이콘을 비우고 선택 없음 표시. |
| `SelectionId`, `HasSelection` | 현재 UI에 표시 중인 선택 상태 조회. 게임의 권위 있는 선택 상태는 아니다. |
| `InfoPanelClosed(string)` | 사용자가 닫기 버튼으로 **실제로 정보창을 닫은 뒤** 이전 선택 ID를 전달한다. 외부 `HideBuildingInfo`는 이 이벤트를 발생시키지 않는다. |

- 같은 ID 갱신 시 스크롤 위치를 유지하고 다른 ID를 선택하면 처음으로 돌아간다.
- 창 비활성화/재활성화는 표시 스냅샷을 유지한다. 새 판에는 명시적으로 `HideBuildingInfo`를 호출한다.
- 닫기는 정보창만 닫는다. 월드 선택 해제까지 해야 한다면 건물 선택 담당 어댑터가 이벤트를 구독해 처리한다.
- 비동기 데이터 조회는 어댑터에서 최신 선택 ID를 검사하고 이전 선택의 늦은 응답을 버린다.
- 생산/자원/지원 건물 전환 시 이전 건물의 유닛·효과·아이콘이 남지 않아야 한다.
- 숫자/밸런스/피해·재화 계산은 UI가 수행하지 않는다. 실제 상태 변경은 외부 담당자가 처리하고 새 표시 스냅샷을 보낸다.

```csharp
// 건물 담당의 선택 알림을 받는 연결 코드 예시. 팀 API 명칭은 아직 확정되지 않았다.
buildingInfoPanel.ShowBuildingInfo(new Game.UI.BuildingInfoData(
    selectionId: "selected-instance-id",
    displayName: "전사 훈련소",
    categoryLabel: "유닛 생산 건물",
    level: 1,
    productionSummary: "전사 · 전열 근거리 병종"));
// 선택 해제나 대상 제거 알림을 받으면:
buildingInfoPanel.HideBuildingInfo();
```

## 확인 순서

1. `Game > UI > Create Missing Building Info Assets`로 누락 에셋을 생성한다. 이미 있으면 보존한다.
2. `Assets/Scenes/Test/MvpBuildingInfoTest.unity`를 열고 Play.
3. 초기 선택 없음 표시를 확인하고 자원/전사/궁사/지원 버튼을 차례로 누른다.
4. `레벨 표시 갱신`은 전사 샘플의 레벨 1/2와 전직 표시만 바꾼다. 실제 업그레이드나 비용 차감이 아니다.
5. `선택 해제`와 정보창 `닫기`를 반복한다. 닫은 뒤 다른 건물 선택이 가능해야 한다.
6. Play 종료 후 `Game > UI > Validate Building Info UI` 실행. 결과는 Console과 `Logs/BuildingUiValidation`에서 확인한다.

테스트 씬의 건물 명칭과 100 골드는 임시 데이터다. 웨이브 시작은 코어 미연동으로 비활성화한다.
프리팹은 EventSystem을 포함하지 않는다. 실제 씬의 InputSystemUIInputModule을 가진 EventSystem 하나를 재사용한다.
공통 HUD의 결과 모달보다 뒤에 표시하도록 Canvas sorting order는 5다(HUD는 10).
기본 1920×1080, 1280×720, 1024×768을 검사 대상으로 한다. 모바일 안전 영역·게임패드 검증은 범위 밖이다.

## 다음 합의와 구현 순서

1. 김도현: 인스턴스/슬롯 식별자, 선택/해제/제거 알림, 표시 데이터 공급 방식 확정.
2. 건물·재화 담당: 건설/업그레이드/해체 가능 여부, 비용·환급액, 실제 실행/실패 결과 계약 확정.
3. 버튼·비용·실패 사유·전투 잠금은 UI 샘플 단계 구현/검증 완료(`BuildingActionUiSpec.md`). 위 계약 확정 후 실제 담당 시스템에 연결.
4. 코어·재화 브랜치 통합 후 3웨이브 MVP 실제 흐름 검증.

건설 조작과 공간 해금 UI는 MVP 필수이지만 이번 표시 패널만으로 완료 처리하지 않는다.

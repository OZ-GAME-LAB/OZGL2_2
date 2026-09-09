# 건설·업그레이드·해체 UI

2026-09-08 / 담당: 이진우 / `feature/ui-common-hud`

## 구현 범위

UI 전용 컴포넌트 `Game.UI.BuildingActionPanel`과 프리팹, 독립 테스트 씬을 추가했다.
Unity 기능 구현 스킬의 책임 분리 기준에 따라 실제 건물 생성/해체, 비용 차감, 환급 계산, 웨이브 상태 소유권은 UI에 넣지 않았다.
아래 계약은 **팀 합의 전 UI 측 연결 제안**이다. 김도현(건물), 홍재희(재화), 문규성(진행)의 실제 API를 대체하지 않는다.

- 비용/환급 골드, 실행 불가 사유 표시.
- 건설 단계 여부와 담당 시스템 연결 여부에 따른 입력 잠금.
- 요청 접수 직후 중복 클릭 잠금; 성공/실패 결과 수신.
- 실패 시 선택 유지 및 재시도. 성공 시 최신 견적 수신 전 재실행 방지.
- 선택 변경/해제/컴포넌트 비활성화 중에도 미완료 요청 잠금 유지.
- 이전 선택에 대한 늦은 응답은 현재 선택의 문구/정보를 덮어쓰지 않음.

## 파일 및 실행

- `Assets/Scripts/UI/BuildingActionViewData.cs`: 표시용 견적, 선택 스냅샷, 요청 ID.
- `Assets/Scripts/UI/BuildingActionPanel.cs`: 요청/결과 표시 컴포넌트.
- `Assets/Prefabs/UI/MvpBuildingActions.prefab`: 기존 HUD와 같은 uGUI/TMP 스타일.
- `Assets/Scenes/Test/MvpBuildingActionTest.unity`: 공통 HUD + 건물 정보창 + 행동 패널.
- `Assets/Scripts/UI/Samples/MvpBuildingActionSample.cs`: 테스트 응답 담당. 실제 게임에 배치하지 않음.
- `Assets/Scripts/UI/Editor/MvpBuildingActionBuilder.cs`: 누락된 새 에셋만 생성. 기존 파일/씬/Build Settings 덮어쓰기 없음.
- `Assets/Scripts/UI/Editor/MvpBuildingActionValidation.cs`: 자체 검사 + 기존 정보창/HUD 회귀 검사.

Unity에서 테스트 씬을 열고 Play한다. 아래 `공간 A`, `건물 B`, `조건 불가`, `전투 전환`은 테스트용 선택/상태다.
행동 버튼 클릭 후 아래 `성공 응답` 또는 `실패 응답`을 눌러 비동기 응답을 수동 재현한다.
**30/50/21 골드는 UI 검사용 임시 견적**이다. 70% 환급 규칙이나 게임 밸런스를 확정한 것이 아니다.
HUD 골드는 항상 100이며 조건 불가 샘플은 일부러 별도의 실패 사유만 표시한다. 재화 계산 테스트가 아니다.
Play 종료 후 샘플 변경은 저장되지 않는다.

## 연결 계약

| API | 담당/의미 |
| --- | --- |
| `ShowActions(BuildingActionViewData data)` | 선택/재화/업그레이드 변경 시 외부에서 최신 견적 전달. `TargetId`는 슬롯 또는 건물 인스턴스 ID. |
| `HideActions()` | 선택 표시 해제. 실행 중 요청의 취소를 의미하지 않음. |
| `SetActionsAllowed(bool allowed, string reason = null)` | 코어의 준비/전투 상태 반영. 기본값은 잠금. |
| `ActionRequested` | `BuildingActionRequest` 전달. **한 개의 실행 담당 어댑터만 구독**. 비용을 실행 권한으로 신뢰하지 말 것. |
| `TryResolveRequest(Guid requestId, bool succeeded, string message = null)` | 현재 대기 요청과 일치할 때만 결과 접수. 미일치/중복 응답은 false. |

`BuildingActionOffer`는 외부가 계산한 `GoldAmount`, `CanExecute`, `DisabledReason`을 담는다.
사용 불가에는 사유가 필수다. 해당 행동 자체가 없으면 해당 offer를 null로 전달한다.
Build에는 건설할 종류를 식별하는 `OptionId`가 필수다. UI는 한 번에 선택된 건설 옵션 하나를 표시한다.
다수 건물의 카탈로그/선택 목록, 해체 확인 팝업, 전직 선택 화면은 이번 범위에 포함되지 않는다.

권장 연결 순서:

1. 어댑터가 `ActionRequested`를 구독하고 현재 선택 스냅샷/코어 상태를 전달한다.
2. 요청 수신자는 요청의 인스턴스 ID와 종류, 최신 골드/비용, 건설 공간, 업그레이드 단계, 게임 상태를 **다시 검사**한다.
3. 실제 처리는 건물/재화 시스템에서 원자적으로 수행한다. 실패 시 부분 차감/부분 건설을 남기지 않는다.
4. Unity 메인 스레드에서 `TryResolveRequest(request.RequestId, succeeded, reason)`를 호출한다.
5. 성공 시 **결과 접수 이후** 최신 `ShowActions` 및 기존 `BuildingInfoPanel.ShowBuildingInfo`를 갱신한다. 더 이상 선택 대상이 없으면 Hide한다.
6. 실패라도 견적이 바뀌었다면 최신 견적을 보낸다. ShowActions는 이전 결과 문구를 지우므로 실패 사유가 계속 필요하면 offer의 DisabledReason에도 전달한다.

동기 처리도 같은 순서로 응답할 수 있다. `Request` 이름은 UI가 실제 완료를 소유하지 않기 때문에 사용한다.
요청이 대기하는 동안 다른 선택의 행동도 잠근다. 이는 하나의 UI 패널에서 동시 실행을 만들지 않기 위한 의도다.
UI를 숨기거나 비활성화해도 요청은 취소되지 않는다. 어댑터가 완료/실패/실제 취소를 확인해 응답해야 한다.
자동 타임아웃으로 잠금을 푸는 코드는 없다. 외부 작업이 진행 중일 수 있기 때문이다.
새 플레이/씬 전환 시에는 어댑터가 이전 실행을 종료하고 UI 수명을 함께 정리한다. 정적 이벤트/싱글턴은 추가하지 않았다.

## 다음 팀 통합 작업

- 공통 슬롯/건물 인스턴스 ID 및 선택 해제 이벤트 합의.
- 건설 종류 선택 목록과 실제 생산 건물 정의 연결.
- 업그레이드 비용/최대 단계/전직 결과와 해체 환급 정책 확정.
- 실제 골드/진행 상태 변경 시 UI 스냅샷 갱신.
- 실패 코드 → 사용자 문구 변환, 긴 문구/다국어 정책.
- 실제 해체 확인 UX 및 진행 중 작업의 취소/씬 전환 정책.

검증 범위와 한계는 `BuildingActionUiValidation.md`를 참조한다.

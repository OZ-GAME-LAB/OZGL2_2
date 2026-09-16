# 건설 공간 월드 클릭 UI — 2026-09-15

## 작업 범위

- 프로젝트: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-building-integration-20260915`
- 이전 건설 연동을 유지하고 월드 입력 경로만 추가했다. 팀원의 건물/재화/Core/유닛 코드는 수정하지 않았다.
- 기존 `PlayerUI`, `PlayerBuildingIntegration`, 팀원 씬/프리팹, 공유 폰트, Packages, ProjectSettings를 저장하거나 덮어쓰지 않았다.
- 커밋/푸시/PR/머지는 하지 않았다. 향후 PR 대상은 dev다.

## 새로 확인할 씬

`Assets/Scenes/UI/PlayerBuildingWorldInput.unity`

1. 현재 작업 씬은 사용자가 직접 보존한 뒤 새 씬을 열고 Play한다.
2. 가운데 `+ 건설` 칸을 클릭하면 실제 BuildingSlot의 후보 카탈로그가 열린다.
3. 골드 생산소 → 건설을 선택하면 원본 BuildingBuildController가 실제 건물을 생성하고 골드 30을 차감한다.
4. 팝업을 닫으면 점유된 칸의 이름/색이 바뀌어 있다. 다시 클릭하면 건물 정보와 해체 버튼이 표시된다.
5. 해체 시 원본 컨트롤러의 환급률에 따라 환급하고, 칸 표시가 `+ 건설`로 복구된다.
6. 우클릭/드래그는 선택하지 않는다. 팝업 배경 클릭은 팝업만 닫고 그 뒤 건설 칸까지 클릭하지 않는다.

이것은 **월드 입력 전용 검증 씬**이다. 버튼처럼 보이는 네 칸은 Canvas 버튼이 아니라 SpriteRenderer + Collider2D + BuildingSlot이다. 가격과 단순 시각 자료는 기존 UI 검증 데이터를 재사용하며 정식 밸런스/맵 디자인이 아니다. 혼동을 피하려고 이 씬에서만 시연용 병력 수와 웨이브 시작 버튼을 숨겼다. 내부 Core/재화 초기화는 기존 UI 샘플을 재사용한다. 실제 팀 전투 맵이나 병영→유닛 생산→AI 전투를 통합한 것은 아니다.

## 코드와 책임

- `RuntimeBuildingSelectionTarget`: EventSystem의 PointerDown/Click/Drag를 받아 기존 `RuntimeBuildingUiBinding.SelectSlot`으로 전달한다. 재화, 건설, 해금, 승패를 직접 처리하지 않는다.
- `RuntimeBuildingWorldSlotView`: 실제 슬롯 점유 상태의 이름과 색만 표시한다. Unity에서 파괴된 건물 참조도 빈 칸으로 갱신한다.
- `Editor/RuntimeBuildingWorldUiSetup`: 기존 UI 씬을 새 경로로 복사한 후 그 복사본에만 입력과 표식을 연결한다. 기존 목적지 파일이 있으면 보존하고, 저장되지 않은 씬이 있거나 Play 중이면 중단한다.
- `Editor/RuntimeBuildingWorldUiValidation`: 별도 `UnityUIValidation` 프로젝트의 batch Editor에서만 실행되는 Play Mode 검사다.

## 팀 맵에 연결할 때

1. 동일한 EventSystem/InputSystemUIInputModule을 사용하고 월드 카메라에 Physics2DRaycaster를 연결한다. 새 입력 시스템이나 패키지는 필요 없다.
2. 클릭 Collider2D와 같은 오브젝트 또는 부모의 클릭 경로에 `RuntimeBuildingSelectionTarget`을 배치하고 `_slot`, `_binding`, `_flow`를 명시적으로 지정한다. `_slot`에는 활성 Collider2D가 있어야 한다. 런타임 구성은 `Initialize(binding, slot, flow)`를 사용한다.
3. `_binding`은 기존 건설 카탈로그/정보/행동 패널 및 같은 건물 컨트롤러/재화/Core에 연결한다.
4. 원본 BuildingBuildController의 임시 월드 클릭 메뉴와 입력을 동시에 처리하지 않도록 협의한다. 검증 씬은 기존 UI 연동 씬의 `slotMask = 0`을 유지한다. 팀 공용 씬 설정은 임의 변경하지 않는다.
5. WorldSlotView는 선택 사항이다. 원본 BuildingSlot이 자동으로 숨기는 빈 칸 SpriteRenderer와 UI 상태 표식을 공유하지 않는다. 검증 씬은 UI 표식을 별도 자식에 둔다.

## 검증 근거

- Unity 6000.3.23f1, 격리 복사본 `D:/UnityUIValidation/OZGL2_2-player-ui-20260914` 사용.
- 새 월드 입력/실제 건설/표시 검사 **76개 assertion 통과**. 런타임 오류 0, 해당 최종 실행의 Editor Search 시작 예외 0.
- 기존 건설/두 재화/환급/표시 검사 112개와 실제 유닛 정보 검사 83개도 새로 실행해 통과했다. 해당 회귀 실행의 UI/게임 오류 및 Editor Search 시작 예외는 0이다.
- Physics2DRaycaster/GraphicRaycaster가 실제 반환한 대상으로 ExecuteEvents를 전달했다. OS 마우스/키보드 조작 검사가 아니다.
- 확인 항목: 정확한 슬롯 선택, 카드와 건설/해체 버튼 클릭, 실제 차감·환급, 팝업 뒤 입력 차단, 우클릭/드래그/다른 포인터, 비활성 슬롯/Collider/UI, 누르는 동안 점유 상태 변경, Core 단계 변경과 전투 입력 차단, 재초기화/재활성화, timeScale 0 입력.
- 1280×720 실제 렌더 3장 확인: 빈 칸, 카탈로그, 실제 건설 후 골드/이름/색. 보이는 텍스트 넘침 검사를 포함한다.
- 첫 시도는 캡처 후 Canvas 복원 및 팝업 표시 직후 다음 입력을 같은 호출 흐름에서 보내 raycast 검사가 실패했다. 렌더 깊이가 갱신되는 프레임을 기다린 뒤 실제 사용자 입력 간격에 맞춰 재검증했다. 검사 항목은 제거하지 않았다.
- 기존 Core CS1998 경고는 수정하지 않았다. 플랫폼 Player 빌드, 물리 기기 입력, 팀 맵 전체 플레이는 미검증이다.
- 원시 로그/기준 해시: `Logs/UiBuildingWorldInput20260915/Resumed/`. 첫 생성 씬은 그 아래 `FirstGeneratedScene/`에 보관했다.
- 실행 로그: `world-input-second.log`(최종 월드 입력), `building-regression.log`(기존 건설/유닛 정보). 요약 및 화면은 `FinalResults/`에 있다.
- 작업 시작 시 존재한 852개 파일의 내용이 동일함을 SHA-256으로 확인했다. 새 코드 4개, 그 메타 4개, 새 씬/메타 2개와 이 문서만 추가했다. 검증한 코드와 전달한 코드의 내용도 동일하다.
- Assets 전체 메타 GUID 중복 0, 새 씬 참조 GUID 누락 0(패키지 메타 포함), Git diff 공백 검사 통과. 별도 검증 Editor는 종료했고 사용자 Editor는 그대로 두었다.

## 남은 통합

- 팀 기준 씬(Test_Building / Test_Building02 등) 확정과 공용 입력 소유권 협의.
- 업그레이드/건설 공간 해금 API 연결.
- 최종 결과 이벤트 및 정산 데이터에 따른 결과 UI 자동 표시.
- 실제 유닛 생산·전투 맵과 연결하고 전체 플레이 검증.

새 월드 클릭 기능은 위 누락된 게임 API를 대신 구현하지 않는다.

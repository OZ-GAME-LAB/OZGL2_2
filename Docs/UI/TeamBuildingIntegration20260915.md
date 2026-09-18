# 팀원 건설 씬과 플레이어 UI 연동 — 2026-09-15

## 작업 위치와 범위

- 프로젝트: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-team-building-scene-20260915`
- 기존 HEAD: `aed54244b180588fe72b8bf0813f29c984350c92`. 이전 미커밋 작업을 그대로 보존했다.
- 팀 원본 기준: `dev`의 `f948eac46d10377d616861dc765d782676553877`, [PR #33](https://github.com/OZ-GAME-LAB/OZGL2_2/pull/33).
- 팀원의 `Test_Building`을 새 GUID의 별도 씬으로 복사했다. 원본 팀 씬·코드·프리팹·데이터는 수정하지 않았다.
- 기존 프로젝트에 없던 참조 에셋 120개(메타 포함)만 dev 원본에서 추가했다. 참조 그래프 307개 중 나머지는 기존 파일을 사용했다. `Test_Building02`나 전체 dev를 일괄 병합하지 않았다.
- 신규 UI 코드 3개/메타 3개, 신규 씬/메타 2개 및 문서 2개를 추가했다. 작업 전 존재한 파일 863개의 내용이 모두 동일함을 해시로 확인했다. 팀 원본 120개와 검증된 UI 파일 8개도 전달 후 해시 불일치 0개다.
- 커밋·푸시·PR·머지는 하지 않았다. 이후 PR의 대상은 `dev`다.

## 확인 방법

1. Unity에서 작업 중인 씬의 변경을 먼저 직접 보존한다. 사용자 창이나 열린 씬을 자동으로 저장/변경하지 않았다.
2. `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity`를 열고 Play한다.
3. 팀 원본의 건설 공간 7개 중 하나를 클릭하거나 오른쪽 아래 `건설` 버튼을 누른다.
4. 건물 후보를 고르면 정보/비용/건설 버튼이 표시된다. 건설하면 원본 건물 컨트롤러가 생성·차감하고 HUD가 갱신된다.
5. 건설된 건물을 다시 누르면 정보와 해체 버튼이 표시된다. 해체 후 실제 환급과 빈 공간 복원을 확인한다.

별도 재화 테스트 패널의 Run 시작 버튼은 필요하지 않다. 새 씬 전용 UI 시작 어댑터가 팀 RunCurrencyManager의 공개 초기화 메서드를 한 번 호출한다. Core 초기화와 진행은 팀 BootStrap에 남겨 두었다.

### 현재 팀 테스트 데이터

| 원본 이름 | 종류 | 건설 가격 | 해체 환급 |
|---|---|---:|---:|
| melee | 유닛 생산 | 10 골드 | 7 골드 |
| ranged | 유닛 생산 | 8 골드 | 5 골드 |
| producer | 자원 생산 | 20 골드 | 14 골드 |

- 원본 씬의 환급률 70% 및 소수점 버림을 표시한다. 가격/환급률을 UI에서 정하지 않는다.
- 이름, 설명, 아이콘, 월드 그림, 생산 설명은 팀 데이터에서 읽는다. 영어 테스트 이름과 임시 그림도 임의 변경하지 않았다.
- 생산 건물 2종의 설명은 웨이브 시작 시 3명, 자원 건물의 설명은 웨이브 종료 후 10골드다. 이번 검증은 건설·정보·재화·입력에 한정하며 실제 전투/유닛 생산 실행을 완료했다고 의미하지 않는다.

## 코드 책임

- `TeamBuildingUiStartup`: 기존 GameUIController/CoreHudBinding/RunGoldHudBinding과 팀 RunCurrencyManager 연결. 보석 표시와 간단한 안내 문구 갱신. 판 종료/보상/재시작을 직접 처리하지 않는다.
- `Editor/TeamBuildingUiSetup`: 팀 씬의 새 복사본에 기존 플레이어 Canvas 3개와 RuntimeBuildingUiBinding/RuntimeBuildingSelectionTarget을 연결한다. 기존 목적지 씬은 덮어쓰지 않고, 저장되지 않은 씬이 있으면 중단한다.
- `Editor/TeamBuildingUiValidation`: 별도 UnityUIValidation 프로젝트에서만 실행되는 참조/Play Mode/화면 검증이다. 원본 에셋을 저장하지 않는다.

새 씬에서만 적용한 설정:

- 팀 테스트 Canvas와 재화 OnGUI 패널 숨김. Core의 TestWaitingScript 등 진행 객체는 보존.
- 기존 카메라에 MainCamera 태그/Physics2DRaycaster 설정. 슬롯 위치와 그림은 유지.
- 원본 컨트롤러의 `slotMask = 0`으로 임시 건설 메뉴의 중복 클릭 경로 차단.
- 기존 UI의 가짜 병력 수, 테스트 전투 시작 버튼, 미연동 분기 선택 UI를 숨김.
- 건물 레벨 API가 없으므로 고정 `레벨 1` 표시 및 업그레이드 항목을 숨김.
- 상하 HUD를 화면 가장자리에 고정하고 보석 표시 추가. 원본 UI 템플릿은 저장하지 않음.

## 검증 결과

Unity 6000.3.23f1, `D:/UnityUIValidation/OZGL2_2-player-ui-20260914`에서 실행했다. 사용자 Editor는 켜진 상태로 유지했다.

- 새 팀 씬 연동: 152개 assertion 통과. 게임 런타임 오류 0, 해당 실행의 Editor Search 시작 예외 0.
- 기존 건설 UI: 112개 assertion 통과.
- 실제 유닛 정보 회귀: 83개 검사 통과.
- 기존 월드 클릭 UI: 76개 assertion 통과. 게임 런타임 오류 0.
- 모든 팀 슬롯의 실제 Physics2DRaycaster hit → EventSystem → 카탈로그 → 버튼 → 원본 TryBuild/TryDemolish 경로를 검사했다. OS 마우스 조작 검사는 아니다.
- 원본 건물 3종의 데이터 identity, 정확한 차감/환급, 잔액 HUD, 재화 부족, 재활성화 중 중복 초기화 방지, 보석 이벤트, 팝업 뒤 클릭 차단, 실제 Core 단계에 따른 건설 잠금을 검사했다.
- 새 씬의 missing script, missing reference, 다른 씬 객체 참조를 검사했다.
- 전달된 프로젝트의 메타 GUID 중복 0개, 새 씬의 외부 GUID 참조 61개 중 누락 0개, diff 공백 검사 통과. 기존 ProjectSettings/Packages/씬/공유 폰트의 내용 변화는 없다.
- 1280×720, 1280×800, 1920×1080 렌더와 텍스트 넘침을 검사하고 실제 캡처를 확인했다.
- 첫 컴파일 시 새 검사 코드에서 런타임 internal 문구 유틸리티를 직접 참조한 접근 오류가 있었다. 검사에서 사용자 표시 결과를 비교하도록 수정했고 최종 컴파일/실행은 통과했다. 팀 코드는 수정하지 않았다.
- 기존 Core의 CS1998 경고와 Unity 라이선스 갱신 로그는 별도다. 라이선스 로그가 있었지만 Editor/Play Mode 검증은 정상 완료됐다.

근거: `Logs/UiTeamBuilding20260915/`. `team-ui-final.log`, `building-regression.log`, `world-input-regression.log` 및 `FinalResults/`를 확인한다. 첫 실패 로그와 첫 생성 씬도 보관했다. 로그/이미지는 Git 제외 대상이다.

## 한계와 다음 작업

- 이 씬은 **팀 테스트 데이터 기반 건설 UI 통합 씬**이며 완성 전투 맵/출시 빌드가 아니다.
- Core는 아직 TestSpawner를 사용한다. 실제 적 생성·자동 전투·승패·보상까지의 전체 플레이는 미검증이며 플레이어 웨이브 시작 버튼도 숨겼다.
- 업그레이드·건설 공간 해금·최종 결과 및 정산의 공식 API 연결은 대기 중이다. UI에서 그 로직을 대신 만들지 않았다.
- 기존 원본 TryBuild의 일부 실패 경로에 대한 차감 롤백 계약은 건물 담당자와 협의가 필요하다. 이번에 원본 거래 코드를 수정하지 않았다.
- 씬의 임시 아트/테스트 이름은 유지했다. 최종 디자인/밸런스 완료로 보고하지 않는다.
- Player 빌드, 물리 키보드·마우스, 팀 전투 전체 진행은 검증하지 않았다. Build Settings/Packages/ProjectSettings는 수정하지 않았다.
- 다음 우선순위는 팀 실제 스포너/전투 흐름 확정 후 유닛 선택·생존 수·최종 결과 UI를 해당 이벤트에 연결하는 것이다.

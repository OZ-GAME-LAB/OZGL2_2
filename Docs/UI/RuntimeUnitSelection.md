# 실제 유닛 클릭 선택 UI

## 위치와 범위

- 프로젝트: `D:\Documents\GitHub\OZGL2_2`
- 브랜치: `feature/ui-runtime-integration` (이전 UI 연동 작업을 같은 브랜치에서 이어감)
- Unity: 6000.3.23f1 / 기존 uGUI + InputSystemUIInputModule
- 별도 worktree, 새 입력 패키지/액션맵, 공용 전투 씬 변경 없음.
- 이번 전용 씬: `Assets/Scenes/Test/MvpRuntimeUnitSelectionTest.unity`
- UI 전용 테스트 데이터: `Assets/Data/UI/Tests/RuntimeSelectionAlly.asset`, `RuntimeSelectionEnemy.asset` (임시 수치, 팀 밸런스 데이터 아님)

## 실행 방법

1. 현재 씬을 저장하고 Edit Mode에서 `Game > UI > Open Runtime Unit Selection Scene`을 선택한다.
2. Play를 누른다.
3. 왼쪽의 아군/적군 원형 표시를 왼쪽 클릭하면 실제 유닛 정보가 열린다.
4. 빈 공간을 왼쪽 클릭하면 선택이 해제된다. 오른쪽 클릭과 드래그는 선택하지 않는다.
5. 선택 후 `피해 -25`, `회복 +20`, `보호막 +20`으로 실제 Unit_Life와 정보창을 확인한다.
6. `유닛 재사용`으로 초기화한 뒤 다시 선택한다. 이전 수명의 선택/누름 상태는 유지하지 않는다.

이 테스트는 유닛 자동 이동·공격 AI가 아니다. 버튼이 테스트용으로 실제 Life 메서드를 호출한다. 정식 전투 씬은 그대로 두며 빌드 씬 목록에도 이 씬을 추가하지 않는다.

## 연결 구조

- 기존 `RuntimeUnitInfoSource`와 `RuntimeUnitInfoBinding`을 그대로 사용한다.
- `RuntimeUnitSelectionTarget`이 EventSystem의 포인터 이벤트를 바인딩으로 전달한다. Mouse.current 폴링, 새 싱글턴, 전역 선택 상태를 추가하지 않는다.
- 같은 GameObject에 2D Collider와 SpriteRenderer를 두고, 사용하는 카메라에 Physics2DRaycaster를 설치한다.
- 유닛 클릭 대상에는 바인딩과 해당 소스를 지정한다. 생성 순서에 따라 `Initialize(binding, source)`로 연결할 수도 있다.
- 명시적인 빈 공간 Collider에만 `InitializeClearSurface(binding)`를 사용한다. 소스 누락을 빈 공간으로 추정하지 않는다.
- UI에는 기존 GraphicRaycaster와 raycastTarget이 켜진 패널/버튼을 유지한다. 화면을 덮는 투명 Graphic의 raycastTarget 설정은 통합 때 함께 확인한다.
- 눌렀을 때의 포인터 ID 및 유닛 수명을 검사한다. 비활성화/드래그/재사용을 거친 오래된 클릭으로 새 유닛을 선택하지 않는다.
- 이동과 해제가 같은 프레임에 전달되는 경우에도 마지막 좌표와 누른 좌표의 차이를 기존 EventSystem의 드래그 임계값으로 검사한다. 임계값 미만의 손떨림은 클릭으로 허용한다.
- 이 컴포넌트는 정보 조회용이다. 캐릭터 이동, 공격 명령, 게임 상태 변경을 발생시키지 않는다.

공용 씬 통합 시 담당자와 선택 Collider/카메라, 스폰 직후 연결 위치를 합의해야 한다. 기존 선택 시스템이 추후 들어오면 새 입력 처리를 중복 설치하지 말고 `RuntimeUnitInfoBinding.TrySelect` 호출만 재사용한다.

## 자동 검사

`Game > UI > Validate Runtime Unit Selection In Play Mode`가 전용 씬을 열어 실제 Physics2DRaycaster/GraphicRaycaster 결과와 포인터 핸들러 호출을 검사한다. 검사 후 원래 씬 구성으로 돌아온다. 이는 하드웨어 마우스 입력을 주입하는 검사가 아니므로 실제 마우스 스모크 테스트 결과와 구분해서 보고한다.

기존 검사는 다음 메뉴로 유지한다.

- `Validate Runtime HUD In Play Mode`: 실제 코어/재화 HUD 및 유닛 정보 바인딩 검사
- `Validate Existing UI Regression`: 기존 HUD/건물/유닛 정보창 자체 Editor 검사

## 검증 결과 (2026-09-09)

Unity 6000.3.23f1의 현재 D 드라이브 프로젝트에서 실행했다. 아래 숫자는 사용자 정의 검증문의 개수이며 Unity Test Runner/NUnit 테스트 케이스 수가 아니다.

| 검사 | 결과 |
| --- | --- |
| 실제 2D/UI 레이캐스트 + 선택 입력 Play 검사 | 124개 통과 |
| 기존 실제 유닛 정보 바인딩 Play 검사 | 83개 통과 |
| 기존 코어·재화 HUD Play 검사 | 124개 통과 |
| 기존 HUD/건물 정보/건물 동작/유닛 정보 Editor 회귀 검사 | 518개 통과 (98 + 113 + 150 + 157) |

총 849개 검증문 통과. 최종 선택 검사 후 Console 경고 0개, 오류 0개를 화면에서 확인했다. `git diff --check`도 통과했다.

- 1920×1080 Game View에서 실제 마우스 입력으로 아군/적군 정보 표시, 피해 100→75, 보호막 20, 오른쪽 클릭 무시, 닫기, 빈 공간 해제, 유닛 재사용 후 재선택을 확인했다.
- UI 위 유닛 클릭 관통 방지, 풀링 수명 변경, 비활성화, 중복 선택, 사망/디스폰은 Play 검사에서 확인했다.
- 초기 닫기 검사 실패는 활성화된 Graphic이 아직 렌더링되기 전(`depth=-1`) 같은 프레임에 클릭한 검사 순서 문제였다. 한 프레임을 기다린 뒤 실제 레이캐스트를 검사하도록 수정했고 이후 통과했다. 공용 정보창 프리팹은 변경하지 않았다.
- 드래그 자동 조작에서는 Unity가 누름/해제를 같은 좌표 `(890.60, 646.02)`로 받았다. 따라서 이 도구로 실제 드래그 스모크 테스트는 검증할 수 없었다. 드래그 콜백 취소, 해제 시 이동 거리, 임계값 미만 클릭은 Play 검사에서 통과했으나 사람의 물리 마우스로 한 번 더 확인해야 한다. 임시 좌표 로그는 코드에서 제거했다.
- 공용 전투 씬/실제 스포너 연결, 전투 AI, Player 빌드, 터치·게임패드 입력은 이번 검증 범위 밖이다.
- 로그: `Logs/RuntimeUnitSelectionValidation/editor-validation-20260909-150319.log` (초기 검사 실패와 해결 후 PASS 기록을 함께 보존).

작업 후 전용 씬을 Edit Mode로 열어두고 기존 Unity 창 배치를 복원했다. 커밋·푸시·PR·머지는 하지 않았다.

## 보존 주의

기존 GitHub Desktop stash는 적용하거나 삭제하지 않는다. Unity가 새 한글 문구를 표시하면서 `Assets/Art/Fonts/NotoSansKR/NotoSansKR SDF.asset`에 글리프/아틀라스를 자동 저장할 수 있다. 이번 실행에서 이 자동 변경을 확인했으며, 임의 복원·stash 덮어쓰기는 하지 않는다. 커밋 때 폰트 변경은 UI 코드와 구분해서 검토해야 한다.

# 등록 유닛 생존 수 HUD

## 작업 기준

- 경로: `D:/Documents/GitHub/OZGL2_2`
- 브랜치: `feature/ui-runtime-integration`, 기준 HEAD `e777134`
- Unity: 6000.3.23f1, uGUI/TMP, 기존 입력 모듈 사용
- 2026-09-09 원격 조회: dev `d6cc18d`, core `dbc33b0`. 코어에는 `TestSpawner`만 있고 건물 브랜치에는 아직 구현 코드가 없다. 정식 스포너/시설/최종 결과 API는 연결 대기 상태다.

이번에는 MVP의 **아군·적군 생존 수 표시**를 구현한다. 실제 유닛 공개 상태와 이벤트를 읽지만, 등록되지 않은 유닛까지 전장 전체 수로 추측하지 않는다. 승패·웨이브 종료·보상·스폰·유닛 초기화는 UI의 책임이 아니다.

## 실행

`Game > UI > Open Runtime Unit Count Scene` → Play.

전용 씬은 `Assets/Scenes/Test/MvpRuntimeUnitCountTest.unity`다. 기존 클릭 테스트 씬을 별도 파일로 복사해 생존 수 HUD만 추가한다. 원본 씬, 공용 UI 프리팹, 팀원 코드, Build Settings는 변경하지 않는다.

1. 초기 아군/적군 생존 수가 각각 1인지 확인한다.
2. 아군 또는 적군 원을 클릭하고 `피해 -25`를 눌러 실제 체력을 줄인다.
3. 체력이 남아 있으면 생존 수가 유지되고, 0이 되면 해당 진영 수만 1 줄어든다.
4. 사망 유닛을 다시 공격하거나 회복 요청해도 음수/중복 증가가 발생하지 않아야 한다.
5. `유닛 재사용` 후 두 수가 다시 1이 되고, 다시 클릭해 정보를 볼 수 있어야 한다.

숫자와 버튼은 UI 검증용이며 정식 밸런스나 전투 AI가 아니다.

## 스포너 담당자 연결 계약

`RuntimeUnitCountHud`를 전투 HUD 수명에 맞는 오브젝트에 붙이고 `_allyText`, `_enemyText`를 연결한다. 동적 구성 시 `Initialize(allyText, enemyText)`를 사용한다.

생성 또는 풀 재사용 순서:

```csharp
// 아래는 호출 순서 예시. 스폰/초기화의 소유권은 기존 담당 시스템에 있다.
unit.gameObject.SetActive(true);
unit.Initialize();
bool registered = countHud.TryRegister(infoSource);
string lifetimeId = infoSource.SelectionId;

// 지연된 제거 요청은 등록 당시 수명 ID로만 해제한다.
countHud.TryUnregister(infoSource, lifetimeId);
```

- `infoSource`는 기존 `RuntimeUnitInfoSource`이며 같은 GameObject의 `Unit_Core`, `Unit_Life`를 읽는다.
- `TryRegister`는 활성·초기화 완료 유닛만 받는다. 실패 시 게임 상태와 기존 등록은 바꾸지 않는다. 같은 수명을 여러 번 등록해도 한 번만 센다.
- HP가 0이거나 사망 상태면 생존 수에서 제외하지만, 활성 사체의 등록은 유지한다. 생존 수와 등록 수는 다르다.
- Source 또는 GameObject 비활성화는 즉시 등록을 해제한다. 다시 활성화/초기화한 수명은 다시 등록해야 한다.
- `TryUnregister(source, lifetimeId)`는 오래된 수명의 지연 요청으로 새 수명을 제거하지 않는다.
- HUD 비활성화 동안 이벤트를 해제한다. 재활성화 시 현재 상태를 다시 읽고, 파괴되거나 수명이 바뀐 등록은 제거한다. 새 스폰의 명시적 등록은 HUD가 숨겨져 있어도 가능하다.
- 현재 팀 코드는 진영 변경과 Core/Life 컴포넌트 단독 활성화 변경 이벤트를 제공하지 않는다. 그런 별도 변경을 적용하는 담당자는 `Refresh()`를 호출한다. GameObject/Source 전체 수명 변경은 자동 반영된다.
- 새 판이나 명단 교체 시 `ClearRegistrations()`를 호출한 뒤 새 유닛을 등록한다. 이 함수는 유닛을 죽이거나 선택을 해제하지 않는다.
- 이벤트 갱신은 변경된 유닛 1개의 기여분만 증감한다. 전장 검색, 프레임 폴링, 체력 변화마다 전 유닛 순회는 하지 않는다. 메인 스레드에서 호출한다.

## 검증

메뉴: `Game > UI > Validate Runtime Unit Counts In Play Mode`.

새 씬에서 기존 레이캐스트/포인터 핸들러 검사와 생존 수 검사를 함께 실행하고 원래 씬 구성으로 복원한다. 이는 프로젝트 자체 검증문이며 Unity Test Runner/NUnit 케이스 수와 구분한다.

2026-09-09 Unity 6000.3.23f1 Editor 검증 결과:

| 실행 대상 | 결과 |
| --- | --- |
| 새 생존 수 씬의 선택·레이캐스트·참조·글리프 검사 | 135개 PASS |
| 등록·사망·중복 알림·풀 재사용·비활성 HUD·컴포넌트 재동기화 | 58개 PASS |
| 원본 선택 전용 씬 (선택적 count HUD 연결 없음) | 124개 PASS |
| 기존 실제 코어/재화 HUD Play 검사 | 124개 PASS |
| 기존 실제 유닛 정보 Play 검사 | 83개 PASS |
| 기존 Editor UI 회귀 검사 | HUD 98 + 건물 정보 113 + 건물 액션 150 + 유닛 정보 157개 PASS |

- 최초 검사에서 `Enemy Count`의 세로 텍스트 영역 부족이 발견됐다. 새 씬과 생성기의 count 카드 높이를 150, 숫자 라벨 높이를 64로 늘린 후 글리프/overflow 검사에 통과했다. 원본 선택 씬과 공유 프리팹은 수정하지 않았다.
- 네이티브 마우스 입력으로 1920×1080에서 아군 선택 → 피해 25 → 생존 수 유지 → 체력 0/생존 수 0 → 중복 피해·회복 후 0 유지 → 재사용 후 양쪽 1 복귀를 확인했다.
- 1366×768에서도 배치와 한글 표시를 확인하고 적 선택 → 체력 0/적군 수 0(아군 1 유지) → 재사용 후 1/1 복귀를 확인했다. 이후 1920×1080으로 복원했다.
- 위 자동 포인터 검사는 핸들러 직접 호출이며 하드웨어 입력 검사와 구분한다. 드래그·터치·게임패드 및 Player 빌드는 이번에 검증하지 않았다.
- 최종 Console: 오류 0, 경고 0. 테스트 사망에 대한 일반 로그 2개만 남았다. Play를 종료하고 새 count 씬을 Edit Mode로 열어 두었다.
- 증거 로그: `Logs/RuntimeUnitCountValidation/editor-validation-20260909-154339.log`. 최초 실패와 수정 후 PASS를 함께 보존한 로컬 Editor 로그이며 Git 제외 대상이다. Editor 로그에는 환경 정보가 포함될 수 있으므로 원문을 PR에 올리지 않는다.
- 새 Assets 파일의 `.meta` 존재와 `git diff --check`를 확인했다. 공유 글꼴은 Unity의 자동 글리프 추가로 변경되어 있으며 기존 변경분/stash와 함께 그대로 보존했다.

## 아직 연결하지 않은 항목

- 정식 스포너의 전체 생성/제거 흐름과 전장 전체 집계
- 관문/베이스캠프 내구도 API 및 HUD
- 확정 승패·보상 결과, 정식 재시작 진입점
- Player 빌드, 터치/게임패드 입력

기존 글꼴의 자동 글리프 변경과 GitHub Desktop stash는 임의 복원하거나 덮어쓰지 않는다. 커밋·푸시·PR·머지는 별도 요청 전까지 하지 않는다.

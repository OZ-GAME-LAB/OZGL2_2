# 최신 코어와 UI 분기 진행 연동

## 상태 — 2026-09-10

기존 UI 작업을 유지한 채 **UI 전용 테스트 씬에서 최신 코어 연동을 완료**했다. 정식 전장/MVP 전체 완성이나 GitHub 업로드를 의미하지 않는다.

- 작업 경로: `D:/Documents/GitHub/OZGL2_2`
- 작업 브랜치: `feature/ui-integration-validation`
- 브랜치 HEAD: `e777134` 유지. 작업은 미커밋 상태다.
- 가져온 원본: `origin/dev`의 `2c088a650831c909442af866089e6070b73ae9ed` (팀원이 PR #15로 반영한 코어)
- Unity: `6000.3.23f1`
- 통합 전 검증 기록: `Docs/UI/IntegrationValidation20260910.md` (1,042개 PASS, 최신 코어 적용 전 기록)

## 팀원 작업 보호

사용자 승인 후 `Assets/Scripts/Core`와 `Assets/Data/Waves`만 위 커밋의 원본으로 가져왔다. 코어의 메서드 본문·공개 API·기획 상수·웨이브 데이터는 직접 편집하지 않았다.

- 대상 65개 파일을 Git blob 기준으로 비교한 결과, 원본과 다른 파일은 **0개**다. 줄바꿈 설정은 Git의 정규화를 적용해 비교했다.
- Git에서 기존 코어 7개 파일이 `M`으로 보이는 것은 현재 HEAD보다 최신인 팀원 원본을 가져왔기 때문이다.
- 다른 팀원의 유닛·재화·건물 코드, 공용 프리팹, 팀원 테스트 씬, 패키지, Project Settings는 수정하지 않았다.
- 기존 유닛 정보·클릭 선택·생존 수 UI 코드와 씬은 보존했다. 기존 검사도 다시 통과했다.
- 공용 Noto Sans 폰트의 기존 미커밋 변경은 그대로 보존했다. 새 문구는 UI 테스트 전용 `CoreIntegrationTestFont.asset`을 사용해 공용 동적 폰트에 글리프를 추가하지 않는다.
- 기존 GitHub Desktop stash는 적용/삭제하지 않았다. 새 worktree, 커밋, 푸시, PR, 머지를 만들지 않았다.
- 전체 `dev`를 합친 것이 아니라 코어/필수 데이터의 선택적 통합이다. 향후 PR 준비 시 현재 `dev`와 이력을 다시 비교하고, PR 대상은 반드시 `dev`로 지정한다.

## UI에서 달라진 점

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/UI/CoreHudBinding.cs` | 코어의 현재 분기 표시, `BattleResolving`/`QuarterComplete` 표시, 프리셋 누락 시 시작 잠금 |
| `Assets/Scripts/UI/CoreRunDecisionBinding.cs` | 분기 완료 시 ‘승리로 종료 / 계속 도전’ 선택창과 코어 요청 연결 |
| `Assets/Scripts/UI/Samples/MvpRuntimeHudSample.cs` | 테스트 보상 키를 분기+웨이브로 구분, 코어의 마지막 분기/웨이브 테스트 이동 연결 |
| `Assets/Scripts/UI/Editor/MvpCoreIntegrationBuilder.cs` | 우리 Runtime HUD 씬에만 카탈로그 참조·선택창·테스트 이동 버튼·검사용 폰트 연결 |
| `Assets/Scripts/UI/Editor/MvpRuntimeHudBuilder.cs` | 기존 씬이 있을 때 필요한 코어 연동을 검사하고, 이미 완료됐으면 저장 없이 반환 |
| `Assets/Scripts/UI/Editor/MvpCoreQuarterValidation.cs` | 실제 코어를 사용한 분기·선택·취소·복원 Play 검사 |
| `Assets/Scripts/UI/Editor/MvpRuntimeHudValidation.cs` | 즉시 종료/3웨이브 종료 가정을 최신 비동기·분기 흐름으로 변경, 기존 검사 유지 |

수정한 씬은 `Assets/Scenes/Test/MvpRuntimeHudTest.unity` 하나다. 기존 공통 HUD 프리팹을 변경한 것이 아니라 이 UI 테스트 씬의 인스턴스와 추가 UI만 연결했다. Build Settings에 씬을 추가하지 않았다.

## 실행 방법

1. 위 D 드라이브 프로젝트를 Unity 6000.3.23f1로 연다.
2. `Assets/Scenes/Test/MvpRuntimeHudTest.unity`를 열고 Play한다.
3. `웨이브 시작 → 테스트 적 전멸 → 전투 정산 대기 → 테스트 보상 +30`을 진행한다.
4. 3웨이브가 끝나면 다음 분기의 1웨이브로 진행하고 분기 표시가 바뀐다.
5. 빠른 확인은 건설 단계에서 `마지막 분기로 → 마지막 웨이브로 → 웨이브 시작 → 테스트 적 전멸`을 사용한다.
6. `승리로 종료`는 코어의 Finished 단계로, `계속 도전`은 보상 선택 대기를 거쳐 다음 분기로 이어진다.
7. 재시작 검사는 `재화·코어 리셋` 버튼을 사용한다.

현재 팀원 코어의 `MAIN_QUARTERS = 3`을 그대로 따른다. 기획의 5분기에 맞추기 위해 UI가 코어 상수를 임의 변경하지 않았다. 테스트 이동은 보상을 지급하지 않는다.

씬이 없는 다른 체크아웃에서는 `Game > UI > Create Missing Runtime HUD Test Scene`으로 생성한다. 기존 UI 씬의 필요한 연결만 적용하려면 `Game > UI > Upgrade Runtime HUD For Core Quarters`를 사용한다. 저장되지 않은 대상 씬은 먼저 저장해야 한다.

## 연결 계약

- `CoreHudBinding.Initialize(ui, flow, waves)`는 코어의 단계/웨이브 이벤트를 표시하고 실제 `TryStartWave()` 완료를 기다린다. 선택적 `_quarterText`를 연결하면 분기를 표시한다.
- `CoreRunDecisionBinding.Initialize(flow, waves)`는 `QuarterDecisionRequested`와 `PhaseChanged`를 구독한다. 코어가 `QuarterComplete`이고 `CanChooseRunDecision`일 때만 선택할 수 있다.
- 선택창의 두 버튼은 기존 `ChooseFinishRun()` / `ChooseContinueRun()`만 호출한다. UI가 직접 다음 분기·승패·보상을 결정하지 않는다.
- 첫 선택 직후 두 버튼을 잠그고 창을 숨긴다. 중복 클릭과 선택 가능 단계 밖의 입력은 무시한다.
- HUD 숨김은 코어 요청을 취소하거나 자동 선택하지 않는다. 재활성화하면 코어의 현재 대기 상태로 창을 복원한다.
- 초기 포커스는 `승리로 종료`이며 두 버튼 간 명시적 좌우 탐색을 제공한다. 창이 닫힐 때 숨겨진 버튼의 포커스를 제거하거나 유효한 이전 UI로 돌린다.
- 전체 배경 Graphic이 아래 UI의 클릭을 막는다. 마우스/키보드 상태를 매 프레임 폴링하지 않는다.
- 코어의 임시 `ShowContinueConfirmationAsync()` 본문은 수정하거나 호출하지 않았다. 현재 실제로 실행되는 요청 이벤트와 선택 메서드를 사용했다.
- `MvpRuntimeHudSample`의 +30 지급과 테스트 대기 해제는 검사용이다. 정식 보상/유물 시스템의 완료 신호로 간주하면 안 된다.

## 검증 결과

Unity Editor의 숨김 배치 프로세스로 실행했다. 사용자의 마우스·키보드를 조작하거나 창을 활성화하지 않았다. 최종 검사 후 배치 프로세스는 종료됐다.

| 검사 | 검증문 | 결과 |
| --- | ---: | --- |
| 코어/재화 HUD 및 해상도·글리프 검사 | 252 | PASS |
| 새 분기/계속·종료/취소/포커스·레이캐스트 검사 | 85 | PASS |
| 기존 실제 유닛 정보 바인딩 | 83 | PASS |
| 기존 HUD/건물 정보/건물 동작/유닛 정보 Editor 회귀 검사 | 518 | PASS |
| 기존 원본 유닛 선택 씬 | 124 | PASS |
| 생존 수 씬의 선택·레이캐스트 | 135 | PASS |
| 등록·사망·재사용·생존 수 집계 | 58 | PASS |
| 합계 | **1,255** | **PASS** |

이 수치는 프로젝트 자체 검증문 개수다. NUnit 테스트 케이스 수가 아니며, 반복 실행의 개수를 합산하지 않았다.

- 전 분기 순차 진행, 분기별 보상 중복 방지, 계속 진행 후 패배 시 기본 구간 클리어 기록 유지, 종료 선택, 자동 진행, HUD 숨김 중 요청 도착, 비활성/재초기화, 선택 대기/종료 연출 중 리셋을 확인했다.
- 새 코어에 맞춘 Runtime HUD 검사를 반복 실행해 저장된 씬의 재실행도 확인했다.
- 실행한 모든 Unity 프로세스의 종료 코드 `0`, C# 컴파일 오류 및 검사 예외 `0`.
- 코어 원본의 `GameFlowController.SelectAndApplyAsync`에 `CS1998` 경고 1종이 있다. 아직 구현 전인 async 메서드에 await가 없는 경고이며, 원본을 보존하기 위해 수정하지 않았다. 경고가 전혀 없다고 보고하지 않는다.
- 새 Assets의 `.meta` 누락 0개, 프로젝트 `.meta` GUID 중복 0개.
- UI 스크립트의 `git diff --check` 통과. Unity가 저장한 씬의 빈 YAML 값 뒤 공백과 코어 원본의 공백은 임의 정리하지 않았다.
- `1280×720` 및 `1920×1080` 선택창 이미지를 실제로 열어 한글·배치·활성 버튼 표시를 확인했다. 첫 프레임의 버튼 색상 전환을 최종 상태로 오해하지 않도록 색상 전환 완료를 기다린 뒤 캡처했다.

### 로컬 증거

`Logs/CoreUiIntegration20260910/`의 `runtime-final.log`, `ui-regression.log`, `unit-selection.log`, `unit-counts.log`가 최종 결과의 근거다. `runtime-first.log`, `runtime-repeat.log`에는 앞선 반복 실행 결과도 남아 있다.

이미지: `Logs/RuntimeHudValidation/hud-1280x720-quarter-choice.png`, `hud-1920x1080-quarter-choice.png`, `hud-1920x1080.png`.

로그/이미지는 Git 제외 대상이다. 환경 정보가 포함될 수 있으므로 로그 전체를 그대로 PR에 게시하지 않는다. 배치 명령 규칙은 통합 전 검증 문서의 재실행 항목과 동일하다.

## 남은 범위와 주의사항

- 정식 스포너·전투 AI·관문/베이스캠프 상태·유물 적용·최종 결과 데이터는 연결 대기다. `HasClearedMainGame`만 보고 최종 승패 패널을 만들어 내지 않았다.
- 공용 게임 씬과 팀원 `Assets/Scenes/Test/Test.unity`는 건드리지 않았다. 현재 로컬의 기존 팀원 테스트 씬에는 새 `_waveCatalog` 연결이 없으므로 이 작업의 검증 씬으로 사용하지 않는다. 해당 씬을 포함한 전체 dev 동기화/설정은 별도 검토가 필요하다.
- Play Mode의 실제 코어 흐름과 UI 내부 포인터/레이캐스트를 검사했지만 하드웨어 마우스·키보드·터치·게임패드 입력 및 Player 빌드는 이번에 실행하지 않았다.
- 다음 통합은 각 담당자의 정식 결과/보상/스포너 계약을 확인한 뒤 UI 바인딩에서 진행한다. 팀원 코드와 공용 씬을 임의로 고치지 않는다.

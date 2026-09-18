# UI 통합 검증 — 2026-09-10

## 작업 기준

- 실제 프로젝트: `D:/Documents/GitHub/OZGL2_2`
- 새 작업 브랜치: `feature/ui-integration-validation`
- 분기 기준: 기존 UI 작업의 HEAD `e777134841945caf8a1224f88474760b080cb002`
- Unity: `6000.3.23f1` (`09d2ecc7fb28`)
- 기존 미커밋 작업을 보존한 상태에서 같은 체크아웃의 브랜치만 새로 만들고 전환했다. 별도 worktree를 만들지 않았다.
- 이번 검증에서 런타임 코드, 팀원 코드, 씬, 프리팹, 패키지, Build Settings는 수정하지 않았다. 이 문서만 추가했다.
- 마우스·키보드 입력이나 창 활성화를 사용하지 않았다. 실행 중인 Unity가 없는 것을 확인하고 `-batchmode` 프로세스를 숨김 상태로 실행했다.

## 실제 실행 결과

다음 수치는 프로젝트 자체 검증문의 개수이며, NUnit / Unity Test Runner 테스트 케이스 수가 아니다. 현재 체크아웃을 검사했으며 최신 원격 코어를 합친 결과가 아니다.

| 실행 | 검증문 | 결과 |
| --- | ---: | --- |
| 생존 수 씬의 유닛 선택·2D/UI 레이캐스트 | 135 | PASS |
| 유닛 등록·생존 수·사망·풀 재사용 | 58 | PASS |
| 기존 HUD·건물 정보·건물 동작·유닛 정보 Editor 회귀 검사 | 518 | PASS (98 + 113 + 150 + 157) |
| 실제 코어·재화 HUD Play 검사 | 124 | PASS |
| 실제 유닛 정보 바인딩 Play 검사 | 83 | PASS |
| 원본 유닛 선택 씬 Play 검사 | 124 | PASS |
| 합계 | 1,042 | PASS |

- 네 번의 Unity 프로세스 모두 종료 코드 `0`. 해당 로그에서 C# 컴파일 오류와 검사 예외가 발견되지 않았다.
- 자동 포인터 검사는 Unity 내부 이벤트 핸들러와 레이캐스트를 사용했다. 물리 마우스·드래그·터치·게임패드를 시험한 것으로 해석하지 않는다.
- 자동 생성된 `1280×720` HUD 이미지와 `1024×768` 유닛 정보창 이미지를 열어 한글 및 배치를 확인했다. 실제 플레이 화면을 조작한 검사가 아니다.
- Player 빌드, 정식 전장 전체 연결, 새 코어 브랜치와의 통합은 이번 결과에 포함되지 않는다.

### 증거와 재실행

로컬 로그는 Git 제외 대상이다. 환경 정보가 포함될 수 있으므로 원문 전체를 PR에 올리지 않는다.

| 진입점 (`Game.UI.Editor.MvpRuntimeHudValidation`) | 로그 (`Logs/UiValidation20260910/`) |
| --- | --- |
| `RunUnitCounts` | `unit-count-baseline.log` |
| `RunRegression` | `ui-regression.log` |
| `Run` | `runtime-hud.log` |
| `RunUnitSelection` | `unit-selection.log` |

닫힌 프로젝트에 `-batchmode -projectPath <프로젝트> -executeMethod <진입점> -logFile <새 로그 경로>`로 실행한다. `RunRegression`만 `-quit`을 추가한다. 나머지는 Play 검사 종료 후 자체적으로 Editor를 종료하므로 `-quit`을 추가하지 않는다. 렌더링/레이캐스트 검사에는 `-nographics`를 사용하지 않는다. 이미 실행 중인 사용자 Editor를 강제 종료하지 않는다.

## 변경 보존 확인

- 검사 전후 UI 스크립트·테스트 씬·관련 데이터/문서·공유 폰트·UI 프리팹·Build Settings 등 106개 파일의 SHA-256이 모두 같았다.
- 기존 공유 폰트의 미커밋 변경을 복원하거나 덮어쓰지 않았다.
- 새 Assets 파일의 `.meta` 누락: 0개.
- `git diff --check` 통과. Git의 LF/CRLF 알림은 코드 오류와 구분한다.
- 기존 `!!GitHub_Desktop<feature/ui-runtime-integration>` stash를 적용하거나 삭제하지 않았다.
- 커밋·푸시·PR·머지는 하지 않았다. 이후 PR 대상은 `dev`이며 `main`이 아니다.

## 최신 코어와의 연결 검토 — 아직 적용하지 않음

2026-09-10 원격 조회 기준:

- `origin/dev`: `d6cc18d202485abd6eb9672ab65ace83b187681a`
- `origin/feature/core-game-test`: `17d0b46c16aa88425cb37b7832ce47aab242769b`

원격 코어에는 단순 합치기 외에 분기 진행과 비동기 전투 종료 변경이 있다. 팀원 브랜치의 변경 범위를 확인했지만 가져오거나 수정하지 않았다.

| 연결 지점 | 확인한 변경 | UI 담당 후속 작업 |
| --- | --- | --- |
| 상태 표시 | `BattleResolving`, `QuarterComplete` 추가 | `CoreHudBinding`의 표시 문구와 버튼 잠금 검증 추가 |
| 진행 위치 | `CurQuarter`, `MAIN_QUARTERS`, 분기 전환 추가 | 웨이브와 분기 표시 정책을 확인하고 연속 분기 검사 추가 |
| 전투 종료 | `ResolveBattleAsync(ResultType)` 및 종료 연출 대기 추가 | 즉시 보상/결과로 바뀐다는 기존 테스트 가정을 수정 |
| 시작 준비 | 유효한 `CurrentPreset`과 `WaveSODictionary` 필요 | UI 전용 테스트 씬에 카탈로그 참조를 명시적으로 연결하고 누락 검사 |
| 계속하기 선택 | `QuarterDecisionRequested`, `CanChooseRunDecision`, `ChooseFinishRun`, `ChooseContinueRun` 추가 | 계속/종료 선택 UI와 중복 입력·취소·재시작 검증 |
| 보상/분기 반복 | 분기별 웨이브 번호가 다시 1부터 시작 | 테스트 보상의 중복 방지 키와 재시작/분기별 지급 검사 검토 |
| 정식 결과/유물 | 테스트 대기 및 임시 메서드가 남아 있음 | 결과 데이터·보상 적용 완료 계약을 담당자와 확정한 뒤 연결 |

현재 원격 코어의 `MAIN_QUARTERS = 3`은 기획의 기본 5분기와 다르다. UI에서 수치를 임의 보정하거나 코어 상수를 변경하지 않는다.

### 다음 작업 순서

1. 최신 코어 변경을 이 브랜치에 가져올지 사용자와 확정한다. 공유 씬·데이터·폰트 등 동반 변경을 무조건 합치지 않는다.
2. 통합한다면 팀원이 제공한 코어를 기준으로 UI 바인딩·UI 전용 샘플/검사만 수정한다. 코어 공개 API 변경이 필요하면 담당자에게 확인한다.
3. 전투 종료 대기 → 보상 → 다음 웨이브/분기와 계속/종료 선택 → 결과·재시작을 검증한다.
4. 기존 1,042개 검증문의 회귀 범위를 유지하고, 새 분기 흐름 검사를 추가한 결과를 별도로 기록한다.

새 코어와의 호환 작업은 검토 단계다. 이 문서의 PASS를 새 코어 통합 완료나 MVP 전체 완성으로 보고하지 않는다.

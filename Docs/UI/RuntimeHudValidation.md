# 실제 재화·코어 HUD 검증

검증일: 2026-09-09. Unity 6000.3.23f1 / Windows Editor / URP 2D / uGUI + TMP.

작업 위치: `D:/Documents/GitHub/OZGL2_2-ui-integration`.
브랜치: `feature/ui-runtime-integration`. 구현 전 통합 기준 커밋: `38c37e7`.
의존 코드와 남은 계약은 `RuntimeHudIntegration.md` 참고.

## 최종 결과

| 검사 | 결과 | 로컬 근거 |
| --- | --- | --- |
| 통합 기준 코드 최초 import/컴파일 | Unity 종료 0 | `Logs/UiIntegrationBaseline.log` |
| 실제 Play Mode 연동 + 글리프/넘침 검사 | 124개 PASS, Unity 종료 0 | `Logs/UiIntegrationPlay5.log` |
| 최종 코드 반복 Play Mode 검사 | 124개 PASS, Unity 종료 0 | `Logs/UiIntegrationPlayFinal.log` |
| 기존 건물 정보 UI | 113개 PASS | `Logs/UiIntegrationRegression.log` |
| 기존 HUD | 98개 PASS | 같은 로그 |
| 기존 건물 액션 UI | 150개 PASS | 같은 로그 |
| 기존 유닛 정보 UI | 157개 PASS, Unity 종료 0 | 같은 로그 |
| 화면 캡처 | 1280×720, 1920×1080 육안 확인 | `Logs/RuntimeHudValidation/hud-*.png` |

서로 다른 자체 검사 642개(새 검사 124 + 기존 회귀 518)를 통과했다.
Unity Test Runner의 NUnit 테스트 개수가 아니다. 최종 로그에는 C# 컴파일 오류나 검사 중 Error/Exception이 없다.
글리프 존재와 텍스트 넘침을 검사했고, 두 해상도에서 한글·버튼·HUD 배치를 확인했다.

## 실제로 검증한 연결

- 실제 `RunCurrencyManager`의 시작 잔액, 증감, 부족한 비용 거절, 골드 외 재화 구분.
- HUD 숨김 동안 이벤트 구독 해제, 다시 표시할 때 최신 잔액/단계 반영.
- 반복 Initialize, 매니저 종료 후 미초기화 표시, 새 판 초기화 후 Refresh.
- 실제 `GameFlowController`/`WaveController`의 준비·전투·보상·종료 단계와 웨이브 번호.
- 연속 시작 요청 차단, 실제 비동기 준비 완료까지 입력 잠금.
- 준비 도중 코어 리셋, 오래된 취소 완료가 새 요청을 덮어쓰지 않음.
- HUD를 숨겨도 진행 중인 코어 준비는 계속되며 다시 표시할 때 상태 복구.
- 테스트 패배, 3웨이브 진행, 샘플 보상 중복 지급 방지, 종료 후 리셋.
- 코어가 최종 결과 데이터를 공개하지 않은 상태에서 승패 패널을 임의로 표시하지 않음.

## 검증 중 수정한 테스트 도구 문제

실패 로그도 `Logs/UiIntegrationPlay.log`, `Play2/3.log`에 해당하는 `UiIntegrationPlay2.log` / `UiIntegrationPlay3.log`로 남겼다.

1. 배치 시작의 저장되지 않은 씬에서는 Additive 새 씬 생성이 실패했다. 배치 생성만 Single로 처리했다.
2. Single 씬 생성 전 불러온 재화 ScriptableObject가 씬 전환 시 해제되어 참조가 0으로 저장됐다. 씬 생성 후 에셋을 불러오도록 수정했고, 저장된 씬에 골드·카탈로그 GUID가 연결된 것을 확인했다.
3. 새 로컬 환경에서 검색 인덱스가 없는 상태로 Editor 시작 콜백보다 먼저 Play를 요청해 `SearchDatabase.EnumerateAll` 예외가 발생했다. 가설은 “Play 예정 상태에서 검색 DB 최초 생성이 생략된다”였으며, 같은 검증의 Play 진입을 Editor의 `delayCall` 뒤로 옮겨 구분 실험했다. 이후 Search.index 생성 → Play 진입 순서로 실행됐고 `UiIntegrationPlay4.log`부터 예외 없이 통과했다. 오류 필터링, 테스트 비활성화, 패키지 변경은 하지 않았다.

검색 DB 가설의 참고: Unity 공개 [SearchDatabase 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/QuickSearch/Editor/Indexing/SearchDatabase.cs)의 최초 생성/Play 상태 검사. 공개 master와 설치 바이너리의 완전한 버전 일치는 확인하지 않았으므로, 내부 엔진 결함의 일반적인 원인으로 단정하지 않는다. 로컬 실행 순서 변경 효과는 반복 검증했다.

## 검증 범위 밖

- 실제 적 AI, 전투 피해, 관문/베이스캠프 파괴 판정, 건설 비용 흐름.
- 정식 보상/최종 결과 화면, 영구 저장, 로드아웃.
- 실제 마우스/키보드 포인터 입력, 타깃 플랫폼 Player 빌드.
- 테스트에서 입력은 `Button.onClick.Invoke()`로 전달했고, 전투 결과는 코어의 TestSpawner 및 샘플 버튼으로 주입했다.
- 시작 골드 100, 보상 30 등은 샘플 수치다. 정식 경제 규칙이 아니다.

## 재실행

- 새 연동: `Game > UI > Validate Runtime HUD In Play Mode`.
- 기존 UI: `Game > UI > Validate Existing UI Regression`.
- 배치 연동: `-batchmode -projectPath D:\Documents\GitHub\OZGL2_2-ui-integration -executeMethod Game.UI.Editor.MvpRuntimeHudValidation.Run -logFile <로그 경로>`.
- 배치 회귀: 위의 메서드를 `Game.UI.Editor.MvpRuntimeHudValidation.RunRegression`으로 바꾸고 `-quit` 추가.
- 연동 배치에는 `-quit`을 넣지 않는다. Play 검증 종료 시 코드가 종료 코드를 반환한다. 화면 검증에는 그래픽 장치가 필요하므로 `-nographics`도 넣지 않는다.
- 로그와 캡처는 Git에서 제외된 로컬 파일이다. 다른 환경에서는 재실행해 생성한다.

## 짧은 일일 보고

> 실제 재화·코어와 공통 HUD 연동 완료. 골드 갱신, 단계·웨이브 표시, 시작 버튼 중복 방지와 리셋 처리 구현. Play Mode 및 기존 UI 자체 검사 642개 통과. 다음 작업은 코어 결과/보상 API 확정 후 결과 화면 연결. 로컬 기능 브랜치에서 작업했으며 원격 push/PR/merge는 하지 않음.

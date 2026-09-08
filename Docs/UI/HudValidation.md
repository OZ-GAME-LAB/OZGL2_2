# 공통 HUD 작업 및 검증 기록

검증일: 2026-09-08 / Unity 6000.3.23f1 / `feature/ui-common-hud`

## 결과

오늘 범위인 화면 목록·연동 규격과 공통 HUD, 보상/최종 결과 패널, 독립 테스트 씬을 구현했다. 실제 코어·재화·건물 시스템과의 통합은 아직 하지 않았다.

| 검증 | 결과 / 근거 |
| --- | --- |
| Unity 스크립트 컴파일 | 생성용 배치 실행 및 Editor 재컴파일 성공. 최종 코드에 C# 컴파일 오류 없음. |
| 에셋 생성 | `MvpCommonHud.prefab`, `MvpHudTest.unity`, Korean TMP font 생성 완료. 기존 에셋 덮어쓰기 없음. |
| 계약/참조/레이아웃 검사 | `Game/UI/Validate MVP HUD`: 최종 98개 검사 통과. 최종 실행 구간에서 Error/Exception/Assert 로그 없음. |
| 초기 UI | `100` 골드, `1 / 3`, `건설`, 시작 버튼 활성 상태를 Play 모드에서 확인. |
| 골드 갱신 | 샘플 버튼 입력 후 `100 → 150` 표시 확인. |
| 시작 거절/재시도 | 거절 안내 후 다시 시작 가능. 재시도 및 더블 클릭 이후 1웨이브 전투 상태 유지. |
| 보상 및 계속하기 | 1·2웨이브 보상 각 30, `150 → 180 → 210`, `1 / 3 → 2 / 3 → 3 / 3` 확인. |
| 마지막 웨이브 | 3웨이브 승리 후 최종 승리, 획득 골드 90, 보유 골드 240 표시 확인. |
| 재시작 | 최종 결과의 다시 시작 버튼으로 `100`, `1 / 3`, `건설` 복원 확인. |
| 패배 | 재시작 후 1웨이브 패배 입력으로 `패배`, 획득 골드 0, 다시 시작 버튼 표시 확인. |
| 화면 크기 | 1920×1080, 1280×720, 1024×768 오프스크린 렌더 및 글자 영역 검사 통과. Editor Game 뷰에서도 한국어·패널 배치 확인. |
| 메타파일 | 신규 Assets 파일 46개의 `.meta` 누락 0개 확인. |
| 기존 팀 파일 | tracked 파일의 내용 diff 없음. 공용 씬·Build Settings·패키지 버전 변경 없음. |

자동 검사는 프리팹 실제 컴포넌트의 공개 API, Button 이벤트, 상태/텍스트를 검사한다. 리스너 생명주기는 Edit Mode에서 초기화/정리 메서드를 직접 호출해 중복 등록과 제거를 검사한다. Unity Test Runner의 PlayMode 테스트를 작성/실행했다는 의미는 아니다. 실제 Play 검증은 위 표의 마우스 조작 경로로 수행했다.

## 검증 중 수정한 도구 문제

- TMP 필수 리소스 가져오기는 비동기이므로 완료 콜백 이후 폰트/프리팹을 생성하도록 수정했다.
- 배치 실행의 이름 없는 시작 씬에는 Single 모드를 사용하고, 대화형 에디터에서는 별도 Additive 씬을 닫아 기존 씬으로 돌아오도록 했다.
- Edit Mode에서 `SendMessage`로 생명주기를 호출하면 Unity assertion이 발생해 직접 메서드를 호출하는 검사로 변경했다.
- `TMP_FontAsset.TryAddCharacters`는 이미 모든 글리프가 있는 경우에도 false를 반환한다. 반복 실행이 가능한 `HasCharacters(..., tryAddCharacter: true)` 검사로 수정했다.
- 오프스크린 캡처는 상태 전환 직후의 렌더러 캐시가 섞이지 않도록 매번 별도 복제본으로 수행한다.

이전 시도의 실패 로그는 로컬 로그에 남아 있다. 최종 성공 표식은 `Logs/HudPlayMode.log`의 `[UI/MvpHudValidation] PASS: 98 checks.`이며, 배치 검증 로그는 `Logs/HudValidation.log`에 있다. `Logs` 폴더는 Git 제외 대상이다.

## 다시 확인하는 방법

1. Unity에서 기존 작업 씬을 저장한 뒤 `Assets/Scenes/Test/MvpHudTest.unity`를 연다.
2. Play를 누르고 `Docs/UI/MvpCommonHudSpec.md`의 확인 순서를 수행한다.
3. Play 종료 후 `Game > UI > Validate MVP HUD`를 실행한다. Console에 PASS가 출력되어야 한다.
4. 화면 캡처는 `Logs/HudValidation`에 생성된다.

프리팹/씬이 이미 있으면 생성 도구는 덮어쓰지 않는다. 이후 배치나 문구를 바꾸려면 프리팹을 직접 편집하고 검증한다. 생성 도구는 최초 누락 에셋 생성용이다.

## 제한 및 다음 작업

- Windows Editor의 마우스 입력 경로를 검증했다. 키보드 Submit 자동 입력은 동작 확인이 되지 않았으므로 키보드/게임패드 지원 검증 완료로 간주하지 않는다.
- 실제 게임 판정, 보상 지급, 생산 조건, 코어/재화 연결, 실행 파일 빌드, 팀 브랜치 통합은 별도 작업이다.
- 다음 우선 작업은 건물 선택·정보 패널이며 건물 ID/선택 이벤트/표시 데이터 계약 합의가 선행된다.
- `TutorialMap` 원격 브랜치가 확인되지 않아 PR이나 원격 push를 하지 않았다. 현재 결과는 로컬 변경 상태다.
- Unity가 실행 중 생성한 미추적 `ProjectSettings/SceneTemplateSettings.json`은 설정 삭제 안전 검토에 따라 보존했다. 커밋 전 팀이 포함 여부를 결정한다.

## 글꼴 출처

한국어 글꼴은 [Noto CJK 공식 저장소의 Korean Regular](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf)를 원본 그대로 사용한다. SIL Open Font License 원문은 `Assets/Art/Fonts/NotoSansKR/OFL.txt`에 함께 보관한다. Windows 설치 글꼴에 의존하지 않는다. TMP 기본 리소스는 현재 프로젝트에 설치된 uGUI 패키지의 공식 Essential Resources를 사용한다.

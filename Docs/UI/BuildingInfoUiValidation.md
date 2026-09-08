# 건물 정보 UI 검증 기록

2026-09-08 / Unity 6000.3.23f1 / `feature/ui-common-hud`.

## 현재 판정

컴파일·Edit Mode 자체 검사·오프스크린 화면 검증 완료. 2026-09-08 후속 작업에서 **실제 Play 모드 마우스 조작도 통과**했다.
건물/코어/재화 실제 통합 및 실행 파일 빌드는 이번 검사 범위가 아니다.

## 기준 상태

- HEAD `e472459fc88d5d8970cce00734c1834d504273bc`.
- 기존 공통 HUD 작업은 로컬 미추적 변경 상태였다. 해당 작업을 보존했다.
- 시작 시 `MvpHudTest` 씬, Play 종료 상태. 기존 HUD 자체 검사 결과는 98개 통과였다.
- 패키지/공용 씬/Build Settings 변경 없음. Unity MCP 연결은 없으며 로컬 Editor와 로그를 사용했다.

## 결과

| 항목 | 결과 및 근거 |
| --- | --- |
| Unity 컴파일 | 런타임 및 Editor 어셈블리 컴파일 성공. 최종 코드에 C# 오류 없음. |
| 건물 정보 자체 검사 | `Game > UI > Validate Building Info UI`: **113개 통과**. |
| 기존 HUD 회귀 검사 | `Game > UI > Validate MVP HUD`: **98개 다시 통과**. |
| 표시 계약 | 필수 데이터 유효성, 잘못된 입력의 부분 적용 방지, 이름/레벨/생산/효과, 아이콘/설명 누락 대체 표시 확인. |
| 선택/닫기 | 같은 종류의 다른 인스턴스 구분, 동일 대상 표시 갱신, 이전 정보 정리, 닫기 이벤트 ID, 반복 enable/disable 리스너 중복/제거 확인. |
| 스크롤 | 긴 설명의 콘텐츠 높이, 같은 선택 갱신 시 스크롤 유지, 다른 선택 시 맨 위 복귀 확인. |
| 화면 검사 | 1920×1080, 1280×720, 1024×768에서 패널 경계/글자 영역/글리프 검사. 1280×720 일반·긴 설명, 1024×768 캡처를 육안으로 확인. |
| 메타/GUID | 신규 Assets 파일 7개 모두 `.meta` 존재. 전체 Assets 메타 GUID 중복 그룹 0개. |
| 팀 파일 보존 | Git tracked 파일 내용 diff 없음. 기존 HUD 런타임 API와 기존 프리팹/씬 변경 없음. |
| 실제 Play 조작 | 4종 선택, 전사 레벨 갱신, 닫기/재선택/외부 선택 해제 확인. 종료 후 런타임 변경 저장 없음. |
| Player 빌드/기기 입력 | Player 빌드, 키보드/게임패드 미실행. 마우스 클릭은 Editor Play에서 확인. |

검사는 Unity Test Runner의 EditMode/PlayMode 테스트가 아닌 메뉴 실행형 자체 검증 도구다.
프리팹을 별도 씬에 복제하고 공개 API/버튼 이벤트로 표시를 확인한다. Edit Mode 생명주기 검사에만 private 메서드를 직접 호출한다.
스크롤 위치 검사는 프로그램으로 값을 설정한다. 실제 마우스 휠/드래그 조작 검증과 구분한다.

## 검증 중 수정

- 첫 검사에서 종류 문구(`Category`)의 텍스트 영역 높이가 부족했다.
- 새 건물 프리팹의 Category 높이를 28→40, Level 높이를 34→44로 조정하고 위치를 함께 맞췄다. 생성 도구도 같은 값으로 맞췄다.
- 해당 새 프리팹의 확인된 RectTransform 값만 수정했으며 GUID/fileID와 기존 HUD 에셋은 보존했다.
- 최종 건물 UI 113개 및 HUD 98개 실행 구간에서 Error/Exception/Assert 0개.
- 이전 실패 로그는 삭제/숨김 처리하지 않았다. Console에 남은 이전 메시지와 최종 성공 결과를 구분한다.

## 로그와 캡처

- `Logs/HudPlayMode.log`: `[UI/MvpBuildingUiValidation] PASS: 113 checks.` 및 뒤의 `[UI/MvpHudValidation] PASS: 98 checks.`
- `Logs/BuildingUiValidation/building-warrior-1920x1080.png`
- `Logs/BuildingUiValidation/building-warrior-1280x720.png`
- `Logs/BuildingUiValidation/building-warrior-1024x768.png`
- `Logs/BuildingUiValidation/building-long-description.png`
- `Logs/BuildingUiValidation/building-empty.png`

로그/캡처는 Git 제외 폴더에 둔다. 로그 파일명에 PlayMode가 포함되어 있어도 이번 건물 UI 검사는 Edit Mode에서 실행했다.
기존 동적 한국어 TMP 폰트는 신규 한국어 표시를 위해 글리프/아틀라스가 갱신될 수 있다. 글꼴 소스·라이선스·GUID는 유지한다.

## 다음 확인

1. 김도현 담당의 선택 ID/이벤트/표시 데이터 계약을 합의한다.
2. 건설·업그레이드·해체 UI는 후속 구현/검증 완료. `BuildingActionUiSpec.md`, `BuildingActionUiValidation.md`를 참조한다.
3. 실제 담당 시스템과 연결한 뒤 재검증한다. 긴 설명의 실제 마우스 휠/드래그는 별도 확인한다.

이번 작업에서도 커밋·푸시·PR·GitHub 이슈 등록은 하지 않았다.

# 실제 유닛 정보 UI 연결

## 작업 위치와 범위

- 프로젝트: `D:\Documents\GitHub\OZGL2_2`
- 브랜치: `feature/ui-runtime-integration`
- Unity: `6000.3.23f1`, Windows Editor
- 변경 범위: `Assets/Scripts/UI` 및 이 문서만. 팀원 유닛 코드, 공용 씬/프리팹, 패키지/설정은 변경하지 않는다.
- 별도 worktree를 만들거나 사용하지 않는다. 향후 PR 대상은 `dev`; 이번 작업에서 커밋/푸시/PR/머지는 하지 않는다.

## 연결 컴포넌트

| 컴포넌트 | 위치/역할 |
| --- | --- |
| `RuntimeUnitInfoSource` | `Unit_Core`, `Unit_Life`, `Unit_RuntimeStatus`와 같은 GameObject에 추가하는 UI 어댑터. 공개 값/이벤트만 읽는다. |
| `RuntimeUnitInfoBinding` | `UnitInfoPanel`과 같은 GameObject에 배치. 선택한 소스 구독, 화면 갱신, 선택 해제를 담당한다. |

유닛 생성/선택 담당자와 연결할 때 사용할 순서:

1. 스폰 주체가 기존 방식대로 `Unit_Core.Initialize(...)`를 완료한다. UI는 대신 초기화하지 않는다.
2. 선택 입력 주체가 `binding.TrySelect(source)`를 호출한다. 미초기화/비활성/없는 유닛은 `false`를 반환하며 기존 선택을 유지한다.
3. 이후 체력, 보호막, 능력치 변경은 이벤트로 표시한다. 매 프레임 검색하거나 전투 수치를 변경하지 않는다.
4. 빈 공간 선택 등 명시적 해제가 필요하면 `binding.ClearSelection()`을 호출한다.

```csharp
// 스폰/선택 시스템에 연결할 때의 호출 예시. 해당 팀원 코드를 이번에 수정하지는 않았다.
bool selected = unitInfoBinding.TrySelect(unitInfoSource);
```

병종, 설명, 아이콘은 현재 UnitData에 해당 필드가 없어 소스의 선택적 UI 필드로 제공한다. 병종 미입력 시 `병종 미지정`을 표시하며 병종/진영 세력을 추정하지 않는다. 공격력·방어력·공격 속도·이동 속도·보호막은 실제 공개 값이다.

## 수명 및 이벤트 계약

- `Unit_Life.HpChanged`는 초기화 때 `(현재, 최대)`, 피해/회복 때 `(이전, 현재)`를 전달한다. 어댑터는 인자를 수치로 해석하지 않고 `CurrentHp`, `MaxHp`를 다시 조회한다.
- 최대 체력이 증가하면 HpChanged가 없을 수 있으므로 `MaxHpChanged`도 구독한다. 감소 시 실제 유닛의 체력 제한 처리와 후속 알림을 반영한다.
- 체력 0/사망은 정보창에 남긴다. 유닛 GameObject 비활성화/파괴 시 선택을 해제한다.
- UI 비활성화 시 구독을 해제하고, 다시 켤 때 같은 유닛 수명인 경우 최신 값을 읽는다.
- 소스는 활성화마다 새 SelectionId를 부여한다. 풀 반환 시 비활성화하고 재사용 시 활성화하는 흐름이 기준이다. 활성 상태에서 `Unit_Core.Initialize`만 다시 호출하는 경우를 별도 스폰으로 판정하지 않는다.
- 사용자가 닫거나 다른 유닛을 선택한 뒤 이전 유닛 알림이 와도 창을 다시 열거나 새 선택을 덮어쓰지 않는다.
- `UnitInfoPanel` 인스턴스 하나의 선택은 이 바인딩 하나가 소유한다. 다른 UI 컨트롤러와 동시에 선택을 갱신하지 않는다.

## 검증 실행

현재 씬을 저장하고 Edit Mode에서 `Game > UI > Validate Runtime HUD In Play Mode`를 선택한다. 기존 HUD 검사 후 실제 `Unit_Core/Unit_Life/Unit_RuntimeStatus`와 임시 ScriptableObject를 만들어 유닛 UI를 검사한다. 검사 완료 후 임시 오브젝트/데이터를 제거하고 원래 씬 구성으로 돌아온다. 씬이나 유닛 데이터 에셋을 저장하지 않는다.

기존 회귀 검사는 `Game > UI > Validate Existing UI Regression`으로 실행한다. 두 검사는 NUnit/Test Runner 테스트 개수가 아닌 프로젝트 자체 검증의 assertion 개수로 보고한다.

검사 범위:

- 미초기화 선택 거절, 아군/적군 실제 데이터 표시
- 피해/회복, 보호막 흡수, 최대 체력 증가/감소, 공격력 버프/해제
- 숨기기/재표시, 닫기, 연속 선택, 이전 개체 알림 무시
- 사망/디스폰, 풀 재사용, UI가 숨겨진 사이 재사용, 바인딩 파괴
- 긴 설명 스크롤 유지, 1280×720/1920×1080 글리프 및 텍스트 넘침

이미지: `Logs/RuntimeUnitInfoValidation/unit-1280x720.png`, `unit-1920x1080.png`.

## 2026-09-09 검증 결과

상태: **Ready with limitations — UI 연결 컴포넌트 범위 통과, 공용 게임 씬 통합은 별도 작업.**

기준 커밋 `e777134841945caf8a1224f88474760b080cb002`에 이번 UI 변경을 적용한 작업 트리에서 실행했다. 시작 전 작업 트리는 깨끗했고 Unity Console 오류/경고는 0/0이었다. GitHub Desktop에 보관된 기존 변경 3개는 적용·삭제하지 않았다.

| 기준 | 결과/증거 |
| --- | --- |
| D드라이브 프로젝트·브랜치 일치 | Unity 프로세스 projectPath 및 Desktop Current worktree/branch 확인. Desktop에 이번 변경 8개 표시. |
| Unity 컴파일 | 통과. 실제 Editor 임포트/Assembly-CSharp 및 Editor 어셈블리 컴파일 성공. |
| 유닛 UI 실제 런타임 | `MvpRuntimeUnitInfoValidation`: Play Mode 자체 검사 83개 통과. 피해/회복/능력치/수명 시나리오 포함. |
| 기존 코어·재화 HUD 런타임 | `MvpRuntimeHudValidation`: Play Mode 자체 검사 124개 통과. |
| 기존 UI 회귀 | HUD 98 + 건물 정보 113 + 건물 행동 150 + 유닛 정보 157 = 518개 통과. |
| 화면 | 1280×720/1920×1080 캡처 직접 확인. 체력 80/100, 전투 수치, 한글과 카드 배치 정상. |
| 오류/경고 및 정리 | 최종 Console 오류 0, 경고 0. Play 종료, 원래 MvpRuntimeHudTest 씬 복원. 새 회귀 없음. |
| 원본 보존 | 변경 8개는 UI 스크립트/메타/이 문서뿐. 팀원 코드·기존 씬·프리팹·설정·stash 유지. |
| 실제 전투 씬/Player 빌드 | 미실행. 이번 컴포넌트 검증에 포함하지 않음. |

합계 725개는 최종 각 검증 묶음의 assertion 합이며 재실행 횟수를 중복 합산하지 않았다. Unity Test Runner 테스트 725개라는 의미는 아니다.

실행 로그 보존: `Logs/RuntimeUnitInfoValidation/editor-validation-20260909-1417.log` (로컬 검사 산출물, Git 제외). 다음 통합 단계는 팀원과 스폰·선택 호출 위치를 합의한 후 전투 씬에서 연결하는 것이다.

## 남은 연결 범위

이 작업은 **UI 연결 컴포넌트와 실제 유닛 컴포넌트 기반 검사**다. 공용 전투 씬의 클릭 선택, 실제 스포너/풀, 전투 AI, 최종 Player 빌드까지 연결·검증한 것은 아니다. 팀원 프리팹/씬은 건드리지 않았으므로 담당자와 스폰·선택 호출 위치를 합의한 후 통합해야 한다. 현재 열려 있는 `MvpRuntimeHudTest` 씬의 일반 Play만으로 유닛 선택 기능이 자동 추가되지는 않는다.

# 전투 승리 아티팩트 선택 UI

## 작업 범위

- 프로젝트: `D:\Documents\GitHub\OZGL2_2`
- 브랜치: `feature/ui-integration-validation`
- 최신 진행 기획과 PDF 보드의 **매 전투 승리 후 기본 3개(효과에 따라 가변) 후보 → 1개 선택/확정 또는 모두 포기** UI.
- UI 구현과 독립 샘플을 사용한다. 팀원 Core, Economy, Artifacts, 공유 씬/프리팹, Packages, ProjectSettings는 수정하지 않는다. 우리가 만든 `MvpArtifactReward.prefab`에 가변 후보와 PDF 승리 보상 와이어프레임 배치를 반영한다.
- 이전 분기 종료/계속 UI는 검토용 구현이며 최신 기획에서 확정된 규칙으로 간주하지 않는다.

## 제공 파일과 실행

- `Assets/Scripts/UI/ArtifactRewardViewData.cs`: 불변 후보/보상 스냅샷과 요청 데이터.
- `Assets/Scripts/UI/ArtifactRewardPanel.cs`: 선택, 하이라이트, 확정, 선택 해제, 포기, 요청 상태.
- `Assets/Prefabs/UI/MvpArtifactReward.prefab`: 별도 Canvas의 모달.
- `Assets/Scenes/Test/MvpArtifactRewardTest.unity`: UI 전용 모의 응답 씬.
- `Assets/Scripts/UI/Samples/MvpArtifactRewardSample.cs`: 샘플 후보와 즉시 성공 응답. 실제 지급/효과 없음.
- `Assets/Data/UI/Tests/ArtifactRewardTestFont.asset`: 테스트 전용 한국어 폰트. 팀 공용 동적 폰트를 저장하지 않는다.
- `Game > UI > Create Missing Artifact Reward Assets`: 없는 전용 에셋 생성 및 우리 아티팩트 프리팹의 페이지 참조/승리 헤더 배치를 한 번 보완. 완료 후 반복 실행은 저장하지 않는다. 기존 씬은 재저장하지 않는다.
- `Game > UI > Validate Artifact Reward In Play Mode`: 저장된 씬 상태에서 자동 검사. 완료 후 Play Mode 종료.

테스트 씬을 열고 Play하면 기본 3개 후보가 표시된다. 후보를 클릭해도 요청하지 않으며 **선택 확정**에서만 전달한다. 선택하지 않았거나 선택 해제를 누르면 **모두 포기하고 계속**이 표시된다. 모의 응답 후 뒤의 **다음 모의 보상 열기 (N개)**를 누르면 후보 수가 3 → 5 → 1 → 2 → 7 순서로 순환한다. 씬은 Build Settings에 추가하지 않는다.

한 페이지에는 최대 3개를 표시한다. 후보가 더 많으면 카드 양옆 이전/다음 버튼으로 이동하며, 전체 후보를 자르지 않는다. 페이지를 바꿔도 선택은 유지되고 상태 문구에 선택한 후보 이름이 표시된다. 현재 페이지 밖의 후보도 그대로 확정할 수 있다. 요청 처리 중에는 페이지 이동도 잠긴다. 선택되지 않은 상태에서만 포기가 전달된다.

## PDF 와이어프레임 배치 반영 (2026-09-10)

원본 PDF의 승리 UI 영역을 확대 렌더링해 확인했다. 원본은 흰 배경/회색 테두리의 배치용 도식이며 최종 게임 아트가 아니다. 이번 범위는 구조를 맞추는 작업이지 픽셀 단위 복제나 최종 디자인 완료가 아니다.

- 상단 중앙에 별도 승리 제목 영역을 둔다.
- 획득 골드와 보석은 중앙에 두 줄로 표시한다. PDF의 1700G 예시를 실제 보상 값으로 넣지 않는다.
- 세로 카드 3개를 같은 간격으로 배치한다. 각 카드 내부는 **이미지 → 이름 → 등급 → 효과** 순서다.
- 하단 주 버튼을 중앙에 배치한다. 선택 여부에 따라 선택 확정/모두 포기로 동작한다.
- 원본 외의 보조 기능인 선택 해제, 처리 상태, 페이지 이동은 유지하되 주 버튼과 구분한다. 페이지가 하나뿐이면 이동 버튼은 숨긴다.
- 기존 어두운 임시 팔레트/한국어 폰트와 샘플 내용을 유지한다. 실제 아티팩트 Sprite와 최종 아트/폰트/테마는 별도 작업이다.

배치 변경은 전용 프리팹의 기존 오브젝트/참조를 재사용한다. `VictoryHeaderFrame`과 등급 텍스트의 최소 높이를 확인하여 완료된 배치를 자동으로 반복 덮어쓰지 않는다. 좌우 페이지 버튼의 방향키 탐색은 바로 옆 카드로 돌아오게 배치와 일치시켰다. 게임 규칙, 외부 공개 API, 후보 데이터와 요청 계약은 바꾸지 않았다.

### 배치 검증 결과

상태: **UI 단독 범위 검증 완료, 실제 게임 통합에는 제한 있음 (Ready with limitations).**

| 확인 항목 | 결과/근거 |
| --- | --- |
| PDF 배치 구조 | 통과: 중앙 제목/2줄 보상/세로 카드/이미지-이름-등급-효과 순서/중앙 주 버튼을 캡처와 사각형 경계 검사로 확인 |
| 기존 선택·확정·포기·페이지·비동기 요청 | 통과: `MvpRuntimeHudValidation.RunArtifacts`, 1,193개 사용자 정의 assertion, `Logs/artifact-wireframe-final.log` |
| Runtime HUD/분기/유닛 정보 회귀 | 통과: `MvpRuntimeHudValidation.Run`, 252 + 85 + 83개 assertion, `Logs/artifact-wireframe-runtime-regression.log` |
| Editor UI 회귀 | 통과: `MvpRuntimeHudValidation.RunRegression`, 98 + 113 + 150 + 157개 assertion, `Logs/artifact-wireframe-editor-regression.log` |
| 컴파일/실행 | Unity 6000.3.23f1, 최종 3개 실행 exit code 0. 신규 컴파일 오류/실행 예외 없음. 기존 Core `GameFlowController.cs:305`의 CS1998은 미수정 |
| 화면 확인 | 1280×720 및 1920×1080, 선택/미선택/5개 후보 2페이지/1개 후보 캡처. 한글·글자 넘침·버튼 경계·카드 겹침·실제 카드 렌더링 검사 통과 |
| 변경 보호 | 시작 시 파일 487개 중 UI 프리팹, UI 코드 3개, 문서 2개만 변경. 나머지 481개/모든 씬/폰트/팀원 코드/설정/기존 메타는 내용 동일. `.meta` 260개 GUID 중복 없음 |
| 반복 실행 | 최종 3개 검사 전후 소스 파일 해시 동일. 이미 보완된 프리팹/폰트를 반복 저장하지 않음 |
| 범위 외 | 실제 아티팩트 아트·효과 적용·매 전투 승리 연결·Player 빌드·하드웨어 입력 검사는 미완료/미실행 |

합계 2,131개 assertion이며 NUnit 테스트 개수는 아니다. 첫 배치 검사에서 등급 텍스트 높이 부족을 발견한 로그는 `Logs/artifact-wireframe-first.log`에 보존했다. 높이를 수정한 다음 1,174개 검사(`Logs/artifact-wireframe-layout.log`)를 통과했고, 좌우 탐색 검사 19개를 추가한 최종 실행이 1,193개다. 실패 검사를 제거하거나 팀원 코드를 바꾸지 않았다.

이미지: `Logs/ArtifactRewardValidation/reward-{1280x720|1920x1080}-{selected|unselected|five-page2|single}.png`. 테스트 전용 씬 `Assets/Scenes/Test/MvpArtifactRewardTest.unity`를 열어 Play하면 확인할 수 있다. 실제 게임 씬의 보상 UI 연결 완료로 오해하지 않는다. 검사는 숨김 Unity batchmode에서 수행했으며 OS 입력/창 조작, 커밋/푸시/PR/merge는 하지 않았다.

## 외부 시스템 연결 계약

### 팀 아티팩트 매니저용 비동기 선택 계약 (2026-09-18)

- UI 소유 `ArtifactRewardBinding.SelectAsync(IReadOnlyList<ArtifactData> candidates, CancellationToken token)`을 추가했다. 후보는 팀 아티팩트 시스템이 생성하며 UI는 재추첨하지 않는다.
- 카드 선택 후 확정하면 전달받은 목록의 **동일한 `ArtifactData` 객체**를 반환한다. 미선택 확정(포기)은 `null`을 반환한다. 선택만으로 인벤토리·효과·재화는 변경하지 않는다.
- 선택 전용 창에는 확인되지 않은 골드·보석 `--` 줄을 숨긴다. 실제 지급 금액을 전달하는 기존 승리 보상 창의 표시는 유지한다.
- 호출자가 취소 토큰을 취소하거나 UI를 비활성화/파괴하면 대기를 취소하고 모달을 정리한다. 빈 목록·중복 ID는 열지 않고 오류로 알린다. 한 번에 한 요청만 처리한다.
- 기존 `TryShowReward`는 샘플/기존 연동을 위해 유지된다. 한 보상에 두 경로를 동시에 호출하지 않아야 한다.
- 팀 담당자가 `ArtifactManager.SelectAndApplyAsync`에서 이 UI를 기다리고, 반환된 항목이 해당 후보에 속하는지 재확인한 뒤 **한 번만** `TryAdd`를 호출해야 실제 게임 보상이 완성된다. 현재 팀 매니저와 Core의 테스트 토글은 이 브랜치에서 수정하지 않았다. 따라서 실제 게임 진행 전체 연결은 아직 완료되지 않았다.

최신 `dev`를 반영한 뒤 기존 UI 전용 런타임 샘플을 팀의 새 재화·웨이브 초기화 API에 맞췄다. 테스트 전용 전투 대역과 1레벨 코어 데이터는 검증 중에만 사용하며 실제 팀 스폰·건설·전투를 대신하지 않는다. Unity 6000.3.23f1 Play Mode에서 선택 UI 1,205개, 승리 보상 130개, 공통 HUD 267개 검사가 통과했다. HUD 검사 안의 재화 20개·분기 115개·유닛 정보 83개 검사는 중복 합산하지 않는다. 이 수치는 사용자 정의 검증 항목이며 NUnit 테스트 개수가 아니다. 로그는 `Logs/ui-artifact-async-*.log`에 있다.

1. 실제 담당 시스템이 해당 전투의 보상과 1개 이상의 서로 다른 후보를 결정한다. 기본은 3개이며 효과에 따른 개수 변경도 담당 시스템이 계산한다. UI는 전달받은 전체 목록을 표시한다.
2. 수신자는 `ChoiceRequested`를 구독한다. 하나의 권한 있는 처리 주체만 연결한다.
3. `ShowReward(ArtifactRewardViewData)`에 플레이/전투별 고유한 `RewardId`와 후보를 전달한다.
4. UI는 `ArtifactRewardRequest`의 `RequestId`, `RewardId`, `ArtifactId`만 전달한다. 포기일 때 `ArtifactId == null`, `IsForfeit == true`다.
5. 수신자는 현재 보상인지, 후보에 속하는지, 이미 수령했는지 재검사하고 실제 효과를 한 번만 적용한다.
6. 처리 완료 시 `TryResolveRequest(request.RequestId, succeeded, message)`로 응답한다. 요청 접수와 실제 완료는 다르다.
7. 성공하면 UI가 닫히지만 다음 단계 진행은 담당 시스템이 결정한다. 실패하면 메시지와 선택을 유지하며 재시도할 수 있다.

`AwardedGold`/`AwardedGems`는 **이미 지급된 실제 보상 표시**이며 UI가 재지급하지 않는다. `0`은 지급 없음, `null`은 확인할 수 없음(`--`)이다. 매번 보석을 지급한다고 가정하지 않는다. 희귀도 이름/색상과 효과 설명도 수신 데이터 그대로 표시한다.

`ArtifactRewardViewData.CandidateCount`는 기존 호출 호환을 위해 남긴 기본값 3이지 입력 제한이 아니다. 실제 수는 `Candidates.Count`로 조회한다. 빈 목록/중복 ID/null 후보는 거절한다. 빈 후보의 자동 진행 규칙은 확정하지 않았다. `CurrentPageIndex`는 0부터 시작하고 `PageCount`는 데이터가 없으면 0이다. 같은 `RewardId`에 다른 개수의 목록을 넣어도 원래 스냅샷을 대체하지 않으며, 실제 새 보상은 새 ID를 사용한다.

실제 아티팩트 이미지가 있으면 `Sprite`를 전달한다. 없으면 **이미지 준비 중**을 표시한다. 샘플 이름·등급·효과·골드 30은 테스트 예시이지 확정 밸런스나 실제 보상 규칙이 아니다.

## 재진입과 수명 주기

- 전송 전에 잠가 더블클릭/동기 재진입을 차단한다. 후보 선택은 요청을 발생시키지 않는다.
- `HideReward()` 또는 컴포넌트 비활성화는 실제 요청을 취소하지 않는다. 대기 중 응답은 숨겨진 상태에서도 처리할 수 있다.
- 동일 `RewardId` 재오픈은 최초 스냅샷, 선택, 처리 중/완료 상태를 유지한다. 완료한 동일 보상은 다시 열리지 않는다.
- 다른 보상으로 대기 중 요청을 덮어쓰면 예외가 발생한다.
- 실제 소유자가 이전 플레이/보상을 무효화한 뒤 `ResetReward()`를 호출한다. UI는 이후 이전 요청 ID 응답을 무시한다. 이 메서드 자체가 게임 로직이나 진행 중인 서버/비동기 작업을 취소하지는 않는다.
- 새 시도마다 새 `RequestId`를 사용한다. 이전 실패 요청의 늦은 성공 응답도 무시한다.
- UI는 전체 수령 이력을 저장하지 않는다. 과거 보상을 새 보상으로 다시 제출하는 것까지 막는 중복 적용 검사는 실제 담당 시스템의 책임이다.
- 모달은 전체 화면 포인터를 가로막는다. 기본 포커스는 첫 카드(자동 선택 아님)이며 방향 이동/Submit을 지원하고 닫힐 때 이전 유효 UI 포커스를 복원한다. 전역 게임 입력 잠금은 담당 입력 시스템에서 처리해야 한다.

## 현재 연결하지 않은 범위

현재 팀원 `GameFlowController`의 `ArtifactSelectionRequested`는 분기 종료 흐름에만 있다. 후보/지급 내역/선택 처리 결과를 전달하는 실제 아티팩트 계약도 아직 없다. 따라서 **매 전투 승리 → 실제 효과 적용 → 다음 단계**는 연결 완료가 아니다. 이번 UI를 기존 이벤트에 임의로 붙이거나 UI에서 코어 전이를 우회하지 않았다.

코어/아티팩트 담당자와 매 승리 보상 요청 시점, 고유 보상 ID, 후보 데이터, 선택/포기 처리, 완료 응답, 플레이 초기화 규칙을 합의한 후 어댑터를 연결한다. 상점/이벤트 확률, 마나, 영구 성장, 최종 정산 공식, 전멸 판정은 이번 범위에서 구현하지 않는다.

## 가변 후보 확장 검증 (2026-09-10)

원문 검토와 미확정 항목은 `Docs/UI/PlanningBoardReview.md`를 참고한다. UI 단독 범위 검증 완료이며, 실제 게임 보상/효과 통합 완료를 뜻하지 않는다.

| 검사 | 통과한 사용자 정의 assertion | 로그 |
| --- | ---: | --- |
| 기존 선택 동작 + 가변 후보/페이지/수명 주기/화면 검사 | 989 | `Logs/artifact-variable-first.log`, `Logs/artifact-variable-repeat.log` (각 실행 989) |
| Runtime HUD + 분기 연동 + 유닛 정보 | 252 + 85 + 83 | `Logs/artifact-variable-runtime-regression.log` |
| HUD/건물 정보/건물 액션/유닛 정보 Editor 회귀 검사 | 98 + 113 + 150 + 157 | `Logs/artifact-variable-editor-regression.log` |

반복 실행을 중복 합산하지 않은 합계 1,927개 assertion 통과, 모든 실행 exit code 0. NUnit 테스트 개수가 아니다.

- 1·2·3·4·5·7·25개 후보에서 전체 후보 접근/선택/확정, 남는 슬롯 숨김, 페이지 경계, 페이지 밖 선택 표시, 포기/재시도, 처리 중 페이지 잠금, 새 보상 초기화를 검사했다.
- 키보드/패드 탐색에 해당하는 EventSystem 합성 move/submit으로 페이지 버튼 접근과 마지막/첫 페이지 포커스 복원을 검사했다. 실제 OS 입력을 조작하지 않았다.
- 5개 후보의 2페이지(후보 4·5) 및 1개 후보 화면을 1280×720, 1920×1080에서 캡처/검사했다. 이미지 경로는 `Logs/ArtifactRewardValidation/reward-{해상도}-five-page2.png`, `reward-{해상도}-single.png`다.
- 기존 스냅샷/요청 계약을 유지한다. 동일 RewardId에 후보 수가 다른 데이터를 다시 전달해도 원래 스냅샷을 유지하며, 이전 요청의 늦은 응답은 새 보상에 적용되지 않는다.
- 신규 컴파일 오류/최종 실행 예외 없음. 전체 컴파일에서 기존 `GameFlowController.cs:305`의 CS1998 경고를 확인했으며 해당 코드는 수정하지 않았다.
- 시작 기준 기존 파일 486개 중 변경은 아티팩트 UI 관련 코드 5개, 해당 UI 프리팹/전용 테스트 폰트, 이 문서 총 8개다. 나머지 478개는 내용 해시가 동일하다. `PlanningBoardReview.md`만 새로 추가했다.
- 팀원 코드, 기존 다른 UI 작업, 모든 씬, 공유 폰트, Packages/ProjectSettings와 기존 `.meta`는 동일하다. `.meta` 260개에서 GUID 중복 없음. 프리팹 보완 뒤 반복 검사에서도 프리팹/테스트 폰트 내용이 동일하여 반복 저장이 없음을 확인했다.
- 커밋/푸시/PR/merge, 실제 효과 적용, Player 빌드 또는 하드웨어 입력 검사는 수행하지 않았다.

### 최초 3개 고정 버전 검증 (이전 기록)

검사 코드: `MvpArtifactRewardValidation.cs`. 기존 `MvpRuntimeHudValidation`의 저장/복원/Play 종료 절차를 재사용하며 `RunArtifacts` 진입점과 종료 과정 오류 기록을 추가했다.

대상: 후보 3개/중복 ID 검증, 불변 스냅샷, 선택과 확정 분리, 포기, 중복 입력, 동기 응답, 지연/외부/오래된 응답, 거절 후 재시도, 숨김/재활성화, 초기화, 수신자 연결/해제, 이미지 폴백, 모달 차단, 합성 탐색/Submit, 1280×720 및 1920×1080의 한글/텍스트 넘침/버튼 경계.

2026-09-10 실행 결과: **UI 단독 범위 검증 완료, 실제 게임 보상 통합은 미완료.**

| 검사 | 통과한 사용자 정의 assertion | 로그 |
| --- | ---: | --- |
| 아티팩트 선택 UI | 320 | `Logs/artifact-reward-capture-verified.log` |
| 기존 Runtime HUD + 분기 코어 연동 + 유닛 정보 | 252 + 85 + 83 | `Logs/artifact-final-runtime.log` |
| 기존 HUD/건물 정보/건물 액션/유닛 정보 Editor 회귀 검사 | 98 + 113 + 150 + 157 | `Logs/artifact-regression-editor.log` |
| 기존 등록 유닛 선택 + 생존 수 | 135 + 58 | `Logs/artifact-final-counts.log` |

합계 1,451개 assertion 통과, 해당 실행은 모두 exit code 0. Unity Test Runner의 NUnit 테스트 개수가 아니다. 아티팩트 검사는 중간 반복 실행에서도 통과했고, 최종 320개에는 카드가 실제 이미지에 렌더링됐는지 검사하는 항목까지 포함한다.

- 신규 컴파일 오류/최종 실행 예외 없음. 전체 스크립트 컴파일에서 기존 팀원 코드 `GameFlowController.cs:305`의 `CS1998` 경고를 확인했으며 수정하지 않았다.
- 초기 테스트 씬 생성, 포커스 복원, EventSystem 선행 파괴, 오프스크린 캡처 문제를 검사 중 발견하고 UI/검사기 범위에서 수정했다. 테스트 종료 과정의 예외도 실패로 기록하도록 실행기를 보완했다.
- 1280×720 및 1920×1080에서 선택/미선택 화면의 한글, 텍스트 넘침, 버튼 경계와 이미지 출력을 확인했다. 캡처는 `Logs/ArtifactRewardValidation/reward-{해상도}-{selected|unselected}.png`에 있다. 기존 HUD 검사 방식처럼 화면 부분만 복제한 별도 Canvas로 캡처하며 원본 컨트롤러는 복제하지 않는다.
- 시작 시 비교한 기존 파일 469개 중 변경은 UI 검사 실행기 `MvpRuntimeHudValidation.cs` 1개뿐이다. 나머지 468개는 내용 해시가 동일하다. 기존 미커밋 작업과 팀원 Core/데이터/씬/공유 폰트를 보존했다.
- 새 Unity 에셋의 `.meta` 포함. 프로젝트의 `.meta` 260개에서 GUID 중복 없음.
- Play Mode 합성 UI 이벤트 검사이며 사람의 키보드/마우스 조작, 실제 Player 빌드/하드웨어 입력, 실제 전투 보상/효과 적용 검사는 아니다. 원격 push, PR, merge를 수행하지 않았다.

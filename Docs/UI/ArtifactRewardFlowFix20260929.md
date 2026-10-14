# 전투 보상 이후 유물 선택 진행 수정 — 2026-09-29

기준: dev `c23f5f2`. 브랜치: `fix/ui-artifact-reward-progress`. PR 대상: `dev`.

## 문제와 변경

`PlayerTeamBuildingIntegration`에서 골드 보상 확인 후 유물 선택창이 열리지 않았다.
최신 `ArtifactManager.SelectAndApplyAsync()`는 `TrySelectReward()`가 성공할 때까지 기다리지만,
기존 UI는 `TestWaitingScript.ChooseResultBtn()`만 호출해 매니저의 대기를 완료하지 못했다.

- `CoreGameLoopUiBinding`: 골드 확인 후 같은 씬의 매니저가 준비한 보상을 UI에 연결한다. 선택·지급 완료 후에만 Core의 기존 완료 입력을 전달한다.
- `ArtifactRewardBinding`: 활성 선택 중에는 `SelectionCandidates`를 재추첨 없이 표시하고 `TrySelectReward()`로 지급을 요청한다. 기존 독립 UI/인터페이스 경로는 유지한다.
- `ArtifactRewardPanel`: 팀 매니저에는 포기 API가 없어 해당 경로에서 선택 없는 확인을 비활성화한다. 다른 UI의 기존 포기 동작은 유지한다.
- `TeamBuildingUiValidation`: 저장된 실제 통합 씬을 그대로 여는 `RunStoredBatch()` 추가. 골드 확인, 후보 일치, 선택창 재활성화, 잘못된 선택 거부, 화면 클릭 전달, 중복 지급 방지, 다음 웨이브 및 보스·최종 결과 진행을 검사한다.

팀 Core/Artifacts/Economy 스크립트, 씬, 프리팹, 데이터 및 폰트는 이번 수정에서 편집하지 않았다.

## 재현과 검증

Unity 6000.3.23f1 격리 프로젝트에서 기존 `CoreGameLoopUiBinding`으로 실행하면
골드 확인 이후 유물창 표시 대기에서 30초 타임아웃이 발생했다.
수정본은 같은 저장 씬의 보상 선택 및 진행 검사 408개를 통과했다.
`MvpRuntimeHudValidation.RunArtifacts`의 독립 유물 UI 회귀 검사 1,229개도 통과했다.
C# 컴파일 오류 0건, 통합 검사의 UI 런타임 오류 0건이다.
전투 결과 진입은 Core의 `ResolveBattleAsync()`로 구동하므로 AI 전투 전체 완주 검사는 아니다.

검증 위치: `C:/Users/User/Documents/ChatGPT/DND Project/UnityUIValidation/RewardFlowFix20260929`.
실행: `Game.UI.Editor.TeamBuildingUiValidation.RunStoredBatch`.
상세 결과: `Logs/TeamBuildingUi20260915/results.txt`.
유물 선택 화면: `Logs/TeamBuildingUi20260915/09-team-artifact-selection.png`.
원인 재현 로그: `reward-flow-before.log`.
최종 통합 로그: `reward-flow-release.log`. 기존 UI 회귀 로그: `artifact-regression.log`.

최신 dev의 비활성 `CurrencyTestPanel._persistent` 타입 불일치 경고와
기존 팀 유닛 랠리 이동 실패 로그는 별도 이슈다. 해당 코드는 수정하지 않았다.

## 수동 확인

1. `Assets/Scenes/UI/PlayerTeamBuildingIntegration.unity`를 열고 Play.
2. 전투 승리 후 골드 보상창의 계속 버튼 클릭.
3. 유물 카드 선택 후 선택하기 클릭. 선택 전에는 확인 버튼이 비활성화된다.
4. 유물 1회 지급 후 다음 노드로 진행하는지 확인. 보스에서도 동일하게 확인.

유물 포기를 허용하려면 매니저 담당자가 공식 포기 완료 API를 제공해야 한다.
UI에서 임의로 지급 완료 상태를 바꾸거나 후보를 다시 추첨하지 않는다.

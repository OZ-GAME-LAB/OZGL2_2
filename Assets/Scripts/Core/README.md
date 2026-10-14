# GameFlowController 연동 가이드

현재 코어는 실제 건물 생산과 유닛 스포너를 연결한다. 테스트 씬은 `Assets/Scenes/Test/Test.unity`이다.
보상 선택과 부착 콘텐츠는 아직 `TestWaitingScript`의 완료 입력을 사용한다.

## 1. 책임과 초기화

- `GameFlowController`: 페이즈, 전투 결과·보상 처리 순서, 분기 진행, 종료와 리셋.
- `NodeController`: 분기별 노드 생성과 현재 위치. GameFlow가 내부 객체로 소유한다.
- `WaveController`: 적 생성 요청, 준비 완료 알림 수신, 실제 생존 수 기반 승패 판정, 런타임 정리 요청.
- `BuildingSpawner`: BattlePreparing 진입 시 건물 설정에 따른 아군 생성 요청.
- `SpawnManager / RuntimeUnitManager`: 실제 생성, 그룹 배치와 준비 상태, 전투 시작 및 유닛 관리.

`BootStrap.Start()`에서 코어·건물·아티팩트·재화·카메라 참조를 연결하고 초기화한 뒤 `BeginRun()`을 호출한다.
`BeginRun()`은 None 또는 Finished이고 전환 중이 아닐 때 런타임과 노드 진행을 초기화하고 1분기 1웨이브의 Preparation으로 진입한다.

## 2. 아군·적 생성과 전투 시작

```text
시작 버튼 → GameFlowController.TrySpawnUnits()
  → Preparation / 전환 잠금 / 현재 프리셋 검사
  → BattlePreparing
    → BuildingSpawner가 SpawnManager.SpawnAllyGroup()으로 아군 요청
    → WaveController.PrepareEnemy(runToken, spawnToken)로 적 요청
      → SpawnManager.SpawnEnemyWaveAsync()

아군·적 생성 완료 + 등록 그룹의 준비 완료
  → RuntimeUnitManager.PreparationCompleted
  → WaveController가 현재 페이즈·토큰·처리 여부 검사
  → GameFlowController.TryStartWave()
  → Battle → WaveController.BattleStart()
  → RuntimeUnitManager.StartBattlePhase()
```

외부 시작 입력은 `TrySpawnUnits()`를 호출한다.
이 메서드의 true는 적 생성 작업이 정상 반환했다는 뜻이며, 모든 유닛의 배치 완료나 Battle 진입을 보장하지 않는다.
전투 진입 여부는 `CurPhase` 또는 `PhaseChanged`로 확인한다. `TryStartWave()`는 WaveController의 준비 완료 전달 경로다.

아군 완료는 실제 SpawnManager가 알린다. 코어에서 미리 `NotifyAllySpawnCompleted()`를 호출하지 않는다.
생성 시간을 흉내 내던 `SpawnTime`, 아군 없는 테스트 옵션, 더미 `TestSpawner`는 제거했다.
현재 적 생성 비용 10과 웨이브 프리셋 데이터는 이번 정리에서 변경하지 않았다.

## 3. 전투 종료와 정리

```text
RuntimeUnitManager.UnitDied
  → WaveController가 GetRemain(out enemy, out allies) 조회
  → 적 0명: Victory / 적이 남고 아군 0명: Defeat
  → GameFlowController.ResolveBattleAsync(result)
```

정상 승패 판정은 실제 유닛 수를 사용한다. 코어는 Battle에서 전환 중이 아닐 때만 결과를 받아 중복 처리를 막는다.
`CleanupBattle()`은 준비 완료 알림 수신을 먼저 무효화하고 `RuntimeUnitManager.ClearRuntime()`으로 유닛·그룹·준비 상태를 정리한다.
그룹 해제 도중 발생하는 준비 완료 알림으로 다시 전투에 들어가지 않도록 페이즈·토큰·한 번 처리 검사를 유지한다.

## 4. 보상과 분기 진행

```text
BattleResolving → 결과 연출 대기
  ├─ 패배 → 전장 정리 → Finished
  └─ 승리
      ├─ 일반 노드 → Reward → 보상 완료 → 전장 정리
      │   → 부착 Event / Store 완료 → 다음 노드 → Preparation
      └─ 보스 → QuarterComplete → Reward → 유물 보상 완료
          → 전장 정리 → 무한분기 Curse 완료 → QuarterComplete
          → 필요 시 종료 / 계속 선택
              ├─ 종료 → Finished
              └─ 계속 또는 선택 생략 → 다음 분기 → Preparation
```

스토리 분기 수는 3이다. 3분기부터 보스 승리 후 종료·계속 선택을 제공하며, 저주는 4분기부터 보스에 붙는다.
`HasClearedMainGame`은 이번 판의 스토리 클리어 기록이며 이후 패배해도 유지하고 새 판·리셋 시 초기화한다.
`AutoContinue`는 종료 선택만 생략하며 보상·저주 대기는 생략하지 않는다.

`ArtifactManager.SelectAndApplyAsync()`는 아직 실제 선택 UI 완료 대기를 구현하지 않았다.
따라서 `WaitArtifactSelection()`은 Reward 진입 전에 토글 대기를 만들고, 아티팩트 요청과 `ChooseResultBtn()` 입력을 기다린다.
UI 샘플도 선택·적용 완료 후 이 입력으로 코어 대기를 해제한다. 실제 선택·적용 완료 계약으로 통합될 때 토글을 제거한다.

부착 콘텐츠는 `ProcessEventAsync()`가 완료된 Node를 받아 처리한다.
None은 즉시 반환하고, Event·Curse는 Event, Shop은 Store에서 기다린다. 이 메서드가 노드를 이동시키지는 않는다.
실제 이벤트 효과·구매·저주 적용은 아직 이 경로에 연결되지 않았다.

## 5. 유지하는 테스트 입력

- Battle Start: 실제 `TrySpawnUnits()` 요청.
- Clear / Fail: 기존 씬의 `WaveController.SetSuccess() / SetFail()` 연결을 유지한다. Battle에서 GameFlow의 결과 처리에 테스트 승패를 직접 전달하며, 가짜 유닛 수나 사망 이벤트를 만들지 않는다.
- ChooseResult: Reward에서는 보상 대기만, Event / Store에서는 현재 부착 콘텐츠 대기만 완료한다. 보상 뒤 이벤트가 붙으면 입력이 한 번 더 필요하다.
- Finish / Continue: `ChooseFinishRun() / ChooseContinueRun()`으로 현재 종료 선택에 응답한다.
- LastWave / LastQuarter: Preparation이고 전환 중이 아닐 때만 이동한다. 클리어·보상을 발생시키지 않으며 같은 분기 마지막 노드로 이동할 때 재추첨하지 않는다.
- Reset: `ResetRun()`으로 이전 진행을 무효화하고 새 판을 요청한다.
- `StagingTime`: 실제 결과 연출 연결 전의 임시 지연이다. GameFlow에서 직접 기다린다.

`TestWaitingScript`, 상태 표시 컴포넌트, 위 입력의 씬 참조는 유지한다.
사용처가 없는 `ResultBtn()`, 시간 대기 래퍼, GameFlow의 빈 연동 예시 메서드는 제거했다.
테스트 전용 메서드는 클래스 하단, 필드는 기존 필드 영역에 둔다.

## 6. 부착 콘텐츠 완료 계약

`TestWaitingScript.WaitPostBattleContentAsync(Node, CancellationToken)`는 요청마다 별도 완료 대상을 만들고,
`CompletePostBattleContent(long requestId)`로 완료한다.
`PostBattleRequestId`, `PendingPostBattleContent`, `IsWaitingForPostBattleContent`는 현재 요청 조회용이다.

지연 콜백은 요청 시작 때 받은 ID를 보관해 전달한다. 오래된 ID·중복 완료·취소 후 입력은 무시한다.
이전 요청 정리가 새 요청을 지우지 않도록 ID를 비교한다.
페이즈 변경 전에 대기를 만들므로 즉시 도착하는 완료 입력도 받을 수 있다.

## 7. 리셋과 남은 생성 계약

`ResetRun()`은 기존 진행 토큰을 취소하고 None으로 전환해 새 전투 진입을 막는다.
진행 중인 적 생성 작업이 반환하면 `BeginRun()`에서 런타임을 비우고 1분기 1웨이브로 돌아간다.
현재 스포너는 중간 취소 시 배치 공간 정리를 건너뛰므로, 리셋에서는 적 생성을 자연 완료까지 기다린다.
GameFlow가 파괴될 때는 적 생성 토큰도 취소한다. 이 처리와 준비 완료 가드는 테스트 우회가 아니라 수명 관리이므로 유지한다.

실제 아군 연동 후 다음 경계는 별도 보완이 필요하다.

- 생산 건물이 없거나 생산 수량이 모두 0이면 아군 완료 알림이 발생하지 않는다. 생산 0개를 허용할지와 해당 배치의 완료 통지 규칙을 정해야 한다.
- 아군 생성은 현재 스포너 파괴 토큰만 사용하고 전체 대기 API가 없다. 적보다 늦은 아군 생성 도중 리셋하면 이전 아군이 리셋 이후 등록될 수 있다. 스포너에 아군·적 전체 생성 완료 대기 또는 취소·정리 계약을 추가한 뒤 리셋에 연결해야 한다.

이번 정리에서는 건물·유닛 생성 로직을 변경하지 않았다.

## 8. 노드 정보와 UI 조회

분기마다 노드 5개를 생성하며 첫 노드는 일반, 마지막은 보스, 중간 정예는 1~2개다.
각 Node는 웨이브 번호, 선택된 WaveSO, 세력, 부착 콘텐츠 종류를 보관한다.
3/4웨이브 중 상점 한 개, 나머지 비보스 세 자리 중 이벤트 한 개, 남은 두 자리에는 독립적으로 10% 이벤트 판정을 한다.
실제 분기 번호는 증가하며 편성 조회만 카탈로그 5분기로 제한한다.

GameFlow는 현재 Node와 읽기 전용 목록을 노출하고 NodeController 자체를 공개하지 않는다.
WaveController의 `CurWave / CurQuarter / CurrentPreset / CurrentFaction / IsLastWave`는 GameFlow 정보에 위임한다.
`WaveChangedInfo`는 분기·웨이브·전투 타입·부착 종류를 함께 전달한다.

UI는 `CurPhase`로 최초 상태를 표시하고 `PhaseChanged`로 갱신한다.
페이즈 알림 시점에 전환 잠금이 아직 유지될 수 있으므로 이벤트 안에서 다음 진행을 연쇄 요청하지 않는다.
건설 입력 가능 여부는 `CanEnterBuildMode()`로 확인하고 비용·배치 규칙은 건물·재화 시스템이 담당한다.

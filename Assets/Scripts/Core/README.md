# GameFlowController 연동 가이드

이 문서는 각 팀원의 시스템을 **어느 메서드에서 호출하고, 어떤 결과를 기다린 뒤 다음 상태로 이동하는지** 설명한다.
현재 구현은 `GameFlowController.cs`와 `WaveController.cs`, 테스트 입력은 `Test/TestWaitingScript.cs`와 WaveController의 테스트 메서드를 기준으로 한다.
테스트 씬은 `Assets/Scenes/Test/Test.unity`이며 버튼 사용법은 실행 후 팁 버튼에서 확인한다.

> 아래의 ‘현재 흐름’은 구현된 코드다. ‘연결 흐름’과 예시 메서드는 연결 방향을 설명하는 초안이며,
> 아직 존재하는 API가 아니다. 실제 메서드명·인자·완료 전달 방식은 담당자와 협의한다.

## 1. 시작과 참조 연결

현재 `BootStrap.Start()`가 `GameFlowController.BeginRun()`을 호출한다.
`BeginRun()`은 현재 상태가 None 또는 Finished이고 전환 중이 아닐 때 웨이브 번호와 테스트 생존 수를 초기화한 뒤 1웨이브의 Preparation으로 진입한다.

팀원 시스템이 추가되면 `BootStrap`에서 필요한 컴포넌트 참조를 연결하고 초기화한 뒤 `BeginRun()`을 호출하는 순서로 확장한다.
생산·웨이브·보상 시스템을 호출할 참조는 코어에 전달하고, 각 시스템 내부에서 사용할 참조는 해당 시스템에 전달한다.
예를 들어 건물의 아군 생산은 BuildingSystem이 UnitSystem에 생성을 요청하며, 개별 생성 요청을 모두 코어로 우회시키지 않는다.

## 2. 전투 시작: UI → 코어 → 아군·적 준비

### 현재 흐름

```text
시작 버튼 → TestWaitingScript.WaveStartBtn()
  → GameFlowController.TryStartWave()
    → Preparation인지, 전환 중이 아닌지 검사
    → _isTransitioning = true
    → ChangePhase(BattlePreparing)
    → await WaveController.PrepareEnemy(token)
      → TestSpawner.SpawnAllUnits(token)   ← 현재는 생존 수 초기화 후 3초 대기
    → 취소되었으면 false 반환
    → ChangePhase(Battle)
    → _isTransitioning = false, true 반환
```

UI 담당은 시작 버튼에서 `TryStartWave()`를 호출한다. 결과가 필요하면 `await`로 받고,
현재 테스트 버튼처럼 결과를 사용하지 않으면 `.Forget()`으로 실행한다.
현재 `UniTask<bool>`의 true는 **시작 요청 접수가 아니라 Battle 진입 완료**를 뜻한다.
false는 잘못된 상태에서의 요청 또는 대기 중 취소를 뜻한다.

### 팀원 코드가 들어갈 위치

`TryStartWave()`는 현재 **`WaveController.PrepareEnemy(token)`으로 적 준비 완료를 기다린다.** 실제 연동 시 WaveController가 호출하는 스포너를 연결하고, 코어에 아군 준비 작업의 대기도 추가한다.
`ChangePhase(BattlePreparing)` 이후 작업을 시작하고, `ChangePhase(Battle)` 이전에 모두 완료되어야 한다.

- 김도현: 건물의 아군 생산 메서드(`ProduceAllies`) 제공. 필요한 아군 생성과 담당하는 등장 준비가 끝나야 완료 처리한다.
- 문규성: WaveController에서 적 편성에 따른 생성·등장 준비를 요청하고 완료를 전달한다.
- 양원준: 건물·웨이브 시스템의 요청에 따라 실제 유닛을 생성한다. 준비 중에는 전투 행동이 시작되지 않도록 하고,
  코어의 Battle 진입에 맞춰 전투를 시작하는 연결을 협의한다.

**다음은 UniTask를 사용하는 연결 예시다. 건물 시스템의 필드명과 메서드명은 설명용이며, `PrepareEnemy`는 현재 메서드다.**

```csharp
// TryStartWave의 적 준비 대기에 아군 준비를 함께 연결하는 예시
var token = _cts.Token;
bool canceled = await UniTask.WhenAll(
    _buildingSystem.ProduceAllies(token),
    _waveController.PrepareEnemy(token)
).SuppressCancellationThrow();

if (canceled || token.IsCancellationRequested) return false;
// 이후 기존 코드가 Battle 전환과 잠금 해제를 담당
```

두 메서드가 `UniTask`를 반환하면 코어가 `WhenAll`로 양쪽 완료를 기다릴 수 있다.
아군이 먼저 끝나도 적 준비가 남아 있으면 대기를 유지한다.
각 담당 메서드는 **생성 요청을 전달한 직후가 아니라 실제 준비가 끝난 뒤** 완료되어야 한다.
반환 전에 내부 작업을 `.Forget()`으로 분리하면 코어가 그 작업의 완료를 기다릴 수 없으므로 이 구간에서는 사용하지 않는다.

## 3. 콜백·이벤트로 완료를 전달하는 경우

현재 코어에는 `OnAlliesReady()` 같은 외부 완료 콜백 메서드가 없다.
팀원 시스템이 UniTask를 사용한다면 위와 같이 반환된 작업을 await하는 방식으로 연결하면 되고, 별도 완료 콜백은 필요 없다.

기존 시스템이 콜백 방식이라면 다음 구조로 연결할 수 있다.

```text
코어가 생성 메서드를 호출하면서 완료 콜백 전달
  → 생성 시스템이 생성·등장 작업 수행
  → 전체 작업이 끝나면 완료 콜백 호출
  → 코어의 연결 코드가 해당 작업의 대기를 완료 처리
  → 아군·적 양쪽 대기 완료 후 코어가 Battle로 변경
```

이때 콜백은 **자신이 맡은 작업 하나의 완료만** 전달한다.
콜백에서 `ChangePhase`, `_curPhase`, `_isTransitioning`을 변경하거나 `TryStartWave()`를 다시 호출하지 않는다.

코어의 연결 코드에서 `UniTaskCompletionSource`를 사용하면 콜백을 await 가능한 작업으로 바꿀 수 있다.
작업마다 새 완료 대상을 만들고, 정상 완료 콜백은 `TrySetResult()`, 취소는 `TrySetCanceled(token)`로 전달하는 방식이다.
실제 연결 시 오류 전달과 콜백 해제도 함께 처리해야 한다. **대기만 취소해도 원래 생성 작업이 자동으로 중단되는 것은 아니다.**

생성 완료를 이벤트로 제공하는 경우에는 **이벤트 구독 → 생성 메서드 호출 → 완료 대기 → 구독 해제** 순서로 연결한다.
작업이 즉시 완료될 수도 있으므로 호출 전에 구독하며, 취소 시에도 구독을 해제한다.
반복 실행되는 이벤트라면 어느 요청의 완료인지 구분해 이전 요청의 이벤트가 새 대기를 끝내지 않게 한다.
이 연결 코드는 코어 담당과 함께 작성하며, 담당 시스템은 정상 완료·취소·오류를 구분해서 전달한다.

## 4. 전투 종료: 유닛 → 웨이브 → 코어

현재 Clear 버튼은 `WaveController.SetSuccess()`, Fail 버튼은 `WaveController.SetFail()`을 호출한다.
두 메서드는 Battle 상태에서만 TestSpawner의 생존 수를 변경하고 인자 없는 `MonsterKilled` 이벤트를 발행한다.
WaveController는 이벤트를 구독하고 `GetRemain(out enemy, out allies)`으로 생존 수를 조회해 전투 결과를 판단한다. 실제 스포너도 이 조회·알림 규약에 맞춰 연결한다.

```text
스포너의 MonsterKilled 이벤트
  → WaveController가 GetRemain으로 생존 수 조회 및 승패 판단
  → 승리: GameFlowController.CompleteWave() → Reward
  → 패배: GameFlowController.FinishedGame(Defeat) → Finished
```

원준 담당 유닛 코드가 사망할 때마다 `CompleteWave()`를 직접 호출하지 않는다.
웨이브 담당이 생성 완료 이후 전멸 여부를 판단하고, 전투 종료 결과를 한 번만 알리는 구조다.
적 전멸은 승리, 아군 전원 사망은 패배이며, `CompleteWave()`는 승리 후 보상과 다음 웨이브 또는 최종 종료를 처리하므로 패배용으로 호출하지 않는다.
마지막 웨이브의 승리도 보상 선택을 먼저 기다린다. 실제 유닛의 사망 알림과 생존 수 관리는 아직 TestSpawner를 사용하는 단계다.

## 5. 보상: 코어 → 보상 시스템 ↔ UI → 코어

### 현재 흐름

```text
CompleteWave()
  → Battle인지, 전환 중이 아닌지 검사
  → _isTransitioning = true
  → ChangePhase(Reward)
  → await WaitToggle(token)           ← 현재는 테스트 버튼 대기
       ChooseResultBtn()이 _toggle = true로 변경하면 대기 완료
  → 취소되었으면 반환
  → 일반 웨이브: ProgressStage()로 번호 증가·WaveChanged 발행
                → ChangePhase(Preparation)
  → 마지막 웨이브: FinishRun(Victory) → ChangePhase(Finished)
  → _isTransitioning = false
```

### 팀원 코드가 들어갈 위치

`CompleteWave()`에서 **`WaitToggle` 호출을 보상 절차의 완료를 기다리는 코드로 교체**한다.
그 뒤, 다음 웨이브 진행 또는 최종 종료를 결정하기 전에 필요한 유닛 정리를 연결한다.

1. 코어가 보상 시스템의 메서드를 호출해 이번 웨이브의 보상 절차를 시작한다.
2. UI 담당은 아이템 선택·포기 입력을 보상 시스템의 메서드에 전달한다.
3. 재희 담당 보상 시스템은 선택한 보상을 적용하거나 포기를 처리한다.
4. 실제 처리가 끝나면 UniTask를 완료하거나 합의한 완료 콜백·이벤트를 전달한다.
5. 코어가 보상 완료를 확인하고, 원준 담당 유닛 정리 메서드를 호출한다. 정리가 비동기라면 완료까지 기다린다.
6. 코어가 다음 상태로 변경한다.

UI의 버튼 클릭만으로 코어의 보상 대기를 끝내지 않는다. **선택 결과가 실제로 적용된 시점**이 보상 완료다.
포기도 정상적인 선택 종료이므로 완료를 전달하고, 리셋은 취소로 구분한다.
현재 테스트는 ChooseResult 버튼으로 보상 대기를 완료한다. 일반 웨이브는 다음 번호의 Preparation으로 복귀하고, 마지막 웨이브는 번호를 증가시키지 않고 Finished로 전환한다.

## 6. 상태 표시와 건설 입력

UI는 `CurPhase`로 최초 상태를 표시하고 `PhaseChanged` 이벤트로 이후 표시를 갱신한다.
WaveText의 `TestWaveViewer`는 `WaveChanged`와 `PhaseChanged`를 구독해 현재 웨이브 번호를 표시하며, Finished에서는 `gameFinished`를 표시한다. 리셋하면 다시 1웨이브를 표시한다.
다음은 GameFlowController 참조가 준비된 UI 컴포넌트 내부의 연결 예시다.

```csharp
// UI 연결 시
_controller.PhaseChanged += RefreshPhase;
RefreshPhase(_controller.CurPhase);

// UI 연결 해제 시
_controller.PhaseChanged -= RefreshPhase;
```

`RefreshPhase(GamePhase phase)`에서 단계별 패널·문구를 갱신한다.
`ChangePhase()`가 상태를 저장한 직후 이벤트를 호출하므로, 이벤트 시점에는 `_isTransitioning`이 아직 true일 수 있다.
따라서 이벤트는 화면 갱신에 사용하고, 이벤트 안에서 다음 상태 전환 메서드를 연쇄 호출하지 않는다.
특히 이벤트 안에서 `CanEnterBuildMode()`를 한 번 조회한 결과만으로 버튼을 고정하면 잠금 해제 후 갱신을 놓칠 수 있다.
입력 가능 표시의 갱신 시점은 실제 UI 연결 시 코어 담당과 맞춘다.

건설 입력에서는 `CanEnterBuildMode()`로 진행 상태상 허용되는지 확인한다.
건설 비용·배치 가능 여부는 BuildingSystem이 검사하고 재화 시스템과 연결한다.
GameFlowController에 가격 계산이나 건물 내부 처리를 추가하지 않는다.

## 7. 비동기 작업 중 리셋

현재 Reset 버튼은 `GameFlowController.ResetRun()`을 호출한다.
`ResetRun()`은 `ClearToken()`으로 기존 대기를 취소·정리하고 새 토큰 소스를 만든 뒤, 잠금을 초기화하고 None으로 전환한다.
이후 `BeginRun()`에서 웨이브 번호와 테스트 생존 수를 초기화하고 1웨이브의 Preparation으로 복귀한다. 컨트롤러 파괴 시에도 기존 토큰을 취소한다.

팀원 비동기 메서드는 코어가 전달한 `CancellationToken`을 내부 대기·생성 작업에 전달한다.
취소 시 정상 완료로 반환하지 않고 취소 결과를 유지해야 코어의 `SuppressCancellationThrow()`가 취소를 확인할 수 있다.
이 메서드는 취소를 bool로 받게 하는 기능이며, 일반 오류까지 처리하지는 않는다.

취소된 이전 작업은 상태와 잠금을 변경하지 않고 종료한다.
특히 이전 작업의 콜백이나 `finally`에서 `_isTransitioning = false`를 실행하면 리셋 후 새 작업의 잠금까지 풀 수 있다.
각 시스템은 자기 작업의 이벤트 구독·생성 오브젝트 등을 정리하고, 코어 상태 초기화는 코어에 맡긴다.

연결 전 담당자와 확정할 항목은 **호출 메서드와 인자, 완료 시점, UniTask 또는 콜백·이벤트 방식,
취소 시 중단·정리할 대상, 오류 전달 방식**이다.
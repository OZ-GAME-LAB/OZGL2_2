# 아웃게임 설정 테스트 사용법과 코드 설명

## 1. 실행 방법

1. `Assets/Scenes/Test/OutGameSetupTest.unity`를 엽니다.
2. Play를 누르면 풍요의 제단이 선택된 제단 화면에서 시작합니다.
3. `특성`에서 혈석을 사용해 특성 레벨을 올린 뒤 `제단`으로 돌아옵니다.
4. `다음`으로 토템 화면을 엽니다. 토템을 좌클릭하면 +1, 우클릭하면 -1입니다.
5. `시작`을 누르면 Console에 제단·특성·토템·누적 보너스가 출력됩니다.

이번 테스트는 실제 인게임으로 이동하거나 전투 수치를 적용하지 않습니다. 특성·토템을 하나도 선택하지 않아도 시작할 수 있고, 시작 후에도 토템 화면에서 계속 설정을 바꿀 수 있습니다.

실행 중 화면을 이동해도 상태는 유지됩니다. 특성을 구매하면 특성 레벨과 혈석 잔액을 하나의 JSON 파일에 저장하고, Play를 다시 실행하면 복원합니다. 제단·토템 선택은 매번 초기화합니다. 실제 영구재화 매니저와는 연결하지 않고 테스트 지갑을 사용합니다.

## 2. 전체 흐름

```text
OutGameBootstrap.Start
  └─ Initialize
      ├─ UI Controller의 이전 이벤트 연결 해제
      ├─ TestWallet.Initialize                  혈석 초기화
      ├─ Altar/Trait/Totem Selector.Initialize  선택 상태 초기화
      ├─ PersistentSaveCoordinator.Initialize  저장 매니저와 Provider 연결
      ├─ TryLoadOrCreate                       특성·혈석 복원, 최초 실행만 기본값 저장
      ├─ OutGameController.Initialize          컨텐츠 참조 연결
      └─ OutGameUIController.Initialize        UI 초기화·이벤트 연결 후 제단 화면 표시

UI의 선택 요청
  → OutGameUIController
  → 해당 Selector의 Try 메서드
  → 상태 변경에 성공하면 Changed
  → OutGameUIController
  → View.Refresh

특성 구매 요청
  → OutGameUIController
  → ITraitProgression.TryUpgrade             TraitSelector가 구매 전체를 처리
  → 구매 조건 검사, 특성·혈석의 구매 후 사본 작성
  → IPersistentSaveWriter.TrySave(data)      Coordinator에는 완성된 데이터만 전달
  → SaveManager.TrySave                     두 영역을 같은 파일에 저장
  → TraitSelector가 저장 성공 시에만 두 Provider에 반영
  → Changed                                 두 상태가 모두 반영된 뒤 UI 갱신

시작 버튼
  → OutGameController.StartRun
  → CreateStartContext
  → 각 Selector의 상태를 새로운 목록으로 복사
  → Debug.Log
```

선택 상태의 원본은 Selector입니다. View는 보여 줄 항목을 기억하고 그리는 역할입니다. Controller는 제단·특성·토템 상태를 매번 중복해서 저장하지 않고, 시작 버튼을 누른 순간에만 묶습니다.

## 3. 스크립트별 책임

### 초기화와 코어

| 스크립트 | 책임과 주요 메서드 |
|---|---|
| `OutGameBootstrap` | 지갑, Selector 3개, Controller, UI Controller, SaveManager, PersistentSaveCoordinator를 Inspector로 받습니다. 기본 초기화 후 특성·혈석을 복원하고 마지막에 UI를 연결합니다. 재호출해도 저장된 영구 상태가 유지됩니다. |
| `PersistentSaveCoordinator` | `CaptureSaveData`, `TrySave`, `TryLoad`, `TryLoadOrCreate`로 데이터를 수집·검증·저장·복원합니다. 업그레이드나 비용 차감은 처리하지 않습니다. |
| `PersistentSaveData` | 특성 Provider의 `TraitSaveData`와 지갑 Provider의 `PersistentWalletSaveData`를 묶는 저장용 클래스입니다. |
| `OutGameController` | 선택 결과를 모아 게임 시작을 처리합니다. `CreateStartContext`는 데이터 복사, `StartRun`은 컨텍스트를 보관하고 로그를 출력합니다. UI나 패널 참조를 가지지 않습니다. |
| `OutGameUIController` | View 초기화, 이벤트 연결, 최초 화면 표시와 화면 이동을 담당합니다. `ShowAltar`, `ShowTraits`, `ShowTotems`에서 해당 View의 GameObject를 표시합니다. `Shutdown`에서 자신이 연결한 이벤트를 해제합니다. |
| `OutGameTestWallet` | Inspector의 초기 혈석으로 `Balance`를 설정하고 저장이 있으면 복원합니다. `ICurrencySpender`와 `ISaveDataProvider<PersistentWalletSaveData>`를 구현하며, 다른 종류의 재화는 지원하지 않습니다. |

Bootstrap은 연결 순서, Controller는 시작 처리, UIController는 UI 초기화·이벤트 연결·화면 이동을 담당합니다. 구매 조건이나 토템 보너스 계산은 Selector에 있습니다. 개별 View는 다른 화면을 참조하지 않습니다.

기존 `OutGameUIBinding`은 `OutGameUIController`로 이름을 변경했습니다. 스크립트의 meta GUID를 유지했고 Bootstrap의 필드에는 `FormerlySerializedAs("_uiBinding")`을 적용해 기존 Inspector 연결을 이어받습니다. 화면 이동용 클래스를 추가하지 않고 기존 UI 클래스에 책임을 모았습니다.

### 컨텐츠와 인터페이스

| 스크립트 | 책임 |
|---|---|
| `OutGameAltarSelector` | `AltarCatalog`에서 목록을 받고 실제 선택 제단을 관리합니다. `IsUnlocked`, `GetUnlockCondition`으로 표시 정보를 제공하고 `TrySelect`로 선택합니다. |
| `OutGameTraitSelector` | 특성 SO 목록과 현재 레벨을 관리합니다. `CanUpgrade`가 저장 준비·선행 특성·최대 레벨·비용을 검사합니다. `TryUpgrade`는 구매 후 사본을 저장하도록 요청하고, 성공 시 혈석과 특성 레벨을 함께 반영합니다. |
| `OutGameTotemSelector` | 토템 SO 목록과 현재 레벨을 관리합니다. `TryChangeLevel`로 증감하고, `GetRewardBonusPercent`로 레벨 합 × 20을 반환합니다. |
| `Contracts/OutGameContracts` | `IAltarSelection`, `ITraitProgression`, `ITotemSelection`과 시작 컨텍스트의 순수 데이터 형식을 정의합니다. |
| `Contracts/PersistentSaveContracts` | `IPersistentSaveWriter`는 전체 사본 조회·저장 요청, `IPersistentWallet`은 재화 확인·저장 데이터 복원·변경 알림을 제공합니다. Selector가 저장 구현이나 테스트 지갑의 구체 클래스에 의존하지 않도록 합니다. |

인터페이스는 UI에서 필요한 조회와 요청만 드러냅니다. 다른 담당자가 컨텐츠 구현을 교체할 때 같은 인터페이스를 유지하면 UI의 구매·선택 요청 구조를 유지할 수 있습니다. `IReadOnlyList`는 UI가 목록에 임의로 항목을 추가하거나 제거하지 않도록 사용했습니다.

`TrySelect`의 `true`는 선택 가능한 제단이라는 뜻입니다. 이미 선택한 제단을 다시 눌러도 `true`지만 변경 이벤트는 발생하지 않습니다. `TryUpgrade`와 `TryChangeLevel`의 `true`는 실제 레벨 변경 성공입니다. 잠긴 제단 클릭, 토템의 최소·최대 레벨에서 추가 증감 같은 경우는 `false`가 됩니다. UI는 실패하더라도 해당 항목의 설명을 보여 줄 수 있습니다.

### 화면 표시와 버튼

| 스크립트 | 책임 |
|---|---|
| `OutGameAltarView` | 제단 버튼의 이름·색상, 상세 설명, 이동 버튼을 관리합니다. `Inspect`로 잠긴 제단의 설명도 표시합니다. |
| `OutGameTraitView` | 특성 노드, 선택 특성 설명·레벨, 잔액, 구매 버튼을 표시합니다. 노드 클릭은 정보 조회이고 구매 버튼 클릭이 업그레이드 요청입니다. |
| `OutGameTotemView` | 두 페이지의 토템 배치, 페이지 이동, 상세 설명, 누적 보너스, 이전·시작 요청을 관리합니다. |
| `OutGameChoiceButton` | 제단·특성 버튼에서 공통으로 사용하는 클릭 전달과 이름·배경색 표시입니다. 컨텐츠의 구매 조건은 모릅니다. |
| `OutGameTotemColumn` | 토템 한 열의 단계들을 아래에서 위 순서로 묶어 표시합니다. |
| `OutGameTotemStepButton` | 좌클릭·우클릭을 구분해 증감 요청을 전달하고 단계 색상과 라벨을 표시합니다. |

버튼은 부모를 검색해 Selector를 찾지 않습니다. Bootstrap과 UIController에서 전달받은 연결을 사용합니다. 토템은 `Button.onClick`과 포인터 클릭에서 동일한 요청을 두 번 보내지 않도록 포인터 클릭 경로만 사용합니다.

View와 버튼의 `Shutdown`은 코드에서 연결한 리스너만 해제합니다. 화면을 숨겼다가 다시 보이는 동작은 상태를 초기화하지 않습니다.

## 4. 화면 동작과 테스트 데이터

### 제단

| 제단 ID | 초기 동작 |
|---|---|
| `Abundance` (11000) | 풍요의 제단. 처음부터 선택되어 있으며 SO의 설명을 표시합니다. |
| `Conquest` (11001) | 잠김. `5분기 보스웨이브 클리어`를 표시합니다. |
| `Arcane` (11003) | 잠김. `스킬피해로 적 500마리 누적 처치`를 표시합니다. |
| `Guardian` (11002) | 잠김. `무한모드 1분기 클리어`를 표시합니다. |

잠긴 제단을 눌러도 `SelectedAltar`는 풍요로 유지됩니다. 해금 조건 문구는 테스트용이며 실제 달성 여부는 판정하지 않습니다. 기존 다섯 번째 버튼 슬롯은 숨깁니다. 첫 화면의 `이전` 버튼은 비활성화합니다.

### 특성

혈석은 새 저장 파일에서 기본 500입니다. Hierarchy의 `OutGameSetup/Systems`를 선택하고 `OutGameTestWallet`의 `Initial Bloodstone`을 수정하면 새 저장 파일 생성 시 적용됩니다. 기존 저장 파일이 있으면 그 잔액을 우선 복원합니다.

| 특성 | 선행 특성 | 레벨당 혈석 |
|---|---|---:|
| 든든한 밑천 (`StartingGold`) | 없음 | 100 |
| 풍요로운 생산 (`Production`) | StartingGold 1레벨 | 150 |
| 승리의 보수 (`WaveReward`) | StartingGold 1레벨 | 150 |
| 사냥꾼의 몫 (`KillGold`) | WaveReward 1레벨 | 150 |
| 강인한 체력 (`MaxHealth`) | 없음 | 200 |
| 견고한 방어 (`Defense`) | MaxHealth 1레벨 | 150 |
| 단련된 무기 (`AttackPower`) | 없음 | 200 |
| 신속한 손놀림 (`AttackSpeed`) | AttackPower 1레벨 | 200 |
| 영원한 유산 (`BloodstoneReward`) | AttackPower 1레벨 | 250 |

선행 관계는 각 `TraitData` SO의 `Prerequisite` 필드에서 설정합니다. 비어 있으면 선행 조건이 없고, 다른 특성 SO를 연결하면 해당 특성 1레벨이 필요합니다. 위 표의 기존 관계는 SO 9개에 반영했습니다. 구매 가능 여부와 저장 데이터의 선행 조건 검증은 모두 이 SO 참조를 읽습니다. 자기 참조나 순환 관계 검증은 구현하지 않았습니다.

현재 SO의 최대 레벨은 모두 5입니다. 비용은 매 레벨 고정이며 SO 값을 그대로 읽습니다. 설명의 효과 수치는 `ValuePerLevel × 현재 레벨`로 표시하지만 실제 전투·경제 시스템에 적용하지 않습니다.

- 레벨 0: 회색. 레벨 1 이상: 옅은 파란색.
- 구매 가능: 레벨 상승 버튼 활성화.
- 잔액 부족 또는 선행 조건 미충족: 구매 버튼 비활성화, 비용 부분 빨간색, 이유 표시.
- 최대 레벨: 버튼 비활성화, 비용 대신 `최대 레벨` 표시.

### 토템

첫 페이지 열 순서는 격노, 메아리, 군세, 취약, 교차사격, 궁핍입니다. 두 번째 페이지는 단일 토템이 있는 2·5열에 잔향과 추격을 표시합니다.

- 일반 토템 4종: 최대 3레벨.
- 토글 토템 4종: 0이면 비활성, 1이면 활성.
- 좌클릭 +1, 우클릭 -1. 범위 끝에서는 더 변경하지 않습니다.
- 일반 토템의 선택 단계는 아래에서 위로 붉게 채워집니다.
- 페이지나 화면을 이동해도 선택 레벨은 유지합니다.
- 모든 선택 레벨의 합에 20%를 곱합니다. 3레벨 + 1레벨은 80%, 전체 최대는 320%입니다.

이 보너스는 임시 테스트 규칙입니다. `TotemData.RewardBonusPerLevel`은 이번 계산에서 사용하지 않습니다.

## 5. 시작 컨텍스트

```csharp
public class OutGameStartContext
{
    public AltarId SelectedAltar;
    public List<TraitLevelEntry> Traits;
    public List<TotemLevelEntry> Totems;
    public int RewardBonusPercent;
}
```

각 Entry는 ID와 Level만 담습니다. SO, GameObject, UI 참조는 담지 않습니다. 특성 9개와 토템 8개를 레벨 0인 항목까지 모두 복사합니다.

`CreateStartContext()`는 매번 새로운 객체·목록·Entry를 만듭니다. 예를 들어 시작 로그를 남긴 뒤 격노 토템을 1에서 2로 올려도 이전 컨텍스트의 값은 1입니다. 다시 시작을 누르면 새 컨텍스트가 만들어집니다. `LastStartContext`에서 가장 최근 결과를 코드로 확인할 수 있습니다.

로그는 다음 형식입니다. 실제 출력에는 모든 특성과 토템이 포함됩니다.

```text
[OutGame] 게임 시작 컨텍스트
제단: 풍요의 제단 / Abundance (11000)
특성 (레벨 0 포함):
  든든한 밑천 / StartingGold (12000): Lv.1
  ...
토템 (레벨 0 포함):
  격노의 토템 / EnemyDamage (10000): Lv.3
  ...
누적 보너스: 80%
```

## 6. 기존 코드와 프리팹 재사용

- 기존 Core의 `OutGameBootstrap`, `OutGameAltarSelector`, `OutGameTraitSelector`, `OutGameTotemSelector`에 구현을 채웠습니다.
- `AltarData`, `AltarCatalog`, `TotemData`, `TotemSelectionMode`, `OutGameIds`는 그대로 사용합니다. `TraitData`에는 선행 특성 SO 참조를 추가하고 기존 특성 에셋에 관계를 연결했습니다.
- 기존 SO 21개는 표시 정보의 원본입니다. 현재 레벨·선택 여부는 SO에 기록하지 않습니다.
- 테스트 지갑은 `ICurrencySpender`와 저장 Provider를 포함하는 `IPersistentWallet`을 구현합니다. 특성 Selector는 이 인터페이스를 통해 비용을 확인하고, 저장 성공 후 지갑 사본을 반영합니다.
- 기존 제단·특성·토템 패널, 버튼, 화살표는 새 기능용 프리팹 안의 중첩 프리팹으로 재사용합니다. 기존 와이어프레임 프리팹과 `OutGameTest` 씬은 유지합니다.
- 기존 `SaveManager`와 `SaveKey<T>`를 JSON 저장/로드에 재사용합니다. 검증에서 확인한 Windows의 간헐적 파일 교체 오류를 처리하도록 `SaveManager`의 파일 교체 부분에만 제한 재시도를 추가했습니다. 인게임 `BootStrap`, 실제 `PersistentCurrencyManager`, 인게임 의존성이 있는 `AltarManager`는 연결하지 않습니다.

## 7. 특성·혈석 저장 테스트

저장 위치는 `Application.persistentDataPath/Saves/outgame-test-persistent.json`입니다. 기존 `outgame-test-profile` 테스트 파일과 구분합니다. JSON에는 저장 키·버전·타입 정보와 `Data.Traits._levels`, `Data.Wallet.Bloodstone`이 들어갑니다.

1. `OutGameSetupTest` 씬을 실행하고 든든한 밑천을 한 번 구매합니다.
2. 특성 1레벨, 혈석 400이 됩니다. 이 순간 파일 저장도 완료됩니다.
3. Play를 종료하고 다시 실행합니다. 특성 1레벨과 혈석 400이 복원됩니다.
4. Play 중 Systems의 `PersistentSaveCoordinator` 컴포넌트 메뉴에서 `Persistent Save > Save Traits And Bloodstone` 또는 `Load Traits And Bloodstone`을 실행할 수도 있습니다. 완료 로그에 실제 파일 경로가 표시됩니다.

`CaptureSaveData`는 원본 리스트뿐 아니라 내부 항목까지 복사합니다. 복원은 ID로 대응하므로 저장 목록 순서가 바뀌어도 정상이며, 저장에 없는 신규 특성은 0레벨로 처리합니다. 미등록/중복 ID, null 데이터, 비어 있는 특성 목록, 범위 밖 레벨, 선행 특성이 없는 상태, 음수 혈석은 거부합니다. 두 영역을 모두 검증하기 전에는 어느 쪽도 복원하지 않습니다.

특성·지갑 DTO에도 각각 `Version = 1`을 기록합니다. Unity의 `JsonUtility`가 JSON의 `null` 영역을 빈 객체로 만드는 경우가 있어, 정상 생성된 영역인지 구분하는 값입니다. 기본 생성자의 버전은 0이며 실제 데이터 생성자에서만 1을 설정합니다. 직접 DTO를 만든다면 `new TraitSaveData(levels)`와 `new PersistentWalletSaveData(bloodstone)`을 사용합니다. 정상 캡처에는 0레벨을 포함한 특성 9개가 모두 들어갑니다.

구매는 `OutGameTraitSelector.TryUpgrade`로 요청합니다. `TryUpgrade(id)`와 오류 원인을 받는 `TryUpgrade(id, out error)` 모두 같은 저장 경로를 거칩니다. UI는 `ITraitProgression`만 알고 Coordinator를 직접 호출하지 않습니다.

Selector는 전체 저장 사본에서 특성과 혈석 영역만 변경하고 `IPersistentSaveWriter.TrySave(data, out error)`에 전달합니다. Coordinator의 이 메서드는 파일만 저장하며 실제 상태나 변경 이벤트를 건드리지 않습니다. 저장이 성공하면 Selector가 특성·혈석을 함께 반영합니다. 별도의 `TrySpend`를 다시 호출하지 않으므로 중복 차감하지 않습니다. 지갑의 `TrySpend` 자체는 여전히 메모리만 변경하는 일반 재화 API입니다.

수동 `TrySave(out error)`는 현재 Provider 상태를 수집해 저장합니다. `TryLoad`는 검증한 저장 데이터를 복원합니다. 새 영구 콘텐츠를 추가할 때 저장 영역의 수집·검증·복원 연결은 추가해야 하지만, Coordinator에 콘텐츠별 구매·해금 메서드를 추가하지 않습니다.

저장이 실패하면 구매 전 상태를 유지하고 경고를 출력합니다. 기존 파일이 손상되었거나 버전/내용 검증에 실패하면 Bootstrap 초기화를 중단하고 원본 파일을 보존합니다. 로드 실패를 새 사용자로 취급해 초기값으로 덮어쓰지 않습니다. 시작 중 복원에 실패했다면 파일을 복구한 뒤 Play를 다시 시작합니다. 이미 UI까지 초기화된 상태에서 수동 불러오기가 실패한 경우에는 파일 복구 후 Coordinator의 불러오기를 재시도할 수 있습니다.

`TryLoadOrCreate`는 초기화용 메서드이고, 일반 불러오기에는 `TryLoad`를 사용합니다. `Changed`는 UI 갱신에만 연결되어 있어 불러오기가 다시 자동 저장으로 이어지지 않습니다.

파일 교체 중 Windows 공유/잠금 오류(32, 33, 1175)가 나면 같은 임시 파일로 25ms, 50ms, 100ms 후 재시도합니다. 구매 자체는 반복하지 않습니다. 최대 175ms의 추가 대기가 생길 수 있으며, 계속 실패하면 구매를 반영하지 않고 실패를 반환합니다. 그 외 파일 오류는 바로 실패 처리하고 원본을 먼저 삭제하는 방식은 사용하지 않습니다.

## 8. 나중에 연결할 부분

### 실제 영구재화·저장

실제 영구재화 서비스에 `IPersistentWallet` 구현 또는 어댑터를 마련해 특성 Selector에 주입하고, Coordinator의 지갑 Provider와 View의 잔액 표시/변경 이벤트도 실제 지갑에 연결해야 합니다. 구매 중 지갑 사본 작성과 성공 후 반영은 TraitSelector가 담당합니다. 저장 DTO가 바뀌면 이 부분도 새 지갑 데이터에 맞춥니다.

제단 해금 등 영구 상태가 늘어나면 해당 Provider와 DTO를 추가하고 `PersistentSaveData`에 포함합니다. 저장 스키마 변경 시 기존 데이터 이전 정책과 `SaveKey.Version`을 함께 결정해야 합니다. 특성과 차감된 혈석을 같은 파일에 저장하는 원칙은 유지합니다.

### 실제 제단 해금

`OutGameAltarSelector.IsUnlocked`와 `GetUnlockCondition`의 테스트 규칙을 해금 상태 서비스 및 조건 데이터로 교체합니다. 잠긴 항목을 조회하는 기능과 실제 선택을 판정하는 기능은 그대로 사용할 수 있습니다.

### 실제 인게임 시작

`OutGameController.StartRun`에서 만들어진 컨텍스트를 런 시작 데이터로 전달하고, 씬 전환 후 인게임 Bootstrap에서 ID로 SO를 조회해 각 매니저에 적용하도록 연결하면 됩니다. 현재 로그 출력 위치가 전달 지점입니다. 상태 목록을 새로 복사하는 방식은 그대로 유지하는 편이 안전합니다.

### UI 항목 추가

이 테스트 UI는 고정 슬롯 방식입니다. SO를 추가하는 것만으로 버튼이 자동 생성되지는 않습니다. Selector의 SO 목록과 해당 View의 슬롯/ID를 함께 연결해야 합니다. 기존 버튼 프리팹을 재사용할 수 있으며, 이후 목록이 커질 때 동적 목록 방식으로 바꾸더라도 Selector 인터페이스는 유지할 수 있습니다.

## 9. 검증 기록

구매 책임을 TraitSelector로 옮긴 뒤 전체 프로젝트의 런타임·에디터 코드를 컴파일했습니다. Unity 6000.3.23f1에서 저장 검증 156개, 기존 UI 및 복원 회귀 검증 714개 assertion을 통과했습니다. 저장 검증에는 공통 TrySave가 파일만 저장하고 상태·이벤트를 변경하지 않는지, 잘못된 JSON 영역·레벨·버전, 구매 실패 시 상태 보존, 짧은 파일 잠금의 재시도와 지속 잠금의 실패 처리가 포함됩니다. UI 검증에는 참조·클릭 판정·글리프·레이아웃 검사도 포함됩니다.

저장된 테스트 씬을 Unity 6000.3.23f1에서 다시 열고 초기화한 후, `GraphicRaycaster`로 버튼이 클릭 가능한지 확인하고 실제 uGUI 포인터 클릭 이벤트를 전달하는 방식으로 다음을 검증했습니다.

- 처음 제단만 표시, 풍요 자동 선택, 특성 9개·토템 8개가 모두 0레벨.
- 잠긴 제단 3개의 조건 표시와 실제 선택 유지.
- 특성 선행 조건, 구매 1회당 차감·상승 1회, 정확한 금액, 잔액 부족, 최대 레벨.
- 구매 불가 비용의 빨간색, 활성 특성의 파란색, 현재/최대 레벨 표시.
- 토템 좌클릭·우클릭, 일반/토글 상한과 하한, 페이지와 화면 이동 후 상태 유지.
- 보너스 0%·80%·320%, 시작 후 토템 화면 유지.
- 특성·토템 항목까지 복사되어 이후 선택 변경에 영향을 받지 않는 컨텍스트.
- 재초기화 시 저장된 잔액·특성 레벨 복원과 이벤트 중복 방지.
- 코어만 초기화할 때 현재 화면이 유지되고 UIController에서 화면을 전환하는지 확인.
- 여러 화면 상태에서 한국어 글리프와 텍스트 넘침 확인.

이 검증은 에디터에서 실행한 기능 검증입니다. 마우스로 실제 Play 화면을 조작한 수동 검증과 별도 플레이어 빌드는 수행하지 않았습니다.

결과는 프로젝트의 `.utmp/OutGameSetupValidation/report.txt`, 상세 로그는 같은 폴더의 `unity-validation.log`, 화면 이미지는 `01-altar.png`부터 `08-trait-max.png`까지입니다. `.utmp`는 Git에 포함되지 않는 로컬 검증 결과 폴더입니다.

선행 조건을 SO로 옮기는 작업에서는 특성 에셋 9개에 `_prerequisite` 필드만 추가했고 기존 필드와 메타 GUID를 유지했습니다. 씬·프리팹과 기존 사용자 변경사항은 보존했습니다. 이관 후 저장 156개, UI 714개 검증을 다시 통과했습니다. 자기 참조·순환 관계 검증이나 관련 테스트는 추가하지 않았습니다.

### 에디터 보조 스크립트

`Editor` 폴더의 다음 스크립트는 게임 실행용 코드가 아닙니다. 이미 만들어진 테스트 씬을 실행하려면 이 메뉴를 사용할 필요가 없습니다.

| 스크립트 | 역할 |
|---|---|
| `OutGameSetupTestBuilder` | Unity Editor API로 기존 패널 프리팹을 중첩하고 필요한 컴포넌트·참조를 연결해 루트 프리팹, 씬, 카탈로그를 저장합니다. 기존 결과물이 있으면 덮어쓰지 않습니다. |
| `OutGameSetupTestValidation` | `Tools > OutGame > Validate Setup Test` 메뉴에서 저장된 씬을 검사합니다. 검사 상태를 씬이나 프리팹에 저장하지 않으며, 보고서와 화면 이미지는 `.utmp/OutGameSetupValidation`에 출력합니다. |
| `PersistentSaveValidation` | `Tools > OutGame > Validate Persistent Save` 메뉴에서 저장·복원·구매 실패·잘못된 데이터와 파일 보존을 검사합니다. 보고서는 `.utmp/PersistentSaveValidation/report.txt`에 출력합니다. |

두 검증은 `.utmp` 아래 별도 저장 경로를 사용하며 실제 플레이 저장 파일을 변경하지 않습니다.

씬은 `Canvas` 루트 프리팹 인스턴스, `EventSystem`과 Input System UI 입력 모듈, `Main Camera`로 구성됩니다. 런타임 UI 생성 코드는 없습니다.

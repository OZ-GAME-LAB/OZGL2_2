// Current date KDH 2026-09-08
// 경제 담당이 이어받을 임시 지갑입니다.
// 건물 쪽은 CanAfford / TrySpend만 호출하고, 잔액을 직접 깎지 않습니다.
// 배열 인덱스로 enum을 써서, 클릭 때마다 Dictionary를 만들지 않습니다.
// 재화 담당자의 코드로 교체할 것임.
using System;
using UnityEngine;

namespace OZGL.KDH
{
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private int startingGold = 100;
        [SerializeField] private int startingGem;

        private int[] _amounts;
        private static int _typeCount = -1;

        public event Action<BuildingResourceType, int> Changed;

        private void Awake()
        {
            EnsureAmounts();
            _amounts[(int)BuildingResourceType.Gold] = Mathf.Max(0, startingGold);
            _amounts[(int)BuildingResourceType.Gem] = Mathf.Max(0, startingGem);
        }

        public int Get(BuildingResourceType type)
        {
            EnsureAmounts();
            int index = (int)type;
            if (!IsValidIndex(index))
            {
                Debug.LogWarning($"[PlayerWallet] 알 수 없는 ResourceType입니다: {type}", this);
                return 0;
            }

            return _amounts[index];
        }

        public void Add(BuildingResourceType type, int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[PlayerWallet] Add에 음수가 들어왔습니다: {amount}. type: {type}", this);
                return;
            }

            if (amount == 0)
                return;

            EnsureAmounts();
            int index = (int)type;
            if (!IsValidIndex(index))
            {
                Debug.LogWarning($"[PlayerWallet] 알 수 없는 ResourceType입니다: {type}", this);
                return;
            }

            _amounts[index] += amount;
            Changed?.Invoke(type, _amounts[index]);
        }

        // UI가 버튼 활성만 확인할 때는 부족해도 경고를 내지 않습니다. (매 새로고침마다 콘솔이 채워지지 않게)
        public bool CanAfford(BuildingResourceCost[] costs)
        {
            EnsureAmounts();

            if (costs == null || costs.Length == 0)
                return true;

            for (int i = 0; i < costs.Length; i++)
            {
                int need = costs[i].amount;
                if (need <= 0)
                    continue;

                int index = (int)costs[i].type;
                if (!IsValidIndex(index))
                {
                    Debug.LogWarning($"[PlayerWallet] 알 수 없는 ResourceType입니다: {costs[i].type}", this);
                    return false;
                }

                if (_amounts[index] < need)
                    return false;
            }

            return true;
        }

        // 부족하면 하나도 깎지 않습니다. 건설 실패 후 돈만 사라지는 일을 막기 위함입니다.
        public bool TrySpend(BuildingResourceCost[] costs)
        {
            if (!CanAfford(costs))
            {
                Debug.LogWarning("[PlayerWallet] 재화가 부족해 차감하지 않았습니다.", this);
                return false;
            }

            if (costs == null || costs.Length == 0)
                return true;

            for (int i = 0; i < costs.Length; i++)
            {
                int need = costs[i].amount;
                if (need <= 0)
                    continue;

                int index = (int)costs[i].type;
                _amounts[index] -= need;
                Changed?.Invoke(costs[i].type, _amounts[index]);
            }

            return true;
        }

        // Current date KDH 2026-09-09
        // 철거 환불. 비율은 내림해서 재화가 늘어나지 않게 합니다.
        public void Refund(BuildingResourceCost[] costs, float rate)
        {
            if (costs == null || costs.Length == 0)
                return;

            rate = Mathf.Clamp01(rate);
            for (int i = 0; i < costs.Length; i++)
            {
                int refund = RefundAmount(costs[i].amount, rate);
                if (refund <= 0)
                    continue;

                Add(costs[i].type, refund);
            }
        }

        // 교체 시 (새 비용 - 옛 환불)만 검사합니다. UI 새로고침에서 경고를 내지 않습니다.
        public bool CanAffordNet(BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            EnsureAmounts();
            creditRate = Mathf.Clamp01(creditRate);

            int count = TypeCount;
            for (int i = 0; i < count; i++)
            {
                int net = GetNetAmount((BuildingResourceType)i, pay, credit, creditRate);
                if (net > 0 && _amounts[i] < net)
                    return false;
            }

            return true;
        }

        // 부족하면 지갑을 하나도 바꾸지 않습니다.
        public bool TrySettleNet(BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            if (!CanAffordNet(pay, credit, creditRate))
            {
                Debug.LogWarning("[PlayerWallet] 교체 차액이 부족해 정산하지 않았습니다.", this);
                return false;
            }

            EnsureAmounts();
            creditRate = Mathf.Clamp01(creditRate);

            int count = TypeCount;
            for (int i = 0; i < count; i++)
            {
                int net = GetNetAmount((BuildingResourceType)i, pay, credit, creditRate);
                if (net == 0)
                    continue;

                _amounts[i] -= net;
                Changed?.Invoke((BuildingResourceType)i, _amounts[i]);
            }

            return true;
        }

        public int GetNetAmount(BuildingResourceType type, BuildingResourceCost[] pay, BuildingResourceCost[] credit, float creditRate)
        {
            int payAmount = SumByType(pay, type);
            int creditAmount = RefundAmount(SumByType(credit, type), Mathf.Clamp01(creditRate));
            return payAmount - creditAmount;
        }

        public static int RefundAmount(int amount, float rate)
        {
            if (amount <= 0)
                return 0;

            rate = Mathf.Clamp01(rate);
            return Mathf.FloorToInt(amount * rate);
        }

        private static int SumByType(BuildingResourceCost[] costs, BuildingResourceType type)
        {
            if (costs == null)
                return 0;

            int sum = 0;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].type != type)
                    continue;

                if (costs[i].amount > 0)
                    sum += costs[i].amount;
            }

            return sum;
        }

        private void EnsureAmounts()
        {
            int count = TypeCount;
            if (_amounts != null && _amounts.Length == count)
                return;

            _amounts = new int[count];
        }

        private static int TypeCount
        {
            get
            {
                // Enum.GetNames는 할당이 나므로 Awake/최초 1번만 호출합니다.
                if (_typeCount < 0)
                    _typeCount = Enum.GetNames(typeof(BuildingResourceType)).Length;
                return _typeCount;
            }
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && _amounts != null && index < _amounts.Length;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (startingGold < 0)
                startingGold = 0;
            if (startingGem < 0)
                startingGem = 0;
        }
#endif
    }
}

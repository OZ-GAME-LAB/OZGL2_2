using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>후보/획득/효과는 팀원 매니저가 소유한다. UI는 한 보상의 선택 의도만 중계한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactRewardBinding : MonoBehaviour
    {
        public event Action<string> Completed;

        public bool IsChoosing => _rewardId != null && !_completed;
        public bool IsApplying => _isApplying;
        public bool IsFaulted => _faulted;
        public ArtifactRewardViewData CurrentReward => _viewData;

        [SerializeField] private ArtifactRewardPanel _panel;

        private ArtifactManager _manager;
        private IReadOnlyList<ArtifactInstance> _inventory;
        private readonly Dictionary<string, ArtifactData> _candidates = new Dictionary<string, ArtifactData>();
        private readonly HashSet<string> _completedIds = new HashSet<string>();
        private ArtifactRewardViewData _viewData;
        private string _rewardId;
        private bool _completed;
        private bool _isApplying;
        private bool _faulted;
        private bool _listening;

        private void OnEnable()
        {
            Subscribe();
            if (_viewData != null && IsChoosing && HasCurrentInventory()) _panel.ShowReward(_viewData);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_panel != null) _panel.HideReward();
        }

        public bool TryInitialize(ArtifactManager manager)
        {
            if (_panel == null || manager == null || !manager.IsInitialized || _isApplying ||
                (IsChoosing && _manager != manager)) return false;
            if (_manager == manager && ReferenceEquals(_inventory, manager.Instances))
            {
                Subscribe();
                return true;
            }
            Unsubscribe();
            ResetReward();
            _manager = manager;
            _inventory = manager.Instances;
            Subscribe();
            return true;
        }

        /// <summary>awarded 값은 이미 지급한 재화다. 반복 호출은 최초 후보를 그대로 표시한다.</summary>
        public bool TryShowReward(string rewardId, int? awardedGold, int? awardedGems)
        {
            if (!isActiveAndEnabled || !HasCurrentInventory() || _isApplying || _faulted ||
                string.IsNullOrWhiteSpace(rewardId) || awardedGold < 0 || awardedGems < 0) return false;
            if (_completedIds.Contains(rewardId)) return true;
            if (_rewardId == rewardId)
            {
                if (_viewData != null) _panel.ShowReward(_viewData);
                return true;
            }
            if (IsChoosing) return false;
            if (!_manager.TryCreateCandidates(out var candidates)) return false;

            // 추첨 성공 시 먼저 키/원본 후보를 보존한다. 표시 실패를 이유로 재추첨하지 않는다.
            _rewardId = rewardId;
            _completed = false;
            _candidates.Clear();
            _viewData = null;
            try
            {
                var offers = new ArtifactRewardOffer[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    var data = candidates[i];
                    if (data == null || string.IsNullOrWhiteSpace(data.Id) || _candidates.ContainsKey(data.Id))
                        throw new InvalidOperationException("Invalid or duplicate artifact candidate.");
                    _candidates.Add(data.Id, data);
                    offers[i] = new ArtifactRewardOffer(data.Id, data.DisplayName, RarityName(data.Rarity),
                        string.IsNullOrWhiteSpace(data.Description) ? "효과 설명 미등록" : data.Description.Replace("\\n", "\n"),
                        data.Icon, RarityColor(data.Rarity));
                }
                if (offers.Length == 0)
                {
                    CompleteReward();
                    return true;
                }
                _viewData = new ArtifactRewardViewData(rewardId, awardedGold, awardedGems, offers);
                _panel.ShowReward(_viewData);
                return true;
            }
            catch (Exception exception)
            {
                _faulted = true;
                Debug.LogException(exception, this);
                return false;
            }
        }

        /// <summary>플레이 소유자가 종료/리셋할 때 호출한다. 인벤토리나 효과는 변경하지 않는다.</summary>
        public void ResetReward()
        {
            if (_isApplying) return;
            _rewardId = null;
            _viewData = null;
            _completed = false;
            _faulted = false;
            _candidates.Clear();
            _completedIds.Clear();
            if (_panel != null) _panel.ResetReward();
        }

        private void HandleChoiceRequested(ArtifactRewardRequest request)
        {
            if (!isActiveAndEnabled || !IsChoosing || request.RewardId != _rewardId || _isApplying) return;
            if (_faulted || !HasCurrentInventory())
            {
                _panel.TryResolveRequest(request.RequestId, false, "보상 상태가 변경되었습니다. 플레이를 다시 시작해주세요.");
                return;
            }
            _isApplying = true;
            try
            {
                if (!request.IsForfeit &&
                    (!_candidates.TryGetValue(request.ArtifactId, out var data) || !_manager.TryAdd(data)))
                {
                    _panel.TryResolveRequest(request.RequestId, false, "획득 실패: 최대 중첩·효과 설정 확인 후 재시도하거나 포기해주세요.");
                    return;
                }
                // 실제 적용 성공/포기 확정 후에만 대기를 끝낸다. 완료를 먼저 기록해 재진입을 막는다.
                _completed = true;
                _completedIds.Add(_rewardId);
                _panel.TryResolveRequest(request.RequestId, true);
                Completed?.Invoke(_rewardId);
            }
            catch (Exception exception)
            {
                // 동기 이벤트가 예외를 던지면 팀원 API가 일부 적용되었을 수 있다. 자동 재지급하지 않는다.
                _faulted = true;
                _panel.TryResolveRequest(request.RequestId, false, "처리 중 오류: 중복 지급 방지를 위해 재시도를 중단했습니다. Console을 확인해주세요.");
                Debug.LogException(exception, this);
            }
            finally { _isApplying = false; }
        }

        private void HandleCleared(IReadOnlyList<ArtifactInstance> removed) => ResetReward();

        private bool HasCurrentInventory() => _manager != null && _manager.IsInitialized &&
            ReferenceEquals(_inventory, _manager.Instances);

        private void CompleteReward()
        {
            _completed = true;
            _completedIds.Add(_rewardId);
            _panel.ResetReward();
            Completed?.Invoke(_rewardId);
        }

        private void Subscribe()
        {
            if (_listening || !isActiveAndEnabled || _panel == null || _manager == null) return;
            _panel.ChoiceRequested += HandleChoiceRequested;
            _manager.Cleared += HandleCleared;
            _listening = true;
        }

        private void Unsubscribe()
        {
            if (!_listening) return;
            if (_panel != null) _panel.ChoiceRequested -= HandleChoiceRequested;
            if (_manager != null) _manager.Cleared -= HandleCleared;
            _listening = false;
        }

        private static string RarityName(ArtifactRarity rarity) => rarity switch
        {
            ArtifactRarity.Common => "일반",
            ArtifactRarity.Rare => "희귀",
            ArtifactRarity.Legendary => "전설",
            ArtifactRarity.Mythic => "신화",
            _ => rarity.ToString()
        };

        private static Color RarityColor(ArtifactRarity rarity) => rarity switch
        {
            ArtifactRarity.Rare => new Color32(112, 186, 255, 255),
            ArtifactRarity.Legendary => new Color32(255, 205, 99, 255),
            ArtifactRarity.Mythic => new Color32(246, 136, 172, 255),
            _ => new Color32(200, 214, 220, 255)
        };
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>담당 시스템이 제공한 표시 정보. 희귀도/효과/보상을 UI가 계산하지 않는다.</summary>
    public sealed class ArtifactRewardOffer
    {
        public string ArtifactId { get; }
        public string DisplayName { get; }
        public string RarityName { get; }
        public string EffectDescription { get; }
        public Sprite Icon { get; }
        public Color RarityColor { get; }

        public ArtifactRewardOffer(string artifactId, string displayName, string rarityName,
            string effectDescription, Sprite icon, Color rarityColor)
        {
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentException("Artifact ID is required.", nameof(artifactId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(rarityName)) throw new ArgumentException("Rarity is required.", nameof(rarityName));
            if (string.IsNullOrWhiteSpace(effectDescription)) throw new ArgumentException("Effect is required.", nameof(effectDescription));
            ArtifactId = artifactId;
            DisplayName = displayName;
            RarityName = rarityName;
            EffectDescription = effectDescription;
            Icon = icon;
            RarityColor = rarityColor;
        }
    }

    public sealed class ArtifactRewardViewData
    {
        /// <summary>기본 후보 수. 실제 후보 수는 Candidates.Count를 사용한다.</summary>
        public const int CandidateCount = 3;
        public string RewardId { get; }
        public int? AwardedGold { get; }
        public int? AwardedGems { get; }
        public bool ShowRewardCurrencies { get; }
        public IReadOnlyList<ArtifactRewardOffer> Candidates { get; }

        /// <summary>후보 선택만 필요한 창. 재화 보상 줄은 표시하지 않는다.</summary>
        public ArtifactRewardViewData(string rewardId, bool showRewardCurrencies,
            params ArtifactRewardOffer[] candidates)
            : this(rewardId, null, null, candidates)
        {
            ShowRewardCurrencies = showRewardCurrencies;
        }

        /// <param name="rewardId">플레이/전투별로 유일한 보상 식별자. 재오픈에도 같은 ID를 유지한다.</param>
        /// <param name="awardedGold">이미 지급된 실제 보상. 알 수 없으면 null, 지급 없음은 0.</param>
        /// <param name="awardedGems">이미 지급된 실제 보상. 매 전투 보석 지급을 가정하지 않는다.</param>
        public ArtifactRewardViewData(string rewardId, int? awardedGold, int? awardedGems,
            params ArtifactRewardOffer[] candidates)
        {
            if (string.IsNullOrWhiteSpace(rewardId)) throw new ArgumentException("Reward ID is required.", nameof(rewardId));
            if (awardedGold < 0) throw new ArgumentOutOfRangeException(nameof(awardedGold));
            if (awardedGems < 0) throw new ArgumentOutOfRangeException(nameof(awardedGems));
            if (candidates == null || candidates.Length == 0)
                throw new ArgumentException("At least one candidate is required.", nameof(candidates));
            var copy = new ArtifactRewardOffer[candidates.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null || !ids.Add(candidates[i].ArtifactId))
                    throw new ArgumentException("Candidates must be non-null with distinct IDs.", nameof(candidates));
                copy[i] = candidates[i];
            }
            RewardId = rewardId;
            AwardedGold = awardedGold;
            AwardedGems = awardedGems;
            ShowRewardCurrencies = true;
            Candidates = Array.AsReadOnly(copy);
        }
    }

    /// <summary>선택 의도만 전달한다. 수신자가 보상 유효성/중복 수령/효과 적용을 검사한다.</summary>
    public sealed class ArtifactRewardRequest
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public string RewardId { get; }
        public string ArtifactId { get; }
        public bool IsForfeit => ArtifactId == null;

        internal ArtifactRewardRequest(string rewardId, string artifactId)
        {
            RewardId = rewardId;
            ArtifactId = artifactId;
        }
    }
}

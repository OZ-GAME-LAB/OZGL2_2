using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Samples
{
    /// <summary>UI 전용 모의 수신자. 재화, 아티팩트 효과, 게임 진행은 변경하지 않는다.</summary>
    public sealed class MvpArtifactRewardSample : MonoBehaviour
    {
        [SerializeField] private ArtifactRewardPanel _panel;
        [SerializeField] private Button _openButton;
        [SerializeField] private TMP_Text _statusText;

        private static readonly int[] ExampleCounts = { 3, 5, 1, 2, 7 };
        private TMP_Text _openText;
        private int _exampleIndex;

        private void OnEnable()
        {
            _panel.ChoiceRequested += HandleChoiceRequested;
            _openButton.onClick.AddListener(ShowNextReward);
            _openText = _openButton.GetComponentInChildren<TMP_Text>();
        }

        private void Start()
        {
            ShowNextReward();
        }

        private void OnDisable()
        {
            if (_panel != null) _panel.ChoiceRequested -= HandleChoiceRequested;
            if (_openButton != null) _openButton.onClick.RemoveListener(ShowNextReward);
        }

        public void ShowNextReward()
        {
            if (_panel.IsRequestPending) return;
            _panel.ShowReward(CreateExample(Guid.NewGuid().ToString(), ExampleCounts[_exampleIndex]));
            _exampleIndex = (_exampleIndex + 1) % ExampleCounts.Length;
            if (_openText != null) _openText.text = $"다음 모의 보상 열기 ({ExampleCounts[_exampleIndex]}개)";
        }

        public static ArtifactRewardViewData CreateExample(string rewardId) => CreateExample(rewardId, ArtifactRewardViewData.CandidateCount);

        public static ArtifactRewardViewData CreateExample(string rewardId, int candidateCount)
        {
            if (candidateCount < 1) throw new ArgumentOutOfRangeException(nameof(candidateCount));
            var examples = new[]
            {
                new ArtifactRewardOffer("sample_guard", "수호의 조각 (샘플)", "희귀",
                    "전열의 생존을 돕는 효과 예시.\n실제 효과와 수치는 담당 시스템에서 제공됩니다.", null, new Color32(117, 196, 255, 255)),
                new ArtifactRewardOffer("sample_ember", "잔불의 인장 (샘플)", "일반",
                    "공격을 강화하는 효과 예시.\n아티팩트의 실제 효과는 이 씬에서 적용하지 않습니다.", null, new Color32(193, 204, 217, 255)),
                new ArtifactRewardOffer("sample_vow", "관문의 맹세 (샘플)", "전설",
                    "아군을 지원하는 효과 예시.\n이미지와 최종 기획 데이터는 추후 연결합니다.", null, new Color32(248, 193, 93, 255))
            };
            var candidates = new ArtifactRewardOffer[candidateCount];
            for (int i = 0; i < candidateCount; i++) candidates[i] = i < examples.Length ? examples[i]
                : new ArtifactRewardOffer("sample_extra_" + (i + 1), $"추가 아티팩트 {i + 1} (샘플)", "일반",
                    "후보 개수 확장 확인용입니다.\n실제 아티팩트 효과는 적용하지 않습니다.", null, new Color32(193, 204, 217, 255));
            return new ArtifactRewardViewData(rewardId, 30, 0, candidates);
        }

        private void HandleChoiceRequested(ArtifactRewardRequest request)
        {
            _statusText.text = request.IsForfeit ? "모의 응답: 모두 포기 완료" : "모의 응답: " + request.ArtifactId;
            _panel.TryResolveRequest(request.RequestId, true);
        }
    }
}

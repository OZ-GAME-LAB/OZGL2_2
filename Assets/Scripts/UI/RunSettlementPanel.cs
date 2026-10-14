using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>정산 시스템에서 전달된 결과만 표시한다. 점수 계산이나 혈석 지급을 수행하지 않는다.</summary>
    public sealed class RunSettlementPanel : MonoBehaviour
    {
        public event Action MainMenuRequested;
        public bool IsVisible => _root != null && _root.activeInHierarchy;

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _totems;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _artifacts;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private TMP_Text _bloodstone;
        [SerializeField] private TMP_Text _gaugeText;
        [SerializeField] private RectTransform _gaugeFill;
        [SerializeField] private TMP_Text _notice;
        [SerializeField] private Button _mainButton;

        private bool _mainRequestPending;

        private void OnEnable() => _mainButton.onClick.AddListener(HandleMain);
        private void OnDisable()
        {
            _mainButton.onClick.RemoveListener(HandleMain);
            Hide();
        }

        public void ShowPending(bool victory)
        {
            Show(new RunSettlementViewData(victory, null, null, null, null, null, null, null));
        }

        public void Show(RunSettlementViewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _mainRequestPending = false;
            _title.text = data.IsVictory ? "원정 완료" : "원정 종료";
            _totems.text = data.TotemSummary ?? "선택 토템 정보 대기";
            _progress.text = data.ProgressSummary ?? "진행 기록 연결 대기";
            _artifacts.text = data.ArtifactSummary ?? "획득 유물 기록 대기";
            _score.text = data.TotalScore.HasValue ? data.TotalScore.Value.ToString("N0") : "—";
            _bloodstone.text = data.Bloodstones.HasValue ? $"획득 혈석  {data.Bloodstones.Value:N0}" : "획득 혈석  —";
            bool hasGauge = data.GaugeCurrent.HasValue && data.GaugeTarget.HasValue;
            _gaugeText.text = hasGauge ? $"{data.GaugeCurrent:N0} / {data.GaugeTarget:N0}" : "혈석 게이지 연결 대기";
            _gaugeFill.anchorMax = new Vector2(hasGauge ? (float)data.GaugeCurrent.Value / data.GaugeTarget.Value : 0, 1);
            _notice.text = data.TotalScore.HasValue && data.Bloodstones.HasValue ? "정산 결과" : "정산 시스템 연결 대기 · 표시만으로 재화가 지급되지 않습니다";
            _mainButton.interactable = MainMenuRequested != null;
            _root.SetActive(true);
        }

        public void Hide()
        {
            _mainRequestPending = false;
            if (_root != null) _root.SetActive(false);
        }

        public void ResetMainMenuRequest()
        {
            _mainRequestPending = false;
            _mainButton.interactable = MainMenuRequested != null;
        }

        private void HandleMain()
        {
            if (!isActiveAndEnabled || !IsVisible || _mainRequestPending || MainMenuRequested == null) return;
            _mainRequestPending = true;
            _mainButton.interactable = false;
            MainMenuRequested.Invoke();
        }
    }

    /// <summary>UI 수신용 스냅샷. null은 미연결이며 0 보상과 구분한다.</summary>
    public sealed class RunSettlementViewData
    {
        public bool IsVictory { get; }
        public string TotemSummary { get; }
        public string ProgressSummary { get; }
        public string ArtifactSummary { get; }
        public long? TotalScore { get; }
        public int? Bloodstones { get; }
        public int? GaugeCurrent { get; }
        public int? GaugeTarget { get; }

        public RunSettlementViewData(bool isVictory, string totems, string progress, string artifacts,
            long? totalScore, int? bloodstones, int? gaugeCurrent, int? gaugeTarget)
        {
            if (totalScore < 0 || bloodstones < 0 || gaugeCurrent < 0 || gaugeTarget <= 0 ||
                gaugeCurrent.HasValue != gaugeTarget.HasValue || gaugeCurrent > gaugeTarget)
                throw new ArgumentOutOfRangeException(nameof(totalScore), "Invalid settlement display values.");
            IsVictory = isVictory;
            TotemSummary = totems;
            ProgressSummary = progress;
            ArtifactSummary = artifacts;
            TotalScore = totalScore;
            Bloodstones = bloodstones;
            GaugeCurrent = gaugeCurrent;
            GaugeTarget = gaugeTarget;
        }
    }
}

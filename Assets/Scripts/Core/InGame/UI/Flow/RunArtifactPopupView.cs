using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.InGame
{
    /// <summary>결산 버튼을 종료 시점의 유물 기록에 연결한다.</summary>
    public sealed class RunArtifactPopupView : MonoBehaviour
    {
        [SerializeField] private Button _artifactButton;
        [SerializeField] private RunResultView _resultView;
        [SerializeField] private ArtifactInventoryPopupView _inventoryView;

        private void OnEnable()
        {
            if (_artifactButton != null)
                _artifactButton.onClick.AddListener(Open);
        }

        private void OnDisable()
        {
            if (_artifactButton != null)
                _artifactButton.onClick.RemoveListener(Open);
        }

        public void Open()
        {
            if (_artifactButton == null || !_artifactButton.isActiveAndEnabled || !_artifactButton.IsInteractable())
                return;
            if (_resultView == null || !_resultView.IsVisible || !_resultView.IsPending ||
                _resultView.CurrentSummary == null || _inventoryView == null)
                return;

            _inventoryView.Show(_resultView.CurrentSummary.ArtifactInventory);
        }
    }
}

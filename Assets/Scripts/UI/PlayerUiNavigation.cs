using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>필요할 때 하나의 정보 팝업만 연다. 전투/보상 상태는 기존 Core가 소유한다.</summary>
    public sealed class PlayerUiNavigation : MonoBehaviour
    {
        [SerializeField] private GameFlowController _flow;
        [SerializeField] private BuildingCatalogPanel _catalog;
        [SerializeField] private PlayerPopup[] _popups;
        [SerializeField] private Button[] _catalogButtons;
        [SerializeField] private GameObject[] _blockingPanels;
        [SerializeField] private ArtifactInventoryPanel _artifactInventory;

        private PlayerPopup _current;

        private void OnEnable()
        {
            foreach (var popup in _popups) popup.Shown += HandleShown;
            foreach (var button in _catalogButtons) button.onClick.AddListener(HandleCatalog);
            _flow.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            foreach (var popup in _popups) popup.Shown -= HandleShown;
            foreach (var button in _catalogButtons) button.onClick.RemoveListener(HandleCatalog);
            if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
            if (_current != null) _current.Hide();
            _current = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TryCloseActivePopup();
        }

        private void LateUpdate()
        {
            // Core의 PhaseChanged는 입력 잠금 해제 전에도 발행되므로 최신 계약으로 동기화한다.
            bool blocking = HasBlockingPanel();
            if (blocking)
            {
                foreach (var popup in _popups) if (popup.IsVisible) popup.Hide();
                _current = null;
            }
            bool canBuild = _flow != null && _flow.CanEnterBuildMode() && !blocking;
            foreach (var button in _catalogButtons)
                if (button.interactable != canBuild) button.interactable = canBuild;
        }

        public bool TryCloseActivePopup()
        {
            if (!isActiveAndEnabled || HasBlockingPanel() || _current == null || !_current.IsVisible) return false;
            if (_artifactInventory != null && _artifactInventory.TryCloseDetail()) return true;
            _current.Hide();
            return true;
        }

        private void HandleCatalog()
        {
            if (_flow != null && _flow.CanEnterBuildMode() && !HasBlockingPanel()) _catalog.Show();
        }

        private void HandleShown(PlayerPopup opened)
        {
            if (HasBlockingPanel()) { opened.Hide(); return; }
            foreach (var popup in _popups) if (popup != opened) popup.Hide();
            _current = opened;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Preparation) return;
            foreach (var popup in _popups) popup.Hide();
            _current = null;
        }

        private bool HasBlockingPanel()
        {
            foreach (var panel in _blockingPanels) if (panel != null && panel.activeInHierarchy) return true;
            return false;
        }
    }
}

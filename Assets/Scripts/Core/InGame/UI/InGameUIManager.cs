using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.UI.InGame
{
    /// <summary>현재 HUD와 열린 팝업 목록을 관리한다. 게임 명령은 기능별 Presenter가 처리한다.</summary>
    public sealed class InGameUIManager : MonoBehaviour
    {
        [SerializeField] private UIScreen[] _screenInstances = Array.Empty<UIScreen>();
        private readonly Dictionary<UIId, UIScreen> _screens = new Dictionary<UIId, UIScreen>();
        private readonly List<UIScreen> _openedPopups = new List<UIScreen>();
        private UIScreen _currentHud;
        private bool _screensReady;
        private bool _closingPopups;

        public bool IsReady { get; private set; }
        public UIScreen TopPopup => _openedPopups.Count == 0 ? null : _openedPopups[_openedPopups.Count - 1];
        public int OpenPopupCount => _openedPopups.Count;
        public bool HasBlockingPopup => _openedPopups.Exists(screen => screen.BlocksHudInput);

        /// <summary>미리 배치된 화면을 등록한다. 비활성 UI 루트에서도 초기화할 수 있다.</summary>
        public bool InitializeScreens()
        {
            IsReady = false;
            CloseAllPopups(UICloseReason.ContextLost);
            _screensReady = false;
            _screens.Clear();
            if (_currentHud != null) _currentHud.Hide(UICloseReason.ContextLost, false);
            _currentHud = null;
            foreach (UIScreen screen in _screenInstances)
            {
                if (screen == null || screen.Id == UIId.None || _screens.ContainsKey(screen.Id))
                {
                    Debug.LogError("[InGameUI] 화면 참조가 없거나 화면 ID가 중복되었습니다.", this);
                    return false;
                }
                if (!screen.Attach(this)) return false;
                screen.Hide(UICloseReason.ContextLost, false);
                _screens.Add(screen.Id, screen);
            }
            _screensReady = true;
            IsReady = true;
            return true;
        }

        private void OnEnable() { if (IsReady) ShowHud(); }
        private void OnDisable()
        {
            CloseAllPopups(UICloseReason.ContextLost);
            if (_currentHud != null) _currentHud.SetInputEnabled(false);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseTopPopup();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) CloseTopPopup();
#endif
        }

        public bool TryGetScreen(UIId id, out UIScreen screen)
        {
            screen = null;
            return _screensReady && _screens.TryGetValue(id, out screen);
        }

        public bool ShowHud(UIId id = UIId.Hud)
        {
            if (_closingPopups || !TryGetScreen(id, out UIScreen screen) || !screen.IsHud) return false;
            if (_currentHud == screen)
            {
                RefreshInput();
                return true;
            }
            if (_currentHud != null)
            {
                CloseAllPopups(UICloseReason.ContextLost);
            }
            _closingPopups = true;
            try
            {
                if (_currentHud != null) _currentHud.Hide(UICloseReason.Replaced, true);
                _currentHud = screen;
                screen.Display();
            }
            finally
            {
                _closingPopups = false;
                RefreshInput();
            }
            return true;
        }

        /// <summary>현재 팝업을 보존하고 그 위에 화면을 쌓는다. 같은 화면은 한 번만 등록한다.</summary>
        public bool OpenPopup(UIId id)
        {
            if (_closingPopups || !TryGetScreen(id, out UIScreen screen) || screen.IsHud) return false;
            if (_openedPopups.Contains(screen)) return true;
            UIScreen top = TopPopup;
            if (top != null && top.CloseWhenCovered && !ClosePopup(top.Id, UICloseReason.Replaced))
                return false;
            _openedPopups.Add(screen);
            screen.transform.SetAsLastSibling();
            screen.Display();
            RefreshInput();
            return true;
        }

        /// <summary>기존 팝업 묶음을 닫고 다음 화면으로 전환한다.</summary>
        public bool ReplacePopup(UIId id)
        {
            if (_closingPopups || !TryGetScreen(id, out UIScreen screen) || screen.IsHud) return false;
            if (_openedPopups.Count == 1 && TopPopup == screen) return true;
            // 일반 건설창이 필수 보상이나 분기 화면을 치우지 못하게 한다.
            if (HasBlockingPopup && !screen.BlocksHudInput) return false;
            CloseAllPopups(UICloseReason.Replaced);
            return OpenPopup(id);
        }

        public bool CloseTopPopup() => TopPopup != null && ClosePopup(TopPopup.Id, UICloseReason.UserCancel);

        public bool ClosePopup(UIId id, UICloseReason reason = UICloseReason.UserCancel)
        {
            if (_closingPopups || !TryGetScreen(id, out UIScreen screen)) return false;
            int index = _openedPopups.IndexOf(screen);
            if (index < 0) return false;
            if (reason == UICloseReason.UserCancel && (!screen.CanCloseByUser || index != _openedPopups.Count - 1))
                return false;

            _closingPopups = true;
            try
            {
                // 부모가 종료되면 부모의 선택 데이터를 사용하는 위쪽 상세 창도 함께 닫는다.
                while (_openedPopups.Count > index) RemoveTopPopup(reason);
            }
            finally
            {
                _closingPopups = false;
                RefreshInput();
            }
            return true;
        }

        //모든 팝업 닫기
        public void CloseAllPopups(UICloseReason reason)
        {
            if (_closingPopups) return;
            _closingPopups = true;
            try
            {
                while (_openedPopups.Count > 0) RemoveTopPopup(reason);
            }
            finally
            {
                _closingPopups = false;
                RefreshInput();
            }
        }

        //가장 최근에 열린 팝업창을 닫기
        private void RemoveTopPopup(UICloseReason reason)
        {
            UIScreen screen = TopPopup;
            // 닫힘 알림이 다시 닫기를 요청해도 같은 화면을 두 번 처리하지 않는다.
            _openedPopups.RemoveAt(_openedPopups.Count - 1);
            screen.Hide(reason, true);
        }

        private void RefreshInput()
        {
            if (_currentHud != null) _currentHud.SetInputEnabled(!HasBlockingPopup);
            for (int i = 0; i < _openedPopups.Count; i++)
                _openedPopups[i].SetInputEnabled(i == _openedPopups.Count - 1); //가장 최근에 열린 팝업창 이외에는 입력을 제한한다
        }
    }
}

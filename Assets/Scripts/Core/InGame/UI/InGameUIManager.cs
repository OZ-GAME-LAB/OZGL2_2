using System;
using System.Collections.Generic;
using Game.Core;
using OZGL.KDH;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.UI.InGame
{
    /// <summary>화면의 수명·순서·입력만 관리한다. 게임 명령은 기능별 Presenter가 처리한다.</summary>
    public sealed class InGameUIManager : MonoBehaviour
    {
        [SerializeField] private UIRegistration[] _screens = Array.Empty<UIRegistration>();
        [SerializeField] private GameHudView _hud;
        [SerializeField] private HudPresenter _hudPresenter;
        [SerializeField] private BuildingUIPresenter _building;
        [SerializeField] private ArtifactRewardPresenter _artifact;
        [SerializeField] private RunFlowPresenter _flow;
        [SerializeField] private RunDecisionView _decision;
        [SerializeField] private BuildingShortcut[] _shortcuts = Array.Empty<BuildingShortcut>();

        private sealed class OpenScreen
        {
            public UIRegistration Registration;
            public UIScreen Screen;
            public UIHandle Handle;
            public GameObject PreviousFocus;
        }

        private readonly Dictionary<UIId, UIRegistration> _registrations = new Dictionary<UIId, UIRegistration>();
        private readonly List<OpenScreen> _opened = new List<OpenScreen>();
        private UIScreen _currentHud;
        private bool _screensReady;
        private bool _closingAll;
        private int _version;
        public bool IsReady { get; private set; }
        public bool HasModalOpen => _opened.Exists(item => item.Registration.Layer == UILayer.Modal);
        public UIHandle TopHandle => _opened.Count == 0 ? default : _opened[_opened.Count - 1].Handle;

        public void Initialize(RunCurrencyManager wallet, WaveController waves,
            GameFlowController flow, ArtifactManager artifacts, BuildingCoreProgress coreProgress,
            TestWaitingScript contentGate, BuildingBuildController buildingController, BuildingSlot[] slots)
        {
            IsReady = false;
            if (wallet == null || waves == null || flow == null || artifacts == null ||
                coreProgress == null || contentGate == null || buildingController == null || slots == null ||
                _hud == null || _hudPresenter == null || _building == null || _artifact == null ||
                _flow == null || _decision == null)
            {
                Debug.LogError("[InGameUI] 초기화 참조가 없습니다. 부트스트랩과 UI 프리팹 연결을 확인해주세요.", this);
                return;
            }
            if (!wallet.IsInitialized)
            {
                Debug.LogError("[InGameUI] 재화 매니저를 먼저 초기화해주세요.", this);
                return;
            }
            if (!InitializeScreens()) return;
            _hud.Initialize();
            _artifact.ResetReward();
            if (!_artifact.TryInitialize(artifacts))
            {
                Debug.LogError("[InGameUI] 유물 매니저 초기화가 완료되지 않았습니다.", this);
                return;
            }
            _hudPresenter.Initialize(_hud, wallet, flow, waves);
            _building.Initialize(buildingController, wallet, flow, coreProgress, slots);
            _flow.Initialize(_hud, flow, waves, wallet, contentGate, artifacts, _artifact);
            _decision.Initialize(flow, waves);
            foreach (BuildingShortcut shortcut in _shortcuts) if (shortcut != null) shortcut.Initialize(flow);
            IsReady = true;
            ShowHud();
        }

        /// <summary>게임 시스템 없이도 등록과 공통 화면 동작을 준비할 수 있다.</summary>
        public bool InitializeScreens()
        {
            IsReady = false;
            CloseAll(UICloseReason.ContextLost);
            _screensReady = false;
            _registrations.Clear();
            if (_currentHud != null) _currentHud.Conceal(UICloseReason.ContextLost, false);
            _currentHud = null;
            foreach (UIRegistration entry in _screens)
            {
                if (entry == null || entry.Id == UIId.None || _registrations.ContainsKey(entry.Id) ||
                    (entry.Instance == null && entry.Prefab == null) ||
                    (entry.Instance != null && entry.Prefab != null && !entry.Created))
                {
                    Debug.LogError("[InGameUI] 화면 ID 중복 또는 화면/프리팹 연결 오류입니다.", this);
                    return false;
                }
                if (entry.Instance != null)
                {
                    if (!entry.Instance.Attach(this, entry.Id)) return false;
                    entry.Instance.Conceal(UICloseReason.ContextLost, false);
                }
                _registrations.Add(entry.Id, entry);
            }
            _screensReady = true;
            return true;
        }

        private void OnEnable() { if (IsReady) ShowHud(); }
        private void OnDisable() => CloseAll(UICloseReason.ContextLost);

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseTop();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) CloseTop();
#endif
        }

        public bool TryGetScreen(UIId id, out UIScreen screen)
        {
            screen = null;
            if (!_screensReady || !_registrations.TryGetValue(id, out UIRegistration entry)) return false;
            if (entry.Instance == null)
            {
                // 최초 사용 때만 생성하고 이 씬이 끝날 때까지 같은 인스턴스를 재사용한다.
                UIScreen instance = Instantiate(entry.Prefab, entry.Parent != null ? entry.Parent : transform);
                if (!instance.Attach(this, id)) { Destroy(instance.gameObject); return false; }
                instance.Conceal(UICloseReason.ContextLost, false);
                if (instance.Root != instance.gameObject) instance.gameObject.SetActive(true);
                entry.Instance = instance;
                entry.Created = true;
            }
            screen = entry.Instance;
            return true;
        }

        public UIHandle ShowHud(UIId id = UIId.Hud)
        {
            if (!TryGetScreen(id, out UIScreen screen) || _registrations[id].Layer != UILayer.Hud) return default;
            if (_currentHud == screen && screen.Handle.IsValid) return screen.Handle;
            if (_currentHud != null)
            {
                CloseAll(UICloseReason.ContextLost);
                _currentHud.Conceal(UICloseReason.Replaced, true);
            }
            _currentHud = screen;
            UIHandle handle = new UIHandle(id, ++_version);
            screen.Display(handle);
            RefreshInput();
            if (_opened.Count > 0) Focus(_opened[_opened.Count - 1].Screen.InitialFocus);
            return handle;
        }

        public UIHandle Open(UIId id)
        {
            if (!_screensReady || _closingAll || !_registrations.TryGetValue(id, out UIRegistration entry)) return default;
            if (entry.Layer == UILayer.Hud) return ShowHud(id);
            foreach (OpenScreen opened in _opened) if (opened.Handle.Id == id) return opened.Handle;
            if (entry.Layer != UILayer.Modal && HasModalOpen) return default;
            return Replace(id);
        }

        /// <summary>기존 팝업 묶음을 닫고 새 화면을 연다. 필수 진행 화면 교체는 Presenter가 요청한다.</summary>
        public UIHandle Replace(UIId id)
        {
            if (!TryGetScreen(id, out UIScreen screen) || _closingAll) return default;
            UIRegistration entry = _registrations[id];
            if (entry.Layer == UILayer.Hud) return ShowHud(id);
            if (entry.Layer != UILayer.Modal && HasModalOpen) return default;
            GameObject focus = _opened.Count == 0 ? CurrentFocus() : _opened[0].PreviousFocus;
            CloseAll(UICloseReason.Replaced);
            return Display(entry, screen, focus, -1);
        }

        /// <summary>현재 화면을 보존하고 그 위에 상세 화면을 연다.</summary>
        public UIHandle Push(UIId id)
        {
            if (!TryGetScreen(id, out UIScreen screen) || _closingAll) return default;
            foreach (OpenScreen opened in _opened) if (opened.Handle.Id == id) return opened.Handle;
            UIRegistration entry = _registrations[id];
            if (entry.Layer == UILayer.Hud) return default;
            int sortingOrder = _opened.Count == 0 ? -1 : _opened[_opened.Count - 1].Screen.SortingOrder;
            return Display(entry, screen, CurrentFocus(), sortingOrder);
        }

        private UIHandle Display(UIRegistration entry, UIScreen screen, GameObject previousFocus, int sortingOrder)
        {
            UIHandle handle = new UIHandle(entry.Id, ++_version);
            var opened = new OpenScreen { Registration = entry, Screen = screen, Handle = handle, PreviousFocus = previousFocus };
            _opened.Add(opened);
            screen.Display(handle, sortingOrder);
            RefreshInput();
            Focus(screen.InitialFocus);
            return handle;
        }

        public bool Close(UIHandle handle, UICloseReason reason = UICloseReason.UserCancel)
        {
            int index = _opened.FindIndex(item => item.Handle.Equals(handle));
            if (index < 0) return false;
            OpenScreen target = _opened[index];
            if (reason == UICloseReason.UserCancel && (!target.Registration.AllowUserClose || index != _opened.Count - 1)) return false;
            // 부모가 닫히면 부모의 선택 데이터를 쓰는 상세 창도 함께 닫는다.
            var handles = new List<UIHandle>();
            for (int i = _opened.Count - 1; i >= index; i--) handles.Add(_opened[i].Handle);
            foreach (UIHandle closing in handles)
            {
                int currentIndex = _opened.FindIndex(item => item.Handle.Equals(closing));
                if (currentIndex >= 0) RemoveAt(currentIndex, reason);
            }
            RefreshInput();
            RestoreFocus(target.PreviousFocus);
            return true;
        }

        public bool CloseTop() => Close(TopHandle, UICloseReason.UserCancel);

        private void CloseAll(UICloseReason reason)
        {
            if (_closingAll) return;
            _closingAll = true;
            try { while (_opened.Count > 0) RemoveAt(_opened.Count - 1, reason); }
            finally { _closingAll = false; RefreshInput(); }
        }

        private void RemoveAt(int index, UICloseReason reason)
        {
            OpenScreen opened = _opened[index];
            // 알림에서 같은 화면을 닫는 경우를 위해 먼저 목록에서 제거한다.
            _opened.RemoveAt(index);
            opened.Screen.Conceal(reason, true);
        }

        private void RefreshInput()
        {
            if (_currentHud != null) _currentHud.SetInputEnabled(!HasModalOpen);
            for (int i = 0; i < _opened.Count; i++) _opened[i].Screen.SetInputEnabled(i == _opened.Count - 1);
        }

        private static GameObject CurrentFocus() => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        private static void Focus(Selectable target)
        {
            if (EventSystem.current != null && target != null && target.isActiveAndEnabled && target.IsInteractable())
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
        private void RestoreFocus(GameObject previous)
        {
            Selectable target = previous != null ? previous.GetComponent<Selectable>() : null;
            if (target != null && target.isActiveAndEnabled && target.IsInteractable()) Focus(target);
            else if (_opened.Count > 0) Focus(_opened[_opened.Count - 1].Screen.InitialFocus);
            else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}

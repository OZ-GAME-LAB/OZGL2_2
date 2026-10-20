using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class LoadingView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _label;

    private static readonly string[] Messages = { "로딩 중", "로딩 중.", "로딩 중..", "로딩 중..." };
    private EventSystem _eventSystem;
    private bool _navigationEnabled;
    private bool _visible;
    private bool _animating;
    private float _shownAt;
    private int _messageIndex;

    public void Show()
    {
        _visible = _animating = true;
        _shownAt = Time.unscaledTime;
        _messageIndex = 0;
        _label.text = Messages[0];
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        BlockNavigation();
    }

    public void Hide()
    {
        _visible = _animating = false;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        RestoreNavigation();
    }

    public void ShowError(string message)
    {
        Show();
        _animating = false;
        _label.text = "로딩에 실패했습니다.\n" + message;
    }

    private void Update()
    {
        if (!_visible) return;
        BlockNavigation();
        if (!_animating) return;
        int index = (int)((Time.unscaledTime - _shownAt) / 0.35f) % Messages.Length;
        if (_messageIndex == index) return;
        _messageIndex = index;
        _label.text = Messages[index];
    }

    // 씬 교체 후 새 EventSystem에도 적용한다. 포인터 입력은 배경 Image가 막는다.
    public void BlockNavigation()
    {
        if (_eventSystem == EventSystem.current) return;
        RestoreNavigation();
        _eventSystem = EventSystem.current;
        if (_eventSystem == null) return;
        _navigationEnabled = _eventSystem.sendNavigationEvents;
        _eventSystem.sendNavigationEvents = false;
    }

    private void RestoreNavigation()
    {
        if (_eventSystem != null) _eventSystem.sendNavigationEvents = _navigationEnabled;
        _eventSystem = null;
    }

    private void OnDestroy() => RestoreNavigation();
}

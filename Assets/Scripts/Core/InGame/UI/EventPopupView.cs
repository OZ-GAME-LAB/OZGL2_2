using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>사건의 선택지와 결과를 표시한다. 비용과 보상은 이벤트 매니저가 처리한다.</summary>
[DisallowMultipleComponent]
public sealed class EventPopupView : MonoBehaviour, IEventUI
{
    [Serializable]
    private sealed class ChoiceView
    {
        public UnityEngine.UI.Button Button;
        public TMP_Text Title;
        public TMP_Text Description;
    }

    [SerializeField] private UIScreen _screen;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private UnityEngine.UI.Image _image;
    [SerializeField] private ChoiceView[] _choices;

    private UniTaskCompletionSource<int> _completion;

    public UniTask<int> ChooseAsync(EventData data, bool[] available, string[] reasons,
        string feedback, CancellationToken token)
    {
        Validate(data, token);
        if (data.Choices == null || data.Choices.Count < 1 || data.Choices.Count > _choices.Length ||
            available == null || available.Length != data.Choices.Count ||
            reasons == null || reasons.Length != data.Choices.Count)
            throw new ArgumentException("이벤트 선택지와 선택 가능 정보가 일치하지 않습니다.");

        ShowContent(data, string.IsNullOrWhiteSpace(feedback)
            ? data.Description : data.Description + "\n" + feedback);
        for (int i = 0; i < _choices.Length; i++)
        {
            ChoiceView view = _choices[i];
            view.Button.gameObject.SetActive(i < data.Choices.Count);
            if (i >= data.Choices.Count) continue;
            view.Title.text = data.Choices[i].Title;
            view.Description.text = data.Choices[i].Description;
            if (!available[i] && !string.IsNullOrWhiteSpace(reasons[i]))
                view.Description.text += "\n<color=#E77878>" + reasons[i] + "</color>";
            view.Button.interactable = available[i];
        }
        return WaitForClickAsync(token);
    }

    public async UniTask ShowMessageAsync(EventData data, string message, string buttonText,
        CancellationToken token)
    {
        Validate(data, token);
        ShowContent(data, message);
        for (int i = 0; i < _choices.Length; i++)
        {
            _choices[i].Button.gameObject.SetActive(i == 0);
            _choices[i].Button.interactable = i == 0;
        }
        _choices[0].Title.text = buttonText;
        _choices[0].Description.text = string.Empty;
        await WaitForClickAsync(token);
    }

    public void Close()
    {
        _completion?.TrySetCanceled();
        if (_screen != null)
            _screen.Manager?.ClosePopup(_screen.Id, UICloseReason.ContextLost);
    }

    private void OnDisable() => _completion?.TrySetCanceled();

    private void Validate(EventData data, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (_completion != null) throw new InvalidOperationException("이벤트 선택을 이미 기다리는 중입니다.");
        if (!enabled || _screen == null || _screen.Manager == null || _title == null ||
            _description == null || _image == null || _choices == null || _choices.Length != 3)
            throw new InvalidOperationException("이벤트 UI 참조를 연결해주세요.");
        for (int i = 0; i < _choices.Length; i++)
        {
            ChoiceView view = _choices[i];
            if (view == null || view.Button == null || view.Title == null || view.Description == null)
                throw new InvalidOperationException("이벤트 선택지 UI 참조를 연결해주세요.");
            for (int j = 0; j < i; j++)
                if (_choices[j].Button == view.Button)
                    throw new InvalidOperationException("이벤트 선택지 버튼이 중복되었습니다.");
        }
    }

    private void ShowContent(EventData data, string description)
    {
        _title.text = data.Title;
        _description.text = description;
        _image.sprite = data.Image;
        _image.enabled = data.Image != null;
    }

    private async UniTask<int> WaitForClickAsync(CancellationToken token)
    {
        var completion = new UniTaskCompletionSource<int>();
        var listeners = new UnityAction[_choices.Length];
        bool clicked = false;
        _completion = completion;
        void OnClosed(UIScreen screen, UICloseReason reason) => completion.TrySetCanceled();
        _screen.Closed += OnClosed;
        for (int i = 0; i < _choices.Length; i++)
        {
            int index = i;
            listeners[i] = () =>
            {
                if (_completion != completion || clicked || completion.Task.Status != UniTaskStatus.Pending || !_screen.IsVisible ||
                    _screen.Manager.TopPopup != _screen || !_choices[index].Button.IsInteractable()) return;
                clicked = true;
                LockChoices();
                completion.TrySetResult(index);
            };
            _choices[i].Button.onClick.AddListener(listeners[i]);
        }

        try
        {
            using (token.Register(() => completion.TrySetCanceled(token)))
            {
                token.ThrowIfCancellationRequested();
                if (!_screen.Manager.OpenPopup(_screen.Id) || !_screen.IsVisible || !isActiveAndEnabled)
                    throw new InvalidOperationException("이벤트 화면을 열 수 없습니다.");
                if (EventSystem.current != null)
                    foreach (ChoiceView choice in _choices)
                        if (choice.Button.gameObject.activeInHierarchy && choice.Button.IsInteractable())
                        {
                            EventSystem.current.SetSelectedGameObject(choice.Button.gameObject);
                            break;
                        }
                return await completion.Task;
            }
        }
        finally
        {
            await UniTask.SwitchToMainThread();
            if (_screen != null) _screen.Closed -= OnClosed;
            for (int i = 0; i < _choices.Length; i++)
                if (_choices[i].Button != null) _choices[i].Button.onClick.RemoveListener(listeners[i]);
            if (_completion == completion)
            {
                _completion = null;
                LockChoices();
                // 정상 선택은 창을 유지한다. 다음 요청이나 하위 유물 선택이 이어진다.
                if (!clicked || completion.Task.Status != UniTaskStatus.Succeeded) Close();
            }
        }
    }

    private void LockChoices()
    {
        foreach (ChoiceView choice in _choices)
            if (choice.Button != null) choice.Button.interactable = false;
    }
}

using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoadingManager : MonoBehaviour, ISceneTransition
{
    public static SceneLoadingManager Instance { get; private set; }
    public bool IsLoading { get; private set; }

    [SerializeField] private LoadingView _view;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _view.Hide();
    }

    private void Start() => EnterSceneAsync(null).Forget();

    public UniTask LoadAsync(string sceneName) => EnterSceneAsync(sceneName);

    private async UniTask EnterSceneAsync(string sceneName)
    {
        if (IsLoading)
            throw new InvalidOperationException("씬 전환이 이미 진행 중입니다.");

        IsLoading = true;
        ISceneEntry entry;
        try
        {
            var token = this.GetCancellationTokenOnDestroy();
            _view.Show();
            // 로딩 화면을 먼저 렌더링하고, 이후 실제 로드/초기화를 시작한다.
            await UniTask.NextFrame(cancellationToken: token);

            if (sceneName != null)
                await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: token);

            // 씬이 바뀔때마다 ISceneEntry를 새롭게 찾아 이전 씬의 객체를 가지고 있지 않는다
            entry = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .OfType<ISceneEntry>().Single();

            _view.BlockNavigation();
            await entry.InitializeAsync(this);
            token.ThrowIfCancellationRequested();
            _view.Hide();
        }
        catch (Exception exception)
        {
            if (this != null) _view.ShowError(exception.Message);
            throw;
        }
        finally
        {
            IsLoading = false;
        }

        // 완료된 런의 즉시 메인 복귀도 가능하도록 잠금 해제 이후 실행한다.
        entry.OnPresented();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

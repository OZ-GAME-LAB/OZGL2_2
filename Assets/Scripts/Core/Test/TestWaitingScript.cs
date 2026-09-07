using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

//실제 연결전 보상대기 및 전투준비연출 -> 전투로 이어지는 대기 구현
public class TestWaitingScript : MonoBehaviour
{
    [SerializeField] private GameFlowController _controller;
    
    private bool _toggle;

    private void Start()
    {
    }

    public void WaveStartBtn()
    {
        Debug.Log($"[TestWaitingScript] 웨이브 시작 테스트");
        _controller.TryStartWave().Forget();
    }

    public void ResultBtn()
    {
        Debug.Log($"[TestWaitingScript] 전투 종료 테스트");
        _controller.CompleteWave().Forget();
    }

    public void ResetBtn()
    {
        Debug.Log($"[TestWaitingScript] 게임 리셋 테스트");
        _controller.Initialize();
    }

    public void ChooseResultBtn()
    {
        Debug.Log($"[TestWaitingScript] 보상 선택 테스트");
        _toggle = true;
    }
    public async UniTask WaitForSeconds(CancellationToken cts)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cts);
    }

    public async UniTask WaitToggle(CancellationToken cts)
    {
        _toggle = false;

        await UniTask.WaitUntil(
            () => _toggle,
            cancellationToken: cts);
    }
}

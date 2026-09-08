using Game.Core;
using UnityEngine;

public class BootStrap : MonoBehaviour
{
    [SerializeField] private TestWaitingScript _testScript; //테스트용으로 , 실제 구현시 삭제할것
    [SerializeField] private GameFlowController _gameFlowController;
    [SerializeField] private WaveController _waveController;
    [SerializeField] private ISpawner _spawner = new TestSpawner(); //테스트용으로 , 실제 구현시 스폰파트에서 만든 스크립트 넣기

    void Start()
    {
        _testScript.Initialize(_gameFlowController, _waveController);
        _gameFlowController.Initialize(_waveController, _testScript);
        _waveController.Initialize(_gameFlowController, _spawner);
        _gameFlowController.BeginRun();

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

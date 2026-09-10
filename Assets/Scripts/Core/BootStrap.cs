using Game.Core;
using UnityEngine;
/// 각 시스템의 참조 연결과 초기화 순서를 관리하고,
/// 준비가 완료되면 GameFlowController.BeginRun()을 호출한다.
///
/// 각 담당 파트 요청 사항:
/// - 외부 연결이 필요한 대표 매니저와 Initialize 메서드를 제공
/// - Initialize에서 필요한 참조를 전달받고 내부 준비와 이벤트 구독을 처리
/// - 필요한 참조와 선행 초기화 대상은 구현 전에 공유
/// - 모든 시스템 초기화 이후 실행해야 하는 작업이 있다면 별도 시작 메서드를 제공하고 호출 시점 공지
/// - 비동기 초기화는 완료까지 기다릴 수 있도록 UniTask를 반환
/// - 구현 후 Bootstrap에서 연결 가능한 시점에 안내
///
/// 초기화 중 다른 시스템에 게임 진행을 요청하는 이벤트는 발생시키지 않고
/// 이벤트 구독 해제와 재초기화 시 중복 구독 방지는 각 시스템에서 처리.
public class BootStrap : MonoBehaviour
{
    [SerializeField] private TestWaitingScript _testScript; //테스트용으로 , 실제 구현시 삭제할것
    [SerializeField] private GameFlowController _gameFlowController;
    [SerializeField] private WaveController _waveController;
    [SerializeField] private ISpawner _spawner = new TestSpawner(); //테스트용으로 , 실제 구현시 스폰파트에서 만든 스크립트 넣기
    //각자 대표매니저 1개 만들고 각각 필요한 참조를 말하면 제공

    void Start()
    {
        if (!ValidateReferences()) return;

        _testScript.Initialize(_gameFlowController, _waveController);
        _gameFlowController.Initialize(_waveController, _testScript);
        _waveController.Initialize(_gameFlowController, _spawner);
        _gameFlowController.BeginRun();
        
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        if (_testScript == null)
        {
            Debug.LogError("[BootStrap] _testScript 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_gameFlowController == null)
        {
            Debug.LogError("[BootStrap] _gameFlowController 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_waveController == null)
        {
            Debug.LogError("[BootStrap] _waveController 참조가 없습니다. Inspector에서 연결해주세요.", this);
            valid = false;
        }
        if (_spawner == null)
        {
            Debug.LogError("[BootStrap] _spawner가 없습니다. 생성·주입 코드를 확인해주세요.", this);
            valid = false;
        }
        return valid;
    }
}

using Game.Core;
using UnityEngine;

public class BootStrap : MonoBehaviour
{
    [SerializeField] private GameFlowController _gameFlowController;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _gameFlowController.BeginRun();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

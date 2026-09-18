using System;
using Game.Core;
using TMPro;
using UnityEngine;

public class TestPhaseViewer : MonoBehaviour
{
    [SerializeField] private GameFlowController _controller;
    private TextMeshProUGUI _text;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _text = GetComponent<TextMeshProUGUI>();
        if (_controller == null)
        {
            Debug.LogWarning("[TestPhaseViewer] : 인스펙터 참조가 없습니다. ");
            _text.text = "Can't Viewing Current Phase";
            return;
        }

        _controller.PhaseChanged += UpdatePhase;
        UpdatePhase(_controller.CurPhase);
    }

    private void OnDestroy()
    {
        _controller.PhaseChanged -= UpdatePhase;
    }

    private void UpdatePhase(GamePhase phase)
    {
        _text.text = $"Current Phase : {phase}";
    }
}

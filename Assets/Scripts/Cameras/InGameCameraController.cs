using System;
using Game.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Cameras
{
    public class InGameCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private CinemachineCamera _baseCamera;
        [SerializeField] private CinemachineCamera _battlefieldCamera;
        [SerializeField] private CinemachineCamera _focusCamera;
        [SerializeField] private GameFlowController _gameFlowController;

        [SerializeField] private Transform _focusTarget;

        private void Awake()
        {
            ShowBase();
        }

        // 테스트용 입력
        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!_gameFlowController.CanEnterBuildMode()) return;

            // 화면 전환 중인 위치를 클릭 대상으로 사용하지 않는다.
            if (_brain.IsBlending) return;

            // UI 버튼 클릭은 월드 확대에서 제외
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                ShowBase();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;

            Ray ray = _worldCamera.ScreenPointToRay(
                mouse.position.ReadValue());

            // 월드의 Z=0 평면과 클릭 광선의 교차점
            Plane ground = new Plane(Vector3.forward, Vector3.zero);

            if (ground.Raycast(ray, out float distance))
                FocusAt(ray.GetPoint(distance));
        }
        public void Toggle()
        {
            if (!_gameFlowController.CanEnterBuildMode()) return;

            if (_baseCamera.Priority > _battlefieldCamera.Priority)
                ShowBattleField();
            else
                ShowBase();
        }

        public void ShowBase()
        {
            _baseCamera.Priority = 10;
            _battlefieldCamera.Priority = 0;
            _focusCamera.Priority = -1;
        }

        public void ShowBattleField()
        {
            _battlefieldCamera.Priority = 10;
            _baseCamera.Priority = 0;
            _focusCamera.Priority = -1;
        }

        public void FocusAt(Vector3 worldPosition)
        {
            if (!_gameFlowController.CanEnterBuildMode()) return;

            // 기지 화면에서만 확대 허용
            if (_baseCamera.Priority <= _battlefieldCamera.Priority) return;

            // 현재 프로젝트는 XY 평면에 배치된 2D 게임
            _focusTarget.position = new Vector3(
                worldPosition.x, worldPosition.y, 0f);

            _focusCamera.Priority = 20;
        }
    }
}
using System;
using UnityEngine;

namespace Units
{
    public sealed class CastController
    {
        // ============================================================
        // Runtime State
        // ============================================================

        private float _remaining;


        private Action _onCompleted;


        // ============================================================
        // Properties
        // ============================================================

        public bool IsCasting { get; private set; }


        // ============================================================
        // Cast
        // ============================================================

        public void StartCast(
            float duration,
            Action onCompleted)
        {
            Cancel();

            _remaining = Mathf.Max(
                0f,
                duration
            );

            _onCompleted = onCompleted;

            IsCasting = true;

            if (_remaining <= 0f)
                Complete();
        }


        // ============================================================
        // Update
        // ============================================================

        // Unit_Combat에서 전달한 시간으로 진행하며, Cancel 이후에는 완료를 호출하지 않는다.
        public void Tick(
            float deltaTime)
        {
            if (!IsCasting)
                return;

            _remaining -= Mathf.Max(
                0f,
                deltaTime
            );

            if (_remaining <= 0f)
                Complete();
        }


        // ============================================================
        // Cancel
        // ============================================================

        public void Cancel()
        {
            IsCasting = false;

            _remaining = 0f;

            _onCompleted = null;
        }


        // ============================================================
        // Complete
        // ============================================================

        private void Complete()
        {
            var callback = _onCompleted;

            Cancel();

            callback?.Invoke();
        }
    }
}

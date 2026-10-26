using System;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>음원별 기본 음량과 반복·전환 시간을 보관한다. 사용자 전체 음량은 Mixer에서 적용한다.</summary>
    [Serializable]
    public sealed class BGMContext
    {
        // ============================================================
        // Track Settings
        // ============================================================
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float _pitch = 1f;
        [SerializeField] private bool _loop = true;
        [SerializeField, Min(0f)] private float _fadeInDuration = 1f;
        [SerializeField, Min(0f)] private float _fadeOutDuration = 1f;

        public AudioClip Clip => _clip;
        public float Volume => Mathf.Clamp01(_volume);
        public float Pitch => Mathf.Clamp(_pitch, 0.1f, 3f);
        public bool Loop => _loop;
        public float FadeInDuration => Mathf.Max(0f, _fadeInDuration);
        public float FadeOutDuration => Mathf.Max(0f, _fadeOutDuration);

        public BGMContext(bool loop = true) => _loop = loop;
    }
}

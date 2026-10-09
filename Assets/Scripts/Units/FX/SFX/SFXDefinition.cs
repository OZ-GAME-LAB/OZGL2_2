using System;
using Units.Skills;
using UnityEngine;
using UnityEngine.Audio;

namespace Units.FX
{
    [Serializable]
    public sealed class SFXDefinition
    {
        [SerializeField, Tooltip("카탈로그 분류용 권장 동작. 실제 실행 시점은 공격의 FX 설정을 따릅니다.")]
        private SkillFXOperation _operation;

        [SerializeField] private FXCatalogCategory _category;
        public FXCatalogCategory Category => _category;

        public SkillFXOperation Operation => _operation;

        [SerializeField] private string _key;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float _pitch = 1f;
        [SerializeField] private bool _loop;
        [SerializeField] private AudioMixerGroup _mixerGroup;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend;
        [SerializeField, Min(0.01f)] private float _minDistance = 1f;
        [SerializeField, Min(0.01f)] private float _maxDistance = 30f;
        [SerializeField, Min(0f)] private float _fadeOut = 0.05f;
        [SerializeField, Min(0.05f)] private float _maxLifetime = 0.3f;
        [SerializeField, Min(1)] private int _maxConcurrent = 8;
        [SerializeField, Min(0f)] private float _minInterval = 0.03f;

        public string Key => _key;
        public AudioClip Clip => _clip;
        public float Volume => Mathf.Clamp01(_volume);
        public float Pitch => Mathf.Clamp(_pitch, 0.1f, 3f);
        public bool Loop => _loop;
        public AudioMixerGroup MixerGroup => _mixerGroup;
        public float SpatialBlend => Mathf.Clamp01(_spatialBlend);
        public float MinDistance => Mathf.Max(0.01f, _minDistance);
        public float MaxDistance => Mathf.Max(MinDistance, _maxDistance);
        public float FadeOut => Mathf.Max(0f, _fadeOut);
        public float MaxLifetime => Mathf.Max(0.05f, _maxLifetime);
        public int MaxConcurrent => Mathf.Max(1, _maxConcurrent);
        public float MinInterval => Mathf.Max(0f, _minInterval);
    }
}

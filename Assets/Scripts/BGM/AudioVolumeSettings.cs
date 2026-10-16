using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>씬과 독립된 실행 중 BGM/SFX 음량을 보관한다. 디스크 저장과 설정 UI는 추후 연결한다.</summary>
    public static class AudioVolumeSettings
    {
        // ============================================================
        // Shared Settings
        // ============================================================
        public const string BGMParameter = "BGMVolume";
        public const string SFXParameter = "SFXVolume";
        public static float BGMVolume { get; private set; } = 1f;
        public static float SFXVolume { get; private set; } = 1f;
        private static AudioMixer _mixer;

        // 에디터의 Domain Reload 비활성화 상태에서도 새 실행은 기본값으로 시작한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            _mixer = null;
            BGMVolume = SFXVolume = 1f;
        }

        /// <summary>Start 이후 같은 Mixer를 연결하고, 씬 전환 전에 보관한 음량을 적용한다.</summary>
        public static void Bind(AudioMixer mixer)
        {
            if (mixer == null) return;
            if (_mixer != null && _mixer != mixer)
            {
                Debug.LogError("[AudioVolumeSettings] 모든 씬의 BGM/SFX에 같은 Mixer를 지정해주세요.");
                return;
            }
            _mixer = mixer;
            Apply(BGMParameter, BGMVolume);
            Apply(SFXParameter, SFXVolume);
        }

        // ============================================================
        // Settings UI / Save Integration
        // ============================================================
        /// <summary>설정 UI 또는 저장값 복원 시 호출한다. 0은 무음, 1은 원래 음량이다.</summary>
        public static void SetBGMVolume(float volume)
        {
            BGMVolume = Sanitize(volume);
            Apply(BGMParameter, BGMVolume);
        }

        /// <summary>SFX 음량만 변경하며 BGM 설정에는 영향을 주지 않는다.</summary>
        public static void SetSFXVolume(float volume)
        {
            SFXVolume = Sanitize(volume);
            Apply(SFXParameter, SFXVolume);
        }

        private static float Sanitize(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);

        private static void Apply(string parameter, float volume)
        {
            if (_mixer == null) return;
            float decibels = volume <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(volume));
            if (!_mixer.SetFloat(parameter, decibels))
                Debug.LogWarning($"[AudioVolumeSettings] Mixer 노출 파라미터가 없습니다: {parameter}");
        }
    }
}

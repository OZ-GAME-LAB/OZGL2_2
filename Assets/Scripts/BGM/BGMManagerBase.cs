using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>씬에 속한 두 AudioSource로 BGM을 재생하고 전환한다. 전역 인스턴스를 유지하지 않는다.</summary>
    [DisallowMultipleComponent]
    public abstract class BGMManagerBase : MonoBehaviour
    {
        // ============================================================
        // References / Runtime State
        // ============================================================
        [SerializeField, Tooltip("모든 씬에서 같은 GameAudioMixer의 BGM 그룹을 지정합니다.")]
        private AudioMixerGroup _mixerGroup;
        private readonly Voice[] _voices = new Voice[2];
        private BGMContext _requested;
        private BGMContext _pending;
        private bool _started;
        private bool _focusLost;
        private bool _applicationPaused;
        private bool _suspended;
        private bool _resumePending;

        private sealed class Voice
        {
            internal AudioSource Source;
            internal BGMContext Context;
            internal float From, Target, Elapsed, Duration;
            internal bool Fading;
            internal bool PausedByApplication;
        }

        protected AudioMixerGroup MixerGroup => _mixerGroup;
        protected bool IsApplicationSuspended => _suspended || _resumePending;

        public BGMContext CurrentContext => _requested;
        // 일시정지는 재생 완료가 아니다. 씬 전환 대기는 모든 채널이 정리된 뒤 완료한다.
        public bool IsSilent => _pending == null &&
            (_voices[0] == null || _voices[0].Context == null) &&
            (_voices[1] == null || _voices[1].Context == null);

        protected virtual void Awake()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                // 기존 AudioSource를 덮어쓰지 않고 이 매니저 소유의 채널만 생성한다.
                var child = new GameObject($"BGM Channel {i + 1}");
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = _mixerGroup;
                _voices[i] = new Voice { Source = source };
            }
        }

        protected virtual void Start()
        {
            _started = true;
            if (_mixerGroup != null) AudioVolumeSettings.Bind(_mixerGroup.audioMixer);
            else Debug.LogWarning("[BGM] 공통 BGM MixerGroup을 지정해주세요.", this);
        }

        // ============================================================
        // Playback / Crossfade
        // ============================================================
        /// <summary>같은 Context 요청은 유지한다. 자연 종료된 단발 곡을 다시 재생하려면 restart를 지정한다.</summary>
        public void Play(BGMContext context, bool restart = false)
        {
            if (!isActiveAndEnabled) return;
            if (context == null || context.Clip == null)
            {
                Debug.LogWarning("[BGM] 재생할 Context의 AudioClip을 지정해주세요.", this);
                return;
            }
            if (!restart && ReferenceEquals(_requested, context)) return;
            _requested = context;
            _pending = null;

            // 이전 곡으로 돌아오는 요청은 재생 중인 채널의 현재 볼륨에서 되돌린다.
            if (!restart)
            {
                foreach (var voice in _voices)
                {
                    if (voice.Context != context || (!voice.Source.isPlaying && !voice.PausedByApplication)) continue;
                    Fade(voice, context.Volume, context.FadeInDuration);
                    FadeOtherVoices(voice);
                    return;
                }
            }

            Voice free = FindFreeVoice();
            FadeOtherVoices(null);
            if (free != null) Begin(free, context);
            else
            {
                // 두 곡이 겹친 도중에는 채널을 강제로 끊지 않는다. 마지막 요청만 보관하고
                // 기존 채널 중 하나가 페이드아웃을 마치면 해당 채널에서 새 곡을 시작한다.
                _pending = context;
            }
        }

        /// <summary>재생 중인 모든 곡을 각자의 시간으로 페이드아웃한다. 씬 파괴 시에는 즉시 정리한다.</summary>
        public void Stop(bool immediate = false)
        {
            _requested = _pending = null;
            foreach (var voice in _voices)
            {
                if (voice == null) continue;
                if (immediate) Release(voice);
                else FadeOut(voice);
            }
        }

        private void Begin(Voice voice, BGMContext context)
        {
            voice.Context = context;
            voice.Source.clip = context.Clip;
            voice.Source.loop = context.Loop;
            voice.Source.pitch = context.Pitch;
            voice.Source.volume = 0f;
            voice.Source.Play();
            Fade(voice, context.Volume, context.FadeInDuration);
            // 백그라운드에서 새 요청이 들어와도 복귀 전까지 재생 위치를 진행하지 않는다.
            if (_suspended || _resumePending) PauseVoice(voice);
        }

        private Voice FindFreeVoice()
        {
            foreach (var voice in _voices)
                if (voice.Context == null) return voice;
            return null;
        }

        private void FadeOtherVoices(Voice keep)
        {
            foreach (var voice in _voices)
                if (voice != keep) FadeOut(voice);
        }

        private static void FadeOut(Voice voice)
        {
            if (voice.Context == null || (voice.Fading && voice.Target == 0f)) return;
            Fade(voice, 0f, voice.Context.FadeOutDuration);
        }

        private static void Fade(Voice voice, float target, float duration)
        {
            voice.From = voice.Source.volume;
            voice.Target = target;
            voice.Elapsed = 0f;
            voice.Duration = duration;
            voice.Fading = true;
            if (duration > 0f) return;
            voice.Source.volume = target;
            voice.Fading = false;
            if (target == 0f) Release(voice);
        }

        private void Update()
        {
            // timeScale에 영향받지 않는다. AudioListener 일시정지 중에는 음악과 페이드를 함께 멈춘다.
            if (_suspended || AudioListener.pause) return;
            if (_resumePending)
            {
                _resumePending = false;
                foreach (var voice in _voices)
                {
                    if (!voice.PausedByApplication) continue;
                    voice.PausedByApplication = false;
                    if (voice.Context != null) voice.Source.UnPause();
                }
                // 엔진의 복귀 처리가 끝난 Update에서 이어 재생한다. 복귀 첫 프레임의
                // 큰 unscaledDeltaTime으로 페이드가 건너뛰거나 종료로 오인되지 않게 한다.
                return;
            }
            foreach (var voice in _voices)
            {
                if (voice.Context == null) continue;
                if (!voice.Source.isPlaying) { Release(voice); continue; }
                if (!voice.Fading) continue;
                voice.Elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(voice.Elapsed / voice.Duration);
                voice.Source.volume = Mathf.Lerp(voice.From, voice.Target, progress);
                if (progress < 1f) continue;
                voice.Fading = false;
                if (voice.Target == 0f) Release(voice);
            }
            if (_pending == null) return;
            Voice free = FindFreeVoice();
            if (free == null) return;
            BGMContext next = _pending;
            _pending = null;
            Begin(free, next);
        }

        // ============================================================
        // Application Pause / Resume
        // ============================================================
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            _focusLost = !hasFocus;
            RefreshApplicationPause();
        }

        protected virtual void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            RefreshApplicationPause();
        }

        private void RefreshApplicationPause()
        {
            // 포커스와 앱 정지 알림은 순서가 다를 수 있으므로 두 사유가 모두 해제되어야 복귀한다.
            bool suspended = _focusLost || _applicationPaused;
            if (_suspended == suspended) return;
            _suspended = suspended;
            _resumePending = !suspended;
            if (!suspended) return;
            foreach (var voice in _voices) PauseVoice(voice);
        }

        private static void PauseVoice(Voice voice)
        {
            if (voice?.Context == null || voice.Source == null || voice.PausedByApplication) return;
            // 엔진이 먼저 정지시켜 isPlaying이 false여도 채널과 페이드 정보를 보존한다.
            // UnPause는 이미 자연 종료된 음원을 새로 재생하지 않는다.
            voice.Source.Pause();
            voice.PausedByApplication = true;
        }

        // ============================================================
        // Scene Lifetime
        // ============================================================
        protected virtual void OnEnable()
        {
            _focusLost = !Application.isFocused;
            RefreshApplicationPause();
            if (_started && _mixerGroup != null) AudioVolumeSettings.Bind(_mixerGroup.audioMixer);
        }

        protected virtual void OnDisable() => Stop(true);

        protected virtual void OnDestroy()
        {
            // 컴포넌트만 제거하는 경우에도 소유 채널이 씬에 남지 않게 정리한다.
            foreach (var voice in _voices)
                if (voice?.Source != null) Destroy(voice.Source.gameObject);
        }

        private static void Release(Voice voice)
        {
            if (voice.Source == null) return;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.volume = 0f;
            voice.Context = null;
            voice.Fading = false;
            voice.PausedByApplication = false;
        }
    }
}

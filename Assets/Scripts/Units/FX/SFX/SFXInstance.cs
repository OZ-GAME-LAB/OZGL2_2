using UnityEngine;
using Units.Skills;

namespace Units.FX
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SFXInstance : MonoBehaviour
    {
        private AudioSource _source;
        private SFXDefinition _definition;
        private FXPlayback _playback;
        private float _age, _fade;
        private bool _stopping;
        internal SkillFXRequest Request => _playback.Request;
        internal string Key => _definition.Key;

        internal void Play(SFXDefinition definition, SkillFXRequest request)
        {
            _source = GetComponent<AudioSource>();
            _definition = definition;
            _playback = new FXPlayback(request);
            _age = _fade = 0f;
            _stopping = false;
            _source.playOnAwake = false;
            _source.clip = definition.Clip;
            _source.volume = definition.Volume;
            _source.pitch = definition.Pitch;
            _source.loop = definition.Loop;
            _source.outputAudioMixerGroup = definition.MixerGroup;
            _source.spatialBlend = definition.SpatialBlend;
            _source.minDistance = definition.MinDistance;
            _source.maxDistance = definition.MaxDistance;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.dopplerLevel = 0f;
            _playback.Tick();
            transform.position = _playback.Position;
            gameObject.SetActive(true);
            _source.Play();
        }

        internal bool Tick(float delta)
        {
            _age += delta;
            if (!_playback.Tick())
            {
                if (Request.Entry.EndPolicy == SkillFXEndPolicy.ClearImmediately)
                    return true;
                if (Request.Entry.EndPolicy == SkillFXEndPolicy.Independent) _playback.Detach();
                else Stop();
            }
            transform.position = _playback.Position;
            if (_stopping)
            {
                _fade += delta;
                _source.volume = _definition.Volume * (1f - Mathf.Clamp01(_fade / Mathf.Max(0.001f, _definition.FadeOut)));
                if (_fade >= _definition.FadeOut) return true;
            }
            // AudioListener.pause 중에는 자연 종료로 판정하지 않는다.
            if (!AudioListener.pause && _age > 0.01f && !_source.isPlaying) return true;
            return Request.Entry.EndPolicy != SkillFXEndPolicy.KeepActive && _age >= _definition.MaxLifetime;
        }
        internal void Detach() => _playback.Detach();
        internal void Stop() { _stopping = true; _playback.Detach(); }
        internal void ResetPlayback()
        {
            _source.Stop();
            _source.clip = null;
            _source.outputAudioMixerGroup = null;
            _source.loop = false;
            _source.volume = _source.pitch = 1f;
            gameObject.SetActive(false);
            _playback = null;
            _definition = null;
        }
    }
}

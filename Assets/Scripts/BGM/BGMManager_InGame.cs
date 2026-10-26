using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>인게임에서 사용하는 일곱 음원과 게임 진행 연결.</summary>
    public sealed class BGMManager_InGame : BGMManagerBase
    {
        [Header("반복 BGM")]
        [SerializeField, InspectorName("건설 단계")] private BGMContext _construction = new();
        [SerializeField, InspectorName("일반 전투")] private BGMContext _battle = new();
        [SerializeField, InspectorName("보스 전투")] private BGMContext _boss = new();
        [SerializeField, InspectorName("이벤트")] private BGMContext _event = new();
        [Header("단발 음악")]
        [SerializeField, InspectorName("웨이브 시작")] private BGMContext _waveStart = new(false);
        [SerializeField, InspectorName("매 웨이브 승리")] private BGMContext _victory = new(false);
        [SerializeField, InspectorName("패배")] private BGMContext _defeat = new(false);

        private GameFlowController _flow;
        private AudioSource _cue;
        private bool _ready;
        private bool _cuePaused;

        protected override void Awake()
        {
            base.Awake();
            var child = new GameObject("Wave Start Channel");
            child.transform.SetParent(transform, false);
            _cue = child.AddComponent<AudioSource>();
            _cue.playOnAwake = false;
            _cue.loop = false;
            _cue.spatialBlend = 0f;
            _cue.outputAudioMixerGroup = MixerGroup;
        }

        protected override void Start()
        {
            base.Start();
            _ready = true;
            Bind(true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_ready) Bind();
        }

        private void Bind(bool initial = false)
        {
            _flow = FindFirstObjectByType<GameFlowController>();

            if (_flow == null)
            {
                Debug.LogWarning("[BGM] GameFlowController를 찾을 수 없습니다.", this);
                return;
            }

            _flow.PhaseChanged += HandlePhaseChanged;
            _flow.BattleResultConfirmed += PlayBattleResult;

            // 최초 연결 시 현재 페이즈에 맞는 음악을 적용한다.
            // BattlePreparing 상태라면 Wave Start를 한 번 재생한다.
            ApplyPhase(
                _flow.CurPhase,
                initial && _flow.CurPhase == GamePhase.BattlePreparing
            );
        }

        public void HandlePhaseChanged(GamePhase phase) => ApplyPhase(phase, true);

        private void ApplyPhase(GamePhase phase, bool announce)
        {
            switch (phase)
            {
                case GamePhase.None:
                    Stop();
                    StopCue();
                    break;

                case GamePhase.Preparation:
                    StopCue();
                    Play(_construction);
                    break;

                case GamePhase.BattlePreparing:
                    Stop();
                    StopCue();

                    if (announce)
                        PlayCue();

                    break;

                case GamePhase.Battle:
                    StopCue();

                    Play(
                        _flow != null &&
                        _flow.CurrentNode?.BattleType == WaveBattleType.Boss
                            ? _boss
                            : _battle
                    );

                    break;

                case GamePhase.Event:
                    Play(_event);
                    break;

                case GamePhase.BattleResolving:
                    if (!announce &&
                        _flow != null &&
                        _flow.LastBattleResult.HasValue)
                    {
                        PlayBattleResult(_flow.LastBattleResult.Value);
                    }
                    break;

                    // 보상/상점/결산에는 직전 음악을 유지한다.
            }
        }

        public void PlayBattleResult(ResultType result)
        {
            StopCue();
            Play(result == ResultType.Victory ? _victory : _defeat, true);
        }

        private void PlayCue()
        {
            StopCue();
            if (_waveStart.Clip == null) return;
            _cue.clip = _waveStart.Clip;
            _cue.volume = _waveStart.Volume;
            _cue.pitch = _waveStart.Pitch;
            _cue.Play();
            RefreshCuePause();
        }

        private void LateUpdate() => RefreshCuePause();

        private void RefreshCuePause()
        {
            if (_cue == null) return;
            if (IsApplicationSuspended && !_cuePaused)
            {
                _cue.Pause();
                _cuePaused = true;
            }
            else if (!IsApplicationSuspended && _cuePaused)
            {
                _cue.UnPause();
                _cuePaused = false;
            }
        }

        private void StopCue()
        {
            if (_cue != null) { _cue.Stop(); _cue.clip = null; }
            _cuePaused = false;
        }

        protected override void OnDisable()
        {
            if (_flow != null)
            {
                _flow.PhaseChanged -= HandlePhaseChanged;
                _flow.BattleResultConfirmed -= PlayBattleResult;
            }
            _flow = null;
            StopCue();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (_cue != null) Destroy(_cue.gameObject);
            base.OnDestroy();
        }
    }
}


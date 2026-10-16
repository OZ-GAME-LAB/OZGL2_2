using UnityEngine;

namespace Game.Audio
{
    /// <summary>MainScreen과 OutGame 씬에서 각각 지정한 음악을 씬 수명 동안 재생한다.</summary>
    public sealed class BGMManager_OutGame : BGMManagerBase
    {
        [SerializeField] private BGMContext _music = new();
        private bool _started;

        // ============================================================
        // Scene Playback
        // ============================================================
        protected override void Start()
        {
            base.Start();
            _started = true;
            Play(_music);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_started) Play(_music);
        }
    }
}

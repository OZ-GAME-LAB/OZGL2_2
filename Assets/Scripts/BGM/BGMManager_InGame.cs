using System;
using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>외부에서 전달받은 페이즈와 승패에 따라 음악을 선택한다. Flow 연결은 아직 활성화하지 않는다.</summary>
    public sealed class BGMManager_InGame : BGMManagerBase
    {
        // ============================================================
        // Phase / Result Settings
        // ============================================================
        public enum PlaybackAction { KeepCurrent, Play, Stop }

        [Serializable]
        private sealed class PhaseMusic
        {
            [SerializeField] private GamePhase _phase;
            [SerializeField] private PlaybackAction _action;
            [SerializeField] private BGMContext _music = new();

            internal GamePhase Phase => _phase;
            internal PlaybackAction Action => _action;
            internal BGMContext Music => _music;

            internal PhaseMusic(GamePhase phase, PlaybackAction action)
            { _phase = phase; _action = action; }
        }

        [SerializeField] private PhaseMusic[] _phaseMusic =
        {
            new(GamePhase.None, PlaybackAction.Stop),
            new(GamePhase.Preparation, PlaybackAction.Play),
            new(GamePhase.BattlePreparing, PlaybackAction.KeepCurrent),
            new(GamePhase.Battle, PlaybackAction.Play),
            new(GamePhase.BattleResolving, PlaybackAction.KeepCurrent),
            new(GamePhase.QuarterComplete, PlaybackAction.KeepCurrent),
            new(GamePhase.Reward, PlaybackAction.KeepCurrent),
            new(GamePhase.Event, PlaybackAction.KeepCurrent),
            new(GamePhase.Store, PlaybackAction.KeepCurrent),
            new(GamePhase.Finished, PlaybackAction.KeepCurrent)
        };
        [SerializeField] private BGMContext _victory = new(false);
        [SerializeField] private BGMContext _defeat = new(false);

        // ============================================================
        // Playback Requests
        // ============================================================
        /// <summary>페이즈 정책을 적용한다. 목록에 없는 페이즈는 기존 음악을 유지한다.</summary>
        public void HandlePhaseChanged(GamePhase phase)
        {
            if (_phaseMusic == null) return;
            foreach (var entry in _phaseMusic)
            {
                if (entry == null || entry.Phase != phase) continue;
                switch (entry.Action)
                {
                    case PlaybackAction.Play: Play(entry.Music); break;
                    case PlaybackAction.Stop: Stop(); break;
                }
                return;
            }
        }

        /// <summary>확정된 승패를 전달받는다. 승리·패배 Context의 Loop를 끄면 한 번만 재생한다.</summary>
        public void PlayBattleResult(ResultType result)
        {
            switch (result)
            {
                case ResultType.Victory: Play(_victory); break;
                case ResultType.Defeat: Play(_defeat); break;
            }
        }

        // ============================================================
        // Flow Integration (연결 시 활성화)
        // ============================================================
        // 현재는 GameFlowController와 씬을 수정하지 않는다.
        // 추후 Flow 참조를 주입한 뒤 아래 구독과 해제를 수명에 맞춰 함께 활성화한다.
        // OnEnable:  flow.PhaseChanged += HandlePhaseChanged;
        // 초기 동기화: HandlePhaseChanged(flow.CurPhase);
        // OnDisable: flow.PhaseChanged -= HandlePhaseChanged;
        //
        // PhaseChanged(GamePhase)만으로는 승리와 패배를 구분할 수 없다.
        // Flow에서 결과 확정 후 BattleResolving 진입 직전에 다음을 호출한다.
        // bgmManager.PlayBattleResult(result);
        // 런 포기 시에도 ResultType.Defeat를 전달한다.
        // 늦은 활성화 시 결과 복원이 필요하면 Flow에 현재 결과 보관 기능을 추가한다.
        // 씬 전환 페이드가 필요하면 Stop() 후 IsSilent가 될 때까지 기다린 뒤 언로드한다.
    }
}

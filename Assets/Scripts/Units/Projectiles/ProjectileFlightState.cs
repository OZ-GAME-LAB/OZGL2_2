using System;
using System.Collections.Generic;
using Units.Skills;
using UnityEngine;

// 한 발의 명중·적용·종료를 기록하고 지연 Once 및 FX 정리를 한 번만 통지한다.
namespace Units
{
    // 투사체 1발의 일회 종료/명중 기록. 마지막 요청 참조가 해제되면 실행 횟수 기록도 회수된다.
    public sealed class ProjectileFlightState
    {

        // ============================================================
        // Data / Runtime State
        // ============================================================

        private readonly HashSet<(int, int)> _hits = new();
        private readonly HashSet<(int, int)> _damageFXHits = new();

        private Action<bool> _completed;

        private Action _applied;

        private SkillEffectBatch _batch;

        private CombatEventMetadata _metadata;

        private Vector2 _position;
        private Vector2 _direction;
        private float _attackRadius;
        internal void SetFXRadius(float radius) => _attackRadius = Mathf.Max(0f, radius);
        private Transform _followTarget;
        private Units.FX.FXScope _fxScope;
        private IReadOnlyList<SkillFXEntry> _fxEntries;

        public void ConfigureFX(IReadOnlyList<SkillFXEntry> entries, CombatEventMetadata metadata)
        {
            _fxEntries = entries;
            _metadata = metadata;
            _fxScope = Units.FX.FXScope.Projectile(metadata);
        }

        public void BindFX(Transform followTarget, Vector2 direction)
        {
            _followTarget = followTarget;
            _direction = direction;
            if (_batch != null)
            {
                _batch.FXFollowTarget = followTarget;
                _batch.FXDirection = direction;
            }
        }

        // ============================================================
        // Execution
        // ============================================================

        internal void Configure(
            SkillEffectBatch batch,
            CombatEventMetadata metadata)
        {
            _batch = batch;

            _metadata = metadata;
            ConfigureFX(batch?.FXEntries, metadata);
            if (batch != null) batch.FXScope = _fxScope;
        }

        // ============================================================
        // Notification
        // ============================================================

        public void NotifyFX(
            SkillFXHook hook,
            Vector2 position,
            CombatTargetSnapshot target = default)
        {
            if (Ended)
                return;

            _position = position;

            var batch = _batch;

            if (_fxEntries == null)
                return;

            foreach (var fx in _fxEntries)
            {
                if (Ended)
                    break;

                if (fx != null && fx.Hook == hook)
                    Units.FX.UnitFXBridge.Dispatch(new SkillFXRequest(fx, _metadata, position,
                        direction: _direction, target: target, followTarget: _followTarget, scope: _fxScope, attackRadius: _attackRadius));
            }
        }

        // ============================================================
        // Properties
        // ============================================================

        public bool Ended { get; private set; }

        public bool Applied { get; private set; }

        // ============================================================
        // Constructor
        // ============================================================

        public ProjectileFlightState(
            Action<bool> completed = null,
            Action applied = null)
        {
            _completed = completed;

            _applied = applied;
        }

        // ============================================================
        // Execution
        // ============================================================

        public bool Enter(CombatTargetSnapshot target) => !Ended && _hits.Add((target.ObjectId, target.LifetimeVersion));

        // 충돌 폭발은 Controller의 Collision 경로에서 한 번 재생한다.
        // 여기서는 실제 피해 결과만 대상별 OnHit으로 전달한다. 여러 피해 효과도 한 발당 한 번이다.
        internal void RecordImpactResult(CombatApplicationResult result)
        {
            if (result == null || Ended) return;
            Record(result);
            if (Ended || result.Kind != CombatApplicationKind.Damage || !result.WasApplied
                || (result.HpDamage <= 0f && result.ShieldAbsorbed <= 0f)) return;

            var target = result.Target;
            if (_damageFXHits.Add((target.ObjectId, target.LifetimeVersion)))
                NotifyFX(SkillFXHook.OnHit, target.Position, target);
        }

        // 첫 실제 적용만 알리므로 Once 패시브가 거절된 타격에 소모되지 않는다.
        public void Record(CombatApplicationResult result)
        {
            if (!Ended && !Applied && result.WasApplied)
            {
                Applied = true;

                var callback = _applied;

                _applied = null;

                callback?.Invoke();
            }
        }

        // ============================================================
        // Cleanup
        // ============================================================

        // 충돌·소멸·풀 반환이 겹쳐도 정리와 완료 알림은 한 번만 보낸다.
        public void Complete()
        {
            if (Ended)
                return;

            Ended = true;

            _applied = null;

            var callback = _completed;

            _completed = null;

            _hits.Clear();
            _damageFXHits.Clear();

            var batch = _batch;

            _batch = null;

            try
            {
                batch?.CleanupConditionalFX();

                if (_fxEntries != null)
                    foreach (var fx in _fxEntries)
                        if (fx != null)
                            Units.FX.UnitFXBridge.Dispatch(new SkillFXRequest(fx, _metadata, _position, true,
                                direction: _direction, followTarget: _followTarget, scope: _fxScope,
                                cleanupReason: Units.FX.FXCleanupReason.ScopeEnded));
                _fxEntries = null;
                _followTarget = null;
            }
            finally
            {
                callback?.Invoke(Applied);
            }
        }
    }
}

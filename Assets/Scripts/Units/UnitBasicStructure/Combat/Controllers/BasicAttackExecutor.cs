using System.Collections.Generic;
using Units.Skills;
using UnityEngine;
using System;


namespace Units
{
    public class BasicAttackExecutor
    {
        // ============================================================
        // References
        // ============================================================

        private readonly Unit_Core _core;

        private readonly TargetResolver _targetResolver;


        // ============================================================
        // Data
        // ============================================================

        private readonly BasicAttackData _data;


        // ============================================================
        // Runtime State
        // ============================================================

        private int _executionId;
        private CombatEventMetadata _fxMetadata;
        private SkillFXEntry[] _fxEntries = Array.Empty<SkillFXEntry>();
        private Vector2 _fxDirection;
        public event Action<SkillFXRequest> FXRequested;

        private void EmitFX(SkillFXHook hook, Vector2 position, CombatTargetSnapshot target = default, bool cleanup = false)
        {
            foreach (var entry in _fxEntries)
                if (entry != null && (cleanup || entry.Hook == hook))
                {
                    var request = new SkillFXRequest(entry, _fxMetadata, position, cleanup, _fxDirection, target);
                    if (FXRequested == null) continue;
                    foreach (Action<SkillFXRequest> listener in FXRequested.GetInvocationList())
                    {
                        // 연출 수신자의 오류가 실제 공격 판정을 중단하지 않게 한다.
                        try { listener(request); }
                        catch (Exception exception) { Debug.LogException(exception); }
                    }
                }
        }


        // ============================================================
        // Runtime Buffer
        // ============================================================

        private readonly List<ICombatTarget> _singleTargetBuffer;

        private readonly List<ICombatTarget> _projectileTargetBuffer;


        // ============================================================
        // Constructor
        // ============================================================

        public BasicAttackExecutor(
            Unit_Core core,
            BasicAttackData data,
            TargetResolver targetResolver)
        {
            _core =
                core;

            _data =
                data;

            _targetResolver =
                targetResolver;


            _singleTargetBuffer =
                new List<ICombatTarget>(
                    1
                );


            _projectileTargetBuffer =
                new List<ICombatTarget>();
        }


        // ============================================================
        // Execute
        // ============================================================

        public void Execute(
            ICombatTarget target,
            Action onCompleted)
        {
            int executionId = ++_executionId;
            bool fxStarted = false;

            try
            {
                if (_core == null ||
                    _core.RuntimeStatus == null ||
                    _data == null ||
                    !CombatTargetUtility.IsValid(target))
                {
                    return;
                }

                _core.SetFacingDirection((Vector2)target.Transform.position - (Vector2)_core.transform.position);

                _core.PlayAnimation_Attack();

                var root = CombatEventMetadata.Create(_core.CombatTarget);
                _fxMetadata = CombatEventMetadata.Create(_core.CombatTarget, root, executionId: root.EventId, actionIndex: 0);
                _fxDirection = (Vector2)target.Transform.position - (Vector2)_core.transform.position;
                var mapping = _core.GetComponent<Units.FX.UnitFXBridge>()?.BasicAttackMapping
                    ?? Units.FX.VFXManager.Instance?.BasicAttackMapping;
                _fxEntries = mapping != null ? mapping.Capture(_data.AttackFXType, _data.HitFXType) : Array.Empty<SkillFXEntry>();
                fxStarted = true;
                EmitFX(SkillFXHook.OnStart, _core.transform.position, new CombatTargetSnapshot(target));
                if (_data.ExecutionType == BasicAttackExecutionType.Direct)
                    EmitFX(SkillFXHook.Fire, _core.transform.position, new CombatTargetSnapshot(target));

                switch (_data.ExecutionType)
                {
                    case BasicAttackExecutionType.Direct:

                        ExecuteDirect(
                            target
                        );

                        break;


                    case BasicAttackExecutionType.Projectile:

                        ExecuteProjectile(
                            target
                        );

                        break;
                }
            }
            finally
            {
                if (fxStarted && executionId == _executionId && _core != null)
                {
                    EmitFX(SkillFXHook.OnComplete, _core.transform.position);
                    EmitFX(SkillFXHook.OnComplete, _core.transform.position, cleanup: true);
                }
                // 공격 행동은 투사체 명중을 기다리지 않고 발사 직후 완료한다.
                if (executionId == _executionId)
                {
                    onCompleted?.Invoke();
                }
            }
        }


        public void Cancel()
        {
            _executionId++;
        }


        // ============================================================
        // Direct
        // ============================================================

        private void ExecuteDirect(
            ICombatTarget target)
        {
            switch (_data.AreaType)
            {
                case BasicAttackAreaType.Single:

                    ExecuteSingle(
                        target
                    );

                    break;


                case BasicAttackAreaType.TargetCircle:

                    ExecuteTargetCircle(
                        target
                    );

                    break;


                case BasicAttackAreaType.SelfCircle:

                    ExecuteSelfCircle();

                    break;


                case BasicAttackAreaType.SelfCone:

                    ExecuteSelfCone(
                        target
                    );

                    break;
            }
        }


        // ============================================================
        // Single
        // ============================================================

        private void ExecuteSingle(
            ICombatTarget target)
        {
            _singleTargetBuffer.Clear();


            _singleTargetBuffer.Add(
                target
            );


            RequestDamage(
                _singleTargetBuffer
            );


            // TODO:
            // Attack FX 실행
            // 공통 EmitFX 경로에서 처리한다.

            // TODO:
            // Hit FX 실행
            // 실제 피해 결과 또는 투사체 명중 경로에서 처리한다.
        }


        // ============================================================
        // Target Circle
        // ============================================================

        private void ExecuteTargetCircle(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            Vector2 center =
                target.Transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxDamageableCount,
                    HitAreaType.Circle,
                    GetEnemyTeam()
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Attack FX 실행
            // 공통 EmitFX 경로에서 처리한다.

            // TODO:
            // Hit FX 실행
            // 실제 피해 결과 또는 투사체 명중 경로에서 처리한다.
        }


        // ============================================================
        // Self Circle
        // ============================================================

        private void ExecuteSelfCircle()
        {
            if (_targetResolver == null)
                return;


            Vector2 center =
                _core.transform.position;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    center,
                    Vector2.zero,
                    _data.AreaRadius,
                    0f,
                    _data.MaxDamageableCount,
                    HitAreaType.Circle,
                    GetEnemyTeam()
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Attack FX 실행
            // 공통 EmitFX 경로에서 처리한다.

            // TODO:
            // Hit FX 실행
            // 실제 피해 결과 또는 투사체 명중 경로에서 처리한다.
        }


        // ============================================================
        // Self Cone
        // ============================================================

        private void ExecuteSelfCone(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            Vector2 origin =
                _core.transform.position;


            Vector2 direction =
                (
                    (Vector2)target.Transform.position
                    - origin
                ).normalized;


            TargetHitRequest hitRequest =
                new TargetHitRequest(
                    origin,
                    direction,
                    _data.AreaRadius,
                    _data.AreaAngle,
                    _data.MaxDamageableCount,
                    HitAreaType.Cone,
                    GetEnemyTeam()
                );


            IReadOnlyList<ICombatTarget> targets =
                _targetResolver.ResolveHitTargets(
                    hitRequest
                );


            RequestDamage(
                targets
            );


            // TODO:
            // Attack FX 실행
            // 공통 EmitFX 경로에서 처리한다.

            // TODO:
            // Hit FX 실행
            // 실제 피해 결과 또는 투사체 명중 경로에서 처리한다.
        }


        // ============================================================
        // Projectile
        // ============================================================

        private void ExecuteProjectile(
            ICombatTarget target)
        {
            if (_targetResolver == null)
                return;


            ProjectileImpactType impactType =
                _data.AreaType == BasicAttackAreaType.Single
                    ? ProjectileImpactType.Single
                    : _data.AreaType == BasicAttackAreaType.SelfCone
                        ? ProjectileImpactType.Cone
                        : ProjectileImpactType.Circle;

            // Projectile의 SelfCircle / SelfCone도 충돌 위치에서 범위를 판정한다.

            Vector2 origin =
                _core.transform.position;


            _projectileTargetBuffer.Clear();


            // 현재 배정된 Target은 항상 첫 번째 발사 대상으로 사용한다.
            _projectileTargetBuffer.Add(
                target
            );


            if (_data.MaxTargetCount > 1)
            {
                TargetCandidateRequest candidateRequest =
                    new TargetCandidateRequest(
                        origin,
                        _data.BasicAttackRange,
                        GetEnemyTeam()
                    );


                IReadOnlyList<ICombatTarget> candidates =
                    _targetResolver.ResolveCandidates(
                        candidateRequest
                    );


                for (int i = 0;
                     i < candidates.Count
                     && _projectileTargetBuffer.Count < _data.MaxTargetCount;
                     i++)
                {
                    ICombatTarget candidate =
                        candidates[i];


                    if (candidate == target)
                        continue;


                    _projectileTargetBuffer.Add(
                        candidate
                    );
                }
            }


            DamageRequest damageRequest =
                new DamageRequest(
                    _core.CombatTarget,
                    null,
                    DamageSourceType.BasicAttack,
                    _data.DamageType,
                    1f,
                    _fxMetadata
                );


            // 목표마다 1발씩 발사하고, 각 투사체의 광역 피해 인원은 별도로 제한한다.
            for (int i = 0;
                 i < _projectileTargetBuffer.Count;
                 i++)
            {
                ProjectileRequest request =
                    new ProjectileRequest(
                        _core.CombatTarget,
                        _projectileTargetBuffer[i],
                        origin,
                        _data.ProjectileSpeed,
                        impactType,
                        _data.AreaRadius,
                        _data.AreaAngle,
                        impactType == ProjectileImpactType.Single
                            ? 1
                            : _data.MaxDamageableCount,
                        damageRequest,
                        flight: CreateFlight()
                    );


                if (ProjectileManager.GetOrCreate().Fire(request))
                {
                    _fxDirection = (Vector2)_projectileTargetBuffer[i].Transform.position - origin;
                    EmitFX(SkillFXHook.Fire, origin, new CombatTargetSnapshot(_projectileTargetBuffer[i]));
                }
            }


            // 목표마다 생성된 ProjectileRequest를 ProjectileManager에 전달한다.
            // DamageRequest는 발사 시점의 공격 정보를 보관하고,
            // 실제 피해 대상은 Projectile 충돌 시 ImpactType에 따라 확정된다.
            //
            // Single
            // → 충돌 대상을 DamageRequest의 대상으로 사용
            //
            // Circle / Cone
            // → 충돌 위치 기준 범위 판정 후 DamageRequest의 대상으로 사용


            // TODO:
            // Attack FX 실행
            // 공통 EmitFX 경로에서 처리한다.
        }


        // ============================================================
        // Target Team
        // ============================================================

        private ProjectileFlightState CreateFlight()
        {
            var flight = new ProjectileFlightState();
            flight.ConfigureFX(_fxEntries, _fxMetadata);
            return flight;
        }

        private UnitTeam GetEnemyTeam()
        {
            return _core.Team == UnitTeam.Ally
                ? UnitTeam.Enemy
                : UnitTeam.Ally;
        }


        // ============================================================
        // Damage
        // ============================================================

        private void RequestDamage(
            IReadOnlyList<ICombatTarget> targets)
        {
            if (targets == null)
                return;

            if (targets.Count <= 0)
                return;

            if (DamageResolver.Instance == null)
            {
                Debug.LogError(
                    "[BasicAttackExecutor] DamageResolver가 존재하지 않습니다."
                );

                return;
            }


            DamageRequest request =
                new DamageRequest(
                    _core.CombatTarget,
                    targets,
                    DamageSourceType.BasicAttack,
                    _data.DamageType,
                    1f,
                    _fxMetadata
                );


            foreach (var result in DamageResolver.Instance.ResolveWithResults(request))
                if (result.Status != CombatApplicationStatus.Invalid)
                    EmitFX(SkillFXHook.OnHit, result.Target.Position, result.Target);
        }
    }
}

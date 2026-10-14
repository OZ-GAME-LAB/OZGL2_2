using System;
using UnityEngine;
using System.Collections;



namespace Units
{
    public class Unit_Life : MonoBehaviour
    {

        public const float DeathDisappearDelay = 2f;

        private const float ShieldLimitMultiplier = 2f;


        // ============================================================
        // Reference
        // ============================================================

        private Unit_Core _core;


        // ============================================================
        // Runtime Data
        // ============================================================

        private float _currentHp;

        private float _currentShield;

        private bool _isDead;

        // 사망 직전 활성 상태를 저장해 풀 재사용 시 원래 설정만 복원한다.
        private readonly System.Collections.Generic.List<Collider2D> _deathColliders = new();
        private readonly System.Collections.Generic.List<Rigidbody2D> _deathBodies = new();

        private int _damageDepth;

        private int _lifeGeneration;

        private DamageResult? _lethalCause;

        private CombatApplicationResult _lethalApplication;

        public CombatDeathResult LastDeath { get; private set; }


        // ============================================================
        // Properties
        // ============================================================

        public float CurrentHp => _currentHp;

        public float CurrentShield => _currentShield;

        public float MaxHp => _core != null && _core.RuntimeStatus != null ? _core.RuntimeStatus.MaxHp : 0f;

        public float ShieldLimit => MaxHp * ShieldLimitMultiplier;

        public bool IsDead => _isDead;


        // ============================================================
        // Events
        // ============================================================

        public event Action<float, float> HpChanged;

        public event Action<float, float> ShieldChanged;

        public event Action<DamageResult> Damaged;

        // G4에서 처치 패시브에 연결할 확정 사망 결과. 기존 Damaged를 중복 발행하지 않는다.
        public event Action<CombatDeathResult> DeathConfirmed;

        public event Action<float> Healed;

        public event Action<float> ShieldAdded;


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnEnable()
        {
            SubscribeRuntimeStatusEvents();
        }

        private void OnDisable()
        {
            UnsubscribeRuntimeStatusEvents();
        }


        // ============================================================
        // Initialize
        // ============================================================

        public void Initialize(Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError($"[Unit_Life] {name} : Unit_Core가 없습니다.");

                return;
            }

            UnsubscribeRuntimeStatusEvents();

            _lifeGeneration++;

            _damageDepth = 0;

            _lethalCause = null;

            _lethalApplication = null;

            LastDeath = null;

            StopAllCoroutines();

            _core = core;

            RestoreDeathPhysics();

            SubscribeRuntimeStatusEvents();

            _currentHp = MaxHp;

            _currentShield = 0f;

            _isDead = false;

            HpChanged?.Invoke(_currentHp, _currentHp);

            ShieldChanged?.Invoke(_currentShield, _currentShield);
        }


        // ============================================================
        // Damage
        // ============================================================

        public void TakeDamage(DamageResult result)
        {
            TakeDamageWithResult(result);
        }

        public CombatApplicationResult TakeDamageWithResult(DamageResult result)
        {
            var target = _core != null ? _core.CombatTarget : null;

            if (_isDead || !result.TargetSnapshot.MatchesLifetime || !ReferenceEquals(target, result.Target) || float.IsNaN(result.Damage) || float.IsInfinity(result.Damage))
            {
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Damage,
                    target,
                    result.Metadata,
                    "Invalid damage target or amount"
                );
            }

            var applied = new CombatApplicationResult(
                CombatApplicationKind.Damage,
                target,
                result.Damage,
                result.Metadata
            );

            if (result.Damage <= 0f)
                return applied;

            int generation = _lifeGeneration;

            _damageDepth++;

            using (CombatEventContext.Enter(result.Metadata))
            {
                try
                {
                    float remainingDamage = result.Damage;

                    ApplyShieldDamage(ref remainingDamage, applied);

                    if (generation != _lifeGeneration)
                        return applied;

                    ApplyHpDamage(
                        remainingDamage,
                        result,
                        applied
                    );

                    if (generation != _lifeGeneration)
                        return applied;

                    Damaged?.Invoke(result);
                }
                finally
                {
                    if (generation == _lifeGeneration)
                    {
                        _damageDepth--;

                        if (_damageDepth == 0 && !_isDead && _currentHp <= 0f && _lethalCause.HasValue)
                        {
                            var death = new CombatDeathResult(new CombatTargetSnapshot(target), _lethalCause.Value);

                            LastDeath = death;

                            if (_lethalApplication != null)
                                _lethalApplication.Death = death;

                            using (CombatEventContext.Enter(death.Metadata))
                            {
                                Die();

                                if (generation == _lifeGeneration)
                                {
                                    DeathConfirmed?.Invoke(death);

                                    if (generation == _lifeGeneration)
                                        _core?.NotifyDeathConfirmed(death);
                                }
                            }
                        }
                    }
                }
            }

            return applied;
        }

        private void ApplyShieldDamage(
            ref float remainingDamage,
            CombatApplicationResult applied)
        {
            if (_currentShield <= 0f || remainingDamage <= 0f)
                return;

            float previousShield = _currentShield;

            float shieldDamage = Mathf.Min(_currentShield, remainingDamage);

            _currentShield -= shieldDamage;

            remainingDamage -= shieldDamage;

            applied.ShieldAbsorbed = shieldDamage;

            applied.Status = CombatApplicationStatus.Applied;

            ShieldChanged?.Invoke(previousShield, _currentShield);
        }

        private void ApplyHpDamage(
            float damage,
            DamageResult cause,
            CombatApplicationResult applied)
        {
            if (damage <= 0f)
                return;

            float previousHp = _currentHp;

            _currentHp = Mathf.Max(0f, _currentHp - damage);

            applied.HpDamage = previousHp - _currentHp;

            if (applied.HpDamage > 0f)
                applied.Status = CombatApplicationStatus.Applied;

            // 실제로 양수 HP를 0으로 만든 사건을 보관한다. 이미 0인 대상의 후속 피해가 훔치지 않는다.
            if (previousHp > 0f && _currentHp <= 0f)
            {
                _lethalCause = cause;

                _lethalApplication = applied;
            }

            HpChanged?.Invoke(previousHp, _currentHp);
        }


        // ============================================================
        // Heal
        // ============================================================

        public void Heal(float amount)
        {
            HealWithResult(amount, CombatEventMetadata.Create(_core != null ? _core.CombatTarget : null));
        }

        public CombatApplicationResult HealWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            var target = _core != null ? _core.CombatTarget : null;

            if (_isDead || float.IsNaN(amount) || float.IsInfinity(amount))
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Heal,
                    target,
                    metadata,
                    "Invalid life state or amount"
                );

            var applied = new CombatApplicationResult(
                CombatApplicationKind.Heal,
                target,
                amount,
                metadata
            );

            if (amount <= 0f || _currentHp >= MaxHp)
                return applied;

            int generation = _lifeGeneration;

            using (CombatEventContext.Enter(metadata))
            {
                float previous = _currentHp;

                _currentHp = Mathf.Min(MaxHp, _currentHp + amount);

                applied.HealedAmount = _currentHp - previous;

                if (applied.HealedAmount <= 0f)
                    return applied;

                applied.Status = CombatApplicationStatus.Applied;

                if (_currentHp > 0f)
                {
                    _lethalCause = null;

                    _lethalApplication = null;
                }

                HpChanged?.Invoke(previous, _currentHp);

                if (generation == _lifeGeneration)
                    Healed?.Invoke(applied.HealedAmount);
            }

            return applied;
        }


        // ============================================================
        // Shield
        // ============================================================

        public void AddShield(float amount)
        {
            AddShieldWithResult(amount, CombatEventMetadata.Create(_core != null ? _core.CombatTarget : null));
        }

        public CombatApplicationResult AddShieldWithResult(
            float amount,
            CombatEventMetadata metadata)
        {
            var target = _core != null ? _core.CombatTarget : null;

            if (_isDead || float.IsNaN(amount) || float.IsInfinity(amount))
                return CombatApplicationResult.Invalid(
                    CombatApplicationKind.Shield,
                    target,
                    metadata,
                    "Invalid life state or amount"
                );

            var applied = new CombatApplicationResult(
                CombatApplicationKind.Shield,
                target,
                amount,
                metadata
            );

            if (amount <= 0f || _currentShield >= ShieldLimit)
                return applied;

            int generation = _lifeGeneration;

            using (CombatEventContext.Enter(metadata))
            {
                float previous = _currentShield;

                _currentShield = Mathf.Min(ShieldLimit, _currentShield + amount);

                applied.ShieldAdded = _currentShield - previous;

                if (applied.ShieldAdded <= 0f)
                    return applied;

                applied.Status = CombatApplicationStatus.Applied;

                ShieldChanged?.Invoke(previous, _currentShield);

                if (generation == _lifeGeneration)
                    ShieldAdded?.Invoke(applied.ShieldAdded);
            }

            return applied;
        }


        // ============================================================
        // Death
        // ============================================================

        private void Die()
        {
            if (_isDead)
                return;

            _isDead = true;

            DisableDeathPhysics();

            Debug.Log($"[Unit_Life] {name} 사망");

            int generation = _lifeGeneration;

            _core?.NotifyKillAttributed(LastDeath);

            if (generation != _lifeGeneration)
                return;

            _core?.NotifyDeath();

            if (generation != _lifeGeneration)
                return;

            StartCoroutine(DisableAfterDeath());
        }


        private void DisableDeathPhysics()
        {
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                if (!collider.enabled || collider.GetComponentInParent<Unit_Core>() != _core)
                    continue;

                _deathColliders.Add(collider);
                collider.enabled = false;
            }

            foreach (var body in GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (!body.simulated || body.GetComponentInParent<Unit_Core>() != _core)
                    continue;

                _deathBodies.Add(body);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
        }

        private void RestoreDeathPhysics()
        {
            foreach (var collider in _deathColliders)
                if (collider != null) collider.enabled = true;

            foreach (var body in _deathBodies)
                if (body != null) body.simulated = true;

            _deathColliders.Clear();
            _deathBodies.Clear();
        }

        // 사망 연출이 끝난 현재 수명만 비활성화하고 풀에 반환한다.
        private IEnumerator DisableAfterDeath()
        {
            int generation = _lifeGeneration;

            yield return new WaitForSeconds(DeathDisappearDelay);

            if (generation != _lifeGeneration)
                yield break;

            if (_core != null)
                _core.ReturnToPool();
            else
                gameObject.SetActive(false);
        }


        // ============================================================
        // Runtime Status Event
        // ============================================================

        private void SubscribeRuntimeStatusEvents()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            _core.RuntimeStatus.MaxHpChanged += OnMaxHpChanged;
        }

        private void UnsubscribeRuntimeStatusEvents()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;

            _core.RuntimeStatus.MaxHpChanged -= OnMaxHpChanged;
        }

        private void OnMaxHpChanged(
            float previousMaxHp,
            float currentMaxHp)
        {
            if (_currentHp > currentMaxHp)
            {
                float previousHp = _currentHp;

                _currentHp = currentMaxHp;

                HpChanged?.Invoke(previousHp, _currentHp);
            }

            if (_currentShield > ShieldLimit)
            {
                float previousShield = _currentShield;

                _currentShield = ShieldLimit;

                ShieldChanged?.Invoke(previousShield, _currentShield);
            }
        }
    }
}

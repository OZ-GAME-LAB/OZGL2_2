using System;
using UnityEngine;
using System.Collections;



namespace Units
{
    public class Unit_Life : MonoBehaviour
    {
        private const float ShieldLimitMultiplier =
            2f;


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


        // ============================================================
        // Properties
        // ============================================================

        public float CurrentHp
            => _currentHp;


        public float CurrentShield
            => _currentShield;


        public float MaxHp
            => _core != null
                && _core.RuntimeStatus != null
                    ? _core.RuntimeStatus.MaxHp
                    : 0f;


        public float ShieldLimit
            => MaxHp
            * ShieldLimitMultiplier;


        public bool IsDead
            => _isDead;


        // ============================================================
        // Events
        // ============================================================

        public event Action<float, float> HpChanged;

        public event Action<float, float> ShieldChanged;


        public event Action<DamageResult> Damaged;

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

        public void Initialize(
            Unit_Core core)
        {
            if (core == null)
            {
                Debug.LogError(
                    $"[Unit_Life] {name} : Unit_Core가 없습니다."
                );

                return;
            }


            UnsubscribeRuntimeStatusEvents();


            _core =
                core;


            SubscribeRuntimeStatusEvents();


            _currentHp =
                MaxHp;

            _currentShield =
                0f;

            _isDead =
                false;


            HpChanged?.Invoke(
                _currentHp,
                _currentHp
            );


            ShieldChanged?.Invoke(
                _currentShield,
                _currentShield
            );
        }


        // ============================================================
        // Damage
        // ============================================================

        public void TakeDamage(
            DamageResult result)
        {
            if (_isDead)
                return;


            float damage =
                result.Damage;


            if (damage <= 0f)
                return;


            float remainingDamage =
                damage;


            ApplyShieldDamage(
                ref remainingDamage
            );


            ApplyHpDamage(
                remainingDamage
            );


            Damaged?.Invoke(
                result
            );


            if (_currentHp <= 0f)
            {
                Die();
            }
        }


        private void ApplyShieldDamage(
            ref float remainingDamage)
        {
            if (_currentShield <= 0f)
                return;

            if (remainingDamage <= 0f)
                return;


            float previousShield =
                _currentShield;


            float shieldDamage =
                Mathf.Min(
                    _currentShield,
                    remainingDamage
                );


            _currentShield -=
                shieldDamage;

            remainingDamage -=
                shieldDamage;


            ShieldChanged?.Invoke(
                previousShield,
                _currentShield
            );
        }


        private void ApplyHpDamage(
            float damage)
        {
            if (damage <= 0f)
                return;


            float previousHp =
                _currentHp;


            _currentHp =
                Mathf.Max(
                    0f,
                    _currentHp - damage
                );


            HpChanged?.Invoke(
                previousHp,
                _currentHp
            );
        }


        // ============================================================
        // Heal
        // ============================================================

        public void Heal(
            float amount)
        {
            if (_isDead)
                return;

            if (amount <= 0f)
                return;

            if (_currentHp >= MaxHp)
                return;


            float previousHp =
                _currentHp;


            _currentHp =
                Mathf.Min(
                    MaxHp,
                    _currentHp + amount
                );


            float healedAmount =
                _currentHp - previousHp;


            if (healedAmount <= 0f)
                return;


            HpChanged?.Invoke(
                previousHp,
                _currentHp
            );


            Healed?.Invoke(
                healedAmount
            );
        }


        // ============================================================
        // Shield
        // ============================================================

        public void AddShield(
            float amount)
        {
            if (_isDead)
                return;

            if (amount <= 0f)
                return;

            if (_currentShield >= ShieldLimit)
                return;


            float previousShield =
                _currentShield;


            _currentShield =
                Mathf.Min(
                    ShieldLimit,
                    _currentShield + amount
                );


            float addedAmount =
                _currentShield - previousShield;


            if (addedAmount <= 0f)
                return;


            ShieldChanged?.Invoke(
                previousShield,
                _currentShield
            );


            ShieldAdded?.Invoke(
                addedAmount
            );
        }


        public void AddShieldByMaxHp(
            float ratio)
        {
            if (ratio <= 0f)
                return;


            AddShield(
                MaxHp * ratio
            );
        }


        // ============================================================
        // Death
        // ============================================================

        private void Die()
        {
            if (_isDead)
                return;


            _isDead =
                true;


            Debug.Log(
                $"[Unit_Life] {name} 사망"
            );


            _core?.NotifyDeath();


            StartCoroutine(
                DisableAfterDeath()
            );
        }


        // 임시적으로 사망 후 0.3초 후에 비활성화하도록 설정
        private IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(
                0.3f
            );


            gameObject.SetActive(
                false
            );
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


            _core.RuntimeStatus.MaxHpChanged +=
                OnMaxHpChanged;
        }


        private void UnsubscribeRuntimeStatusEvents()
        {
            if (_core == null)
                return;

            if (_core.RuntimeStatus == null)
                return;


            _core.RuntimeStatus.MaxHpChanged -=
                OnMaxHpChanged;
        }


        private void OnMaxHpChanged(
            float previousMaxHp,
            float currentMaxHp)
        {
            if (_currentHp > currentMaxHp)
            {
                float previousHp =
                    _currentHp;


                _currentHp =
                    currentMaxHp;


                HpChanged?.Invoke(
                    previousHp,
                    _currentHp
                );
            }


            if (_currentShield > ShieldLimit)
            {
                float previousShield =
                    _currentShield;


                _currentShield =
                    ShieldLimit;


                ShieldChanged?.Invoke(
                    previousShield,
                    _currentShield
                );
            }
        }
    }
}
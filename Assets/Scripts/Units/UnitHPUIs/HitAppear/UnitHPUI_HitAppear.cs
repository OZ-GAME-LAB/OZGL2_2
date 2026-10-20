using Units;
using UnityEngine;
using UnityEngine.UI;

namespace Units.UI
{
    /// <summary>
    /// 하나의 유닛 HP UI를 담당한다.
    ///
    /// Unit_Gateway에 바인딩되어 HP / Shield 변경 이벤트를 구독하고,
    /// 값이 변경될 때 게이지와 표시 상태를 갱신한다.
    ///
    /// 일반 유닛은 HP가 감소했거나,
    /// 설정에 따라 Shield가 존재할 때 표시한다.
    ///
    /// 보스처럼 항상 표시해야 하는 경우
    /// Always Visible 정책을 사용할 수 있다.
    ///
    /// HP Frame은 공통 이미지를 사용하며,
    /// HP Fill만 아군 / 적군에 따라 Sprite를 변경한다.
    /// </summary>
    public class UnitHPUI_HitAppear : MonoBehaviour
    {
        // ============================================================
        // Constants
        // ============================================================

        private const float HpComparisonEpsilon =
            0.001f;

        private const float MaxShieldHpMultiplier =
            2f;


        // ============================================================
        // HP
        // ============================================================

        [Header("HP")]
        [SerializeField]
        private Image _hpFill;


        // ============================================================
        // Shield
        // ============================================================

        [Header("Shield")]
        [SerializeField]
        private GameObject _shieldRoot;

        [SerializeField]
        private Image _shieldFill;


        // ============================================================
        // Team Visual
        // ============================================================

        [Header("Team Visual")]
        [SerializeField]
        private Sprite _allyHPFillSprite;

        [SerializeField]
        private Sprite _enemyHPFillSprite;


        // ============================================================
        // Runtime
        // ============================================================

        private Unit_Gateway _target;

        private bool _showWhenHasShield;

        private bool _alwaysVisible;


        // ============================================================
        // Properties
        // ============================================================

        public Unit_Gateway Target
            => _target;

        public bool IsBound
            => _target != null;


        // ============================================================
        // Bind
        // ============================================================

        public void Bind(
            Unit_Gateway target,
            bool showWhenHasShield,
            bool alwaysVisible = false)
        {
            if (_target == target)
            {
                _showWhenHasShield =
                    showWhenHasShield;

                _alwaysVisible =
                    alwaysVisible;

                RefreshAll();

                return;
            }


            Unsubscribe();

            _target =
                target;

            _showWhenHasShield =
                showWhenHasShield;

            _alwaysVisible =
                alwaysVisible;


            if (_target == null ||
                !_target.IsTargetable)
            {
                Unbind();

                return;
            }


            Subscribe();

            RefreshTeamVisual();
            RefreshAll();
        }


        public void Unbind()
        {
            Unsubscribe();

            _target =
                null;

            _showWhenHasShield =
                false;

            _alwaysVisible =
                false;

            SetVisible(
                false
            );
        }


        // ============================================================
        // Subscription
        // ============================================================

        private void Subscribe()
        {
            if (_target == null)
                return;


            _target.HpChanged +=
                OnHpChanged;

            _target.ShieldChanged +=
                OnShieldChanged;

            _target.Died +=
                OnDied;
        }


        private void Unsubscribe()
        {
            if (_target == null)
                return;


            _target.HpChanged -=
                OnHpChanged;

            _target.ShieldChanged -=
                OnShieldChanged;

            _target.Died -=
                OnDied;
        }


        // ============================================================
        // Events
        // ============================================================

        private void OnHpChanged(
            float previousHp,
            float currentHp)
        {
            RefreshHP(
                currentHp
            );

            RefreshVisibility();
        }


        private void OnShieldChanged(
            float previousShield,
            float currentShield)
        {
            RefreshShield(
                currentShield
            );

            RefreshVisibility();
        }


        private void OnDied(
            Unit_Gateway unit)
        {
            if (unit != _target)
                return;


            SetVisible(
                false
            );
        }


        // ============================================================
        // Refresh
        // ============================================================

        private void RefreshAll()
        {
            if (_target == null)
            {
                SetVisible(
                    false
                );

                return;
            }


            RefreshHP(
                _target.CurrentHp
            );

            RefreshShield(
                _target.CurrentShield
            );

            RefreshVisibility();
        }


        private void RefreshTeamVisual()
        {
            if (_target == null ||
                _hpFill == null)
            {
                return;
            }


            switch (_target.Team)
            {
                case UnitTeam.Ally:
                    if (_allyHPFillSprite != null)
                    {
                        _hpFill.sprite =
                            _allyHPFillSprite;
                    }

                    break;


                case UnitTeam.Enemy:
                    if (_enemyHPFillSprite != null)
                    {
                        _hpFill.sprite =
                            _enemyHPFillSprite;
                    }

                    break;
            }
        }


        // ============================================================
        // HP
        // ============================================================

        private void RefreshHP(
            float currentHp)
        {
            if (_target == null ||
                _hpFill == null)
            {
                return;
            }


            float maxHp =
                _target.MaxHp;


            float ratio =
                maxHp > 0f
                    ? currentHp / maxHp
                    : 0f;


            _hpFill.fillAmount =
                Mathf.Clamp01(
                    ratio
                );
        }


        // ============================================================
        // Shield
        // ============================================================

        private void RefreshShield(
            float currentShield)
        {
            if (_target == null)
                return;


            bool hasShield =
                currentShield > 0f;


            if (_shieldRoot != null)
            {
                _shieldRoot.SetActive(
                    hasShield
                );
            }


            if (_shieldFill == null)
                return;


            float maxShield =
                _target.MaxHp *
                MaxShieldHpMultiplier;


            float ratio =
                maxShield > 0f
                    ? currentShield / maxShield
                    : 0f;


            _shieldFill.fillAmount =
                Mathf.Clamp01(
                    ratio
                );
        }


        // ============================================================
        // Visibility
        // ============================================================

        private void RefreshVisibility()
        {
            if (_target == null ||
                !_target.IsTargetable)
            {
                SetVisible(
                    false
                );

                return;
            }


            if (_alwaysVisible)
            {
                SetVisible(
                    true
                );

                return;
            }


            float maxHp =
                _target.MaxHp;

            float currentHp =
                _target.CurrentHp;

            float currentShield =
                _target.CurrentShield;


            bool isDamaged =
                maxHp > 0f &&
                currentHp <
                maxHp - HpComparisonEpsilon;


            bool hasVisibleShield =
                _showWhenHasShield &&
                currentShield > 0f;


            SetVisible(
                isDamaged ||
                hasVisibleShield
            );
        }


        private void SetVisible(
            bool visible)
        {
            if (gameObject.activeSelf ==
                visible)
            {
                return;
            }


            gameObject.SetActive(
                visible
            );
        }


        // ============================================================
        // Cleanup
        // ============================================================

        private void OnDestroy()
        {
            Unsubscribe();

            _target =
                null;
        }
    }
}
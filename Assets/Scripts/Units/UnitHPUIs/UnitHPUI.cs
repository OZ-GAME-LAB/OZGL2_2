using Units;
using UnityEngine;
using UnityEngine.UI;

namespace Units.UI
{
    /// <summary>
    /// 하나의 유닛 HP UI를 담당한다.
    ///
    /// Unit_Gateway에 바인딩되어 HP / Shield 변경 이벤트를 구독하고,
    /// 값이 변경될 때만 게이지를 갱신한다.
    ///
    /// HP Frame은 공통 이미지를 사용하며,
    /// HP Fill만 아군 / 적군에 따라 Sprite를 변경한다.
    ///
    /// 대상 탐색과 화면상의 위치 갱신은 UnitHPUIManager가 담당한다.
    /// </summary>
    public class UnitHPUI : MonoBehaviour
    {
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


        // ============================================================
        // Properties
        // ============================================================

        public Unit_Gateway Target => _target;

        public bool IsBound => _target != null;


        // ============================================================
        // Bind
        // ============================================================

        /// <summary>
        /// 표시할 유닛을 할당한다.
        /// 기존 대상이 있다면 이벤트 구독을 해제한 뒤
        /// 새로운 대상에 다시 연결한다.
        /// </summary>
        public void Bind(Unit_Gateway target)
        {
            if (_target == target)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                RefreshAll();
                return;
            }

            Unsubscribe();

            _target = target;

            if (_target == null ||
                !_target.IsTargetable)
            {
                Unbind();
                return;
            }

            Subscribe();

            RefreshTeamVisual();
            RefreshAll();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }


        /// <summary>
        /// 현재 대상과의 연결을 해제하고 UI를 숨긴다.
        /// </summary>
        public void Unbind()
        {
            Unsubscribe();

            _target = null;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }


        // ============================================================
        // Subscription
        // ============================================================

        private void Subscribe()
        {
            if (_target == null)
                return;

            _target.HpChanged += OnHpChanged;
            _target.ShieldChanged += OnShieldChanged;
        }


        private void Unsubscribe()
        {
            if (_target == null)
                return;

            _target.HpChanged -= OnHpChanged;
            _target.ShieldChanged -= OnShieldChanged;
        }


        // ============================================================
        // Events
        // ============================================================

        private void OnHpChanged(
            float previousHp,
            float currentHp)
        {
            RefreshHP(currentHp);
        }


        private void OnShieldChanged(
            float previousShield,
            float currentShield)
        {
            RefreshShield(currentShield);
        }


        // ============================================================
        // Refresh
        // ============================================================

        /// <summary>
        /// 현재 대상의 전체 표시 상태를 갱신한다.
        /// </summary>
        private void RefreshAll()
        {
            if (_target == null)
                return;

            RefreshHP(
                _target.CurrentHp
            );

            RefreshShield(
                _target.CurrentShield
            );
        }


        // ============================================================
        // Team Visual
        // ============================================================

        /// <summary>
        /// 현재 대상의 Team에 따라
        /// HP Fill Sprite만 변경한다.
        ///
        /// HP Frame은 프리팹에 설정된 공통 이미지를 그대로 사용한다.
        /// </summary>
        private void RefreshTeamVisual()
        {
            if (_target == null ||
                _hpFill == null)
            {
                return;
            }

            _hpFill.sprite =
                _target.Team == UnitTeam.Ally
                    ? _allyHPFillSprite
                    : _enemyHPFillSprite;
        }


        // ============================================================
        // HP
        // ============================================================

        private void RefreshHP(float currentHp)
        {
            if (_hpFill == null ||
                _target == null)
            {
                return;
            }

            float maxHp =
                _target.MaxHp;

            _hpFill.fillAmount =
                maxHp > 0f
                    ? Mathf.Clamp01(
                        currentHp / maxHp
                    )
                    : 0f;
        }


        // ============================================================
        // Shield
        // ============================================================

        private void RefreshShield(float currentShield)
        {
            if (_target == null)
                return;

            bool hasShield =
                currentShield > 0f;

            if (_shieldRoot != null &&
                _shieldRoot.activeSelf != hasShield)
            {
                _shieldRoot.SetActive(
                    hasShield
                );
            }

            if (_shieldFill == null)
                return;

            if (!hasShield)
            {
                _shieldFill.fillAmount = 0f;
                return;
            }

            float maxHp =
                _target.MaxHp;

            // 최대 보호막은 최대 체력의 2배까지 허용한다.
            float shieldLimit =
                maxHp * 2f;

            _shieldFill.fillAmount =
                shieldLimit > 0f
                    ? Mathf.Clamp01(
                        currentShield /
                        shieldLimit
                    )
                    : 0f;
        }


        // ============================================================
        // Lifecycle
        // ============================================================

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
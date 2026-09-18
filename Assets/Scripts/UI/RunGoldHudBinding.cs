using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>RunCurrencyManager가 소유한 골드를 HUD에 표시한다.</summary>
    public sealed class RunGoldHudBinding : MonoBehaviour
    {
        [SerializeField] private GameUIController _ui;
        [SerializeField] private RunCurrencyManager _currencyManager;

        private bool _isSubscribed;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Initialize(GameUIController ui, RunCurrencyManager currencyManager)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (currencyManager == null) throw new ArgumentNullException(nameof(currencyManager));
            Unbind();
            _ui = ui;
            _currencyManager = currencyManager;
            if (isActiveAndEnabled) Bind();
        }

        /// <summary>재화 초기화/종료는 BalanceChanged를 발행하지 않아 외부 초기화 주체가 호출한다.</summary>
        public void Refresh()
        {
            if (_ui == null) return;
            if (_currencyManager == null || !_currencyManager.IsInitialized)
            {
                _ui.ClearGold();
                return;
            }

            _ui.SetGold(_currencyManager.GetBalance(CurrencyType.Gold));
        }

        private void HandleBalanceChanged(CurrencyData currency, int previousBalance, int newBalance)
        {
            if (currency != null && currency.Type == CurrencyType.Gold) Refresh();
        }

        private void Bind()
        {
            if (_isSubscribed || _ui == null || _currencyManager == null) return;
            _currencyManager.BalanceChanged += HandleBalanceChanged;
            _isSubscribed = true;
            Refresh();
        }

        private void Unbind()
        {
            if (_isSubscribed && _currencyManager != null)
                _currencyManager.BalanceChanged -= HandleBalanceChanged;
            _isSubscribed = false;
        }
    }
}

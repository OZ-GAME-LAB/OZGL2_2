using System.Collections.Generic;
using System.Collections.ObjectModel;

public class CurrencyWallet
{
    public CurrencyLifetime Lifetime { get; }
    public IReadOnlyDictionary<CurrencyData, int> Balances { get; }

    private readonly Dictionary<CurrencyData, int> _balances =
        new Dictionary<CurrencyData, int>();

    public CurrencyWallet(CurrencyLifetime lifetime)
    {
        Lifetime = lifetime;
        Balances = new ReadOnlyDictionary<CurrencyData, int>(_balances);
    }

    public int GetBalance(CurrencyData currency)
    {
        if (!IsRegistered(currency))
        {
            return 0;
        }

        return _balances[currency];
    }

    public bool CanSpend(CurrencyAmount currencyAmount)
    {
        CurrencyData currency = currencyAmount.Currency;
        int amount = currencyAmount.Amount;

        if (!IsRegistered(currency))
        {
            return false;
        }

        if (amount < 0)
        {
            return false;
        }

        return GetBalance(currency) >= amount;
    }

    public bool TryRegister(CurrencyData currency)
    {
        if (!CanUseCurrency(currency))
        {
            return false;
        }

        if (_balances.ContainsKey(currency))
        {
            return true;
        }

        _balances.Add(currency, 0);

        return true;
    }

    public bool TryAdd(CurrencyAmount currencyAmount)
    {
        CurrencyData currency = currencyAmount.Currency;
        int amount = currencyAmount.Amount;

        if (!IsRegistered(currency))
        {
            return false;
        }

        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        int currentBalance = GetBalance(currency);

        if (currentBalance > int.MaxValue - amount)
        {
            return false;
        }

        _balances[currency] = currentBalance + amount;

        return true;
    }

    public bool TrySpend(CurrencyAmount currencyAmount)
    {
        if (!CanSpend(currencyAmount))
        {
            return false;
        }

        if (currencyAmount.Amount == 0)
        {
            return true;
        }

        CurrencyData currency = currencyAmount.Currency;
        int currentBalance = GetBalance(currency);

        _balances[currency] =
            currentBalance - currencyAmount.Amount;

        return true;
    }

    public bool TrySetBalance(CurrencyData currency, int amount)
    {
        if (!IsRegistered(currency))
        {
            return false;
        }

        if (amount < 0)
        {
            return false;
        }

        _balances[currency] = amount;

        return true;
    }

    private bool CanUseCurrency(CurrencyData currency)
    {
        if (currency == null)
        {
            return false;
        }

        return currency.Lifetime == Lifetime;
    }

    private bool IsRegistered(CurrencyData currency)
    {
        if (!CanUseCurrency(currency))
        {
            return false;
        }

        return _balances.ContainsKey(currency);
    }
}

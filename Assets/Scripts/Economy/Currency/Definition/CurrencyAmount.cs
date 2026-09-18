using System;
using UnityEngine;

[Serializable]
public struct CurrencyAmount
{
    public CurrencyData Currency => _currency;
    public int Amount => _amount;

    [SerializeField] private CurrencyData _currency;
    [SerializeField, Min(0)] private int _amount;

    public CurrencyAmount(CurrencyData currency, int amount)
    {
        _currency = currency;
        _amount = amount;
    }
}
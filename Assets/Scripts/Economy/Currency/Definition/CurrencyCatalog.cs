using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CurrencyCatalog",
    menuName = "Economy/Currency Catalog"
)]
public class CurrencyCatalog : ScriptableObject
{
    public IReadOnlyList<CurrencyData> Currencies => _currencies;

    [SerializeField] private List<CurrencyData> _currencies =
        new List<CurrencyData>();

    private readonly Dictionary<string, CurrencyData> _currencyById =
        new Dictionary<string, CurrencyData>(StringComparer.Ordinal);

    private readonly Dictionary<CurrencyType, CurrencyData> _currencyByType =
        new Dictionary<CurrencyType, CurrencyData>();

    private void OnEnable()
    {
        RebuildDicionary();
    }

    public bool TryGetById(string id, out CurrencyData currency)
    {
        currency = null;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return _currencyById.TryGetValue(id, out currency);
    }

    public bool TryGetByType(CurrencyType type, out CurrencyData currency)
    {
        return _currencyByType.TryGetValue(type, out currency);
    }

    public bool Contains(CurrencyData currency)
    {
        if (currency == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currency.Id))
        {
            return false;
        }

        if (!_currencyById.TryGetValue(
            currency.Id,
            out CurrencyData registeredCurrency))
        {
            return false;
        }

        return registeredCurrency == currency &&
            _currencyByType.TryGetValue(currency.Type, out CurrencyData typedCurrency) &&
            typedCurrency == currency;
    }

    private void RebuildDicionary()
    {
        _currencyById.Clear();
        _currencyByType.Clear();

        foreach (CurrencyData currency in _currencies)
        {
            if (currency == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(currency.Id))
            {
                continue;
            }

            if (!IsValidType(currency.Type) || _currencyByType.ContainsKey(currency.Type) ||
                _currencyById.ContainsKey(currency.Id))
            {
                continue;
            }

            _currencyById.Add(currency.Id, currency);
            _currencyByType.Add(currency.Type, currency);
        }
    }

    private static bool IsValidType(CurrencyType type)
    {
        return type != CurrencyType.None && Enum.IsDefined(typeof(CurrencyType), type);
    }

    // 유효성 검사용 함수 S-
    private void OnValidate()
    {
        RebuildDicionary();
        ValidateCurrencies();
    }

    private void ValidateCurrencies()
    {
        HashSet<string> registeredIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<CurrencyType> registeredTypes = new HashSet<CurrencyType>();

        for (int i = 0; i < _currencies.Count; i++)
        {
            CurrencyData currency = _currencies[i];

            if (currency == null)
            {
                Debug.LogError(
                    $"[Economy/CurrencyCatalog] 비어 있는 재화가 있습니다. Index: {i}",
                    this
                );

                continue;
            }

            if (!IsValidType(currency.Type))
            {
                Debug.LogError($"[Economy/CurrencyCatalog] 유효한 재화 Type을 설정해야 합니다. Asset: {currency.name}", currency);
            }
            else if (!registeredTypes.Add(currency.Type))
            {
                Debug.LogError($"[Economy/CurrencyCatalog] 중복된 재화 Type입니다. Type: {currency.Type}", currency);
            }

            if (string.IsNullOrWhiteSpace(currency.Id))
            {
                Debug.LogError(
                    $"[Economy/CurrencyCatalog] 재화 ID가 비어 있습니다. Asset: {currency.name}",
                    currency
                );

                continue;
            }

            if (!registeredIds.Add(currency.Id))
            {
                Debug.LogError(
                    $"[Economy/CurrencyCatalog] 중복된 재화 ID입니다. ID: {currency.Id}",
                    currency
                );
            }
        }
    }
    // 유효성 검사용 함수 E-
}

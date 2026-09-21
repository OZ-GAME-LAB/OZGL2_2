using System;
using System.Text;
using TMPro;
using UnityEngine;

public class TestScriptReader : MonoBehaviour
{
    private ICurrencyReader _wallet;
    private TextMeshProUGUI text;
    private void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    public void Initialize(ICurrencyReader reader)
    {
        _wallet = reader;
        foreach (var balance in _wallet.Balances)
        {
            textRefresh(balance.Key, 0, 0);
        }

        _wallet.BalanceChanged += textRefresh;
    }
    public void textRefresh(CurrencyData data, int post, int current)
    {
        if (_wallet.Balances == null)
        {
            text.text = "미갱신";
            return;
        }
        StringBuilder sb = new StringBuilder();
        sb.Append("골드 : ").Append(_wallet.Balances[data]).Append("\n");
        sb.Append("젬 : ").Append(_wallet.Balances[data]);

        text.text = sb.ToString();
    }
}

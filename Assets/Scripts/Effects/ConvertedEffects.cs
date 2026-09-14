using System.Collections.Generic;
using Units;

// 각 시스템에 등록할 보정치 목록 (합산 및 실제 적용은 각 시스템에서 처리)
public class ConvertedEffects
{
    // 생성자에 null을 전달하면 해당 목록도 null
    public IReadOnlyList<AllyStatModifier> AllyModifiers { get; }
    public IReadOnlyList<EnemyStatModifier> EnemyModifiers { get; }
    public IReadOnlyList<CurrencyModifier> CurrencyModifiers { get; }

    public ConvertedEffects(List<AllyStatModifier> allyModifiers,
        List<EnemyStatModifier> enemyModifiers,
        List<CurrencyModifier> currencyModifiers)
    {
        if (allyModifiers != null)
        {
            AllyModifiers = new List<AllyStatModifier>(allyModifiers);
        }

        if (enemyModifiers != null)
        {
            EnemyModifiers = new List<EnemyStatModifier>(enemyModifiers);
        }
        if (currencyModifiers != null)
        {
            CurrencyModifiers = new List<CurrencyModifier>(currencyModifiers);
        }
    }

}

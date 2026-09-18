using System.Collections.Generic;
using Units;

// 유닛 효과 변환으로 생성한 아군·적군 보정치 목록
public class ConvertedUnitStatEffects
{
    public List<AllyStatModifier> AllyModifiers { get; }
    public List<EnemyStatModifier> EnemyModifiers { get; }

    public ConvertedUnitStatEffects(List<AllyStatModifier> allyModifiers,
        List<EnemyStatModifier> enemyModifiers)
    {
        AllyModifiers = allyModifiers;
        EnemyModifiers = enemyModifiers;
    }
}

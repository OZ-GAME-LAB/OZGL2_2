using UnityEngine;

public sealed class TotemFlowEffects
{
    public float EnemyCountPercent { get; private set; }

    public void AddEnemyCountPercent(float value)
        => EnemyCountPercent += value;

    public int CalculateEnemyCount(int baseCount)
        => Mathf.RoundToInt(baseCount * (1f + EnemyCountPercent));

    public void Clear() => EnemyCountPercent = 0f;
}

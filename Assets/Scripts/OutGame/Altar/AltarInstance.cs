// Current date KDH 2026-09-16
/// <summary>
/// 이번 Run에 적용된 제단입니다.
/// EffectManager와 UnitStatModifierManager가 Source로 사용해, 같은 객체로 효과를 제거할 수 있습니다.
/// </summary>
public class AltarInstance
{
    public AltarData Data { get; }

    public AltarInstance(AltarData data)
    {
        if (data == null)
        {
            UnityEngine.Debug.LogError("[OutGame/AltarInstance] AltarData가 없습니다. 제단 효과를 등록할 수 없습니다.");
        }

        Data = data;
    }
}

// Run에서 보유한 아티팩트 상태 (효과의 Source로 사용)
public class ArtifactInstance
{
    public ArtifactData Data { get; }
    public int StackCount { get; private set; }

    public ArtifactInstance(ArtifactData data)
    {
        Data = data;
        StackCount = 1;
    }

    public bool TryIncreaseStack()
    {
        if (Data == null || StackCount >= Data.MaxStacks)
        {
            return false;
        }

        StackCount++;
        return true;
    }
}

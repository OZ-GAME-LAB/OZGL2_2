namespace Game.Core
{
    /// <summary>종료 시점의 유물 종류와 중첩 수. 유물 에셋은 공유하고 개수는 복사한다.</summary>
    public sealed class RunArtifactEntry
    {
        public ArtifactData Data { get; }
        public int StackCount { get; }

        public RunArtifactEntry(ArtifactData data, int stackCount)
        {
            Data = data;
            StackCount = stackCount;
        }
    }
}

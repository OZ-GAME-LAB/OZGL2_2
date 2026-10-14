namespace Units
{
    public readonly struct Hero_PhaseInfo
    {
        public string Id { get; }
        public string Name { get; }
        public int Version { get; }
        public float Elapsed { get; }
        public bool TransitionPending { get; }
        public Hero_PhaseInfo(string id, string name, int version, float elapsed, bool pending)
        { Id = id; Name = name; Version = version; Elapsed = elapsed; TransitionPending = pending; }
    }
}

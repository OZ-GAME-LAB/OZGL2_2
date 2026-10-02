using System;
namespace Units
{
    public sealed class Hero_Gateway : Unit_Gateway, IHeroGateway
    {
        public bool HasHeroPhase => Core != null && Core.HasHeroPhase;
        public event Action<Hero_PhaseInfo> HeroPhaseChanged;
        public bool TryGetPhaseInfo(out Hero_PhaseInfo info)
        {
            info = default;
            return Core != null && Core.TryGetHeroPhaseInfo(out info);
        }
        internal void NotifyHeroPhaseChanged(Hero_PhaseInfo info) => HeroPhaseChanged?.Invoke(info);
    }
}

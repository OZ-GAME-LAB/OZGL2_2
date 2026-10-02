using System;
namespace Units
{
    public interface IHeroGateway
    {
        bool HasHeroPhase { get; }
        bool TryGetPhaseInfo(out Hero_PhaseInfo info);
        event Action<Hero_PhaseInfo> HeroPhaseChanged;
    }
}

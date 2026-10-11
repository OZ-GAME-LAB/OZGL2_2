


namespace Units.FX
{
    /// <summary>
    /// VFX ParticleSystem의 재생 시작 방식을 결정한다.
    /// </summary>
    public enum VFXParticleStartMode
    {
        /// <summary>
        /// 프리팹에 설정된 ParticleSystem 재생 방식을 그대로 사용한다.
        /// </summary>
        Original = 0,

        /// <summary>
        /// 재생 직후 지정된 수의 파티클을 추가로 즉시 방출한다.
        /// 외부 VFX 에셋의 Emission 설정을 수정하지 않고
        /// 즉발형 공격/피격 연출에 사용할 수 있다.
        /// </summary>
        EmitImmediately = 1
    }
}
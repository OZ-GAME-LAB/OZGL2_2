


namespace Units.FX
{
    /// <summary>
    /// VFX 재생 시 ParticleSystem의 Scaling Mode 보정 방식을 결정한다.
    /// </summary>
    public enum VFXParticleScalingMode
    {
        /// <summary>
        /// 프리팹에 저장된 원본 Scaling Mode를 그대로 사용한다.
        /// </summary>
        Original = 0,

        /// <summary>
        /// ParticleSystem이 부모를 포함한 전체 Transform 계층의
        /// Scale을 반영하도록 Hierarchy 모드로 보정한다.
        /// </summary>
        Hierarchy = 1
    }
}
namespace Units.FX
{
    /// <summary>
    /// VFX 재생 시 ParticleSystem의 Simulation Space 보정 방식을 결정한다.
    /// </summary>
    public enum VFXParticleSimulationMode
    {
        /// <summary>
        /// 외부 VFX 프리팹의 원본 Simulation Space를 그대로 사용한다.
        /// </summary>
        Original = 0,

        /// <summary>
        /// ParticleSystem을 Local Space로 보정한다.
        /// VFX 루트의 이동·회전을 파티클이 함께 따라가야 할 때 사용한다.
        /// </summary>
        Local = 1
    }
}
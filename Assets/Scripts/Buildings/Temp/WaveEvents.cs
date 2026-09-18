// Current date KDH 2026-09-11
// 웨이브 담당이 클리어 시점에 한 번 호출하면 됩니다.
// 건물은 Update에서 웨이브를 폴링하지 않고, 이 이벤트만 구독합니다.
// 이벤트 담당자의 코드로 교체할 것임.
using System;

namespace OZGL.KDH
{
    public static class WaveEvents
    {
        public static event Action<int> WaveCleared;

        public static void RaiseWaveCleared(int waveIndex)
        {
            WaveCleared?.Invoke(waveIndex);
        }
    }
}

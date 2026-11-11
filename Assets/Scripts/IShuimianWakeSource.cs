using System.Collections.Generic;
using UnityEngine;

namespace Shuimian.Gameplay
{
    // Common GPU input; ships and characters retain their own movement/contact logic.
    public interface IShuimianWakeSource
    {
        ShuimianWaterMesh Water { get; set; }
        bool CanSimulate { get; }
        Vector2 Position { get; }
        Vector2 Direction { get; }
        float Speed { get; }
        float MinSpeed { get; }
        void ClearHistory();
        void SampleMotion(float time);
        void AppendSamples(List<WakeSample> destination, float time);
    }

    public static class ShuimianWakeSources
    {
        static readonly List<IShuimianWakeSource> sources = new List<IShuimianWakeSource>();
        public static IReadOnlyList<IShuimianWakeSource> Active => sources;
        public static void Register(IShuimianWakeSource source) { if (!sources.Contains(source)) sources.Add(source); }
        public static void Unregister(IShuimianWakeSource source) => sources.Remove(source);
    }
}

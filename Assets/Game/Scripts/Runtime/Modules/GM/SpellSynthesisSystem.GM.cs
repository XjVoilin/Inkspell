#if JULYGF_DEBUG
namespace Game
{
    public sealed partial class SpellSynthesisSystem
    {
        // 拒绝的请求不会消费此设置；实际合成后恢复随机，换实例时不会残留。
        internal bool? DebugNextSuccess { get; set; }

        partial void OverrideSynthesisRandom(double successRate, ref double randomUnit)
        {
            if (!DebugNextSuccess.HasValue) return;

            randomUnit = DebugNextSuccess.Value ? successRate * .5 : 1.0;
            DebugNextSuccess = null;
        }
    }
}
#endif

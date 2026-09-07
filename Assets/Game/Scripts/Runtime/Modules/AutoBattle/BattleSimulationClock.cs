using System;

namespace Game
{
    /// <summary>把不稳定的渲染帧时间转换为有上限的固定战斗步数。</summary>
    internal sealed class BattleSimulationClock
    {
        internal const float StepSeconds = 1f / 30f;
        private const int MaximumStepsPerFrame = 10;
        private double _accumulatedSeconds;

        internal int TakeSteps(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return 0;
            }

            var maximumElapsed = (double)StepSeconds * MaximumStepsPerFrame;
            _accumulatedSeconds = Math.Min(
                _accumulatedSeconds + deltaTime,
                maximumElapsed);

            var steps = Math.Min(
                (int)(_accumulatedSeconds / StepSeconds),
                MaximumStepsPerFrame);
            _accumulatedSeconds -= steps * StepSeconds;
            return steps;
        }

        internal void Reset()
        {
            _accumulatedSeconds = 0d;
        }
    }
}

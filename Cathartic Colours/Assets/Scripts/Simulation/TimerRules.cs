using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>Which timer rule a game uses. A slot for time attack (CC-010).</summary>
    public enum TimerRuleKind : byte
    {
        None = 0
    }

    /// <summary>
    /// A timer rule: what happens as time passes, called through <see cref="BoardSimulation.Advance"/> (ADR-005).
    /// A rule with state of its own (time left) adds that state to the save in its own feature.
    /// </summary>
    public interface ITimerRule
    {
        /// <summary>Returns a result with <see cref="StepResult.Accepted"/> true only when time changed the game.</summary>
        StepResult Advance(ref BoardState board, float elapsedSeconds, ref Random random, NativeList<SimEvent> events);
    }

    /// <summary>Time changes nothing: the result is not accepted, and nothing is drawn.</summary>
    public struct NoTimer : ITimerRule
    {
        public StepResult Advance(ref BoardState board, float elapsedSeconds, ref Random random,
            NativeList<SimEvent> events) => default;
    }

    /// <summary>Calls the timer rule a <see cref="TimerRuleKind"/> names. Unknown kinds do nothing.</summary>
    public static class TimerRules
    {
        public static StepResult Advance(TimerRuleKind kind, ref BoardState board, float elapsedSeconds,
            ref Random random, NativeList<SimEvent> events)
        {
            switch (kind)
            {
                default: return new NoTimer().Advance(ref board, elapsedSeconds, ref random, events);
            }
        }
    }
}

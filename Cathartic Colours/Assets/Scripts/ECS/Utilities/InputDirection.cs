using Unity.Mathematics;

namespace ECS.Utilities
{
    /// <summary>
    /// The single rule that turns a 2D input vector (stick, keys, D-pad or normalised swipe) into a grid direction.
    /// Pure and free of the Input System so it can be tested in EditMode.
    /// </summary>
    public static class InputDirection
    {
        /// <summary>Magnitude at or above which an analogue input counts as a deliberate tilt.</summary>
        public const float DeadZone = 0.5f;

        /// <summary>Magnitude below which a held input counts as back at centre and may fire again.</summary>
        public const float ReleaseZone = 0.25f;

        /// <summary>
        /// Returns (+-1, 0) or (0, +-1), or (0, 0) when the vector's magnitude is below <paramref name="deadZone"/>.
        /// The dominant axis wins; an exact tie (a perfect diagonal) resolves to vertical.
        /// </summary>
        public static int2 Resolve(float2 input, float deadZone = DeadZone)
        {
            if (math.lengthsq(input) < deadZone * deadZone)
                return int2.zero;

            if (math.abs(input.x) > math.abs(input.y))
                return new int2(input.x > 0f ? 1 : -1, 0);

            return new int2(0, input.y > 0f ? 1 : -1);
        }
    }
}

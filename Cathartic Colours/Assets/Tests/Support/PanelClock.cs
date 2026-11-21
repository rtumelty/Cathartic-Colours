// Keeps the UI Test Framework's simulated clock in step with real time (CC-046.1, gap recorded in ADR-006).
//
// A RuntimePanelSimulator replaces its panel's clock with PanelSimulator.currentTime, a static that only moves when a
// test calls FrameUpdate or IncrementCurrentTime. That suits a panel the fixture owns and updates itself. Our panels
// are the scenes' own UIDocuments, updated by the player loop, and a frozen clock would stop their scheduled work and
// USS transitions. This object advances the simulated clock to Time.realtimeSinceStartupAsDouble every frame, so the
// panels see real time between simulated clicks. It lives for the rest of the run (DontDestroyOnLoad), because the
// simulator keeps its clock on the panel after the test ends.
using UnityEngine;
using UnityEngine.UIElements.TestFramework;

namespace TestSupport
{
    public sealed class PanelClock : MonoBehaviour
    {
        static PanelClock instance;

        /// <summary>Creates the clock once per run and brings the simulated time up to real time now.</summary>
        public static void Ensure()
        {
            if (instance == null)
            {
                var go = new GameObject("CC-046 PanelClock") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PanelClock>();
            }
            Sync();
        }

        /// <summary>Moves the simulated clock forward to real time (never backwards).</summary>
        public static void Sync()
        {
            double behind = Time.realtimeSinceStartupAsDouble - PanelSimulator.currentTime;
            if (behind > 0)
                PanelSimulator.IncrementCurrentTime(behind);
        }

        void Update() => Sync();
    }
}

// Base class for playthrough tests (CC-046): a UI Test Framework RuntimeUITestFixture that attaches to the scenes' own
// UIDocument panels, plus a GameDriver created and ended around every test.
//
// The fixture is the official way to use the UI Test Framework at runtime, in the editor and in players: it sets up
// the framework's per-test event state and removes its simulated clock from the panel when the class is done. Unlike
// the fixture's default, this class never creates a UIDocument of its own; the driver calls SetUIContent with the
// UIDocument of each scene it opens (main menu, gameplay).
//
//   public class MyCase : PlaythroughTest
//   {
//       [UnityTest, Category("TP_STD_04")]
//       public IEnumerator SmallPairMergesToMedium()
//       {
//           yield return Driver.ResumeWith(". . Rs Rs|...", GameMode.Standard);
//           ...
//       }
//   }
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine.UIElements.TestFramework;

namespace TestSupport
{
    public abstract class PlaythroughTest : RuntimeUITestFixture
    {
        /// <summary>The driver for the current test, created before it and ended after it.</summary>
        protected GameDriver Driver { get; private set; }

        protected PlaythroughTest()
        {
            // The panels belong to the scenes; the fixture must never clear them.
            clearContentAfterTest = false;
        }

        /// <summary>Folder for screenshots and logs. Override to choose one; null means a temp folder per run.</summary>
        protected virtual string ReportFolder => GameDriver.DefaultReportFolder;

        /// <summary>The fixture would create its own UIDocument here. This class uses the scenes' documents instead.</summary>
        public override void RecreatePanel()
        {
        }

        [UnitySetUp]
        public IEnumerator PlaythroughSetUp()
        {
            Driver = GameDriver.Begin(this, ReportFolder);
            yield break;
        }

        [UnityTearDown]
        public IEnumerator PlaythroughTearDown()
        {
            if (Driver != null)
                yield return Driver.End();
            Driver = null;
        }
    }
}

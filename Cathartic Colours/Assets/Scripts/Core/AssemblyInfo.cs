using System.Runtime.CompilerServices;

// The EditMode and PlayMode tests set the test-only save path override (SaveGameManager.SaveFilePathOverride).
[assembly: InternalsVisibleTo("CatharticColours.Core.Tests")]
[assembly: InternalsVisibleTo("CatharticColours.PlayMode.Tests")]
// The test-only PlayMode driver (CC-046) redirects saves the same way.
[assembly: InternalsVisibleTo("CatharticColours.TestSupport")]

using System.Runtime.CompilerServices;

// The EditMode and PlayMode tests set the test-only save path override (SaveGameManager.SaveFilePathOverride).
[assembly: InternalsVisibleTo("CatharticColours.Core.Tests")]
[assembly: InternalsVisibleTo("CatharticColours.PlayMode.Tests")]

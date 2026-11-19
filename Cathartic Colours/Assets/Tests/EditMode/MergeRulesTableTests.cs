// Written from docs/wiki/systems/merge-rules.md (per-mode rule tables) and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Every colour x size pair (plus indicator-flagged blocks) is checked against an expected-value function
// that encodes the spec tables, for each mode, both through the rule struct and through MergeRulesFactory.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Simulation.Tests
{
    public class MergeRulesTableTests
    {
        struct ExpectedMerge
        {
            public BlockColor Color;
            public BlockSize Size;
            public ScoreTier Tier;
            public bool Destroys;
            public bool CheckColor;
            public bool CheckSize;
        }

        static readonly BlockColor[] AllColors =
        {
            BlockColor.None, BlockColor.Red, BlockColor.Green, BlockColor.Yellow,
            BlockColor.Blue, BlockColor.Magenta, BlockColor.Cyan, BlockColor.White
        };

        static readonly BlockSize[] AllSizes = { BlockSize.Small, BlockSize.Medium, BlockSize.Large };

        static bool IsPrimary(BlockColor c)
        {
            return c == BlockColor.Red || c == BlockColor.Green || c == BlockColor.Blue;
        }

        // Primary + primary -> secondary (spec: Red+Green=Yellow, Red+Blue=Magenta, Green+Blue=Cyan).
        static bool TryPrimaryPair(BlockColor a, BlockColor b, out BlockColor result)
        {
            result = BlockColor.None;
            if (a == BlockColor.Red && b == BlockColor.Green || a == BlockColor.Green && b == BlockColor.Red) { result = BlockColor.Yellow; return true; }
            if (a == BlockColor.Red && b == BlockColor.Blue || a == BlockColor.Blue && b == BlockColor.Red) { result = BlockColor.Magenta; return true; }
            if (a == BlockColor.Green && b == BlockColor.Blue || a == BlockColor.Blue && b == BlockColor.Green) { result = BlockColor.Cyan; return true; }
            return false;
        }

        // Secondary + the primary it lacks (spec: Yellow+Blue, Magenta+Green, Cyan+Red).
        static bool IsSecondaryWithMissingPrimary(BlockColor a, BlockColor b)
        {
            return a == BlockColor.Yellow && b == BlockColor.Blue || a == BlockColor.Blue && b == BlockColor.Yellow
                || a == BlockColor.Magenta && b == BlockColor.Green || a == BlockColor.Green && b == BlockColor.Magenta
                || a == BlockColor.Cyan && b == BlockColor.Red || a == BlockColor.Red && b == BlockColor.Cyan;
        }

        static bool IsSecondary(BlockColor c)
        {
            return c == BlockColor.Yellow || c == BlockColor.Magenta || c == BlockColor.Cyan;
        }

        static BlockSize NextSize(BlockSize s)
        {
            return s == BlockSize.Small ? BlockSize.Medium : BlockSize.Large;
        }

        // ---------------------------------------------------------------- expected tables

        static ExpectedMerge? ExpectedStandard(Block m, Block t)
        {
            if (m.IsIndicator || t.IsIndicator) return null;
            if (m.Color != t.Color || m.Size != t.Size) return null;
            if (m.Size == BlockSize.Small)
                return new ExpectedMerge { Color = m.Color, Size = BlockSize.Medium, Tier = ScoreTier.Tier1, CheckColor = true, CheckSize = true };
            if (m.Size == BlockSize.Medium)
                return new ExpectedMerge { Color = m.Color, Size = BlockSize.Large, Tier = ScoreTier.Tier2, CheckColor = true, CheckSize = true };
            // Large + Large: both destroyed, Tier3. Result colour/size not specified by the spec.
            return new ExpectedMerge { Tier = ScoreTier.Tier3, Destroys = true };
        }

        static ExpectedMerge? ExpectedColorMerge(Block m, Block t)
        {
            if (m.IsIndicator || t.IsIndicator) return null;
            if (m.Color == BlockColor.None || t.Color == BlockColor.None) return null;
            if (m.Color == BlockColor.White || t.Color == BlockColor.White) return null;
            if (m.Color == t.Color) return null;

            BlockColor combined;
            if (TryPrimaryPair(m.Color, t.Color, out combined))
                return new ExpectedMerge { Color = combined, Size = m.Size, Tier = ScoreTier.Tier1, CheckColor = true, CheckSize = true };
            if (IsSecondaryWithMissingPrimary(m.Color, t.Color))
                return new ExpectedMerge { Color = BlockColor.White, Tier = ScoreTier.Tier3, Destroys = true, CheckColor = true };
            return null; // two secondaries, or a secondary with a primary it already contains
        }

        static ExpectedMerge? ExpectedAdvanced(Block m, Block t)
        {
            if (m.IsIndicator || t.IsIndicator) return null;
            if (m.Color == BlockColor.None || t.Color == BlockColor.None) return null;

            // Small + Small: same primary colour -> Medium, Tier1
            if (m.Size == BlockSize.Small && t.Size == BlockSize.Small)
            {
                if (m.Color == t.Color && IsPrimary(m.Color))
                    return new ExpectedMerge { Color = m.Color, Size = BlockSize.Medium, Tier = ScoreTier.Tier1, CheckColor = true, CheckSize = true };
                return null;
            }

            // Medium + Medium: two different primaries -> Large secondary, Tier2
            if (m.Size == BlockSize.Medium && t.Size == BlockSize.Medium)
            {
                BlockColor combined;
                if (TryPrimaryPair(m.Color, t.Color, out combined))
                    return new ExpectedMerge { Color = combined, Size = BlockSize.Large, Tier = ScoreTier.Tier2, CheckColor = true, CheckSize = true };
                return null;
            }

            // Large + Medium either way round: Large secondary + the Medium primary it lacks -> Large White, Tier3
            bool largeMedium = m.Size == BlockSize.Large && t.Size == BlockSize.Medium;
            bool mediumLarge = m.Size == BlockSize.Medium && t.Size == BlockSize.Large;
            if (largeMedium || mediumLarge)
            {
                var large = largeMedium ? m : t;
                var medium = largeMedium ? t : m;
                if (IsSecondary(large.Color) && IsPrimary(medium.Color) && IsSecondaryWithMissingPrimary(large.Color, medium.Color))
                    return new ExpectedMerge { Color = BlockColor.White, Size = BlockSize.Large, Tier = ScoreTier.Tier3, CheckColor = true, CheckSize = true };
                return null;
            }

            // Large + Large: both White -> both destroyed, Tier4
            if (m.Size == BlockSize.Large && t.Size == BlockSize.Large
                && m.Color == BlockColor.White && t.Color == BlockColor.White)
                return new ExpectedMerge { Tier = ScoreTier.Tier4, Destroys = true };

            return null;
        }

        // ---------------------------------------------------------------- enumeration and checking

        static List<Block> AllBlocks()
        {
            var list = new List<Block>();
            foreach (var color in AllColors)
                foreach (var size in AllSizes)
                {
                    list.Add(new Block { Color = color, Size = size, IsIndicator = false });
                    list.Add(new Block { Color = color, Size = size, IsIndicator = true });
                }
            return list;
        }

        static void CheckMode(
            string label,
            System.Func<Block, Block, ExpectedMerge?> expected,
            System.Func<Block, Block, bool> can,
            System.Func<Block, Block, MergeResult> merge,
            bool skipBothColourNone)
        {
            var failures = new StringBuilder();
            int checkedPairs = 0;
            int mergingPairs = 0;
            var blocks = AllBlocks();
            foreach (var m in blocks)
            {
                foreach (var t in blocks)
                {
                    // SPEC-QUESTION: Standard says "same colour and same size" with "any colour"; whether two
                    // colourless (None) blocks merge is not specified, so that case is not asserted.
                    if (skipBothColourNone && m.Color == BlockColor.None && t.Color == BlockColor.None) continue;

                    checkedPairs++;
                    var exp = expected(m, t);
                    string pair = label + " mover=" + TestBoards.Describe(m) + " target=" + TestBoards.Describe(t);

                    bool actualCan = can(m, t);
                    if (actualCan != exp.HasValue)
                    {
                        failures.AppendLine(pair + ": CanMerge expected " + exp.HasValue + " but was " + actualCan);
                        continue;
                    }
                    if (!exp.HasValue) continue;

                    mergingPairs++;
                    var e = exp.Value;
                    var res = merge(m, t);
                    if (res.DestroysBoth != e.Destroys)
                        failures.AppendLine(pair + ": DestroysBoth expected " + e.Destroys + " but was " + res.DestroysBoth);
                    if (res.Tier != e.Tier)
                        failures.AppendLine(pair + ": Tier expected " + e.Tier + " but was " + res.Tier);
                    if (e.CheckColor && res.Color != e.Color)
                        failures.AppendLine(pair + ": Color expected " + e.Color + " but was " + res.Color);
                    if (e.CheckSize && res.Size != e.Size)
                        failures.AppendLine(pair + ": Size expected " + e.Size + " but was " + res.Size);
                }
            }

            Assert.Greater(mergingPairs, 0, label + ": the expected table produced no merging pairs (test bug)");
            Assert.IsEmpty(failures.ToString(), checkedPairs + " pairs checked, failures:\n" + failures);
        }

        // ---------------------------------------------------------------- full-table tests

        [Test]
        public void Standard_AllPairs_MatchSpecTable_ViaRuleStruct()
        {
            var rules = new StandardMergeRules();
            CheckMode("Standard/struct", ExpectedStandard, (m, t) => rules.CanMerge(m, t), (m, t) => rules.Merge(m, t), true);
        }

        [Test]
        public void Standard_AllPairs_MatchSpecTable_ViaFactory()
        {
            CheckMode("Standard/factory", ExpectedStandard,
                (m, t) => MergeRulesFactory.CanMerge(GameMode.Standard, m, t),
                (m, t) => MergeRulesFactory.Merge(GameMode.Standard, m, t), true);
        }

        [Test]
        public void ColorMerge_AllPairs_MatchSpecTable_ViaRuleStruct()
        {
            var rules = new ColorMergeRules();
            CheckMode("ColorMerge/struct", ExpectedColorMerge, (m, t) => rules.CanMerge(m, t), (m, t) => rules.Merge(m, t), false);
        }

        [Test]
        public void ColorMerge_AllPairs_MatchSpecTable_ViaFactory()
        {
            CheckMode("ColorMerge/factory", ExpectedColorMerge,
                (m, t) => MergeRulesFactory.CanMerge(GameMode.ColorMerge, m, t),
                (m, t) => MergeRulesFactory.Merge(GameMode.ColorMerge, m, t), false);
        }

        [Test]
        public void AdvancedColorMerge_AllPairs_MatchSpecTable_ViaRuleStruct()
        {
            var rules = new AdvancedColorMergeRules();
            CheckMode("Advanced/struct", ExpectedAdvanced, (m, t) => rules.CanMerge(m, t), (m, t) => rules.Merge(m, t), false);
        }

        [Test]
        public void AdvancedColorMerge_AllPairs_MatchSpecTable_ViaFactory()
        {
            CheckMode("Advanced/factory", ExpectedAdvanced,
                (m, t) => MergeRulesFactory.CanMerge(GameMode.AdvancedColorMerge, m, t),
                (m, t) => MergeRulesFactory.Merge(GameMode.AdvancedColorMerge, m, t), false);
        }

        // ---------------------------------------------------------------- readable named cases

        static Block B(BlockColor c, BlockSize s) { return Block.Of(c, s); }

        [Test]
        public void Standard_TwoSmallSameColour_MergeToMedium_Tier1()
        {
            var rules = new StandardMergeRules();
            var a = B(BlockColor.Red, BlockSize.Small);
            Assert.IsTrue(rules.CanMerge(a, a));
            var r = rules.Merge(a, a);
            Assert.AreEqual(BlockColor.Red, r.Color);
            Assert.AreEqual(BlockSize.Medium, r.Size);
            Assert.AreEqual(ScoreTier.Tier1, r.Tier);
            Assert.IsFalse(r.DestroysBoth);
        }

        [Test]
        public void Standard_TwoMediumSameColour_MergeToLarge_Tier2()
        {
            var rules = new StandardMergeRules();
            var a = B(BlockColor.Blue, BlockSize.Medium);
            var r = rules.Merge(a, a);
            Assert.AreEqual(BlockColor.Blue, r.Color);
            Assert.AreEqual(BlockSize.Large, r.Size);
            Assert.AreEqual(ScoreTier.Tier2, r.Tier);
            Assert.IsFalse(r.DestroysBoth);
        }

        [Test]
        public void Standard_TwoLargeSameColour_BothDestroyed_Tier3()
        {
            var rules = new StandardMergeRules();
            var a = B(BlockColor.Green, BlockSize.Large);
            Assert.IsTrue(rules.CanMerge(a, a));
            var r = rules.Merge(a, a);
            Assert.IsTrue(r.DestroysBoth);
            Assert.AreEqual(ScoreTier.Tier3, r.Tier);
        }

        [Test]
        public void Standard_SecondariesAndWhite_MergeLikeAnyOtherColour()
        {
            var rules = new StandardMergeRules();
            foreach (var colour in new[] { BlockColor.Yellow, BlockColor.Magenta, BlockColor.Cyan, BlockColor.White })
            {
                var a = B(colour, BlockSize.Small);
                Assert.IsTrue(rules.CanMerge(a, a), colour + " Small + Small should merge in Standard");
                var r = rules.Merge(a, a);
                Assert.AreEqual(colour, r.Color);
                Assert.AreEqual(BlockSize.Medium, r.Size);
            }
        }

        [Test]
        public void Standard_DifferentColourOrDifferentSize_DoNotMerge()
        {
            var rules = new StandardMergeRules();
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Green, BlockSize.Small)));
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Red, BlockSize.Medium)));
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Large), B(BlockColor.Red, BlockSize.Medium)));
        }

        [Test]
        public void AllModes_IndicatorNeverMerges_OnEitherSide()
        {
            foreach (GameMode mode in new[] { GameMode.Standard, GameMode.ColorMerge, GameMode.AdvancedColorMerge })
            {
                var indicator = Block.Indicator;
                foreach (var other in new[]
                {
                    Block.Indicator, B(BlockColor.White, BlockSize.Small), B(BlockColor.Red, BlockSize.Small),
                    B(BlockColor.Blue, BlockSize.Medium), B(BlockColor.White, BlockSize.Large)
                })
                {
                    Assert.IsFalse(MergeRulesFactory.CanMerge(mode, indicator, other), mode + ": indicator as mover vs " + TestBoards.Describe(other));
                    Assert.IsFalse(MergeRulesFactory.CanMerge(mode, other, indicator), mode + ": indicator as target vs " + TestBoards.Describe(other));
                }
            }
        }

        [Test]
        public void ColorMerge_RedGreen_Yellow_Tier1()
        {
            var rules = new ColorMergeRules();
            var r = rules.Merge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Green, BlockSize.Small));
            Assert.AreEqual(BlockColor.Yellow, r.Color);
            Assert.AreEqual(ScoreTier.Tier1, r.Tier);
            Assert.IsFalse(r.DestroysBoth);
        }

        [Test]
        public void ColorMerge_YellowBlue_BothDestroyed_WhiteParticle_Tier3()
        {
            var rules = new ColorMergeRules();
            Assert.IsTrue(rules.CanMerge(B(BlockColor.Yellow, BlockSize.Small), B(BlockColor.Blue, BlockSize.Small)));
            var r = rules.Merge(B(BlockColor.Yellow, BlockSize.Small), B(BlockColor.Blue, BlockSize.Small));
            Assert.IsTrue(r.DestroysBoth);
            Assert.AreEqual(BlockColor.White, r.Color);
            Assert.AreEqual(ScoreTier.Tier3, r.Tier);
        }

        [Test]
        public void ColorMerge_IgnoresSize_AndResultSizeIsMoversSize()
        {
            var rules = new ColorMergeRules();
            var smallRed = B(BlockColor.Red, BlockSize.Small);
            var largeGreen = B(BlockColor.Green, BlockSize.Large);
            Assert.IsTrue(rules.CanMerge(smallRed, largeGreen));
            Assert.IsTrue(rules.CanMerge(largeGreen, smallRed));
            Assert.AreEqual(BlockSize.Small, rules.Merge(smallRed, largeGreen).Size, "mover Small");
            Assert.AreEqual(BlockSize.Large, rules.Merge(largeGreen, smallRed).Size, "mover Large");
        }

        [Test]
        public void ColorMerge_EqualColoursSecondariesAndWhite_DoNotMerge()
        {
            var rules = new ColorMergeRules();
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Red, BlockSize.Small)));
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Yellow, BlockSize.Small), B(BlockColor.Magenta, BlockSize.Small)), "two secondaries");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Yellow, BlockSize.Small), B(BlockColor.Red, BlockSize.Small)), "secondary + primary it contains");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.White, BlockSize.Small), B(BlockColor.Red, BlockSize.Small)), "White never merges");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.White, BlockSize.Small)), "White never merges");
        }

        [Test]
        public void Advanced_LargeSecondaryAndMediumPrimaryItLacks_MergeToWhiteLarge_BothOrders()
        {
            var rules = new AdvancedColorMergeRules();
            var largeMagenta = B(BlockColor.Magenta, BlockSize.Large);
            var mediumGreen = B(BlockColor.Green, BlockSize.Medium);
            foreach (var pair in new[] { (largeMagenta, mediumGreen), (mediumGreen, largeMagenta) })
            {
                Assert.IsTrue(rules.CanMerge(pair.Item1, pair.Item2));
                var r = rules.Merge(pair.Item1, pair.Item2);
                Assert.AreEqual(BlockColor.White, r.Color);
                Assert.AreEqual(BlockSize.Large, r.Size);
                Assert.AreEqual(ScoreTier.Tier3, r.Tier);
                Assert.IsFalse(r.DestroysBoth);
            }
        }

        [Test]
        public void Advanced_TwoDifferentMediumPrimaries_MergeToLargeSecondary_Tier2()
        {
            var rules = new AdvancedColorMergeRules();
            var r = rules.Merge(B(BlockColor.Red, BlockSize.Medium), B(BlockColor.Blue, BlockSize.Medium));
            Assert.AreEqual(BlockColor.Magenta, r.Color);
            Assert.AreEqual(BlockSize.Large, r.Size);
            Assert.AreEqual(ScoreTier.Tier2, r.Tier);
        }

        [Test]
        public void Advanced_WhiteLargePair_BothDestroyed_Tier4()
        {
            var rules = new AdvancedColorMergeRules();
            var w = B(BlockColor.White, BlockSize.Large);
            Assert.IsTrue(rules.CanMerge(w, w));
            var r = rules.Merge(w, w);
            Assert.IsTrue(r.DestroysBoth);
            Assert.AreEqual(ScoreTier.Tier4, r.Tier);
        }

        [Test]
        public void Advanced_DeadEnds_NeverMerge()
        {
            var rules = new AdvancedColorMergeRules();
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Medium), B(BlockColor.Red, BlockSize.Medium)), "same-colour Medium pair");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Red, BlockSize.Medium)), "Small + Medium");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Medium), B(BlockColor.Red, BlockSize.Small)), "Medium + Small");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Red, BlockSize.Small), B(BlockColor.Green, BlockSize.Small)), "different Small colours");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Magenta, BlockSize.Large), B(BlockColor.Red, BlockSize.Medium)), "Large secondary + Medium primary it already contains");
            Assert.IsFalse(rules.CanMerge(B(BlockColor.Magenta, BlockSize.Large), B(BlockColor.Magenta, BlockSize.Large)), "two Large secondaries");
        }

        [Test]
        public void Factory_UnknownMode_BehavesAsStandard()
        {
            var unknown = (GameMode)99;
            var a = B(BlockColor.Red, BlockSize.Small);
            var g = B(BlockColor.Green, BlockSize.Small);
            Assert.IsTrue(MergeRulesFactory.CanMerge(unknown, a, a));
            Assert.IsFalse(MergeRulesFactory.CanMerge(unknown, a, g), "Colour merging is not a Standard rule");
            var r = MergeRulesFactory.Merge(unknown, a, a);
            Assert.AreEqual(BlockColor.Red, r.Color);
            Assert.AreEqual(BlockSize.Medium, r.Size);
            Assert.AreEqual(ScoreTier.Tier1, r.Tier);
        }
    }
}

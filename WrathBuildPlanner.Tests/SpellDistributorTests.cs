using System.Collections.Generic;
using System.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class SpellDistributorTests {
        static readonly Dictionary<(int, int), int> SorcererTable = new Dictionary<(int, int), int> {
            { (1, 1), 2 }, { (3, 1), 1 }, { (4, 2), 1 },
        };

        static int Table(int casterLevel, int spellLevel) =>
            SorcererTable.TryGetValue((casterLevel, spellLevel), out int count) ? count : 0;

        static Dictionary<int, List<string>> Known(params (int level, string[] spells)[] lists) =>
            lists.ToDictionary(l => l.level, l => l.spells.ToList());

        [Fact]
        public void SpontaneousFollowsTheTable() {
            var result = SpellDistributor.Distribute(new[] { "Sorcerer", "Sorcerer", "Sorcerer", "Sorcerer" }, "Sorcerer",
                Known((1, new[] { "A", "B", "C" }), (2, new[] { "D" })), Table, c => 0, c => c < 4 ? 1 : 2);
            Assert.Equal(new[] { "A", "B" }, result.ByLevel[1]);
            Assert.False(result.ByLevel.ContainsKey(2));
            Assert.Equal(new[] { "C" }, result.ByLevel[3]);
            Assert.Equal(new[] { "D" }, result.ByLevel[4]);
            Assert.Equal(0, result.Dropped);
        }

        [Fact]
        public void MulticlassMapsClassLevelToCharacterLevel() {
            var result = SpellDistributor.Distribute(new[] { "Fighter", "Sorcerer", "Sorcerer" }, "Sorcerer",
                Known((1, new[] { "A", "B" })), Table, c => 0, c => 1);
            Assert.Equal(new[] { "A", "B" }, result.ByLevel[2]);
            Assert.Single(result.ByLevel);
        }

        [Fact]
        public void WizardFreePicksTakeTheHighestLevelFirst() {
            var result = SpellDistributor.Distribute(new[] { "Wizard", "Wizard", "Wizard" }, "Wizard",
                Known((1, new[] { "A", "B", "C", "D", "E", "F", "G" }), (2, new[] { "H", "I" })),
                (c, s) => c == 1 && s == 1 ? 3 : 0, c => 2, c => c < 3 ? 1 : 2);
            Assert.Equal(new[] { "A", "B", "C", "D", "E" }, result.ByLevel[1]);
            Assert.Equal(new[] { "F", "G" }, result.ByLevel[2]);
            Assert.Equal(new[] { "H", "I" }, result.ByLevel[3]);
            Assert.Equal(0, result.Dropped);
        }

        [Fact]
        public void SurplusIsDropped() {
            var result = SpellDistributor.Distribute(new[] { "Sorcerer" }, "Sorcerer",
                Known((1, new[] { "A", "B", "C", "D" })), Table, c => 0, c => 1);
            Assert.Equal(new[] { "A", "B" }, result.ByLevel[1]);
            Assert.Equal(2, result.Dropped);
        }
    }
}

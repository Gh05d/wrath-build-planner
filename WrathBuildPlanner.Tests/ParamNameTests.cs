using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    /// <summary>
    /// English names for a feat's further choice taken from the game's enum member names, so an English build matches
    /// in a game running in another language (GameNames.ParamNames; review 2026-10-06).
    /// </summary>
    public class ParamNameTests {
        [Theory]
        [InlineData("Greatsword", "Greatsword")]
        [InlineData("Evocation", "Evocation")]
        [InlineData("SkillPerception", "Perception")]
        [InlineData("SkillThievery", "Trickery")]           // the game shows Trickery, the enum says Thievery
        [InlineData("SkillKnowledgeArcana", "Knowledge (Arcana)")]
        [InlineData("WeaponLightShield", "LightShield")]    // matched as "Light Shield": Normalize drops spaces
        [InlineData("WeaponHeavyShield", "HeavyShield")]
        public void EnumMemberGivesTheEnglishName(string member, string english) =>
            Assert.Contains(NameMatcher.Normalize(english), System.Array.ConvertAll(Vocabulary.EnumMemberNames(member), NameMatcher.Normalize));

        [Fact]
        public void TheMemberNameItselfStays() =>
            Assert.Contains("SkillThievery", Vocabulary.EnumMemberNames("SkillThievery"));

        [Fact]
        public void NothingForNothing() => Assert.Empty(Vocabulary.EnumMemberNames(""));
    }
}

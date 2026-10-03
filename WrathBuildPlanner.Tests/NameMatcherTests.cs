using System.Collections.Generic;
using System.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class NameMatcherTests {
        static List<NameCandidate> Candidates(params string[][] identities) =>
            identities.Select(names => new NameCandidate { Names = names, Display = names[0], Tag = names[0] }).ToList();

        static readonly List<NameCandidate> Schools = Candidates(
            new[] { "Specialist School — Evocation", "SpecialisationSchoolEvocationProgression", "f8019b7724d72a241a97157bc37f1c3b" },
            new[] { "Specialist School — Necromancy", "SpecialisationSchoolNecromancyProgression", "e9450978cc9feeb468fb8ee3a90607e3" },
            new[] { "Specialist School — Universalist", "SpecialisationSchoolUniversalistProgression", "0933849149cfc9244ac05d6a5b57fd80" });

        [Fact]
        public void NormalizeIgnoresCaseSpacePunctuationAndAccents() {
            Assert.Equal("catsgrace", NameMatcher.Normalize("Cat’s  Grace"));
            Assert.Equal("catsgrace", NameMatcher.Normalize("cat's grace"));
            Assert.Equal("resume", NameMatcher.Normalize("Résumé"));
            Assert.Equal("", NameMatcher.Normalize(null));
        }

        [Fact]
        public void ExactDisplayInternalAndGuidMatch() {
            Assert.Equal("Specialist School — Evocation", NameMatcher.Match("specialist school - evocation", Schools).Match.Display);
            Assert.Equal("Specialist School — Evocation", NameMatcher.Match("SpecialisationSchoolEvocationProgression", Schools).Match.Display);
            Assert.Equal("Specialist School — Necromancy", NameMatcher.Match("e9450978cc9feeb468fb8ee3a90607e3", Schools).Match.Display);
        }

        [Fact]
        public void CategoryPrefixIsRemovedInStageTwo() {
            var outcome = NameMatcher.Match("Evocation", Schools);
            Assert.Equal(MatchKind.Unique, outcome.Kind);
            Assert.Equal("Specialist School — Evocation", outcome.Match.Display);

            var mounts = Candidates(new[] { "Animal Companion — Horse", "AnimalCompanionFeatureHorse" });
            Assert.Equal(MatchKind.Unique, NameMatcher.Match("Horse", mounts).Kind);
        }

        [Fact]
        public void ParenthesisCategoryIsRemovedInStageTwo() {
            var feats = Candidates(
                new[] { "Metamagic (Empower Spell)", "EmpowerSpellFeat" },
                new[] { "Metamagic (Extend Spell)", "ExtendSpellFeat" });
            Assert.Equal("Metamagic (Empower Spell)", NameMatcher.Match("Empower Spell", feats).Match.Display);
            Assert.Equal("Metamagic (Empower Spell)", NameMatcher.Match("Metamagic (Empower Spell)", feats).Match.Display);
        }

        [Fact]
        public void ExactMatchWinsOverPrefixMatch() {
            var list = Candidates(new[] { "Evocation", "SpellFocusEvocation" }, new[] { "Specialist School — Evocation", "SchoolEvocation" });
            var outcome = NameMatcher.Match("Evocation", list);
            Assert.Equal(MatchKind.Unique, outcome.Kind);
            Assert.Equal("Evocation", outcome.Match.Display);
        }

        [Fact]
        public void TwoCandidatesInOneStageAreAmbiguous() {
            var list = Candidates(new[] { "Opposition School — Evocation", "OppositionSchoolEvocation" }, new[] { "Specialist School — Evocation", "SchoolEvocation" });
            var outcome = NameMatcher.Match("Evocation", list);
            Assert.Equal(MatchKind.Ambiguous, outcome.Kind);
            Assert.Equal(2, outcome.Tied.Count);
            Assert.Null(outcome.Match);
        }

        [Fact]
        public void NoMatchGivesSuggestionsButNoMatch() {
            var archetypes = Candidates(new[] { "Sage Sorcerer", "SageSorcererArchetype" }, new[] { "Seeker", "SeekerArchetype" }, new[] { "Crossblooded", "CrossbloodedArchetype" });
            var outcome = NameMatcher.Match("Sage", archetypes);
            Assert.Equal(MatchKind.None, outcome.Kind);
            Assert.Null(outcome.Match);
            Assert.Equal("Sage Sorcerer", outcome.Suggestions[0]);
            Assert.DoesNotContain("Crossblooded", outcome.Suggestions);
        }

        [Fact]
        public void TypoGivesSuggestion() {
            var feats = Candidates(new[] { "Power Attack", "PowerAttackFeature" }, new[] { "Toughness", "Toughness" });
            var outcome = NameMatcher.Match("Power Atack", feats);
            Assert.Equal(MatchKind.None, outcome.Kind);
            Assert.Equal(new[] { "Power Attack" }, outcome.Suggestions);
        }

        [Fact]
        public void InternalNameDiffersFromDisplay() {
            var spells = Candidates(new[] { "Shield", "MageShield", "ef768022b0785eb43a18969903c537c4" });
            Assert.Equal(MatchKind.Unique, NameMatcher.Match("Shield", spells).Kind);
            Assert.Equal(MatchKind.Unique, NameMatcher.Match("MageShield", spells).Kind);
        }

        [Fact]
        public void EmptyInputsNeverMatch() {
            Assert.Equal(MatchKind.None, NameMatcher.Match("", Schools).Kind);
            Assert.Equal(MatchKind.None, NameMatcher.Match("Evocation", new List<NameCandidate>()).Kind);
        }

        [Fact]
        public void SameCandidateReachedByTwoNamesIsNotAmbiguous() {
            var list = Candidates(new[] { "Shield", "Shield (English)", "shield" });
            Assert.Equal(MatchKind.Unique, NameMatcher.Match("Shield", list).Kind);
        }

        [Fact]
        public void ParenChainSplitting() {
            Assert.True(NameMatcher.TrySplitParenChain("Weapon Focus (Greatsword)", out string head, out string tail));
            Assert.Equal("Weapon Focus", head);
            Assert.Equal("Greatsword", tail);
            Assert.False(NameMatcher.TrySplitParenChain("Power Attack", out _, out _));
            Assert.False(NameMatcher.TrySplitParenChain("(Greatsword)", out _, out _));
        }
    }
}

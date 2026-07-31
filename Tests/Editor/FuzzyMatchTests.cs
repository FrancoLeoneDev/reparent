using NUnit.Framework;

namespace Noctis.Reparent.Tests
{
    public sealed class FuzzyMatchTests
    {
        [Test]
        public void EmptyQueryMatchesEverything()
        {
            Assert.IsTrue(FuzzyMatch.TryScore("Mesa_Comedor", string.Empty, out int score));
            Assert.AreEqual(0, score);
        }

        [Test]
        public void MatchesScatteredSubsequence()
        {
            Assert.IsTrue(FuzzyMatch.TryScore("Mesa_Comedor", "mscm", out _));
        }

        [Test]
        public void IsCaseInsensitive()
        {
            Assert.IsTrue(FuzzyMatch.TryScore("Mesa_Comedor", "MESA", out _));
        }

        [Test]
        public void RejectsOutOfOrderCharacters()
        {
            Assert.IsFalse(FuzzyMatch.TryScore("Mesa_Comedor", "comedormesa", out _));
        }

        [Test]
        public void RejectsMissingCharacters()
        {
            Assert.IsFalse(FuzzyMatch.TryScore("Mesa_Comedor", "silla", out _));
        }

        [Test]
        public void ContiguousMatchBeatsScatteredMatch()
        {
            FuzzyMatch.TryScore("Mesa_Comedor", "mesa", out int contiguous);
            FuzzyMatch.TryScore("MiEscaleraSuperAlta", "mesa", out int scattered);

            Assert.Greater(contiguous, scattered);
        }

        [Test]
        public void WordBoundaryMatchBeatsMidWordMatch()
        {
            FuzzyMatch.TryScore("Luz_Cocina", "c", out int boundary);
            FuzzyMatch.TryScore("Escalera", "c", out int midWord);

            Assert.Greater(boundary, midWord);
        }

        [Test]
        public void CamelCaseHumpCountsAsWordBoundary()
        {
            FuzzyMatch.TryScore("LuzCocina", "c", out int hump);
            FuzzyMatch.TryScore("Escalera", "c", out int midWord);

            Assert.Greater(hump, midWord);
        }

        [Test]
        public void ShorterNameOutranksLongerNameForTheSameQuery()
        {
            FuzzyMatch.TryScore("Mesa", "mesa", out int shortName);
            FuzzyMatch.TryScore("SuperMegaMesaGigante", "mesa", out int longName);

            Assert.Greater(shortName, longName);
        }
    }
}

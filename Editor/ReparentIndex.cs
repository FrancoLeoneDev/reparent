using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Noctis.Reparent
{
    /// <summary>
    /// A GameObject that can act as a parent, together with the hierarchy path it lives under.
    /// </summary>
    public readonly struct ParentCandidate
    {
        public readonly Transform Target;
        public readonly string Name;

        /// <summary>Path of the ancestors, excluding the candidate itself. Roots show the scene name.</summary>
        public readonly string Path;

        public ParentCandidate(Transform target, string path)
        {
            Target = target;
            Name = target.name;
            Path = path;
        }
    }

    /// <summary>
    /// Builds the list of valid parent candidates for a selection and ranks them against a query.
    /// Rebuilt once per popup, never per frame.
    /// </summary>
    public static class ReparentIndex
    {
        private static readonly List<Scored> ScoredBuffer = new List<Scored>();

        private readonly struct Scored
        {
            public readonly int Index;
            public readonly int Score;

            public Scored(int index, int score)
            {
                Index = index;
                Score = score;
            }
        }

        /// <summary>
        /// Collects every GameObject that may become a parent for <paramref name="excluded"/>.
        /// Skips the excluded transforms and their whole subtrees, which is what prevents cycles.
        /// Inside Prefab Mode only the open prefab is considered.
        /// </summary>
        public static void Collect(Scene scene, IReadOnlyList<Transform> excluded, List<ParentCandidate> results)
        {
            results.Clear();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                string stageLabel = System.IO.Path.GetFileNameWithoutExtension(stage.assetPath);
                AddRecursive(stage.prefabContentsRoot.transform, stageLabel, excluded, results);
                return;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            // An unsaved scene has an empty name; roots would end up with a blank path.
            string sceneLabel = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AddRecursive(root.transform, sceneLabel, excluded, results);
            }
        }

        /// <summary>
        /// Ranks <paramref name="candidates"/> against <paramref name="query"/>, best first.
        /// An empty query keeps the hierarchy order.
        /// </summary>
        public static void Rank(IReadOnlyList<ParentCandidate> candidates, string query, List<ParentCandidate> results, int limit)
        {
            results.Clear();

            if (string.IsNullOrEmpty(query))
            {
                int count = Mathf.Min(candidates.Count, limit);
                for (int i = 0; i < count; i++)
                {
                    results.Add(candidates[i]);
                }

                return;
            }

            ScoredBuffer.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (FuzzyMatch.TryScore(candidates[i].Name, query, out int score))
                {
                    ScoredBuffer.Add(new Scored(i, score));
                }
            }

            // Stable on ties so equal scores keep hierarchy order.
            ScoredBuffer.Sort(CompareByScore);

            int resultCount = Mathf.Min(ScoredBuffer.Count, limit);
            for (int i = 0; i < resultCount; i++)
            {
                results.Add(candidates[ScoredBuffer[i].Index]);
            }
        }

        /// <summary>True when a candidate's name equals <paramref name="name"/>, ignoring case.</summary>
        public static bool ContainsName(IReadOnlyList<ParentCandidate> candidates, string name)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareByScore(Scored a, Scored b)
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
        }

        private static void AddRecursive(Transform current, string parentPath, IReadOnlyList<Transform> excluded, List<ParentCandidate> results)
        {
            if (IsExcluded(current, excluded))
            {
                return;
            }

            results.Add(new ParentCandidate(current, parentPath));

            string childPath = parentPath.Length == 0 ? current.name : parentPath + "/" + current.name;
            for (int i = 0; i < current.childCount; i++)
            {
                AddRecursive(current.GetChild(i), childPath, excluded, results);
            }
        }

        private static bool IsExcluded(Transform current, IReadOnlyList<Transform> excluded)
        {
            for (int i = 0; i < excluded.Count; i++)
            {
                if (excluded[i] == current)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Subsequence matcher. Pure C#, no Unity dependencies, so it can be unit tested on its own.
    /// </summary>
    public static class FuzzyMatch
    {
        private const int ScoreStart = 30;
        private const int ScoreWordBoundary = 15;
        private const int ScoreContiguous = 10;
        private const int PenaltySkip = 1;

        /// <summary>
        /// True when every character of <paramref name="query"/> appears in <paramref name="candidate"/>
        /// in order. Higher scores mean tighter matches.
        /// </summary>
        public static bool TryScore(string candidate, string query, out int score)
        {
            score = 0;

            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            int cursor = 0;
            int previousMatch = -2;

            for (int q = 0; q < query.Length; q++)
            {
                char wanted = char.ToLowerInvariant(query[q]);
                bool matched = false;

                while (cursor < candidate.Length)
                {
                    if (char.ToLowerInvariant(candidate[cursor]) == wanted)
                    {
                        score += BonusAt(candidate, cursor, previousMatch);
                        previousMatch = cursor;
                        cursor++;
                        matched = true;
                        break;
                    }

                    score -= PenaltySkip;
                    cursor++;
                }

                if (!matched)
                {
                    score = 0;
                    return false;
                }
            }

            return true;
        }

        private static int BonusAt(string candidate, int index, int previousMatch)
        {
            if (index == previousMatch + 1)
            {
                return ScoreContiguous;
            }

            if (index == 0)
            {
                return ScoreStart;
            }

            return IsWordBoundary(candidate, index) ? ScoreWordBoundary : 0;
        }

        private static bool IsWordBoundary(string candidate, int index)
        {
            char previous = candidate[index - 1];
            if (previous == '_' || previous == '-' || previous == '.' || previous == ' ')
            {
                return true;
            }

            return char.IsLower(previous) && char.IsUpper(candidate[index]);
        }
    }
}

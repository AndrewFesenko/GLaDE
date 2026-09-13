using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Per-problem progress kept in PlayerPrefs: attempts, problems solved unaided, guided runs.</summary>
    public static class ProgressStore
    {
        public struct Stats { public int attempts; public int solvedUnaided; public int guided; }

        static string Key(string problemId, string field) => $"glade.{problemId}.{field}";

        public static Stats Get(string problemId) => new Stats
        {
            attempts = PlayerPrefs.GetInt(Key(problemId, "attempts"), 0),
            solvedUnaided = PlayerPrefs.GetInt(Key(problemId, "solved"), 0),
            guided = PlayerPrefs.GetInt(Key(problemId, "guided"), 0),
        };

        public static void RecordAttempt(string problemId)
        {
            PlayerPrefs.SetInt(Key(problemId, "attempts"), PlayerPrefs.GetInt(Key(problemId, "attempts"), 0) + 1);
            PlayerPrefs.Save();
        }

        public static void RecordSolvedUnaided(string problemId)
        {
            PlayerPrefs.SetInt(Key(problemId, "solved"), PlayerPrefs.GetInt(Key(problemId, "solved"), 0) + 1);
            PlayerPrefs.Save();
        }

        public static void RecordGuided(string problemId)
        {
            PlayerPrefs.SetInt(Key(problemId, "guided"), PlayerPrefs.GetInt(Key(problemId, "guided"), 0) + 1);
            PlayerPrefs.Save();
        }

        public static string Summary(string problemId)
        {
            var s = Get(problemId);
            if (s.attempts == 0 && s.guided == 0) return "not attempted yet";
            return $"solved unaided {s.solvedUnaided}×  ·  guided {s.guided}×  ·  {s.attempts} answer check{(s.attempts == 1 ? "" : "s")}";
        }
    }
}

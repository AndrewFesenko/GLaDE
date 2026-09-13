using System.Collections.Generic;
using GLaDE.Core;
using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>All problems available in the game, in display order.</summary>
    [CreateAssetMenu(menuName = "GLaDE/Problem Library", fileName = "Problem Library")]
    public class ProblemLibrary : ScriptableObject
    {
        public List<StaticsProblem> problems = new List<StaticsProblem>();
    }
}

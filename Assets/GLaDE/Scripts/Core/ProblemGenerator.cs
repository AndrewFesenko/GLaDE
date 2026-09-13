using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GLaDE.Core
{
    /// <summary>
    /// Produces randomised, guaranteed-solvable instances of a problem and generates their teaching steps.
    /// </summary>
    public static class ProblemGenerator
    {
        /// <summary>Random instance drawn from the parameter grids. Falls back to the authored defaults if no valid draw is found.</summary>
        public static ProblemInstance Generate(StaticsProblem problem, int seed)
        {
            var rng = new System.Random(seed);
            int attempts = Mathf.Max(1, problem.validity.maxAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var values = new Dictionary<string, double>();
                foreach (var prm in problem.parameters) values[prm.name] = Draw(prm, rng);
                var inst = Build(problem, values, seed, out string reason);
                if (inst != null) return inst;
            }
            Debug.LogWarning($"[GLaDE] No valid random instance of '{problem.name}' after {attempts} attempts; using defaults.");
            return GenerateDefault(problem);
        }

        /// <summary>Instance using every parameter's default value.</summary>
        public static ProblemInstance GenerateDefault(StaticsProblem problem)
        {
            var values = new Dictionary<string, double>();
            foreach (var prm in problem.parameters) values[prm.name] = prm.defaultValue;
            var inst = Build(problem, values, 0, out string reason);
            if (inst == null) Debug.LogError($"[GLaDE] Default instance of '{problem.name}' is invalid: {reason}");
            return inst;
        }

        /// <summary>Instance from explicit parameter values (authoring / tests).</summary>
        public static ProblemInstance Build(StaticsProblem problem, Dictionary<string, double> values, int seed, out string reason)
        {
            var inst = new ProblemInstance { Problem = problem, Seed = seed };
            foreach (var kv in values) inst.Params[kv.Key] = kv.Value;
            if (!StaticsSolver.Solve(problem, inst, out reason)) return null;
            if (!Validate(problem, inst, out reason)) return null;
            SolutionGenerator.Generate(inst);
            inst.PromptText = FillPrompt(problem, inst);
            return inst;
        }

        static double Draw(ParameterDef prm, System.Random rng)
        {
            if (prm.step <= 0 || prm.max <= prm.min) return prm.defaultValue;
            int steps = Mathf.FloorToInt((prm.max - prm.min) / prm.step + 1e-4f);
            double v = prm.min + rng.Next(steps + 1) * prm.step;
            return Math.Round(v, 6);
        }

        public static bool Validate(StaticsProblem p, ProblemInstance inst, out string reason)
        {
            var rules = p.validity;
            var ids = inst.Nodes.Keys.ToList();
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    if ((inst.Nodes[ids[i]] - inst.Nodes[ids[j]]).Length < rules.minNodeSpacing)
                    { reason = $"nodes {ids[i]} and {ids[j]} too close"; return false; }

            double total = inst.TotalAppliedLoad();
            if (total < 1e-6) { reason = "no applied load"; return false; }
            foreach (var kv in inst.MemberForces)
                if (Math.Abs(kv.Value) > rules.maxForceRatio * total) { reason = $"member {kv.Key} force is {kv.Value:F1} kN, too large for {total:F1} kN of load"; return false; }
            foreach (var r in inst.Reactions)
                if (!r.IsMoment && Math.Abs(r.Value) > rules.maxForceRatio * total) { reason = $"reaction {r.Name} unreasonably large"; return false; }

            if (inst.Answers.Count > 0 && inst.Answers.Values.All(v => Math.Abs(v) < rules.minAnswerMagnitude))
            { reason = "every target answer is (nearly) zero"; return false; }

            reason = null;
            return true;
        }

        public static string FillPrompt(StaticsProblem p, ProblemInstance inst)
        {
            string text = p.prompt ?? "";
            foreach (var prm in p.parameters)
            {
                if (!inst.Params.TryGetValue(prm.name, out double v)) continue;
                string formatted = ProblemInstance.Fmt(v, prm.decimals) + (string.IsNullOrEmpty(prm.unit) ? "" : " " + prm.unit);
                text = text.Replace("{" + prm.name + "}", formatted);
            }
            return text;
        }

        /// <summary>Formatted "name = value unit" for each parameter, for labels.</summary>
        public static string FormatParameter(StaticsProblem p, ProblemInstance inst, string name)
        {
            var prm = p.parameters.FirstOrDefault(x => x.name == name);
            if (prm == null || !inst.Params.TryGetValue(name, out double v)) return name;
            return ProblemInstance.Fmt(v, prm.decimals) + (string.IsNullOrEmpty(prm.unit) ? "" : " " + prm.unit);
        }
    }
}

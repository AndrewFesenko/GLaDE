using System;
using System.Collections.Generic;
using System.Globalization;

namespace GLaDE.Core
{
    /// <summary>Double-precision 2D vector for the solver (problem space, metres and kN).</summary>
    public readonly struct V2
    {
        public readonly double x, y;
        public V2(double x, double y) { this.x = x; this.y = y; }
        public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
        public static V2 operator *(V2 a, double s) => new V2(a.x * s, a.y * s);
        public static V2 operator *(double s, V2 a) => new V2(a.x * s, a.y * s);
        public double Length => Math.Sqrt(x * x + y * y);
        public V2 Normalized { get { double l = Length; return l < 1e-12 ? new V2(0, 0) : new V2(x / l, y / l); } }
        public static double Cross(V2 a, V2 b) => a.x * b.y - a.y * b.x;
        public static double Dot(V2 a, V2 b) => a.x * b.x + a.y * b.y;
        public static V2 FromAngleDeg(double deg) => new V2(Math.Cos(deg * Math.PI / 180.0), Math.Sin(deg * Math.PI / 180.0));
        public double AngleDeg => Math.Atan2(y, x) * 180.0 / Math.PI;
        public UnityEngine.Vector2 ToVector2() => new UnityEngine.Vector2((float)x, (float)y);
        public override string ToString() => $"({x:0.###}, {y:0.###})";
    }

    /// <summary>A known force acting on the structure at a point (applied load or distributed-load resultant).</summary>
    public class AppliedForce
    {
        public string Id;
        public string Node;          // node id, or null for a resultant not at a node
        public V2 Position;
        public V2 Force;
        public double Magnitude => Force.Length;
        public bool FromDistributed;
        public string SourceDistributedId;
    }

    /// <summary>One unknown support reaction component.</summary>
    public class ReactionUnknown
    {
        public string Name;          // Ax, Ay, Gy, R_B, M_A
        public string Node;
        public V2 Direction;         // unit direction the positive reaction acts (ignored for moments)
        public bool IsMoment;
        public double Value;         // solved value (kN or kN·m)
        public V2 ForceVector => IsMoment ? new V2(0, 0) : Direction * Value;
    }

    /// <summary>One revealed teaching step, ready for the whiteboard.</summary>
    public class SolutionStep
    {
        public StepKind Kind;
        public string Title;
        public string Body;                                   // TextMeshPro rich text
        public List<string> HighlightMembers = new List<string>();
        public List<string> HighlightNodes = new List<string>();
        public Dictionary<string, double> Values = new Dictionary<string, double>();
    }

    /// <summary>
    /// A fully generated instance of a problem: concrete parameter values, solved geometry and
    /// forces, and the teaching steps that explain how to get there.
    /// </summary>
    public class ProblemInstance
    {
        public StaticsProblem Problem;
        public int Seed;
        public string PromptText;
        public Dictionary<string, double> Params = new Dictionary<string, double>();
        public Dictionary<string, V2> Nodes = new Dictionary<string, V2>();
        public List<AppliedForce> Loads = new List<AppliedForce>();          // concentrated loads only
        public List<AppliedForce> Resultants = new List<AppliedForce>();     // distributed-load resultants
        public List<ReactionUnknown> Reactions = new List<ReactionUnknown>();
        public Dictionary<string, double> MemberForces = new Dictionary<string, double>();   // + tension, − compression
        public Dictionary<string, double> Answers = new Dictionary<string, double>();
        public List<SolutionStep> Steps = new List<SolutionStep>();

        public IEnumerable<AppliedForce> AllKnownForces()
        {
            foreach (var l in Loads) yield return l;
            foreach (var r in Resultants) yield return r;
        }

        public ReactionUnknown FindReaction(string name)
        {
            foreach (var r in Reactions) if (r.Name == name) return r;
            return null;
        }

        public double TotalAppliedLoad()
        {
            double sum = 0;
            foreach (var f in AllKnownForces()) sum += f.Magnitude;
            return sum;
        }

        public bool TryGetAnswer(string target, out double value)
        {
            if (MemberForces.TryGetValue(target, out value)) return true;
            var m = Problem != null ? Problem.FindMember(target) : null;
            if (m != null && MemberForces.TryGetValue(m.Id, out value)) return true;
            var r = FindReaction(target);
            if (r != null) { value = r.Value; return true; }
            value = 0;
            return false;
        }

        public static string Fmt(double v, int decimals = 1)
        {
            if (Math.Abs(v) < 0.5 * Math.Pow(10, -decimals)) v = 0; // avoid "-0.0"
            return v.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string Force(double v) => Fmt(v, 1) + " kN";
        public static string Length(double v) => Fmt(v, 2) + " m";
        public static string TensionOrCompression(double v) => v < -1e-6 ? "C" : (v > 1e-6 ? "T" : "zero-force");
    }
}

using System;
using System.Collections.Generic;

namespace GLaDE.Core
{
    /// <summary>
    /// Builds and solves the 2D equilibrium system for a problem instance.
    /// Trusses: method of joints as a linear system (2 equations per joint; unknowns = member forces + reactions).
    /// Rigid bodies: sum Fx, sum Fy, sum M about the origin; unknowns = reactions.
    /// </summary>
    public static class StaticsSolver
    {
        const double Eps = 1e-9;

        /// <summary>Evaluates geometry and loads from the parameter values, then solves. Returns false with a reason on failure.</summary>
        public static bool Solve(StaticsProblem p, ProblemInstance inst, out string error)
        {
            error = null;
            inst.Nodes.Clear(); inst.Loads.Clear(); inst.Resultants.Clear(); inst.Reactions.Clear(); inst.MemberForces.Clear();

            // Geometry
            foreach (var nd in p.nodes)
            {
                if (string.IsNullOrEmpty(nd.id)) { error = "Node with empty id"; return false; }
                if (!Expr.TryEval(nd.x, inst.Params, out double px) || !Expr.TryEval(nd.y, inst.Params, out double py))
                { error = $"Bad coordinate expression on node {nd.id}"; return false; }
                inst.Nodes[nd.id] = new V2(px, py);
            }

            // Concentrated loads
            foreach (var l in p.loads)
            {
                if (!inst.Nodes.TryGetValue(l.node, out var pos)) { error = $"Load {l.id} references unknown node {l.node}"; return false; }
                if (!Expr.TryEval(l.magnitude, inst.Params, out double mag)) { error = $"Bad magnitude on load {l.id}"; return false; }
                inst.Loads.Add(new AppliedForce { Id = l.id, Node = l.node, Position = pos, Force = V2.FromAngleDeg(l.angleDeg) * mag });
            }

            // Distributed loads -> resultants (downward, trapezoidal)
            foreach (var d in p.distributedLoads)
            {
                if (!inst.Nodes.TryGetValue(d.nodeA, out var a) || !inst.Nodes.TryGetValue(d.nodeB, out var b))
                { error = $"Distributed load {d.id} references an unknown node"; return false; }
                if (!Expr.TryEval(d.intensityA, inst.Params, out double wA) || !Expr.TryEval(d.intensityB, inst.Params, out double wB))
                { error = $"Bad intensity on distributed load {d.id}"; return false; }
                double len = (b - a).Length;
                double R = 0.5 * (wA + wB) * len;
                double total = wA + wB;
                double t = total < Eps ? 0.5 : (wA + 2 * wB) / (3.0 * total); // centroid fraction from A
                inst.Resultants.Add(new AppliedForce
                {
                    Id = d.id, Node = null, Position = a + (b - a) * t, Force = new V2(0, -R),
                    FromDistributed = true, SourceDistributedId = d.id
                });
            }

            // Reaction unknowns
            foreach (var s in p.supports)
            {
                if (!inst.Nodes.ContainsKey(s.node)) { error = $"Support references unknown node {s.node}"; return false; }
                switch (s.type)
                {
                    case SupportType.Pin:
                        inst.Reactions.Add(new ReactionUnknown { Name = s.node + "x", Node = s.node, Direction = new V2(1, 0) });
                        inst.Reactions.Add(new ReactionUnknown { Name = s.node + "y", Node = s.node, Direction = new V2(0, 1) });
                        break;
                    case SupportType.Roller:
                    {
                        var dir = V2.FromAngleDeg(s.reactionAngleDeg);
                        string name = Math.Abs(dir.x) < 1e-6 ? s.node + "y" : (Math.Abs(dir.y) < 1e-6 ? s.node + "x" : "R" + s.node);
                        inst.Reactions.Add(new ReactionUnknown { Name = name, Node = s.node, Direction = dir });
                        break;
                    }
                    case SupportType.Fixed:
                        inst.Reactions.Add(new ReactionUnknown { Name = s.node + "x", Node = s.node, Direction = new V2(1, 0) });
                        inst.Reactions.Add(new ReactionUnknown { Name = s.node + "y", Node = s.node, Direction = new V2(0, 1) });
                        inst.Reactions.Add(new ReactionUnknown { Name = "M" + s.node, Node = s.node, IsMoment = true });
                        break;
                }
            }

            // Unknown ordering
            var unknownNames = new List<string>();
            if (p.kind == StructureKind.Truss)
                foreach (var m in p.members)
                {
                    if (!inst.Nodes.ContainsKey(m.a) || !inst.Nodes.ContainsKey(m.b)) { error = $"Member {m.Id} references an unknown node"; return false; }
                    if ((inst.Nodes[m.a] - inst.Nodes[m.b]).Length < Eps) { error = $"Member {m.Id} has zero length"; return false; }
                    unknownNames.Add(m.Id);
                }
            int memberCount = unknownNames.Count;
            foreach (var r in inst.Reactions) unknownNames.Add(r.Name);
            int n = unknownNames.Count;
            var index = new Dictionary<string, int>();
            for (int i = 0; i < n; i++) index[unknownNames[i]] = i;

            // Equations
            var rows = new List<double[]>();
            var rhs = new List<double>();

            if (p.kind == StructureKind.Truss)
            {
                foreach (var kv in inst.Nodes)
                {
                    string node = kv.Key; V2 pos = kv.Value;
                    var rx = new double[n]; var ry = new double[n];
                    double bx = 0, by = 0;
                    foreach (var m in p.members)
                    {
                        if (!m.Connects(node)) continue;
                        V2 u = (inst.Nodes[m.Other(node)] - pos).Normalized; // tension pulls joint toward the other end
                        rx[index[m.Id]] += u.x; ry[index[m.Id]] += u.y;
                    }
                    foreach (var r in inst.Reactions)
                    {
                        if (r.Node != node || r.IsMoment) continue;
                        rx[index[r.Name]] += r.Direction.x; ry[index[r.Name]] += r.Direction.y;
                    }
                    foreach (var f in inst.AllKnownForces())
                    {
                        if (f.Node != node) continue;
                        bx -= f.Force.x; by -= f.Force.y;
                    }
                    rows.Add(rx); rhs.Add(bx);
                    rows.Add(ry); rhs.Add(by);
                }
            }
            else
            {
                var rx = new double[n]; var ry = new double[n]; var rm = new double[n];
                double bx = 0, by = 0, bm = 0;
                foreach (var r in inst.Reactions)
                {
                    int i = index[r.Name];
                    if (r.IsMoment) { rm[i] += 1; continue; }
                    rx[i] += r.Direction.x; ry[i] += r.Direction.y;
                    rm[i] += V2.Cross(inst.Nodes[r.Node], r.Direction);
                }
                foreach (var f in inst.AllKnownForces())
                {
                    bx -= f.Force.x; by -= f.Force.y; bm -= V2.Cross(f.Position, f.Force);
                }
                rows.Add(rx); rhs.Add(bx);
                rows.Add(ry); rhs.Add(by);
                rows.Add(rm); rhs.Add(bm);
            }

            if (rows.Count != n)
            {
                error = rows.Count > n
                    ? $"Structure is unstable / a mechanism ({n} unknowns, {rows.Count} equations)"
                    : $"Structure is statically indeterminate ({n} unknowns, {rows.Count} equations)";
                return false;
            }

            var x = new double[n];
            if (!GaussianSolve(rows, rhs, x)) { error = "Equilibrium system is singular (degenerate geometry)"; return false; }

            for (int i = 0; i < memberCount; i++) inst.MemberForces[unknownNames[i]] = x[i];
            foreach (var r in inst.Reactions) r.Value = x[index[r.Name]];

            inst.Answers.Clear();
            foreach (var t in p.targets)
                if (inst.TryGetAnswer(t, out double v)) inst.Answers[t] = v;
                else { error = $"Target '{t}' is neither a member nor a reaction"; return false; }
            return true;
        }

        /// <summary>Gaussian elimination with partial pivoting. Returns false if singular.</summary>
        public static bool GaussianSolve(List<double[]> rows, List<double> rhs, double[] x)
        {
            int n = x.Length;
            var a = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) a[i, j] = rows[i][j];
                a[i, n] = rhs[i];
            }
            for (int col = 0; col < n; col++)
            {
                int piv = col; double best = Math.Abs(a[col, col]);
                for (int r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > best) { best = Math.Abs(a[r, col]); piv = r; }
                if (best < Eps) return false;
                if (piv != col) for (int j = 0; j <= n; j++) { double t = a[col, j]; a[col, j] = a[piv, j]; a[piv, j] = t; }
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = a[r, col] / a[col, col];
                    if (Math.Abs(f) < Eps) continue;
                    for (int j = col; j <= n; j++) a[r, j] -= f * a[col, j];
                }
            }
            for (int i = 0; i < n; i++) x[i] = a[i, n] / a[i, i];
            return true;
        }

        /// <summary>Nodes reachable from start without crossing any member in cutMembers.</summary>
        public static HashSet<string> SideOfCut(StaticsProblem p, string start, ICollection<string> cutMembers)
        {
            var cut = new HashSet<string>();
            foreach (var c in cutMembers) { var m = p.FindMember(c); if (m != null) cut.Add(m.Id); }
            var seen = new HashSet<string> { start };
            var stack = new Stack<string>(); stack.Push(start);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var m in p.members)
                {
                    if (cut.Contains(m.Id) || !m.Connects(node)) continue;
                    var o = m.Other(node);
                    if (seen.Add(o)) stack.Push(o);
                }
            }
            return seen;
        }

        /// <summary>Signed perpendicular distance from point P to the infinite line through A and B.</summary>
        public static double DistanceToLine(V2 p, V2 a, V2 b)
        {
            var u = (b - a).Normalized;
            return V2.Cross(u, p - a);
        }
    }
}

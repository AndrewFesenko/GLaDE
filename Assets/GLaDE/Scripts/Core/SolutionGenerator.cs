using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GLaDE.Core
{
    /// <summary>
    /// Turns the authored solution plan into teaching steps with the numbers of a concrete instance.
    /// Every equation shown is built from the actual geometry, so the arithmetic on the whiteboard is
    /// always consistent with the 3D model the student is looking at.
    /// </summary>
    public static class SolutionGenerator
    {
        const double Eps = 1e-6;

        public static void Generate(ProblemInstance inst)
        {
            inst.Steps.Clear();
            var ctx = new Ctx(inst);
            foreach (var spec in inst.Problem.solutionPlan)
            {
                SolutionStep step;
                try
                {
                    switch (spec.kind)
                    {
                        case StepKind.FreeBodyWhole: step = ctx.FreeBodyWhole(spec); break;
                        case StepKind.Reactions: step = ctx.Reactions(spec); break;
                        case StepKind.Resultants: step = ctx.Resultants(spec); break;
                        case StepKind.Section: step = ctx.Section(spec); break;
                        case StepKind.Joint: step = ctx.Joint(spec); break;
                        case StepKind.FinalAnswer: step = ctx.FinalAnswer(spec); break;
                        default: continue;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GLaDE] Could not generate step '{spec.kind}': {e.Message}");
                    continue;
                }
                if (!string.IsNullOrEmpty(spec.title)) step.Title = spec.title;
                if (!string.IsNullOrWhiteSpace(spec.teachingNote)) step.Body += "\n\n<i>" + spec.teachingNote.Trim() + "</i>";
                inst.Steps.Add(step);
            }
        }

        // ------------------------------------------------------------------ symbols & formatting

        /// <summary>Rich-text symbol for a member force, reaction, or load id.</summary>
        public static string Sym(string name, StaticsProblem p = null)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.Contains("_")) { var parts = name.Split('_'); return parts[0] + "<sub>" + parts[1] + "</sub>"; }
            if (p != null && p.IsMemberId(name)) return "F<sub>" + name + "</sub>";
            if (name.Length >= 2 && (name.EndsWith("x") || name.EndsWith("y")))
                return name.Substring(0, name.Length - 1) + "<sub>" + name[name.Length - 1] + "</sub>";
            if (name.Length >= 2 && (name[0] == 'R' || name[0] == 'M') && char.IsUpper(name[1]))
                return name[0] + "<sub>" + name.Substring(1) + "</sub>";
            return name;
        }

        static string F(double v) => ProblemInstance.Force(v);
        static string L(double v) => ProblemInstance.Length(v);
        static string N(double v, int d = 1) => ProblemInstance.Fmt(v, d);
        static string Deg(double v) => ProblemInstance.Fmt(v, 1) + "°";
        static string TC(double v) => ProblemInstance.TensionOrCompression(v);
        static string Answer(double v) => v < -Eps ? F(-v) + " (C)" : (v > Eps ? F(v) + " (T)" : "0 (zero-force member)");
        static string Bold(string s) => "<b>" + s + "</b>";
        static string Eq(string s) => "<color=#FFD166>" + s + "</color>";
        static string Result(string s) => "<color=#06D6A0><b>" + s + "</b></color>";

        /// <summary>Formats "+ a(b)" style terms with a leading sign.</summary>
        static string Term(double sign, string content, bool first)
        {
            if (Math.Abs(sign) < Eps) return "";
            string s = sign < 0 ? "− " : (first ? "" : "+ ");
            return s + content + " ";
        }

        // ------------------------------------------------------------------ context

        class Ctx
        {
            readonly ProblemInstance inst;
            readonly StaticsProblem p;
            readonly HashSet<string> solvedMembers = new HashSet<string>();
            readonly HashSet<string> solvedReactions = new HashSet<string>();

            public Ctx(ProblemInstance instance) { inst = instance; p = instance.Problem; }

            string S(string name) => Sym(name, p);

            V2 Node(string id)
            {
                if (!inst.Nodes.TryGetValue(id, out var v)) throw new Exception($"unknown node '{id}'");
                return v;
            }

            /// <summary>Unit vector along a member as seen from the end at node j (tension pulls the joint toward the other end).</summary>
            V2 Dir(MemberDef m, string j) => (Node(m.Other(j)) - Node(j)).Normalized;

            string SupportDescription()
            {
                var parts = new List<string>();
                foreach (var s in p.supports)
                {
                    switch (s.type)
                    {
                        case SupportType.Pin: parts.Add($"the pin at {s.node} supplies two reaction components, {S(s.node + "x")} and {S(s.node + "y")}"); break;
                        case SupportType.Roller:
                        {
                            var r = inst.Reactions.First(x => x.Node == s.node);
                            parts.Add($"the roller at {s.node} supplies a single reaction {S(r.Name)} perpendicular to its surface");
                            break;
                        }
                        case SupportType.Fixed: parts.Add($"the fixed support at {s.node} supplies {S(s.node + "x")}, {S(s.node + "y")} and a couple moment {S("M" + s.node)}"); break;
                    }
                }
                return string.Join("; ", parts);
            }

            // -------------------------------------------------------------- steps

            public SolutionStep FreeBodyWhole(StepSpec spec)
            {
                var sb = new StringBuilder();
                string body = p.kind == StructureKind.Truss ? "the entire truss" : "the whole beam";
                sb.Append($"Before writing any equation, isolate {body} as one rigid body and draw its free-body diagram.\n\n");
                sb.Append("Remove each support and replace it with the forces it can exert: ").Append(SupportDescription()).Append(".\n\n");
                if (inst.Loads.Count > 0)
                {
                    sb.Append("Keep the applied loads exactly where they act: ");
                    sb.Append(string.Join(", ", inst.Loads.Select(l => $"{S(l.Id)} = {F(l.Magnitude)} at {l.Node}")));
                    sb.Append(".\n\n");
                }
                if (inst.Resultants.Count > 0)
                    sb.Append("Distributed loads stay drawn as distributed for now; we will replace them with resultants when we need to sum moments.\n\n");
                sb.Append("Count the unknowns: ").Append(inst.Reactions.Count).Append(" reaction components. A single rigid body in a plane gives us three independent equilibrium equations, so ")
                  .Append(inst.Reactions.Count <= 3 ? "the reactions are solvable from this diagram alone." : "we will need to isolate parts of the structure as well.");
                var step = new SolutionStep { Kind = StepKind.FreeBodyWhole, Title = "Free-body diagram of the whole structure", Body = sb.ToString() };
                step.HighlightNodes.AddRange(p.supports.Select(s => s.node));
                return step;
            }

            public SolutionStep Resultants(StepSpec spec)
            {
                var sb = new StringBuilder();
                sb.Append("A distributed load is just many small forces. For equilibrium we may replace it with one equivalent resultant: the area under the loading diagram, acting through the centroid of that area.\n");
                foreach (var d in p.distributedLoads)
                {
                    var r = inst.Resultants.First(x => x.SourceDistributedId == d.id);
                    var a = Node(d.nodeA); var b = Node(d.nodeB);
                    double len = (b - a).Length;
                    double wA = Expr.Eval(d.intensityA, inst.Params), wB = Expr.Eval(d.intensityB, inst.Params);
                    double fromA = (r.Position - a).Length;
                    sb.Append("\n").Append(Bold($"Load {S(d.id)} between {d.nodeA} and {d.nodeB}")).Append("\n");
                    if (Math.Abs(wA - wB) < Eps)
                        sb.Append(Eq($"{S(d.id)}·L = {N(wA, 2)} kN/m × {L(len)} = {F(r.Magnitude)}")).Append("\n")
                          .Append($"Uniform, so the resultant acts at the middle: {L(fromA)} from {d.nodeA}.\n");
                    else
                        sb.Append(Eq($"½({N(wA, 2)} + {N(wB, 2)}) kN/m × {L(len)} = {F(r.Magnitude)}")).Append("\n")
                          .Append($"Trapezoidal, so the centroid sits {L(fromA)} from {d.nodeA}, closer to the heavier end.\n");
                    step_values[d.id] = r.Magnitude;
                }
                var step = new SolutionStep { Kind = StepKind.Resultants, Title = "Replace distributed loads with resultants", Body = sb.ToString() };
                foreach (var kv in step_values) step.Values[kv.Key] = kv.Value;
                step_values.Clear();
                return step;
            }
            readonly Dictionary<string, double> step_values = new Dictionary<string, double>();

            public SolutionStep Reactions(StepSpec spec)
            {
                var sb = new StringBuilder();
                var step = new SolutionStep { Kind = StepKind.Reactions, Title = "Find the support reactions" };
                var unsolved = inst.Reactions.Where(r => !solvedReactions.Contains(r.Name)).ToList();
                sb.Append("Treat the whole structure as one rigid body. ").Append(char.ToUpper(SupportDescription()[0]) + SupportDescription().Substring(1)).Append(". ")
                  .Append($"That is {unsolved.Count} unknown{(unsolved.Count == 1 ? "" : "s")}, and we have three equations: ΣM = 0, ΣF<sub>y</sub> = 0, ΣF<sub>x</sub> = 0.\n\n");

                string about = string.IsNullOrEmpty(spec.momentAbout) ? p.supports[0].node : spec.momentAbout;
                V2 P = Node(about);

                // --- Moment equation about P
                var passThrough = new List<ReactionUnknown>();
                var momentUnknowns = new List<(ReactionUnknown r, double coef)>();
                foreach (var r in unsolved)
                {
                    double c = r.IsMoment ? 1.0 : V2.Cross(Node(r.Node) - P, r.Direction);
                    if (Math.Abs(c) < Eps) passThrough.Add(r); else momentUnknowns.Add((r, c));
                }
                sb.Append(Bold($"Sum moments about {about}. "));
                if (passThrough.Count > 0)
                    sb.Append(string.Join(" and ", passThrough.Select(r => S(r.Name)))).Append($" pass{(passThrough.Count == 1 ? "es" : "")} through {about}, so {(passThrough.Count == 1 ? "it contributes" : "they contribute")} no moment. ");
                if (momentUnknowns.Count == 1) sb.Append($"That leaves {S(momentUnknowns[0].r.Name)} as the only unknown. ");
                sb.Append("Counter-clockwise positive:\n");
                var eq = new StringBuilder($"ΣM<sub>{about}</sub> = 0:   ");
                bool first = true;
                foreach (var f in inst.AllKnownForces())
                {
                    double M = V2.Cross(f.Position - P, f.Force);
                    if (Math.Abs(M) < Eps) continue;
                    double arm = M / f.Magnitude;
                    eq.Append(Term(Math.Sign(M), $"({F(f.Magnitude)})({L(Math.Abs(arm))})", first)); first = false;
                }
                foreach (var (r, c) in momentUnknowns)
                {
                    eq.Append(Term(Math.Sign(c), r.IsMoment ? S(r.Name) : $"{S(r.Name)}({L(Math.Abs(c))})", first)); first = false;
                }
                eq.Append("= 0");
                sb.Append(Eq(eq.ToString())).Append("\n");
                if (momentUnknowns.Count == 1)
                {
                    var (r, c) = momentUnknowns[0];
                    sb.Append(Result($"{S(r.Name)} = {F(r.Value)}")).Append(r.Value < 0 ? "   (negative: it acts opposite to the direction assumed)\n\n" : "\n\n");
                    solvedReactions.Add(r.Name); step.Values[r.Name] = r.Value;
                }
                else
                {
                    foreach (var (r, c) in momentUnknowns) { sb.Append(Result($"{S(r.Name)} = {F(r.Value)}")).Append("  "); solvedReactions.Add(r.Name); step.Values[r.Name] = r.Value; }
                    sb.Append("\n(solved together with the force equations below)\n\n");
                }

                // --- Vertical
                SumForces(sb, step, 'y');
                // --- Horizontal
                SumForces(sb, step, 'x');

                foreach (var r in inst.Reactions) if (!solvedReactions.Contains(r.Name)) { solvedReactions.Add(r.Name); step.Values[r.Name] = r.Value; }
                step.Body = sb.ToString().TrimEnd();
                step.HighlightNodes.AddRange(p.supports.Select(s => s.node));
                return step;
            }

            void SumForces(StringBuilder sb, SolutionStep step, char axis)
            {
                var unsolved = inst.Reactions.Where(r => !solvedReactions.Contains(r.Name) && !r.IsMoment).ToList();
                var involved = unsolved.Where(r => Math.Abs(axis == 'y' ? r.Direction.y : r.Direction.x) > Eps).ToList();
                if (involved.Count == 0) return;
                string axisName = axis == 'y' ? "vertically" : "horizontally";
                sb.Append(Bold($"Sum forces {axisName}. ")).Append(axis == 'y' ? "Upward positive:\n" : "Rightward positive:\n");
                var eq = new StringBuilder($"ΣF<sub>{axis}</sub> = 0:   ");
                bool first = true;
                foreach (var r in inst.Reactions)
                {
                    if (r.IsMoment) continue;
                    double c = axis == 'y' ? r.Direction.y : r.Direction.x;
                    if (Math.Abs(c) < Eps) continue;
                    if (solvedReactions.Contains(r.Name)) { double v = c * r.Value; eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); }
                    else eq.Append(Term(Math.Sign(c), Math.Abs(Math.Abs(c) - 1) < Eps ? S(r.Name) : $"{S(r.Name)}({N(Math.Abs(c), 3)})", first));
                    first = false;
                }
                foreach (var f in inst.AllKnownForces())
                {
                    double v = axis == 'y' ? f.Force.y : f.Force.x;
                    if (Math.Abs(v) < Eps) continue;
                    eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false;
                }
                eq.Append("= 0");
                sb.Append(Eq(eq.ToString())).Append("\n");
                foreach (var r in involved)
                {
                    sb.Append(Result($"{S(r.Name)} = {F(r.Value)}")).Append("  ");
                    solvedReactions.Add(r.Name); step.Values[r.Name] = r.Value;
                }
                sb.Append("\n\n");
            }

            public SolutionStep Section(StepSpec spec)
            {
                if (p.kind != StructureKind.Truss) throw new Exception("Section steps apply to trusses only");
                var cut = spec.cutMembers.Select(id => p.FindMember(id) ?? throw new Exception($"unknown member '{id}'")).ToList();
                var kept = StaticsSolver.SideOfCut(p, spec.keepNode, spec.cutMembers);
                var discarded = inst.Nodes.Keys.Where(n => !kept.Contains(n)).ToList();
                if (discarded.Count == 0) throw new Exception("the chosen cut does not separate the truss");
                double keptX = kept.Average(n => Node(n).x), otherX = discarded.Average(n => Node(n).x);
                string sideName = keptX < otherX ? "left" : "right";
                var step = new SolutionStep { Kind = StepKind.Section, Title = $"Cut through {string.Join(", ", cut.Select(m => m.Id))}" };
                step.HighlightMembers.AddRange(cut.Select(m => m.Id));

                var sb = new StringBuilder();
                sb.Append($"Pass an imaginary section through {Join(cut.Select(m => m.Id))} — the members we want — and keep the <b>{sideName}</b> portion (the side containing joint {spec.keepNode}). ");
                sb.Append("Draw that portion alone: its applied loads, the support reactions acting on it, and one force for each cut member. ");
                sb.Append("Assume every cut member is in <b>tension</b> (pulling away from the cut). A negative answer then simply means compression.\n\n");
                sb.Append($"Three unknowns, three equations. Pick each equation so that it contains as few unknowns as possible.\n");

                var keptLoads = inst.Loads.Where(l => kept.Contains(l.Node)).ToList();
                var keptReactions = inst.Reactions.Where(r => kept.Contains(r.Node)).ToList();

                foreach (var eqSpec in spec.equations)
                {
                    ParseEquation(eqSpec, out char type, out string aboutNode, out string targetId);
                    var target = p.FindMember(targetId) ?? throw new Exception($"unknown member '{targetId}' in equation '{eqSpec}'");
                    if (!cut.Contains(target)) throw new Exception($"{targetId} is not one of the cut members");
                    sb.Append("\n");
                    if (type == 'M') MomentEquation(sb, step, cut, kept, keptLoads, keptReactions, aboutNode, target);
                    else ForceEquation(sb, step, cut, kept, keptLoads, keptReactions, type, target);
                }
                step.Body = sb.ToString().TrimEnd();
                return step;
            }

            static string Join(IEnumerable<string> items)
            {
                var l = items.ToList();
                if (l.Count <= 1) return string.Join("", l);
                return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[l.Count - 1];
            }

            static void ParseEquation(string spec, out char type, out string about, out string target)
            {
                // "M:D=KJ" | "Fy=KD" | "Fx=CD"
                var parts = spec.Split('=');
                if (parts.Length != 2) throw new Exception($"bad equation '{spec}'");
                target = parts[1].Trim();
                var lhs = parts[0].Trim();
                about = null;
                if (lhs.StartsWith("M")) { type = 'M'; about = lhs.Substring(lhs.IndexOf(':') + 1).Trim(); }
                else if (lhs.Equals("Fy", StringComparison.OrdinalIgnoreCase)) type = 'y';
                else if (lhs.Equals("Fx", StringComparison.OrdinalIgnoreCase)) type = 'x';
                else throw new Exception($"bad equation '{spec}'");
            }

            /// <summary>Kept-side end of a cut member.</summary>
            string KeptEnd(MemberDef m, HashSet<string> kept)
            {
                bool a = kept.Contains(m.a), b = kept.Contains(m.b);
                if (a == b) throw new Exception($"member {m.Id} is not cut by this section");
                return a ? m.a : m.b;
            }

            void MomentEquation(StringBuilder sb, SolutionStep step, List<MemberDef> cut, HashSet<string> kept,
                List<AppliedForce> loads, List<ReactionUnknown> reactions, string aboutNode, MemberDef target)
            {
                V2 P = Node(aboutNode);
                step.HighlightNodes.Add(aboutNode);
                var dropOut = new List<string>(); var alsoUnknown = new List<MemberDef>();
                foreach (var m in cut)
                {
                    if (m == target) continue;
                    string j = KeptEnd(m, kept);
                    double c = V2.Cross(Node(j) - P, Dir(m, j));
                    if (Math.Abs(c) < 1e-6) dropOut.Add(m.Id);
                    else if (!solvedMembers.Contains(m.Id)) alsoUnknown.Add(m);
                }
                sb.Append(Bold($"Sum moments about {aboutNode}. "));
                if (dropOut.Count > 0)
                    sb.Append($"The lines of action of {Join(dropOut.Select(S))} pass through {aboutNode}, so they create no moment there. ");
                if (alsoUnknown.Count == 0) sb.Append($"{S(target.Id)} is the only unknown left. ");
                sb.Append("Counter-clockwise positive:\n");

                var eq = new StringBuilder($"ΣM<sub>{aboutNode}</sub> = 0:   ");
                bool first = true;
                foreach (var f in loads)
                {
                    double M = V2.Cross(f.Position - P, f.Force);
                    if (Math.Abs(M) < Eps) continue;
                    eq.Append(Term(Math.Sign(M), $"({F(f.Magnitude)})({L(Math.Abs(M / f.Magnitude))})", first)); first = false;
                }
                foreach (var r in reactions)
                {
                    if (r.IsMoment) { eq.Append(Term(Math.Sign(r.Value), F(Math.Abs(r.Value)), first)); first = false; continue; }
                    double M = V2.Cross(Node(r.Node) - P, r.ForceVector);
                    if (Math.Abs(M) < Eps) continue;
                    eq.Append(Term(Math.Sign(M), $"({F(Math.Abs(r.Value))})({L(Math.Abs(M / Math.Abs(r.Value)))})", first)); first = false;
                }
                foreach (var m in cut)
                {
                    if (m == target) continue;
                    string j = KeptEnd(m, kept);
                    double c = V2.Cross(Node(j) - P, Dir(m, j));
                    if (Math.Abs(c) < 1e-6) continue;
                    double val = inst.MemberForces[m.Id];
                    double M = c * val;
                    eq.Append(Term(Math.Sign(M), $"({F(Math.Abs(val))})({L(Math.Abs(c))})", first)); first = false;
                }
                string tj = KeptEnd(target, kept);
                double tc = V2.Cross(Node(tj) - P, Dir(target, tj));
                if (Math.Abs(tc) < 1e-6) throw new Exception($"{target.Id} passes through {aboutNode}; moments about {aboutNode} cannot find it");
                eq.Append(Term(Math.Sign(tc), $"{S(target.Id)}({L(Math.Abs(tc))})", first));
                eq.Append("= 0");
                sb.Append(Eq(eq.ToString())).Append("\n");
                double v = inst.MemberForces[target.Id];
                sb.Append($"The arm of {S(target.Id)} about {aboutNode} is the perpendicular distance from {aboutNode} to member {target.Id}, {L(Math.Abs(tc))}.\n");
                sb.Append(Result($"{S(target.Id)} = {F(v)}  →  {Answer(v)}")).Append("\n");
                solvedMembers.Add(target.Id); step.Values[target.Id] = v;
            }

            void ForceEquation(StringBuilder sb, SolutionStep step, List<MemberDef> cut, HashSet<string> kept,
                List<AppliedForce> loads, List<ReactionUnknown> reactions, char axis, MemberDef target)
            {
                Func<V2, double> comp = v => axis == 'y' ? v.y : v.x;
                var dropOut = new List<string>(); var alsoUnknown = new List<MemberDef>();
                foreach (var m in cut)
                {
                    if (m == target) continue;
                    string j = KeptEnd(m, kept);
                    if (Math.Abs(comp(Dir(m, j))) < 1e-6) dropOut.Add(m.Id);
                    else if (!solvedMembers.Contains(m.Id)) alsoUnknown.Add(m);
                }
                string axisWord = axis == 'y' ? "vertical" : "horizontal";
                sb.Append(Bold($"Sum {axisWord} forces. "));
                if (dropOut.Count > 0)
                    sb.Append($"{Join(dropOut.Select(S))} {(dropOut.Count == 1 ? "is" : "are")} {(axis == 'y' ? "horizontal" : "vertical")}, so {(dropOut.Count == 1 ? "it has" : "they have")} no {axisWord} component. ");
                var solvedOthers = cut.Where(m => m != target && solvedMembers.Contains(m.Id) && !dropOut.Contains(m.Id)).ToList();
                if (solvedOthers.Count > 0) sb.Append($"Substitute the value of {Join(solvedOthers.Select(m => S(m.Id)))} found above. ");
                if (alsoUnknown.Count == 0) sb.Append($"{S(target.Id)} is the only unknown left. ");
                string tj = KeptEnd(target, kept);
                V2 tu = Dir(target, tj);
                double tcoef = comp(tu);
                if (Math.Abs(tcoef) < 1e-6) throw new Exception($"{target.Id} has no {axisWord} component");
                if (Math.Abs(Math.Abs(tcoef) - 1) > 1e-6)
                {
                    double ang = Math.Abs(Math.Atan2(tu.y, tu.x) * 180 / Math.PI); if (ang > 90) ang = 180 - ang;
                    sb.Append($"Member {target.Id} rises at {Deg(ang)} from the horizontal, so its {axisWord} component is {S(target.Id)}·{(axis == 'y' ? "sin" : "cos")} {Deg(ang)} = {N(Math.Abs(tcoef), 3)} {S(target.Id)}. ");
                }
                sb.Append(axis == 'y' ? "Upward positive:\n" : "Rightward positive:\n");

                var eq = new StringBuilder($"ΣF<sub>{axis}</sub> = 0:   ");
                bool first = true;
                foreach (var r in reactions)
                {
                    if (r.IsMoment) continue;
                    double v = comp(r.ForceVector); if (Math.Abs(v) < Eps) continue;
                    eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false;
                }
                foreach (var f in loads)
                {
                    double v = comp(f.Force); if (Math.Abs(v) < Eps) continue;
                    eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false;
                }
                foreach (var m in cut)
                {
                    if (m == target) continue;
                    string j = KeptEnd(m, kept);
                    double c = comp(Dir(m, j)); if (Math.Abs(c) < 1e-6) continue;
                    double v = c * inst.MemberForces[m.Id];
                    eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false;
                }
                eq.Append(Term(Math.Sign(tcoef), Math.Abs(Math.Abs(tcoef) - 1) < 1e-6 ? S(target.Id) : $"{S(target.Id)}({N(Math.Abs(tcoef), 3)})", first));
                eq.Append("= 0");
                sb.Append(Eq(eq.ToString())).Append("\n");
                double val = inst.MemberForces[target.Id];
                sb.Append(Result($"{S(target.Id)} = {F(val)}  →  {Answer(val)}")).Append("\n");
                solvedMembers.Add(target.Id); step.Values[target.Id] = val;
            }

            public SolutionStep Joint(StepSpec spec)
            {
                if (p.kind != StructureKind.Truss) throw new Exception("Joint steps apply to trusses only");
                string J = spec.joint; V2 pos = Node(J);
                var at = p.members.Where(m => m.Connects(J)).ToList();
                var unknown = at.Where(m => !solvedMembers.Contains(m.Id)).ToList();
                var known = at.Where(m => solvedMembers.Contains(m.Id)).ToList();
                var step = new SolutionStep { Kind = StepKind.Joint, Title = $"Equilibrium of joint {J}" };
                step.HighlightNodes.Add(J); step.HighlightMembers.AddRange(at.Select(m => m.Id));
                var sb = new StringBuilder();
                sb.Append($"Isolate the pin at {J}. Every member meeting there pulls on the pin along its own axis if in tension. ");
                sb.Append($"Joint {J} connects {Join(at.Select(m => m.Id))}");
                if (known.Count > 0) sb.Append($"; {Join(known.Select(m => S(m.Id)))} {(known.Count == 1 ? "is" : "are")} already known");
                sb.Append($". With {unknown.Count} unknown{(unknown.Count == 1 ? "" : "s")} and two equations, the joint is solvable.\n");
                if (unknown.Count > 2) throw new Exception($"joint {J} has {unknown.Count} unknown members");

                foreach (char axis in new[] { 'x', 'y' })
                {
                    Func<V2, double> comp = v => axis == 'y' ? v.y : v.x;
                    var eq = new StringBuilder($"ΣF<sub>{axis}</sub> = 0:   ");
                    bool first = true;
                    foreach (var r in inst.Reactions.Where(r => r.Node == J && !r.IsMoment))
                    { double v = comp(r.ForceVector); if (Math.Abs(v) < Eps) continue; eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false; }
                    foreach (var f in inst.Loads.Where(l => l.Node == J))
                    { double v = comp(f.Force); if (Math.Abs(v) < Eps) continue; eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false; }
                    foreach (var m in known)
                    { double v = comp(Dir(m, J)) * inst.MemberForces[m.Id]; if (Math.Abs(v) < Eps) continue; eq.Append(Term(Math.Sign(v), F(Math.Abs(v)), first)); first = false; }
                    foreach (var m in unknown)
                    {
                        double c = comp(Dir(m, J)); if (Math.Abs(c) < 1e-6) continue;
                        eq.Append(Term(Math.Sign(c), Math.Abs(Math.Abs(c) - 1) < 1e-6 ? S(m.Id) : $"{S(m.Id)}({N(Math.Abs(c), 3)})", first)); first = false;
                    }
                    eq.Append("= 0");
                    sb.Append("\n").Append(Eq(eq.ToString())).Append("\n");
                }
                sb.Append("\nSolving the two equations together:\n");
                foreach (var m in unknown)
                {
                    double v = inst.MemberForces[m.Id];
                    sb.Append(Result($"{S(m.Id)} = {F(v)}  →  {Answer(v)}")).Append("\n");
                    solvedMembers.Add(m.Id); step.Values[m.Id] = v;
                }
                step.Body = sb.ToString().TrimEnd();
                return step;
            }

            public SolutionStep FinalAnswer(StepSpec spec)
            {
                var step = new SolutionStep { Kind = StepKind.FinalAnswer, Title = "Answer" };
                var sb = new StringBuilder();
                foreach (var t in p.targets)
                {
                    if (!inst.TryGetAnswer(t, out double v)) continue;
                    bool isMember = p.IsMemberId(t);
                    sb.Append(Result($"{S(t)} = {(isMember ? Answer(v) : F(v))}")).Append("\n");
                    step.Values[t] = v;
                    if (isMember) step.HighlightMembers.Add(p.FindMember(t).Id);
                }
                if (p.kind == StructureKind.Truss)
                    sb.Append("\nCheck the signs against the diagram: a compression member pushes on its joints, a tension member pulls. ");
                else
                    sb.Append("\nA positive reaction acts in the direction drawn on the free-body diagram; a negative one acts the opposite way. ");
                sb.Append("If a result looks unreasonably large compared with the applied loads, revisit the moment arms.");
                step.Body = sb.ToString();
                return step;
            }
        }
    }
}

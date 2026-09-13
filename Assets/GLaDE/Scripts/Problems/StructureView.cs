using System;
using System.Collections.Generic;
using System.Linq;
using GLaDE.Core;
using TMPro;
using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>
    /// Builds the room-scale 3D model of a problem instance (members, joints, supports, loads, labels,
    /// dimension lines) and exposes it to the interaction layer. Pivot of this transform is the
    /// centre-bottom of the structure.
    /// </summary>
    public class StructureView : MonoBehaviour
    {
        public VisualTheme theme;
        [Tooltip("TextMeshPro font size per metre of desired cap height. Tune once for the font in use.")]
        public float pointsPerMetre = 10f;

        public ProblemInstance Instance { get; private set; }
        public float Scale { get; private set; } = 0.15f;
        public Transform KeptGroup { get; private set; }
        public Transform DiscardedGroup { get; private set; }

        readonly Dictionary<string, MemberView> members = new Dictionary<string, MemberView>();
        readonly Dictionary<string, Transform> nodeObjects = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> nodeLocal = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Transform> nodeGroups = new Dictionary<string, Transform>();   // node id -> group holding joint, label, supports, loads at that node
        readonly Dictionary<string, Transform> loadArrows = new Dictionary<string, Transform>();
        readonly List<Transform> dimensionObjects = new List<Transform>();
        readonly List<GameObject> stubs = new List<GameObject>();
        Transform root;
        double minX, maxX, minY, maxY;

        public IEnumerable<MemberView> Members => members.Values;
        public MemberView GetMember(string id)
        {
            if (members.TryGetValue(id, out var m)) return m;
            var def = Instance?.Problem.FindMember(id);
            return def != null && members.TryGetValue(def.Id, out m) ? m : null;
        }
        public bool HasNode(string id) => nodeLocal.ContainsKey(id);
        public Vector3 NodeWorld(string id) => root.TransformPoint(nodeLocal[id]);
        public Vector3 NodeLocal(string id) => nodeLocal[id];
        public Transform Root => root;

        /// <summary>Converts a problem-space point (metres) to a local point of the structure root.</summary>
        public Vector3 ToLocal(V2 p) => new Vector3((float)((p.x - (minX + maxX) * 0.5) * Scale), (float)((p.y - minY) * Scale), 0f);
        public Vector3 ToWorld(V2 p) => root.TransformPoint(ToLocal(p));
        public float HeightMetres => (float)((maxY - minY) * Scale);
        public float WidthMetres => (float)((maxX - minX) * Scale);

        // ------------------------------------------------------------------ build

        public void Build(ProblemInstance inst, VisualTheme visualTheme, float scale)
        {
            Clear();
            Instance = inst; theme = visualTheme; Scale = scale;
            root = new GameObject("Structure").transform;
            root.SetParent(transform, false);

            minX = inst.Nodes.Values.Min(v => v.x); maxX = inst.Nodes.Values.Max(v => v.x);
            minY = inst.Nodes.Values.Min(v => v.y); maxY = inst.Nodes.Values.Max(v => v.y);
            foreach (var kv in inst.Nodes) nodeLocal[kv.Key] = ToLocal(kv.Value);

            var p = inst.Problem;
            bool isBeam = p.kind == StructureKind.RigidBody;
            float radius = isBeam ? theme.memberRadius * 2.2f : theme.memberRadius;

            foreach (var m in p.members)
            {
                var go = new GameObject("Member " + m.Id);
                go.transform.SetParent(root, false);
                var view = go.AddComponent<MemberView>();
                view.Configure(m.Id, m.a, m.b, nodeLocal[m.a], nodeLocal[m.b], radius, theme);
                members[m.Id] = view;
            }

            foreach (var kv in inst.Nodes)
            {
                string id = kv.Key;
                var group = new GameObject("Node " + id).transform;
                group.SetParent(root, false);
                group.localPosition = nodeLocal[id];
                nodeGroups[id] = group;

                if (!isBeam || p.FindSupport(id) != null || inst.Loads.Any(l => l.Node == id))
                {
                    var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    joint.name = "Joint";
                    MeshFactory.SafeDestroy(joint.GetComponent<Collider>());
                    joint.transform.SetParent(group, false);
                    float jr = isBeam ? theme.jointRadius * 0.6f : theme.jointRadius;
                    joint.transform.localScale = Vector3.one * jr * 2;
                    joint.GetComponent<MeshRenderer>().sharedMaterial = theme.joint;
                    nodeObjects[id] = joint.transform;
                }

                bool bottom = Math.Abs(kv.Value.y - minY) < 1e-6;
                bool hasSupport = p.FindSupport(id) != null;
                var label = MakeLabel(group, id, theme.labelSize, theme.labelColor, FontStyles.Bold);
                float off = theme.jointRadius + theme.labelSize * 0.9f;
                label.transform.localPosition = bottom && !hasSupport ? new Vector3(0, -off, 0) : new Vector3(0, off, 0);
                if (hasSupport) label.transform.localPosition = new Vector3(-theme.jointRadius * 2.6f, off * 0.6f, 0);
            }

            foreach (var s in p.supports) BuildSupport(s, nodeGroups[s.node]);
            lowestArrowY = 0f;
            float maxLoad = (float)Math.Max(1e-3, inst.Loads.Count > 0 ? inst.Loads.Max(l => l.Magnitude) : 1);
            foreach (var l in inst.Loads) BuildLoadArrow(l, maxLoad);
            foreach (var d in p.distributedLoads) BuildDistributedLoad(d, inst);
            BuildDimensions(inst);
        }

        public void Clear()
        {
            if (root != null) MeshFactory.SafeDestroy(root.gameObject);
            if (KeptGroup != null) MeshFactory.SafeDestroy(KeptGroup.gameObject);
            if (DiscardedGroup != null) MeshFactory.SafeDestroy(DiscardedGroup.gameObject);
            root = null; KeptGroup = null; DiscardedGroup = null;
            members.Clear(); nodeObjects.Clear(); nodeLocal.Clear(); nodeGroups.Clear(); loadArrows.Clear(); dimensionObjects.Clear(); stubs.Clear();
            Instance = null;
        }

        TextMeshPro MakeLabel(Transform parent, string text, float sizeMetres, Color color, FontStyles style = FontStyles.Normal, bool billboard = true)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = sizeMetres * pointsPerMetre;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            tmp.sortingOrder = 10;
            if (billboard) go.AddComponent<Billboard>();
            return tmp;
        }

        void BuildSupport(SupportDef s, Transform group)
        {
            float jr = theme.jointRadius;
            var holder = new GameObject("Support " + s.type).transform;
            holder.SetParent(group, false);
            float plateY;
            switch (s.type)
            {
                case SupportType.Pin:
                {
                    var tri = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tri.name = "Pin";
                    MeshFactory.SafeDestroy(tri.GetComponent<Collider>());
                    tri.transform.SetParent(holder, false);
                    tri.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    tri.transform.localPosition = new Vector3(0, -jr * 1.5f, 0);
                    tri.transform.localScale = new Vector3(jr * 2.4f, jr * 2.4f, jr * 1.6f);
                    tri.GetComponent<MeshRenderer>().sharedMaterial = theme.support;
                    plateY = -jr * 3.3f;
                    break;
                }
                case SupportType.Roller:
                {
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        wheel.name = "Wheel";
                        MeshFactory.SafeDestroy(wheel.GetComponent<Collider>());
                        wheel.transform.SetParent(holder, false);
                        wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                        wheel.transform.localPosition = new Vector3(i * jr * 1.1f, -jr * 2.2f, 0);
                        wheel.transform.localScale = new Vector3(jr * 1.8f, jr * 0.8f, jr * 1.8f);
                        wheel.GetComponent<MeshRenderer>().sharedMaterial = theme.support;
                    }
                    plateY = -jr * 3.3f;
                    break;
                }
                default: // Fixed: a wall block beside the node
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Wall";
                    MeshFactory.SafeDestroy(wall.GetComponent<Collider>());
                    wall.transform.SetParent(holder, false);
                    wall.transform.localPosition = new Vector3(-jr * 2.5f, 0, 0);
                    wall.transform.localScale = new Vector3(jr * 2f, jr * 10f, jr * 6f);
                    wall.GetComponent<MeshRenderer>().sharedMaterial = theme.support;
                    return;
                }
            }
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            MeshFactory.SafeDestroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(holder, false);
            plate.transform.localPosition = new Vector3(0, plateY, 0);
            plate.transform.localScale = new Vector3(jr * 5f, jr * 0.5f, jr * 3f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = theme.support;

            // Stand down to the floor so the structure feels physically supported.
            float worldPlateY = holder.TransformPoint(new Vector3(0, plateY, 0)).y;
            if (worldPlateY > 0.05f)
            {
                var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stand.name = "Stand";
                MeshFactory.SafeDestroy(stand.GetComponent<Collider>());
                stand.transform.SetParent(holder, false);
                float h = worldPlateY / holder.lossyScale.y;
                stand.transform.localPosition = new Vector3(0, plateY - h * 0.5f, 0);
                stand.transform.localScale = new Vector3(jr * 1.4f, h * 0.5f, jr * 1.4f);
                stand.GetComponent<MeshRenderer>().sharedMaterial = theme.stand;
            }
        }

        float lowestArrowY;   // local y of the lowest hanging load arrow, so dimension lines can stay clear

        void BuildLoadArrow(AppliedForce l, float maxLoad)
        {
            var group = nodeGroups[l.Node];
            Vector3 dir = new Vector3((float)l.Force.x, (float)l.Force.y, 0).normalized;
            float len = Mathf.Lerp(theme.arrowMinLength, theme.arrowMaxLength, (float)(l.Magnitude / maxLoad));
            var arrow = MeshFactory.Arrow("Load " + l.Id, len, theme.arrowShaftRadius, theme.loadArrow, group);
            arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);

            // Textbook convention: the arrow pushes onto the body (tip at the joint) when there is free space
            // behind it; otherwise it hangs off the joint (tail at the joint), e.g. a downward load on a
            // bottom-chord joint, so it never runs through the structure.
            Vector3 nodeLocalPos = nodeLocal[l.Node];
            Vector3 pushMid = nodeLocalPos - dir * (len * 0.5f);
            bool hang = InsideStructure(pushMid);
            Vector3 gap = dir * (theme.jointRadius * 0.8f);
            arrow.transform.localPosition = hang ? gap : -dir * (len + theme.jointRadius * 0.8f);
            float tailY = nodeLocalPos.y + (hang ? gap.y : -dir.y * (len + theme.jointRadius * 0.8f));
            float tipY = tailY + dir.y * len;
            lowestArrowY = Mathf.Min(lowestArrowY, Mathf.Min(tailY, tipY));

            var label = MakeLabel(group, SolutionGenerator.Sym(l.Id) + " = " + ProblemInstance.Force(l.Magnitude), theme.labelSize * 0.85f, theme.loadLabelColor);
            // Beside the arrow rather than on its line, so it never sits on top of a member.
            Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized;
            if (Mathf.Abs(side.x) < 0.5f) side = Vector3.right;
            Vector3 along = hang ? dir * (len * 0.55f + theme.jointRadius) : -dir * (len * 0.55f);
            label.transform.localPosition = along + side * (theme.labelSize * 1.2f);
            label.alignment = side.x > 0 ? TMPro.TextAlignmentOptions.Left : TMPro.TextAlignmentOptions.Right;
            label.rectTransform.pivot = new Vector2(side.x > 0 ? 0f : 1f, 0.5f);   // text starts at the label position
            loadArrows[l.Id] = arrow.transform;
        }

        /// <summary>True if a local point lies inside the structure's bounding box (with a small margin).</summary>
        bool InsideStructure(Vector3 local)
        {
            float halfW = WidthMetres * 0.5f, h = HeightMetres, m = theme.jointRadius;
            if (h < 1e-4f) return false;   // a beam has no interior to avoid
            return local.x > -halfW + m && local.x < halfW - m && local.y > m && local.y < h - m;
        }

        void BuildDistributedLoad(DistributedLoadDef d, ProblemInstance inst)
        {
            Vector3 a = nodeLocal[d.nodeA], b = nodeLocal[d.nodeB];
            double wA = Expr.Eval(d.intensityA, inst.Params), wB = Expr.Eval(d.intensityB, inst.Params);
            double wMax = Math.Max(Math.Abs(wA), Math.Abs(wB));
            if (wMax < 1e-6) return;
            var holder = new GameObject("Distributed " + d.id).transform;
            holder.SetParent(root, false);
            float length = Vector3.Distance(a, b);
            int count = Mathf.Clamp(Mathf.RoundToInt(length / 0.09f), 3, 16);
            float maxH = theme.arrowMaxLength * 0.8f;
            var tops = new List<Vector3>();
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                double w = wA + (wB - wA) * t;
                float h = (float)(Math.Abs(w) / wMax) * maxH;
                if (h < 0.02f) { tops.Add(Vector3.Lerp(a, b, t) + Vector3.up * (theme.memberRadius * 2.2f)); continue; }
                var arrow = MeshFactory.Arrow("w" + i, h, theme.arrowShaftRadius * 0.7f, theme.loadArrow, holder);
                Vector3 basePos = Vector3.Lerp(a, b, t) + Vector3.up * (theme.memberRadius * 2.2f);
                arrow.transform.localPosition = basePos + Vector3.up * h;
                arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.down);
                tops.Add(basePos + Vector3.up * h);
            }
            for (int i = 0; i < tops.Count - 1; i++) MakeLine(holder, tops[i], tops[i + 1], theme.arrowShaftRadius * 0.5f, theme.loadArrow);
            string text = Math.Abs(wA - wB) < 1e-6
                ? SolutionGenerator.Sym(d.id) + " = " + ProblemInstance.Fmt(wA, 1) + " kN/m"
                : SolutionGenerator.Sym(d.id) + " = " + ProblemInstance.Fmt(wA, 1) + " → " + ProblemInstance.Fmt(wB, 1) + " kN/m";
            var label = MakeLabel(holder, text, theme.labelSize * 0.85f, theme.loadLabelColor);
            label.transform.localPosition = (a + b) * 0.5f + Vector3.up * (theme.arrowMaxLength + theme.jointRadius + theme.labelSize * 1.4f); // clear of the tallest point-load arrow
        }

        void BuildDimensions(ProblemInstance inst)
        {
            var holder = new GameObject("Dimensions").transform;
            holder.SetParent(root, false);
            dimensionObjects.Add(holder);
            float yLine = Mathf.Min(-(theme.jointRadius * 4f + theme.labelSize * 2.2f), lowestArrowY - theme.labelSize * 1.6f);
            var xs = inst.Nodes.Values.Select(v => v.x).Distinct().OrderBy(x => x).ToList();
            float tick = theme.labelSize * 0.5f;
            Vector3 L = new Vector3((float)((xs[0] - (minX + maxX) * 0.5) * Scale), yLine, 0);
            Vector3 R = new Vector3((float)((xs[xs.Count - 1] - (minX + maxX) * 0.5) * Scale), yLine, 0);
            MakeLine(holder, L, R, theme.arrowShaftRadius * 0.4f, theme.support);
            for (int i = 0; i < xs.Count; i++)
            {
                float x = (float)((xs[i] - (minX + maxX) * 0.5) * Scale);
                MakeLine(holder, new Vector3(x, yLine - tick, 0), new Vector3(x, yLine + tick, 0), theme.arrowShaftRadius * 0.4f, theme.support);
                if (i < xs.Count - 1)
                {
                    double span = xs[i + 1] - xs[i];
                    float xm = (float)(((xs[i] + xs[i + 1]) * 0.5 - (minX + maxX) * 0.5) * Scale);
                    var lab = MakeLabel(holder, ProblemInstance.Fmt(span, 1) + " m", theme.labelSize * 0.7f, theme.dimensionColor, FontStyles.Normal, false);
                    lab.transform.localPosition = new Vector3(xm, yLine - theme.labelSize * 0.9f, 0);
                }
            }
            if (maxY - minY > 1e-6)
            {
                float xLine = (float)((minX - (minX + maxX) * 0.5) * Scale) - theme.jointRadius * 4f - theme.labelSize * 1.5f;
                Vector3 B = new Vector3(xLine, 0, 0), T = new Vector3(xLine, HeightMetres, 0);
                MakeLine(holder, B, T, theme.arrowShaftRadius * 0.4f, theme.support);
                MakeLine(holder, B - Vector3.right * tick, B + Vector3.right * tick, theme.arrowShaftRadius * 0.4f, theme.support);
                MakeLine(holder, T - Vector3.right * tick, T + Vector3.right * tick, theme.arrowShaftRadius * 0.4f, theme.support);
                var lab = MakeLabel(holder, ProblemInstance.Fmt(maxY - minY, 1) + " m", theme.labelSize * 0.7f, theme.dimensionColor, FontStyles.Normal, false);
                lab.transform.localPosition = new Vector3(xLine - theme.labelSize * 1.6f, HeightMetres * 0.5f, 0);
            }
        }

        GameObject MakeLine(Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Line";
            MeshFactory.SafeDestroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(parent, false);
            cyl.transform.localPosition = (a + b) * 0.5f;
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            cyl.transform.localScale = new Vector3(radius * 2, Vector3.Distance(a, b) * 0.5f, radius * 2);
            cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return cyl;
        }

        // ------------------------------------------------------------------ highlighting

        public void ClearMemberHighlights()
        {
            foreach (var m in members.Values) m.SetHighlighted(false);
        }

        public void HighlightMembers(IEnumerable<string> ids, bool on)
        {
            foreach (var id in ids) { var m = GetMember(id); if (m != null) m.SetHighlighted(on); }
        }

        public void HighlightNodes(IEnumerable<string> ids, bool on)
        {
            foreach (var id in ids)
            {
                if (!nodeObjects.TryGetValue(id, out var joint)) continue;
                joint.localScale = Vector3.one * theme.jointRadius * (on ? 3.2f : 2f);
                joint.GetComponent<MeshRenderer>().sharedMaterial = on ? theme.memberHighlight : theme.joint;
            }
        }

        public void ClearNodeHighlights() => HighlightNodes(nodeObjects.Keys.ToList(), false);

        /// <summary>Colours members by the sign of their solved force.</summary>
        public void ShowMemberForces(bool show)
        {
            foreach (var kv in members)
            {
                if (!show) { if (kv.Value.State != MemberState.Ghost) kv.Value.SetState(MemberState.Normal); continue; }
                if (kv.Value.State == MemberState.Ghost) continue;
                if (!Instance.MemberForces.TryGetValue(kv.Key, out double f)) continue;
                kv.Value.SetState(f > 1e-6 ? MemberState.Tension : (f < -1e-6 ? MemberState.Compression : MemberState.ZeroForce));
            }
        }

        // ------------------------------------------------------------------ sectioning

        /// <summary>Every member crossing the plane, with the crossing point.</summary>
        public List<(MemberView view, Vector3 point, float t)> MembersCrossing(Plane plane)
        {
            var list = new List<(MemberView, Vector3, float)>();
            foreach (var m in members.Values)
                if (m.State != MemberState.Ghost && m.IntersectsPlane(plane, out var pt, out float t)) list.Add((m, pt, t));
            return list;
        }

        /// <summary>
        /// Physically separates the structure: kept nodes go into KeptGroup, the rest into DiscardedGroup.
        /// Cut members are shortened to their kept portion; a ghost stub shows the discarded remainder.
        /// Returns the cut points on the kept side (world space) keyed by member id.
        /// </summary>
        public Dictionary<string, Vector3> Split(HashSet<string> keptNodes, Dictionary<string, Vector3> cutPointsWorld)
        {
            KeptGroup = new GameObject("Kept").transform; KeptGroup.SetParent(root, false);
            DiscardedGroup = new GameObject("Discarded").transform; DiscardedGroup.SetParent(root, false);
            var keptCutPoints = new Dictionary<string, Vector3>();

            foreach (var kv in nodeGroups) kv.Value.SetParent(keptNodes.Contains(kv.Key) ? KeptGroup : DiscardedGroup, true);

            foreach (var kv in members)
            {
                var m = kv.Value;
                bool aKept = keptNodes.Contains(m.nodeA), bKept = keptNodes.Contains(m.nodeB);
                if (aKept == bKept)
                {
                    m.transform.SetParent(aKept ? KeptGroup : DiscardedGroup, true);
                    continue;
                }
                // cut member: shorten to the kept portion
                if (!cutPointsWorld.TryGetValue(kv.Key, out var cutWorld)) cutWorld = (m.WorldA + m.WorldB) * 0.5f;
                Vector3 cutLocal = root.InverseTransformPoint(cutWorld);
                string keptNode = aKept ? m.nodeA : m.nodeB;
                string otherNode = aKept ? m.nodeB : m.nodeA;
                Vector3 keptEnd = nodeLocal[keptNode], otherEnd = nodeLocal[otherNode];
                float radius = m.body.transform.localScale.x * 0.5f;

                var stub = new GameObject("Stub " + m.memberId);
                stub.transform.SetParent(root, false);
                var stubView = stub.AddComponent<MemberView>();
                stubView.Configure(m.memberId, otherNode, keptNode, otherEnd, cutLocal, radius, theme);
                stub.name = "Stub " + m.memberId;
                stub.transform.SetParent(DiscardedGroup, true);
                stubs.Add(stub);

                m.transform.SetParent(root, true);
                m.Configure(m.memberId, keptNode, otherNode, keptEnd, cutLocal, radius, theme);
                m.transform.SetParent(KeptGroup, true);
                keptCutPoints[kv.Key] = root.TransformPoint(cutLocal);
            }
            return keptCutPoints;
        }

        /// <summary>Fades one half (members, joints, loads, supports) to the ghost look, or restores it.</summary>
        public void SetGroupGhost(Transform group, bool ghost)
        {
            if (group == null) return;
            foreach (var m in group.GetComponentsInChildren<MemberView>())
                m.SetState(ghost ? MemberState.Ghost : MemberState.Normal);
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponentInParent<MemberView>() != null) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue; // text keeps its own material; only its alpha fades
                if (ghost) { if (r.sharedMaterial != theme.memberGhost) r.gameObject.AddComponent<GhostMemory>().original = r.sharedMaterial; r.sharedMaterial = theme.memberGhost; }
                else { var mem = r.GetComponent<GhostMemory>(); if (mem != null) { r.sharedMaterial = mem.original; MeshFactory.SafeDestroy(mem); } }
            }
            foreach (var t in group.GetComponentsInChildren<TMPro.TextMeshPro>())
                t.alpha = ghost ? 0.25f : 1f;
        }

        /// <summary>Remembers a renderer's material while it is ghosted.</summary>
        public class GhostMemory : MonoBehaviour { public Material original; }

        /// <summary>Slides the discarded half away from the kept half so the cut reads clearly.</summary>
        public void SeparateHalves(HashSet<string> keptNodes, float gap)
        {
            if (KeptGroup == null || DiscardedGroup == null) return;
            float keptX = keptNodes.Average(n => nodeLocal[n].x);
            float otherX = nodeLocal.Where(kv => !keptNodes.Contains(kv.Key)).Average(kv => kv.Value.x);
            float sign = otherX >= keptX ? 1f : -1f;
            DiscardedGroup.localPosition = new Vector3(sign * gap, 0, 0);
        }

        /// <summary>Reverses Split(): everything back under the root, stubs removed, states reset.</summary>
        public void Unsplit()
        {
            if (KeptGroup == null && DiscardedGroup == null) return;
            foreach (var s in stubs) MeshFactory.SafeDestroy(s);
            stubs.Clear();
            foreach (var kv in nodeGroups) kv.Value.SetParent(root, true);
            float radius = theme.memberRadius * (Instance.Problem.kind == StructureKind.RigidBody ? 2.2f : 1f);
            foreach (var kv in members)
            {
                var m = kv.Value;
                m.transform.SetParent(root, true);
                var def = Instance.Problem.FindMember(kv.Key);
                m.Configure(def.Id, def.a, def.b, nodeLocal[def.a], nodeLocal[def.b], radius, theme);
            }
            if (KeptGroup != null) MeshFactory.SafeDestroy(KeptGroup.gameObject);
            if (DiscardedGroup != null) MeshFactory.SafeDestroy(DiscardedGroup.gameObject);
            KeptGroup = null; DiscardedGroup = null;
        }

        /// <summary>Colliders of all members on one side, for a "grab this half" interactable.</summary>
        public List<Collider> CollidersUnder(Transform group)
        {
            return group.GetComponentsInChildren<Collider>().ToList();
        }
    }
}

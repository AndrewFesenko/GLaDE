using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GLaDE.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    public enum Phase { Isolate, ChooseSide, BuildFbd, Solve, Done }

    /// <summary>
    /// Runs one problem scene: generates a randomised instance, builds the 3D structure, and drives the
    /// decomposition loop (isolate a body → build its free-body diagram → step through the solution).
    /// Everything problem-specific comes from the <see cref="StaticsProblem"/> asset.
    /// </summary>
    public class ProblemManager : MonoBehaviour
    {
        [Header("Content")]
        public StaticsProblem problem;
        public VisualTheme theme;
        [Tooltip("0 = pick a fresh random seed each time.")]
        public int seed = 0;

        [Header("Scene pieces")]
        public StructureView structure;
        public Whiteboard whiteboard;
        public SectionPlane sectionPlane;
        public Transform tokenRack;
        public int tokenCount = 8;
        public float tokenSpacing = 0.11f;

        public ProblemInstance Instance { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int StepIndex { get; private set; }
        public bool Assisted { get; private set; }
        public IReadOnlyList<ForceSocket> Sockets => sockets;
        public IReadOnlyList<ForceToken> Tokens => tokens;
        public HashSet<string> KeptNodes { get; private set; } = new HashSet<string>();

        readonly List<ForceToken> tokens = new List<ForceToken>();
        readonly List<ForceSocket> sockets = new List<ForceSocket>();
        Dictionary<string, Vector3> cutPoints = new Dictionary<string, Vector3>();
        List<string> cutMemberIds = new List<string>();
        HashSet<string> sideA, sideB;
        XRSimpleInteractable pickA, pickB;
        XRGrabInteractable bodyGrab;
        Vector3 bodyHome;
        int hintsUsed;
        StepSpec plannedSection;
        System.Random seeder = new System.Random();

        // ------------------------------------------------------------------ lifecycle

        void Start()
        {
            if (ProblemHub.SelectedProblem != null) problem = ProblemHub.SelectedProblem;
            if (whiteboard != null)
            {
                whiteboard.NextPressed += NextStep;
                whiteboard.BackPressed += PreviousStep;
                whiteboard.HintPressed += Hint;
                whiteboard.SkipPressed += ShowMeHow;
                whiteboard.NewProblemPressed += NewProblem;
                whiteboard.ResetPressed += ResetAttempt;
            }
            if (sectionPlane != null) sectionPlane.Released += OnCutReleased;
            NewProblem();
        }

        /// <summary>Fresh random instance of the same problem.</summary>
        public void NewProblem()
        {
            int s = seed != 0 ? seed : seeder.Next(1, int.MaxValue);
            LoadInstance(ProblemGenerator.Generate(problem, s));
        }

        /// <summary>Same numbers, start the decomposition over.</summary>
        public void ResetAttempt()
        {
            if (Instance == null) { NewProblem(); return; }
            LoadInstance(Instance);
        }

        public void LoadInstance(ProblemInstance inst)
        {
            if (inst == null) { Debug.LogError("[GLaDE] No problem instance"); return; }
            Instance = inst;
            hintsUsed = 0; Assisted = false; StepIndex = 0;
            KeptNodes.Clear(); cutPoints.Clear(); cutMemberIds.Clear();
            ClearSockets(); ClearSidePickers(); ClearBodyGrab();

            structure.Build(inst, theme, problem.worldScale);
            BuildTokens();
            plannedSection = problem.solutionPlan.FirstOrDefault(s => s.kind == StepKind.Section);

            whiteboard?.SetHeader($"{problem.title}  <size=70%><alpha=#99>{problem.sourceReference}</alpha></size>", inst.PromptText);
            whiteboard?.ClearStep();
            whiteboard?.ShowFeedback("", 0);

            if (sectionPlane != null)
            {
                bool useCut = problem.kind == StructureKind.Truss && plannedSection != null;
                sectionPlane.SetActive(useCut);
                if (useCut) sectionPlane.ResetPose();
            }
            if (problem.kind == StructureKind.RigidBody) EnableBodyGrab();
            EnterPhase(Phase.Isolate);
        }

        // ------------------------------------------------------------------ phases

        void EnterPhase(Phase p)
        {
            CurrentPhase = p;
            switch (p)
            {
                case Phase.Isolate:
                    whiteboard?.SetPhase(problem.kind == StructureKind.Truss
                        ? "<b>1 · Isolate a body.</b>  Grab the section plane and pass it through the members you are asked about. A section can only cut <b>three</b> unknown members — three equations, three unknowns."
                        : "<b>1 · Isolate the body.</b>  Grab the beam and lift it clear of its supports. Whatever touched it must be replaced by a force.");
                    whiteboard?.SetProgress("");
                    whiteboard?.ConfigureButtons(next: false, back: false, hint: true, skip: hintsUsed > 0);
                    break;
                case Phase.ChooseSide:
                    whiteboard?.SetPhase("<b>2 · Choose the part to keep.</b>  Grab the half you want to analyse. Either works — the one with fewer loads means less arithmetic.");
                    whiteboard?.ConfigureButtons(next: false, back: false, hint: true, skip: hintsUsed > 0);
                    break;
                case Phase.BuildFbd:
                    whiteboard?.SetPhase("<b>" + (problem.kind == StructureKind.Truss ? "3" : "2") + " · Build the free-body diagram.</b>  Everything the isolated body was touching becomes a force. Drop a force token on every cut member end and every support reaction. Tokens take their name from where you put them.");
                    UpdateFbdProgress();
                    whiteboard?.ConfigureButtons(next: false, back: false, hint: true, skip: hintsUsed > 0);
                    break;
                case Phase.Solve:
                    whiteboard?.SetPhase("<b>" + (problem.kind == StructureKind.Truss ? "4" : "3") + " · Solve.</b>  Step through the equilibrium equations. Each step explains <i>why</i> that equation is the right one to write next.");
                    whiteboard?.SetProgress("");
                    StepIndex = 0;
                    ShowStep(true);
                    break;
                case Phase.Done:
                    whiteboard?.SetPhase(Assisted
                        ? "<b>Done (with help).</b>  Try a new problem and see if you can get through the cut and the free-body diagram on your own."
                        : "<b>Done.</b>  Every step of that was yours. Generate a new problem — the numbers change, the method does not.");
                    whiteboard?.ConfigureButtons(next: false, back: true, hint: false, skip: false);
                    break;
            }
        }

        // ------------------------------------------------------------------ isolate: truss cut

        void OnCutReleased(List<MemberView> crossed, Dictionary<string, Vector3> points)
        {
            if (CurrentPhase != Phase.Isolate || problem.kind != StructureKind.Truss) return;
            var ids = crossed.Select(m => m.memberId).ToList();
            if (ids.Count == 0) { whiteboard?.ShowFeedback("The plane is not cutting anything yet. Move it into the truss."); return; }

            var planned = plannedSection.cutMembers.Select(id => problem.FindMember(id).Id).ToHashSet();
            var actual = ids.ToHashSet();
            if (actual.SetEquals(planned))
            {
                cutMemberIds = ids; cutPoints = points;
                CommitCut();
                return;
            }
            if (ids.Count > 3)
                whiteboard?.ShowFeedback($"That section cuts <b>{ids.Count}</b> members ({string.Join(", ", ids)}). Equilibrium of a section gives only three equations, so three unknown forces is the most we can solve. Tilt or move the plane.");
            else if (planned.Overlaps(actual))
                whiteboard?.ShowFeedback($"Close: you cut {string.Join(", ", ids)}. The question asks for {string.Join(", ", planned)}. One section through all three of them lets you solve them together.");
            else
                whiteboard?.ShowFeedback($"That is a valid section ({string.Join(", ", ids)}), but none of those are the members you were asked about. Slide the plane so it passes through {string.Join(", ", planned)}.");
        }

        void CommitCut()
        {
            sideA = StaticsSolver.SideOfCut(problem, plannedSection.keepNode, cutMemberIds);
            sideB = Instance.Nodes.Keys.Where(n => !sideA.Contains(n)).ToHashSet();
            foreach (var m in structure.Members) m.SetHighlighted(false);
            structure.Split(sideA, cutPoints);
            structure.SeparateHalves(sideA, 0.12f);
            sectionPlane?.SetActive(false);
            whiteboard?.ShowFeedback($"Section through {string.Join(", ", cutMemberIds)} — good. The truss is now two bodies.");
            MakeSidePickers();
            EnterPhase(Phase.ChooseSide);
        }

        void MakeSidePickers()
        {
            pickA = MakePicker(structure.KeptGroup, () => ChooseSide(sideA));
            pickB = MakePicker(structure.DiscardedGroup, () => ChooseSide(sideB));
        }

        XRSimpleInteractable MakePicker(Transform group, Action onPick)
        {
            var go = new GameObject("Pick " + group.name);
            go.transform.SetParent(group, false);
            var si = go.AddComponent<XRSimpleInteractable>();
            si.colliders.Clear();
            si.colliders.AddRange(structure.CollidersUnder(group));
            si.selectEntered.AddListener(_ => onPick());
            return si;
        }

        void ClearSidePickers()
        {
            if (pickA) MeshFactory.SafeDestroy(pickA.gameObject);
            if (pickB) MeshFactory.SafeDestroy(pickB.gameObject);
            pickA = null; pickB = null;
        }

        public void ChooseSide(HashSet<string> kept)
        {
            if (CurrentPhase != Phase.ChooseSide) return;
            ClearSidePickers();
            if (!ReferenceEquals(kept, sideA))
            {
                // Student kept the other half: rebuild the split the other way round and regenerate the section text for that side.
                structure.Unsplit();
                structure.Split(kept, cutPoints);
                structure.SeparateHalves(kept, 0.12f);
                string keepNode = kept.First();
                foreach (var s in problem.solutionPlan) if (s.kind == StepKind.Section && s == plannedSection) s.keepNode = keepNode;
                SolutionGenerator.Generate(Instance);
                foreach (var s in problem.solutionPlan) if (s.kind == StepKind.Section && s == plannedSection) s.keepNode = plannedSection.keepNode;
            }
            KeptNodes = kept;
            structure.SetGroupGhost(structure.DiscardedGroup, true);
            BuildSockets();
            EnterPhase(Phase.BuildFbd);
        }

        // ------------------------------------------------------------------ isolate: rigid body lift

        void EnableBodyGrab()
        {
            var root = structure.Root.gameObject;
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();   // explicit null check: Unity's fake null defeats ??
            rb.isKinematic = true; rb.useGravity = false;
            bodyGrab = root.GetComponent<XRGrabInteractable>();
            if (bodyGrab == null) bodyGrab = root.AddComponent<XRGrabInteractable>();
            bodyGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            bodyGrab.useDynamicAttach = true;
            bodyGrab.throwOnDetach = false;
            bodyGrab.retainTransformParent = true;
            bodyGrab.colliders.Clear();
            bodyGrab.colliders.AddRange(structure.Members.Select(m => (Collider)m.capsule));
            bodyGrab.selectExited.AddListener(OnBodyReleased);
            bodyHome = root.transform.position;
        }

        void ClearBodyGrab()
        {
            if (bodyGrab != null) { bodyGrab.selectExited.RemoveListener(OnBodyReleased); MeshFactory.SafeDestroy(bodyGrab); bodyGrab = null; }
            if (structure.Root != null) { var rb = structure.Root.GetComponent<Rigidbody>(); if (rb) MeshFactory.SafeDestroy(rb); }
        }

        void Update()
        {
            if (CurrentPhase == Phase.Isolate && bodyGrab != null && bodyGrab.isSelected && Vector3.Distance(structure.Root.position, bodyHome) > 0.12f)
                IsolateBody();
        }

        void OnBodyReleased(SelectExitEventArgs args)
        {
            if (CurrentPhase == Phase.Isolate && Vector3.Distance(structure.Root.position, bodyHome) > 0.12f) IsolateBody();
        }

        void IsolateBody()
        {
            if (CurrentPhase != Phase.Isolate) return;
            KeptNodes = Instance.Nodes.Keys.ToHashSet();
            // supports stay behind as ghosts
            foreach (var t in structure.Root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Support ")).ToList())
                structure.SetGroupGhost(t, true);
            whiteboard?.ShowFeedback("The beam is free. Its supports are gone — so their reactions must now be drawn as forces.");
            BuildSockets();
            EnterPhase(Phase.BuildFbd);
        }

        // ------------------------------------------------------------------ free-body diagram

        void BuildSockets()
        {
            ClearSockets();
            Transform parent = structure.KeptGroup != null ? structure.KeptGroup : structure.Root;
            Quaternion rot = structure.Root.rotation;
            // cut member ends
            foreach (var id in cutMemberIds)
            {
                var view = structure.GetMember(id);
                if (view == null || !cutPoints.TryGetValue(id, out var pt)) continue;
                Vector3 keptEnd = KeptNodes.Contains(view.nodeA) ? view.WorldA : view.WorldB;
                Vector3 dir = (view.WorldB - view.WorldA).normalized;
                if (!KeptNodes.Contains(view.nodeA)) dir = -dir; // tension: away from the kept joint
                var cutWorld = structure.KeptGroup != null ? structure.KeptGroup.TransformPoint(structure.Root.InverseTransformPoint(pt)) : pt;
                var s = ForceSocket.Create(id, SolutionGenerator.Sym(id, problem), false, cutWorld, dir, parent, theme, theme.labelColor);
                s.Changed += OnSocketChanged;
                sockets.Add(s);
            }
            // reactions on the kept side
            foreach (var r in Instance.Reactions)
            {
                if (!KeptNodes.Contains(r.Node) || r.IsMoment) continue;
                Vector3 pos = structure.NodeWorld(r.Node);
                if (structure.KeptGroup != null) pos = structure.KeptGroup.TransformPoint(structure.Root.InverseTransformPoint(pos));
                Vector3 dir = rot * new Vector3((float)r.Direction.x, (float)r.Direction.y, 0);
                // offset the two pin components slightly so both sockets are reachable
                if (Math.Abs(r.Direction.x) > 0.5) pos += rot * new Vector3(0, -0.035f, 0);
                var s = ForceSocket.Create(r.Name, SolutionGenerator.Sym(r.Name, problem), true, pos, dir, parent, theme, theme.reactionLabelColor);
                s.Changed += OnSocketChanged;
                sockets.Add(s);
            }
            if (tokens.Count < sockets.Count + 2) BuildTokens(sockets.Count + 2);
        }

        void ClearSockets()
        {
            foreach (var s in sockets) if (s) { s.ForceRelease(); MeshFactory.SafeDestroy(s.gameObject); }
            sockets.Clear();
            foreach (var t in tokens) if (t) t.ReturnHome();
        }

        void OnSocketChanged(ForceSocket s)
        {
            if (CurrentPhase != Phase.BuildFbd) return;
            UpdateFbdProgress();
            if (sockets.All(x => x.IsFilled))
            {
                whiteboard?.ShowFeedback("Free-body diagram complete. Now the equations can be written.");
                EnterPhase(Phase.Solve);
            }
        }

        void UpdateFbdProgress()
        {
            int filled = sockets.Count(x => x.IsFilled);
            whiteboard?.SetProgress($"{filled} of {sockets.Count} forces placed");
        }

        void BuildTokens(int minimum = 0)
        {
            foreach (var t in tokens) if (t) MeshFactory.SafeDestroy(t.gameObject);
            tokens.Clear();
            if (tokenRack == null) return;
            int count = Mathf.Max(tokenCount, minimum);
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * tokenSpacing;
                var t = ForceToken.Create("Force Token " + (i + 1), theme, tokenRack, new Vector3(x, 0.02f, 0));
                tokens.Add(t);
            }
        }

        // ------------------------------------------------------------------ solve

        void ShowStep(bool animate)
        {
            if (Instance == null || Instance.Steps.Count == 0) { EnterPhase(Phase.Done); return; }
            StepIndex = Mathf.Clamp(StepIndex, 0, Instance.Steps.Count - 1);
            var step = Instance.Steps[StepIndex];
            whiteboard?.ShowStep($"Step {StepIndex + 1} of {Instance.Steps.Count} — {step.Title}", step.Body, animate);
            whiteboard?.ConfigureButtons(next: true, back: StepIndex > 0, hint: false, skip: false);
            whiteboard?.SetButtonLabel(whiteboard.nextButton, StepIndex == Instance.Steps.Count - 1 ? "Finish" : "Next step");

            structure.ClearMemberHighlights(); structure.ClearNodeHighlights();
            structure.HighlightMembers(step.HighlightMembers, true);
            structure.HighlightNodes(step.HighlightNodes, true);
            structure.ShowMemberForces(step.Kind == StepKind.FinalAnswer);
        }

        public void NextStep()
        {
            if (CurrentPhase != Phase.Solve) return;
            if (StepIndex >= Instance.Steps.Count - 1)
            {
                structure.ClearMemberHighlights(); structure.ClearNodeHighlights();
                structure.ShowMemberForces(true);
                EnterPhase(Phase.Done);
                return;
            }
            StepIndex++;
            ShowStep(true);
        }

        public void PreviousStep()
        {
            if (CurrentPhase == Phase.Done) { CurrentPhase = Phase.Solve; StepIndex = Instance.Steps.Count - 1; ShowStep(false); return; }
            if (CurrentPhase != Phase.Solve || StepIndex == 0) return;
            StepIndex--;
            ShowStep(false);
        }

        // ------------------------------------------------------------------ help

        public void Hint()
        {
            hintsUsed++;
            string text;
            switch (CurrentPhase)
            {
                case Phase.Isolate:
                    text = problem.kind == StructureKind.Truss && plannedSection != null
                        ? $"Hint: you want the forces in {string.Join(", ", plannedSection.cutMembers)}. One straight section can cross all three of them at once. Hold the plane roughly vertical and slide it until exactly those three light up."
                        : "Hint: grab the beam anywhere along its length and lift. As soon as it leaves the supports, the free-body diagram begins.";
                    break;
                case Phase.ChooseSide:
                    text = "Hint: count the loads on each half. The half with fewer applied loads gives shorter equations. Either half must be in equilibrium on its own.";
                    break;
                case Phase.BuildFbd:
                {
                    var missing = sockets.Where(s => !s.IsFilled).ToList();
                    int cutMissing = missing.Count(s => !s.isReaction), reactMissing = missing.Count(s => s.isReaction);
                    text = "Hint: " + (cutMissing > 0 ? $"{cutMissing} cut member end{(cutMissing == 1 ? "" : "s")} still need{(cutMissing == 1 ? "s" : "")} a force. " : "")
                         + (reactMissing > 0 ? $"{reactMissing} support reaction{(reactMissing == 1 ? "" : "s")} still missing — a pin gives two components, a roller one." : "")
                         + " Look for the faint markers.";
                    break;
                }
                default: text = "Use Next step to move through the solution."; break;
            }
            whiteboard?.ShowFeedback(text, 12f);
            whiteboard?.ConfigureButtons(next: false, back: false, hint: true, skip: true);
        }

        /// <summary>Performs the current phase for the student and marks the attempt as assisted.</summary>
        public void ShowMeHow()
        {
            Assisted = true;
            StartCoroutine(AutoPhase());
        }

        IEnumerator AutoPhase()
        {
            switch (CurrentPhase)
            {
                case Phase.Isolate:
                    if (problem.kind == StructureKind.Truss && plannedSection != null)
                    {
                        // pose the plane through the planned members
                        var ids = plannedSection.cutMembers.Select(id => problem.FindMember(id).Id).ToList();
                        var pts = ids.Select(id => { var m = structure.GetMember(id); return (m.WorldA + m.WorldB) * 0.5f; }).ToList();
                        Vector3 centre = pts.Aggregate(Vector3.zero, (a, b) => a + b) / pts.Count;
                        Vector3 normal = Vector3.Cross(Vector3.up, structure.Root.forward).normalized; // vertical plane across the truss
                        // choose the plane orientation that separates the planned side from the rest
                        float best = float.MaxValue; Vector3 bestNormal = normal; Vector3 bestCentre = centre;
                        for (int i = -30; i <= 30; i += 10)
                        {
                            var n = Quaternion.AngleAxis(i, structure.Root.forward) * normal;
                            var plane = new Plane(n, centre);
                            var crossing = structure.MembersCrossing(plane).Select(c => c.view.memberId).ToHashSet();
                            float score = crossing.SetEquals(ids) ? Mathf.Abs(i) : 1000 + Mathf.Abs(crossing.Count - ids.Count) * 100;
                            if (score < best) { best = score; bestNormal = n; }
                        }
                        sectionPlane.SetActive(true);
                        sectionPlane.transform.SetPositionAndRotation(bestCentre, Quaternion.LookRotation(bestNormal, Vector3.up));
                        yield return new WaitForSeconds(0.6f);
                        sectionPlane.CommitCut();
                    }
                    else
                    {
                        structure.Root.position = bodyHome + Vector3.up * 0.2f;
                        IsolateBody();
                    }
                    break;
                case Phase.ChooseSide:
                    yield return new WaitForSeconds(0.3f);
                    ChooseSide(sideA);
                    break;
                case Phase.BuildFbd:
                    foreach (var s in sockets.Where(s => !s.IsFilled).ToList())
                    {
                        var token = tokens.FirstOrDefault(t => !t.IsPlaced);
                        if (token == null) break;
                        s.Fill(token);
                        yield return new WaitForSeconds(0.35f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ automation for tests

        /// <summary>Runs the whole decomposition without a player. Returns false if a phase did not advance.</summary>
        public IEnumerator AutoplayAll()
        {
            int guard = 0;
            while (CurrentPhase != Phase.Solve && guard++ < 6)
            {
                Assisted = true;
                yield return AutoPhase();
                yield return new WaitForSeconds(0.2f);
            }
            while (CurrentPhase == Phase.Solve) { whiteboard?.FinishReveal(); NextStep(); yield return null; }
        }
    }
}

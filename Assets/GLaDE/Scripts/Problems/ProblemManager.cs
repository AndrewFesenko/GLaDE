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
    public enum Phase
    {
        /// <summary>Homework mode: read, work it out on the notepad, enter answers.</summary>
        Attempt,
        Isolate, ChooseSide, BuildFbd, Solve, Done
    }

    /// <summary>
    /// Runs one problem scene. A problem opens as homework: the student solves it and types the answers.
    /// "Guide me" (or a hint) switches to the guided decomposition: isolate a body, build its free-body
    /// diagram with force tokens, then step through the solution. Everything problem-specific comes from
    /// the <see cref="StaticsProblem"/> asset.
    /// </summary>
    public class ProblemManager : MonoBehaviour
    {
        [Header("Content")]
        public StaticsProblem problem;
        public VisualTheme theme;
        [Tooltip("0 = pick a fresh random seed each time.")]
        public int seed = 0;
        [Tooltip("Answers within this fraction of the true value count as correct.")]
        public float relativeTolerance = 0.03f;
        public float absoluteTolerance = 0.3f;

        [Header("Scene pieces")]
        public StructureView structure;
        public Whiteboard whiteboard;
        public SectionPlane sectionPlane;
        public Transform tokenRack;
        public GameObject toolsRoot;        // token table, rack sign: shown only in guided mode
        public int tokenCount = 8;
        public float tokenSpacing = 0.11f;

        public ProblemInstance Instance { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int StepIndex { get; private set; }
        public bool Assisted { get; private set; }
        public int Attempts { get; private set; }
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
        bool guided;
        StepSpec plannedSection;
        readonly System.Random seeder = new System.Random();

        // ------------------------------------------------------------------ lifecycle

        void Start()
        {
            if (ProblemHub.SelectedProblem != null) problem = ProblemHub.SelectedProblem;
            if (whiteboard != null)
            {
                whiteboard.CheckPressed += CheckAnswers;
                whiteboard.GuidePressed += StartGuide;
                whiteboard.NextPressed += NextStep;
                whiteboard.BackPressed += PreviousStep;
                whiteboard.HintPressed += Hint;
                whiteboard.SkipPressed += ShowMeHow;
                whiteboard.NewProblemPressed += NewProblem;
                whiteboard.ResetPressed += ResetAttempt;
                whiteboard.ReviewPressed += ReviewSolution;
                if (whiteboard.answerPanel != null) whiteboard.answerPanel.Changed += OnAnswersChanged;
            }
            if (sectionPlane != null) sectionPlane.Released += OnCutReleased;
            NewProblem();
        }

        public void NewProblem()
        {
            int s = seed != 0 ? seed : seeder.Next(1, int.MaxValue);
            LoadInstance(ProblemGenerator.Generate(problem, s));
        }

        /// <summary>Same numbers, start over.</summary>
        public void ResetAttempt()
        {
            if (Instance == null) { NewProblem(); return; }
            LoadInstance(Instance);
        }

        public void LoadInstance(ProblemInstance inst)
        {
            if (inst == null) { Debug.LogError("[GLaDE] No problem instance"); return; }
            Instance = inst;
            hintsUsed = 0; Assisted = false; guided = false; StepIndex = 0; Attempts = 0;
            KeptNodes.Clear(); cutPoints.Clear(); cutMemberIds.Clear();
            ClearSockets(); ClearSidePickers(); ClearBodyGrab();

            structure.Build(inst, theme, problem.worldScale);
            BuildTokens();
            plannedSection = problem.solutionPlan.FirstOrDefault(s => s.kind == StepKind.Section);

            whiteboard?.SetHeader($"{problem.title}  <size=70%><color=#FFFFFF99>{problem.sourceReference}</color></size>", inst.PromptText);
            whiteboard?.ClearStep();
            whiteboard?.ShowFeedback("", 0);
            whiteboard?.answerPanel?.Build(inst);

            ShowTools(false);
            EnterPhase(Phase.Attempt);
        }

        void ShowTools(bool on)
        {
            if (toolsRoot != null) toolsRoot.SetActive(on);
            if (tokenRack != null) tokenRack.gameObject.SetActive(on);
            if (sectionPlane != null)
            {
                bool useCut = on && problem.kind == StructureKind.Truss && plannedSection != null;
                sectionPlane.SetActive(useCut);
                if (useCut) sectionPlane.ResetPose();
            }
            if (on && problem.kind == StructureKind.RigidBody) EnableBodyGrab(); else ClearBodyGrab();
            IgnoreToolCollisions();
        }

        /// <summary>The cutting plane must never shove the tokens off the table.</summary>
        void IgnoreToolCollisions()
        {
            if (sectionPlane == null) return;
            var planeCols = sectionPlane.GetComponentsInChildren<Collider>(true);
            foreach (var t in tokens)
            {
                if (t == null) continue;
                foreach (var tc in t.GetComponentsInChildren<Collider>(true))
                    foreach (var pc in planeCols) Physics.IgnoreCollision(pc, tc, true);
            }
        }

        // ------------------------------------------------------------------ phases

        string StepNumber(int guidedIndex) => (guidedIndex).ToString();

        void EnterPhase(Phase p)
        {
            CurrentPhase = p;
            bool truss = problem.kind == StructureKind.Truss;
            switch (p)
            {
                case Phase.Attempt:
                    whiteboard?.ShowAnswerSheet(true);
                    whiteboard?.SetPhase("<b>Your homework.</b>  Solve it the way you would on paper: sketch the free-body diagram on the notepad, write the equilibrium equations, and enter your answers here. Stuck? <b>Guide me</b> walks you through taking the structure apart.");
                    whiteboard?.SetProgress("");
                    whiteboard?.ConfigureButtons(check: true, guide: true, newProblem: true);
                    whiteboard?.SetButtonEnabled(whiteboard.checkButton, whiteboard.answerPanel != null && whiteboard.answerPanel.IsComplete());
                    break;
                case Phase.Isolate:
                    whiteboard?.ShowAnswerSheet(false);
                    whiteboard?.ClearStep();
                    whiteboard?.SetPhase(truss
                        ? "<b>1 · Isolate a body.</b>  You cannot see the force inside a member, so make it visible: grab the <b>section plane</b> and slice through the members you are asked about. Each cut member then acts on the piece you keep as an external force. One rigid piece gives three equilibrium equations, so a section may cut at most <b>three</b> unknown members."
                        : "<b>1 · Isolate the body.</b>  Grab the beam and lift it clear of its supports. Whatever touched it must be replaced by a force.");
                    whiteboard?.SetProgress("");
                    whiteboard?.ConfigureButtons(hint: true, skip: hintsUsed > 0, reset: true);
                    break;
                case Phase.ChooseSide:
                    whiteboard?.SetPhase("<b>2 · Choose the part to keep.</b>  Point at a half and squeeze the grip to keep it. Either half is in equilibrium on its own; the one with fewer loads means shorter equations.");
                    whiteboard?.ConfigureButtons(hint: true, skip: hintsUsed > 0, reset: true);
                    break;
                case Phase.BuildFbd:
                    whiteboard?.SetPhase("<b>" + (truss ? "3" : "2") + " · Build the free-body diagram.</b>  Everything the isolated body was touching becomes a force. Grab a <b>force token</b> from the table and drop it on each faint marker: every cut member end and every support reaction. A token names itself from where you put it.");
                    UpdateFbdProgress();
                    whiteboard?.ConfigureButtons(hint: true, skip: hintsUsed > 0, reset: true);
                    break;
                case Phase.Solve:
                    whiteboard?.SetPhase("<b>" + (truss ? "4" : "3") + " · Solve.</b>  Step through the equilibrium equations. Each step explains <i>why</i> that equation is the right one to write next.");
                    whiteboard?.SetProgress("");
                    StepIndex = 0;
                    ShowStep(true);
                    break;
                case Phase.Done:
                    whiteboard?.ShowAnswerSheet(true);
                    whiteboard?.answerPanel?.Reveal(Instance);
                    whiteboard?.answerPanel?.SetInteractable(false);
                    whiteboard?.SetPhase(Assisted
                        ? "<b>Done, with guidance.</b>  You have seen the method once. Generate a new problem and try to reach the answers on your own before asking for help."
                        : $"<b>Correct — every value.</b>  Solved unaided in {Attempts} attempt{(Attempts == 1 ? "" : "s")}. New problem, new numbers, same method.");
                    whiteboard?.SetProgress("");
                    whiteboard?.ConfigureButtons(newProblem: true, review: true, guide: !guided);
                    structure.ClearMemberHighlights(); structure.ClearNodeHighlights();
                    structure.ShowMemberForces(true);
                    break;
            }
        }

        // ------------------------------------------------------------------ homework: answers

        void OnAnswersChanged()
        {
            if (CurrentPhase != Phase.Attempt) return;
            whiteboard?.SetButtonEnabled(whiteboard.checkButton, whiteboard.answerPanel.IsComplete());
        }

        public void CheckAnswers()
        {
            if (CurrentPhase != Phase.Attempt || whiteboard?.answerPanel == null) return;
            var panel = whiteboard.answerPanel;
            if (!panel.IsComplete()) { whiteboard.ShowFeedback("Fill in every row first" + (problem.kind == StructureKind.Truss ? ", including tension or compression." : "."), 6f); return; }
            Attempts++;
            int correct = panel.Grade(Instance, relativeTolerance, absoluteTolerance);
            int total = panel.Rows.Count;
            if (correct == total)
            {
                whiteboard.ShowFeedback("All correct. The members are now coloured: blue in tension, red in compression.", 12f);
                EnterPhase(Phase.Done);
                return;
            }
            string msg = $"{correct} of {total} correct. ";
            if (Attempts == 1) msg += "Check the marked rows: a sign or a tension/compression slip is the usual culprit. Redo those rows and press Check again.";
            else if (Attempts == 2) msg += "Still off? Re-draw the free-body diagram on the notepad: are all loads, reactions and cut-member forces on it? Or press Guide me to take the structure apart together.";
            else msg += "Press Guide me — walking through the cut and the free-body diagram once usually fixes it for good.";
            whiteboard.ShowFeedback(msg, 20f);
        }

        /// <summary>Switches from homework to the guided decomposition.</summary>
        public void StartGuide()
        {
            if (CurrentPhase != Phase.Attempt && CurrentPhase != Phase.Done) return;
            guided = true; Assisted = true;
            if (CurrentPhase == Phase.Done)
            {
                // Review after solving unaided: rebuild the structure so it can be taken apart again.
                var inst = Instance;
                structure.Build(inst, theme, problem.worldScale);
                BuildTokens();
                KeptNodes.Clear(); cutPoints.Clear(); cutMemberIds.Clear();
                ClearSockets(); ClearSidePickers();
            }
            ShowTools(true);
            whiteboard?.ShowFeedback(problem.kind == StructureKind.Truss
                ? "The tools are out. The blue <b>section plane</b> is a knife: whatever it passes through gets cut. The arrows on the table are <b>force tokens</b>: blank until placed. Nothing is graded here — this is the method, step by step."
                : "The tools are out. Grab the beam to start. The arrows on the table are <b>force tokens</b>: blank until placed on the free-body diagram.", 25f);
            EnterPhase(Phase.Isolate);
        }

        /// <summary>After finishing, re-open the solution steps for reading.</summary>
        public void ReviewSolution()
        {
            if (Instance == null || Instance.Steps.Count == 0) return;
            CurrentPhase = Phase.Solve;
            whiteboard?.SetPhase("<b>Worked solution.</b>  Read through the steps; use Back and Next.");
            StepIndex = 0;
            ShowStep(false);
        }

        // ------------------------------------------------------------------ isolate: truss cut

        void OnCutReleased(List<MemberView> crossed, Dictionary<string, Vector3> points)
        {
            if (CurrentPhase != Phase.Isolate || problem.kind != StructureKind.Truss) return;
            var ids = crossed.Select(m => m.memberId).ToList();
            if (ids.Count == 0) { whiteboard?.ShowFeedback("The plane is not cutting anything yet. Slide it into the truss until members light up."); return; }

            var planned = plannedSection.cutMembers.Select(id => problem.FindMember(id).Id).ToHashSet();
            var actual = ids.ToHashSet();
            if (actual.SetEquals(planned))
            {
                cutMemberIds = ids; cutPoints = points;
                CommitCut();
                return;
            }
            if (ids.Count > 3)
                whiteboard?.ShowFeedback($"That section cuts <b>{ids.Count}</b> members ({string.Join(", ", ids)}). One piece gives only three equations, so three unknown forces is the most we can solve. Tilt or move the plane.");
            else if (planned.Overlaps(actual))
                whiteboard?.ShowFeedback($"Close: you cut {string.Join(", ", ids)}. The question asks for {string.Join(", ", planned)}. One section through all three lets you solve them together.");
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

        /// <summary>
        /// A grab-anywhere interactable for one half. XRI snapshots the collider list when the component
        /// registers, so the object is created inactive, configured, then enabled.
        /// </summary>
        XRSimpleInteractable MakePicker(Transform group, Action onPick)
        {
            var go = new GameObject("Pick " + group.name);
            go.SetActive(false);
            go.transform.SetParent(group, false);
            var si = go.AddComponent<XRSimpleInteractable>();
            si.colliders.Clear();
            si.colliders.AddRange(structure.CollidersUnder(group));
            si.selectEntered.AddListener(_ => onPick());
            si.hoverEntered.AddListener(_ => structure.HighlightGroup(group, true));
            si.hoverExited.AddListener(_ => structure.HighlightGroup(group, false));
            go.SetActive(true);
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
            structure.HighlightGroup(structure.KeptGroup, false);
            structure.HighlightGroup(structure.DiscardedGroup, false);
            ClearSidePickers();
            if (!ReferenceEquals(kept, sideA))
            {
                // Student kept the other half: rebuild the split the other way round and regenerate the section text for that side.
                structure.Unsplit();
                structure.Split(kept, cutPoints);
                structure.SeparateHalves(kept, 0.12f);
                string keepNode = kept.First();
                string original = plannedSection.keepNode;
                plannedSection.keepNode = keepNode;
                SolutionGenerator.Generate(Instance);
                plannedSection.keepNode = original;
            }
            KeptNodes = kept;
            structure.SetGroupGhost(structure.DiscardedGroup, true);
            BuildSockets();
            EnterPhase(Phase.BuildFbd);
        }

        // ------------------------------------------------------------------ isolate: rigid body lift

        void EnableBodyGrab()
        {
            if (structure.Root == null) return;
            var root = structure.Root.gameObject;
            ClearBodyGrab();
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            // Same registration rule as the side pickers: configure while inactive, then enable.
            root.SetActive(false);
            bodyGrab = root.AddComponent<XRGrabInteractable>();
            bodyGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            bodyGrab.useDynamicAttach = true;
            bodyGrab.throwOnDetach = false;
            bodyGrab.retainTransformParent = true;
            bodyGrab.colliders.Clear();
            bodyGrab.colliders.AddRange(structure.Members.Select(m => (Collider)m.capsule));
            bodyGrab.selectExited.AddListener(OnBodyReleased);
            root.SetActive(true);
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
            foreach (var id in cutMemberIds)
            {
                var view = structure.GetMember(id);
                if (view == null || !cutPoints.TryGetValue(id, out var pt)) continue;
                Vector3 dir = (view.WorldB - view.WorldA).normalized;
                if (!KeptNodes.Contains(view.nodeA)) dir = -dir; // tension: away from the kept joint
                var cutWorld = structure.KeptGroup != null ? structure.KeptGroup.TransformPoint(structure.Root.InverseTransformPoint(pt)) : pt;
                var s = ForceSocket.Create(id, SolutionGenerator.Sym(id, problem), false, cutWorld, dir, parent, theme, theme.labelColor);
                s.Changed += OnSocketChanged;
                sockets.Add(s);
            }
            foreach (var r in Instance.Reactions)
            {
                if (!KeptNodes.Contains(r.Node) || r.IsMoment) continue;
                Vector3 pos = structure.NodeWorld(r.Node);
                if (structure.KeptGroup != null) pos = structure.KeptGroup.TransformPoint(structure.Root.InverseTransformPoint(pos));
                Vector3 dir = rot * new Vector3((float)r.Direction.x, (float)r.Direction.y, 0);
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
            bool wasActive = tokenRack.gameObject.activeSelf;
            tokenRack.gameObject.SetActive(true);
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * tokenSpacing;
                tokens.Add(ForceToken.Create("Force Token " + (i + 1), theme, tokenRack, new Vector3(x, 0.02f, 0)));
            }
            tokenRack.gameObject.SetActive(wasActive);
            IgnoreToolCollisions();
        }

        // ------------------------------------------------------------------ solve

        void ShowStep(bool animate)
        {
            if (Instance == null || Instance.Steps.Count == 0) { EnterPhase(Phase.Done); return; }
            StepIndex = Mathf.Clamp(StepIndex, 0, Instance.Steps.Count - 1);
            var step = Instance.Steps[StepIndex];
            whiteboard?.ShowStep($"Step {StepIndex + 1} of {Instance.Steps.Count} — {step.Title}", step.Body, animate);
            whiteboard?.ConfigureButtons(next: true, back: StepIndex > 0, reset: true);
            whiteboard?.SetButtonLabel(whiteboard.nextButton, StepIndex == Instance.Steps.Count - 1 ? "Finish" : "Next step >");

            structure.ClearMemberHighlights(); structure.ClearNodeHighlights();
            structure.HighlightMembers(step.HighlightMembers, true);
            structure.HighlightNodes(step.HighlightNodes, true);
            structure.ShowMemberForces(step.Kind == StepKind.FinalAnswer);
        }

        public void NextStep()
        {
            if (CurrentPhase != Phase.Solve) return;
            if (StepIndex >= Instance.Steps.Count - 1) { EnterPhase(Phase.Done); return; }
            StepIndex++;
            ShowStep(true);
        }

        public void PreviousStep()
        {
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
                        ? $"Hint: you want the forces in {string.Join(", ", plannedSection.cutMembers)}. One straight section can cross all three at once. Hold the plane roughly upright and slide it sideways until exactly those three light up, then let go."
                        : "Hint: grab the beam anywhere along its length and lift. As soon as it leaves the supports, the free-body diagram begins.";
                    break;
                case Phase.ChooseSide:
                    text = "Hint: count the loads on each half. The half with fewer applied loads gives shorter equations. Point at it and squeeze the grip.";
                    break;
                case Phase.BuildFbd:
                {
                    var missing = sockets.Where(s => !s.IsFilled).ToList();
                    int cutMissing = missing.Count(s => !s.isReaction), reactMissing = missing.Count(s => s.isReaction);
                    text = "Hint: " + (cutMissing > 0 ? $"{cutMissing} cut member end{(cutMissing == 1 ? "" : "s")} still need{(cutMissing == 1 ? "s" : "")} a force. " : "")
                         + (reactMissing > 0 ? $"{reactMissing} support reaction{(reactMissing == 1 ? "" : "s")} still missing — a pin gives two components, a roller one." : "")
                         + " Look for the faint blue markers on the kept piece.";
                    break;
                }
                default: text = "Use Next step to move through the solution."; break;
            }
            whiteboard?.ShowFeedback(text, 14f);
            whiteboard?.ConfigureButtons(hint: true, skip: true, reset: true);
        }

        /// <summary>Performs the current guided phase for the student.</summary>
        public void ShowMeHow()
        {
            Assisted = true;
            StartCoroutine(AutoPhase());
        }

        IEnumerator AutoPhase()
        {
            switch (CurrentPhase)
            {
                case Phase.Attempt:
                    StartGuide();
                    break;
                case Phase.Isolate:
                    if (problem.kind == StructureKind.Truss && plannedSection != null)
                    {
                        var ids = plannedSection.cutMembers.Select(id => problem.FindMember(id).Id).ToList();
                        var pts = ids.Select(id => { var m = structure.GetMember(id); return (m.WorldA + m.WorldB) * 0.5f; }).ToList();
                        Vector3 centre = pts.Aggregate(Vector3.zero, (a, b) => a + b) / pts.Count;
                        Vector3 normal = Vector3.Cross(Vector3.up, structure.Root.forward).normalized;
                        float best = float.MaxValue; Vector3 bestNormal = normal;
                        for (int i = -30; i <= 30; i += 10)
                        {
                            var n = Quaternion.AngleAxis(i, structure.Root.forward) * normal;
                            var crossing = structure.MembersCrossing(new Plane(n, centre)).Select(c => c.view.memberId).ToHashSet();
                            float score = crossing.SetEquals(ids) ? Mathf.Abs(i) : 1000 + Mathf.Abs(crossing.Count - ids.Count) * 100;
                            if (score < best) { best = score; bestNormal = n; }
                        }
                        sectionPlane.SetActive(true);
                        sectionPlane.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(bestNormal, Vector3.up));
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

        /// <summary>Runs the whole guided decomposition without a player.</summary>
        public IEnumerator AutoplayAll()
        {
            if (CurrentPhase == Phase.Attempt) StartGuide();
            int guard = 0;
            while (CurrentPhase != Phase.Solve && CurrentPhase != Phase.Done && guard++ < 6)
            {
                Assisted = true;
                yield return AutoPhase();
                yield return new WaitForSeconds(0.2f);
            }
            while (CurrentPhase == Phase.Solve) { whiteboard?.FinishReveal(); NextStep(); yield return null; }
        }

        /// <summary>Types the true answers into the sheet and checks them (tests the homework path).</summary>
        public void AutoAnswer(bool correct)
        {
            if (whiteboard?.answerPanel == null || CurrentPhase != Phase.Attempt) return;
            var panel = whiteboard.answerPanel;
            foreach (var row in panel.Rows)
            {
                Instance.TryGetAnswer(row.target, out double truth);
                double v = row.isMember ? Math.Abs(truth) : truth;
                if (!correct) v += 5;
                row.entry = ProblemInstance.Fmt(v, 1);
                if (row.isMember) row.tension = truth >= 0;
            }
            CheckAnswers();
        }
    }
}

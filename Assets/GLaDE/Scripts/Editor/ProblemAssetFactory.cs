using System.Collections.Generic;
using GLaDE.Core;
using GLaDE.Problems;
using UnityEditor;
using UnityEngine;

namespace GLaDE.EditorTools
{
    /// <summary>
    /// Authors the shipped problem assets from code so they are reproducible. After creation they are
    /// ordinary ScriptableObjects: tweak ranges, text and plans in the inspector, or duplicate one to
    /// make a new problem without writing code.
    /// </summary>
    public static class ProblemAssetFactory
    {
        public const string DataFolder = "Assets/GLaDE/Data";
        public const string ProblemFolder = "Assets/GLaDE/Data/Problems";
        public const string TrussAssetPath = ProblemFolder + "/Truss F6-9 Pratt.asset";
        public const string BeamAssetPath = ProblemFolder + "/Beam Reactions.asset";
        public const string LibraryPath = DataFolder + "/Problem Library.asset";

        [MenuItem("GLaDE/Create Problem Assets", priority = 20)]
        public static void CreateAll()
        {
            EnsureFolders();
            var truss = CreateOrLoad<StaticsProblem>(TrussAssetPath);
            AuthorPrattTruss(truss);
            var beam = CreateOrLoad<StaticsProblem>(BeamAssetPath);
            AuthorBeam(beam);
            var lib = CreateOrLoad<ProblemLibrary>(LibraryPath);
            lib.problems = new List<StaticsProblem> { truss, beam };
            EditorUtility.SetDirty(truss); EditorUtility.SetDirty(beam); EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[GLaDE] Problem assets created/updated.");
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/GLaDE")) AssetDatabase.CreateFolder("Assets", "GLaDE");
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/GLaDE", "Data");
            if (!AssetDatabase.IsValidFolder(ProblemFolder)) AssetDatabase.CreateFolder(DataFolder, "Problems");
        }

        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static NodeDef N(string id, string x, string y) => new NodeDef { id = id, x = x, y = y };
        static MemberDef M(string a, string b) => new MemberDef { a = a, b = b };
        static ParameterDef P(string name, string label, string unit, float min, float max, float step, float def, int decimals = 1)
            => new ParameterDef { name = name, label = label, unit = unit, min = min, max = max, step = step, defaultValue = def, decimals = decimals };

        /// <summary>Hibbeler F6-9: Pratt truss, six panels, loads at B, C, D. Find KJ, KD, CD by the method of sections.</summary>
        public static void AuthorPrattTruss(StaticsProblem p)
        {
            p.problemId = "F6-9";
            p.title = "Pratt truss — forces in KJ, KD and CD";
            p.category = ProblemCategory.Truss;
            p.sourceReference = "Hibbeler, Statics, F6-9";
            p.prompt = "Determine the force in members <b>KJ</b>, <b>KD</b> and <b>CD</b> of the Pratt truss, and state whether each is in tension or compression.\n"
                     + "Panels are {L} wide and the truss is {h} tall. Loads: P<sub>B</sub> = {P_B}, P<sub>C</sub> = {P_C}, P<sub>D</sub> = {P_D}.";
            p.sceneName = "Problem_Truss";
            p.kind = StructureKind.Truss;
            p.worldScale = 0.15f;
            p.parameters = new List<ParameterDef>
            {
                P("L", "Panel length", "m", 1.5f, 3f, 0.5f, 2f),
                P("h", "Truss height", "m", 2f, 4f, 0.5f, 3f),
                P("P_B", "Load at B", "kN", 5f, 30f, 5f, 20f, 0),
                P("P_C", "Load at C", "kN", 5f, 30f, 5f, 20f, 0),
                P("P_D", "Load at D", "kN", 5f, 30f, 5f, 30f, 0),
            };
            p.nodes = new List<NodeDef>
            {
                N("A", "0", "0"), N("B", "L", "0"), N("C", "2*L", "0"), N("D", "3*L", "0"), N("E", "4*L", "0"), N("F", "5*L", "0"), N("G", "6*L", "0"),
                N("L", "L", "h"), N("K", "2*L", "h"), N("J", "3*L", "h"), N("I", "4*L", "h"), N("H", "5*L", "h"),
            };
            p.members = new List<MemberDef>
            {
                M("A", "B"), M("B", "C"), M("C", "D"), M("D", "E"), M("E", "F"), M("F", "G"),   // bottom chord
                M("L", "K"), M("K", "J"), M("J", "I"), M("I", "H"),                              // top chord
                M("L", "B"), M("K", "C"), M("J", "D"), M("I", "E"), M("H", "F"),                 // verticals
                M("A", "L"), M("L", "C"), M("K", "D"), M("I", "D"), M("H", "E"), M("G", "H"),   // diagonals
            };
            p.supports = new List<SupportDef>
            {
                new SupportDef { node = "A", type = SupportType.Pin },
                new SupportDef { node = "G", type = SupportType.Roller, reactionAngleDeg = 90 },
            };
            p.loads = new List<LoadDef>
            {
                new LoadDef { id = "P_B", node = "B", magnitude = "P_B", angleDeg = -90 },
                new LoadDef { id = "P_C", node = "C", magnitude = "P_C", angleDeg = -90 },
                new LoadDef { id = "P_D", node = "D", magnitude = "P_D", angleDeg = -90 },
            };
            p.distributedLoads = new List<DistributedLoadDef>();
            p.targets = new List<string> { "KJ", "KD", "CD" };
            p.solutionPlan = new List<StepSpec>
            {
                new StepSpec { kind = StepKind.FreeBodyWhole },
                new StepSpec { kind = StepKind.Reactions, momentAbout = "A",
                    teachingNote = "Why start with the whole truss? A section on the left will carry A's reactions, so we need them first. Summing moments about A is the trick that finds G's reaction in one line." },
                new StepSpec { kind = StepKind.Section, cutMembers = new List<string> { "KJ", "KD", "CD" }, keepNode = "A",
                    equations = new List<string> { "M:D=KJ", "Fy=KD", "Fx=CD" },
                    teachingNote = "Method of sections in one sentence: cut through the members you want, keep one side, and pick each equation so only one unknown survives. Moments about a point where two unknowns meet is the classic move." },
                new StepSpec { kind = StepKind.FinalAnswer },
            };
            p.validity = new ValidityRules { maxForceRatio = 6f, minAnswerMagnitude = 1f, minNodeSpacing = 0.5f, maxAttempts = 64 };
        }

        /// <summary>Simply supported beam with a uniform load over its left half and a point load. Find the reactions.</summary>
        public static void AuthorBeam(StaticsProblem p)
        {
            p.problemId = "F5-Beam";
            p.title = "Simply supported beam — support reactions";
            p.category = ProblemCategory.Beam;
            p.sourceReference = "Hibbeler, Statics, ch. 5 (fundamental problem style)";
            p.prompt = "The beam is pinned at <b>A</b> and rests on a roller at <b>B</b>. A uniform load of {w} acts from A to the midpoint D, and a concentrated load P = {P} acts at C, {a} from A.\n"
                     + "Determine the horizontal and vertical components of reaction at the pin A and the reaction at the roller B.";
            p.sceneName = "Problem_Beam";
            p.kind = StructureKind.RigidBody;
            p.worldScale = 0.22f;
            p.parameters = new List<ParameterDef>
            {
                P("L", "Beam length", "m", 6f, 10f, 1f, 8f),
                P("a", "Position of P from A", "m", 2f, 7f, 1f, 6f),
                P("w", "Distributed load", "kN/m", 1f, 5f, 1f, 2f),
                P("P", "Point load", "kN", 5f, 20f, 5f, 10f, 0),
            };
            p.nodes = new List<NodeDef>
            {
                N("A", "0", "0"), N("D", "L/2", "0"), N("C", "min(a, L-1)", "0"), N("B", "L", "0"),
            };
            p.members = new List<MemberDef> { M("A", "D"), M("D", "C"), M("C", "B") };
            p.supports = new List<SupportDef>
            {
                new SupportDef { node = "A", type = SupportType.Pin },
                new SupportDef { node = "B", type = SupportType.Roller, reactionAngleDeg = 90 },
            };
            p.loads = new List<LoadDef> { new LoadDef { id = "P", node = "C", magnitude = "P", angleDeg = -90 } };
            p.distributedLoads = new List<DistributedLoadDef>
            {
                new DistributedLoadDef { id = "w", nodeA = "A", nodeB = "D", intensityA = "w", intensityB = "w" },
            };
            p.targets = new List<string> { "Ax", "Ay", "By" };
            p.solutionPlan = new List<StepSpec>
            {
                new StepSpec { kind = StepKind.FreeBodyWhole },
                new StepSpec { kind = StepKind.Resultants },
                new StepSpec { kind = StepKind.Reactions, momentAbout = "A",
                    teachingNote = "Moments about the pin is the standard opening: both of A's unknown components vanish from the equation, leaving only the roller reaction." },
                new StepSpec { kind = StepKind.FinalAnswer },
            };
            p.validity = new ValidityRules { maxForceRatio = 6f, minAnswerMagnitude = 1f, minNodeSpacing = 0.9f, maxAttempts = 64 };
        }
    }
}

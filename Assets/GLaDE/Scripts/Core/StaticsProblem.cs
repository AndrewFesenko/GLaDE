using System.Collections.Generic;
using UnityEngine;

namespace GLaDE.Core
{
    /// <summary>
    /// A statics problem authored as data. Geometry, loads and supports are expressions over the
    /// randomisable parameters; the solution plan lists the teaching steps the whiteboard reveals.
    /// New problems are new assets, not new code.
    /// </summary>
    [CreateAssetMenu(menuName = "GLaDE/Statics Problem", fileName = "NewStaticsProblem")]
    public class StaticsProblem : ScriptableObject
    {
        [Header("Identity")]
        public string problemId = "F6-9";
        public string title = "Pratt truss";
        public ProblemCategory category = ProblemCategory.Truss;
        [Tooltip("Where the problem comes from, e.g. Hibbeler Statics 14e, F6-9.")]
        public string sourceReference = "";
        [TextArea(3, 8), Tooltip("Shown to the student. Use {ParameterName} to insert a given value.")]
        public string prompt = "Determine the force in members KJ, KD and CD. State whether each is in tension or compression.";
        public Sprite thumbnail;
        [Tooltip("Scene that hosts this problem. The hub loads it by name.")]
        public string sceneName = "Problem_Truss";

        [Header("Structure")]
        public StructureKind kind = StructureKind.Truss;
        [Tooltip("Metres of problem space per Unity metre. A 12 m truss at 0.15 spans 1.8 m in the room.")]
        public float worldScale = 0.15f;
        public List<ParameterDef> parameters = new List<ParameterDef>();
        public List<NodeDef> nodes = new List<NodeDef>();
        public List<MemberDef> members = new List<MemberDef>();
        public List<SupportDef> supports = new List<SupportDef>();
        public List<LoadDef> loads = new List<LoadDef>();
        public List<DistributedLoadDef> distributedLoads = new List<DistributedLoadDef>();

        [Header("Solution")]
        [Tooltip("Member ids (KJ) or reaction names (Ay) the student must find.")]
        public List<string> targets = new List<string>();
        public List<StepSpec> solutionPlan = new List<StepSpec>();
        public ValidityRules validity = new ValidityRules();

        public NodeDef FindNode(string id)
        {
            foreach (var n in nodes) if (n.id == id) return n;
            return null;
        }

        /// <summary>Finds a member by id in either orientation ("KJ" or "JK").</summary>
        public MemberDef FindMember(string id)
        {
            foreach (var m in members)
                if (m.Id == id || m.b + m.a == id) return m;
            return null;
        }

        public SupportDef FindSupport(string node)
        {
            foreach (var s in supports) if (s.node == node) return s;
            return null;
        }

        public bool IsMemberId(string id) => FindMember(id) != null;
    }
}

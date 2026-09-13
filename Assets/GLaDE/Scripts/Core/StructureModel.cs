using System;
using System.Collections.Generic;
using UnityEngine;

namespace GLaDE.Core
{
    public enum StructureKind
    {
        /// <summary>Pin-jointed truss: unknowns are member forces plus support reactions.</summary>
        Truss,
        /// <summary>A single rigid body (beam, frame member): unknowns are support reactions only.</summary>
        RigidBody
    }

    public enum SupportType { Pin, Roller, Fixed }

    public enum ProblemCategory { Truss, Beam, Frame, Pulley, Friction }

    /// <summary>A joint / point of interest. Coordinates are expressions in metres that may reference parameters.</summary>
    [Serializable]
    public class NodeDef
    {
        public string id = "A";
        public string x = "0";
        public string y = "0";
    }

    /// <summary>A two-force member between two nodes (truss) or a drawn segment of a rigid body.</summary>
    [Serializable]
    public class MemberDef
    {
        public string a = "A";
        public string b = "B";
        public string Id => a + b;

        public bool Connects(string node) => a == node || b == node;
        public string Other(string node) => a == node ? b : a;
    }

    [Serializable]
    public class SupportDef
    {
        public string node = "A";
        public SupportType type = SupportType.Pin;
        [Tooltip("Roller only: direction the reaction acts, in degrees from +x (90 = vertical).")]
        public float reactionAngleDeg = 90f;
    }

    /// <summary>A concentrated load applied at a node. Magnitude is an expression; angle is the direction the force acts.</summary>
    [Serializable]
    public class LoadDef
    {
        public string id = "P";
        public string node = "B";
        public string magnitude = "10";
        [Tooltip("Direction the force acts, degrees from +x. -90 = straight down.")]
        public float angleDeg = -90f;
    }

    /// <summary>A linearly varying distributed load acting downward (-y) between two nodes, in kN/m.</summary>
    [Serializable]
    public class DistributedLoadDef
    {
        public string id = "w";
        public string nodeA = "A";
        public string nodeB = "B";
        public string intensityA = "2";
        public string intensityB = "2";
    }

    /// <summary>A randomisable given. Values are drawn on the grid min, min+step, ... max.</summary>
    [Serializable]
    public class ParameterDef
    {
        public string name = "L";
        public string label = "Panel length";
        public string unit = "m";
        public float min = 1f;
        public float max = 3f;
        public float step = 0.5f;
        public float defaultValue = 2f;
        [Tooltip("Decimal places shown to the student.")]
        public int decimals = 1;
    }

    public enum StepKind
    {
        /// <summary>Narrative only: describe the free-body diagram of the whole structure.</summary>
        FreeBodyWhole,
        /// <summary>Solve all support reactions using sum of moments about a node, then sum Fy and sum Fx.</summary>
        Reactions,
        /// <summary>Replace distributed loads with equivalent resultants.</summary>
        Resultants,
        /// <summary>Method of sections: cut the listed members, keep the side containing keepNode, solve the listed equations.</summary>
        Section,
        /// <summary>Method of joints at one joint.</summary>
        Joint,
        /// <summary>Summarise the target answers.</summary>
        FinalAnswer
    }

    /// <summary>
    /// One authored step of the solution plan. The generator turns this into teaching text with
    /// the numbers of the current problem instance.
    /// </summary>
    [Serializable]
    public class StepSpec
    {
        public StepKind kind = StepKind.Reactions;
        [Tooltip("Optional title override.")]
        public string title = "";
        [TextArea(2, 6), Tooltip("Optional author note appended to the generated text.")]
        public string teachingNote = "";
        [Tooltip("Reactions/Section: node to take moments about.")]
        public string momentAbout = "";
        [Tooltip("Section: member ids to cut, e.g. KJ, KD, CD.")]
        public List<string> cutMembers = new List<string>();
        [Tooltip("Section: a node on the side of the cut that is kept.")]
        public string keepNode = "";
        [Tooltip("Section: equations in order. Format: M:<node>=<member> | Fx=<member> | Fy=<member>.")]
        public List<string> equations = new List<string>();
        [Tooltip("Joint: the joint to analyse.")]
        public string joint = "";
    }

    [Serializable]
    public class ValidityRules
    {
        [Tooltip("Reject instances where any member force exceeds this multiple of the total applied load.")]
        public float maxForceRatio = 6f;
        [Tooltip("Reject instances where every target answer is smaller than this (kN).")]
        public float minAnswerMagnitude = 0.5f;
        [Tooltip("Reject instances where any two nodes are closer than this (m).")]
        public float minNodeSpacing = 0.5f;
        public int maxAttempts = 64;
    }
}

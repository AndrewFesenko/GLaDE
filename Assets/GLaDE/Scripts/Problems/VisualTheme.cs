using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Materials and sizes shared by every problem scene. Created by the scene builder, editable in the inspector.</summary>
    [CreateAssetMenu(menuName = "GLaDE/Visual Theme", fileName = "GLaDE Visual Theme")]
    public class VisualTheme : ScriptableObject
    {
        [Header("Structure")]
        public Material member;
        public Material joint;
        public Material support;
        public Material ground;
        public Material stand;

        [Header("States")]
        public Material memberHighlight;   // hovered by the section plane / referenced by a step
        public Material memberCut;         // about to be cut
        public Material memberGhost;       // discarded half
        public Material memberTension;
        public Material memberCompression;
        public Material memberZero;

        [Header("Forces")]
        public Material loadArrow;
        public Material reactionArrow;
        public Material tokenIdle;
        public Material tokenPlaced;
        public Material socketHint;

        [Header("Tools")]
        public Material sectionPlane;
        public Material sectionPlaneActive;
        public Material whiteboardSurface;
        public Material whiteboardFrame;

        [Header("Sizes (metres, room scale)")]
        public float memberRadius = 0.018f;
        public float jointRadius = 0.032f;
        public float arrowShaftRadius = 0.012f;
        public float arrowMinLength = 0.16f;
        public float arrowMaxLength = 0.34f;
        public float labelSize = 0.06f;

        [Header("Text colours")]
        public Color labelColor = new Color(0.95f, 0.95f, 0.97f);
        public Color loadLabelColor = new Color(1f, 0.55f, 0.35f);
        public Color reactionLabelColor = new Color(0.45f, 0.85f, 1f);
        public Color dimensionColor = new Color(0.75f, 0.8f, 0.9f);
    }
}

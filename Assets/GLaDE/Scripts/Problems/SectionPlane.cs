using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    /// <summary>
    /// The grabbable cutting plane. While held it highlights every member it crosses; on release it reports
    /// the cut so the problem manager can judge it. Stays where the student leaves it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SectionPlane : MonoBehaviour
    {
        public VisualTheme theme;
        public StructureView structure;
        public MeshRenderer surface;
        public float width = 0.9f, height = 0.7f;

        /// <summary>Fired when the plane is released: members crossed, and crossing points in world space.</summary>
        public event Action<List<MemberView>, Dictionary<string, Vector3>> Released;

        XRGrabInteractable grab;
        readonly List<MemberView> crossing = new List<MemberView>();
        readonly Dictionary<string, Vector3> crossPoints = new Dictionary<string, Vector3>();
        Vector3 homePos; Quaternion homeRot;
        bool active = true;

        public Plane WorldPlane => new Plane(transform.forward, transform.position);
        public IReadOnlyList<MemberView> Crossing => crossing;

        public static SectionPlane Create(VisualTheme theme, StructureView structure, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Section Plane");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var sp = go.AddComponent<SectionPlane>();
            sp.theme = theme; sp.structure = structure;
            sp.Build();
            return sp;
        }

        void Build()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.name = "Surface";
            MeshFactory.SafeDestroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = new Vector3(width, height, 0.004f);
            surface = quad.GetComponent<MeshRenderer>();
            surface.sharedMaterial = theme.sectionPlane;

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Handle";
            MeshFactory.SafeDestroy(frame.GetComponent<Collider>());
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = new Vector3(0, -height * 0.5f - 0.02f, 0);
            frame.transform.localScale = new Vector3(width + 0.04f, 0.035f, 0.035f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = theme.whiteboardFrame;

            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height + 0.06f, 0.05f);
            col.center = new Vector3(0, -0.02f, 0);

            grab = GetComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            grab.retainTransformParent = true;
        }

        void Awake()
        {
            // Listeners are runtime-only (not serialized), so they are attached here rather than in Build().
            grab = GetComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(OnReleased);
            homePos = transform.position; homeRot = transform.rotation;
        }

        void Update()
        {
            if (!active || structure == null || structure.Instance == null) return;
            if (!grab.isSelected) return;
            Evaluate();
        }

        /// <summary>Recomputes which members the plane crosses and highlights them.</summary>
        public void Evaluate()
        {
            foreach (var m in crossing) m.SetHighlighted(false);
            crossing.Clear(); crossPoints.Clear();
            if (structure == null || structure.Instance == null) return;
            foreach (var (view, point, _) in structure.MembersCrossing(WorldPlane))
            {
                crossing.Add(view); crossPoints[view.memberId] = point;
                view.SetHighlighted(true);
            }
            if (surface != null) surface.sharedMaterial = crossing.Count > 0 ? theme.sectionPlaneActive : theme.sectionPlane;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!active) return;
            Evaluate();
            Released?.Invoke(new List<MemberView>(crossing), new Dictionary<string, Vector3>(crossPoints));
        }

        /// <summary>Simulates a release at the current pose (used by tests and by "show me how").</summary>
        public void CommitCut()
        {
            Evaluate();
            Released?.Invoke(new List<MemberView>(crossing), new Dictionary<string, Vector3>(crossPoints));
        }

        public void SetActive(bool on)
        {
            active = on;
            if (!on) { foreach (var m in crossing) m.SetHighlighted(false); crossing.Clear(); crossPoints.Clear(); }
            if (surface != null) surface.sharedMaterial = theme.sectionPlane;
            gameObject.SetActive(on);
        }

        public void ResetPose()
        {
            transform.SetPositionAndRotation(homePos, homeRot);
            if (surface != null) surface.sharedMaterial = theme.sectionPlane;
        }
    }
}

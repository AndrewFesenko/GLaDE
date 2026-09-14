using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    /// <summary>
    /// Grab knob on the tail of a load arrow, used in what-if mode. Drag it sideways to move the load
    /// to another joint, or away from the joint to change its magnitude. Snaps back to the arrow when released.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class LoadHandle : MonoBehaviour
    {
        public string loadId;
        public bool IsGrabbed => grab != null && grab.isSelected;
        public System.Action<LoadHandle> Released;

        XRGrabInteractable grab;
        Vector3 restLocalPos;

        public static LoadHandle Create(string loadId, Transform parent, Vector3 localPos, float radius, Material mat)
        {
            var go = new GameObject("Load Handle " + loadId);
            go.SetActive(false);                       // XRI snapshots colliders on enable: build first, enable after
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Knob";
            MeshFactory.SafeDestroy(vis.GetComponent<Collider>());
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = Vector3.one * radius * 2f;
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<SphereCollider>();
            col.radius = radius * 1.6f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var h = go.AddComponent<LoadHandle>();
            h.loadId = loadId;
            var g = go.GetComponent<XRGrabInteractable>();
            g.movementType = XRBaseInteractable.MovementType.Instantaneous;
            g.useDynamicAttach = true;
            g.throwOnDetach = false;
            g.retainTransformParent = true;
            g.trackRotation = false;
            g.colliders.Clear(); g.colliders.Add(col);
            go.SetActive(true);
            return h;
        }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(_ => { SnapBack(); Released?.Invoke(this); });
            restLocalPos = transform.localPosition;
        }

        public void SetRest(Vector3 localPos)
        {
            restLocalPos = localPos;
            if (!IsGrabbed) transform.localPosition = localPos;
        }

        public void SnapBack() => transform.localPosition = restLocalPos;
    }
}

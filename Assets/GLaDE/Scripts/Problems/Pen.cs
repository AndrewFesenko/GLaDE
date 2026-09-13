using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    /// <summary>A grabbable pen. While held, its tip paints on any <see cref="Notepad"/> it touches.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Pen : MonoBehaviour
    {
        public Transform tip;
        public float reach = 0.025f;

        XRGrabInteractable grab;
        Notepad lastPad;
        Vector2 lastUv;
        bool drawing;

        void Awake() { grab = GetComponent<XRGrabInteractable>(); }

        void Update()
        {
            if (tip == null) return;
            // Ray from a little behind the tip, along the pen axis, so pushing into the pad still registers.
            Vector3 origin = tip.position - tip.up * 0.02f;
            if (Physics.Raycast(origin, tip.up, out var hit, 0.02f + reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var pad = hit.collider.GetComponent<Notepad>();
                if (pad != null)
                {
                    Vector2 uv = hit.textureCoord;
                    if (drawing && pad == lastPad) pad.Stroke(lastUv, uv); else pad.Dot(uv);
                    lastPad = pad; lastUv = uv; drawing = true;
                    return;
                }
            }
            drawing = false; lastPad = null;
        }
    }
}

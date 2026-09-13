using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    /// <summary>
    /// A grabbable force-vector arrow. Unlabelled until the student drops it into a <see cref="ForceSocket"/>,
    /// where it takes the socket's name and direction. Tail is at the local origin, pointing along +Y.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ForceToken : MonoBehaviour
    {
        public VisualTheme theme;
        public TextMeshPro label;
        public GameObject arrow;
        public float length = 0.22f;

        public ForceSocket Socket { get; private set; }
        public bool IsPlaced => Socket != null;

        Rigidbody rb;
        XRGrabInteractable grab;
        Vector3 homePos; Quaternion homeRot; Transform homeParent;

        public static ForceToken Create(string name, VisualTheme theme, Transform parent, Vector3 localPos)
        {
            // Built while inactive: XRGrabInteractable collects its colliders when it wakes up, so the
            // capsule must exist before the object is enabled or the token can never be grabbed.
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var token = go.AddComponent<ForceToken>();
            token.theme = theme;
            token.BuildVisual();
            go.SetActive(true);
            token.SetHome();
            return token;
        }

        void BuildVisual()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 0.25f; rb.useGravity = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            arrow = MeshFactory.Arrow("Arrow", length, theme.arrowShaftRadius * 1.1f, theme.tokenIdle, transform);
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = 0.028f; col.height = length + 0.03f; col.center = new Vector3(0, length * 0.5f, 0);

            var attach = new GameObject("Attach").transform;
            attach.SetParent(transform, false);

            grab = GetComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.Add(col);
            grab.attachTransform = attach;
            grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.retainTransformParent = true;
            grab.selectExited.AddListener(OnReleased);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0, length + 0.05f, 0);
            label = labelGo.AddComponent<TextMeshPro>();
            label.text = "?";
            label.fontSize = theme.labelSize * 10f * 0.9f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(1.5f, 0.4f);
            label.color = theme.labelColor;
            labelGo.AddComponent<Billboard>();
        }

        public void SetHome()
        {
            homePos = transform.position; homeRot = transform.rotation; homeParent = transform.parent;
        }

        public void ReturnHome()
        {
            if (Socket != null) Socket.ForceRelease();
            SetPlaced(null);
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            transform.SetParent(homeParent, true);
            transform.SetPositionAndRotation(homePos, homeRot);
        }

        /// <summary>Called by the socket when the token snaps in (socket != null) or leaves (null).</summary>
        public void SetPlaced(ForceSocket socket)
        {
            Socket = socket;
            if (label != null)
            {
                label.text = socket != null ? socket.displayLabel : "?";
                label.color = socket != null ? socket.labelColor : theme.labelColor;
            }
            if (arrow != null) MeshFactory.SetArrowMaterial(arrow, socket != null ? theme.tokenPlaced : theme.tokenIdle);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            // Tokens that fall off the table come back to the rack instead of being lost.
            CancelInvoke(nameof(CheckLost));
            Invoke(nameof(CheckLost), 2.5f);
        }

        void CheckLost()
        {
            if (Socket != null || grab.isSelected) return;
            if (transform.position.y < 0.05f || Vector3.Distance(transform.position, homePos) > 6f) ReturnHome();
        }
    }
}

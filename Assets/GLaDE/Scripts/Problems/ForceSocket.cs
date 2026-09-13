using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GLaDE.Problems
{
    /// <summary>Interaction filter that admits only <see cref="ForceToken"/> interactables.</summary>
    public class ForceTokenOnlyFilter : UnityEngine.XR.Interaction.Toolkit.Filtering.IXRSelectFilter, UnityEngine.XR.Interaction.Toolkit.Filtering.IXRHoverFilter
    {
        public bool canProcess => true;
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => interactable.transform.GetComponent<ForceToken>() != null;
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => interactable.transform.GetComponent<ForceToken>() != null;
    }

    /// <summary>
    /// A place on the free-body diagram where a force must be drawn: a cut member end or a support reaction.
    /// Accepts one <see cref="ForceToken"/> and orients it along the assumed positive direction.
    /// </summary>
    [RequireComponent(typeof(XRSocketInteractor))]
    public class ForceSocket : MonoBehaviour
    {
        public string forceName;        // KJ, Ay, Gy ...
        public string displayLabel;     // rich text shown on the token
        public Color labelColor = Color.white;
        public bool isReaction;
        public XRSocketInteractor socket;
        public GameObject hint;

        public event Action<ForceSocket> Changed;
        public ForceToken Token { get; private set; }
        public bool IsFilled => Token != null;

        public static ForceSocket Create(string forceName, string displayLabel, bool isReaction, Vector3 worldPos, Vector3 worldDir, Transform parent, VisualTheme theme, Color labelColor)
        {
            var go = new GameObject("Socket " + forceName);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, worldDir.normalized);

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 0.06f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;

            var fs = go.AddComponent<ForceSocket>();
            fs.forceName = forceName; fs.displayLabel = displayLabel; fs.isReaction = isReaction; fs.labelColor = labelColor;
            fs.socket = go.GetComponent<XRSocketInteractor>();
            fs.socket.hoverSocketSnapping = true;
            fs.socket.recycleDelayTime = 0.3f;
            // Only force tokens may snap in. Without this the socket would grab whatever grabbable it overlaps,
            // including the beam it is attached to.
            var filter = new ForceTokenOnlyFilter();
            fs.socket.selectFilters.Add(filter);
            fs.socket.hoverFilters.Add(filter);
            var attach = new GameObject("Attach").transform;
            attach.SetParent(go.transform, false);
            fs.socket.attachTransform = attach;
            fs.socket.selectEntered.AddListener(fs.OnEntered);
            fs.socket.selectExited.AddListener(fs.OnExited);

            // A faint sphere marks the spot without giving away what force belongs there.
            fs.hint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fs.hint.name = "Hint";
            MeshFactory.SafeDestroy(fs.hint.GetComponent<Collider>());
            fs.hint.transform.SetParent(go.transform, false);
            fs.hint.transform.localScale = Vector3.one * 0.05f;
            fs.hint.GetComponent<MeshRenderer>().sharedMaterial = theme.socketHint;
            return fs;
        }

        void OnEntered(SelectEnterEventArgs args)
        {
            var token = args.interactableObject.transform.GetComponent<ForceToken>();
            if (token == null) return;
            Token = token;
            token.SetPlaced(this);
            if (hint != null) hint.SetActive(false);
            Changed?.Invoke(this);
        }

        void OnExited(SelectExitEventArgs args)
        {
            var token = args.interactableObject.transform.GetComponent<ForceToken>();
            if (token != null && token == Token)
            {
                token.SetPlaced(null);
                Token = null;
                if (hint != null) hint.SetActive(true);
                Changed?.Invoke(this);
            }
        }

        public void ForceRelease()
        {
            if (Token == null || socket == null) return;
            var manager = socket.interactionManager;
            if (manager != null && socket.hasSelection) manager.SelectExit((IXRSelectInteractor)socket, socket.firstInteractableSelected);
        }

        /// <summary>Programmatically drops a token into this socket (used by "show me how" and automated tests).</summary>
        public void Fill(ForceToken token)
        {
            if (token == null || socket == null) return;
            var manager = socket.interactionManager;
            var interactable = token.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (manager == null || interactable == null) return;
            IXRSelectInteractable sel = interactable;
            IXRSelectInteractor sock = socket;
            if (interactable.isSelected)
                foreach (var i in new System.Collections.Generic.List<IXRSelectInteractor>(interactable.interactorsSelecting)) manager.SelectCancel(i, sel);
            token.transform.SetPositionAndRotation(socket.attachTransform.position, socket.attachTransform.rotation);
            manager.SelectEnter(sock, sel);
        }
    }
}

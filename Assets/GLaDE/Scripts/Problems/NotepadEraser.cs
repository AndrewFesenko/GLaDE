using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GLaDE.Problems
{
    /// <summary>Clears a notepad when selected (a small eraser block on the desk).</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class NotepadEraser : MonoBehaviour
    {
        public Notepad notepad;
        void Awake() { GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => { if (notepad) notepad.Clear(); }); }
    }
}

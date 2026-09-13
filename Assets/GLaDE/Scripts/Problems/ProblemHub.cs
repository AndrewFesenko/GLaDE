using System.Collections.Generic;
using GLaDE.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GLaDE.Problems
{
    /// <summary>
    /// The VR hub menu: a world-space board listing every problem. Selecting one loads its scene; the
    /// problem scene's manager picks the selection up from <see cref="SelectedProblem"/>.
    /// </summary>
    public class ProblemHub : MonoBehaviour
    {
        public static StaticsProblem SelectedProblem;
        public static string HubSceneName = "Hub";

        public ProblemLibrary library;
        public Transform buttonContainer;
        public Button buttonTemplate;
        public TMP_Text descriptionText;
        public TMP_Text howToText;

        void Start()
        {
            if (library == null || buttonTemplate == null || buttonContainer == null) return;
            buttonTemplate.gameObject.SetActive(false);
            foreach (var p in library.problems)
            {
                if (p == null) continue;
                var b = Instantiate(buttonTemplate, buttonContainer);
                b.gameObject.SetActive(true);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label) label.text = $"<b>{p.title}</b>\n<size=70%><color=#FFFFFFAA>{p.category} · {p.sourceReference}</color>\n<color=#9BE8C0>{ProgressStore.Summary(p.problemId)}</color></size>";
                var captured = p;
                b.onClick.AddListener(() => Open(captured));
            }
            if (descriptionText) descriptionText.text = "Pick a problem. Every attempt has new numbers, so learn the method, not the answer.";
            if (howToText) howToText.text =
                "<b>How to play</b>\n" +
                "<b>Point + trigger</b> presses buttons and writes on the notepad (hold to draw).\n" +
                "<b>Grip</b> grabs: the section plane, force tokens, a truss half, the beam, and the green bar on top of any board.\n" +
                "Solve on the notepad, type your answers on the whiteboard, press <b>Check</b>. Stuck? <b>Guide me</b> takes the structure apart with you.";
        }

        public static void Open(StaticsProblem p)
        {
            SelectedProblem = p;
            SceneManager.LoadScene(p.sceneName);
        }

        public static void ReturnToHub()
        {
            SelectedProblem = null;
            SceneManager.LoadScene(HubSceneName);
        }
    }
}

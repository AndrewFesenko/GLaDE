using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GLaDE.Problems
{
    /// <summary>
    /// The teaching surface beside the problem. Shows the givens, the current phase instruction, and
    /// reveals solution steps one at a time with a readable typewriter cadence. Purely presentational:
    /// the <see cref="ProblemManager"/> decides what to show.
    /// </summary>
    public class Whiteboard : MonoBehaviour
    {
        [Header("Text")]
        public TMP_Text titleText;
        public TMP_Text promptText;
        public TMP_Text phaseText;
        public TMP_Text stepTitleText;
        public TMP_Text stepBodyText;
        public TMP_Text progressText;
        public TMP_Text feedbackText;

        [Header("Buttons")]
        public Button nextButton;
        public Button backButton;
        public Button hintButton;
        public Button skipButton;
        public Button newProblemButton;
        public Button resetButton;

        [Header("Reveal")]
        public float charactersPerSecond = 70f;
        public bool keyboardShortcuts = true;

        public event Action NextPressed, BackPressed, HintPressed, SkipPressed, NewProblemPressed, ResetPressed;

        Coroutine reveal;
        Coroutine feedbackFade;
        bool revealing;

        void Awake()
        {
            if (nextButton) nextButton.onClick.AddListener(() => Press(NextPressed));
            if (backButton) backButton.onClick.AddListener(() => Press(BackPressed));
            if (hintButton) hintButton.onClick.AddListener(() => Press(HintPressed));
            if (skipButton) skipButton.onClick.AddListener(() => Press(SkipPressed));
            if (newProblemButton) newProblemButton.onClick.AddListener(() => Press(NewProblemPressed));
            if (resetButton) resetButton.onClick.AddListener(() => Press(ResetPressed));
        }

        void Press(Action a)
        {
            if (revealing) { FinishReveal(); return; }
            a?.Invoke();
        }

        void Update()
        {
            if (!keyboardShortcuts) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) { if (nextButton && nextButton.interactable && nextButton.gameObject.activeInHierarchy) Press(NextPressed); }
            else if (kb.leftArrowKey.wasPressedThisFrame) { if (backButton && backButton.interactable && backButton.gameObject.activeInHierarchy) Press(BackPressed); }
            else if (kb.hKey.wasPressedThisFrame) { if (hintButton && hintButton.gameObject.activeInHierarchy) Press(HintPressed); }
            else if (kb.nKey.wasPressedThisFrame) { Press(NewProblemPressed); }
        }

        public void SetHeader(string title, string prompt)
        {
            if (titleText) titleText.text = title;
            if (promptText) promptText.text = prompt;
        }

        public void SetPhase(string text)
        {
            if (phaseText) phaseText.text = text;
        }

        public void SetProgress(string text)
        {
            if (progressText) progressText.text = text;
        }

        /// <summary>Clears the step area (used outside the Solve phase).</summary>
        public void ClearStep()
        {
            StopReveal();
            if (stepTitleText) stepTitleText.text = "";
            if (stepBodyText) { stepBodyText.text = ""; stepBodyText.maxVisibleCharacters = int.MaxValue; }
        }

        public void ShowStep(string title, string body, bool animate = true)
        {
            StopReveal();
            if (stepTitleText) stepTitleText.text = title;
            if (stepBodyText == null) return;
            stepBodyText.text = body;
            if (animate && charactersPerSecond > 0 && gameObject.activeInHierarchy) reveal = StartCoroutine(Reveal());
            else stepBodyText.maxVisibleCharacters = int.MaxValue;
        }

        IEnumerator Reveal()
        {
            revealing = true;
            stepBodyText.maxVisibleCharacters = 0;
            stepBodyText.ForceMeshUpdate();
            int total = stepBodyText.textInfo.characterCount;
            float shown = 0;
            while (shown < total)
            {
                shown += Time.deltaTime * charactersPerSecond;
                stepBodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            stepBodyText.maxVisibleCharacters = int.MaxValue;
            revealing = false; reveal = null;
        }

        void StopReveal()
        {
            if (reveal != null) StopCoroutine(reveal);
            reveal = null; revealing = false;
        }

        public void FinishReveal()
        {
            StopReveal();
            if (stepBodyText) stepBodyText.maxVisibleCharacters = int.MaxValue;
        }

        /// <summary>Transient message (hints, wrong-cut feedback). Fades after a while unless sticky.</summary>
        public void ShowFeedback(string text, float seconds = 8f)
        {
            if (feedbackText == null) return;
            if (feedbackFade != null) StopCoroutine(feedbackFade);
            feedbackText.text = text;
            feedbackText.alpha = 1f;
            if (seconds > 0 && gameObject.activeInHierarchy) feedbackFade = StartCoroutine(FadeFeedback(seconds));
        }

        IEnumerator FadeFeedback(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            float t = 0;
            while (t < 1f) { t += Time.deltaTime; feedbackText.alpha = 1f - t; yield return null; }
            feedbackText.text = "";
            feedbackText.alpha = 1f;
        }

        public void ConfigureButtons(bool next, bool back, bool hint, bool skip, bool newProblem = true, bool reset = true)
        {
            Show(nextButton, next); Show(backButton, back); Show(hintButton, hint); Show(skipButton, skip);
            Show(newProblemButton, newProblem); Show(resetButton, reset);
        }

        static void Show(Button b, bool on)
        {
            if (b == null) return;
            b.gameObject.SetActive(on);
        }

        public void SetButtonLabel(Button b, string label)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t) t.text = label;
        }
    }
}

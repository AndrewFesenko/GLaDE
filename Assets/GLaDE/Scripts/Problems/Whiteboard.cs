using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GLaDE.Problems
{
    /// <summary>
    /// The teaching surface beside the problem. Shows the givens, the current instruction, the homework
    /// answer sheet, and reveals solution steps one at a time with a readable typewriter cadence.
    /// Purely presentational: the <see cref="ProblemManager"/> decides what to show.
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

        [Header("Panels")]
        public RectTransform stepArea;
        public AnswerPanel answerPanel;

        [Header("Buttons")]
        public Button checkButton;
        public Button guideButton;
        public Button nextButton;
        public Button backButton;
        public Button hintButton;
        public Button skipButton;
        public Button newProblemButton;
        public Button resetButton;
        public Button reviewButton;

        [Header("Reveal")]
        public float charactersPerSecond = 70f;
        public bool keyboardShortcuts = true;

        public event Action CheckPressed, GuidePressed, NextPressed, BackPressed, HintPressed, SkipPressed, NewProblemPressed, ResetPressed, ReviewPressed;

        Coroutine reveal;
        Coroutine feedbackFade;
        bool revealing;

        void Awake()
        {
            Wire(checkButton, () => CheckPressed);
            Wire(guideButton, () => GuidePressed);
            Wire(nextButton, () => NextPressed);
            Wire(backButton, () => BackPressed);
            Wire(hintButton, () => HintPressed);
            Wire(skipButton, () => SkipPressed);
            Wire(newProblemButton, () => NewProblemPressed);
            Wire(resetButton, () => ResetPressed);
            Wire(reviewButton, () => ReviewPressed);
        }

        void Wire(Button b, Func<Action> ev)
        {
            if (b) b.onClick.AddListener(() => Press(ev()));
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
            if (kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) { if (Visible(nextButton)) Press(NextPressed); }
            else if (kb.leftArrowKey.wasPressedThisFrame) { if (Visible(backButton)) Press(BackPressed); }
            else if (kb.hKey.wasPressedThisFrame) { if (Visible(hintButton)) Press(HintPressed); }
            else if (kb.gKey.wasPressedThisFrame) { if (Visible(guideButton)) Press(GuidePressed); }
            else if (kb.nKey.wasPressedThisFrame) { if (Visible(newProblemButton)) Press(NewProblemPressed); }
        }

        static bool Visible(Button b) => b && b.interactable && b.gameObject.activeInHierarchy;

        public void SetHeader(string title, string prompt)
        {
            if (titleText) titleText.text = title;
            if (promptText) promptText.text = prompt;
        }

        public void SetPhase(string text) { if (phaseText) phaseText.text = text; }
        public void SetProgress(string text) { if (progressText) progressText.text = text; }

        /// <summary>Shows the answer sheet instead of the step text.</summary>
        public void ShowAnswerSheet(bool on)
        {
            if (answerPanel) answerPanel.gameObject.SetActive(on);
            if (stepArea) stepArea.gameObject.SetActive(!on);
        }

        public void ClearStep()
        {
            StopReveal();
            if (stepTitleText) stepTitleText.text = "";
            if (stepBodyText) { stepBodyText.text = ""; stepBodyText.maxVisibleCharacters = int.MaxValue; }
        }

        public void ShowStep(string title, string body, bool animate = true)
        {
            ShowAnswerSheet(false);
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

        /// <summary>Transient message (hints, feedback). Fades after a while; 0 seconds = sticky.</summary>
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

        /// <summary>Which buttons are visible. Everything not listed is hidden.</summary>
        public void ConfigureButtons(bool check = false, bool guide = false, bool next = false, bool back = false, bool hint = false,
            bool skip = false, bool newProblem = false, bool reset = false, bool review = false)
        {
            Show(checkButton, check); Show(guideButton, guide); Show(nextButton, next); Show(backButton, back);
            Show(hintButton, hint); Show(skipButton, skip); Show(newProblemButton, newProblem); Show(resetButton, reset); Show(reviewButton, review);
        }

        static void Show(Button b, bool on) { if (b) b.gameObject.SetActive(on); }

        public void SetButtonLabel(Button b, string label) => UIKit.SetLabel(b, label);
        public void SetButtonEnabled(Button b, bool on) { if (b) b.interactable = on; }
    }
}

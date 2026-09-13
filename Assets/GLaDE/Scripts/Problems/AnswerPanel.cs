using System;
using System.Collections.Generic;
using System.Globalization;
using GLaDE.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GLaDE.Problems
{
    /// <summary>
    /// The homework answer sheet on the whiteboard: one row per target (value + tension/compression for
    /// members, signed value for reactions) and a keypad. Built in code at runtime so it adapts to any problem.
    /// </summary>
    public class AnswerPanel : MonoBehaviour
    {
        public class Row
        {
            public string target;
            public bool isMember;
            public RectTransform root;
            public TMP_Text valueText;
            public Button tensionButton, compressionButton;
            public TMP_Text resultText;
            public string entry = "";
            public bool? tension;   // null = not chosen (members only)
        }

        public RectTransform container;
        public float rowHeight = 64f;
        public float keySize = 58f;

        public IReadOnlyList<Row> Rows => rows;
        public event Action Changed;

        readonly List<Row> rows = new List<Row>();
        Row selected;
        RectTransform keypad;
        TMP_Text instruction;

        static readonly Color RowIdle = new Color(0.16f, 0.19f, 0.27f, 0.95f);
        static readonly Color RowSelected = new Color(0.22f, 0.30f, 0.45f, 1f);
        static readonly Color TcOn = new Color(0.25f, 0.60f, 0.45f);
        static readonly Color TcOff = new Color(0.24f, 0.28f, 0.40f);

        ProblemInstance builtFor;

        /// <summary>Rebuilds the sheet for a problem instance. Entries typed for the same instance are kept.</summary>
        public void Build(ProblemInstance inst)
        {
            var keep = new Dictionary<string, (string entry, bool? tension)>();
            if (ReferenceEquals(builtFor, inst)) foreach (var r in rows) keep[r.target] = (r.entry, r.tension);
            builtFor = inst;
            foreach (Transform c in container) Destroy(c.gameObject);
            rows.Clear(); selected = null;

            float w = container.rect.width;
            float y = 0f;
            instruction = UIKit.MakeText(container, "Instruction",
                "Work it out on the notepad, then enter your answers. Pick a row, type with the keypad" +
                (inst.Problem.kind == StructureKind.Truss ? ", and mark tension or compression." : ". Reactions are positive in the direction drawn (up / right)."),
                20, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0, y), new Vector2(w, 52));
            instruction.color = new Color(0.85f, 0.88f, 0.95f);
            y -= 58;

            foreach (var target in inst.Problem.targets)
            {
                bool isMember = inst.Problem.IsMemberId(target);
                var row = new Row { target = target, isMember = isMember };
                row.root = UIKit.MakePanel(container, "Row " + target, RowIdle, new Vector2(0f, 1f), new Vector2(0, y), new Vector2(w, rowHeight));
                var rowBtn = row.root.gameObject.AddComponent<Button>();
                rowBtn.targetGraphic = row.root.GetComponent<Image>();
                var captured = row;
                rowBtn.onClick.AddListener(() => Select(captured));

                var label = UIKit.MakeText(row.root, "Name", SolutionGenerator.Sym(target, inst.Problem) + " =", 26, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(14, 0), new Vector2(120, rowHeight));
                label.color = new Color(0.6f, 0.85f, 1f);
                row.valueText = UIKit.MakeText(row.root, "Value", "", 26, TextAlignmentOptions.MidlineRight, new Vector2(0f, 0.5f), new Vector2(140, 0), new Vector2(170, rowHeight));
                row.valueText.color = Color.white;
                var unit = UIKit.MakeText(row.root, "Unit", "kN", 22, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(318, 0), new Vector2(50, rowHeight));
                unit.color = new Color(0.75f, 0.8f, 0.9f);

                if (isMember)
                {
                    row.tensionButton = UIKit.MakeButton(row.root, "T", "T", 22, new Vector2(70, rowHeight - 14));
                    Place(row.tensionButton, new Vector2(380, 0));
                    row.compressionButton = UIKit.MakeButton(row.root, "C", "C", 22, new Vector2(70, rowHeight - 14));
                    Place(row.compressionButton, new Vector2(458, 0));
                    row.tensionButton.onClick.AddListener(() => { captured.tension = true; Select(captured); Refresh(); });
                    row.compressionButton.onClick.AddListener(() => { captured.tension = false; Select(captured); Refresh(); });
                }
                row.resultText = UIKit.MakeText(row.root, "Result", "", 22, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(545, 0), new Vector2(w - 560, rowHeight));
                if (keep.TryGetValue(target, out var prev)) { row.entry = prev.entry; row.tension = prev.tension; }
                rows.Add(row);
                y -= rowHeight + 8;
            }

            BuildKeypad(y - 6);
            if (rows.Count > 0) Select(rows[0]);
            Refresh();
        }

        static void Place(Button b, Vector2 pos)
        {
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
        }

        void BuildKeypad(float y)
        {
            keypad = new GameObject("Keypad", typeof(RectTransform)).GetComponent<RectTransform>();
            keypad.SetParent(container, false);
            keypad.anchorMin = new Vector2(0f, 1f); keypad.anchorMax = new Vector2(0f, 1f); keypad.pivot = new Vector2(0f, 1f);
            keypad.anchoredPosition = new Vector2(0, y);
            keypad.sizeDelta = new Vector2(container.rect.width, keySize * 3 + 16);
            string[] keys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", "0", ".", "−" };
            float gap = 8f;
            for (int i = 0; i < keys.Length; i++)
            {
                int col = i % 3, rowI = i / 3;
                var key = UIKit.MakeButton(keypad, "Key " + keys[i], keys[i], 26, new Vector2(keySize * 1.6f, keySize));
                var rt = key.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(col * (keySize * 1.6f + gap), -rowI * (keySize + gap));
                string k = keys[i];
                key.onClick.AddListener(() => Type(k));
            }
            // fourth row: backspace + clear
            float x4 = 3 * (keySize * 1.6f + gap) + 20f;
            var back = UIKit.MakeButton(keypad, "Backspace", "Delete", 22, new Vector2(keySize * 2.2f, keySize), UIKit.WarnColor);
            var brt = back.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(x4, 0);
            back.onClick.AddListener(Backspace);
            var clear = UIKit.MakeButton(keypad, "Clear", "Clear row", 22, new Vector2(keySize * 2.2f, keySize), UIKit.WarnColor);
            var crt = clear.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(0f, 1f); crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(x4, -(keySize + gap));
            clear.onClick.AddListener(() => { if (selected != null) { selected.entry = ""; selected.tension = null; Refresh(); } });
            keypad.sizeDelta = new Vector2(container.rect.width, 4 * (keySize + gap));
        }

        public float ContentHeight => rows.Count == 0 ? 0 : 58 + rows.Count * (rowHeight + 8) + 4 * (keySize + 8) + 12;

        void Select(Row r)
        {
            selected = r;
            Refresh();
        }

        void Type(string k)
        {
            if (selected == null) return;
            if (k == "−") { selected.entry = selected.entry.StartsWith("-") ? selected.entry.Substring(1) : "-" + selected.entry; }
            else if (k == ".") { if (!selected.entry.Contains(".")) selected.entry += selected.entry.Length == 0 || selected.entry == "-" ? "0." : "."; }
            else if (selected.entry.Replace("-", "").Replace(".", "").Length < 6) selected.entry += k;
            Refresh();
        }

        void Backspace()
        {
            if (selected == null || selected.entry.Length == 0) return;
            selected.entry = selected.entry.Substring(0, selected.entry.Length - 1);
            Refresh();
        }

        void Refresh()
        {
            foreach (var r in rows)
            {
                r.root.GetComponent<Image>().color = r == selected ? RowSelected : RowIdle;
                r.valueText.text = r.entry.Length == 0 ? "<alpha=#55>0.0" : r.entry + (r == selected ? "<color=#FFD166>|</color>" : "");
                if (r.tensionButton) UIKit.SetColor(r.tensionButton, r.tension == true ? TcOn : TcOff);
                if (r.compressionButton) UIKit.SetColor(r.compressionButton, r.tension == false ? TcOn : TcOff);
            }
            Changed?.Invoke();
        }

        /// <summary>True when every row has a number (and T/C for members).</summary>
        public bool IsComplete()
        {
            foreach (var r in rows)
            {
                if (!TryParse(r.entry, out _)) return false;
                if (r.isMember && r.tension == null) return false;
            }
            return rows.Count > 0;
        }

        public static bool TryParse(string s, out double v) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>Marks each row against the solved answers. Returns the number of correct rows.</summary>
        public int Grade(ProblemInstance inst, double relativeTolerance, double absoluteTolerance)
        {
            int correct = 0;
            foreach (var r in rows)
            {
                if (!inst.TryGetAnswer(r.target, out double truth)) continue;
                bool ok = false; string why = "";
                if (TryParse(r.entry, out double v))
                {
                    double tol = Math.Max(absoluteTolerance, relativeTolerance * Math.Abs(truth));
                    if (r.isMember)
                    {
                        bool magOk = Math.Abs(Math.Abs(v) - Math.Abs(truth)) <= tol;
                        bool zero = Math.Abs(truth) < 1e-6;
                        bool tcOk = zero ? true : (r.tension == (truth > 0));
                        ok = magOk && tcOk;
                        why = !magOk ? "magnitude" : (!tcOk ? "tension / compression" : "");
                    }
                    else
                    {
                        ok = Math.Abs(v - truth) <= tol;
                        why = ok ? "" : (Math.Abs(Math.Abs(v) - Math.Abs(truth)) <= tol ? "sign" : "magnitude");
                    }
                }
                else why = "no number";
                if (ok) correct++;
                r.resultText.text = ok ? "<color=#06D6A0>✓ correct</color>" : $"<color=#FF6B6B>✗ check {why}</color>";
            }
            return correct;
        }

        /// <summary>Fills the rows with the true answers (used when the student gives up or after guidance).</summary>
        public void Reveal(ProblemInstance inst)
        {
            foreach (var r in rows)
            {
                if (!inst.TryGetAnswer(r.target, out double truth)) continue;
                r.entry = ProblemInstance.Fmt(r.isMember ? Math.Abs(truth) : truth, 1);
                if (r.isMember) r.tension = truth >= 0;
                r.resultText.text = "<color=#FFD166>revealed</color>";
            }
            selected = null;
            Refresh();
        }

        public void SetInteractable(bool on)
        {
            foreach (var b in container.GetComponentsInChildren<Button>(true)) b.interactable = on;
        }
    }
}

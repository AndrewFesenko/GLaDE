using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace GLaDE.Problems
{
    /// <summary>
    /// A multi-page drawing surface for hand-written working. Point at it and hold the trigger (mouse
    /// button in the simulator) to write. Strokes are recorded, so Undo removes the last one and pages
    /// can be re-rendered; the notebook is saved per problem so notes survive leaving the scene.
    /// Needs a MeshCollider (a Quad) so hits carry texture coordinates.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class Notepad : MonoBehaviour
    {
        [Serializable] class Stroke { public List<Vector2> points = new List<Vector2>(); }
        [Serializable] class Page { public List<Stroke> strokes = new List<Stroke>(); }
        [Serializable] class Notebook { public List<Page> pages = new List<Page>(); public int current; }

        public int width = 1024, height = 768;
        public Color paper = new Color(0.97f, 0.96f, 0.92f);
        public Color ink = new Color(0.12f, 0.15f, 0.35f);
        public float brushRadiusPixels = 4f;
        [Tooltip("Notes are saved under this key; leave empty to keep them only for this session.")]
        public string notebookKey = "";
        public TMP_Text pageLabel;

        Texture2D tex;
        Color32[] pixels;
        bool dirty;
        MeshRenderer rend;
        Collider surface;
        XRSimpleInteractable interactable;
        Notebook book = new Notebook();
        readonly Dictionary<IXRSelectInteractor, Stroke> activeStrokes = new Dictionary<IXRSelectInteractor, Stroke>();
        readonly Dictionary<IXRSelectInteractor, Vector2> lastUv = new Dictionary<IXRSelectInteractor, Vector2>();

        Page CurrentPage => book.pages[book.current];
        public int PageIndex => book.current;
        public int PageCount => book.pages.Count;

        void Awake()
        {
            rend = GetComponent<MeshRenderer>();
            surface = GetComponent<Collider>();
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectExited.AddListener(a => EndStroke(a.interactorObject));
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            pixels = new Color32[width * height];
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            rend.material = mat;
            Load();
            if (book.pages.Count == 0) book.pages.Add(new Page());
            book.current = Mathf.Clamp(book.current, 0, book.pages.Count - 1);
            Render();
        }

        void Update()
        {
            if (!interactable.isSelected) return;
            foreach (var interactor in interactable.interactorsSelecting)
            {
                if (!TryGetPointerRay(interactor, out var ray)) continue;
                if (surface.Raycast(ray, out var hit, 10f))
                {
                    Vector2 uv = hit.textureCoord;
                    if (!activeStrokes.TryGetValue(interactor, out var stroke))
                    {
                        stroke = new Stroke();
                        CurrentPage.strokes.Add(stroke);
                        activeStrokes[interactor] = stroke;
                        DrawDot(uv);
                    }
                    else if (lastUv.TryGetValue(interactor, out var prev)) DrawSegment(prev, uv);
                    stroke.points.Add(uv);
                    lastUv[interactor] = uv;
                }
                else EndStroke(interactor);
            }
        }

        void EndStroke(IXRSelectInteractor interactor)
        {
            if (activeStrokes.Remove(interactor)) Save();
            lastUv.Remove(interactor);
        }

        /// <summary>The ray the interactor is pointing with: the curve origin for ray-style interactors, else its transform.</summary>
        static bool TryGetPointerRay(IXRSelectInteractor interactor, out Ray ray)
        {
            if (interactor is ICurveInteractionDataProvider curve && curve.curveOrigin != null)
            {
                Vector3 origin = curve.curveOrigin.position;
                Vector3 dir = curve.curveOrigin.forward;
                if (curve.TryGetCurveEndPoint(out var end) != EndPointType.None && (end - origin).sqrMagnitude > 1e-6f) dir = (end - origin).normalized;
                ray = new Ray(origin, dir);
                return true;
            }
            var t = interactor.transform;
            if (t == null) { ray = default; return false; }
            ray = new Ray(t.position, t.forward);
            return true;
        }

        // ------------------------------------------------------------------ pages & history (wired to buttons)

        public void Undo()
        {
            var page = CurrentPage;
            if (page.strokes.Count == 0) return;
            page.strokes.RemoveAt(page.strokes.Count - 1);
            activeStrokes.Clear(); lastUv.Clear();
            Render(); Save();
        }

        /// <summary>Clears the current page.</summary>
        public void Clear()
        {
            CurrentPage.strokes.Clear();
            activeStrokes.Clear(); lastUv.Clear();
            Render(); Save();
        }

        public void PreviousPage()
        {
            if (book.current == 0) return;
            book.current--;
            activeStrokes.Clear(); lastUv.Clear();
            Render(); Save();
        }

        /// <summary>Next page; past the last page starts a new one.</summary>
        public void NextPage()
        {
            if (book.current == book.pages.Count - 1)
            {
                if (CurrentPage.strokes.Count == 0) return;   // no point stacking empty pages
                book.pages.Add(new Page());
            }
            book.current++;
            activeStrokes.Clear(); lastUv.Clear();
            Render(); Save();
        }

        // ------------------------------------------------------------------ rendering

        void Render()
        {
            Color32 p = paper;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = p;
            Color32 g = Color.Lerp(paper, ink, 0.12f);
            int step = width / 32;
            for (int y = 0; y < height; y += step) for (int x = 0; x < width; x++) pixels[y * width + x] = g;
            for (int x = 0; x < width; x += step) for (int y = 0; y < height; y++) pixels[y * width + x] = g;
            foreach (var s in CurrentPage.strokes)
            {
                if (s.points.Count == 0) continue;
                DrawDot(s.points[0]);
                for (int i = 1; i < s.points.Count; i++) DrawSegment(s.points[i - 1], s.points[i]);
            }
            if (pageLabel) pageLabel.text = $"Page {book.current + 1} / {book.pages.Count}";
            dirty = true;
        }

        void DrawSegment(Vector2 fromUv, Vector2 toUv)
        {
            Vector2 a = new Vector2(fromUv.x * width, fromUv.y * height);
            Vector2 b = new Vector2(toUv.x * width, toUv.y * height);
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (brushRadiusPixels * 0.5f)));
            for (int i = 0; i <= steps; i++) DotPixels(Vector2.Lerp(a, b, (float)i / steps));
            dirty = true;
        }

        void DrawDot(Vector2 uv) => DotPixels(new Vector2(uv.x * width, uv.y * height));

        void DotPixels(Vector2 px)
        {
            int r = Mathf.CeilToInt(brushRadiusPixels);
            int cx = Mathf.RoundToInt(px.x), cy = Mathf.RoundToInt(px.y);
            Color32 c = ink;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r * r) continue;
                    int X = cx + x, Y = cy + y;
                    if (X < 0 || Y < 0 || X >= width || Y >= height) continue;
                    pixels[Y * width + X] = c;
                }
            dirty = true;
        }

        void LateUpdate()
        {
            if (!dirty) return;
            tex.SetPixels32(pixels);
            tex.Apply(false);
            dirty = false;
        }

        // ------------------------------------------------------------------ test helpers

        /// <summary>Adds a finished stroke programmatically (tests, demos).</summary>
        public void AddStroke(IList<Vector2> uvs)
        {
            var s = new Stroke(); s.points.AddRange(uvs);
            CurrentPage.strokes.Add(s);
            Render(); Save();
        }

        // ------------------------------------------------------------------ persistence

        string SavePath => string.IsNullOrEmpty(notebookKey) ? null : Path.Combine(Application.persistentDataPath, $"notebook_{notebookKey}.json");

        void Save()
        {
            var path = SavePath; if (path == null) return;
            try { File.WriteAllText(path, JsonUtility.ToJson(book)); }
            catch (Exception e) { Debug.LogWarning("[GLaDE] Could not save notebook: " + e.Message); }
        }

        void Load()
        {
            var path = SavePath; if (path == null || !File.Exists(path)) return;
            try { book = JsonUtility.FromJson<Notebook>(File.ReadAllText(path)) ?? new Notebook(); }
            catch (Exception e) { Debug.LogWarning("[GLaDE] Could not load notebook: " + e.Message); book = new Notebook(); }
        }
    }
}

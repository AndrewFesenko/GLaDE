using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace GLaDE.Problems
{
    /// <summary>
    /// A drawing surface for hand-written working: free-body sketches, equations, arithmetic.
    /// Point at it and hold the trigger (mouse button in the simulator) to write; the ray's hit point is
    /// the pen. Needs a MeshCollider (a Quad) so hits carry texture coordinates.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class Notepad : MonoBehaviour
    {
        public int width = 1024, height = 768;
        public Color paper = new Color(0.97f, 0.96f, 0.92f);
        public Color ink = new Color(0.12f, 0.15f, 0.35f);
        public float brushRadiusPixels = 4f;

        Texture2D tex;
        Color32[] pixels;
        bool dirty;
        MeshRenderer rend;
        Collider surface;
        XRSimpleInteractable interactable;
        readonly Dictionary<IXRSelectInteractor, Vector2> lastUv = new Dictionary<IXRSelectInteractor, Vector2>();

        void Awake()
        {
            rend = GetComponent<MeshRenderer>();
            surface = GetComponent<Collider>();
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectExited.AddListener(a => lastUv.Remove(a.interactorObject));
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            pixels = new Color32[width * height];
            Clear();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            rend.material = mat;
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
                    if (lastUv.TryGetValue(interactor, out var prev)) Stroke(prev, uv); else Dot(uv);
                    lastUv[interactor] = uv;
                }
                else lastUv.Remove(interactor);
            }
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

        public void Clear()
        {
            Color32 p = paper;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = p;
            // faint grid, like engineering paper
            Color32 g = Color.Lerp(paper, ink, 0.12f);
            int step = width / 32;
            for (int y = 0; y < height; y += step) for (int x = 0; x < width; x++) pixels[y * width + x] = g;
            for (int x = 0; x < width; x += step) for (int y = 0; y < height; y++) pixels[y * width + x] = g;
            lastUv.Clear();
            dirty = true;
        }

        /// <summary>Draws a stroke segment between two texture coordinates (0..1).</summary>
        public void Stroke(Vector2 fromUv, Vector2 toUv)
        {
            Vector2 a = new Vector2(fromUv.x * width, fromUv.y * height);
            Vector2 b = new Vector2(toUv.x * width, toUv.y * height);
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / (brushRadiusPixels * 0.5f)));
            for (int i = 0; i <= steps; i++) DotPixels(Vector2.Lerp(a, b, (float)i / steps));
            dirty = true;
        }

        public void Dot(Vector2 uv) => DotPixels(new Vector2(uv.x * width, uv.y * height));

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
    }
}

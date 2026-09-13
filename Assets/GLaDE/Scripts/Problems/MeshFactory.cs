using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Procedural meshes for arrows and markers so the project needs no external models for them.</summary>
    public static class MeshFactory
    {
        static Mesh s_Cone;

        /// <summary>Unit cone: base circle of radius 1 at y = 0, apex at y = 1.</summary>
        public static Mesh Cone(int segments = 24)
        {
            if (s_Cone != null) return s_Cone;
            var mesh = new Mesh { name = "GLaDE Cone" };
            var verts = new Vector3[segments * 3 + segments * 3];
            var norms = new Vector3[verts.Length];
            var tris = new int[segments * 6];
            int v = 0, t = 0;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2 / segments, a1 = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 apex = new Vector3(0, 1, 0);
                // side (flat shaded per segment)
                Vector3 n = Vector3.Cross(p1 - apex, p0 - apex).normalized;
                verts[v] = p0; norms[v] = n; verts[v + 1] = apex; norms[v + 1] = n; verts[v + 2] = p1; norms[v + 2] = n;
                tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + 2; v += 3;
                // base
                verts[v] = p0; norms[v] = Vector3.down; verts[v + 1] = p1; norms[v + 1] = Vector3.down; verts[v + 2] = Vector3.zero; norms[v + 2] = Vector3.down;
                tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + 2; v += 3;
            }
            mesh.vertices = verts; mesh.normals = norms; mesh.triangles = tris;
            mesh.RecalculateBounds();
            s_Cone = mesh;
            return mesh;
        }

        /// <summary>Builds an arrow whose tail is at the local origin and which points along local +Y.</summary>
        public static GameObject Arrow(string name, float length, float shaftRadius, Material mat, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            float headLen = Mathf.Min(length * 0.35f, shaftRadius * 6f);
            float shaftLen = Mathf.Max(0.01f, length - headLen);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            SafeDestroy(shaft.GetComponent<Collider>());
            shaft.name = "Shaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localPosition = new Vector3(0, shaftLen * 0.5f, 0);
            shaft.transform.localScale = new Vector3(shaftRadius * 2, shaftLen * 0.5f, shaftRadius * 2);
            shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, shaftLen, 0);
            head.transform.localScale = new Vector3(shaftRadius * 2.6f, headLen, shaftRadius * 2.6f);
            head.AddComponent<MeshFilter>().sharedMesh = Cone();
            head.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return root;
        }

        public static void SetArrowMaterial(GameObject arrow, Material mat)
        {
            foreach (var r in arrow.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        }

        /// <summary>Destroys a component safely in both edit and play mode.</summary>
        public static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}

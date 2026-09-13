using System.Collections.Generic;
using GLaDE.Core;
using GLaDE.Problems;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace GLaDE.EditorTools
{
    /// <summary>
    /// Builds the GLaDE scenes from code: materials, the visual theme, the VR hub and one scene per problem.
    /// Re-running is safe; scenes are regenerated from scratch so the layout stays reproducible.
    /// </summary>
    public static class GLaDESceneBuilder
    {
        public const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.6.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        public const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.6.0/XR Interaction Simulator/XR Interaction Simulator.prefab";
        public const string ScenesFolder = "Assets/GLaDE/Scenes";
        public const string MaterialsFolder = "Assets/GLaDE/Materials";
        public const string ThemePath = "Assets/GLaDE/Data/GLaDE Visual Theme.asset";
        public const string FontAssetPath = "Assets/GLaDE/Fonts/GLaDE Sans SDF.asset";
        public const string HubSceneName = "Hub";
        public const string LogoPath = "Assets/GLaDE/Art/GLaDE Logo.png";
        public const string AudioFolder = "Assets/GLaDE/Audio";

        // Room layout (metres). Player faces +z.
        static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -1.0f);
        static readonly Vector3 StructurePos = new Vector3(0f, 1.05f, 0.85f);
        static readonly Vector3 BoardPos = new Vector3(1.85f, 1.5f, 0.55f);
        static readonly Vector3 DeskPos = new Vector3(-1.35f, 0f, 0.15f);      // desk centre on the floor
        static readonly Vector3 PlanePos = new Vector3(-0.85f, 1.35f, 0.45f);
        static readonly Vector3 NotepadPos = new Vector3(-1.85f, 1.5f, 0.55f);

        [MenuItem("GLaDE/Build Everything", priority = 0)]
        public static void BuildAll()
        {
            var theme = CreateTheme();
            // Re-authoring the problem assets triggers a reimport that makes them unloadable for a while, so
            // only create them when missing. Use GLaDE > Create Problem Assets after editing the factory.
            bool haveAssets = AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.TrussAssetPath) != null
                           && AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.BeamAssetPath) != null;
            if (haveAssets) BuildScenes(theme);
            else
            {
                ProblemAssetFactory.CreateAll();
                EditorApplication.delayCall += () => BuildScenes(theme);   // let the new assets import first
            }
        }

        static void BuildScenes(VisualTheme theme)
        {
            // Load each problem right before its scene: creating a new scene unloads unreferenced assets,
            // so a problem loaded earlier comes back as a dead reference.
            BuildProblemScene(ProblemAssetFactory.TrussAssetPath, theme);
            BuildProblemScene(ProblemAssetFactory.BeamAssetPath, theme);
            BuildHubScene(theme);
            UpdateBuildSettings();
            Debug.Log("[GLaDE] Build Everything finished.");
        }

        static StaticsProblem LoadProblem(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<StaticsProblem>(path);
            if (p == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                p = AssetDatabase.LoadAssetAtPath<StaticsProblem>(path);
            }
            if (p == null)
            {
                // Last resort: find it by type and file name.
                string wanted = System.IO.Path.GetFileName(path);
                foreach (var guid in AssetDatabase.FindAssets("t:StaticsProblem"))
                {
                    string candidate = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileName(candidate) == wanted) { p = AssetDatabase.LoadAssetAtPath<StaticsProblem>(candidate); break; }
                }
                Debug.LogWarning($"[GLaDE] LoadProblem('{path}') needed the fallback search; found={(p != null)}");
            }
            return p;
        }

        [MenuItem("GLaDE/Build Truss Scene", priority = 1)]
        public static void BuildTruss() => BuildProblemScene(ProblemAssetFactory.TrussAssetPath, CreateTheme());

        [MenuItem("GLaDE/Build Beam Scene", priority = 2)]
        public static void BuildBeam() => BuildProblemScene(ProblemAssetFactory.BeamAssetPath, CreateTheme());

        [MenuItem("GLaDE/Build Hub Scene", priority = 3)]
        public static void BuildHub() => BuildHubScene(CreateTheme());

        // ------------------------------------------------------------------ font, materials & theme

        /// <summary>
        /// The bundled LiberationSans atlas is static (ASCII + Latin-1). Equations use Greek sigma, arrows and
        /// dashes, so the project uses its own dynamic font asset and makes it the TextMeshPro default.
        /// </summary>
        public static void EnsureDynamicFont()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (liberation != null && liberation.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                liberation.atlasPopulationMode = AtlasPopulationMode.Static;
                liberation.isMultiAtlasTexturesEnabled = false;
                EditorUtility.SetDirty(liberation);
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                if (ttf == null) { Debug.LogWarning("[GLaDE] LiberationSans.ttf not found; import TMP Essentials."); return; }
                if (!AssetDatabase.IsValidFolder("Assets/GLaDE/Fonts")) AssetDatabase.CreateFolder("Assets/GLaDE", "Fonts");
                font = TMP_FontAsset.CreateFontAsset(ttf, 72, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "GLaDE Sans SDF";
                font.atlasTexture.name = "GLaDE Sans SDF Atlas";
                font.material.name = "GLaDE Sans SDF Material";
                AssetDatabase.CreateAsset(font, FontAssetPath);
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                Debug.Log("[GLaDE] Created dynamic font asset " + FontAssetPath);
            }

            var settings = TMP_Settings.instance;
            if (settings != null && font != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null && prop.objectReferenceValue != font)
                {
                    prop.objectReferenceValue = font;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[GLaDE] TMP default font set to GLaDE Sans SDF.");
                }
            }
        }

        public static VisualTheme CreateTheme()
        {
            EnsureDynamicFont();
            ProblemAssetFactory.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/GLaDE", "Materials");
            if (!AssetDatabase.IsValidFolder(ScenesFolder)) AssetDatabase.CreateFolder("Assets/GLaDE", "Scenes");
            var t = ProblemAssetFactory.CreateOrLoad<VisualTheme>(ThemePath);

            t.member = Mat("Member Steel", new Color(0.78f, 0.80f, 0.84f), 0.75f, 0.62f);
            t.joint = Mat("Joint", new Color(0.30f, 0.33f, 0.40f), 0.6f, 0.55f);
            t.support = Mat("Support", new Color(0.36f, 0.30f, 0.24f), 0.2f, 0.45f);
            t.ground = Mat("Ground", new Color(0.36f, 0.38f, 0.42f), 0.0f, 0.15f);
            t.stand = Mat("Stand", new Color(0.22f, 0.24f, 0.28f), 0.4f, 0.35f);

            t.memberHighlight = Mat("Member Highlight", new Color(1f, 0.78f, 0.25f), 0.3f, 0.6f, emission: new Color(0.9f, 0.6f, 0.1f) * 1.2f);
            t.memberCut = Mat("Member Cut", new Color(1f, 0.35f, 0.3f), 0.3f, 0.6f, emission: new Color(0.8f, 0.15f, 0.1f));
            t.memberGhost = Mat("Member Ghost", new Color(0.8f, 0.85f, 0.95f, 0.16f), 0.0f, 0.2f, transparent: true);
            t.memberTension = Mat("Member Tension", new Color(0.25f, 0.55f, 1f), 0.4f, 0.6f, emission: new Color(0.05f, 0.2f, 0.6f));
            t.memberCompression = Mat("Member Compression", new Color(1f, 0.32f, 0.28f), 0.4f, 0.6f, emission: new Color(0.6f, 0.08f, 0.05f));
            t.memberZero = Mat("Member Zero Force", new Color(0.6f, 0.62f, 0.66f), 0.3f, 0.3f);

            t.loadArrow = Mat("Load Arrow", new Color(1f, 0.50f, 0.25f), 0.1f, 0.5f, emission: new Color(0.5f, 0.15f, 0.03f));
            t.reactionArrow = Mat("Reaction Arrow", new Color(0.35f, 0.8f, 1f), 0.1f, 0.5f, emission: new Color(0.05f, 0.25f, 0.45f));
            t.tokenIdle = Mat("Token Idle", new Color(0.92f, 0.92f, 0.95f), 0.2f, 0.55f);
            t.tokenPlaced = Mat("Token Placed", new Color(0.25f, 0.85f, 0.55f), 0.2f, 0.55f, emission: new Color(0.05f, 0.35f, 0.18f));
            t.socketHint = Mat("Socket Hint", new Color(0.5f, 0.9f, 1f, 0.35f), 0f, 0.3f, transparent: true, emission: new Color(0.1f, 0.3f, 0.4f));

            t.sectionPlane = Mat("Section Plane", new Color(0.55f, 0.75f, 1f, 0.22f), 0f, 0.4f, transparent: true);
            t.sectionPlaneActive = Mat("Section Plane Active", new Color(1f, 0.75f, 0.35f, 0.32f), 0f, 0.4f, transparent: true);
            t.whiteboardSurface = Mat("Board Surface", new Color(0.10f, 0.12f, 0.16f), 0.0f, 0.35f);
            t.whiteboardFrame = Mat("Board Frame", new Color(0.55f, 0.45f, 0.32f), 0.1f, 0.5f);

            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            return t;
        }

        static Material Mat(string name, Color color, float metallic, float smoothness, bool transparent = false, Color? emission = null)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ scenes

        public static void BuildProblemScene(string problemPath, VisualTheme theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var problem = LoadProblem(problemPath);
            if (problem == null) { Debug.LogError($"[GLaDE] Problem asset missing at {problemPath}; run GLaDE/Create Problem Assets first."); return; }
            Debug.Log("[GLaDE] Building scene for " + problem.name + " -> " + problem.sceneName);
            BuildEnvironment(theme);
            BuildXRCore();
            BuildRig(PlayerStart, Quaternion.identity);

            var problemGo = new GameObject("Problem");
            var pm = problemGo.AddComponent<ProblemManager>();
            pm.problem = problem; pm.theme = theme;

            var structureGo = new GameObject("Structure View");
            structureGo.transform.SetParent(problemGo.transform, false);
            structureGo.transform.position = StructurePos + (problem.kind == StructureKind.RigidBody ? new Vector3(0, 0.08f, 0) : Vector3.zero);
            var sv = structureGo.AddComponent<StructureView>();
            sv.theme = theme;
            pm.structure = sv;

            pm.whiteboard = BuildWhiteboard(theme, BoardPos, FaceFrom(BoardPos, PlayerStart + Vector3.up * 1.5f), problemGo.transform, true);

            // Desk for the force tokens (they only appear in guided mode).
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Desk";
            desk.transform.SetParent(problemGo.transform, false);
            desk.transform.position = DeskPos + new Vector3(0, 0.41f, 0);
            desk.transform.localScale = new Vector3(1.1f, 0.82f, 0.5f);
            desk.GetComponent<MeshRenderer>().sharedMaterial = theme.stand;
            float deskTop = 0.82f;

            // Notepad: a wall board to the left of the problem, mirroring the whiteboard on the right.
            BuildNotepad(theme, problemGo.transform, NotepadPos, FaceFrom(NotepadPos, PlayerStart + Vector3.up * 1.5f), problem.problemId);

            var tools = new GameObject("Guided Tools");
            tools.transform.SetParent(problemGo.transform, false);
            pm.toolsRoot = tools;
            var rack = new GameObject("Token Rack").transform;
            rack.SetParent(tools.transform, false);
            rack.position = DeskPos + new Vector3(0, deskTop + 0.02f, 0f);
            pm.tokenRack = rack;
            var rackSign = MakeWorldLabel(tools.transform, "<b>Force tokens</b>\n<size=70%>Grab one and drop it on a marker; it names itself.</size>",
                DeskPos + new Vector3(0, deskTop + 0.36f, 0.12f), 0.05f, theme.labelColor);
            rackSign.name = "Rack Sign";

            if (problem.kind == StructureKind.Truss)
            {
                var plane = SectionPlane.Create(theme, sv, tools.transform, PlanePos, Quaternion.Euler(0, 90, 0));
                pm.sectionPlane = plane;
                var planeSign = MakeWorldLabel(plane.transform, "<b>Section plane</b>\n<size=70%>Grab me. Slide me through the members you need.</size>",
                    PlanePos + new Vector3(0, plane.height * 0.5f + 0.08f, 0), 0.05f, theme.labelColor);
                planeSign.name = "Plane Sign";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string path = $"{ScenesFolder}/{problem.sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[GLaDE] Built {path}");
        }

        static GameObject MakeWorldLabel(Transform parent, string text, Vector3 worldPos, float sizeMetres, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text; tmp.fontSize = sizeMetres * 10f; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            go.AddComponent<Billboard>();
            return go;
        }

        /// <summary>Wall-mounted drawing board: point and hold the trigger to write, Clear button on the frame.</summary>
        static void BuildNotepad(VisualTheme theme, Transform parent, Vector3 pos, Quaternion rot, string notebookKey)
        {
            float w = 1.2f, h = 0.95f;
            var root = BuildBoardShell(theme, pos, rot, parent, w, h, out var canvas, true);
            root.name = "Notepad Board";

            float paperH = h - 0.12f;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);   // MeshCollider gives texture coordinates on hit
            quad.name = "Paper";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0, 0.05f, -0.002f);   // a Quad faces -z: toward the player
            quad.transform.localScale = new Vector3(w - 0.04f, paperH, 1f);
            var pad = quad.AddComponent<Notepad>();
            pad.width = 1200; pad.height = Mathf.RoundToInt(1200 * paperH / (w - 0.04f));
            pad.notebookKey = notebookKey;

            var title = UIKit.MakeText(canvas, "Title", "<b>Notepad</b>  <size=70%>hold the trigger to write</size>", 22, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f), new Vector2(24, 14), new Vector2(430, 56));
            title.color = theme.labelColor;

            // Bottom strip: page navigation on the left, undo / clear on the right.
            var row = new GameObject("Pad Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(canvas, false);
            row.anchorMin = new Vector2(1f, 0f); row.anchorMax = new Vector2(1f, 0f); row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-20, 12); row.sizeDelta = new Vector2(720, 56);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleRight;
            var prev = UIKit.MakeButton(row, "Prev", "< Page", 20, new Vector2(120, 52));
            var pageLabel = UIKit.MakeText(row, "Page", "Page 1 / 1", 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 52));
            pageLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;
            pageLabel.color = theme.labelColor;
            var next = UIKit.MakeButton(row, "Next", "Page >", 20, new Vector2(120, 52));
            var undo = UIKit.MakeButton(row, "Undo", "Undo", 20, new Vector2(110, 52), UIKit.WarnColor);
            var clear = UIKit.MakeButton(row, "Clear", "Clear page", 20, new Vector2(140, 52), UIKit.WarnColor);
            pad.pageLabel = pageLabel;
            UnityEventTools.AddPersistentListener(prev.onClick, pad.PreviousPage);
            UnityEventTools.AddPersistentListener(next.onClick, pad.NextPage);
            UnityEventTools.AddPersistentListener(undo.onClick, pad.Undo);
            UnityEventTools.AddPersistentListener(clear.onClick, pad.Clear);
        }

        public static void BuildHubScene(VisualTheme theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEnvironment(theme);
            BuildXRCore();
            BuildRig(PlayerStart, Quaternion.identity);

            var library = AssetDatabase.LoadAssetAtPath<ProblemLibrary>(ProblemAssetFactory.LibraryPath);
            Vector3 boardPos = new Vector3(0f, 1.5f, 1.2f);
            var board = BuildBoardShell(theme, boardPos, FaceFrom(boardPos, PlayerStart + Vector3.up * 1.5f), null, 1.5f, 1.1f, out var canvas, false);
            board.name = "Hub Board";
            AddSounds(board);
            BuildLogo(null, boardPos + new Vector3(0f, 0.95f, 0.0f), FaceFrom(boardPos, PlayerStart), 0.7f, 1f);

            // How-to card to the left of the board
            Vector3 howPos = boardPos + new Vector3(-1.45f, -0.1f, 0.15f);
            var how = BuildBoardShell(theme, howPos, FaceFrom(howPos, PlayerStart + Vector3.up * 1.5f), null, 1.0f, 0.9f, out var howCanvas, false);
            how.name = "How To Board";
            var howText = UIKit.MakeText(howCanvas, "How To", "", 24, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(920, 820));
            howText.rectTransform.pivot = new Vector2(0.5f, 1f);
            howText.color = new Color(0.9f, 0.92f, 0.96f);
            howText.enableAutoSizing = true; howText.fontSizeMin = 16; howText.fontSizeMax = 26;

            var title = UIKit.MakeText(canvas, "Title", "<b>GLaDE</b>  ·  Statics Lab", 46, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1400, 80));
            title.color = new Color(1f, 0.85f, 0.45f);
            var desc = UIKit.MakeText(canvas, "Description", "", 26, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1300, 90));
            desc.color = new Color(0.85f, 0.88f, 0.95f);

            var container = new GameObject("Problems", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            container.SetParent(canvas.transform, false);
            container.anchorMin = new Vector2(0.5f, 1f); container.anchorMax = new Vector2(0.5f, 1f); container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = new Vector2(0, -210); container.sizeDelta = new Vector2(1000, 700);
            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18; layout.childControlHeight = false; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.UpperCenter;

            var template = UIKit.MakeButton(container, "Problem Button", "Problem", 30, new Vector2(1000, 120));
            template.GetComponent<LayoutElement>().preferredHeight = 120;

            var hub = board.AddComponent<ProblemHub>();
            hub.library = library; hub.buttonContainer = container; hub.buttonTemplate = template; hub.descriptionText = desc; hub.howToText = howText;

            EditorSceneManager.MarkSceneDirty(scene);
            string path = $"{ScenesFolder}/{HubSceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[GLaDE] Built {path}");
        }

        public static void UpdateBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in new[] { HubSceneName, "Problem_Truss", "Problem_Beam" })
            {
                string p = $"{ScenesFolder}/{name}.unity";
                if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ environment & rig

        static void BuildEnvironment(VisualTheme theme)
        {
            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(58f, 18f, 0f);   // from behind and above the player

            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, 140f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.47f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.24f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var env = new GameObject("Environment").transform;

            // Accent light over the work area and a soft mat under it: the bench reads as the place to be.
            var spot = new GameObject("Bench Light").AddComponent<Light>();
            spot.type = LightType.Spot; spot.range = 7f; spot.spotAngle = 95f; spot.intensity = 14f;
            spot.color = new Color(1f, 0.97f, 0.9f); spot.shadows = LightShadows.None;
            spot.transform.position = new Vector3(0f, 3.0f, 0.6f); spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Work Mat";
            MeshFactory.SafeDestroy(mat.GetComponent<Collider>());
            mat.transform.SetParent(env, false);
            mat.transform.position = new Vector3(0f, 0.006f, 0.35f);
            mat.transform.localScale = new Vector3(5.2f, 0.012f, 3.2f);
            mat.GetComponent<MeshRenderer>().sharedMaterial = Mat("Work Mat", new Color(0.24f, 0.27f, 0.34f), 0f, 0.25f);

            BuildLogo(env, new Vector3(0f, 2.75f, 5.44f), Quaternion.identity, 1.0f, 0.55f);   // centred high on the back wall, above the structure from the player's viewpoint

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env, false);
            floor.transform.localScale = new Vector3(1.06f, 1f, 1.06f);   // 10.6 m: inside the walls, so teleporting never lands outside the room
            floor.GetComponent<MeshRenderer>().sharedMaterial = theme.ground;
            var area = floor.AddComponent<TeleportationArea>();
            int teleportMask = InteractionLayerMask.GetMask("Teleport");
            area.interactionLayers = teleportMask != 0 ? teleportMask : (1 << 31);

            var wallMat = Mat("Wall", new Color(0.50f, 0.53f, 0.60f), 0f, 0.1f);
            var trimMat = Mat("Wall Trim", new Color(0.32f, 0.35f, 0.42f), 0f, 0.2f);
            float half = 5.5f, h = 3.2f;
            foreach (var (pos, scale) in new[]
            {
                (new Vector3(0, h / 2, half), new Vector3(half * 2, h, 0.4f)),
                (new Vector3(0, h / 2, -half), new Vector3(half * 2, h, 0.4f)),
                (new Vector3(half, h / 2, 0), new Vector3(0.4f, h, half * 2)),
                (new Vector3(-half, h / 2, 0), new Vector3(0.4f, h, half * 2)),
            })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(env, false);
                wall.transform.position = pos; wall.transform.localScale = scale;
                wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                wall.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trim.name = "Trim";
                trim.transform.SetParent(wall.transform, false);
                trim.transform.localPosition = new Vector3(0, -0.5f + 0.06f / h, 0);
                trim.transform.localScale = new Vector3(1.001f, 0.12f / h, 1.15f);
                MeshFactory.SafeDestroy(trim.GetComponent<Collider>());
                trim.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            }
        }

        /// <summary>The GLaDE logo as an unlit transparent quad (no white card), e.g. on a wall.</summary>
        static GameObject BuildLogo(Transform parent, Vector3 pos, Quaternion rot, float size, float alpha)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (tex == null) { Debug.LogWarning("[GLaDE] Logo texture missing at " + LogoPath); return null; }
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.textureType != TextureImporterType.Default))
            {
                importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            string matPath = $"{MaterialsFolder}/Logo.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GLaDE Logo";
            MeshFactory.SafeDestroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(pos, rot);
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return quad;
        }

        static UISounds AddSounds(GameObject host)
        {
            var s = host.AddComponent<UISounds>();
            s.click = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/SFX_Hover_1.wav");
            s.success = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/SFX_Click_SciFi.wav");
            s.error = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/SFX_Hover_2.wav");
            s.snap = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/SFX_Click_Mechanical.wav");
            return s;
        }

        static void BuildXRCore()
        {
            new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            var sim = new GameObject("Simulator Bootstrap").AddComponent<SimulatorBootstrap>();
            sim.simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (sim.simulatorPrefab == null) Debug.LogWarning("[GLaDE] XR Interaction Simulator prefab not found; import the XRI sample.");
        }

        static GameObject BuildRig(Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null) { Debug.LogError("[GLaDE] XR Origin prefab not found at " + RigPrefabPath); return null; }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.SetPositionAndRotation(position, rotation);
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.19f, 0.25f);
                cam.nearClipPlane = 0.03f;
            }
            return rig;
        }

        static Quaternion FaceFrom(Vector3 objectPos, Vector3 viewerPos)
        {
            Vector3 d = objectPos - viewerPos; d.y = 0;
            return Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        // ------------------------------------------------------------------ boards

        /// <summary>Frame, dark surface and a world-space canvas. Optionally a grab bar along the top so the board can be moved.</summary>
        static GameObject BuildBoardShell(VisualTheme theme, Vector3 pos, Quaternion rot, Transform parent, float width, float height, out RectTransform canvasRect, bool grabbable)
        {
            var root = new GameObject("Board");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            MeshFactory.SafeDestroy(frame.GetComponent<Collider>());
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0, 0, 0.025f);
            frame.transform.localScale = new Vector3(width + 0.06f, height + 0.06f, 0.03f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = theme.whiteboardFrame;

            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            MeshFactory.SafeDestroy(surface.GetComponent<Collider>());
            surface.transform.SetParent(root.transform, false);
            surface.transform.localPosition = new Vector3(0, 0, 0.008f);
            surface.transform.localScale = new Vector3(width, height, 0.01f);
            surface.GetComponent<MeshRenderer>().sharedMaterial = theme.whiteboardSurface;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width * 1000f, height * 1000f);
            canvasRect.localScale = Vector3.one * 0.001f;
            canvasRect.localPosition = new Vector3(0, 0, -0.004f);
            canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

            if (grabbable)
            {
                // Grab bar above the board: grab it to move the whole board out of the way (or closer).
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = "Grab Bar";
                MeshFactory.SafeDestroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(root.transform, false);
                bar.transform.localPosition = new Vector3(0, height * 0.5f + 0.07f, 0.02f);
                bar.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                bar.transform.localScale = new Vector3(0.05f, width * 0.5f, 0.05f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = theme.tokenPlaced;
                var barCol = root.AddComponent<BoxCollider>();
                barCol.center = new Vector3(0, height * 0.5f + 0.07f, 0.02f);
                barCol.size = new Vector3(width, 0.09f, 0.09f);
                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true; rb.useGravity = false;
                var grab = root.AddComponent<XRGrabInteractable>();
                grab.colliders.Clear();
                grab.colliders.Add(barCol);   // only the bar grabs the board; the paper below has its own interactable
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.useDynamicAttach = true;
                grab.throwOnDetach = false;
                grab.retainTransformParent = true;
                grab.trackRotation = true;    // tilt and turn it, not just slide it
                root.AddComponent<BoardControls>();
                var label = MakeWorldLabel(root.transform, "<size=70%>grab bar: move the board</size>", root.transform.TransformPoint(new Vector3(0, height * 0.5f + 0.14f, 0)), 0.04f, theme.dimensionColor);
                label.name = "Bar Sign";
                MeshFactory.SafeDestroy(label.GetComponent<Billboard>());
                label.transform.localRotation = Quaternion.identity;   // TMP text reads correctly from the board's front (-z) side
            }
            return root;
        }

        static Whiteboard BuildWhiteboard(VisualTheme theme, Vector3 pos, Quaternion rot, Transform parent, bool hubButton)
        {
            float w = 1.6f, h = 1.25f;
            var root = BuildBoardShell(theme, pos, rot, parent, w, h, out var canvas, true);
            root.name = "Whiteboard";
            var wb = root.AddComponent<Whiteboard>();
            wb.controls = root.GetComponent<BoardControls>();
            wb.sounds = AddSounds(root);

            float W = w * 1000f, H = h * 1000f, margin = 36f;
            float cw = W - 2 * margin;
            float y = -margin;
            wb.titleText = UIKit.MakeText(canvas, "Title", "Problem", 36, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(cw, 48));
            wb.titleText.color = new Color(1f, 0.85f, 0.45f);
            y -= 52;
            wb.promptText = UIKit.MakeText(canvas, "Prompt", "", 22, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(cw, 128));
            wb.promptText.color = new Color(0.9f, 0.92f, 0.96f);
            wb.promptText.enableAutoSizing = true; wb.promptText.fontSizeMin = 15; wb.promptText.fontSizeMax = 22;
            y -= 134;

            var phaseBg = UIKit.MakePanel(canvas, "Phase Panel", new Color(0.18f, 0.22f, 0.32f, 0.9f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(cw, 124));
            wb.phaseText = UIKit.MakeText(phaseBg, "Phase", "", 21, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(cw - 30, 90));
            wb.phaseText.color = new Color(0.92f, 0.95f, 1f);
            wb.phaseText.enableAutoSizing = true; wb.phaseText.fontSizeMin = 14; wb.phaseText.fontSizeMax = 21;
            wb.progressText = UIKit.MakeText(phaseBg, "Progress", "", 19, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-14, 6), new Vector2(420, 28));
            wb.progressText.color = new Color(0.6f, 0.95f, 0.75f);
            y -= 130;

            wb.feedbackText = UIKit.MakeText(canvas, "Feedback", "", 21, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(cw, 96));
            wb.feedbackText.color = new Color(1f, 0.78f, 0.4f);
            wb.feedbackText.fontStyle = FontStyles.Italic;
            wb.feedbackText.enableAutoSizing = true; wb.feedbackText.fontSizeMin = 14; wb.feedbackText.fontSizeMax = 21;
            y -= 102;

            // Content area: either the step text or the answer sheet. Ends above the button row.
            float buttonRow = 74f;
            float contentH = H - margin - (-y) - buttonRow - margin;
            var stepArea = new GameObject("Step Area", typeof(RectTransform)).GetComponent<RectTransform>();
            stepArea.SetParent(canvas, false);
            stepArea.anchorMin = new Vector2(0.5f, 1f); stepArea.anchorMax = new Vector2(0.5f, 1f); stepArea.pivot = new Vector2(0.5f, 1f);
            stepArea.anchoredPosition = new Vector2(0, y); stepArea.sizeDelta = new Vector2(cw, contentH);
            wb.stepArea = stepArea;
            wb.stepTitleText = UIKit.MakeText(stepArea, "Step Title", "", 26, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(cw, 38));
            wb.stepTitleText.color = new Color(0.6f, 0.85f, 1f);
            wb.stepBodyText = UIKit.MakeText(stepArea, "Step Body", "", 21, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(cw, contentH - 42));
            wb.stepBodyText.color = new Color(0.95f, 0.96f, 0.98f);
            wb.stepBodyText.enableAutoSizing = true; wb.stepBodyText.fontSizeMin = 12; wb.stepBodyText.fontSizeMax = 21;

            var answerRt = new GameObject("Answer Sheet", typeof(RectTransform)).GetComponent<RectTransform>();
            answerRt.SetParent(canvas, false);
            answerRt.anchorMin = new Vector2(0.5f, 1f); answerRt.anchorMax = new Vector2(0.5f, 1f); answerRt.pivot = new Vector2(0.5f, 1f);
            answerRt.anchoredPosition = new Vector2(0, y); answerRt.sizeDelta = new Vector2(cw, contentH);
            var answerPanel = answerRt.gameObject.AddComponent<AnswerPanel>();
            answerPanel.container = answerRt;
            wb.answerPanel = answerPanel;

            // Button row along the bottom
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(canvas, false);
            row.anchorMin = new Vector2(0.5f, 0f); row.anchorMax = new Vector2(0.5f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0, margin * 0.6f); row.sizeDelta = new Vector2(cw, buttonRow);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 12; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childAlignment = TextAnchor.MiddleCenter;

            wb.backToSheetButton = UIKit.MakeButton(row, "BackToSheet", "< Back to answers", 22, new Vector2(200, 70));
            wb.checkButton = UIKit.MakeButton(row, "Check", "Check answers", 22, new Vector2(200, 70), UIKit.AccentColor);
            wb.guideButton = UIKit.MakeButton(row, "Guide", "Guide me", 22, new Vector2(170, 70), UIKit.WarnColor);
            wb.backButton = UIKit.MakeButton(row, "Back", "< Back", 22, new Vector2(140, 70));
            wb.nextButton = UIKit.MakeButton(row, "Next", "Next step >", 22, new Vector2(190, 70), UIKit.AccentColor);
            wb.hintButton = UIKit.MakeButton(row, "Hint", "Hint", 22, new Vector2(130, 70));
            wb.skipButton = UIKit.MakeButton(row, "Skip", "Show me how", 22, new Vector2(190, 70), UIKit.WarnColor);
            wb.reviewButton = UIKit.MakeButton(row, "Review", "Read solution", 22, new Vector2(190, 70));
            wb.resetButton = UIKit.MakeButton(row, "Reset", "Start over", 22, new Vector2(150, 70));
            wb.newProblemButton = UIKit.MakeButton(row, "New", "New problem", 22, new Vector2(190, 70));
            if (hubButton)
            {
                var hub = UIKit.MakeButton(row, "Problems", "Problems", 22, new Vector2(150, 70));
                var back = root.AddComponent<ReturnToHubButton>();
                UnityEventTools.AddPersistentListener(hub.onClick, back.Go);
            }

            // Board utilities in the top-right corner: face me, bigger/smaller.
            var utilRow = new GameObject("Utilities", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            utilRow.SetParent(canvas, false);
            utilRow.anchorMin = new Vector2(1f, 1f); utilRow.anchorMax = new Vector2(1f, 1f); utilRow.pivot = new Vector2(1f, 1f);
            utilRow.anchoredPosition = new Vector2(-margin, -margin + 6); utilRow.sizeDelta = new Vector2(260, 44);
            var uhl = utilRow.GetComponent<HorizontalLayoutGroup>();
            uhl.spacing = 8; uhl.childControlWidth = true; uhl.childControlHeight = true; uhl.childForceExpandWidth = true;
            wb.faceButton = UIKit.MakeButton(utilRow, "Face", "Face me", 18, new Vector2(120, 44));
            wb.sizeButton = UIKit.MakeButton(utilRow, "Size", "Bigger", 18, new Vector2(120, 44));
            wb.titleText.rectTransform.sizeDelta = new Vector2(cw - 280, 48);
            return wb;
        }
    }
}

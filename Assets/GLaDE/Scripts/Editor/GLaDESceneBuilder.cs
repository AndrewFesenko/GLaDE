using System.Collections.Generic;
using System.Linq;
using GLaDE.Core;
using GLaDE.Problems;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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
        public const string HubSceneName = "Hub";

        static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -1.0f);
        static readonly Vector3 StructurePos = new Vector3(0f, 1.05f, 0.75f);
        static readonly Vector3 BoardPos = new Vector3(1.55f, 1.45f, 0.35f);
        static readonly Vector3 TablePos = new Vector3(-1.25f, 0f, 0.35f);
        static readonly Vector3 PlanePos = new Vector3(-0.85f, 1.35f, 0.35f);

        [MenuItem("GLaDE/Build Everything", priority = 0)]
        public static void BuildAll()
        {
            var theme = CreateTheme();
            ProblemAssetFactory.CreateAll();
            var truss = AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.TrussAssetPath);
            var beam = AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.BeamAssetPath);
            BuildProblemScene(truss, theme);
            BuildProblemScene(beam, theme);
            BuildHubScene(theme);
            UpdateBuildSettings();
            Debug.Log("[GLaDE] Build Everything finished.");
        }

        [MenuItem("GLaDE/Build Truss Scene", priority = 1)]
        public static void BuildTruss() => BuildProblemScene(AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.TrussAssetPath), CreateTheme());

        [MenuItem("GLaDE/Build Beam Scene", priority = 2)]
        public static void BuildBeam() => BuildProblemScene(AssetDatabase.LoadAssetAtPath<StaticsProblem>(ProblemAssetFactory.BeamAssetPath), CreateTheme());

        [MenuItem("GLaDE/Build Hub Scene", priority = 3)]
        public static void BuildHub() => BuildHubScene(CreateTheme());

        // ------------------------------------------------------------------ materials & theme

        public static VisualTheme CreateTheme()
        {
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

        public static void BuildProblemScene(StaticsProblem problem, VisualTheme theme)
        {
            if (problem == null) { Debug.LogError("[GLaDE] Problem asset missing; run Create Problem Assets first."); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEnvironment(theme);
            BuildXRCore();
            var rig = BuildRig(PlayerStart, Quaternion.identity);

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

            // Token table
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Token Table";
            table.transform.SetParent(problemGo.transform, false);
            table.transform.position = TablePos + new Vector3(0, 0.42f, 0);
            table.transform.localScale = new Vector3(1.0f, 0.84f, 0.42f);
            table.GetComponent<MeshRenderer>().sharedMaterial = theme.stand;
            var rack = new GameObject("Token Rack").transform;
            rack.SetParent(problemGo.transform, false);
            rack.position = TablePos + new Vector3(0, 0.86f, 0);
            pm.tokenRack = rack;

            if (problem.kind == StructureKind.Truss)
            {
                var plane = SectionPlane.Create(theme, sv, problemGo.transform, PlanePos, Quaternion.Euler(0, 90, 0));
                pm.sectionPlane = plane;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string path = $"{ScenesFolder}/{problem.sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[GLaDE] Built {path}");
        }

        public static void BuildHubScene(VisualTheme theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEnvironment(theme);
            BuildXRCore();
            BuildRig(PlayerStart, Quaternion.identity);

            var library = AssetDatabase.LoadAssetAtPath<ProblemLibrary>(ProblemAssetFactory.LibraryPath);
            Vector3 boardPos = new Vector3(0f, 1.5f, 1.2f);
            var board = BuildBoardShell(theme, boardPos, FaceFrom(boardPos, PlayerStart + Vector3.up * 1.5f), null, 1.5f, 1.1f, out var canvas);
            board.name = "Hub Board";

            var title = MakeText(canvas, "Title", "<b>GLaDE</b>  ·  Statics Lab", 46, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1400, 80));
            title.color = new Color(1f, 0.85f, 0.45f);
            var desc = MakeText(canvas, "Description", "", 26, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1300, 90));
            desc.color = new Color(0.85f, 0.88f, 0.95f);

            var container = new GameObject("Problems", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            container.SetParent(canvas.transform, false);
            container.anchorMin = new Vector2(0.5f, 1f); container.anchorMax = new Vector2(0.5f, 1f); container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = new Vector2(0, -210); container.sizeDelta = new Vector2(1000, 700);
            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18; layout.childControlHeight = false; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.UpperCenter;

            var template = MakeButton(container, "Problem Button", "Problem", 30, new Vector2(1000, 120));
            template.GetComponent<LayoutElement>().preferredHeight = 120;

            var hub = board.AddComponent<ProblemHub>();
            hub.library = library; hub.buttonContainer = container; hub.buttonTemplate = template; hub.descriptionText = desc;

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
            foreach (var legacy in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/HomeScreen.unity", "Assets/Scenes/AboutGlade.unity" })
                if (System.IO.File.Exists(legacy)) list.Add(new EditorBuildSettingsScene(legacy, true));
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
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

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

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env, false);
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = theme.ground;
            var area = floor.AddComponent<TeleportationArea>();
            int teleportMask = InteractionLayerMask.GetMask("Teleport");
            area.interactionLayers = teleportMask != 0 ? teleportMask : (1 << 31);

            var wallMat = Mat("Wall", new Color(0.50f, 0.53f, 0.60f), 0f, 0.1f);
            var trimMat = Mat("Wall Trim", new Color(0.32f, 0.35f, 0.42f), 0f, 0.2f);
            float half = 5.5f, h = 3.2f;
            foreach (var (pos, scale) in new[]
            {
                (new Vector3(0, h / 2, half), new Vector3(half * 2, h, 0.1f)),
                (new Vector3(0, h / 2, -half), new Vector3(half * 2, h, 0.1f)),
                (new Vector3(half, h / 2, 0), new Vector3(0.1f, h, half * 2)),
                (new Vector3(-half, h / 2, 0), new Vector3(0.1f, h, half * 2)),
            })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(env, false);
                wall.transform.position = pos; wall.transform.localScale = scale;
                wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trim.name = "Trim";
                trim.transform.SetParent(wall.transform, false);
                trim.transform.localPosition = new Vector3(0, -0.5f + 0.06f / h, 0);
                trim.transform.localScale = new Vector3(1.001f, 0.12f / h, 1.6f);
                MeshFactory.SafeDestroy(trim.GetComponent<Collider>());
                trim.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            }
        }

        static void BuildXRCore()
        {
            new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
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

        // ------------------------------------------------------------------ whiteboard UI

        static GameObject BuildBoardShell(VisualTheme theme, Vector3 pos, Quaternion rot, Transform parent, float width, float height, out RectTransform canvasRect)
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
            return root;
        }

        static Whiteboard BuildWhiteboard(VisualTheme theme, Vector3 pos, Quaternion rot, Transform parent, bool hubButton)
        {
            float w = 1.5f, h = 1.1f;
            var root = BuildBoardShell(theme, pos, rot, parent, w, h, out var canvas);
            root.name = "Whiteboard";
            var wb = root.AddComponent<Whiteboard>();

            float W = w * 1000f, H = h * 1000f, margin = 40f;
            float y = -margin;
            wb.titleText = MakeText(canvas, "Title", "Problem", 38, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, 56)); y -= 60;
            wb.titleText.color = new Color(1f, 0.85f, 0.45f);
            wb.promptText = MakeText(canvas, "Prompt", "", 23, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, 150)); y -= 158;
            wb.promptText.color = new Color(0.9f, 0.92f, 0.96f);

            var phaseBg = MakePanel(canvas, "Phase Panel", new Color(0.18f, 0.22f, 0.32f, 0.9f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, 118));
            wb.phaseText = MakeText(phaseBg, "Phase", "", 22, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(W - 2 * margin - 30, 100));
            wb.phaseText.color = new Color(0.92f, 0.95f, 1f);
            wb.progressText = MakeText(phaseBg, "Progress", "", 20, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-14, 6), new Vector2(420, 30));
            wb.progressText.color = new Color(0.6f, 0.95f, 0.75f);
            y -= 126;

            wb.feedbackText = MakeText(canvas, "Feedback", "", 22, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, 84)); y -= 90;
            wb.feedbackText.color = new Color(1f, 0.78f, 0.4f);
            wb.feedbackText.fontStyle = FontStyles.Italic;

            wb.stepTitleText = MakeText(canvas, "Step Title", "", 27, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, 40)); y -= 46;
            wb.stepTitleText.color = new Color(0.6f, 0.85f, 1f);
            float bodyH = H - margin - (-y) - 110;
            wb.stepBodyText = MakeText(canvas, "Step Body", "", 22, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 2 * margin, bodyH));
            wb.stepBodyText.color = new Color(0.95f, 0.96f, 0.98f);

            // Button row along the bottom
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(canvas, false);
            row.anchorMin = new Vector2(0.5f, 0f); row.anchorMax = new Vector2(0.5f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0, margin * 0.6f); row.sizeDelta = new Vector2(W - 2 * margin, 74);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 12; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childAlignment = TextAnchor.MiddleCenter;

            wb.backButton = MakeButton(row, "Back", "◀ Back", 22, new Vector2(150, 70));
            wb.nextButton = MakeButton(row, "Next", "Next step ▶", 22, new Vector2(190, 70));
            wb.nextButton.image.color = new Color(0.20f, 0.55f, 0.40f);
            wb.hintButton = MakeButton(row, "Hint", "Hint", 22, new Vector2(140, 70));
            wb.skipButton = MakeButton(row, "Skip", "Show me how", 22, new Vector2(190, 70));
            wb.skipButton.image.color = new Color(0.55f, 0.40f, 0.20f);
            wb.resetButton = MakeButton(row, "Reset", "Reset", 22, new Vector2(140, 70));
            wb.newProblemButton = MakeButton(row, "New", "New problem", 22, new Vector2(190, 70));
            if (hubButton)
            {
                var hub = MakeButton(row, "Problems", "Problems", 22, new Vector2(160, 70));
                var back = root.AddComponent<ReturnToHubButton>();
                UnityEventTools.AddPersistentListener(hub.onClick, back.Go);
            }
            return wb;
        }

        static RectTransform MakePanel(RectTransform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : (anchor.y <= 0f ? 0f : 0.5f));
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button MakeButton(RectTransform parent, string name, string label, float fontSize, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<LayoutElement>().preferredWidth = size.x;
            go.GetComponent<LayoutElement>().preferredHeight = size.y;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.24f, 0.28f, 0.40f);
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var text = MakeText(rt, "Label", label, fontSize, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12, 8));
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(6, 4); text.rectTransform.offsetMax = new Vector2(-6, -4);
            text.color = Color.white;
            return btn;
        }
    }
}

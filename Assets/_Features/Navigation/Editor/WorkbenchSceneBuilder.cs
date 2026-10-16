using System.IO;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public static class WorkbenchSceneBuilder
{
    private const string Root = "Assets/_Features/Navigation";
    private static Material floor;
    private static Material wall;
    private static Material dark;
    private static Material white;
    private static Material blue;
    private static Material red;
    private static Material teal;
    private static Material particle;
    private static TMP_FontAsset heading;
    private static TMP_FontAsset text;
    private static Mesh particleMesh;

    public static void SetGameView(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance").GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.ToObject(groupType, 0) });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var fixedType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = System.Activator.CreateInstance(sizeType, new object[] { System.Enum.ToObject(fixedType, 1), width, height, "Workbench " + width + "x" + height });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex").SetValue(view, count - 1);
        view.Repaint();
    }

    [MenuItem("Tools/Navigation/Build Workbench")]
    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Останови Play Mode перед сборкой сцены";
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty && !string.IsNullOrEmpty(current.path)) EditorSceneManager.SaveScene(current);
        Directory.CreateDirectory(Root + "/Data"); Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Root + "/Audio");
        AssetDatabase.Refresh();
        PrepareMaterials(); PrepareFonts();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var settings = AssetDatabase.LoadAssetAtPath<WorkbenchSettings>(Root + "/Data/WorkbenchSettings.asset");
        if (settings == null) { settings = ScriptableObject.CreateInstance<WorkbenchSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Data/WorkbenchSettings.asset"); }
        LoadAudio(settings);
        settings.BounceHeight = 0.045f; settings.SwayAngle = 5;
        SetLayer(8, "Ground"); SetLayer(9, "Obstacle"); SetLayer(10, "Actors"); SetLayer(11, "Markers");
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.64f, 0.71f, 0.78f);
        RenderSettings.skybox = null; RenderSettings.fog = false;
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.7f;
        sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        var rig = new GameObject("CameraRig").transform; rig.position = new Vector3(4.5f, 26, -18.5f);
        var camera = new GameObject("MainCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.SetParent(rig, false); camera.transform.localRotation = Quaternion.Euler(55, 0, 0);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.75f, 0.83f, 0.86f);
        camera.fieldOfView = 51; camera.nearClipPlane = 0.1f; camera.farClipPlane = 150;
        camera.gameObject.AddComponent<AudioListener>();
        var arena = new GameObject("Arena").transform;
        Cube("Ground", arena, new Vector3(0, -0.28f, 0), new Vector3(28, 0.55f, 22), floor, 8);
        var surface = arena.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = (1 << 8) | (1 << 9); surface.overrideTileSize = true; surface.tileSize = 64;
        surface.overrideVoxelSize = true; surface.voxelSize = 0.12f;
        var navigationSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var type = navigationSettings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
        type.FindPropertyRelative("agentRadius").floatValue = 0.42f;
        type.FindPropertyRelative("agentHeight").floatValue = 1.9f;
        type.FindPropertyRelative("agentClimb").floatValue = 0.3f;
        navigationSettings.ApplyModifiedProperties();
        Border(arena);
        for (int i = -13; i <= 13; i++) Cube("GridX", arena, new Vector3(i, 0.009f, 0), new Vector3(0.018f, 0.009f, 21), white);
        for (int i = -10; i <= 10; i++) Cube("GridZ", arena, new Vector3(0, 0.011f, i), new Vector3(27, 0.009f, 0.018f), white);
        var structure = new GameObject("FixedObstacles").transform; structure.SetParent(arena);
        SolidWall("WestDivider", structure, new Vector3(-5, 0, 1.5f), new Vector3(1.3f, 2.1f, 9));
        SolidWall("EastDivider", structure, new Vector3(4, 0, -1.5f), new Vector3(1.3f, 2.1f, 9));
        SolidWall("NorthCover", structure, new Vector3(0, 0, 6.8f), new Vector3(5, 1.6f, 1.2f));
        SolidWall("SouthCover", structure, new Vector3(0, 0, -6.8f), new Vector3(5, 1.6f, 1.2f));
        SolidWall("WestCover", structure, new Vector3(-9, 0, -2), new Vector3(2, 1.8f, 1.2f));
        SolidWall("EastCover", structure, new Vector3(9, 0, 2), new Vector3(2, 1.8f, 1.2f));
        var objectRoot = new GameObject("PlacedObjects").transform;
        GameObject block = ObstaclePrefab("Block", new Vector3(1.3f, 2, 1.3f));
        GameObject longWall = ObstaclePrefab("Wall", new Vector3(1.3f, 2, 4.2f));
        GameObject point = PointPrefab();
        var previews = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            previews[i] = Cube(i == 0 ? "BlockPreview" : "WallPreview", null, Vector3.zero, i == 0 ? new Vector3(1.3f, 2, 1.3f) : new Vector3(1.3f, 2, 4.2f), MaterialAsset("Preview", new Color(0.18f, 0.8f, 0.8f, 0.35f), true)).transform;
            previews[i].position = Vector3.up; previews[i].gameObject.SetActive(false);
        }
        var actorRoot = new GameObject("Agent"); actorRoot.transform.position = new Vector3(-11, 0, -7); actorRoot.layer = 10;
        var actorAgent = AddAgent(actorRoot, settings.Speed);
        var body = CapsuleBody(actorRoot.transform, blue);
        var actorFeet = Feet(actorRoot.transform);
        var dust = Dust("StepDust", actorRoot.transform, Color.white, false);
        var actor = actorRoot.AddComponent<WorkbenchActor>(); actor.Configure(settings, actorAgent, body, dust);
        actor.ConfigureFeet(actorFeet[0], actorFeet[1]);
        var healthBar = new GameObject("HealthBar").transform; healthBar.SetParent(actorRoot.transform); healthBar.localPosition = new Vector3(0, 2.5f, 0);
        Cube("HealthBackground", healthBar, Vector3.zero, new Vector3(1.25f, 0.11f, 0.12f), dark);
        var fill = Cube("HealthFill", healthBar, Vector3.zero, new Vector3(1.15f, 0.13f, 0.14f), teal).transform;
        actor.ConfigureHealthBar(healthBar, fill);
        var enemies = new WorkbenchEnemy[2];
        Vector3[][] patrols = { new[] { new Vector3(-2, 0, -3), new Vector3(-2, 0, 3) }, new[] { new Vector3(7, 0, -5), new Vector3(7, 0, 5) } };
        for (int i = 0; i < enemies.Length; i++)
        {
            var go = new GameObject("Guard" + (i + 1)); go.layer = 10; go.transform.position = patrols[i][0];
            var navigation = AddAgent(go, settings.EnemySpeed); var visual = CapsuleBody(go.transform, red);
            var enemy = go.AddComponent<WorkbenchEnemy>(); enemy.Configure(settings, navigation, visual, actor, patrols[i], 1 << 9);
            var feet = Feet(go.transform); enemy.ConfigureFeet(feet[0], feet[1]);
            enemy.ConfigureDust(Dust("GuardDust", go.transform, Color.white, false));
            var sight = new GameObject("DangerZone"); sight.transform.SetParent(go.transform, false);
            var sightMesh = sight.AddComponent<MeshFilter>(); sight.AddComponent<MeshRenderer>().sharedMaterial = MaterialAsset("DangerZone", new Color(1, 0.17f, 0.14f, 0.15f), true);
            var stateLabel = new GameObject("Awareness").AddComponent<TextMeshPro>(); stateLabel.transform.SetParent(go.transform, false); stateLabel.transform.localPosition = Vector3.up * 2.7f;
            stateLabel.font = text; stateLabel.fontSize = 3.6f; stateLabel.alignment = TextAlignmentOptions.Center; stateLabel.rectTransform.sizeDelta = new Vector2(3.5f, 0.8f); stateLabel.text = "Патруль";
            var boundary = sight.AddComponent<LineRenderer>(); boundary.useWorldSpace = false; boundary.widthMultiplier = 0.04f; boundary.sharedMaterial = MaterialAsset("SightEdge", Color.white, true);
            go.AddComponent<WorkbenchThreatView>().Configure(enemy, settings, sightMesh, 1 << 9, stateLabel, camera, boundary);
            enemies[i] = enemy; go.SetActive(false);
        }
        var lines = new LineRenderer[3];
        Color[] colors = { settings.AgentColor, settings.SafeColor, settings.HiddenColor };
        for (int i = 0; i < lines.Length; i++)
        {
            var line = new GameObject("Route" + (RouteStrategy)i).AddComponent<LineRenderer>();
            line.widthMultiplier = 0.16f; line.numCapVertices = 4; line.numCornerVertices = 4;
            line.material = RouteMaterial(i, colors[i]); line.textureMode = LineTextureMode.Tile;
            line.startColor = Color.white; line.endColor = Color.white; line.positionCount = 0; lines[i] = line;
        }
        var meshView = Overlay("NavMeshOverlay", new Color(0.08f, 0.6f, 1, 0.22f));
        var hiddenView = Overlay("CoverOverlay", new Color(0.1f, 0.85f, 0.45f, 0.28f));
        var bench = new GameObject("NavigationWorkbench").AddComponent<NavMeshWorkbench>(); bench.gameObject.AddComponent<AudioSource>();
        bench.Configure(settings, surface, actor, enemies, camera, objectRoot, new[] { block, longWall }, point, previews, lines, meshView, hiddenView, 1 << 9);
        var fx = new GameObject("Effects").AddComponent<WorkbenchEffects>();
        fx.Configure(camera, Dust("Burst", fx.transform, Color.white, false), FragmentPrefab()); fx.ConfigureSettings(settings);
        Ambient(arena);
        WorkbenchHudBuilder.Build(bench, heading, text);
        Physics.SyncTransforms(); surface.BuildNavMesh();
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(Root + "/Data/WorkbenchNavMesh.asset") != null) AssetDatabase.DeleteAsset(Root + "/Data/WorkbenchNavMesh.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, Root + "/Data/WorkbenchNavMesh.asset");
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NavigationWorkbench.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/NavigationWorkbench.unity", true) };
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        WorkbenchPresentationPatch.Apply();
        WorkbenchGlowPatch.Apply();

        return "NavigationWorkbench scene saved";
    }

    private static void SetLayer(int index, string value)
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = value; tags.ApplyModifiedProperties();
    }

    private static void PrepareMaterials()
    {
        floor = MaterialAsset("Floor", new Color(0.83f, 0.88f, 0.89f));
        wall = MaterialAsset("Obstacles", new Color(0.95f, 0.74f, 0.35f));
        dark = MaterialAsset("Graphite", new Color(0.15f, 0.22f, 0.28f));
        white = MaterialAsset("Markings", new Color(0.73f, 0.8f, 0.82f));
        blue = MaterialAsset("Agent", new Color(0.15f, 0.64f, 0.95f));
        red = MaterialAsset("Guard", new Color(0.95f, 0.27f, 0.25f));
        teal = MaterialAsset("Teal", new Color(0.2f, 0.85f, 0.7f));
        particle = MaterialAsset("Particles", Color.white, true);
        particle.shader = Shader.Find("Workbench/VolumeParticles");
        particleMesh = ParticleMesh();
    }

    private static Material MaterialAsset(string name, Color color, bool transparent = false)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Workbench/Toon");
        if (shader == null) throw new System.InvalidOperationException("Не найден шейдер " + name);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetColor("_BaseColor", color);
        if (transparent)
        {
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
        }
        EditorUtility.SetDirty(material);

        return material;
    }

    private static void PrepareFonts()
    {
        heading = Font("Unbounded"); text = Font("Roboto Bold");
    }

    private static TMP_FontAsset Font(string name)
    {
        string path = Root + "/Fonts/" + name + " SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;
        string sourceName = name == "Roboto Bold" ? "Roboto-Bold" : name;
        var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(Root + "/Fonts/" + sourceName + ".ttf");
        if (source == null) throw new System.InvalidOperationException("Не загружен шрифт " + name);
        font = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        font.name = name + " SDF"; AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font); AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя .,:;!?+-/()[]<>=%");
        EditorUtility.SetDirty(font);

        return font;
    }

    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, int layer = 0)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = layer;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (layer == 0) Object.DestroyImmediate(go.GetComponent<Collider>());

        return go;
    }

    private static GameObject SolidWall(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position; root.layer = 9;
        var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, size.y / 2, 0); collider.size = size;
        Cube("Body", root.transform, Vector3.up * size.y / 2, size, wall);
        Cube("TopStripe", root.transform, Vector3.up * (size.y + 0.025f), new Vector3(size.x * 0.75f, 0.035f, size.z * 0.82f), dark);
        var modifier = root.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1;

        return root;
    }

    private static GameObject ObstaclePrefab(string name, Vector3 size)
    {
        var root = new GameObject(name); root.layer = 9;
        var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up; collider.size = size;
        var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
        Cube("Body", visual, Vector3.up, size, wall);
        Cube("Cap", visual, Vector3.up * 2.02f, new Vector3(size.x * 0.7f, 0.035f, size.z * 0.85f), dark);
        var modifier = root.AddComponent<NavMeshModifier>(); modifier.overrideArea = true; modifier.area = 1;
        root.AddComponent<WorkbenchItem>().Configure(visual, null, null);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + name + ".prefab"); Object.DestroyImmediate(root);

        return prefab;
    }

    private static GameObject PointPrefab()
    {
        var root = new GameObject("Point"); root.layer = 11;
        var collider = root.AddComponent<SphereCollider>(); collider.center = Vector3.up * 0.3f; collider.radius = 0.55f;
        var model = new GameObject("Visual").transform; model.SetParent(root.transform, false);
        var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disk.name = "Ring"; disk.transform.SetParent(model); disk.transform.localPosition = Vector3.up * 0.06f; disk.transform.localScale = new Vector3(0.9f, 0.035f, 0.9f); Object.DestroyImmediate(disk.GetComponent<Collider>());
        disk.GetComponent<Renderer>().sharedMaterial = teal;
        var label = new GameObject("Number").AddComponent<TextMeshPro>(); label.transform.SetParent(model, false); label.transform.localPosition = new Vector3(0, 0.6f, 0); label.transform.rotation = Quaternion.Euler(55, 0, 0); label.font = text;
        label.text = "1"; label.fontSize = 4; label.color = new Color(0.1f, 0.2f, 0.28f); label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(1.2f, 1);
        root.AddComponent<WorkbenchItem>().Configure(model, label, disk.GetComponent<Renderer>());
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Point.prefab"); Object.DestroyImmediate(root);

        return prefab;
    }

    private static NavMeshAgent AddAgent(GameObject go, float speed)
    {
        var agent = go.AddComponent<NavMeshAgent>(); agent.radius = 0.42f; agent.height = 1.9f; agent.speed = speed; agent.acceleration = 16; agent.angularSpeed = 540; agent.stoppingDistance = 0.12f; agent.baseOffset = 0;

        return agent;
    }

    private static Transform CapsuleBody(Transform parent, Material color)
    {
        var root = new GameObject("Body").transform; root.SetParent(parent, false);
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule); capsule.transform.SetParent(root, false); capsule.name = "Capsule"; capsule.transform.localPosition = Vector3.up; capsule.transform.localScale = new Vector3(0.85f, 1, 0.85f); capsule.GetComponent<Renderer>().sharedMaterial = color; Object.DestroyImmediate(capsule.GetComponent<Collider>());
        for (int i = 0; i < 2; i++)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere); eye.name = "Eye" + i; eye.transform.SetParent(root, false); eye.transform.localPosition = new Vector3(i == 0 ? -0.19f : 0.19f, 1.45f, 0.36f); eye.transform.localScale = new Vector3(0.28f, 0.36f, 0.15f); eye.GetComponent<Renderer>().sharedMaterial = floor; Object.DestroyImmediate(eye.GetComponent<Collider>());
            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere); pupil.name = "Pupil"; pupil.transform.SetParent(eye.transform, false); pupil.transform.localPosition = new Vector3(0, 0, 0.5f); pupil.transform.localScale = new Vector3(0.45f, 0.5f, 0.5f); pupil.GetComponent<Renderer>().sharedMaterial = dark; Object.DestroyImmediate(pupil.GetComponent<Collider>());
        }

        return root;
    }

    private static Transform[] Feet(Transform parent)
    {
        var feet = new Transform[2];
        for (int i = 0; i < 2; i++) feet[i] = Cube(i == 0 ? "LeftFoot" : "RightFoot", parent, new Vector3(i == 0 ? -0.27f : 0.27f, 0.1f, 0.05f), new Vector3(0.3f, 0.18f, 0.48f), dark).transform;

        return feet;
    }

    private static ParticleSystem Dust(string name, Transform parent, Color color, bool ambient)
    {
        var system = new GameObject(name).AddComponent<ParticleSystem>(); system.transform.SetParent(parent, false); system.transform.localPosition = Vector3.up * 0.15f;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = ambient ? 5 : 0.5f; main.startSpeed = ambient ? 0.15f : 0.8f; main.startSize = ambient ? 0.045f : 0.16f; main.startColor = color; main.gravityModifier = ambient ? -0.015f : 0.2f; main.maxParticles = ambient ? 250 : 300;
        var emission = system.emission; emission.enabled = ambient; emission.rateOverTime = ambient ? 10 : 0;
        var shape = system.shape; shape.shapeType = ambient ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Sphere; shape.radius = 0.2f; shape.scale = ambient ? new Vector3(26, 6, 20) : Vector3.one;
        var fade = system.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0, 1) }); fade.color = gradient;
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.18f, 1), new Keyframe(0.65f, 0.8f), new Keyframe(1, 0)));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = particleMesh; renderer.sharedMaterial = particle;

        return system;
    }

    private static Mesh ParticleMesh()
    {
        string path = Root + "/Data/ParticleVolume.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        Vector3[] corners = { Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.left * 0.5f, Vector3.right * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f };
        int[] faces = { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
        var vertices = new Vector3[faces.Length]; var indices = new int[faces.Length];
        for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]]; indices[i] = i; }
        mesh = new Mesh { name = "ParticleVolume", vertices = vertices, triangles = indices }; mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);

        return mesh;
    }

    private static void Ambient(Transform arena)
    {
        var motes = Dust("AirDust", arena, new Color(1, 1, 0.9f, 0.5f), true); motes.transform.localPosition = Vector3.up * 3;
        for (int i = 0; i < 4; i++)
        {
            var position = new Vector3(i < 2 ? -12 : 12, 0.06f, i % 2 == 0 ? -8 : 8);
            Cube("Vent", arena, position, new Vector3(1.5f, 0.1f, 1.5f), dark);
            for (int j = 0; j < 5; j++) Cube("VentSlat", arena, position + new Vector3((j - 2) * 0.22f, 0.09f, 0), new Vector3(0.07f, 0.06f, 1.2f), white);
            var steam = Dust("Steam", arena, new Color(0.85f, 0.92f, 1, 0.2f), false); steam.transform.localPosition = position;
            var main = steam.main; main.startLifetime = 2; main.startSize = 0.3f; main.startSpeed = 0.5f; main.gravityModifier = -0.05f;
            var emission = steam.emission; emission.enabled = true; emission.rateOverTime = 1.5f;
        }
    }

    private static void Border(Transform arena)
    {
        Cube("WestRim", arena, new Vector3(-14.25f, 0, 0), new Vector3(0.5f, 0.7f, 23), dark, 9);
        Cube("EastRim", arena, new Vector3(14.25f, 0, 0), new Vector3(0.5f, 0.7f, 23), dark, 9);
        Cube("NorthRim", arena, new Vector3(0, 0, 11.25f), new Vector3(28, 0.7f, 0.5f), dark, 9);
        Cube("SouthRim", arena, new Vector3(0, 0, -11.25f), new Vector3(28, 0.7f, 0.5f), dark, 9);
        for (int i = -12; i <= 12; i += 4) Cube("EdgeLight", arena, new Vector3(i, 0.38f, -11.25f), new Vector3(0.7f, 0.04f, 0.12f), teal);
    }

    private static MeshFilter Overlay(string name, Color color)
    {
        var go = new GameObject(name); var filter = go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>().sharedMaterial = MaterialAsset(name, color, true); go.SetActive(false);

        return filter;
    }

    private static Material RouteMaterial(int index, Color color)
    {
        var material = MaterialAsset("Route" + index, color, true);
        var texture = new Texture2D(32, 4) { name = "RoutePattern", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int x = 0; x < 32; x++) for (int y = 0; y < 4; y++) texture.SetPixel(x, y, new Color(1, 1, 1, index == 0 || x < (index == 1 ? 13 : 22) ? 1 : 0));
        texture.Apply(); string path = Root + "/Materials/RoutePattern" + index + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(texture, path); material.SetTexture("_BaseMap", texture);

        return material;
    }

    private static GameObject FragmentPrefab()
    {
        var go = Cube("Fragment", null, Vector3.zero, Vector3.one * 0.3f, blue, 10); go.AddComponent<Rigidbody>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Fragment.prefab"); Object.DestroyImmediate(go);

        return prefab;
    }

    private static void LoadAudio(WorkbenchSettings settings)
    {
        string source = "E:/Assets/Sfx Packs/half-life2-sounds/";
        string[] inputs = { "buttons/button15.wav", "physics/concrete/concrete_impact_hard1.wav", "physics/metal/metal_box_impact_soft1.wav", "physics/body/body_medium_impact_hard1.wav", "buttons/blip1.wav", "ambient/machines/city_ventpump_loop1.wav" };
        string[] names = { "Click", "Place", "Remove", "Hit", "Arrive", "Ambience" };
        var clips = new AudioClip[inputs.Length];
        for (int i = 0; i < inputs.Length; i++)
        {
            string path = Root + "/Audio/" + names[i] + ".wav";
            if (!File.Exists(source + inputs[i])) throw new FileNotFoundException("Нет звука " + source + inputs[i]);
            File.Copy(source + inputs[i], path, true); AssetDatabase.ImportAsset(path);
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        settings.ClickSound = clips[0]; settings.PlaceSound = clips[1]; settings.RemoveSound = clips[2]; settings.HitSound = clips[3]; settings.ArriveSound = clips[4]; settings.Ambience = clips[5];
    }
}

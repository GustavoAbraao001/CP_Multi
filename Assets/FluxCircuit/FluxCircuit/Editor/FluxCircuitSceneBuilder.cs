using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the prototype arena and the network player prefab after Fusion is imported.
/// </summary>
public static class FluxCircuitSceneBuilder
{
    private const string Root = "Assets/FluxCircuit";
    private const string MaterialsPath = Root + "/Materials";
    private const string PrefabsPath = Root + "/Prefabs";
    private const string ScenePath = Root + "/Scenes/FluxCircuit_Arena.unity";
    private const string PlayerPrefabPath = PrefabsPath + "/FluxPlayer.prefab";

    [MenuItem("Tools/Flux Circuit/Build Scene and Player Prefab")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null;
        if ((sceneExists || prefabExists) && !EditorUtility.DisplayDialog(
                "Substituir arquivos do Flux Circuit?",
                "A cena ou o prefab gerado já existe. Quer recriá-los?",
                "Recriar",
                "Cancelar"))
            return;

        EnsureFolder("Assets", "FluxCircuit");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Prefabs");
        EnsureFolder(Root, "Scenes");

        Material floor = CreateMaterial("Floor", new Color(0.055f, 0.08f, 0.12f));
        Material grid = CreateMaterial("Grid", new Color(0.11f, 0.18f, 0.23f));
        Material wall = CreateMaterial("Wall", new Color(0.08f, 0.13f, 0.18f));
        Material[] gateMaterials =
        {
            CreateMaterial("Gate_01_Amber", new Color(1f, 0.55f, 0.12f), true),
            CreateMaterial("Gate_02_Cyan", new Color(0.12f, 0.85f, 1f), true),
            CreateMaterial("Gate_03_Violet", new Color(0.68f, 0.34f, 1f), true),
            CreateMaterial("Gate_04_Orange", new Color(1f, 0.28f, 0.1f), true),
            CreateMaterial("Gate_05_Green", new Color(0.22f, 1f, 0.46f), true),
            CreateMaterial("Gate_06_Red", new Color(1f, 0.12f, 0.22f), true),
            CreateMaterial("Gate_07_Blue", new Color(0.18f, 0.35f, 1f), true),
            CreateMaterial("Gate_08_Yellow", new Color(1f, 0.9f, 0.12f), true)
        };

        NetworkObject playerPrefab = CreatePlayerPrefab();
        BuildScene(floor, grid, wall, gateMaterials, playerPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Flux Circuit pronto",
            "A arena com 8 portais, o prefab FluxPlayer e o áudio foram atualizados.\n\n" +
            "Confirme FluxPlayer no Network Prefabs Inspector do Fusion e configure o App ID antes de conectar.",
            "OK");
    }

    private static NetworkObject CreatePlayerPrefab()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "FluxPlayer";
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.48f;
        controller.center = Vector3.zero;
        controller.skinWidth = 0.04f;
        controller.stepOffset = 0.25f;

        NetworkObject networkObject = player.AddComponent<NetworkObject>();
        player.AddComponent<NetworkTransform>();
        player.AddComponent<FluxCircuitPlayer>();
        Color playerColor = new Color(0.18f, 0.82f, 1f);
        player.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Player_Default", playerColor, true);

        TrailRenderer trail = player.AddComponent<TrailRenderer>();
        trail.time = 0.5f;
        trail.minVertexDistance = 0.08f;
        trail.widthMultiplier = 0.38f;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.04f);
        trail.material = CreateTrailMaterial();
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        Object.DestroyImmediate(player);
        return saved.GetComponent<NetworkObject>();
    }

    private static void BuildScene(
        Material floor,
        Material grid,
        Material wall,
        Material[] gateMaterials,
        NetworkObject playerPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObject.name = "CircuitFloor";
        floorObject.transform.position = new Vector3(0f, -0.25f, 0f);
        floorObject.transform.localScale = new Vector3(56f, 0.5f, 56f);
        floorObject.GetComponent<Renderer>().sharedMaterial = floor;

        CreateGrid(grid);
        CreateBounds(wall);
        CreateScenery(wall);

        Vector3[] gatePositions =
        {
            new Vector3(-18f, 0f, -18f),
            new Vector3(0f, 0f, -22f),
            new Vector3(18f, 0f, -18f),
            new Vector3(22f, 0f, 0f),
            new Vector3(18f, 0f, 18f),
            new Vector3(0f, 0f, 22f),
            new Vector3(-18f, 0f, 18f),
            new Vector3(-22f, 0f, 0f)
        };
        for (int i = 0; i < gatePositions.Length; i++)
            CreateGate(i, gatePositions[i], gateMaterials[i]);

        GameObject lightObject = new GameObject("KeyLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(0.73f, 0.84f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        RenderSettings.ambientLight = new Color(0.23f, 0.29f, 0.37f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 38f;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
        cameraObject.transform.position = new Vector3(0f, 52f, -43f);
        cameraObject.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        cameraObject.AddComponent<AudioListener>();

        GameObject networkSystem = new GameObject("NetworkSystem");
        networkSystem.AddComponent<NetworkRunner>();
        networkSystem.AddComponent<FluxCircuitAudio>();
        FluxCircuitLauncher launcher = networkSystem.AddComponent<FluxCircuitLauncher>();
        SerializedObject launcherObject = new SerializedObject(launcher);
        launcherObject.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        launcherObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
        bool alreadyIncluded = false;
        foreach (EditorBuildSettingsScene buildScene in currentScenes)
        {
            if (buildScene.path == ScenePath)
            {
                alreadyIncluded = true;
                break;
            }
        }

        if (!alreadyIncluded)
        {
            var updatedScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            currentScenes.CopyTo(updatedScenes, 0);
            updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updatedScenes;
        }
    }

    private static void CreateGrid(Material material)
    {
        for (int i = -24; i <= 24; i += 4)
        {
            GameObject lineX = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineX.name = "Grid_X_" + i;
            lineX.transform.position = new Vector3(i, 0.008f, 0f);
            lineX.transform.localScale = new Vector3(0.025f, 0.012f, 56f);
            lineX.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(lineX.GetComponent<BoxCollider>());

            GameObject lineZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineZ.name = "Grid_Z_" + i;
            lineZ.transform.position = new Vector3(0f, 0.008f, i);
            lineZ.transform.localScale = new Vector3(56f, 0.012f, 0.025f);
            lineZ.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(lineZ.GetComponent<BoxCollider>());
        }
    }

    private static void CreateBounds(Material material)
    {
        CreateBlock("NorthWall", new Vector3(0f, 1f, 28f), new Vector3(56f, 2f, 0.6f), material);
        CreateBlock("SouthWall", new Vector3(0f, 1f, -28f), new Vector3(56f, 2f, 0.6f), material);
        CreateBlock("EastWall", new Vector3(28f, 1f, 0f), new Vector3(0.6f, 2f, 56f), material);
        CreateBlock("WestWall", new Vector3(-28f, 1f, 0f), new Vector3(0.6f, 2f, 56f), material);
    }

    private static void CreateScenery(Material material)
    {
        Vector3[] positions =
        {
            new Vector3(-24f, 1.4f, 0f), new Vector3(24f, 1.4f, 0f),
            new Vector3(0f, 1.4f, -24f), new Vector3(0f, 1.4f, 24f),
            new Vector3(-10f, 1.4f, 0f), new Vector3(10f, 1.4f, 0f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pylon.name = "CircuitPylon_" + (i + 1);
            pylon.transform.position = positions[i];
            pylon.transform.localScale = new Vector3(0.65f, 1.4f, 0.65f);
            pylon.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private static void CreateGate(int index, Vector3 position, Material material)
    {
        GameObject gate = new GameObject("Gate_" + (index + 1).ToString("00"));
        gate.transform.position = position;

        CreateGatePart(gate.transform, "LeftPost", new Vector3(-1.45f, 1.5f, 0f), new Vector3(0.28f, 3f, 0.35f), material);
        CreateGatePart(gate.transform, "RightPost", new Vector3(1.45f, 1.5f, 0f), new Vector3(0.28f, 3f, 0.35f), material);
        CreateGatePart(gate.transform, "TopBeam", new Vector3(0f, 2.98f, 0f), new Vector3(3.18f, 0.3f, 0.35f), material);

        GameObject triggerObject = new GameObject("CheckpointTrigger");
        triggerObject.transform.SetParent(gate.transform, false);
        triggerObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 2.7f, 1.35f);
        Rigidbody triggerBody = triggerObject.AddComponent<Rigidbody>();
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        CheckpointGate checkpoint = triggerObject.AddComponent<CheckpointGate>();
        SerializedObject checkpointObject = new SerializedObject(checkpoint);
        checkpointObject.FindProperty("gateIndex").intValue = index;
        checkpointObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateGatePart(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<BoxCollider>());
    }

    private static void CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material CreateMaterial(string name, Color color, bool emissive = false)
    {
        string assetPath = MaterialsPath + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = name, color = color };
        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }

        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static Material CreateTrailMaterial()
    {
        const string assetPath = MaterialsPath + "/Player_Trail.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var material = new Material(shader) { name = "Player_Trail", color = Color.white };
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}

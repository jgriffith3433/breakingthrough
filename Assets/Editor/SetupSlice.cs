using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Slice prefab, PianoRecital prefab, and Animator from Assets/Art/Slice/Slice.fbx (Gen character3d export).
// Menu: Tools → Slice → Build Prefab. Also runs on load once the FBX exists (without rewriting the scene on load).
[InitializeOnLoad]
static class SetupSlice
{
    const string ModelPath = "Assets/Art/Slice/Slice.fbx";
    const string ControllerPath = "Assets/Art/Slice/Slice.controller";
    const string PrefabPath = "Assets/Art/Slice/Slice.prefab";
    // Runtime load for the musician and slice playtest (WebGL has no AssetDatabase).
    const string ResourcesPrefabPath = "Assets/Resources/Slice/Slice.prefab";
    const string PianoRecitalPrefabPath = "Assets/Prefabs/PianoRecital.prefab";
    const string ResourcesPianoRecitalPath = "Assets/Resources/Slice/PianoRecital.prefab";
    const string BenchMeshPath = "Assets/Art/Slice/PianoBench.asset";
    const string PianoMeshPath = "Assets/Art/Slice/Piano.asset";
    const string MusicPath = "Assets/Art/PianoPlayer/KnollPiano.wav";
    const string MaterialPath = "Assets/Art/Knoll/KnollLit.mat";
    const int MaxRetries = 8;
    static int retries;

    // Clip take names expected on the FBX (Gen export/fbx). Missing ones are skipped.
    public static readonly string[] ClipNames = {
        "piano", "standUp", "stab", "slash", "sitDown", "idle", "hurt", "defeat"
    };

    static SetupSlice()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Slice/Build Prefab")]
    static void MenuBuild()
    {
        retries = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            if (Build(force: true))
                break;
        }
        SwapScenePianoForDeploy();
    }

    public static void EnsureBuiltForDeploy()
    {
        if (!File.Exists(ModelPath))
            return;
        for (int pass = 0; pass < 3; pass++)
        {
            if (Build(force: true))
                break;
        }
        SwapScenePianoForDeploy();
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (!File.Exists(ModelPath))
            return;

        if (retries >= MaxRetries)
            return;

        if (!Build(force: false))
        {
            retries++;
            EditorApplication.delayCall += Run;
        }
    }

    static bool Build(bool force)
    {
        if (!EnsureFolder("Assets/Art/Slice") || !EnsureFolder("Assets/Resources/Slice") || !EnsureFolder("Assets/Prefabs"))
            return false;

        if (!EnsureHumanoidModel())
            return false;

        Dictionary<string, AnimationClip> clips = LoadClips();
        if (clips.Count == 0)
        {
            Debug.LogWarning("Slice.fbx has no named clips yet (piano/standUp/stab…).");
            if (!force)
                return false;
        }

        AnimatorController controller = EnsureController(clips);
        if (controller == null)
            return false;

        if (!EnsurePrefab(controller))
            return false;

        if (!EnsurePianoRecitalPrefab())
            return false;

        AssetDatabase.SaveAssets();
        Debug.Log("Slice prefab ready at " + PrefabPath + " and " + PianoRecitalPrefabPath + " (" + clips.Count + " clips).");
        return true;
    }

    static bool EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return true;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent) && parent != "Assets")
        {
            if (!EnsureFolder(parent))
                return false;
        }
        string guid = AssetDatabase.CreateFolder(parent, name);
        return !string.IsNullOrEmpty(guid);
    }

    static bool IsLoopClip(string key)
    {
        return key == "piano" || key == "idle";
    }

    static float HitNormalizedTime(string key)
    {
        switch (key)
        {
            case "stab": return 0.35f;
            case "slash": return 0.45f;
            default: return -1f;
        }
    }

    static bool EnsureHumanoidModel()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return false;

        bool ok = importer.animationType == ModelImporterAnimationType.Human
            && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
            && importer.materialImportMode != ModelImporterMaterialImportMode.None
            && ClipsConfigured(importer);

        // Keep textures at or under 2K for WebGL.
        TextureImporter[] textures = FindModelTextures(ModelPath);
        bool texturesOk = true;
        foreach (TextureImporter tex in textures)
        {
            if (tex.maxTextureSize > 2048)
            {
                tex.maxTextureSize = 2048;
                tex.SaveAndReimport();
                texturesOk = false;
            }
        }

        if (ok && texturesOk)
            return true;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        ModelImporterClipAnimation[] takes = importer.clipAnimations;
        if (takes == null || takes.Length == 0)
            takes = importer.defaultClipAnimations;
        if (takes != null && takes.Length > 0)
        {
            for (int i = 0; i < takes.Length; i++)
            {
                string key = NormalizeClipName(takes[i].name);
                bool loop = key != null && IsLoopClip(key);
                takes[i].loopTime = loop;
                takes[i].lockRootRotation = true;
                takes[i].lockRootHeightY = true;
                takes[i].lockRootPositionXZ = true;
                takes[i].keepOriginalOrientation = true;
                takes[i].keepOriginalPositionY = true;
                takes[i].keepOriginalPositionXZ = true;

                float hitNorm = key != null ? HitNormalizedTime(key) : -1f;
                if (hitNorm > 0f)
                {
                    takes[i].events = new[]
                    {
                        new AnimationEvent
                        {
                            functionName = "OnSliceKnifeHit",
                            time = hitNorm,
                            messageOptions = SendMessageOptions.DontRequireReceiver
                        }
                    };
                }
            }
            importer.clipAnimations = takes;
        }

        importer.SaveAndReimport();
        return false;
    }

    static bool ClipsConfigured(ModelImporter importer)
    {
        ModelImporterClipAnimation[] takes = importer.clipAnimations;
        if (takes == null || takes.Length == 0)
            return false;
        for (int i = 0; i < takes.Length; i++)
        {
            string key = NormalizeClipName(takes[i].name);
            if (key == null)
                continue;
            if (takes[i].loopTime != IsLoopClip(key))
                return false;
            float hitNorm = HitNormalizedTime(key);
            if (hitNorm > 0f)
            {
                bool hasEvent = false;
                if (takes[i].events != null)
                {
                    for (int e = 0; e < takes[i].events.Length; e++)
                    {
                        if (takes[i].events[e].functionName == "OnSliceKnifeHit")
                            hasEvent = true;
                    }
                }
                if (!hasEvent)
                    return false;
            }
        }
        return true;
    }

    static TextureImporter[] FindModelTextures(string modelPath)
    {
        var list = new List<TextureImporter>();
        string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
                list.Add(importer);
        }
        return list.ToArray();
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var found = new Dictionary<string, AnimationClip>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
                continue;
            string key = NormalizeClipName(clip.name);
            if (string.IsNullOrEmpty(key))
                continue;
            found[key] = clip;
        }
        return found;
    }

    static string NormalizeClipName(string raw)
    {
        string name = raw;
        int colon = name.LastIndexOf(':');
        if (colon >= 0)
            name = name.Substring(colon + 1);
        name = name.Trim();
        foreach (string want in ClipNames)
        {
            if (string.Equals(name, want, System.StringComparison.OrdinalIgnoreCase))
                return want;
        }
        string lower = name.ToLowerInvariant();
        foreach (string want in ClipNames)
        {
            if (lower == want.ToLowerInvariant() || lower.EndsWith("_" + want.ToLowerInvariant()) || lower.EndsWith("|" + want.ToLowerInvariant()))
                return want;
        }
        return null;
    }

    static AnimatorController EnsureController(Dictionary<string, AnimationClip> clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        bool hasClipParam = false;
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == "Clip")
                hasClipParam = true;
        }
        if (!hasClipParam)
            controller.AddParameter("Clip", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState pianoState = null;
        AnimatorState fallbackState = null;
        for (int i = 0; i < ClipNames.Length; i++)
        {
            string name = ClipNames[i];
            AnimationClip motion;
            if (!clips.TryGetValue(name, out motion))
                continue;

            AnimatorState state = FindState(machine, name);
            if (state == null)
                state = machine.AddState(name, new Vector3(240f, i * 48f, 0f));
            state.motion = motion;
            if (name == "piano")
                pianoState = state;
            if (fallbackState == null)
                fallbackState = state;

            AnimatorStateTransition enter = null;
            foreach (AnimatorStateTransition t in machine.anyStateTransitions)
            {
                if (t.destinationState == state)
                    enter = t;
            }
            if (enter == null)
            {
                enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.Equals, i, "Clip");
                enter.hasExitTime = false;
                enter.duration = 0.08f;
                enter.canTransitionToSelf = false;
            }
        }

        if (pianoState != null)
            machine.defaultState = pianoState;
        else if (fallbackState != null)
            machine.defaultState = fallbackState;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == name)
                return child.state;
        }
        return null;
    }

    static bool EnsurePrefab(AnimatorController controller)
    {
        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelRoot == null)
            return false;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelRoot);
        instance.name = "Slice";

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = LoadAvatar(ModelPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.gameObject.GetComponent<SliceAnimRelay>() == null)
            animator.gameObject.AddComponent<SliceAnimRelay>();

        EnsureKnifeInHand(instance.transform);

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, ResourcesPrefabPath);
        Object.DestroyImmediate(instance);
        return true;
    }

    static void EnsureKnifeInHand(Transform sliceRoot)
    {
        Transform hand = FindHandBone(sliceRoot);
        Transform existingKnife = FindKnifeTransform(sliceRoot);
        if (existingKnife != null)
        {
            existingKnife.gameObject.SetActive(true);
            if (hand != null && !existingKnife.IsChildOf(hand) && existingKnife.GetComponent<SkinnedMeshRenderer>() == null)
            {
                existingKnife.SetParent(hand, true);
            }
            return;
        }

        Transform parent = hand != null ? hand : sliceRoot;
        GameObject knifeGo = new GameObject("Knife");
        knifeGo.transform.SetParent(parent, false);
        knifeGo.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
        knifeGo.transform.localRotation = Quaternion.identity;
        knifeGo.transform.localScale = Vector3.one;

        BuildBoxPart(knifeGo.transform, "Blade", new Vector3(0f, 0.11f, 0f), new Vector3(0.025f, 0.16f, 0.012f), new Color(0.82f, 0.86f, 0.9f));
        BuildBoxPart(knifeGo.transform, "Grip", Vector3.zero, new Vector3(0.03f, 0.08f, 0.024f), new Color(0.2f, 0.16f, 0.14f));
    }

    static void BuildBoxPart(Transform parent, string partName, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider col = part.GetComponent<Collider>();
        if (col != null)
            Object.DestroyImmediate(col);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null && shader != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            rend.sharedMaterial = mat;
        }
    }

    static Transform FindHandBone(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.EndsWith("RightHand", System.StringComparison.OrdinalIgnoreCase)
                || n.EndsWith("Hand_R", System.StringComparison.OrdinalIgnoreCase)
                || n.EndsWith("Hand.R", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "Right Hand", System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }
        return null;
    }

    static Transform FindKnifeTransform(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            string n = t.name;
            if (n.IndexOf("knife", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("scalpel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return t;
            }
        }
        return null;
    }

    static bool EnsurePianoRecitalPrefab()
    {
        GameObject sliceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (sliceAsset == null)
            return false;

        Material lit = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        Mesh benchMesh = EnsureMeshAsset(BenchMeshPath, () => BuildBench(0.48f));
        Mesh pianoMesh = EnsureMeshAsset(PianoMeshPath, () => BuildPiano(0.68f));

        var root = new GameObject("PianoRecital");
        root.AddComponent<Musician>();

        AudioSource source = root.AddComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0.22f;
        source.volume = 0.45f;
        source.minDistance = 5f;
        source.maxDistance = 48f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.priority = 64;

        GameObject slice = (GameObject)PrefabUtility.InstantiatePrefab(sliceAsset);
        slice.name = "Slice";
        slice.transform.SetParent(root.transform, false);
        slice.transform.localPosition = Vector3.zero;
        slice.transform.localRotation = Quaternion.identity;
        slice.transform.localScale = Vector3.one;

        GameObject bench = MakeProp("PianoBench", benchMesh, lit, root.transform);
        bench.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        bench.transform.localRotation = Quaternion.identity;

        GameObject piano = MakeProp("Piano", pianoMesh, lit, root.transform);
        piano.transform.localPosition = new Vector3(0f, 0f, 0.10f);
        piano.transform.localRotation = Quaternion.identity;
        if (pianoMesh != null)
        {
            BoxCollider blocker = piano.AddComponent<BoxCollider>();
            blocker.center = pianoMesh.bounds.center;
            blocker.size = pianoMesh.bounds.size;
            blocker.enabled = false;
        }

        var lampObject = new GameObject("PianoLight");
        lampObject.transform.SetParent(root.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 2.1f, 0.35f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.72f, 0.42f);
        lamp.intensity = 1.8f;
        lamp.range = 8f;
        lamp.shadows = LightShadows.None;

        PrefabUtility.SaveAsPrefabAsset(root, PianoRecitalPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, ResourcesPianoRecitalPath);
        Object.DestroyImmediate(root);
        return true;
    }

    static Mesh EnsureMeshAsset(string assetPath, System.Func<Mesh> builder)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        Mesh built = builder();
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, assetPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        }
        existing.Clear();
        existing.vertices = built.vertices;
        existing.colors = built.colors;
        existing.triangles = built.triangles;
        existing.normals = built.normals;
        existing.bounds = built.bounds;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(built);
        return existing;
    }

    static GameObject MakeProp(string name, Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (material != null)
            mr.sharedMaterial = material;
        return go;
    }

    static void SwapScenePianoForDeploy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject recitalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PianoRecitalPrefabPath);
        GameObject sliceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if ((recitalPrefab == null && sliceAsset == null) || !File.Exists("Assets/scene2.unity"))
            return;

        var activeScene = EditorSceneManager.GetActiveScene();
        string previousPath = activeScene.path;
        bool openedScene2 = false;
        if (activeScene.name != "scene2")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/scene2.unity", OpenSceneMode.Single);
            openedScene2 = true;
        }

        GameObject existing = GameObject.Find("PianoRecital");
        Vector3 pos = existing != null ? existing.transform.position : new Vector3(11.76f, 2.0618274f, -1.92f);
        Quaternion rot = existing != null ? existing.transform.rotation : new Quaternion(0f, -0.12162652f, 0f, 0.99257594f);

        if (recitalPrefab != null)
        {
            if (existing != null)
                Object.DestroyImmediate(existing);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(recitalPrefab);
            instance.name = "PianoRecital";
            instance.transform.SetPositionAndRotation(pos, rot);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
        else if (existing != null && sliceAsset != null)
        {
            var remove = new List<GameObject>();
            foreach (Transform child in existing.transform)
            {
                if (child.name == "PianoBench" || child.name == "Piano" || child.name == "PianoLight")
                    continue;
                if (child.GetComponent<Animator>() != null || child.name.Contains("Pirate") || child.name.Contains("Missing"))
                    remove.Add(child.gameObject);
            }
            foreach (GameObject old in remove)
                Object.DestroyImmediate(old);

            Transform existingSlice = existing.transform.Find("Slice");
            GameObject slice = existingSlice != null ? existingSlice.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(sliceAsset);
            slice.name = "Slice";
            slice.transform.SetParent(existing.transform, false);
            slice.transform.SetAsFirstSibling();
            slice.transform.localPosition = Vector3.zero;
            slice.transform.localRotation = Quaternion.identity;
            slice.transform.localScale = Vector3.one;

            Transform bench = existing.transform.Find("PianoBench");
            if (bench != null)
                bench.localPosition = new Vector3(0f, 0f, -0.02f);
            Transform piano = existing.transform.Find("Piano");
            if (piano != null)
                piano.localPosition = new Vector3(0f, 0f, 0.10f);

            if (existing.GetComponent<Musician>() == null)
                existing.AddComponent<Musician>();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        if (openedScene2 && !string.IsNullOrEmpty(previousPath) && previousPath != "Assets/scene2.unity" && File.Exists(previousPath))
        {
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
        }
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null)
                return avatar;
        }
        return null;
    }

    static Mesh BuildPiano(float keyHeight)
    {
        var mesh = new PropMesh();
        Color wood = new Color(0.16f, 0.07f, 0.035f);
        Color woodLight = new Color(0.34f, 0.17f, 0.08f);
        Color ivory = new Color(0.86f, 0.82f, 0.72f);
        Color black = new Color(0.04f, 0.035f, 0.03f);
        Color brass = new Color(0.62f, 0.46f, 0.16f);

        float bodyTop = keyHeight + 0.28f;
        mesh.AddBox(new Vector3(0f, bodyTop * 0.5f, 0.78f), new Vector3(1.55f, bodyTop, 0.9f), wood);
        mesh.AddBox(new Vector3(0f, keyHeight - 0.03f, 0.24f), new Vector3(1.48f, 0.08f, 0.5f), woodLight);
        mesh.AddBox(new Vector3(0f, keyHeight + 0.02f, 0.22f), new Vector3(1.36f, 0.035f, 0.38f), ivory);
        float[] blacks = { -0.5f, -0.36f, -0.1f, 0.04f, 0.18f, 0.42f, 0.56f };
        for (int i = 0; i < blacks.Length; i++)
            mesh.AddBox(new Vector3(blacks[i], keyHeight + 0.05f, 0.12f), new Vector3(0.07f, 0.035f, 0.2f), black);
        mesh.AddBox(new Vector3(0f, bodyTop + 0.16f, 0.82f), new Vector3(1.5f, 0.05f, 0.72f), Quaternion.Euler(-32f, 0f, 0f), woodLight);
        mesh.AddBox(new Vector3(-0.62f, keyHeight * 0.42f, 0.9f), new Vector3(0.09f, keyHeight * 0.84f, 0.09f), wood);
        mesh.AddBox(new Vector3(0.62f, keyHeight * 0.42f, 0.9f), new Vector3(0.09f, keyHeight * 0.84f, 0.09f), wood);
        mesh.AddBox(new Vector3(0f, keyHeight * 0.38f, 0.32f), new Vector3(0.08f, keyHeight * 0.76f, 0.08f), wood);
        mesh.AddBox(new Vector3(0f, keyHeight + 0.26f, 0.46f), new Vector3(0.72f, 0.26f, 0.035f), Quaternion.Euler(-18f, 0f, 0f), woodLight);
        mesh.AddBox(new Vector3(0f, 0.06f, 0.62f), new Vector3(0.28f, 0.035f, 0.1f), brass);
        return mesh.ToMesh("Piano");
    }

    static Mesh BuildBench(float seatHeight)
    {
        var mesh = new PropMesh();
        Color wood = new Color(0.30f, 0.15f, 0.06f);
        mesh.AddBox(new Vector3(0f, seatHeight, 0f), new Vector3(0.72f, 0.08f, 0.36f), wood);
        float leg = Mathf.Max(0.2f, seatHeight - 0.04f);
        mesh.AddBox(new Vector3(-0.28f, leg * 0.5f, 0.12f), new Vector3(0.07f, leg, 0.07f), wood);
        mesh.AddBox(new Vector3(0.28f, leg * 0.5f, 0.12f), new Vector3(0.07f, leg, 0.07f), wood);
        mesh.AddBox(new Vector3(-0.28f, leg * 0.5f, -0.12f), new Vector3(0.07f, leg, 0.07f), wood);
        mesh.AddBox(new Vector3(0.28f, leg * 0.5f, -0.12f), new Vector3(0.07f, leg, 0.07f), wood);
        return mesh.ToMesh("PianoBench");
    }

    sealed class PropMesh
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public void AddBox(Vector3 center, Vector3 size, Color color)
        {
            AddBox(center, size, Quaternion.identity, color);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                Local(-h.x, -h.y, -h.z), Local(-h.x, -h.y, h.z), Local(-h.x, h.y, -h.z), Local(-h.x, h.y, h.z),
                Local(h.x, -h.y, -h.z), Local(h.x, -h.y, h.z), Local(h.x, h.y, -h.z), Local(h.x, h.y, h.z)
            };
            AddQuad(p[0], p[2], p[3], p[1], color);
            AddQuad(p[4], p[5], p[7], p[6], color);
            AddQuad(p[0], p[1], p[5], p[4], color);
            AddQuad(p[2], p[6], p[7], p[3], color);
            AddQuad(p[0], p[4], p[6], p[2], color);
            AddQuad(p[1], p[3], p[7], p[5], color);

            Vector3 Local(float x, float y, float z)
            {
                return center + rotation * new Vector3(x, y, z);
            }
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int index = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _vertices.Add(d);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _triangles.Add(index);
            _triangles.Add(index + 1);
            _triangles.Add(index + 2);
            _triangles.Add(index);
            _triangles.Add(index + 2);
            _triangles.Add(index + 3);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

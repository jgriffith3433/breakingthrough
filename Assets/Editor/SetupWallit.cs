using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the Wallit prefab, Player prefab, and Animator from Assets/Art/Wallit/Wallit.fbx (Gen character3d export).
// Menu: Tools → Wallit → Build Prefab. Also runs on load once the FBX exists (without rewriting the scene on load).
[InitializeOnLoad]
static class SetupWallit
{
    const string ModelPath = "Assets/Art/Wallit/Wallit.fbx";
    const string ControllerPath = "Assets/Art/Wallit/Wallit.controller";
    const string PrefabPath = "Assets/Art/Wallit/Wallit.prefab";
    // Runtime load for the player and wallit playtest (WebGL has no AssetDatabase).
    const string ResourcesPrefabPath = "Assets/Resources/Wallit/Wallit.prefab";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const int MaxRetries = 8;
    static int retries;

    // Clip take names expected on the FBX (Gen export/fbx). Missing ones are skipped.
    public static readonly string[] ClipNames = {
        "idle", "run", "strafeLeft", "strafeRight", "backPedal",
        "swing1", "swing2", "swing3", "groundSmash",
        "homeRunCharge", "homeRunRelease",
        "jump", "landing", "hitReact", "shakeOff", "victory", "death"
    };

    static SetupWallit()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Wallit/Build Prefab")]
    static void MenuBuild()
    {
        retries = 0;
        Build(force: true);
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
        SwapScenePlayerForDeploy();
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
        if (!EnsureFolder("Assets/Art/Wallit") || !EnsureFolder("Assets/Resources/Wallit") || !EnsureFolder("Assets/Prefabs"))
            return false;

        if (!EnsureHumanoidModel())
            return false;

        Dictionary<string, AnimationClip> clips = LoadClips();
        if (clips.Count == 0)
        {
            Debug.LogWarning("Wallit.fbx has no named clips yet (idle/run/swing…).");
            if (!force)
                return false;
        }

        AnimatorController controller = EnsureController(clips);
        if (controller == null)
            return false;

        if (!EnsurePrefab(controller))
            return false;

        if (!EnsurePlayerPrefab())
            return false;

        AssetDatabase.SaveAssets();
        Debug.Log("Wallit prefab ready at " + PrefabPath + " and " + PlayerPrefabPath + " (" + clips.Count + " clips).");
        return true;
    }

    static bool EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return true;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent) && parent.StartsWith("Assets/"))
        {
            if (!EnsureFolder(parent))
                return false;
        }
        string guid = AssetDatabase.CreateFolder(parent, name);
        return !string.IsNullOrEmpty(guid);
    }

    static bool IsLoopClip(string key)
    {
        return key == "idle" || key == "run" || key == "strafeLeft" || key == "strafeRight" || key == "backPedal";
    }

    static float ContactNormalizedTime(string key)
    {
        switch (key)
        {
            case "swing1": return 0.40f;
            case "swing2": return 0.40f;
            case "swing3": return 0.45f;
            case "groundSmash": return 0.45f;
            case "homeRunRelease": return 0.38f;
            default: return -1f;
        }
    }

    static bool IsSwingSpeedClip(string key)
    {
        return key == "swing1" || key == "swing2" || key == "swing3" || key == "groundSmash" || key == "homeRunRelease";
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

                float contact = key != null ? ContactNormalizedTime(key) : -1f;
                if (contact > 0f)
                {
                    takes[i].events = new[]
                    {
                        new AnimationEvent
                        {
                            functionName = "OnBatContact",
                            time = contact,
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
            float contact = ContactNormalizedTime(key);
            if (contact > 0f)
            {
                bool hasEvent = false;
                if (takes[i].events != null)
                {
                    for (int e = 0; e < takes[i].events.Length; e++)
                    {
                        if (takes[i].events[e].functionName == "OnBatContact")
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
        // Mixamo-style "Armature|idle" or "Wallit_idle"
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
        bool hasFireSpeed = false;
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == "Clip")
                hasClipParam = true;
            if (p.name == "FireSpeed")
                hasFireSpeed = true;
        }
        if (!hasClipParam)
            controller.AddParameter("Clip", AnimatorControllerParameterType.Int);
        if (!hasFireSpeed)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "FireSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState defaultState = null;
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
            if (IsSwingSpeedClip(name))
            {
                state.speedParameter = "FireSpeed";
                state.speedParameterActive = true;
            }
            if (defaultState == null || name == "idle")
                defaultState = state;

            // Any-state → clip by Clip int (index into ClipNames).
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

        if (defaultState != null)
            machine.defaultState = defaultState;

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
        instance.name = "Wallit";

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = LoadAvatar(ModelPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.gameObject.GetComponent<WallitAnimRelay>() == null)
            animator.gameObject.AddComponent<WallitAnimRelay>();

        EnsureBatInHand(instance.transform);

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, ResourcesPrefabPath);
        Object.DestroyImmediate(instance);
        return true;
    }

    static void EnsureBatInHand(Transform wallitRoot)
    {
        Transform hand = FindHandBone(wallitRoot);
        Transform existingBat = FindBatTransform(wallitRoot);
        if (existingBat != null)
        {
            if (hand != null && !existingBat.IsChildOf(hand) && existingBat.GetComponent<SkinnedMeshRenderer>() == null)
            {
                existingBat.SetParent(hand, true);
            }
            return;
        }

        Transform parent = hand != null ? hand : wallitRoot;
        GameObject batGo = new GameObject("Bat");
        batGo.transform.SetParent(parent, false);
        if (hand != null)
        {
            batGo.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);
            batGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            batGo.transform.localPosition = new Vector3(0.32f, 1.05f, 0.18f);
            batGo.transform.localRotation = Quaternion.Euler(25f, 0f, -20f);
        }
        batGo.transform.localScale = Vector3.one;

        BuildCapsulePart(batGo.transform, "Handle", new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.25f, 0.06f), new Color(0.12f, 0.09f, 0.08f));
        BuildCapsulePart(batGo.transform, "Barrel", new Vector3(0f, 0.66f, 0f), new Vector3(0.11f, 0.36f, 0.11f), new Color(0.5f, 0.36f, 0.22f));
    }

    static void BuildCapsulePart(Transform parent, string partName, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
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

    static Transform FindBatTransform(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            if (t.name.IndexOf("bat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }
        return null;
    }

    static bool EnsurePlayerPrefab()
    {
        GameObject wallitAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (wallitAsset == null)
            return false;

        GameObject player = new GameObject("Player");
        player.tag = "Player";

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.08f;
        cc.minMoveDistance = 0.001f;
        cc.center = new Vector3(0f, 1.08f, 0f);

        CapsuleCollider trigger = player.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3.22f;
        trigger.height = 11.06f;
        trigger.direction = 2;
        trigger.center = new Vector3(0f, 2.27f, 2.86f);

        MovementMotor motor = player.AddComponent<MovementMotor>();
        motor.GravityMultiplier = 1f;
        motor.LerpTime = 10f;
        motor.DistanceToGround = 0.1f;

        CharacterMovement movement = player.AddComponent<CharacterMovement>();
        movement.MoveMagnitude = 0.05f;
        movement.Speed = 4f;
        movement.SpeedMoveWhileAttack = 0.1f;
        movement.SpeedAttack = 1.5f;
        movement.TurnSpeed = 20f;
        movement.SpeedJump = 20f;

        CharacterMovementController cmc = player.AddComponent<CharacterMovementController>();

        player.AddComponent<BatEquipment>();
        player.AddComponent<PlayerGrowth>();

        GameObject wallit = (GameObject)PrefabUtility.InstantiatePrefab(wallitAsset);
        wallit.name = "Wallit";
        wallit.tag = "playerchild";
        wallit.transform.SetParent(player.transform, false);
        wallit.transform.SetAsFirstSibling();
        wallit.transform.localPosition = Vector3.zero;
        wallit.transform.localRotation = Quaternion.identity;
        wallit.transform.localScale = Vector3.one;

        Animator anim = wallit.GetComponentInChildren<Animator>();
        cmc.Animator = anim;

        GameObject ptarget = new GameObject("ptarget");
        ptarget.transform.SetParent(player.transform, false);
        ptarget.transform.localPosition = new Vector3(0f, 1.767f, 0f);

        GameObject lockOn = new GameObject("LockOn");
        lockOn.transform.SetParent(player.transform, false);
        lockOn.transform.localPosition = new Vector3(0f, 1.52f, 1.81f);
        BoxCollider lockBox = lockOn.AddComponent<BoxCollider>();
        lockBox.isTrigger = true;
        lockBox.size = new Vector3(3.5f, 5f, 5f);
        lockOn.AddComponent<LockOn>();

        GameObject fpCam = new GameObject("FirstPersonCamPos");
        fpCam.transform.SetParent(player.transform, false);
        fpCam.transform.localPosition = new Vector3(0f, 1.898f, 0.211f);

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        Object.DestroyImmediate(player);
        return true;
    }

    static void SwapScenePlayerForDeploy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject wallitAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (wallitAsset == null || !File.Exists("Assets/scene2.unity"))
            return;

        var activeScene = EditorSceneManager.GetActiveScene();
        string previousPath = activeScene.path;
        bool openedScene2 = false;
        if (activeScene.name != "scene2")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/scene2.unity", OpenSceneMode.Single);
            openedScene2 = true;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var remove = new List<GameObject>();
            foreach (Transform child in player.transform)
            {
                if (child.name == "ptarget" || child.name == "LockOn" || child.name == "FirstPersonCamPos")
                    continue;
                if (child.CompareTag("playerchild") || child.GetComponent<Animator>() != null || child.name.Contains("Warrok") || child.name.Contains("Missing"))
                    remove.Add(child.gameObject);
            }
            foreach (GameObject old in remove)
                Object.DestroyImmediate(old);

            Transform existingWallit = player.transform.Find("Wallit");
            GameObject wallit = existingWallit != null ? existingWallit.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(wallitAsset);
            wallit.name = "Wallit";
            wallit.tag = "playerchild";
            wallit.transform.SetParent(player.transform, false);
            wallit.transform.SetAsFirstSibling();
            wallit.transform.localPosition = Vector3.zero;
            wallit.transform.localRotation = Quaternion.identity;
            wallit.transform.localScale = Vector3.one;

            CharacterMovementController cmc = player.GetComponent<CharacterMovementController>();
            if (cmc != null)
                cmc.Animator = wallit.GetComponentInChildren<Animator>();

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
}

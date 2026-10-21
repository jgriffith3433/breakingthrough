using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds the Wallit prefab and Animator from Assets/Art/Wallit/Wallit.fbx (Gen character3d export).
// Does not swap him onto the player yet — gameplay still uses Warrok until a later task wires him in.
// Menu: Tools → Wallit → Build Prefab. Also runs on load once the FBX exists.
[InitializeOnLoad]
static class SetupWallit
{
    const string ModelPath = "Assets/Art/Wallit/Wallit.fbx";
    const string ControllerPath = "Assets/Art/Wallit/Wallit.controller";
    const string PrefabPath = "Assets/Art/Wallit/Wallit.prefab";
    // Runtime load for the wallit playtest (WebGL has no AssetDatabase).
    const string ResourcesPrefabPath = "Assets/Resources/Wallit/Wallit.prefab";
    const int MaxRetries = 8;
    static int retries;

    // Clip take names expected on the FBX (Gen export/fbx). Missing ones are skipped.
    static readonly string[] ClipNames = {
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
        if (!EnsureFolder("Assets/Art/Wallit") || !EnsureFolder("Assets/Resources/Wallit"))
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

        AssetDatabase.SaveAssets();
        Debug.Log("Wallit prefab ready at " + PrefabPath + " (" + clips.Count + " clips). Not wired to the player.");
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

    static bool EnsureHumanoidModel()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return false;

        bool ok = importer.animationType == ModelImporterAnimationType.Human
            && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
            && importer.materialImportMode != ModelImporterMaterialImportMode.None;

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
        importer.SaveAndReimport();
        return false;
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
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == "Clip")
                hasClipParam = true;
        }
        if (!hasClipParam)
            controller.AddParameter("Clip", AnimatorControllerParameterType.Int);

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

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, ResourcesPrefabPath);
        Object.DestroyImmediate(instance);
        return true;
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

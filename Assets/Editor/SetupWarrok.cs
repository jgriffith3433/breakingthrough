using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mixamo Warrok replaces the old hero. The State parameter still drives walk, aim, shoot, and jump.
[InitializeOnLoad]
static class SetupWarrok
{
    const string ModelPath = "Assets/Art/Warrok W Kurniawan.fbx";
    const string ControllerPath = "Assets/hero/Hero.controller";

    static readonly ClipSetup[] Clips =
    {
        new ClipSetup("Assets/Art/Standing Aim Idle 01.fbx", true),
        new ClipSetup("Assets/Art/Walking.fbx", true),
        new ClipSetup("Assets/Art/Standing Aim Walk Forward.fbx", true),
        new ClipSetup("Assets/Art/Mutant Jumping.fbx", false),
        new ClipSetup("Assets/Art/Standing Aim Recoil.fbx", false)
    };

    static SetupWarrok()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (!File.Exists(ModelPath) || EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        if (!EnsureHumanoidModel() || !EnsureAnimationClips())
        {
            EditorApplication.delayCall += Run;
            return;
        }

        AnimationClip aimIdle = LoadClip(Clips[0].Path);
        AnimationClip walk = LoadClip(Clips[1].Path);
        AnimationClip aimWalk = LoadClip(Clips[2].Path);
        AnimationClip jump = LoadClip(Clips[3].Path);
        AnimationClip shoot = LoadClip(Clips[4].Path);
        if (aimIdle == null || walk == null || aimWalk == null || jump == null || shoot == null)
        {
            Debug.LogWarning("Warrok clips are not imported yet.");
            return;
        }

        WireController(aimIdle, walk, aimWalk, jump, shoot);
        SwapModel();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Warrok is on the player. Walk, aim, shoot, and jump use the Mixamo clips.");
    }

    static bool EnsureHumanoidModel()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer.animationType == ModelImporterAnimationType.Human
            && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
            return true;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();
        return false;
    }

    static bool EnsureAnimationClips()
    {
        Avatar avatar = LoadAvatar(ModelPath);
        if (avatar == null)
            return false;

        bool ready = true;
        foreach (ClipSetup clip in Clips)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(clip.Path);
            bool human = importer.animationType == ModelImporterAnimationType.Human
                && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                && importer.sourceAvatar == avatar;
            bool looped = ClipLoopMatches(importer, clip.Loop);
            if (human && looped)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            for (int i = 0; i < takes.Length; i++)
            {
                takes[i].loopTime = clip.Loop;
                takes[i].lockRootRotation = true;
                takes[i].lockRootHeightY = true;
                takes[i].lockRootPositionXZ = true;
                takes[i].keepOriginalOrientation = true;
                takes[i].keepOriginalPositionY = true;
                takes[i].keepOriginalPositionXZ = false;
            }

            importer.clipAnimations = takes;
            importer.SaveAndReimport();
            ready = false;
        }

        return ready;
    }

    static bool ClipLoopMatches(ModelImporter importer, bool loop)
    {
        ModelImporterClipAnimation[] takes = importer.clipAnimations;
        if (takes == null || takes.Length == 0)
            return false;
        for (int i = 0; i < takes.Length; i++)
        {
            if (takes[i].loopTime != loop)
                return false;
        }

        return true;
    }

    static void WireController(AnimationClip aimIdle, AnimationClip walk, AnimationClip aimWalk, AnimationClip jump, AnimationClip shoot)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        var byName = new Dictionary<string, AnimatorState>();
        foreach (ChildAnimatorState child in machine.states)
            byName[child.state.name] = child.state;

        AnimatorState idle = byName.ContainsKey("Idle") ? byName["Idle"] : byName["Aim Idle"];
        AnimatorState move;
        AnimatorState aimMove;
        if (byName.ContainsKey("Run"))
        {
            move = byName["Run"];
            aimMove = byName["Walk"];
        }
        else
        {
            move = byName["Walk"];
            aimMove = byName["Aim Walk"];
        }

        AnimatorState jumpState = byName.ContainsKey("Unreal Matinee Take") ? byName["Unreal Matinee Take"] : byName["Jump"];
        idle.name = "Aim Idle";
        idle.motion = aimIdle;
        move.name = "Walk";
        move.motion = walk;
        aimMove.name = "Aim Walk";
        aimMove.motion = aimWalk;
        jumpState.name = "Jump";
        jumpState.motion = jump;

        AnimatorState shootState = FindState(machine, "Shoot");
        if (shootState == null)
            shootState = machine.AddState("Shoot", new Vector3(504f, 48f, 0f));
        shootState.motion = shoot;

        bool hasShootTransition = false;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            transition.canTransitionToSelf = false;
            if (transition.destinationState == shootState)
                hasShootTransition = true;
        }

        if (!hasShootTransition)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(shootState);
            transition.AddCondition(AnimatorConditionMode.Equals, 2f, "State");
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
        }

        EditorUtility.SetDirty(controller);
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

    static void SwapModel()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Transform existing = player.transform.Find("Warrok W Kurniawan");
        GameObject model;
        if (existing != null)
        {
            model = existing.gameObject;
        }
        else
        {
            List<GameObject> remove = new List<GameObject>();
            foreach (Transform child in player.transform)
            {
                if (child.CompareTag("playerchild") || child.GetComponent<Animator>() != null)
                    remove.Add(child.gameObject);
            }

            foreach (GameObject old in remove)
                Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(player.transform, false);
            model.transform.SetAsFirstSibling();
        }

        model.tag = "playerchild";
        model.transform.localRotation = Quaternion.identity;
        model.transform.localPosition = Vector3.zero;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        animator.avatar = LoadAvatar(ModelPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        FitToPlayer(player, model);

        CharacterMovementController movement = player.GetComponent<CharacterMovementController>();
        if (movement != null)
            movement.Animator = animator;
    }

    static void FitToPlayer(GameObject player, GameObject model)
    {
        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!any)
            return;

        float height = bounds.size.y;
        if (height > 2.4f || height < 1.3f)
            model.transform.localScale = Vector3.one * (1.8f / height);

        bounds = new Bounds(model.transform.position, Vector3.zero);
        any = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        float lift = (player.transform.position.y + 0.02f) - bounds.min.y;
        model.transform.position += new Vector3(0f, lift, 0f);
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

    static AnimationClip LoadClip(string path)
    {
        AnimationClip best = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
                continue;
            best = clip;
        }

        return best;
    }

    struct ClipSetup
    {
        public string Path;
        public bool Loop;

        public ClipSetup(string path, bool loop)
        {
            Path = path;
            Loop = loop;
        }
    }
}

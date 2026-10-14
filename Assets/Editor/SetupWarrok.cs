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
        new ClipSetup("Assets/Art/Standing Aim Recoil.fbx", false),
        new ClipSetup("Assets/Art/Stomp.fbx", true),
        new ClipSetup("Assets/Art/Shaking It Off.fbx", false)
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
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
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
        jumpState.speed = 2f;

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

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == jumpState)
                transition.duration = 0.08f;
        }

        EnsureShootLayer(controller, shoot);
        AnimationClip stomp = LoadClip("Assets/Art/Stomp.fbx");
        AnimationClip shake = LoadClip("Assets/Art/Shaking It Off.fbx");
        EnsureActionStates(controller, stomp, shake);
        EditorUtility.SetDirty(controller);
    }

    static void EnsureActionStates(AnimatorController controller, AnimationClip stomp, AnimationClip shake)
    {
        if (stomp == null)
            Debug.LogWarning("Stomp.fbx has no clip yet.");
        if (shake == null)
            Debug.LogWarning("Shaking It Off.fbx has no clip yet.");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AddAction(machine, "Stomp", stomp, 5);
        AddAction(machine, "Shake", shake, 7);
    }

    static void AddAction(AnimatorStateMachine machine, string name, AnimationClip clip, int stateValue)
    {
        if (clip == null) {
            return;
        }
        AnimatorState state = FindState(machine, name);
        if (state == null) {
            state = machine.AddState(name);
        }
        state.motion = clip;
        AnimatorStateTransition enter = null;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions) {
            if (transition.destinationState == state) {
                enter = transition;
            }
        }
        if (enter == null) {
            enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.Equals, stateValue, "State");
            enter.hasExitTime = false;
        }
        enter.duration = 0.06f;
        enter.canTransitionToSelf = false;
    }

    // Arms shoot on their own layer so a jump can keep playing underneath.
    static void EnsureShootLayer(AnimatorController controller, AnimationClip shoot)
    {
        bool hasFire = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == "Fire")
                hasFire = true;
        }
        if (!hasFire)
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        EnsureFireSpeed(controller);

        int upper = -1;
        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == "Upper Body")
                upper = i;
        }
        if (upper < 0)
        {
            controller.AddLayer("Upper Body");
            upper = controller.layers.Length - 1;
        }

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/hero/UpperBody.mask");
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[upper];
        layer.avatarMask = mask;
        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layers[upper] = layer;
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[upper].stateMachine;
        AnimatorState empty = FindState(machine, "Empty");
        if (empty == null)
            empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
        empty.motion = null;
        empty.writeDefaultValues = false;
        machine.defaultState = empty;

        AnimatorState shot = FindState(machine, "Shoot");
        if (shot == null)
            shot = machine.AddState("Shoot", new Vector3(300f, 120f, 0f));
        shot.motion = shoot;
        shot.writeDefaultValues = true;
        // Quick Draw and streaks speed the draw through this parameter.
        shot.speedParameter = "FireSpeed";
        shot.speedParameterActive = true;

        bool hasExit = false;
        foreach (AnimatorStateTransition transition in shot.transitions)
        {
            if (transition.destinationState != empty)
                continue;
            hasExit = true;
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.duration = 0.05f;
            transition.interruptionSource = TransitionInterruptionSource.Source;
        }
        if (!hasExit)
        {
            AnimatorStateTransition exit = shot.AddTransition(empty);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.05f;
            exit.interruptionSource = TransitionInterruptionSource.Source;
        }

        bool hasEnter = false;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState != shot)
                continue;
            hasEnter = true;
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = true;
        }
        if (!hasEnter)
        {
            AnimatorStateTransition enter = machine.AddAnyStateTransition(shot);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;
        }
    }

    static void EnsureFireSpeed(AnimatorController controller)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != "FireSpeed")
                continue;
            if (Mathf.Approximately(parameters[i].defaultFloat, 1f))
                return;
            parameters[i].defaultFloat = 1f;
            controller.parameters = parameters;
            return;
        }
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "FireSpeed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
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

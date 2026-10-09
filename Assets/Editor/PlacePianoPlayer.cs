using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Seats the Mixamo pirate at a piano, looping Piano Playing, with generated background music.
// Also rebuilds the cottage meshes the first time they are still the old box houses.
[InitializeOnLoad]
static class PlacePianoPlayer
{
    const string ModelPath = "Assets/Art/PianoPlayer/Pirate By P. Konstantinov.fbx";
    const string ClipPath = "Assets/Art/PianoPlayer/Piano Playing.fbx";
    const string ControllerPath = "Assets/Art/PianoPlayer/PianoPlayer.controller";
    const string MusicPath = "Assets/Art/PianoPlayer/KnollPiano.wav";
    const string RootName = "PianoRecital";

    static int _imports;
    static bool _force;

    static PlacePianoPlayer()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Knoll/Place Piano Player")]
    static void PlaceFromMenu()
    {
        _force = true;
        _imports = 0;
        Run();
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        bool changed = false;
        if (_force || HousesNeedRebuild())
        {
            DressGrassyKnoll.RefreshHouseMeshes();
            changed = true;
        }

        if (!File.Exists(ModelPath) || !File.Exists(ClipPath))
        {
            if (changed)
                SaveScene();
            return;
        }

        if (!EnsureHumanoid() || !EnsureClip())
        {
            if (_imports++ < 8)
                EditorApplication.delayCall += Run;
            else
                Debug.LogWarning("Pirate piano clips did not finish importing.");
            if (changed)
                SaveScene();
            return;
        }

        if (_force || GameObject.Find(RootName) == null)
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);
            if (!Place())
            {
                if (_imports++ < 8)
                    EditorApplication.delayCall += Run;
                if (changed)
                    SaveScene();
                return;
            }

            changed = true;
            _force = false;
        }

        if (changed)
        {
            SaveScene();
            Debug.Log("Cottages rebuilt. The pirate is seated at the piano, looping one clip, with background music.");
        }
    }

    static bool HousesNeedRebuild()
    {
        GameObject house = GameObject.Find("House");
        if (house == null)
            return false;
        MeshFilter filter = house.GetComponent<MeshFilter>();
        return filter == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cottage";
    }

    static bool EnsureHumanoid()
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

    static bool EnsureClip()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ClipPath);
        bool ready = importer.animationType == ModelImporterAnimationType.Human
            && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
            && ClipLoops(importer)
            && LoadClip(ClipPath) != null;
        if (ready)
            return true;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
        if (takes == null || takes.Length == 0)
            takes = importer.clipAnimations;
        if (takes != null)
        {
            for (int i = 0; i < takes.Length; i++)
            {
                takes[i].loopTime = true;
                takes[i].lockRootRotation = true;
                takes[i].lockRootHeightY = true;
                takes[i].lockRootPositionXZ = true;
                takes[i].keepOriginalOrientation = true;
                takes[i].keepOriginalPositionY = true;
                takes[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = takes;
        }
        importer.SaveAndReimport();
        return false;
    }

    static bool ClipLoops(ModelImporter importer)
    {
        ModelImporterClipAnimation[] takes = importer.clipAnimations;
        if (takes == null || takes.Length == 0)
            return false;
        for (int i = 0; i < takes.Length; i++)
        {
            if (!takes[i].loopTime)
                return false;
        }

        return true;
    }

    static bool Place()
    {
        AnimationClip clip = LoadClip(ClipPath);
        Avatar avatar = LoadAvatar(ModelPath);
        if (clip == null || avatar == null)
        {
            Debug.LogWarning("Pirate or piano clip is not ready yet. " + Describe(ModelPath) + " | " + Describe(ClipPath));
            return false;
        }

        AnimatorController controller = WireController(clip);
        AudioClip music = EnsureMusic();
        Material lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Knoll/KnollLit.mat");

        Vector3 spot;
        Vector3 forward;
        ChooseSpot(out spot, out forward);
        float ground = DressGrassyKnoll.TerrainHeight(spot.x, spot.z);

        var root = new GameObject(RootName);
        root.transform.position = new Vector3(spot.x, ground, spot.z);
        root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var pirate = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        pirate.name = "Pirate";
        pirate.transform.SetParent(root.transform, false);
        pirate.transform.localRotation = Quaternion.identity;
        pirate.transform.localPosition = Vector3.zero;

        FitHeight(pirate, 1.75f);
        LiftFeet(root.transform.position.y, pirate);

        Animator animator = pirate.GetComponent<Animator>();
        if (animator == null)
            animator = pirate.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform hipsBone = FindBone(pirate.transform, "Hips");
        Transform leftHand = FindBone(pirate.transform, "LeftHand");
        Transform rightHand = FindBone(pirate.transform, "RightHand");
        float hipsBefore = hipsBone != null ? hipsBone.position.y : ground + 1f;
        clip.SampleAnimation(pirate, clip.length * 0.45f);

        float seatHeight = 0.48f;
        float keyHeight = 0.74f;
        Vector3 benchAt = root.transform.position + root.transform.forward * -0.05f;
        Vector3 pianoAt = root.transform.position + forward * 0.62f;
        Quaternion facing = root.transform.rotation;
        bool seated = false;
        if (hipsBone != null && leftHand != null && rightHand != null)
        {
            Vector3 hips = hipsBone.position;
            Vector3 hands = (leftHand.position + rightHand.position) * 0.5f;
            float hipHeight = hips.y - ground;
            if (hipHeight < 0.95f && hipHeight > 0.25f && hipsBefore - hips.y > 0.05f)
            {
                seated = true;
                seatHeight = Mathf.Clamp(hipHeight - 0.04f, 0.36f, 0.7f);
                keyHeight = Mathf.Clamp(hands.y - ground, 0.6f, 0.98f);
                benchAt = new Vector3(hips.x, ground, hips.z);
                Vector3 toKeys = hands - hips;
                toKeys.y = 0f;
                if (toKeys.sqrMagnitude > 0.04f)
                    facing = Quaternion.LookRotation(toKeys.normalized, Vector3.up);
                pianoAt = new Vector3(hands.x, ground, hands.z);
            }
        }

        GameObject bench = Prop("PianoBench", BuildBench(seatHeight), lit, root.transform);
        GameObject piano = Prop("Piano", BuildPiano(keyHeight), lit, root.transform);
        if (seated)
        {
            bench.transform.SetPositionAndRotation(benchAt, facing);
            piano.transform.SetPositionAndRotation(pianoAt, facing);
        }
        else
        {
            bench.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            bench.transform.localRotation = Quaternion.identity;
            piano.transform.localPosition = new Vector3(0f, 0f, 0.58f);
            piano.transform.localRotation = Quaternion.identity;
        }

        BoxCollider blocker = piano.AddComponent<BoxCollider>();
        blocker.center = piano.GetComponent<MeshFilter>().sharedMesh.bounds.center;
        blocker.size = piano.GetComponent<MeshFilter>().sharedMesh.bounds.size;

        var lampObject = new GameObject("PianoLight");
        lampObject.transform.SetParent(root.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 2.1f, 0.35f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.72f, 0.42f);
        lamp.intensity = 1.8f;
        lamp.range = 8f;
        lamp.shadows = LightShadows.None;

        if (music != null)
        {
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
        }

        return true;
    }

    static void ChooseSpot(out Vector3 position, out Vector3 forward)
    {
        GameObject farthest = null;
        float best = -1f;
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate.name != "House")
                continue;
            Vector3 p = candidate.transform.position;
            float distance = p.x * p.x + p.z * p.z;
            if (distance > best)
            {
                best = distance;
                farthest = candidate;
            }
        }

        if (farthest == null)
        {
            position = new Vector3(8f, 0f, 5f);
            forward = new Vector3(-8f, 0f, -5f).normalized;
        }
        else
        {
            forward = farthest.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();
            position = farthest.transform.position + forward * 6.6f;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int i = 0; i < 5 && Blocked(position); i++)
            position += right * 2.4f;
    }

    static bool Blocked(Vector3 position)
    {
        foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            bool player = candidate.CompareTag("Player");
            if (candidate.name != "Tree" && candidate.name != "House" && !player)
                continue;
            Vector3 flat = candidate.transform.position - position;
            flat.y = 0f;
            float limit = player ? 3.5f : candidate.name == "House" ? 3.4f : 1.8f;
            if (flat.magnitude < limit)
                return true;
        }

        return false;
    }

    static AnimatorController WireController(AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.defaultState;
        if (state == null)
        {
            state = machine.AddState("Play");
            machine.defaultState = state;
        }

        state.name = "Play";
        state.motion = clip;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AudioClip EnsureMusic()
    {
        if (!File.Exists(MusicPath))
        {
            const int rate = 44100;
            float[] samples = Compose(rate, 16f);
            WriteWav(MusicPath, samples, rate);
            AssetDatabase.ImportAsset(MusicPath, ImportAssetOptions.ForceUpdate);
        }

        var importer = AssetImporter.GetAtPath(MusicPath) as AudioImporter;
        if (importer != null && !importer.forceToMono)
        {
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
    }

    static float[] Compose(int rate, float seconds)
    {
        var samples = new float[Mathf.RoundToInt(rate * seconds)];
        var hits = new List<NoteHit>
        {
            Note(45, 0.00f, 3.6f, 0.22f), Note(57, 0.02f, 2.8f, 0.16f), Note(60, 0.14f, 2.6f, 0.15f), Note(64, 0.26f, 2.4f, 0.14f),
            Note(72, 0.55f, 0.7f, 0.28f), Note(69, 1.35f, 0.7f, 0.26f), Note(64, 2.15f, 0.7f, 0.26f), Note(60, 2.95f, 0.85f, 0.24f),
            Note(41, 4.00f, 3.6f, 0.22f), Note(53, 4.02f, 2.8f, 0.16f), Note(57, 4.14f, 2.6f, 0.15f), Note(60, 4.26f, 2.4f, 0.14f),
            Note(69, 4.55f, 0.7f, 0.28f), Note(67, 5.35f, 0.7f, 0.26f), Note(65, 6.15f, 0.7f, 0.26f), Note(60, 6.95f, 0.85f, 0.24f),
            Note(36, 8.00f, 3.6f, 0.22f), Note(48, 8.02f, 2.8f, 0.16f), Note(52, 8.14f, 2.6f, 0.15f), Note(55, 8.26f, 2.4f, 0.14f),
            Note(67, 8.55f, 0.7f, 0.28f), Note(64, 9.35f, 0.7f, 0.26f), Note(62, 10.15f, 0.7f, 0.26f), Note(60, 10.95f, 0.85f, 0.24f),
            Note(40, 12.00f, 3.6f, 0.22f), Note(52, 12.02f, 2.8f, 0.16f), Note(56, 12.14f, 2.6f, 0.15f), Note(59, 12.26f, 2.4f, 0.14f),
            Note(71, 12.55f, 0.7f, 0.28f), Note(67, 13.35f, 0.7f, 0.26f), Note(64, 14.15f, 0.7f, 0.26f), Note(62, 14.95f, 0.85f, 0.24f)
        };

        foreach (NoteHit hit in hits)
            AddNote(samples, rate, hit);

        float peak = 0.001f;
        for (int i = 0; i < samples.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        float scale = 0.6f / peak;
        int fade = rate / 10;
        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i] * scale;
            if (i < fade)
                sample *= i / (float)fade;
            if (i > samples.Length - fade)
                sample *= (samples.Length - i) / (float)fade;
            samples[i] = sample;
        }

        return samples;
    }

    static NoteHit Note(int midi, float time, float duration, float gain)
    {
        return new NoteHit { Midi = midi, Time = time, Duration = duration, Gain = gain };
    }

    static void AddNote(float[] samples, int rate, NoteHit hit)
    {
        float freq = 440f * Mathf.Pow(2f, (hit.Midi - 69) / 12f);
        int start = Mathf.RoundToInt(hit.Time * rate);
        int length = Mathf.RoundToInt(hit.Duration * rate);
        for (int i = 0; i < length; i++)
        {
            int index = start + i;
            if (index < 0 || index >= samples.Length)
                continue;
            float t = i / (float)rate;
            float env = Mathf.Exp(-2.6f * t);
            float phase = freq * t * Mathf.PI * 2f;
            float tone = Mathf.Sin(phase) * 0.72f
                + Mathf.Sin(phase * 2f) * 0.2f * Mathf.Exp(-5f * t)
                + Mathf.Sin(phase * 3f) * 0.08f * Mathf.Exp(-8f * t)
                + Mathf.Sin(phase * 1.004f) * 0.16f;
            float hammer = t < 0.018f ? (1f - t / 0.018f) * Mathf.Sin(phase * 6.5f) * 0.2f : 0f;
            samples[index] += (tone * env + hammer) * hit.Gain;
        }
    }

    static void WriteWav(string path, float[] samples, int rate)
    {
        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            int dataSize = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
        }
    }

    static void FitHeight(GameObject model, float target)
    {
        float height = RenderBounds(model).size.y;
        if (height < 1.25f || height > 2.4f)
            model.transform.localScale = Vector3.one * (target / Mathf.Max(height, 0.01f));
    }

    static void LiftFeet(float groundY, GameObject model)
    {
        Bounds bounds = RenderBounds(model);
        float lift = groundY - bounds.min.y;
        model.transform.position += new Vector3(0f, lift, 0f);
    }

    static Bounds RenderBounds(GameObject model)
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

        return bounds;
    }

    static Transform FindBone(Transform root, string token)
    {
        Transform fallback = null;
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name.EndsWith(token, System.StringComparison.OrdinalIgnoreCase))
                return bone;
            if (fallback == null && bone.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                fallback = bone;
        }

        return fallback;
    }

    static GameObject Prop(string name, Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        if (material != null)
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        else
            go.AddComponent<MeshRenderer>();
        return go;
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

    static void SaveScene()
    {
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static string Describe(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        int avatars = 0;
        int clips = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar)
                avatars++;
            if (assets[i] is AnimationClip)
                clips++;
        }

        Animator animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        return path + " assets=" + assets.Length + " avatars=" + avatars + " clips=" + clips
            + " animatorAvatar=" + (animator != null && animator.avatar != null);
    }

    static Avatar LoadAvatar(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model != null)
        {
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null)
                return animator.avatar;
        }

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

    struct NoteHit
    {
        public int Midi;
        public float Time;
        public float Duration;
        public float Gain;
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

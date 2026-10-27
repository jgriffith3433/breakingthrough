using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The look of the hill at night: moonlight, fog, lanterns around the stage, ground mist,
// music notes from the piano, and the camera grade. Built at runtime so scene2 stays untouched.
public class NightAtmosphere : MonoBehaviour {
    public static NightAtmosphere Instance { get; private set; }

    readonly List<Light> lanterns = new List<Light>();
    readonly List<float> lanternSeeds = new List<float>();
    ParticleSystem mist;
    Transform follow;
    float noteIn = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<NightAtmosphere>() != null) {
            return;
        }
        new GameObject("Night Atmosphere").AddComponent<NightAtmosphere>();
    }

    void Awake() {
        Instance = this;
    }

    void Start() {
        Moonlight();
        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<NightGrade>() == null) {
            cam.allowHDR = !UiFit.Touch;
            cam.gameObject.AddComponent<NightGrade>();
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        follow = player != null ? player.transform : null;
        Stage();
        mist = Mist();
    }

    static void Moonlight() {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.1f, 0.13f, 0.22f);
        RenderSettings.fogDensity = 0.0062f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.27f, 0.33f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.16f, 0.2f, 0.3f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.11f);
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (light.type == LightType.Directional) {
                light.color = new Color(0.62f, 0.72f, 1f);
                light.intensity = 0.9f;
                light.shadowStrength = 0.75f;
            }
        }
        QualitySettings.pixelLightCount = UiFit.Touch ? 3 : Mathf.Max(QualitySettings.pixelLightCount, 6);
    }

    void Stage() {
        Musician musician = Musician.Instance;
        Vector3 center = musician != null ? musician.transform.position : Vector3.zero;
        int count = 4;
        var tops = new List<Vector3>();
        for (int i = 0; i < count; i++) {
            float angle = (45f + i * 360f / count) * Mathf.Deg2Rad;
            Vector3 spot = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 5.4f;
            spot.y = Ground(spot);
            Vector3 toCenter = center - spot;
            toCenter.y = 0f;
            Quaternion facing = Quaternion.LookRotation(toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : Vector3.forward);

            GameObject post = ArtLibrary.Make("Lantern");
            Vector3 lamp;
            if (post != null) {
                post.transform.SetPositionAndRotation(spot, facing);
                foreach (MeshRenderer renderer in post.GetComponentsInChildren<MeshRenderer>()) {
                    renderer.sharedMaterial = BirdArt.Glowing;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                post.transform.SetParent(transform, true);
                lamp = spot + facing * new Vector3(0f, 1.88f, 0.5f);
            } else {
                lamp = spot + Vector3.up * 1.9f;
            }
            tops.Add(spot + Vector3.up * 2.25f);

            var lightGo = new GameObject("Lantern Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.position = lamp;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.66f, 0.34f);
            light.range = 11f;
            light.intensity = 1.7f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            lanterns.Add(light);
            lanternSeeds.Add(Random.Range(0f, 100f));
            Glow(lamp, 0.55f, new Color(1f, 0.7f, 0.35f, 0.55f));
        }
        for (int i = 0; i < tops.Count; i++) {
            StringLights(tops[i], tops[(i + 1) % tops.Count]);
        }
    }

    // A sagging wire between two posts with warm bulbs along it.
    void StringLights(Vector3 a, Vector3 b) {
        var wireGo = new GameObject("String Lights");
        wireGo.transform.SetParent(transform, false);
        LineRenderer wire = wireGo.AddComponent<LineRenderer>();
        const int points = 14;
        wire.positionCount = points;
        wire.startWidth = 0.025f;
        wire.endWidth = 0.025f;
        wire.material = new Material(SafeShader.Find("Unlit/Color"));
        wire.material.color = new Color(0.05f, 0.04f, 0.04f);
        wire.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Color[] bulbs = { new Color(1f, 0.78f, 0.4f), new Color(1f, 0.5f, 0.35f), new Color(0.6f, 0.85f, 1f), new Color(0.75f, 1f, 0.55f) };
        for (int i = 0; i < points; i++) {
            float t = i / (float)(points - 1);
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.9f;
            wire.SetPosition(i, p);
            if (i > 0 && i < points - 1) {
                Color c = bulbs[i % bulbs.Length];
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(bulb.GetComponent<Collider>());
                bulb.name = "Bulb";
                bulb.transform.SetParent(wireGo.transform, false);
                bulb.transform.position = p + Vector3.down * 0.07f;
                bulb.transform.localScale = Vector3.one * 0.11f;
                UpgradeLook.Paint(bulb, c * 1.3f);
                bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Glow(bulb.transform.position, 0.45f, new Color(c.r, c.g, c.b, 0.35f));
            }
        }
    }

    // A soft halo sprite that always faces the camera.
    static void Glow(Vector3 at, float size, Color color) {
        var go = new GameObject("Halo");
        go.transform.position = at;
        var halo = go.AddComponent<Halo>();
        halo.Build(size, color);
        if (Instance != null) {
            go.transform.SetParent(Instance.transform, true);
        }
    }

    ParticleSystem Mist() {
        var go = new GameObject("Ground Mist");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.maxParticles = 90;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(9f, 16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.6f, 0.7f, 0.9f, 0.09f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(110f, 0.5f, 110f);
        shape.position = new Vector3(0f, 0.6f, 0f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        Particles(go, "smoke", false, 1f);
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        ps.Play();
        return ps;
    }

    static void Particles(GameObject go, string texture, bool additive, float boost) {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("BT/Particle");
        var material = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
        material.mainTexture = Fx.Texture(texture);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Boost", boost);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 3f;
    }

    void Update() {
        float t = Time.time;
        for (int i = 0; i < lanterns.Count; i++) {
            if (lanterns[i] == null) {
                continue;
            }
            float flicker = Mathf.PerlinNoise(lanternSeeds[i], t * 3.2f);
            lanterns[i].intensity = 1.45f + flicker * 0.5f;
        }
        if (follow != null) {
            Vector3 at = follow.position;
            if (mist != null) {
                mist.transform.position = new Vector3(at.x, at.y - 1f, at.z);
            }
        }

        Musician musician = Musician.Instance;
        if (musician != null && musician.Alive && !WaveDirector.IsLost && Time.timeScale > 0.01f) {
            noteIn -= Time.deltaTime;
            if (noteIn <= 0f) {
                noteIn = Random.Range(0.45f, 1.1f);
                Fx.Notes(musician.transform.position + Vector3.up * 1.7f + Random.insideUnitSphere * 0.4f, 1);
            }
        }
    }

    static float Ground(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 40f, at.z), Vector3.down, out RaycastHit hit, 100f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }
}

// A camera-facing soft glow quad. Bloom picks it up.
public class Halo : MonoBehaviour {
    static Material additive;
    Transform quad;

    public void Build(float size, Color color) {
        if (additive == null) {
            Shader shader = Shader.Find("BT/Particle");
            additive = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
            additive.mainTexture = Fx.Texture("glow");
            additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            additive.SetFloat("_Boost", 1.6f);
        }
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = additive;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var mesh = go.GetComponent<MeshFilter>().mesh;
        var colors = new Color[mesh.vertexCount];
        for (int i = 0; i < colors.Length; i++) {
            colors[i] = color;
        }
        mesh.colors = colors;
        quad = go.transform;
    }

    void LateUpdate() {
        Camera cam = Camera.main;
        if (quad != null && cam != null) {
            quad.rotation = cam.transform.rotation;
        }
    }
}

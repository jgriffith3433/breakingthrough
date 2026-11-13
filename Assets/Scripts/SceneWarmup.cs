using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hitch killer: the first frames of a chapter or a run used to compile shaders and build bot meshes on the
// main thread (worst fps 9 on desktop, 1 on a phone). The game scene's first frames now run behind the load
// screen / paper sheet: every shader is warmed once per page, then one of each bot and a hit burst are made
// far below the arena and thrown away, a few per frame, so no frame in play pays for them first.
public class SceneWarmup : MonoBehaviour {
    static bool shadersDone;
    public static int Passes { get; private set; }
    public static float LongestWarmFrameMs { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (scene.name != MetaProgress.GameScene || mode != LoadSceneMode.Single) {
            return;
        }
        new GameObject("Scene Warmup").AddComponent<SceneWarmup>();
    }

    IEnumerator Start() {
        // Let the load screen draw a frame first, then pay the cost behind it.
        yield return null;
        if (!shadersDone) {
            shadersDone = true;
            Shader.WarmupAllShaders();
            yield return null;
        }
        Passes++;
        var enemyKinds = System.Enum.GetValues(typeof(BotKind));
        int perFrame = UiFit.Touch ? 1 : 2;
        int n = 0;
        foreach (BotKind kind in enemyKinds) {
            if (kind != BotKind.YoBot && kind != BotKind.SpiderBot) {
                continue;
            }
            Warm(kind);
            if (++n % perFrame == 0) {
                yield return null;
            }
        }
        Fx.Impact(new Vector3(0f, -400f, 0f), 1f, true, Vector3.forward);
        yield return null;
        Destroy(gameObject);
    }

    static void Warm(BotKind kind) {
        float t = Time.realtimeSinceStartup;
        try {
            var go = BotArt.Make(kind, Color.white, 0f);
            if (go != null) {
                go.transform.position = new Vector3(0f, -400f, 0f);
                Destroy(go);
            }
        } catch (System.Exception) {
        }
        LongestWarmFrameMs = Mathf.Max(LongestWarmFrameMs, (Time.realtimeSinceStartup - t) * 1000f);
    }
}

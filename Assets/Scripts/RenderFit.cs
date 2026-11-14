using System.Runtime.InteropServices;
using UnityEngine;

// The picture is the right shape on every window and phone: the canvas's backing store has the same aspect as its CSS box
// (Tools/Web/hawkes-canvas-fit.js makes it, capped at 2x), and every screen camera follows the backing store's aspect.
// This keeps the second half true and measures both for the playtest.
//   render.aspectError  how far the camera's aspect is from the canvas backing aspect, and the backing aspect from the CSS
//                       box's aspect (the larger), as a fraction. Under 0.01 is round things round.
//   render.cropPx       CSS pixels of the canvas that the visitor cannot see (the box runs past the viewport) plus the
//                       device pixels of the screen no camera draws to (a camera rect that is not the whole screen).
// A screen camera (no render texture) that drifts off the screen's aspect or rect, say after a resize or a cinematic, is put
// back by ResetAspect and a full rect every half second and on every size change, so a stretched frame never lasts.
public class RenderFit : MonoBehaviour {
    [System.Serializable]
    class PageInfo {
        public float cssW, cssH, left, top, right, bottom, backW, backH, viewW, viewH, dpr;
    }

    public struct Sample {
        public bool page;           // the browser told us about the canvas (false in the editor)
        public float aspectError;
        public float cameraError;   // camera against backing
        public float pageError;     // backing against CSS box
        public float cropPx;
        public float screenAspect, cameraAspect, cssAspect;
        public int screenW, screenH, backW, backH;
        public float cssW, cssH, viewW, viewH, dpr, renderScale;
        public string camera;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern string HawkesRenderInfo();
#else
    static string HawkesRenderInfo() {
        return "";
    }
#endif

    static RenderFit instance;
    int stampSeen = -1;
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        if (instance != null) {
            return;
        }
        var go = new GameObject("Render Fit");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        instance = go.AddComponent<RenderFit>();
    }

    void LateUpdate() {
        int stamp = UiFit.Stamp;
        if (stamp != stampSeen || Time.unscaledTime >= nextCheck) {
            stampSeen = stamp;
            nextCheck = Time.unscaledTime + 0.5f;
            Correct();
        }
    }

    // Every screen camera draws the whole screen at the screen's own aspect.
    static void Correct() {
        if (Screen.width <= 0 || Screen.height <= 0) {
            return;
        }
        float want = (float)Screen.width / Screen.height;
        var full = new Rect(0f, 0f, 1f, 1f);
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++) {
            Camera c = cams[i];
            if (c == null || c.targetTexture != null || c.cameraType != CameraType.Game) {
                continue;
            }
            if (c.rect != full) {
                c.rect = full;
            }
            if (Mathf.Abs(c.aspect / want - 1f) > 0.002f) {
                c.ResetAspect();
            }
        }
    }

    // The camera that draws the frame: the main one, else the deepest screen camera.
    static Camera Main() {
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled && main.targetTexture == null) {
            return main;
        }
        Camera best = null;
        foreach (Camera c in Camera.allCameras) {
            if (c != null && c.targetTexture == null && (best == null || c.depth > best.depth)) {
                best = c;
            }
        }
        return best;
    }

    public static Sample Measure() {
        var s = new Sample { screenW = Screen.width, screenH = Screen.height };
        s.screenAspect = s.screenH > 0 ? (float)s.screenW / s.screenH : 1f;
        Camera cam = Main();
        s.camera = cam != null ? cam.name : "none";
        s.cameraAspect = cam != null ? cam.aspect : s.screenAspect;
        s.backW = s.screenW;
        s.backH = s.screenH;
        s.cssAspect = s.screenAspect;
        s.cssW = s.screenW;
        s.cssH = s.screenH;
        s.viewW = s.screenW;
        s.viewH = s.screenH;
        s.dpr = 1f;
        PageInfo info = null;
        string json = HawkesRenderInfo();
        if (!string.IsNullOrEmpty(json)) {
            try {
                info = JsonUtility.FromJson<PageInfo>(json);
            } catch (System.Exception) {
                info = null;
            }
        }
        float crop = 0f;
        if (info != null && info.cssW > 0f && info.cssH > 0f && info.backW > 0f && info.backH > 0f) {
            s.page = true;
            s.cssW = info.cssW;
            s.cssH = info.cssH;
            s.viewW = info.viewW;
            s.viewH = info.viewH;
            s.dpr = info.dpr;
            s.backW = Mathf.RoundToInt(info.backW);
            s.backH = Mathf.RoundToInt(info.backH);
            s.cssAspect = info.cssW / info.cssH;
            s.renderScale = info.backW / info.cssW;
            float backAspect = info.backW / info.backH;
            s.pageError = Mathf.Abs(backAspect / s.cssAspect - 1f);
            // Unity may still be a frame behind the page's backing store: the screen it reports is what it draws to.
            s.cameraError = Mathf.Abs(s.cameraAspect / backAspect - 1f);
            // The visitor sees the viewport: whatever of the box lies past its right or bottom edge, or before its left or top, is cut.
            crop += Mathf.Max(0f, info.right - info.viewW) + Mathf.Max(0f, info.bottom - info.viewH);
            crop += Mathf.Max(0f, -info.left) + Mathf.Max(0f, -info.top);
        } else {
            s.cameraError = Mathf.Abs(s.cameraAspect / s.screenAspect - 1f);
        }
        if (cam != null) {
            // The part of the screen no camera draws: a camera rect or pixel rect that is not the whole screen.
            crop += Mathf.Max(0f, s.screenW - cam.pixelWidth) + Mathf.Max(0f, s.screenH - cam.pixelHeight);
        }
        s.aspectError = Mathf.Max(s.cameraError, s.pageError);
        s.cropPx = Mathf.Round(crop);
        return s;
    }

    public static string Describe(Sample s) {
        return s.screenW + "x" + s.screenH + " backing, " + s.cssW.ToString("0") + "x" + s.cssH.ToString("0") + " css in a " + s.viewW.ToString("0") + "x" + s.viewH.ToString("0") + " viewport (dpr " + s.dpr.ToString("0.##") + ", render scale " + s.renderScale.ToString("0.##") + "), camera " + s.camera + " aspect " + s.cameraAspect.ToString("0.000") + " vs " + s.screenAspect.ToString("0.000") + ": aspectError " + s.aspectError.ToString("0.0000") + ", cropPx " + s.cropPx.ToString("0") + (s.page ? "" : " (no page)");
    }
}

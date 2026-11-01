using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Hawkes
{
    /// <summary>
    /// Hawkes Analytics for Unity WebGL (Hawkes Gen's docs/analytics.md): the same API as the page's
    /// window.hawkesAnalytics. Props are a dictionary or a JSON object string. It starts itself from
    /// Resources/HawkesAnalyticsSettings and loads the SDK into the page when the page hasn't. In the Editor and
    /// outside WebGL it sends nothing (it logs when the settings ask for it). Nothing here throws.
    /// </summary>
    public static class HawkesAnalytics
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HawkesAnalytics_Init(string product, string sdkUrl, string endpoint, string version, bool debug);
        [DllImport("__Internal")] static extern void HawkesAnalytics_Call(string method, string argsJson);
        static readonly bool Live = true;
#else
        static void HawkesAnalytics_Init(string product, string sdkUrl, string endpoint, string version, bool debug) { }
        static void HawkesAnalytics_Call(string method, string argsJson) { }
        static readonly bool Live = false;
#endif

        static bool started;
        static bool log;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoStart()
        {
            started = false;
            var s = HawkesAnalyticsSettings.Load();
            if (s != null) Init(s);
        }

        /// <summary>Start with these settings (done for you when Resources/HawkesAnalyticsSettings exists).</summary>
        public static void Init(HawkesAnalyticsSettings s)
        {
            if (s == null) return;
            log = s.logOutsideWebGL;
            if (s.captureExceptions && !started)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
            }
            Init(s.product, s.sdkUrl, s.endpoint, s.debug);
        }

        /// <summary>Start without a settings asset. The page's HAWKES_BUILD, when it sets one, wins over Application.version.</summary>
        public static void Init(string product, string sdkUrl = "https://hawkesgen.ai/sdk/analytics@1.js", string endpoint = "", bool debug = false)
        {
            started = true;
            if (!Live) { Log("init", product); return; }
            try { HawkesAnalytics_Init(product ?? "", sdkUrl ?? "", endpoint ?? "", Application.version ?? "", debug); }
            catch (Exception e) { Debug.LogWarning("[HawkesAnalytics] " + e.Message); }
        }

        // ------------------------------------------------------------ the game's API (docs/analytics.md, standard events)
        /// <summary>A custom event: name like "shop_purchase" (^[a-z][a-z0-9_]{1,39}$). Prefer Feature when it fits.</summary>
        public static void Track(string name, IDictionary<string, object> props = null) => Call("track", name, props);
        public static void Track(string name, string propsJson) => Call("track", name, new RawJson(propsJson));
        public static void Screen(string name) => Call("screen", name);
        /// <summary>Merged into every event (level, mode, character, difficulty…); a null value removes the key.</summary>
        public static void SetContext(IDictionary<string, object> context) => Call("setContext", context);
        public static void SetContext(string key, object value) => Call("setContext", new Dictionary<string, object> { { key, value } });
        public static void LoadStart() => Call("loadStart");
        /// <summary>The player can play (the title screen is interactive).</summary>
        public static void Ready() => Call("ready");
        public static void LevelStart(string level, IDictionary<string, object> props = null) => Call("levelStart", level, props);
        /// <summary>result: complete, death, quit or restart. Duration and deaths are counted for you.</summary>
        public static void LevelEnd(string level, string result = "complete", IDictionary<string, object> props = null) => Call("levelEnd", level, result, props);
        public static void Death(string cause, IDictionary<string, object> props = null) => Call("death", cause, props);
        public static void Choice(string node, string option) => Call("choice", node, option);
        public static void Feature(string name, IDictionary<string, object> props = null) => Call("feature", name, props);
        /// <summary>action: play, complete or skip; at: seconds into it.</summary>
        public static void Video(string id, string action, float at = 0) => Call("video", id, action, at);
        public static void Feedback(int stars, string text = null, string context = null) => Call("feedback", Fb(stars, text, context));
        public static void Feedback(bool thumbsUp, string text = null, string context = null) => Call("feedback", Fb(thumbsUp ? "up" : "down", text, context));
        /// <summary>The feedback card, at a natural pause (after a level, a death screen, an ending), never mid-play.</summary>
        public static void Ask(string id, string question, string context = null, bool stars = false, bool comment = false) =>
            Call("ask", new Dictionary<string, object> { { "id", id }, { "question", question }, { "context", context }, { "kind", stars ? "stars" : "thumbs" }, { "comment", comment } });
        public static void Error(string message, string stack = null, string source = "unity") =>
            Call("error", new Dictionary<string, object> { { "message", message }, { "stack", stack } }, source);
        public static void OptOut() => Call("optOut");
        public static void OptIn() => Call("optIn");
        public static void Flush() => Call("flush");

        static Dictionary<string, object> Fb(object rating, string text, string context) =>
            new Dictionary<string, object> { { "rating", rating }, { "text", text }, { "context", context } };

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) Error(message, stack);
        }

        static void Call(string method, params object[] args)
        {
            try
            {
                // Trailing nulls are left out so the SDK sees "no props", not null.
                int n = args.Length;
                while (n > 0 && args[n - 1] == null) n--;
                var sb = new StringBuilder("[");
                for (int i = 0; i < n; i++) { if (i > 0) sb.Append(','); Json(sb, args[i], 0); }
                sb.Append(']');
                if (!Live) { Log(method, sb.ToString()); return; }
                HawkesAnalytics_Call(method, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[HawkesAnalytics] " + method + ": " + e.Message); }
        }

        static void Log(string method, string args)
        {
            if (log) Debug.Log("[HawkesAnalytics] " + method + " " + args);
        }

        // ------------------------------------------------------------ a small JSON writer (no dependencies)
        sealed class RawJson
        {
            public readonly string Json;
            public RawJson(string json) { Json = string.IsNullOrWhiteSpace(json) ? "null" : json; }
        }

        static void Json(StringBuilder sb, object v, int depth)
        {
            if (depth > 6) { sb.Append("null"); return; }
            switch (v)
            {
                case null: sb.Append("null"); return;
                case RawJson raw: sb.Append(raw.Json); return;
                case string s: Str(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case float f: Num(sb, f); return;
                case double d: Num(sb, d); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case int or long or short or byte or uint or ulong or ushort or sbyte: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return;
                case Enum e: Str(sb, e.ToString()); return;
                case Vector2 v2: sb.Append("{\"x\":"); Num(sb, v2.x); sb.Append(",\"y\":"); Num(sb, v2.y); sb.Append('}'); return;
                case Vector3 v3: sb.Append("{\"x\":"); Num(sb, v3.x); sb.Append(",\"y\":"); Num(sb, v3.y); sb.Append(",\"z\":"); Num(sb, v3.z); sb.Append('}'); return;
                case IDictionary dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Str(sb, Convert.ToString(kv.Key, CultureInfo.InvariantCulture));
                        sb.Append(':');
                        Json(sb, kv.Value, depth + 1);
                    }
                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var x in list) { if (!first) sb.Append(','); first = false; Json(sb, x, depth + 1); }
                    sb.Append(']');
                    return;
                }
                default: Str(sb, v.ToString()); return;
            }
        }

        static void Num(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
            else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}

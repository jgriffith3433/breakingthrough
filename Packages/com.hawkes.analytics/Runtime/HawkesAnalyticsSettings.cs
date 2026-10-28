using UnityEngine;

namespace Hawkes
{
    /// <summary>
    /// The game's analytics settings. Create one with Assets > Create > Hawkes > Analytics Settings and keep it at
    /// Assets/Resources/HawkesAnalyticsSettings.asset: HawkesAnalytics starts itself from it when the game boots.
    /// </summary>
    [CreateAssetMenu(menuName = "Hawkes/Analytics Settings", fileName = "HawkesAnalyticsSettings")]
    public class HawkesAnalyticsSettings : ScriptableObject
    {
        [Tooltip("The product id in Hawkes Gen's analytics/products.json (sickofit, yoobaland, theplacedepartment…).")]
        public string product = "";

        [Tooltip("Where the page loads the SDK from when it hasn't loaded it itself.")]
        public string sdkUrl = "https://gen.hawkesgen.ai/sdk/analytics@1.js";

        [Tooltip("Leave empty for the default (https://gen.hawkesgen.ai/v1/analytics/collect).")]
        public string endpoint = "";

        [Tooltip("Log each event in the browser console and keep them on window.hawkesAnalytics.debugLog.")]
        public bool debug = false;

        [Tooltip("Send uncaught exceptions as error events (source: unity).")]
        public bool captureExceptions = true;

        [Tooltip("In the Editor and outside WebGL nothing is sent; log each call to the Console instead.")]
        public bool logOutsideWebGL = false;

        public const string ResourceName = "HawkesAnalyticsSettings";

        public static HawkesAnalyticsSettings Load() => Resources.Load<HawkesAnalyticsSettings>(ResourceName);
    }
}

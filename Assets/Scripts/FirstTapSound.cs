using UnityEngine;

// The first tap on the page's "Tap to play" card is the first moment the browser lets the game make sound, so the game
// answers it at once: a bat whoosh, a hit thunk and a coin ding (the bat's own sounds, in a quick count of three), and
// the menu's music bed starts under them. Fires once per page load, only on a page that had the card.
public class FirstTapSound : MonoBehaviour {
    public static int Fired { get; private set; }
    public static float FiredAt { get; private set; } = -1f;
    public static readonly System.Collections.Generic.List<string> Events = new System.Collections.Generic.List<string>();

    bool seenPrompt;
    bool done;
    float tapAt = -1f;
    int step;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (FindAnyObjectByType<FirstTapSound>() != null) {
            return;
        }
        var go = new GameObject("First Tap Sound");
        DontDestroyOnLoad(go);
        go.AddComponent<FirstTapSound>();
    }

    void Update() {
        if (done) {
            return;
        }
        if (tapAt < 0f) {
            if (SoundSettings.TapPromptUp) {
                seenPrompt = true;
                return;
            }
            if (!seenPrompt) {
                // No card on this page (editor, a page that was tapped before Unity loaded): nothing to answer.
                if (Time.realtimeSinceStartup > 5f) {
                    done = true;
                }
                return;
            }
            tapAt = Time.unscaledTime;
            Fired++;
            FiredAt = tapAt;
            // Under a menu the bed is the menu's own theme; under a run the act score owns the music.
            MainMenu menu = FindAnyObjectByType<MainMenu>();
            if (MusicDirector.Current.Length == 0 && menu != null && menu.Ready) {
                MusicDirector.Play("theme_wallit");
                Events.Add("music-bed|theme_wallit");
            }
        }
        float t = Time.unscaledTime - tapAt;
        if (step == 0) {
            GameAudio.PlayUI("batswing", 0.7f, 1f);
            Events.Add("whoosh|batswing");
            step = 1;
        } else if (step == 1 && t >= 0.16f) {
            GameAudio.PlayUI("batcrack", 0.65f, 0.9f);
            Events.Add("thunk|batcrack");
            step = 2;
        } else if (step == 2 && t >= 0.34f) {
            GameAudio.PlayUI("coin", 0.7f, 1.1f);
            Events.Add("ding|coin");
            step = 3;
            done = true;
        }
    }
}

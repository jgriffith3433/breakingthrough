using UnityEngine;
using UnityEngine.UI;

// Generated UI art (Docs/UiArt.md): the title logo, the panel frame and the icons. Every piece is an optional
// Resources/UiArt/<name>.png made through Hawkes Gen. Each getter returns null when the file is missing, and every
// caller keeps its code-drawn version as the fallback, so the game looks right with none of the art present.
public static class UiArt {
    const string Folder = "UiArt/";
    static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();

    public static Sprite Logo { get { return Load("logo", 0f); } }

    // A grey-silver frame with a flat middle, nine-sliced; tinted by the caller. 16% of its size is border.
    public static Sprite Frame { get { return Load("panel_frame", 0.16f); } }

    public static Sprite Boon(BoonId id) { return Load("boon_" + id.ToString().ToLowerInvariant(), 0f); }

    public static Sprite Upgrade(UpgradeId id) { return Load("upgrade_" + id.ToString().ToLowerInvariant(), 0f); }

    // Ability icons are by name ("refund"), see CampaignProgress.Abilities.
    public static Sprite Ability(string name) { return Load("ability_" + name.ToLowerInvariant().Replace(' ', '_'), 0f); }

    static Sprite Load(string name, float border) {
        Sprite sprite;
        if (cache.TryGetValue(name, out sprite)) {
            return sprite;
        }
        Texture2D tex = Resources.Load<Texture2D>(Folder + name);
        if (tex != null) {
            tex.wrapMode = TextureWrapMode.Clamp;
            float b = Mathf.Round(Mathf.Min(tex.width, tex.height) * border);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
        if (sprite != null) {
            sprite.name = Folder + name;
        }
        cache[name] = sprite;
        return sprite;
    }

    // Puts an icon on an Image: it keeps its shape, and the Image hides when there is no art.
    public static bool Show(Image image, Sprite sprite) {
        if (image == null) {
            return false;
        }
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return sprite != null;
    }
}

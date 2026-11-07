using UnityEngine;

// The one "use this" of the campaign: E on a keyboard, the Interact button on a phone or tablet (TouchControls taps E on
// PlayInput's pad channel, so every place that reads E needs no touch code). Anything the player can use calls Offer each
// frame it is in reach; TouchControls shows the Interact button while an offer stands. Prompts are written through Prompt, so
// they say "E: ..." on a keyboard and "Tap: ..." on touch.
public static class Interact {
    static int offeredFrame = -10;
    static string offeredVerb = "";

    // "E" on a keyboard, "Tap" on touch.
    public static string Key {
        get { return UiFit.Touch ? "Tap" : "E"; }
    }

    // "E: talk to Slice" or "Tap: talk to Slice".
    public static string Prompt(string verb) {
        return Key + ": " + verb;
    }

    // Something usable is in reach this frame. Returns the verb unchanged so a caller can chain it.
    public static string Offer(string verb) {
        offeredFrame = Time.frameCount;
        offeredVerb = verb;
        return verb;
    }

    // Is anything usable in reach (offered this frame or last, so the order of the scripts does not matter)?
    public static bool Offered {
        get { return Time.frameCount - offeredFrame <= 1; }
    }

    public static string Verb {
        get { return Offered ? offeredVerb : ""; }
    }

    // E, pad A, Enter or the Interact button went down this frame.
    public static bool Pressed {
        get {
            return PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.JoystickButton0)
                || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter);
        }
    }
}

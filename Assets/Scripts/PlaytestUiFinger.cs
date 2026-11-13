using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// The playtest bot's finger on a menu or card button (phone viewport). TouchControls.BotFinger only feeds the in-game
// stick and pads, and TouchControls is not even alive in the main menu, so a "tap" made with it never reached a Unity UI
// button (the opening-scene bot waited 10 s for case files that nothing had asked for). This finger does what a real
// touch does: it raycasts the screen point through the EventSystem, so a button that is off screen, covered or
// misplaced is missed, and presses whatever handler sits under it (down, up, click).
public static class PlaytestUiFinger {
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    // True when something clickable was under the point and got the press.
    public static bool Press(Vector2 screen) {
        EventSystem system = EventSystem.current;
        if (system == null) {
            return false;
        }
        var data = new PointerEventData(system) { button = PointerEventData.InputButton.Left, position = screen, pressPosition = screen };
        hits.Clear();
        system.RaycastAll(data, hits);
        foreach (RaycastResult hit in hits) {
            data.pointerCurrentRaycast = hit;
            data.pointerPressRaycast = hit;
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (target == null) {
                continue;
            }
            data.pointerPress = target;
            data.pointerPressRaycast = hit;
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
            return true;
        }
        return false;
    }

    // A tap held for a moment (so the pressed state can show), then lifted.
    public static IEnumerator Tap(Vector2 screen, string what) {
        if (!Press(screen)) {
            Debug.Log("[Playtest] finger at " + screen + " hit no button (" + what + ")");
        }
        yield return new WaitForSecondsRealtime(0.05f);
    }
}

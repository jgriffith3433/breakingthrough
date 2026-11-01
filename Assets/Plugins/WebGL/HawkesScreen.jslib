// What the browser knows about the screen that Unity doesn't say: the pixel ratio (Screen.width is in device
// pixels) and whether the main pointer is a finger. Read by UiFit.
mergeInto(LibraryManager.library, {
  HawkesPixelRatio: function () {
    return window.devicePixelRatio || 1;
  },

  // 1 while the game holds the mouse (pointer lock), 0 when it doesn't. Read by PauseMenu.
  HawkesPointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  },

  // Asking for pointer lock without a click or key just before (the run starts from a timer, not a click) makes the
  // browser reject the request's promise, and nothing catches it. The game asks again every frame and the lock lands
  // on the player's first click, so the rejection is expected: catch it so it is not an unhandled error.
  HawkesGuardPointerLock: function () {
    try {
      var proto = Element.prototype;
      if (proto.requestPointerLock && !proto.requestPointerLock.hawkesGuarded) {
        var original = proto.requestPointerLock;
        var guarded = function () {
          var result = original.apply(this, arguments);
          if (result && typeof result.catch === "function") {
            result.catch(function () {});
          }
          return result;
        };
        guarded.hawkesGuarded = true;
        proto.requestPointerLock = guarded;
      }
    } catch (e) {}
  },

  HawkesCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});

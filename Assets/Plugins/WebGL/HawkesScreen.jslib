// What the browser knows about the screen that Unity doesn't say: the pixel ratio (Screen.width is in device
// pixels) and whether the main pointer is a finger. Read by UiFit.
mergeInto(LibraryManager.library, {
  // Backing pixels per CSS pixel of the canvas as it really is (the page caps the render scale, see
  // Tools/Web/hawkes-canvas-fit.js), so Screen.width / this is the CSS width whatever the cap. Falls back to the device ratio.
  HawkesPixelRatio: function () {
    try {
      var c = document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (c && c.clientWidth > 0 && c.width > 0) {
        return c.width / c.clientWidth;
      }
    } catch (e) {}
    return window.devicePixelRatio || 1;
  },

  // The canvas as the page has it, as JSON: its CSS box, backing store, the viewport and the ratios. Read by RenderFit.
  HawkesRenderInfo: function () {
    var text = "";
    try {
      var c = document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (c) {
        var vv = window.visualViewport;
        var r = c.getBoundingClientRect();
        text = JSON.stringify({
          cssW: r.width, cssH: r.height, left: r.left, top: r.top, right: r.right, bottom: r.bottom,
          backW: c.width, backH: c.height,
          viewW: Math.floor(vv && vv.width ? vv.width : window.innerWidth),
          viewH: Math.floor(vv && vv.height ? vv.height : window.innerHeight),
          dpr: window.devicePixelRatio || 1
        });
      }
    } catch (e) {}
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
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

  // Seconds since the page itself started loading (performance.now()): unlike Time.realtimeSinceStartup it includes the
  // download and the unpacking before Unity runs. Read by the first-load playtest.
  HawkesPageSeconds: function () {
    try {
      return (window.performance && performance.now ? performance.now() : 0) / 1000;
    } catch (e) {
      return 0;
    }
  },

  HawkesCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});

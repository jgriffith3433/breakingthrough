// What the browser knows about the screen that Unity doesn't say: the pixel ratio (Screen.width is in device
// pixels) and whether the main pointer is a finger. Read by UiFit.
mergeInto(LibraryManager.library, {
  HawkesPixelRatio: function () {
    return window.devicePixelRatio || 1;
  },

  HawkesCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});

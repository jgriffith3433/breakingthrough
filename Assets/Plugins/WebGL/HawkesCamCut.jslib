mergeInto(LibraryManager.library, {
  // An intended camera cut (cutscene, act change, respawn): tells the playtest it is not a snap.
  HawkesCamCut: function () {
    try {
      if (window.hawkesPlaytest && typeof window.hawkesPlaytest.cut === "function") {
        window.hawkesPlaytest.cut();
      }
    } catch (e) {}
  }
});

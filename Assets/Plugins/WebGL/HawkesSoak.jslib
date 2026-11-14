// Soak playtest (PlaytestSoak.cs): the page's JS heap and a heartbeat Studio can read if the page stops answering.
// Harmless on a page without performance.memory (Firefox, Safari report 0).
mergeInto(LibraryManager.library, {
  HawkesJsHeapMb: function () {
    try {
      var m = window.performance && window.performance.memory;
      return m ? m.usedJSHeapSize / 1048576 : 0;
    } catch (e) { return 0; }
  },
  HawkesSoakBeat: function (minute) {
    try {
      window.hawkesSoakBeat = { minute: minute, at: Date.now() };
    } catch (e) {}
  }
});

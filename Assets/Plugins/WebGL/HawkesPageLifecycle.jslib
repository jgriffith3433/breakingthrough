// Tells Unity when the page is hidden or shown (a text arrives, the phone locks, the tab changes). Event driven: two
// listeners, no timers. A hide is reported once however many of visibilitychange / pagehide fire for it.
mergeInto(LibraryManager.library, {
  HawkesPageLifecycleInit: function (objectName) {
    var target = UTF8ToString(objectName);
    var hidden = false;
    function send(method, why) {
      try { SendMessage(target, method, why); } catch (e) { }
    }
    function hide(why) {
      if (hidden) { return; }
      hidden = true;
      send('OnPageHidden', why);
    }
    function show(why) {
      if (!hidden) { return; }
      hidden = false;
      send('OnPageShown', why);
    }
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') { hide('visibilitychange'); } else { show('visibilitychange'); }
    });
    window.addEventListener('pagehide', function () { hide('pagehide'); });
    window.addEventListener('pageshow', function () { show('pageshow'); });
  },

  // One flush of the browser's file system (where PlayerPrefs lives) to IndexedDB.
  HawkesFsSync: function () {
    try {
      if (typeof FS !== 'undefined' && FS.syncfs) { FS.syncfs(false, function () { }); }
    } catch (e) { }
  }
});

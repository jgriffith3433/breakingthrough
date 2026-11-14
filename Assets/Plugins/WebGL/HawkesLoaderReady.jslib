// Page side of RecordsCard: clears the page's "Opening your file..." card once the first scene is live.
mergeInto(LibraryManager.library, {
  HawkesLoaderReady: function () {
    try {
      if (typeof window !== "undefined" && typeof window.hawkesGameReady === "function") {
        window.hawkesGameReady();
      }
    } catch (e) {}
  }
});

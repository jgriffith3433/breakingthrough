// First-minute milestones (gameplay, first-input-accepted, first-success) for Hawkes Studio's playtest bridge.
// Tells window.hawkesPlaytest.milestone(name, note) if the page has it; harmless on a page without it.
mergeInto(LibraryManager.library, {
  HawkesMilestone: function (namePtr, notePtr) {
    try {
      var name = UTF8ToString(namePtr);
      var note = UTF8ToString(notePtr);
      var t = window.hawkesPlaytest;
      if (t && typeof t.milestone === 'function') {
        t.milestone(name, note);
      }
    } catch (e) {}
  }
});

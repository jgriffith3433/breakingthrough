// Tells the playtest audio probe (hawkes-audio.js) about every sound GameAudio starts: a "hawkes-sound" window event
// carrying {type:"sound-start", sample, bus, rate, gain, dur} (the same shape as HawkesSound.cs). Pushed, never polled.
mergeInto(LibraryManager.library, {
  HawkesSoundProbeEmit: function (jsonPtr) {
    try {
      window.dispatchEvent(new CustomEvent("hawkes-sound", { detail: JSON.parse(UTF8ToString(jsonPtr)) }));
    } catch (e) {}
  },
});

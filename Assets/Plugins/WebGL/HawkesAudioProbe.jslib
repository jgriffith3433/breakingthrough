// The playtest audio probe's way into Unity's WebAudio (hawkes-audio.js lives on the page and cannot see FMOD's context).
// HawkesSoundProbeEmit: a sound-start from GameAudio, sent to the probe as a "hawkes-sound" window event; every call also
// re-checks that Unity's own AudioContext is announced and its output (everything that reaches the speakers) is copied into
// the probe's recorder tap (ctx.__hawkesAudioTap, made by hawkes-audio.js), so integrated loudness and true peak are measured
// on the real mix. HawkesAudioExpose does the same with no event (called once at start).
mergeInto(LibraryManager.library, {
  HawkesAudioExpose: function () {
    try {
      var w = window;
      var ctx = (typeof WEBAudio !== "undefined" && WEBAudio && WEBAudio.audioContext) ? WEBAudio.audioContext : null;
      if (!ctx) return;
      if (!w.__hawkesAudioHooked) {
        w.__hawkesAudioHooked = true;
        // Whatever Unity connects to the speakers is also kept, so it can be tapped when the probe's recorder exists.
        var proto = (w.AudioNode && w.AudioNode.prototype) || null;
        if (proto && !proto.__hawkesConnectWrapped) {
          var orig = proto.connect;
          proto.connect = function (target) {
            try {
              var c = this.context;
              if (c && target && target === c.destination && this !== c.__hawkesAudioTap) {
                c.__hawkesDestSources = c.__hawkesDestSources || [];
                if (c.__hawkesDestSources.indexOf(this) < 0) c.__hawkesDestSources.push(this);
                if (c.__hawkesAudioTap) orig.call(this, c.__hawkesAudioTap);
              }
            } catch (e) {}
            return orig.apply(this, arguments);
          };
          proto.__hawkesConnectWrapped = true;
        }
      }
      w.__hawkesAudioContext = ctx;
      w.__hawkesSoundContexts = w.__hawkesSoundContexts || [];
      if (w.__hawkesSoundContexts.indexOf(ctx) < 0) {
        w.__hawkesSoundContexts.push(ctx);
        w.dispatchEvent(new CustomEvent("hawkes-sound", { detail: { type: "context", ctx: ctx } }));
      }
      // The recorder tap exists now: feed it from everything already wired to the speakers.
      if (ctx.__hawkesAudioTap && ctx.__hawkesDestSources && !ctx.__hawkesTapFed) {
        ctx.__hawkesTapFed = true;
        ctx.__hawkesDestSources.forEach(function (n) { try { n.connect(ctx.__hawkesAudioTap); } catch (e) {} });
      }
    } catch (e) {}
  },

  HawkesSoundProbeEmit__deps: ["HawkesAudioExpose"],
  HawkesSoundProbeEmit: function (jsonPtr) {
    try {
      _HawkesAudioExpose();
      window.dispatchEvent(new CustomEvent("hawkes-sound", { detail: JSON.parse(UTF8ToString(jsonPtr)) }));
    } catch (e) {}
  },
});

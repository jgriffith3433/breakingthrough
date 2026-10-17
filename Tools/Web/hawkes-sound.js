// The Hawkes "Tap to play" prompt (the studio's sound standard). WebGlBuild copies this file next to the site's
// Game/index.html on every deploy and puts a <script> for it in the page's <head>, so it runs before Unity's loader.
//
// Browsers (iOS above all) keep Web Audio silent until the player touches the page. The prompt covers the page while
// Unity loads underneath; the tap that closes it resumes Unity's AudioContext and primes an audio element, inside the
// gesture, as iOS requires. If Unity hasn't made its AudioContext yet, it is resumed as soon as it exists.
// Audio resumes again when the tab comes back, after an iOS interruption (a call, the lock screen) and on any later
// touch, click or key.
//
// window.hawkesSound: tap() closes the prompt (the playtest bot presses it through the same handler), state() says
// "prompt", "suspended", "running" or "none" (no Web Audio yet), attach(ctx) is called from Unity with WEBAudio's
// context (Plugins/WebGL/HawkesSound.jslib).
(function () {
  var w = window;
  var d = document;
  if (w.hawkesSound) {
    return;
  }

  var SILENCE_WAV = "data:audio/wav;base64,UklGRigAAABXQVZFZm10IBIAAAABAAEARKwAAIhYAQACABAAAABkYXRhAgAAAAEA";
  var Native = w.AudioContext || w.webkitAudioContext;
  var contexts = [];
  var tapped = false;
  var silent = null;
  var prompt = null;

  function track(ctx) {
    if (!ctx || contexts.indexOf(ctx) >= 0) {
      return ctx;
    }
    contexts.push(ctx);
    try {
      // iOS reports "interrupted" after a call or the lock screen; Safari can also drop back to "suspended".
      ctx.addEventListener("statechange", function () {
        if (tapped && ctx.state !== "running" && ctx.state !== "closed" && !d.hidden) {
          resume();
        }
      });
    } catch (e) {}
    return ctx;
  }

  // Every AudioContext the page makes is tracked, so the tap can resume it. Unity always gets a context of its own,
  // never one already running: it only marks its audio as running from the context's statechange event, so a context
  // that was resumed before Unity took it would leave every sound queued for good.
  if (Native) {
    var Wrapped = function (options) {
      var ctx = track(options === undefined ? new Native() : new Native(options));
      if (tapped) {
        resume();
      }
      return ctx;
    };
    Wrapped.prototype = Native.prototype;
    try {
      Object.setPrototypeOf(Wrapped, Native);
    } catch (e) {}
    if (w.AudioContext) {
      w.AudioContext = Wrapped;
    }
    if (w.webkitAudioContext) {
      w.webkitAudioContext = Wrapped;
    }
  }

  function resume() {
    for (var i = 0; i < contexts.length; i++) {
      var ctx = contexts[i];
      if (ctx.state !== "running" && ctx.state !== "closed") {
        try {
          var p = ctx.resume();
          if (p && p.catch) {
            p.catch(function () {});
          }
        } catch (e) {}
      }
    }
  }

  // A one-sample silent buffer through each context: what makes older iOS really start the output.
  function kick() {
    for (var i = 0; i < contexts.length; i++) {
      try {
        var ctx = contexts[i];
        var source = ctx.createBufferSource();
        source.buffer = ctx.createBuffer(1, 1, 22050);
        source.connect(ctx.destination);
        source.start(0);
      } catch (e) {}
    }
  }

  function primeElement() {
    try {
      if (!silent) {
        silent = new Audio(SILENCE_WAV);
        silent.preload = "auto";
        silent.muted = true;
        silent.setAttribute("playsinline", "true");
      }
      silent.currentTime = 0;
      var p = silent.play();
      if (p && p.then) {
        p.then(function () {
          silent.pause();
        }, function () {});
      }
    } catch (e) {}
  }

  function unlock() {
    resume();
    kick();
    primeElement();
  }

  function tap() {
    unlock();
    if (tapped) {
      return;
    }
    tapped = true;
    if (prompt && prompt.parentNode) {
      prompt.parentNode.removeChild(prompt);
    }
    var canvas = d.getElementById("unity-canvas");
    if (canvas) {
      try {
        canvas.focus();
      } catch (e) {}
    }
  }

  function state() {
    if (!tapped) {
      return "prompt";
    }
    if (contexts.length === 0) {
      return "none";
    }
    for (var i = 0; i < contexts.length; i++) {
      if (contexts[i].state === "running") {
        return "running";
      }
    }
    return "suspended";
  }

  w.hawkesSound = {
    tap: tap,
    state: state,
    attach: function (ctx) {
      track(ctx);
      if (tapped) {
        resume();
      }
    },
  };

  // Every later gesture tops the audio up too (Unity can make its context after the tap on a slow phone).
  function onGesture() {
    if (tapped) {
      unlock();
    }
  }
  w.addEventListener("pointerdown", onGesture, true);
  w.addEventListener("touchend", onGesture, { capture: true, passive: true });
  w.addEventListener("click", onGesture, true);
  w.addEventListener("keydown", onGesture, true);
  d.addEventListener("visibilitychange", function () {
    if (!d.hidden && tapped) {
      resume();
    }
  });
  w.addEventListener("pageshow", function () {
    if (tapped) {
      resume();
    }
  });
  w.addEventListener("focus", function () {
    if (tapped) {
      resume();
    }
  });

  function show() {
    if (tapped || prompt || !d.body) {
      return;
    }
    var style = d.createElement("style");
    style.textContent =
      "#hawkes-tap{position:fixed;top:0;left:0;right:0;bottom:0;z-index:1000;display:flex;flex-direction:column;align-items:center;" +
      "justify-content:center;gap:18px;width:100%;height:100%;margin:0;padding:24px;border:0;cursor:pointer;" +
      "background:radial-gradient(ellipse at 50% 40%,rgba(40,34,26,.94),rgba(8,8,12,.97) 70%);color:#f6eedf;" +
      "font-family:Georgia,'Times New Roman',serif;text-align:center;touch-action:manipulation;" +
      "-webkit-tap-highlight-color:transparent;-webkit-user-select:none;user-select:none}" +
      "#hawkes-tap .t{font-size:clamp(26px,6vmin,54px);letter-spacing:.04em}" +
      "#hawkes-tap .s{font-size:clamp(14px,2.4vmin,20px);font-style:italic;opacity:.7;max-width:34em}" +
      "#hawkes-tap .b{margin-top:12px;padding:16px 40px;min-height:56px;border-radius:999px;" +
      "background:#2f9e6b;color:#fff;font:bold clamp(20px,3.6vmin,30px) Arial,sans-serif;" +
      "box-shadow:0 0 0 0 rgba(47,158,107,.6);animation:hawkes-tap-breathe 2.4s ease-in-out infinite}" +
      "#hawkes-tap .n{font:clamp(12px,1.9vmin,15px) Arial,sans-serif;opacity:.55}" +
      "@keyframes hawkes-tap-breathe{50%{box-shadow:0 0 0 14px rgba(47,158,107,0)}}";
    d.head.appendChild(style);
    prompt = d.createElement("button");
    prompt.id = "hawkes-tap";
    prompt.type = "button";
    prompt.setAttribute("aria-label", "Tap to play");
    prompt.innerHTML =
      '<span class="t">The Place Department</span>' +
      '<span class="s">Where things that should not exist are given an address.</span>' +
      '<span class="b">Tap to play</span>' +
      '<span class="n">Sound comes on with your tap. Turn it down in Settings.</span>';
    // pointerup, not click: iOS Safari unlocks audio on touchend, and click can lag or be eaten by a scroll.
    prompt.addEventListener("pointerup", tap);
    prompt.addEventListener("touchend", function (e) {
      e.preventDefault();
      tap();
    });
    prompt.addEventListener("click", tap);
    d.body.appendChild(prompt);
    try {
      prompt.focus();
    } catch (e) {}
  }

  if (d.body) {
    show();
  } else {
    d.addEventListener("DOMContentLoaded", show);
  }
})();

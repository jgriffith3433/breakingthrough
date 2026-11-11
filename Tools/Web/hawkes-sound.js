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

  function hidePrompt() {
    // Also clear any stray #hawkes-tap left in the DOM (a second copy, or a failed earlier remove).
    var node = prompt || d.getElementById("hawkes-tap");
    prompt = null;
    if (!node) {
      return;
    }
    // Hidden first, removed second: if removal ever fails, the overlay is still gone.
    try {
      node.style.setProperty("display", "none", "important");
      node.style.setProperty("pointer-events", "none", "important");
      node.setAttribute("aria-hidden", "true");
    } catch (e) {}
    try {
      if (node.parentNode) {
        node.parentNode.removeChild(node);
      }
    } catch (e) {}
  }

  // The tap always closes the prompt and starts the game. Audio and focus are best effort: whatever AudioContext.resume,
  // the silent element or the canvas focus does (throw, reject, never answer), the overlay goes first and stays gone.
  function tap() {
    var first = !tapped;
    tapped = true;
    hidePrompt();
    detachDismiss();
    if (first) {
      try {
        console.log("overlay dismissed");
      } catch (e) {}
    }
    try {
      unlock();
    } catch (e) {
      try {
        console.warn("overlay dismissed; audio unlock failed: " + e);
      } catch (e2) {}
    }
    var canvas = d.getElementById("unity-canvas");
    if (canvas) {
      try {
        canvas.focus();
      } catch (e) {}
    }
  }

  // Beyond the button's own handlers: while the prompt is up, any click, tap, Enter or Space anywhere on the page
  // dismisses it, caught in the capture phase so nothing underneath can swallow it. A synthetic click on the page
  // lands here too.
  function onDismissEvent(e) {
    if (tapped) {
      return;
    }
    if (e.type === "keydown") {
      var k = e.key;
      if (k !== "Enter" && k !== " " && k !== "Spacebar" && e.code !== "Space" && e.code !== "Enter" && e.code !== "NumpadEnter") {
        return;
      }
    }
    tap();
  }
  var DISMISS = ["pointerup", "pointerdown", "mouseup", "touchend", "click", "keydown", "keyup"];
  function attachDismiss() {
    for (var i = 0; i < DISMISS.length; i++) {
      w.addEventListener(DISMISS[i], onDismissEvent, true);
      d.addEventListener(DISMISS[i], onDismissEvent, true);
    }
  }
  function detachDismiss() {
    for (var i = 0; i < DISMISS.length; i++) {
      w.removeEventListener(DISMISS[i], onDismissEvent, true);
      d.removeEventListener(DISMISS[i], onDismissEvent, true);
    }
  }
  attachDismiss();

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
    // Gameplay has started (Unity's menu is up or the run began): a running game is never covered.
    started: tap,
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

  // A gesture the page already had (before this script or the card existed) counts: the card is never shown then.
  function alreadyInteracted() {
    try {
      return !!(w.navigator && w.navigator.userActivation && w.navigator.userActivation.hasBeenActive);
    } catch (e) {
      return false;
    }
  }

  // One-time gate: once tapped, anything that tries to bring the card back (a second copy, a fullscreen toggle that
  // re-runs the page's code) is cleared at once. Entering or leaving fullscreen never shows it.
  function guard() {
    if (tapped) {
      hidePrompt();
    }
  }
  d.addEventListener("fullscreenchange", guard);
  d.addEventListener("webkitfullscreenchange", guard);
  w.addEventListener("resize", guard);

  function show() {
    if (tapped || prompt || !d.body) {
      return;
    }
    if (alreadyInteracted()) {
      tap();
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
    // aria-label starts with a first-five START match ("Tap to start"): Gen's cold play clicks that before it
    // treats the Unity canvas behind as gameplay. Visible label stays "Tap to play".
    prompt.setAttribute("aria-label", "Tap to start");
    // Six words: the first-five check counts every word a stranger sees in the first minute (limit 40), and the tagline
    // and the sound note are in the menu and Settings for anyone who goes looking.
    prompt.innerHTML =
      '<span class="t">The Place Department</span>' +
      '<span class="b">Tap to play</span>';
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

  function watch() {
    try {
      new MutationObserver(function () {
        if (tapped && d.getElementById("hawkes-tap")) {
          hidePrompt();
        }
      }).observe(d.body, { childList: true });
    } catch (e) {}
  }

  if (d.body) {
    show();
    watch();
  } else {
    d.addEventListener("DOMContentLoaded", function () {
      show();
      watch();
    });
  }
})();

// The page's "rotate your phone" hint: whatever the page or host shows, it must not sit on the HUD (Slice's bar, the
// campaign objective). When such a banner appears it becomes a small pill at the bottom edge with a close button, and
// goes away by itself after six seconds. It watches DOM additions (no polling).
(function () {
  var d = document;
  var HINT = /rotate|landscape|portrait|turn (your )?(phone|device)|sideways/i;

  function tidy(el) {
    if (!el || el.nodeType !== 1 || el.id === "hawkes-tap" || el.getAttribute("data-hawkes-hint")) {
      return;
    }
    var text = (el.textContent || "").trim();
    if (!text || text.length > 140 || !HINT.test(text) || /^(script|style|canvas)$/i.test(el.tagName)) {
      return;
    }
    el.setAttribute("data-hawkes-hint", "1");
    var s = el.style;
    s.setProperty("position", "fixed", "important");
    s.setProperty("top", "auto", "important");
    s.setProperty("bottom", "calc(6px + env(safe-area-inset-bottom, 0px))", "important");
    s.setProperty("left", "50%", "important");
    s.setProperty("right", "auto", "important");
    s.setProperty("transform", "translateX(-50%)", "important");
    s.setProperty("width", "auto", "important");
    s.setProperty("max-width", "min(60vw, 320px)", "important");
    s.setProperty("height", "auto", "important");
    s.setProperty("padding", "4px 30px 4px 10px", "important");
    s.setProperty("font-size", "12px", "important");
    s.setProperty("line-height", "1.3", "important");
    s.setProperty("border-radius", "14px", "important");
    s.setProperty("opacity", "0.85", "important");
    s.setProperty("z-index", "900", "important");
    var close = d.createElement("button");
    close.type = "button";
    close.setAttribute("aria-label", "Dismiss");
    close.textContent = "×";
    close.style.cssText = "position:absolute;top:0;right:0;width:28px;height:100%;min-height:24px;border:0;background:transparent;color:inherit;font-size:18px;cursor:pointer";
    function gone() {
      if (el.parentNode) {
        el.parentNode.removeChild(el);
      }
    }
    close.addEventListener("click", gone);
    el.appendChild(close);
    setTimeout(gone, 6000);
  }

  function scan(node) {
    tidy(node);
    if (node && node.querySelectorAll) {
      var all = node.querySelectorAll("div,p,span,aside,section");
      for (var i = 0; i < all.length; i++) {
        tidy(all[i]);
      }
    }
  }

  function start() {
    scan(d.body);
    new MutationObserver(function (list) {
      for (var i = 0; i < list.length; i++) {
        for (var j = 0; j < list[i].addedNodes.length; j++) {
          scan(list[i].addedNodes[j]);
        }
      }
    }).observe(d.body, { childList: true, subtree: true });
  }

  if (d.body) {
    start();
  } else {
    d.addEventListener("DOMContentLoaded", start);
  }
})();

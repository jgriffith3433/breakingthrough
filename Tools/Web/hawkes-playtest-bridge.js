// Installed next to the Game page on every deploy (WebGlBuild.InstallPlaytestBridge).
// 1. The page's stand-in window.hawkesPlaytest answers before Unity's bridge replaces it, so Studio read the
//    stand-in's four ids. This patches whatever object is assigned there: scenarios() returns every id the build
//    registers (scenarios.json, written by WebGlBuild next to this file) and start(id) waits for Unity's own bridge.
// 2. The "Turn sideways" portrait gate ("Play anyway" link) must never wall a player or a playtest in: it is dismissed by itself.
(function () {
  if (window.__hawkesPlaytestBridge) return;
  window.__hawkesPlaytestBridge = true;

  var me = document.currentScript && document.currentScript.src;
  var base = me ? me.replace(/[^\/]*$/, "") : "";
  var ids = null;
  var started = false;

  function load() {
    try {
      var x = new XMLHttpRequest();
      x.open("GET", base + "scenarios.json", false);
      x.send();
      if (x.status >= 200 && x.status < 300) {
        var d = JSON.parse(x.responseText);
        ids = d.scenarios || d;
      }
    } catch (e) {}
  }
  load();

  function shape(sample, id, descriptions) {
    if (sample && typeof sample === "object") return { id: id, name: id, description: (descriptions && descriptions[id]) || "" };
    return id;
  }

  var standIns = [];
  function patch(obj) {
    if (!obj || obj.__hawkesBridged || !ids || !ids.length) return obj;
    var orig = obj.scenarios, start = obj.start;
    if (typeof orig !== "function") return obj;
    obj.__hawkesBridged = true;
    standIns.push(obj);
    obj.scenarios = function () {
      var have = [];
      try { have = orig.apply(obj, arguments) || []; } catch (e) {}
      var seen = {};
      for (var i = 0; i < have.length; i++) seen[typeof have[i] === "string" ? have[i] : have[i] && (have[i].id || have[i].name)] = 1;
      var out = have.slice();
      for (var j = 0; j < ids.length; j++) if (!seen[ids[j]]) out.push(shape(have[0], ids[j]));
      return out;
    };
    if (typeof start === "function") {
      obj.start = function (id) {
        var args = arguments, self = this;
        var known = false;
        try { var h = orig.call(obj) || []; for (var i = 0; i < h.length; i++) if ((h[i].id || h[i].name || h[i]) === id) known = true; } catch (e) {}
        if (known) return start.apply(self, args);
        // Not the stand-in's own: wait for Unity's bridge (it replaces window.hawkesPlaytest) and hand over.
        return new Promise(function (resolve, reject) {
          var t0 = Date.now();
          (function wait() {
            var cur = window.hawkesPlaytest;
            if (cur && cur !== obj && !cur.__hawkesBridged && typeof cur.start === "function") {
              try { resolve(cur.start.apply(cur, args)); } catch (e) { reject(e); }
            } else if (Date.now() - t0 > 180000) {
              reject(new Error("Unity playtest bridge never loaded for " + id));
            } else setTimeout(wait, 250);
          })();
        });
      };
    }
    return obj;
  }

  var current = window.hawkesPlaytest;
  try {
    Object.defineProperty(window, "hawkesPlaytest", {
      configurable: true,
      get: function () { return current; },
      set: function (v) { current = patch(v); }
    });
    current = patch(current);
  } catch (e) {}

  // The portrait gate: press "Play anyway" (or "Play in portrait anyway") as soon as it shows (any visitor, playtest or not).
  // The page may build the gate late, or show it again by flipping a class or style on a node that is already there
  // (resize, orientationchange), so the watcher also reads attribute changes, and the resize/orientation events.
  // Whatever stays on screen is a real 44 px target, not a 20 px underlined link.
  function visible(el) {
    if (!el || !el.getClientRects) return false;
    var cs = window.getComputedStyle ? window.getComputedStyle(el) : null;
    if (cs && (cs.display === "none" || cs.visibility === "hidden")) return false;
    return el.getClientRects().length > 0;
  }
  function bigTarget(el) {
    if (el.getAttribute("data-hawkes-sized")) return;
    el.setAttribute("data-hawkes-sized", "1");
    var s = el.style;
    s.setProperty("display", "inline-flex", "important");
    s.setProperty("align-items", "center", "important");
    s.setProperty("justify-content", "center", "important");
    s.setProperty("min-width", "44px", "important");
    s.setProperty("min-height", "48px", "important");
    s.setProperty("padding", "12px 24px", "important");
    s.setProperty("margin", "8px", "important");
    s.setProperty("box-sizing", "border-box", "important");
    s.setProperty("border", "1px solid currentColor", "important");
    s.setProperty("border-radius", "10px", "important");
    s.setProperty("text-decoration", "none", "important");
    s.setProperty("cursor", "pointer", "important");
  }
  function press(el) {
    try {
      var r = el.getBoundingClientRect();
      var o = { bubbles: true, cancelable: true, view: window, clientX: r.left + r.width / 2, clientY: r.top + r.height / 2 };
      ["pointerdown", "mousedown", "pointerup", "mouseup"].forEach(function (n) {
        try { el.dispatchEvent(new (n.indexOf("pointer") === 0 && window.PointerEvent ? PointerEvent : MouseEvent)(n, o)); } catch (e) {}
      });
    } catch (e) {}
    try { el.click(); } catch (e) {}
  }
  // The full-screen layer the link sits in: the page's own gate, else the nearest fixed or absolute ancestor.
  function gateOf(el) {
    var g = el.closest("[id*=rotate],[id*=orient],[id*=gate],[class*=rotate],[class*=orient],[class*=gate],[role=dialog]");
    if (g) return g;
    for (var n = el.parentElement; n && n !== document.body && n !== document.documentElement; n = n.parentElement) {
      var pos = window.getComputedStyle(n).position;
      if (pos === "fixed" || pos === "absolute") g = n;
    }
    return g;
  }
  // The page words the link "Play anyway" (older builds: "Play in portrait anyway"); match both.
  var PLAY_ANYWAY = /^\s*play\s+(in\s+portrait(\s+anyway)?|anyway)\s*$/i;
  function dismissGate() {
    var list = document.querySelectorAll("button,a,[role=button]");
    var found = false;
    for (var i = 0; i < list.length; i++) {
      var el = list[i];
      var t = (el.textContent || el.getAttribute("aria-label") || "").trim();
      if (!PLAY_ANYWAY.test(t)) continue;
      found = true;
      var shown = visible(el);
      if (shown) bigTarget(el);
      // Press it once when it first appears, and again whenever the page shows the gate afresh.
      var n = +(el.getAttribute("data-hawkes-pressed") || 0);
      if ((shown || !n) && n < 20) {
        el.setAttribute("data-hawkes-pressed", String(n + 1));
        press(el);
        var gate = gateOf(el);
        if (gate && visible(gate)) gate.style.setProperty("display", "none", "important");
      }
    }
    return found;
  }
  function watch() {
    dismissGate();
    try {
      new MutationObserver(function () { dismissGate(); }).observe(document.documentElement, {
        childList: true, subtree: true, attributes: true, attributeFilter: ["class", "style", "hidden"]
      });
    } catch (e) {}
    window.addEventListener("resize", dismissGate);
    window.addEventListener("orientationchange", dismissGate);
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", watch); else watch();
})();

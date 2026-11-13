// Installed next to the Game page on every deploy (WebGlBuild.InstallPlaytestBridge).
// 1. The page's stand-in window.hawkesPlaytest answers before Unity's bridge replaces it, so Studio read the
//    stand-in's four ids. This patches whatever object is assigned there: scenarios() returns every id the build
//    registers (scenarios.json, written by WebGlBuild next to this file) and start(id) waits for Unity's own bridge.
// 2. The "reads better sideways" portrait gate must never wall a player or a playtest in: it is dismissed by itself.
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

  // The portrait gate: press "Play in portrait anyway" as soon as it shows (any visitor, playtest or not).
  function dismissGate() {
    var list = document.querySelectorAll("button,a,[role=button]");
    for (var i = 0; i < list.length; i++) {
      var t = (list[i].textContent || list[i].getAttribute("aria-label") || "").trim();
      if (/play in portrait( anyway)?/i.test(t)) {
        try { list[i].click(); } catch (e) {}
        var gate = list[i].closest("[id*=rotate],[id*=orient],[id*=gate],[class*=rotate],[class*=orient],[class*=gate],[role=dialog]");
        if (gate) gate.style.display = "none";
        return true;
      }
    }
    return false;
  }
  function watch() {
    dismissGate();
    try {
      new MutationObserver(function () { dismissGate(); }).observe(document.documentElement, { childList: true, subtree: true });
    } catch (e) {}
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", watch); else watch();
})();

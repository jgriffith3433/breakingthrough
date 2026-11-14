// Keeps the Unity canvas the right shape on every window and phone ("Arcade and the main menu render stretched and
// cropped", "the phone viewport renders at 2x and crops on the right").
//
// The rule: the canvas's CSS box is exactly the visible viewport (never wider, so nothing is cut off at the right),
// and its backing store is that box times a capped device pixel ratio, rounded, so the backing aspect equals the CSS
// aspect. A page that sizes the backing store by its own rules (canvas.width = CSS width, or the raw devicePixelRatio
// of 3 on a phone) and a Unity that sizes it by its own (matchWebGLToCanvasSize) used to disagree for a frame or for
// good, which shows as a stretched picture. Here the loader is told the capped ratio, and a MutationObserver puts
// the canvas back the moment anything else changes it. Installed next to the Game page on every deploy
// (WebGlBuild.InstallCanvasFit); edit it here, never in the website repo.
(function () {
  if (window.__hawkesCanvasFit) return;
  window.__hawkesCanvasFit = true;

  var CAP = 2; // render scale cap: a 3x phone draws 2x, which is crisp and keeps the frame rate
  var canvas = null;
  var fixing = false;

  function viewport() {
    var vv = window.visualViewport;
    var w = Math.floor(vv && vv.width ? vv.width : window.innerWidth);
    var h = Math.floor(vv && vv.height ? vv.height : window.innerHeight);
    return { w: Math.max(1, w), h: Math.max(1, h) };
  }

  function ratio() {
    return Math.max(1, Math.min(window.devicePixelRatio || 1, CAP));
  }

  function fit() {
    if (!canvas || fixing) return;
    fixing = true;
    try {
      var v = viewport();
      var r = ratio();
      var style = canvas.style;
      if (style.width !== v.w + "px") style.width = v.w + "px";
      if (style.height !== v.h + "px") style.height = v.h + "px";
      style.maxWidth = "none";
      style.display = "block";
      var bw = Math.round(v.w * r);
      var bh = Math.round(v.h * r);
      if (canvas.width !== bw) canvas.width = bw;
      if (canvas.height !== bh) canvas.height = bh;
    } finally {
      fixing = false;
    }
  }

  function attach(c) {
    if (!c || c === canvas) return;
    canvas = c;
    fit();
    try {
      new MutationObserver(fit).observe(canvas, { attributes: true, attributeFilter: ["width", "height", "style"] });
    } catch (e) {}
  }

  function find() {
    attach(document.querySelector("#unity-canvas") || document.querySelector("canvas"));
  }

  // What the page can read (and the playtest too): the CSS box, the backing store, the viewport, the ratio.
  window.hawkesCanvasFit = {
    cap: CAP,
    measure: function () {
      var c = canvas || document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (!c) return null;
      var v = viewport();
      var rect = c.getBoundingClientRect();
      return {
        cssW: rect.width, cssH: rect.height, left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom,
        backW: c.width, backH: c.height, viewW: v.w, viewH: v.h, dpr: window.devicePixelRatio || 1, cap: CAP
      };
    }
  };

  function wrap() {
    var original = window.createUnityInstance;
    if (typeof original !== "function" || original.__hawkesFit) return;
    var wrapped = function (c, config, onProgress) {
      config = config || {};
      config.devicePixelRatio = ratio();
      if (config.matchWebGLToCanvasSize === undefined) config.matchWebGLToCanvasSize = true;
      attach(c);
      return original.call(this, c, config, onProgress);
    };
    wrapped.__hawkesFit = true;
    window.createUnityInstance = wrapped;
  }

  // The loader script defines createUnityInstance when it runs; a capture listener sees its load before the page's own.
  document.addEventListener("load", function (e) {
    if (e.target && e.target.tagName === "SCRIPT") wrap();
  }, true);

  function onResize() {
    fit();
    // A phone's toolbar slides and a turn settles a frame later: fit again once the viewport has stopped moving.
    requestAnimationFrame(function () { requestAnimationFrame(fit); });
  }
  window.addEventListener("resize", onResize);
  window.addEventListener("orientationchange", function () { setTimeout(onResize, 120); });
  if (window.visualViewport) window.visualViewport.addEventListener("resize", onResize);
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", find);
  else find();
  window.addEventListener("load", function () { find(); wrap(); });
})();

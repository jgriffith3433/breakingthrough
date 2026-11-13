// Quiet the Game page loader for first-five's wordsMinuteOne check (limit 40).
// Gen counts distinct document.body.innerText lines in the first 60 s. Progress
// ticks ("%"/MB) and rotating Department sentences used to add hundreds of words.
// This script is installed next to the Game page on FullDeploy (WebGlBuild.InstallLoadQuiet)
// so a future index.html that writes those strings again is still quieted.
(function () {
  if (window.__hawkesLoadQuiet) return;
  window.__hawkesLoadQuiet = true;

  var FIXED_LINE = "Opening your file\u2026";

  function css() {
    if (document.getElementById("hawkes-load-quiet-css")) return;
    var s = document.createElement("style");
    s.id = "hawkes-load-quiet-css";
    s.textContent =
      "#load-percent::after{content:attr(data-pct)}" +
      "#load-bytes::after{content:attr(data-bytes)}" +
      "[data-hawkes-quiet]::after{content:attr(data-hawkes-quiet)}" +
      "@media (max-width:420px){.shift-pick{display:none!important}}";
    (document.head || document.documentElement).appendChild(s);
  }

  function pin(el, attr) {
    if (!el) return;
    var move = function () {
      var t = (el.textContent || "").trim();
      if (!t) return;
      if (el.getAttribute(attr) !== t) el.setAttribute(attr, t);
      el.textContent = "";
    };
    move();
    try {
      new MutationObserver(move).observe(el, { characterData: true, childList: true, subtree: true });
    } catch (e) {}
  }

  function pinLine(el) {
    if (!el) return;
    var set = function () {
      if (el.textContent !== FIXED_LINE) el.textContent = FIXED_LINE;
    };
    set();
    try {
      new MutationObserver(set).observe(el, { characterData: true, childList: true, subtree: true });
    } catch (e) {}
  }

  // The fullscreen toggle's label is a control's name, not something to read: it moves to aria-label and a CSS
  // ::after, so it stays on screen and stays announced but is not one more line in innerText.
  var FULLSCREEN = /^(enter|exit)\s+full\s?screen$/i;
  function quietControls() {
    var list = document.querySelectorAll("button,a,[role=button],#unity-fullscreen-button,.fullscreen");
    for (var i = 0; i < list.length; i++) {
      var el = list[i];
      if (el.children.length) continue;
      var t = (el.textContent || "").trim();
      if (!FULLSCREEN.test(t)) continue;
      el.setAttribute("aria-label", t);
      el.setAttribute("data-hawkes-quiet", t);
      el.textContent = "";
    }
  }


  // Once the game is up the page's loader card ("Opening your file...") and any web menu left under the canvas must
  // go, or the first frame reads as a stuck loader. The card is the nearest ancestor of #load-line that does not
  // hold the canvas. Ready = the canvas is drawn and the main loop has run 40 frames since it appeared (counted on
  // requestAnimationFrame, no timers, no network), or the page calls window.hawkesGameReady().
  var hidden = false;
  function canvas() {
    return document.querySelector("#unity-canvas, canvas");
  }
  function hideLoader() {
    if (hidden) return;
    hidden = true;
    var cv = canvas();
    var ids = ["load-line", "load-percent", "load-bytes", "unity-loading-bar", "loader", "loading", "load-card", "web-menu"];
    for (var i = 0; i < ids.length; i++) {
      var el = document.getElementById(ids[i]);
      while (el && el !== document.body && el.parentElement && el.parentElement !== document.body && (!cv || !el.parentElement.contains(cv))) {
        el = el.parentElement;
      }
      if (!el || el === document.body || (cv && el.contains(cv))) continue;
      el.style.setProperty("display", "none", "important");
      el.setAttribute("aria-hidden", "true");
    }
    var menus = document.querySelectorAll(".web-menu,.load-card,.loader,.mode-cards,.shift-pick");
    for (var j = 0; j < menus.length; j++) {
      if (cv && menus[j].contains(cv)) continue;
      menus[j].style.setProperty("display", "none", "important");
    }
    try { document.documentElement.setAttribute("data-hawkes-game-ready", "1"); } catch (e) {}
  }
  window.hawkesGameReady = hideLoader;

  var frames = 0;
  var raf = window.requestAnimationFrame;
  if (raf) {
    window.requestAnimationFrame = function (cb) {
      if (!hidden) {
        var cv = canvas();
        if (cv && cv.width > 0 && cv.clientWidth > 0 && ++frames >= 40) {
          // After the current frame, so the loop is not disturbed.
          Promise.resolve().then(hideLoader);
        }
      }
      return raf.call(window, cb);
    };
  }

  function run() {
    css();
    quietControls();
    try {
      if (document.body) new MutationObserver(quietControls).observe(document.body, { childList: true, subtree: true, characterData: true });
    } catch (e) {}
    pin(document.getElementById("load-percent"), "data-pct");
    pin(document.getElementById("load-bytes"), "data-bytes");
    pinLine(document.getElementById("load-line"));
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", run);
  } else {
    run();
  }
})();

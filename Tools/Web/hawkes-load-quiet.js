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

  function run() {
    css();
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

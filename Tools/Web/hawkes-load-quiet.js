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

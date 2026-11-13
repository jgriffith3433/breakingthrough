// The browser side of SoundSettings and the page's "Tap to play" prompt (Tools/Web/hawkes-sound.js, which the WebGL
// deploy installs next to Game/index.html). Every call is safe on a page without the prompt.
mergeInto(LibraryManager.library, {
  // Unity's own AudioContext, for the prompt to resume after a tap, a tab switch or an iOS interruption.
  HawkesSoundAttach: function () {
    try {
      if (window.hawkesSound && typeof WEBAudio !== "undefined" && WEBAudio && WEBAudio.audioContext) {
        window.hawkesSound.attach(WEBAudio.audioContext);
      }
    } catch (e) {}
  },

  HawkesSoundLoad: function () {
    var json = "";
    try {
      json = window.localStorage.getItem("hawkes.sound") || "";
    } catch (e) {}
    var size = lengthBytesUTF8(json) + 1;
    var buffer = _malloc(size);
    stringToUTF8(json, buffer, size);
    return buffer;
  },

  HawkesSoundSave: function (jsonPtr) {
    try {
      window.localStorage.setItem("hawkes.sound", UTF8ToString(jsonPtr));
    } catch (e) {}
  },

  // The playtest bot's press on the prompt: a pointer press on the prompt itself, through its own handler.
  // Returns 1 if the prompt was there.
  HawkesSoundPressPrompt: function () {
    var prompt = document.getElementById("hawkes-tap");
    if (!prompt) {
      return 0;
    }
    var rect = prompt.getBoundingClientRect();
    var x = rect.left + rect.width / 2;
    var y = rect.top + rect.height / 2;
    var init = { bubbles: true, cancelable: true, clientX: x, clientY: y, pointerType: "mouse", isPrimary: true, button: 0 };
    try {
      prompt.dispatchEvent(new PointerEvent("pointerdown", init));
      prompt.dispatchEvent(new PointerEvent("pointerup", init));
    } catch (e) {
      prompt.dispatchEvent(new MouseEvent("click", init));
    }
    return 1;
  },

  // The Game page defines a stand-in window.hawkesPlaytest before Unity loads (the loading card's own contract), and the
  // playtest bridge replaces it when Unity starts. A runner that called start() in between started the stand-in's run,
  // which the replacement drops: Unity sat idle, nobody pressed "Tap to play" and the run was stuck. This reads the id
  // of such a run (call it before HawkesPlaytest.Register); "" when there is none, the bridge is already ours, or the
  // URL has ?playtest= (the bridge starts that one itself).
  HawkesPlaytestStubScenario: function () {
    var id = "";
    try {
      var api = window.hawkesPlaytest;
      var auto = new URLSearchParams(window.location.search).get("playtest");
      if (api && api.engine !== "unity" && !auto && typeof api.report === "function" && typeof api.state === "function") {
        var report = api.report();
        var state = api.state();
        if (report && report.scenario && state && state.status === "running") {
          id = String(report.scenario);
        }
      }
    } catch (e) {}
    var size = lengthBytesUTF8(id) + 1;
    var buffer = _malloc(size);
    stringToUTF8(id, buffer, size);
    return buffer;
  },

  // Starts that run again on the Unity bridge (call it after HawkesPlaytest.Register); its bot presses the prompt.
  HawkesPlaytestStartAdopted: function (idPtr) {
    try {
      var id = UTF8ToString(idPtr);
      if (id && window.hawkesPlaytest && window.hawkesPlaytest.engine === "unity") {
        window.hawkesPlaytest.start(id, {});
      }
    } catch (e) {}
  },

  // "prompt" (not tapped yet), "running", "suspended", "none" (no Web Audio yet) or "" (no prompt on this page).
  HawkesSoundState: function () {
    var state = "";
    try {
      state = window.hawkesSound ? String(window.hawkesSound.state()) : "";
    } catch (e) {}
    var size = lengthBytesUTF8(state) + 1;
    var buffer = _malloc(size);
    stringToUTF8(state, buffer, size);
    return buffer;
  }
});

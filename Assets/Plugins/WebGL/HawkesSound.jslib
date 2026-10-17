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

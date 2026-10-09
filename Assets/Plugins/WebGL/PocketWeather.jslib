// Browser questions the game can't answer from C#.
mergeInto(LibraryManager.library, {
  // 1 if the device's main pointer is a finger and it has no mouse or trackpad besides (phones and
  // tablets, including iPads, which report themselves as Macs), so the first hints can say "Drag"
  // and the on-screen buttons show before anything is touched. An iPad with a trackpad attached
  // says 0 here, and gets the buttons at its first touch instead.
  PW_TouchFirst: function () {
    try {
      var mm = window.matchMedia;
      if (!mm || mm("(any-pointer: fine)").matches) return 0;
      if (mm("(pointer: coarse)").matches) return 1;
      if (navigator.maxTouchPoints > 1 && mm("(hover: none)").matches) return 1;
    } catch (e) {}
    return 0;
  },

  // canvas pixels per CSS pixel (the page caps the backing resolution), so the HUD can size
  // itself by what the player actually sees
  PW_PixelsPerCssPx: function () {
    try {
      var c = Module.canvas, w = c.getBoundingClientRect().width;
      if (w > 0 && c.width > 0) return c.width / w;
    } catch (e) {}
    return 1;
  },

  // The notch, rounded corners and home indicator (CSS env(safe-area-inset-*), which Unity's
  // Screen.safeArea doesn't see in a browser), side 0 left, 1 right, 2 top, 3 bottom, in CSS px.
  // The page keeps an invisible element padded by them (#pw-safe).
  PW_SafeInset: function (side) {
    try {
      var el = document.getElementById("pw-safe");
      if (!el) return 0;
      var s = getComputedStyle(el);
      var v = [s.paddingLeft, s.paddingRight, s.paddingTop, s.paddingBottom][side];
      return parseFloat(v) || 0;
    } catch (e) {}
    return 0;
  },

  // The kind of input the player used last, as the page saw it: 0 nothing yet, 1 a finger or pen,
  // 2 a mouse or trackpad, 3 a key. Unity's own devices can't tell (a touch may move its mouse).
  // The page counts each one in window.pwInput (see index.html); the count says it's a new one.
  PW_LastInput: function () {
    var i = window.pwInput;
    return i ? i.kind : 0;
  },
  PW_InputCount: function () {
    var i = window.pwInput;
    return i ? i.count : 0;
  },

  // A test tool's question about the game (GameRoot.PwReportUi) is answered into window.pwUi.
  PW_SetUi: function (json) {
    try { window.pwUi = JSON.parse(UTF8ToString(json)); } catch (e) { window.pwUi = null; }
  },
});

// Browser questions the game can't answer from C#.
mergeInto(LibraryManager.library, {
  // 1 if the device's main pointer is a finger (phones and tablets, including iPads, which
  // report themselves as Macs), so the first hints can say "Drag" before anything is touched
  PW_TouchFirst: function () {
    try {
      if (window.matchMedia && window.matchMedia("(pointer: coarse)").matches) return 1;
      if (navigator.maxTouchPoints > 1 && window.matchMedia && window.matchMedia("(hover: none)").matches) return 1;
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
});

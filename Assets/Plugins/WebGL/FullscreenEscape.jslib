// Browsers reserve Esc for leaving fullscreen, so the game never sees the press.
// While the page is fullscreen we ask for a keyboard lock on Escape (Chrome, Edge, Opera):
// a tap of Esc then reaches the game, and holding Esc still leaves fullscreen.
// Browsers without the Keyboard Lock API (Firefox, Safari) keep the old behaviour.
mergeInto(LibraryManager.library, {
  XCon_InstallFullscreenEscapeLock: function () {
    if (typeof document === "undefined" || document.__xconEscLockInstalled) {
      return;
    }
    document.__xconEscLockInstalled = true;

    var kb = (typeof navigator !== "undefined") ? navigator.keyboard : null;
    if (!kb || typeof kb.lock !== "function") {
      return;
    }

    var onChange = function () {
      var fs = document.fullscreenElement || document.webkitFullscreenElement;
      if (fs) {
        kb.lock(["Escape"]).catch(function () {});
      } else if (typeof kb.unlock === "function") {
        kb.unlock();
      }
    };

    document.addEventListener("fullscreenchange", onChange);
    document.addEventListener("webkitfullscreenchange", onChange);
  }
});

// The page side of RunAnalytics, next to Gen's own bridge (com.hawkes.analytics, which loads the SDK and forwards
// HawkesAnalytics calls). This file only does what that bridge doesn't: the build stamp, debug mode for playtests,
// the feedback card in the game's colours, and reading the SDK's debug log for the playtest bot. Nothing here throws.
mergeInto(LibraryManager.library, {
  // window.HAWKES_BUILD = { version, commit, date } from Resources/HawkesBuild.json (written by the WebGL deploy).
  // The SDK reads it on every batch; a page that sets its own keeps it.
  HawkesAnalyticsPageSetBuild: function (jsonPtr) {
    try {
      if (window.HAWKES_BUILD) {
        return;
      }
      var build = JSON.parse(UTF8ToString(jsonPtr) || "null");
      if (build && build.version) {
        window.HAWKES_BUILD = build;
      }
    } catch (e) {}
  },

  // 1 when the SDK should keep its debug log: ?hawkes-analytics-debug=1, a playtest URL, or a browser driven by a
  // bot (Studio's runner opens the plain game URL in a webdriver browser).
  HawkesAnalyticsPageDebug: function () {
    try {
      var q = String(location.search || "");
      if (/[?&]hawkes-analytics-debug=1\b/.test(q) || /[?&](playtest|hawkesPlaytest)\b/.test(q)) {
        return 1;
      }
      return navigator.webdriver ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Debug mode after start, for a playtest begun from window.hawkesPlaytest.start() on a page that wasn't in it.
  HawkesAnalyticsPageEnableDebug: function () {
    try {
      var a = window.hawkesAnalytics;
      if (a && a.init && a.config && !a.config().debug) {
        a.init({ debug: true });
      }
    } catch (e) {}
  },

  // The feedback card (the SDK's ask) in the game's colours. The card takes the keyboard focus when it opens; it is
  // handed back to the game so "press any key" on the game-over screen still works. The card stays clickable.
  HawkesAnalyticsPageAsk: function (jsonPtr) {
    try {
      var o = JSON.parse(UTF8ToString(jsonPtr) || "{}");
      var a = window.hawkesAnalytics;
      if (!a || typeof a.ask !== "function") {
        (window.hawkesAnalyticsQ = window.hawkesAnalyticsQ || []).push(["ask", o]);
        return;
      }
      a.ask(o);
      var active = document.activeElement;
      if (active && active.hasAttribute && active.hasAttribute("data-hawkes-ask")) {
        active.blur();
        if (typeof Module !== "undefined" && Module.canvas && Module.canvas.focus) {
          Module.canvas.focus();
        }
      }
    } catch (e) {}
  },

  // A new run: the card never stays over play. Unanswered, it is simply gone (no snooze, no feedback event).
  HawkesAnalyticsPageCloseAsk: function () {
    try {
      var cards = document.querySelectorAll("[data-hawkes-ask]");
      for (var i = 0; i < cards.length; i++) {
        cards[i].remove();
      }
    } catch (e) {}
  },

  // Playtest: whether the card with this id is on the page.
  HawkesAnalyticsPageAskShown: function (idPtr) {
    try {
      return document.querySelector('[data-hawkes-ask="' + UTF8ToString(idPtr) + '"]') ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Playtest: a player's clicks on the card: the n-th star, then Skip (no comment).
  HawkesAnalyticsPageRate: function (idPtr, stars) {
    try {
      var host = document.querySelector('[data-hawkes-ask="' + UTF8ToString(idPtr) + '"]');
      var root = host && (host.shadowRoot || host);
      var buttons = root ? root.querySelectorAll(".ha-r button") : [];
      if (!buttons.length || stars < 1 || stars > buttons.length) {
        return 0;
      }
      buttons[stars - 1].click();
      var row = root.querySelectorAll(".ha-s button");
      if (row.length) {
        row[0].click();
      }
      return 1;
    } catch (e) {
      return 0;
    }
  },

  // Playtest: the SDK's state and its debug log after a sequence number, one event per line:
  // "config<TAB>product<TAB>version<TAB>commit<TAB>env<TAB>debug<TAB>sent", then "seq<TAB>name<TAB>sent<TAB>detail".
  // Empty when the SDK isn't on the page.
  HawkesAnalyticsPageLog: function (afterSeq) {
    var out = "";
    try {
      var a = window.hawkesAnalytics;
      if (a && a.config) {
        var c = a.config(), b = c.build || {}, log = a.debugLog || [], sent = 0, lines = [];
        for (var i = 0; i < log.length; i++) {
          var e = log[i];
          if (e.sent) {
            sent++;
          }
          if (e.seq <= afterSeq) {
            continue;
          }
          var p = e.props || {}, bits = [];
          for (var k in p) {
            var v = p[k];
            if (v !== null && v !== undefined && typeof v !== "object" && bits.length < 8) {
              bits.push(k + "=" + String(v).slice(0, 40));
            }
          }
          lines.push(e.seq + "\t" + e.name + "\t" + (e.sent ? 1 : 0) + "\t" + bits.join(" ").replace(/[\t\n]/g, " "));
        }
        out = ["config", c.product || "", b.version || "", b.commit || "", c.env || "", c.debug ? 1 : 0, sent].join("\t");
        if (lines.length) {
          out += "\n" + lines.join("\n");
        }
      }
    } catch (e) {}
    var size = lengthBytesUTF8(out) + 1;
    var buffer = _malloc(size);
    stringToUTF8(out, buffer, size);
    return buffer;
  },
});

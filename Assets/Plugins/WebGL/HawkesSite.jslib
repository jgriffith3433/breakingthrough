// The website around the game, for the `site` playtest (Playtest.cs). Reads the site's pages (the landing page and
// every page linked from it, outside Game/) and reports their footer links, and can show one page over the game for
// a few seconds so the recording shows its footer. Nothing here runs unless that scenario calls it.
mergeInto(LibraryManager.library, {
  HawkesSiteCheckStart: function () {
    var path = location.pathname;
    var cut = path.indexOf("/Game/");
    var root = location.origin + (cut >= 0 ? path.slice(0, cut + 1) : "/");
    var game = root + "Game/";
    window.__hawkesSiteCheck = null;

    function text(el) {
      return (el.textContent || "").replace(/\s+/g, " ").trim();
    }

    function host(a) {
      try {
        return new URL(a.getAttribute("href"), root).hostname.replace(/^www\./, "");
      } catch (e) {
        return "";
      }
    }

    var seen = {};
    var queue = [root + "index.html"];
    var pages = [];

    function next() {
      if (!queue.length || pages.length >= 25) {
        window.__hawkesSiteCheck = JSON.stringify({ root: root, pages: pages });
        return;
      }
      var url = queue.shift();
      fetch(url, { cache: "no-store" })
        .then(function (r) {
          return r.text().then(function (body) {
            return { status: r.status, body: body };
          });
        })
        .then(function (res) {
          var doc = new DOMParser().parseFromString(res.body, "text/html");
          var links = Array.prototype.slice.call(doc.querySelectorAll("a[href]"));
          var powered = links.filter(function (a) {
            return host(a) === "hawkesgen.com" && /powered by hawkes gen/i.test(text(a));
          });
          var parent = links.filter(function (a) {
            return host(a) === "hawkesbiomedical.com";
          });
          pages.push({
            path: url.slice(root.length - 1),
            status: res.status,
            powered: powered.length > 0,
            poweredText: powered.length ? text(powered[0]) : "",
            parent: parent.length > 0,
            parentText: parent.length ? text(parent[0]) : ""
          });
          links.forEach(function (a) {
            var u;
            try {
              u = new URL(a.getAttribute("href"), url);
            } catch (e) {
              return;
            }
            u.hash = "";
            u.search = "";
            var s = u.href;
            if (/\/$/.test(s)) s += "index.html";
            if (u.origin !== location.origin || s.indexOf(root) !== 0 || s.indexOf(game) === 0) return;
            if (!/\.html$/.test(s) || seen[s]) return;
            seen[s] = true;
            queue.push(s);
          });
        })
        .catch(function (e) {
          pages.push({ path: url.slice(root.length - 1), status: 0, error: String(e) });
        })
        .then(next);
    }

    seen[queue[0]] = true;
    next();
  },

  // "" while the check runs, then its JSON.
  HawkesSiteCheckResult: function () {
    var s = window.__hawkesSiteCheck || "";
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  // 1 when the game page itself carries the site footer (it must not).
  HawkesGamePageHasFooter: function () {
    return document.querySelector("[data-hawkes-footer]") ? 1 : 0;
  },

  // Shows a site page (a path from the check, like "/index.html") over the game, scrolled to its footer.
  HawkesSiteShow: function (pathPtr) {
    var path = UTF8ToString(pathPtr);
    var old = document.getElementById("hawkes-site-view");
    if (old) old.parentNode.removeChild(old);
    var frame = document.createElement("iframe");
    frame.id = "hawkes-site-view";
    frame.title = "Site page (playtest)";
    frame.style.cssText = "position:fixed;inset:0;width:100%;height:100%;border:0;z-index:2147483000;background:#12100e;";
    frame.onload = function () {
      try {
        var w = frame.contentWindow;
        var foot = w.document.querySelector("[data-hawkes-footer]");
        w.document.documentElement.style.scrollBehavior = "auto";
        if (foot) foot.scrollIntoView({ block: "end" });
        else w.scrollTo(0, w.document.body.scrollHeight);
      } catch (e) {}
    };
    var cut = location.pathname.indexOf("/Game/");
    frame.src = (cut >= 0 ? location.pathname.slice(0, cut) : "") + path;
    document.body.appendChild(frame);
  },

  HawkesSiteHide: function () {
    var old = document.getElementById("hawkes-site-view");
    if (old) old.parentNode.removeChild(old);
  }
});

// The bridge from HawkesAnalytics.cs to window.hawkesAnalytics (Hawkes Gen's analytics SDK, docs/analytics.md).
// If the page hasn't loaded the SDK, Init adds its script tag with the product from the Unity settings; calls made
// before it loads wait in window.hawkesAnalyticsQ, which the SDK plays back when it starts. Nothing here throws.
mergeInto(LibraryManager.library, {
  HawkesAnalytics_Init: function (productPtr, srcPtr, endpointPtr, versionPtr, debug) {
    try {
      var product = UTF8ToString(productPtr), src = UTF8ToString(srcPtr), endpoint = UTF8ToString(endpointPtr), version = UTF8ToString(versionPtr);
      // The Game/v<n> folder the build was loaded from, when the page has one.
      var m = /\/Game\/v(\d+)\//i.exec(location.pathname);
      var scripts = document.querySelectorAll('script[src]');
      for (var i = 0; !m && i < scripts.length; i++) m = /\/Game\/v(\d+)\//i.exec(scripts[i].src);
      if (m) version = (version ? version + '+' : '') + 'v' + m[1];
      var a = window.hawkesAnalytics;
      if (a && a.init && a.config) {
        // The page loaded the SDK itself: fill in only what it doesn't have.
        var c = a.config(), o = {};
        if (!c.product && product) o.product = product;
        if (!c.build && version) o.build = version;
        if (debug) o.debug = true;
        a.init(o);
        return;
      }
      window.hawkesAnalyticsQ = window.hawkesAnalyticsQ || [];
      if (!src || document.querySelector('script[data-hawkes-analytics]')) return;
      var s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.setAttribute('data-hawkes-analytics', 'unity');
      if (product) s.setAttribute('data-product', product);
      if (version) s.setAttribute('data-build', version);
      if (endpoint) s.setAttribute('data-endpoint', endpoint);
      if (debug) s.setAttribute('data-debug', '1');
      (document.head || document.documentElement).appendChild(s);
    } catch (e) {
      try { console.warn('[hawkes-analytics] bridge', e); } catch (x) {}
    }
  },

  HawkesAnalytics_Call: function (methodPtr, argsPtr) {
    try {
      var method = UTF8ToString(methodPtr), args = JSON.parse(UTF8ToString(argsPtr) || '[]');
      var a = window.hawkesAnalytics;
      if (a && typeof a[method] === 'function') return void a[method].apply(null, args);
      var q = window.hawkesAnalyticsQ = window.hawkesAnalyticsQ || [];
      q.push([method].concat(args));
      if (q.length > 500) q.shift();
    } catch (e) {}
  },
});

// The landing page's motion (index.html): what rises into view as you scroll, the hero's window settling flat, the tour's
// index following the step in view, and the header's edge once the page has scrolled. Without JavaScript everything just
// shows, and with reduced motion nothing moves (landing.css).
(function () {
  var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var watch = 'IntersectionObserver' in window;
  var rising = document.querySelectorAll('[data-reveal]');

  if (reduce || !watch) {
    rising.forEach(function (element) { element.classList.add('is-in'); });
  } else {
    var reveal = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-in');
          reveal.unobserve(entry.target);
        }
      });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.1 });
    rising.forEach(function (element) { reveal.observe(element); });
  }

  // The tour's index: where its steps don't all fit, its ends fade where there's more to scroll to, and the current
  // step's link is scrolled to the middle of it (the index's own scroll, never the page's).
  var index = document.querySelector('.story-index');
  var track = document.querySelector('.story-index-track');
  var links = document.querySelectorAll('.story-index a');
  if (index && track) {
    var ends = function () {
      var more = track.scrollWidth - track.clientWidth;
      index.classList.toggle('more-before', track.scrollLeft > 1);
      index.classList.toggle('more-after', track.scrollLeft < more - 1);
    };
    track.addEventListener('scroll', ends, { passive: true });
    window.addEventListener('resize', ends);
    ends();
  }
  if (watch && links.length) {
    var current = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        links.forEach(function (link) {
          var isCurrent = link.getAttribute('href') === '#' + entry.target.id;
          link.classList.toggle('is-current', isCurrent);
          if (isCurrent && track) {
            track.scrollTo({ left: link.offsetLeft - (track.clientWidth - link.offsetWidth) / 2, behavior: reduce ? 'auto' : 'smooth' });
          }
        });
      });
    }, { rootMargin: '-45% 0px -50% 0px' });
    document.querySelectorAll('.story-step').forEach(function (step) { current.observe(step); });
  }

  var bar = document.querySelector('.topbar');
  function edge() { bar.classList.toggle('is-scrolled', window.scrollY > 8); }
  window.addEventListener('scroll', edge, { passive: true });
  edge();
})();

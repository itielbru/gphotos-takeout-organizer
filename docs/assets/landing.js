/* Seven-segment burn-in: draws a date like "'23 7 22" as SVG, then plays the one
   authored moment of the page — the corner shows today's (wrong) date and flips
   to the photo's real one. Reduced motion renders the real date immediately. */
(function () {
  var SEG = { // a b c d e f g
    '0': 'abcdef', '1': 'bc', '2': 'abdeg', '3': 'abcdg', '4': 'bcfg',
    '5': 'acdfg', '6': 'acdefg', '7': 'abc', '8': 'abcdefg', '9': 'abcdfg'
  };
  // segment polygons in a 10x18 cell (slanted 7-seg look via skew on the group)
  var P = {
    a: '1.2,0 8.8,0 7.6,1.6 2.4,1.6',
    b: '9,0.6 9,8.2 7.6,7.4 7.6,1.8',
    c: '9,9.8 9,17.4 7.6,16.2 7.6,10.6',
    d: '1.2,18 8.8,18 7.6,16.4 2.4,16.4',
    e: '1,9.8 1,17.4 2.4,16.2 2.4,10.6',
    f: '1,0.6 1,8.2 2.4,7.4 2.4,1.8',
    g: '1.6,9 2.6,8.3 7.4,8.3 8.4,9 7.4,9.7 2.6,9.7'
  };
  var CELL = 10.6, GAP = 5, TICK = 4;

  function render(el, text) {
    var x = 0, out = [];
    for (var i = 0; i < text.length; i++) {
      var ch = text[i];
      if (ch === ' ') { x += GAP; continue; }
      if (ch === "'") { // the apostrophe before a two-digit year
        out.push('<rect class="seg" x="' + (x + 1) + '" y="0" width="1.6" height="4" rx=".6"/>');
        x += TICK; continue;
      }
      var on = SEG[ch] || '';
      var g = '<g transform="translate(' + x + ',0)">';
      'abcdefg'.split('').forEach(function (s) {
        g += '<polygon class="seg' + (on.indexOf(s) < 0 ? ' off' : '') + '" points="' + P[s] + '"/>';
      });
      out.push(g + '</g>');
      x += CELL;
    }
    el.innerHTML = '<svg viewBox="-1 -1 ' + (x + 2) + ' 20" role="img" aria-label="' +
      el.getAttribute('data-label') + '"><g transform="skewX(-6)">' + out.join('') + '</g></svg>';
  }

  var el = document.querySelector('.burn');
  if (!el) return;
  var real = el.getAttribute('data-date');     // "'23 7 22"
  // "today" is literally today: 'YY M D like the camera would print it
  var now = new Date();
  var wrong = "'" + String(now.getFullYear()).slice(-2) + ' ' + (now.getMonth() + 1) + ' ' + now.getDate();
  if (!/^'\d\d \d{1,2} \d{1,2}$/.test(wrong)) wrong = el.getAttribute('data-wrong');
  var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduce || !wrong) { render(el, real); return; }

  render(el, wrong);
  var cap = document.querySelector('.burn-caption');
  setTimeout(function () {
    // brief flicker, then the real date
    el.style.transition = 'opacity .12s';
    el.style.opacity = '.15';
    setTimeout(function () { render(el, real); el.style.opacity = '.96'; if (cap) cap.classList.add('is-real'); }, 140);
  }, 1500);
})();

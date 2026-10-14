// Keeps the x-axis labels of the dashboard time-series cards (TimeSeriesValueCard) and the
// account details hero chart (AccountDetailsHero) inside the card.
//
// A datetime tick can land on the plot edge and its centred label is then clipped by the
// card. ApexCharts has no option for this, so labels are nudged inwards on the rendered SVG
// until they fit. A MutationObserver re-applies it after every (re)render, resize or update.
(function () {
    'use strict';

    var SELECTOR = '.fm-tsvc-card svg.apexcharts-svg, .fm-hero-chart svg.apexcharts-svg';
    var EDGE_PADDING_PX = 4;
    var scheduled = false;

    function insetXAxisLabels(svg) {
        var svgRect = svg.getBoundingClientRect();
        if (svgRect.width === 0) return;

        svg.querySelectorAll('.apexcharts-xaxis-texts-g text').forEach(function (label) {
            var rect = label.getBoundingClientRect();
            if (rect.width === 0) return;

            var shift = 0;
            if (rect.left < svgRect.left + EDGE_PADDING_PX)
                shift = svgRect.left + EDGE_PADDING_PX - rect.left;
            else if (rect.right > svgRect.right - EDGE_PADDING_PX)
                shift = svgRect.right - EDGE_PADDING_PX - rect.right;

            if (shift !== 0) {
                var matrix = label.parentNode.getScreenCTM();
                if (!matrix) return;
                var inverse = matrix.inverse();
                var dx = shift * inverse.a;
                var dy = shift * inverse.b;
                if (!Number.isFinite(dx) || !Number.isFinite(dy)) return;

                // Prepend a translation in the parent's coordinates so the screen-space shift
                // stays horizontal, even when ApexCharts has rotated the label.
                var transform = label.getAttribute('transform') || '';
                label.setAttribute('transform', 'translate(' + dx + ' ' + dy + ') ' + transform);
            }
        });
    }

    function apply() {
        scheduled = false;
        document.querySelectorAll(SELECTOR).forEach(function (svg) {
            insetXAxisLabels(svg);
        });
    }

    function schedule() {
        if (scheduled) return;
        scheduled = true;
        window.requestAnimationFrame(apply);
    }

    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true });
    window.addEventListener('resize', schedule);
})();

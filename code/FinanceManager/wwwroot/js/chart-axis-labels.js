// Keeps the axis labels of the dashboard time-series cards (TimeSeriesValueCard) readable.
//
// ApexCharts has no option for either fix, so they are applied to the rendered SVG:
//  1. The floating y-axis labels sit inside the plot, but ApexCharts emits their group before
//     the plot, so the series line and fill paint over them. Moving the group after the plot
//     draws the labels on top; css/time-series-card.css gives them a card-surface halo so the
//     series reads as passing behind each label.
//  2. A datetime tick can land on the plot edge and its centred label is then clipped by the
//     card. Labels are nudged inwards until they fit.
// A MutationObserver re-applies both after every (re)render, resize or data update.
(function () {
    'use strict';

    var SELECTOR = '.fm-tsvc-card svg.apexcharts-svg';
    var EDGE_PADDING_PX = 4;
    var scheduled = false;

    function raiseYAxis(svg) {
        var yaxis = svg.querySelector(':scope > g.apexcharts-yaxis');
        if (yaxis && svg.lastElementChild !== yaxis)
            svg.appendChild(yaxis);
    }

    function insetXAxisLabels(svg) {
        var svgRect = svg.getBoundingClientRect();
        if (svgRect.width === 0) return;

        var userWidth = svg.viewBox && svg.viewBox.baseVal && svg.viewBox.baseVal.width > 0
            ? svg.viewBox.baseVal.width
            : svgRect.width;
        var scale = userWidth / svgRect.width;

        svg.querySelectorAll('.apexcharts-xaxis-texts-g text').forEach(function (label) {
            var rect = label.getBoundingClientRect();
            if (rect.width === 0) return;

            var shift = 0;
            if (rect.left < svgRect.left + EDGE_PADDING_PX)
                shift = svgRect.left + EDGE_PADDING_PX - rect.left;
            else if (rect.right > svgRect.right - EDGE_PADDING_PX)
                shift = svgRect.right - EDGE_PADDING_PX - rect.right;

            if (shift !== 0) {
                var x = parseFloat(label.getAttribute('x'));
                if (Number.isFinite(x))
                    label.setAttribute('x', String(x + shift * scale));
            }
        });
    }

    function apply() {
        scheduled = false;
        document.querySelectorAll(SELECTOR).forEach(function (svg) {
            raiseYAxis(svg);
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

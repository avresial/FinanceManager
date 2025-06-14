window.setupChart = (id, config) => {
    var ctx = document.getElementById(id).getContext('2d');
    return new Chart(ctx, config);
},
    window.updateChart = (chartRef, visible) => {
        // Example: make visible/hidden the 4th dataset in the chart
        chartRef.data.datasets[3].hidden = visible;
        chartRef.update();
    }
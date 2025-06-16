window.setupChart = (id, config, newdatasets) => {
    var ctx = document.getElementById(id).getContext('2d');
    var chart = new Chart(ctx, config);

    datasets = [];

    newdatasets.forEach((newdataset) => {
        datasets.push({
            data: [],
            cubicInterpolationMode: 'monotone',
            tension: 0.4,
            fill: newdataset.fill,
            borderColor: newdataset.borderColor,
            backgroundColor: newdataset.backgroundColor,
        })
    });

    chart.data.datasets = datasets;

    return chart;
}


window.updateChartData = (chartRef, data) => {
    chartRef.data.datasets[0].data = data;
    chartRef.update();
}

window.clearDatasets = (chartRef) => {

    chartRef.data.datasets.forEach((dataset) => dataset.data = []);
    chartRef.update();
}

window.addDataPoint = (chartRef, datasetIndex, dataPoint) => {
    chartRef.data.datasets[datasetIndex].data.push(dataPoint);
    chartRef.update();
}
window.setupChart = (id, config) => {
    var ctx = document.getElementById(id).getContext('2d');
    var chart = new Chart(ctx, config);
    chart.data = {
        datasets: [
            {
                data: [],
                cubicInterpolationMode: 'monotone',
                tension: 0.4,
                fill: 'start',
                borderColor: '#FFAB00'
                //borderColor = 'rgba(255, 171,0,0.1)'
                //borderColor = "FFAB00"
            }
        ]
    };

    return chart;
}


window.updateChartData = (chartRef, data) => {

    chartRef.data = {
        datasets: [
            {
                data: data,
                cubicInterpolationMode: 'monotone',
                tension: 0.4,
                fill: 'start'
            }
        ]
    };

    chartRef.update();
}

window.clearDatasets = (chartRef) => {
    chartRef.data.datasets = [
        {
            data: [],
            cubicInterpolationMode: 'monotone',
            tension: 0.4,
            fill: 'start',
            borderColor: '#FFAB00'
            //borderColor = 'rgba(255, 171, 0, 0.1)'
        }
    ]
    chartRef.update();
}

window.addDataPoint = (chartRef, dataPoint) => {
    chartRef.data.datasets[0].data.push(dataPoint);
    chartRef.update();
}
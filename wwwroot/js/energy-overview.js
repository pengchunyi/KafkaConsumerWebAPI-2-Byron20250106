document.addEventListener("DOMContentLoaded", () => {
    // 電力流向圖初始化
    const electricityFlowCtx = document.getElementById('powerFlowChart').getContext('2d');
    new Chart(electricityFlowCtx, {
        type: 'bar',
        data: {
            labels: ['市電', '光伏發電', '柴油發電'],
            datasets: [{
                label: '單位:kWh',
                data: [900000, 100000, 0], // 電力數據
                backgroundColor: ['#4caf50', '#2196f3', '#f44336']
            }]
        },
        options: {
            responsive: true,
            //plugins: {
            //    legend: { position: 'top' }
            //}
        }
    });

    // 用電密度圖初始化
    const electricityDensityCtx = document.getElementById('electricityDensityChart').getContext('2d');
    new Chart(electricityDensityCtx, {
        type: 'line',
        data: {
            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
            datasets: [{
                label: '實際值',
                data: [37000, 42000, 50000, 55000, 60000, 39000, 33000, 22000, 45000, 42000, 42000, 30000],
                backgroundColor: '#4caf50',
                borderColor: '#4caf50',
                fill: false
            }, {
                label: '目標值',
                data: [40000, 40000, 40000, 40000, 40000, 40000, 40000, 40000, 40000, 40000, 40000, 40000],
                backgroundColor: '#f44336',
                borderColor: '#f44336',
                fill: false
            }]
        },
        options: {
            responsive: true,
            //plugins: {
            //    legend: { position: 'top' }
            //},
            scales: {
                x: { title: { display: true, text: '月份' } },
                y: { title: { display: true, text: '用電量 (kWh/M USD)' } }
            }
        }
    });

    // 水流向圖初始化
    const waterFlowCtx = document.getElementById('waterFlowChart').getContext('2d');
    new Chart(waterFlowCtx, {
        type: 'bar',
        data: {
            labels: ['自來水', '回收水', '雨水'],
            datasets: [{
                label: '單位:m³',
                data: [10000, 5000, 2000],
                backgroundColor: ['#4caf50', '#2196f3', '#ffc107']
            }]
        },
        options: {
            responsive: true,
            //plugins: {
            //    legend: { position: 'top' }
            //}
        }
    });

    // 用水密度圖初始化
    const waterDensityCtx = document.getElementById('waterDensityChart').getContext('2d');
    new Chart(waterDensityCtx, {
        type: 'line',
        data: {
            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
            datasets: [{
                label: '實際值',
                data: [400, 200, 380, 550, 500, 350, 340, 330, 500, 310, 300, 450],
                backgroundColor: '#4caf50',
                borderColor: '#4caf50',
                fill: false
            }, {
                label: '目標值',
                data: [350, 350, 350, 350, 350, 350, 350, 350, 350, 350, 350, 350],
                backgroundColor: '#f44336',
                borderColor: '#f44336',
                fill: false
            }]
        },
        options: {
            responsive: true,
            //plugins: {
            //    legend: { position: 'top' }
            //},
            scales: {
                x: { title: { display: true, text: '月份' } },
                y: { title: { display: true, text: '用水量 (m³/M USD)' } }
            }
        }
    });
});



//document.addEventListener("DOMContentLoaded", () => {
//    // 電力流向圖初始化
//    const electricityFlowCtx = document.getElementById('powerFlowChart').getContext('2d');
//    new Chart(electricityFlowCtx, {
//        type: 'bar',
//        data: {
//            labels: ['市電', '光伏發電', '柴油發電'],
//            datasets: [{
//                data: [900000, 100000, 0], // 電力數據
//                backgroundColor: ['#4caf50', '#2196f3', '#f44336']
//            }]
//        },
//        options: {
//            responsive: true,
//            plugins: {
//                legend: {
//                    display: false // 隱藏色塊
//                },
//                title: {
//                    display: true,
//                    text: '電力流向圖（單位:kWh）'
//                }
//            },
//            scales: {
//                x: { title: { display: true, text: '來源' } },
//                y: { title: { display: true, text: '數值 (kWh)' } }
//            }
//        }
//    });

//    // 用電密度圖初始化
//    const electricityDensityCtx = document.getElementById('electricityDensityChart').getContext('2d');
//    new Chart(electricityDensityCtx, {
//        type: 'line',
//        data: {
//            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
//            datasets: [{
//                label: '實際值',
//                data: [42000, 42000, 42000, 42000, 42000, 42000, 42000, 42000, 42000, 42000, 42000, 42000],
//                borderColor: '#4caf50',
//                fill: false
//            }, {
//                label: '目標值',
//                data: [50000, 50000, 50000, 50000, 50000, 50000, 50000, 50000, 50000, 50000, 50000, 50000],
//                borderColor: '#f44336',
//                fill: false
//            }]
//        },
//        options: {
//            responsive: true,
//            plugins: {
//                legend: { position: 'top' },
//                title: {
//                    display: true,
//                    text: '用電密度 (kWh/M USD)'
//                }
//            },
//            scales: {
//                x: { title: { display: true, text: '月份' } },
//                y: { title: { display: true, text: '用電量 (kWh/M USD)' } }
//            }
//        }
//    });

//    // 水流向圖初始化
//    const waterFlowCtx = document.getElementById('waterFlowChart').getContext('2d');
//    new Chart(waterFlowCtx, {
//        type: 'bar',
//        data: {
//            labels: ['自來水', '回收水', '雨水'],
//            datasets: [{
//                data: [10000, 5000, 2000],
//                backgroundColor: ['#4caf50', '#2196f3', '#ffc107']
//            }]
//        },
//        options: {
//            responsive: true,
//            plugins: {
//                legend: {
//                    display: false // 隱藏色塊
//                },
//                title: {
//                    display: true,
//                    text: '水流向圖（單位:m³）'
//                }
//            },
//            scales: {
//                x: { title: { display: true, text: '來源' } },
//                y: { title: { display: true, text: '數值 (m³)' } }
//            }
//        }
//    });

//    // 用水密度圖初始化
//    const waterDensityCtx = document.getElementById('waterDensityChart').getContext('2d');
//    new Chart(waterDensityCtx, {
//        type: 'line',
//        data: {
//            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
//            datasets: [{
//                label: '實際值',
//                data: [400, 390, 380, 370, 360, 350, 340, 330, 320, 310, 300, 290],
//                borderColor: '#4caf50',
//                fill: false
//            }, {
//                label: '目標值',
//                data: [350, 350, 350, 350, 350, 350, 350, 350, 350, 350, 350, 350],
//                borderColor: '#f44336',
//                fill: false
//            }]
//        },
//        options: {
//            responsive: true,
//            plugins: {
//                legend: { position: 'top' },
//                title: {
//                    display: true,
//                    text: '用水密度 (m³/M USD)'
//                }
//            },
//            scales: {
//                x: { title: { display: true, text: '月份' } },
//                y: { title: { display: true, text: '用水量 (m³/M USD)' } }
//            }
//        }
//    });
//});

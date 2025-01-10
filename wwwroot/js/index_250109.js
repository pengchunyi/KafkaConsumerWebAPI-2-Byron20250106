// 初始化圖表
const initDonutChart = (ctx, data, labels) => {
	new Chart(ctx, {
		type: 'doughnut',
		data: {
			labels: labels,
			datasets: [{
				data: data,
				backgroundColor: ['#4caf50', '#ffc107'],
			}]
		},
		options: {
			responsive: true,
			plugins: {
				legend: { position: 'top' },
			}
		}
	});
};

const initBarChart = (ctx, labels, data) => {
	new Chart(ctx, {
		type: 'bar',
		data: {
			labels: labels,
			datasets: [{
				label: '目標值',
				data: data[0],
				backgroundColor: '#2196f3',
			}, {
				label: '實際值',
				data: data[1],
				backgroundColor: '#4caf50',
			}]
		},
		options: {
			responsive: true,
			scales: {
				x: { grid: { display: false } },
				y: { beginAtZero: true }
			}
		}
	});
};

document.addEventListener('DOMContentLoaded', () => {
	const donutCtx = document.getElementById('donutChart').getContext('2d');
	const barCtx1 = document.getElementById('barChart1').getContext('2d');
	const barCtx2 = document.getElementById('barChart2').getContext('2d');

	const donutData = [70, 30];
	const barLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
	const barData = [
		[105000, 109000, 110000, 115000, 120000, 125000, 130000, 135000, 140000, 145000, 150000, 155000],
		[85000, 89000, 90000, 95000, 100000, 110000, 120000, 125000, 130000, 135000, 140000, 145000]
	];

	initDonutChart(donutCtx, donutData, ['生產用電', '待機用電']);
	initBarChart(barCtx1, barLabels, barData);
	initBarChart(barCtx2, barLabels, barData);
});



//// 初始化圖表
//const initDonutChart = (ctx, data, labels) => {
//	new Chart(ctx, {
//		type: 'doughnut',
//		data: {
//			labels: labels,
//			datasets: [{
//				data: data,
//				backgroundColor: ['#4caf50', '#ffc107'], // 生產用電: 綠色, 待機用電: 黃色
//			}]
//		},
//		options: {
//			responsive: true,
//			plugins: {
//				legend: { position: 'top' },
//				datalabels: {
//					color: '#000', // 數值顏色（黑色）
//					anchor: 'center', // 將數值置於圖表的中心
//					align: 'center',
//					formatter: (value) => `${value}%` // 格式化數值為百分比
//				}
//			}
//		},
//		plugins: [ChartDataLabels] // 開啟 datalabels 插件
//	});
//};

//const initBarChart = (ctx, labels, data) => {
//	new Chart(ctx, {
//		type: 'bar',
//		data: {
//			labels: labels,
//			datasets: [{
//				label: '目標值',
//				data: data[0],
//				backgroundColor: '#2196f3',
//			}, {
//				label: '實際值',
//				data: data[1],
//				backgroundColor: '#4caf50',
//			}]
//		},
//		options: {
//			responsive: true,
//			scales: {
//				x: { grid: { display: false } },
//				y: { beginAtZero: true }
//			},
//			plugins: {
//				datalabels: {
//					color: '#000', // 數值顏色
//					anchor: 'end', // 將數值置於條形頂端
//					align: 'top',
//					formatter: (value) => value // 直接顯示數值
//				}
//			}
//		},
//		plugins: [ChartDataLabels] // 開啟 datalabels 插件
//	});
//};

//document.addEventListener('DOMContentLoaded', () => {
//	const donutCtx = document.getElementById('donutChart').getContext('2d');
//	const barCtx1 = document.getElementById('barChart1').getContext('2d');
//	const barCtx2 = document.getElementById('barChart2').getContext('2d');

//	const donutData = [70, 30];
//	const barLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
//	const barData = [
//		[105000, 109000, 110000, 115000, 120000, 125000, 130000, 135000, 140000, 145000, 150000, 155000],
//		[85000, 89000, 90000, 95000, 100000, 110000, 120000, 125000, 130000, 135000, 140000, 145000]
//	];

//	initDonutChart(donutCtx, donutData, ['生產用電', '待機用電']);
//	initBarChart(barCtx1, barLabels, barData);
//	initBarChart(barCtx2, barLabels, barData);
//});

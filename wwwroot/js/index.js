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

// 初始化儀表板圖表
const initGaugeChart = (ctx, value, max) => {
	new Chart(ctx, {
		type: 'gauge',
		data: {
			datasets: [{
				value: value, // 即時值
				data: Array.from({ length: 5 }, (_, i) => (max / 4) * i), // 區間數值
				backgroundColor: ['#4caf50', '#8bc34a', '#ffeb3b', '#ff9800', '#f44336'], // 顏色
			}]
		},
		options: {
			responsive: true,
			plugins: {
				legend: { display: false },
				tooltip: { enabled: false },
			},
			needle: {
				radiusPercentage: 2,
				widthPercentage: 3.2,
				lengthPercentage: 80,
				color: '#000' // 指針顏色
			},
			valueLabel: {
				display: true,
				formatter: (value) => `${value.toFixed(1)}`
			}
		}
	});
};


document.addEventListener('DOMContentLoaded', () => {
	// 1. 先宣告好所有要用到的「資料變數」
	const donutData = [70, 30];
	const barLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
	const barData = [
		[105000, 109000, 110000, 115000, 120000, 125000, 130000, 135000, 140000, 145000, 150000, 155000],
		[85000, 89000, 90000, 95000, 100000, 110000, 120000, 125000, 130000, 135000, 140000, 145000]
	];

	// 2. 建立你的 Canvas context
	const donutCtx = document.getElementById('donutChart').getContext('2d');
	const barCtx1 = document.getElementById('barChart1').getContext('2d');
	const barCtx2 = document.getElementById('barChart2').getContext('2d');

	// 3. 呼叫 Donut / Bar 的初始化
	initDonutChart(donutCtx, donutData, ['生產用電', '待機用電']);
	initBarChart(barCtx1, barLabels, barData);
	initBarChart(barCtx2, barLabels, barData);

	// 4. 同樣地，Gauge 也是先抓 Canvas，再呼叫初始化
	const gaugeCtx1 = document.getElementById('gaugeChart1').getContext('2d');
	const gaugeCtx2 = document.getElementById('gaugeChart2').getContext('2d');

	const gaugeValue1 = 7.1;
	const gaugeValue2 = 7.1;
	const maxGaugeValue = 10;

	initGaugeChart(gaugeCtx1, gaugeValue1, maxGaugeValue);
	initGaugeChart(gaugeCtx2, gaugeValue2, maxGaugeValue);
});





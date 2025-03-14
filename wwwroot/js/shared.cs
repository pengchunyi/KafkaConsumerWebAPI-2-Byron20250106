// 存儲休息時間的小時
let restHours = new Set();

async function fetchScheduleData(date)
{
	try
	{
		const response = await fetch(`http://localhost:5000/api/schedule?date=${date}`);


		if (!response.ok)
		{
			throw new Error("獲取數據失敗");
		}

		let scheduleData = await response.json();
		console.log("API 返回數據:", scheduleData); // Debugging

		restHours.clear(); // 清空休息時間

		// 如果 API 返回空數據，則不標記任何休息時間
		if (!scheduleData || scheduleData.length === 0)
		{
			return;
		}

		scheduleData.forEach(entry => {
			// 記錄休息時間的小時
			if (entry.type === "R")
			{
				const restHour = parseInt(entry.startTime.split(":")[0]); // 取得小時部分
				restHours.add(restHour);
			}
		});

		console.log("記錄的休息時段:", [...restHours]); // Debugging

	}
	catch (error)
	{
		console.error("加載排程數據時出錯:", error);
	}
}

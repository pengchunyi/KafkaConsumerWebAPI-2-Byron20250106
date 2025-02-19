using Microsoft.AspNetCore.Mvc;
using KafkaConsumerWebAPI.Services;
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace KafkaConsumerWebAPI.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class EnergyController : ControllerBase
	{
		private readonly MongoDBService _mongoDBService;

		public EnergyController(MongoDBService mongoDBService)
		{
			_mongoDBService = mongoDBService;
		}

		// GET /api/Energy/daily-consumption?date=2025-02-10
		//[HttpGet("daily-consumption")]
		//public async Task<IActionResult> GetDailyConsumption(string date)
		//{
		//	if (string.IsNullOrEmpty(date))
		//		return BadRequest("請提供有效的日期參數 (格式: YYYY-MM-DD)");

		//	// **1. 解析傳入的日期並轉換為 UTC 時間範圍**
		//	DateTime dt;
		//	try
		//	{
		//		dt = DateTime.Parse(date); // e.g., 2025-02-10
		//	}
		//	catch (FormatException)
		//	{
		//		return BadRequest("日期格式錯誤，請使用 YYYY-MM-DD");
		//	}

		//	// 設定 UTC 時間範圍 (UTC+08:00 → UTC)
		//	DateTime startUtc = dt.Date.AddHours(-8); // e.g., 2025-02-09 16:00 UTC
		//	DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1); // e.g., 2025-02-10 15:59:59 UTC

		//	// **2. 從 MongoDB 查詢**
		//	var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
		//	var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

		//	if (docs.Count == 0)
		//		return NotFound($"找不到 {date} (UTC+08:00) 內的數據");

		//	// **3. 分析 & 整理數據**
		//	var grouped = new Dictionary<(string source, int hour), double>();

		//	foreach (var doc in docs)
		//	{
		//		try
		//		{
		//			// 取得 TimeStamp
		//			var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
		//			var utcTime = DateTime.Parse(tsString);
		//			var localTime = utcTime.AddHours(8);
		//			int hour = localTime.Hour;

		//			// 取得 Source
		//			var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;

		//			// 取得 EnergyUsed
		//			var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

		//			var key = (source, hour);
		//			// 只保留最後一筆
		//			grouped[key] = energyUsed;
		//		}
		//		catch { }
		//	}

		//	// **4. 計算每小時能耗 (差分計算)**
		//	var hoursNeed = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
		//	var sources = new HashSet<string>(grouped.Keys.Select(k => k.source));

		//	var finalResult = new Dictionary<int, Dictionary<string, double>>();

		//	foreach (var hr in hoursNeed)
		//	{
		//		finalResult[hr] = new Dictionary<string, double>();
		//		foreach (var src in sources)
		//		{
		//			double prevUsed = 0;
		//			grouped.TryGetValue((src, hr - 1), out prevUsed);

		//			double thisUsed = 0;
		//			grouped.TryGetValue((src, hr), out thisUsed);

		//			double consumed = thisUsed - prevUsed;
		//			if (consumed < 0) consumed = 0;
		//			finalResult[hr][src] = consumed;
		//		}
		//	}

		//	// **5. 組裝回傳給前端的 JSON**
		//	var labelList = new List<string>();
		//	var sourceData = new Dictionary<string, List<double>>();

		//	foreach (var hr in hoursNeed)
		//	{
		//		labelList.Add($"{hr}:00");

		//		foreach (var src in sources)
		//		{
		//			if (!sourceData.ContainsKey(src))
		//			{
		//				sourceData[src] = new List<double>();
		//			}
		//			sourceData[src].Add(finalResult[hr][src]);
		//		}
		//	}

		//	return Ok(new
		//	{
		//		labels = labelList,
		//		data = sourceData
		//	});
		//}


		[HttpGet("daily-consumption")]
		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return BadRequest("請提供日期，例如 ?date=2025-02-07");
			}

			try
			{
				DateTime dt = DateTime.Parse(date); // 解析日期
				DateTime startUtc = dt.Date.AddHours(-8); // 轉換 UTC+8
				DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);

				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

				if (docs == null || docs.Count == 0)
				{
					return NotFound($"找不到 {date} (UTC+08:00) 內的數據");
				}

				var grouped = new Dictionary<int, Dictionary<string, double>>();
				var sources = new string[] { "CFX.A00.SO20050832.Trough", "CFX.A00.SO20050832.Preheat", "CFX.A00.SO20050832.Power" };

				foreach (var doc in docs)
				{
					try
					{
						var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
						var utcTime = DateTime.Parse(tsString);
						var localTime = utcTime.AddHours(8);
						int hour = localTime.Hour;

						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
						if (!sources.Contains(source)) continue;

						var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

						var key = hour;
						if (!grouped.ContainsKey(key)) grouped[key] = new Dictionary<string, double>();
						grouped[key][source] = energyUsed;
					}
					catch { }
				}

				var result = new
				{
					labels = grouped.Keys.OrderBy(x => x).Select(x => $"{x}:00").ToList(),
					data = new Dictionary<string, List<double>>()
				};

				foreach (var src in sources)
				{
					result.data[src] = grouped.Keys.OrderBy(x => x).Select(x => grouped[x].ContainsKey(src) ? grouped[x][src] : 0).ToList();
				}

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"發生錯誤: {ex.Message}");
			}
		}



	}
}

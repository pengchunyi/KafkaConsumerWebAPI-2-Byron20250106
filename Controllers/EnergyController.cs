//using Microsoft.AspNetCore.Mvc;
//using KafkaConsumerWebAPI.Services;
//using System;
//using System.Collections.Generic;
//using MongoDB.Bson;
//using MongoDB.Driver;
//using System.Threading.Tasks;

//namespace KafkaConsumerWebAPI.Controllers
//{
//	[ApiController]
//	[Route("api/[controller]")]
//	public class EnergyController : ControllerBase
//	{
//		private readonly MongoDBService _mongoDBService;

//		public EnergyController(MongoDBService mongoDBService)
//		{
//			_mongoDBService = mongoDBService;
//		}




//		//250217_這邊沒用到======================================
//		[HttpGet("daily-consumption")]
//		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
//		{
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期，例如 ?date=2025-02-07");
//			}

//			try
//			{
//				DateTime dt = DateTime.Parse(date); // 解析日期
//				DateTime startUtc = dt.Date.AddHours(-8); // 轉換 UTC+8
//				DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);

//				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
//				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

//				if (docs == null || docs.Count == 0)
//				{
//					return NotFound($"找不到 {date} (UTC+08:00) 內的數據");
//				}

//				var grouped = new Dictionary<int, Dictionary<string, double>>();
//				var sources = new string[] { "CFX.A00.SO20050832.Trough", "CFX.A00.SO20050832.Preheat", "CFX.A00.SO20050832.Power" };

//				foreach (var doc in docs)
//				{
//					try
//					{
//						var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
//						var utcTime = DateTime.Parse(tsString);
//						var localTime = utcTime.AddHours(8);
//						int hour = localTime.Hour;

//						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
//						if (!sources.Contains(source)) continue;

//						var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

//						var key = hour;
//						if (!grouped.ContainsKey(key)) grouped[key] = new Dictionary<string, double>();
//						grouped[key][source] = energyUsed;
//					}
//					catch { }
//				}

//				var result = new
//				{
//					labels = grouped.Keys.OrderBy(x => x).Select(x => $"{x}:00").ToList(),
//					data = new Dictionary<string, List<double>>()
//				};

//				foreach (var src in sources)
//				{
//					result.data[src] = grouped.Keys.OrderBy(x => x).Select(x => grouped[x].ContainsKey(src) ? grouped[x][src] : 0).ToList();
//				}

//				return Ok(result);
//			}
//			catch (Exception ex)
//			{
//				return StatusCode(500, $"發生錯誤: {ex.Message}");
//			}
//		}
//		//250217_這邊沒用到======================================


//	}
//}



using Microsoft.AspNetCore.Mvc;
using KafkaConsumerWebAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;       // 記得用
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

		[HttpGet("daily-consumption")]
		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return BadRequest("請提供日期，例如 ?date=2025-02-07");
			}

			try
			{
				//DateTime dt = DateTime.Parse(date);
				//// 假設資料庫存的是 UTC 時間 => dt.Date.AddHours(-8) => 當地 0:00
				//DateTime startUtc = dt.Date.AddHours(-8);
				//DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);
				// 假設 date = "2025-02-10"
				// 這裡我們直接把它視為「UTC 的 2025-02-10」，
				// 查詢範圍就是 2025-02-10 00:00:00 ~ 2025-02-10 23:59:59 (UTC)
				DateTime parsedDate = DateTime.Parse(date); // 會得到 2025/02/10 00:00 (本地), 但我們要確定用 UTC
															// 1) 先把 parsedDate 的年月日取出，並指定成 DateTimeKind.Utc
				DateTime startUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);
				DateTime endUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Utc);



				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

				// 如果查無資料 => 回傳空結果，而非 NotFound
				if (docs == null || docs.Count == 0)
				{
					var emptyResult = new
					{
						labels = new List<string>(),
						data = new Dictionary<string, List<double>>()
					};
					return Ok(emptyResult);
				}

				// 你的三種來源
				var sources = new string[] {
					"CFX.A00.SO20050832.Trough",
					"CFX.A00.SO20050832.Preheat",
					"CFX.A00.SO20050832.Power"
				};

				// 先用小時分組
				var grouped = new Dictionary<int, Dictionary<string, double>>();

				Console.WriteLine($"startUtc={startUtc:o}, endUtc={endUtc:o}");
				foreach (var doc in docs)
				{
					try
					{
						var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
						Console.WriteLine($"Doc TimeStamp={tsString}");
						var utcTime = DateTime.Parse(tsString);

						//var localTime = utcTime.AddHours(8); // 轉成本地時間
						// 如果你想要用「本地時間的小時」來畫圖，可以加上 .ToLocalTime()
						// 不過這樣 "21:29 UTC" 會是 "05:29 (隔天)"，就會是 hour = 5
						var localTime = utcTime.ToLocalTime();
						int hour = localTime.Hour;

						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
						if (!sources.Contains(source)) continue;

						var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

						if (!grouped.ContainsKey(hour))
							grouped[hour] = new Dictionary<string, double>();

						grouped[hour][source] = energyUsed;
					}
					catch (Exception ex)
					{
						Console.WriteLine($"解析失敗: {ex.Message}");
						// 也可加 stacktrace ex.StackTrace
						// 再 decide 要不要 rethrow
					}
				}

				// 準備回傳資料
				var result = new
				{
					labels = grouped.Keys.OrderBy(x => x).Select(x => $"{x}:00").ToList(),
					data = new Dictionary<string, List<double>>()
				};

				// 依照 source 順序產生對應的數值陣列
				foreach (var src in sources)
				{
					result.data[src] = grouped
						.Keys
						.OrderBy(x => x)
						.Select(x => grouped[x].ContainsKey(src) ? grouped[x][src] : 0)
						.ToList();
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

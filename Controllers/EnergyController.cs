//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using MongoDB.Bson;
//using KafkaConsumerWebAPI.Services;

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

//		/// <summary>
//		/// 取得 2025-02-07 00:00:00 ~ 23:59:59 (UTC+8) 的三種 Source (Preheat/Trough/Power) 每小時能耗
//		/// </summary>
//		[HttpGet("daily-consumption")]
//		public async Task<IActionResult> GetDailyConsumption()
//		{
//			// 1. 設定要查詢的 UTC 時間範圍 => 對應 2025-02-07 (UTC+8)
//			//    => 2025-02-07 00:00:00 UTC+8 = 2025-02-06 16:00:00 UTC
//			//    => 2025-02-07 23:59:59.9999999 UTC+8 = 2025-02-07 15:59:59.9999999 UTC
//			DateTime startUtc = new DateTime(2025, 2, 7, 0, 0, 0, DateTimeKind.Unspecified)
//				.AddHours(-8);
//			DateTime endUtc = new DateTime(2025, 2, 7, 23, 59, 59, 999, DateTimeKind.Unspecified)
//				.AddHours(-8);

//			// 2. 從 MongoDB 撈取時間範圍內(UTC)的 "EnergyConsumed" 訊息
//			//    依你現有Topic => "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed"
//			var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
//			var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

//			// 3. 我們依小時(UTC+8)進行分組，但要注意 doc 是 UTC+0
//			//    取 doc["Data"]["Data"]["RawData"]["TimeStamp"] 轉成 DateTime => 再加8小時 => 取得 hour
//			//    Source => doc["Data"]["Data"]["Meta"]["Source"]
//			//    EnergyUsed => doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"]
//			//    要拿"小時末" (xx:59:59) 的值，然後與前一小時做相減 => 取得那一小時的消耗量

//			// step.1: 先將結果按 Source & 時間 做分類
//			// key: (source, hour)
//			// value: EnergyUsed (要找同一小時段內 "最後一筆" 來對應 "xx:59:59")
//			var grouped = new Dictionary<(string source, int hour), double>();

//			foreach (var doc in docs)
//			{
//				try
//				{
//					// TimeStamp in UTC
//					var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
//					var utcTime = DateTime.Parse(tsString);

//					// 轉換成本地(UTC+8)時間
//					var localTime = utcTime.AddHours(8);

//					// 取得該筆的整點小時 => localTime.Hour (0~23)
//					int hour = localTime.Hour;

//					// 取得 Source
//					var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
//					// 只關心 Trough / Preheat / Power
//					if (source != "CFX.A00.SO20050832.Trough" &&
//						source != "CFX.A00.SO20050832.Preheat" &&
//						source != "CFX.A00.SO20050832.Power")
//					{
//						continue;
//					}

//					// 取得 EnergyUsed
//					var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

//					// 可能同一小時多筆 => 只取最後一筆(EnergyUsed最大時間點)
//					// 可用 last-write-wins 的方式: 反正遍歷是按 TimeStamp 升序
//					// => 每次都覆蓋 => 最後留到 grouped[key] 的就是該小時最後一筆
//					var key = (source, hour);
//					grouped[key] = energyUsed;
//				}
//				catch { /* 有解析失敗就略過 */ }
//			}

//			// step.2: 算出"每小時耗能" => 需要當前小時(EnergyUsed) - 前一小時(EnergyUsed)
//			// 例如 (source, hour=8) - (source, hour=7)
//			// 這裡要注意: 你要的時段為 0~23 => labels: 8:00 AM, 9:00 AM... etc
//			// 可能 logs 不一定都有 0~23 ，需要自己列出 hour=0~23 做計算
//			var hourRange = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };

//			// 依需求對 8:00AM ~ 8:00PM => 8~20? 你自己可調整
//			// 你給的 label: 8:00 AM ~ 8:00 PM => hour=8..20 => total 13 小時
//			// 這裡直接根據 labels "8..20" 先示範
//			var hoursNeed = new int[] { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
//			var sources = new string[] {
//				"CFX.A00.SO20050832.Trough",
//				"CFX.A00.SO20050832.Preheat",
//				"CFX.A00.SO20050832.Power"
//			};

//			// key: hour => Dictionary<source, eachHourConsumption>
//			var finalResult = new Dictionary<int, Dictionary<string, double>>();

//			foreach (var hr in hoursNeed)
//			{
//				finalResult[hr] = new Dictionary<string, double>();
//				foreach (var src in sources)
//				{
//					// 先去找 "上個小時"
//					double prevUsed = 0;
//					if (grouped.TryGetValue((src, hr - 1), out double val2))
//					{
//						prevUsed = val2;
//					}
//					// 再找現在
//					double thisUsed = 0;
//					if (grouped.TryGetValue((src, hr), out double val3))
//					{
//						thisUsed = val3;
//					}
//					// 小時耗能 = thisUsed - prevUsed
//					// (若沒有任何紀錄 => 0 - 0 = 0)
//					var consumed = thisUsed - prevUsed;
//					if (consumed < 0) consumed = 0; // 可能出現歸0/清零? => 不確定 => 自行決策

//					finalResult[hr][src] = consumed;
//				}
//			}

//			// step.3: 組裝前端需要的結構 => 
//			// 例如: 
//			// {
//			//   labels: [ "8", "9", "10", ... ],
//			//   data: {
//			//       Trough: [0.5, 1.2, ...],
//			//       Preheat: [...],
//			//       Power: [...]
//			//   }
//			// }
//			var labelList = new List<string>();
//			var troughList = new List<double>();
//			var preheatList = new List<double>();
//			var powerList = new List<double>();

//			foreach (var hr in hoursNeed)
//			{
//				labelList.Add($"{hr}:00"); // "8:00", "9:00", ...
//				troughList.Add(finalResult[hr]["CFX.A00.SO20050832.Trough"]);
//				preheatList.Add(finalResult[hr]["CFX.A00.SO20050832.Preheat"]);
//				powerList.Add(finalResult[hr]["CFX.A00.SO20050832.Power"]);
//			}

//			// 回傳給前端
//			var response = new
//			{
//				labels = labelList,
//				data = new
//				{
//					Trough = troughList,
//					Preheat = preheatList,
//					Power = powerList
//				}
//			};

//			return Ok(response);
//		}
//	}
//}



using Microsoft.AspNetCore.Mvc;
using KafkaConsumerWebAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

		// GET /api/Energy/daily-consumption?date=2025-02-07
		[HttpGet("daily-consumption")]
		public async Task<IActionResult> GetDailyConsumption(string date = "2025-02-07")
		{
			// 1. 將 2025-02-07 => UTC 範圍 2025-02-06 16:00 ~ 2025-02-07 15:59:59.9999999
			DateTime dt = DateTime.Parse(date); // 2025-02-07
			DateTime startUtc = dt.Date.AddHours(-8);
			DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);

			// 2. 從 MongoDB 查詢
			var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
			var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

			// 3. 分析 => 依小時 & Source (Trough / Preheat / Power) 分組 => 取 last(EnergyUsed)
			//    然後做差分 => 每小時用量
			var grouped = new Dictionary<(string source, int hour), double>();
			foreach (var doc in docs)
			{
				try
				{
					// timeStamp
					var tsString = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
					var utcTime = DateTime.Parse(tsString);
					var localTime = utcTime.AddHours(8);
					int hour = localTime.Hour;

					// Source
					var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
					if (source != "CFX.A00.SO20050832.Trough" &&
						source != "CFX.A00.SO20050832.Preheat" &&
						source != "CFX.A00.SO20050832.Power")
					{
						continue;
					}

					// EnergyUsed
					var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

					var key = (source, hour);
					// 若同小時有多筆 => 保留最後一筆
					grouped[key] = energyUsed;
				}
				catch { }
			}

			// hours => 8..20 (只做示範13hr)
			var hoursNeed = new int[] { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
			var sources = new string[] {
				"CFX.A00.SO20050832.Trough",
				"CFX.A00.SO20050832.Preheat",
				"CFX.A00.SO20050832.Power"
			};

			// 差分 => thisHr - prevHr
			var finalResult = new Dictionary<int, Dictionary<string, double>>();
			foreach (var hr in hoursNeed)
			{
				finalResult[hr] = new Dictionary<string, double>();
				foreach (var src in sources)
				{
					double prevUsed = 0;
					grouped.TryGetValue((src, hr - 1), out prevUsed);

					double thisUsed = 0;
					grouped.TryGetValue((src, hr), out thisUsed);

					double consumed = thisUsed - prevUsed;
					if (consumed < 0) consumed = 0;
					finalResult[hr][src] = consumed;
				}
			}

			// 組裝前端 => labels, Trough, Preheat, Power
			var labelList = new List<string>();
			var troughList = new List<double>();
			var preheatList = new List<double>();
			var powerList = new List<double>();

			foreach (var hr in hoursNeed)
			{
				labelList.Add($"{hr}:00");
				troughList.Add(finalResult[hr]["CFX.A00.SO20050832.Trough"]);
				preheatList.Add(finalResult[hr]["CFX.A00.SO20050832.Preheat"]);
				powerList.Add(finalResult[hr]["CFX.A00.SO20050832.Power"]);
			}

			return Ok(new
			{
				labels = labelList,
				data = new
				{
					Trough = troughList,
					Preheat = preheatList,
					Power = powerList
				}
			});
		}
	}
}

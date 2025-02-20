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


				//250220新增===================================================================================
				// 事先準備：用一個 Dictionary<string, double> 來儲存「每個 source 上次的 EnergyUsed」。
				var lastValueMap = new Dictionary<string, double>();
				//250220新增===================================================================================

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
						//250220新增===================================================
						var tsElement = doc["Data"]["Data"]["RawData"]["TimeStamp"];
						DateTime utcTime;
						if (tsElement.IsBsonDateTime)
						{
							// 這筆資料已是 BsonDateTime 了
							// 直接轉成 C# DateTime
							utcTime = tsElement.ToUniversalTime();
						}
						else
						{
							// 還是字串
							utcTime = DateTime.Parse(tsElement.AsString).ToUniversalTime();
						}
						//250220新增===================================================



						//var localTime = utcTime.AddHours(8); // 轉成本地時間
						// 如果你想要用「本地時間的小時」來畫圖，可以加上 .ToLocalTime()
						// 不過這樣 "21:29 UTC" 會是 "05:29 (隔天)"，就會是 hour = 5
						var localTime = utcTime.ToLocalTime();
						int hour = localTime.Hour;

						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
						if (!sources.Contains(source)) continue;

						var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();


						//250220新增===================================================================================
						// 4) 取出「上一次」的累計量 (若沒有，就預設 0)
						double lastVal = 0.0;
						if (lastValueMap.ContainsKey(source))
						{
							lastVal = lastValueMap[source];
						}

						// 5) 「本小時用電量」= (本次累計) - (上次累計)
						double hourUsage = energyUsed - lastVal;
						// 如果要避免出現負值或不合理值，可以再判斷 hourUsage < 0 就當作 0 或跳過

						// 更新「上次累計量」
						lastValueMap[source] = energyUsed;
						//250220新增===================================================================================

						if (!grouped.ContainsKey(hour))
							grouped[hour] = new Dictionary<string, double>();

						grouped[hour][source] = hourUsage;
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


//using Microsoft.AspNetCore.Mvc;
//using KafkaConsumerWebAPI.Services;
//using System;
//using System.Collections.Generic;
//using System.Linq;
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

//		[HttpGet("daily-consumption")]
//		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
//		{
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期 (yyyy-MM-dd)");
//			}

//			try
//			{
//				// 1) 將前端 date 當作「UTC 的這一天」
//				DateTime parsedDate = DateTime.Parse(date);
//				DateTime startUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);
//				DateTime endUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Utc);

//				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
//				// 2) 查詢該日 (UTC) 的所有文件 (已按TimeStamp升冪)
//				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

//				if (docs == null || docs.Count == 0)
//				{
//					// 回傳空
//					var emptyResult = new
//					{
//						labels = new List<string>(),
//						data = new Dictionary<string, List<double>>()
//					};
//					return Ok(emptyResult);
//				}

//				// 你想處理的來源
//				var sources = new string[] {
//					"CFX.A00.SO20050832.Trough",
//					"CFX.A00.SO20050832.Preheat",
//					"CFX.A00.SO20050832.Power"
//				};

//				// grouped[hour][source] => double (該小時累加的用電量)
//				// 先準備 0~23 每小時 (你要只顯示有資料也可再改)
//				var grouped = new Dictionary<int, Dictionary<string, double>>();
//				for (int h = 0; h < 24; h++)
//				{
//					grouped[h] = new Dictionary<string, double>();
//					foreach (var s in sources)
//					{
//						grouped[h][s] = 0;
//					}
//				}

//				// 用來記錄「上一次」該 source 的累計值
//				var lastValueMap = new Dictionary<string, double>();

//				// 3) 按照文件時間升序遍歷
//				//    取得「差值 = 當前 - 上次」 => 累加到該小時
//				foreach (var doc in docs)
//				{
//					try
//					{
//						// 3-1) 取出 TimeStamp
//						var tsElement = doc["Data"]["Data"]["RawData"]["TimeStamp"];
//						DateTime utcTime;
//						if (tsElement.IsBsonDateTime)
//							utcTime = tsElement.ToUniversalTime();
//						else
//							utcTime = DateTime.Parse(tsElement.AsString).ToUniversalTime();

//						// 3-2) 若本地日期 != parsedDate => 跳過
//						var localTime = utcTime.ToLocalTime();
//						if (localTime.Date != parsedDate.Date)
//							continue;

//						// 3-3) 取出 source / EnergyUsed
//						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
//						if (!sources.Contains(source))
//							continue;

//						double energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

//						// 3-4) 計算「差值 = 本次 - 上次」
//						double lastVal = 0.0;
//						if (lastValueMap.TryGetValue(source, out double prevVal))
//						{
//							lastVal = prevVal;
//						}
//						double usage = energyUsed - lastVal;
//						if (usage < 0) usage = 0;

//						// 3-5) 把差值「累加到當前文件所在小時」
//						int hour = localTime.Hour;
//						grouped[hour][source] += usage;

//						// 3-6) 更新 lastValueMap
//						lastValueMap[source] = energyUsed;
//					}
//					catch (Exception ex)
//					{
//						Console.WriteLine($"解析失敗: {ex.Message}");
//					}
//				}

//				// 4) 組 labels/data => labels= 0..23:00
//				var labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList();

//				var resultData = new Dictionary<string, List<double>>();
//				foreach (var s in sources)
//				{
//					var list = new List<double>();
//					for (int h = 0; h < 24; h++)
//					{
//						list.Add(grouped[h][s]);
//					}
//					resultData[s] = list;
//				}

//				var result = new
//				{
//					labels = labels,
//					data = resultData
//				};

//				return Ok(result);
//			}
//			catch (Exception ex)
//			{
//				return StatusCode(500, $"發生錯誤: {ex.Message}");
//			}
//		}
//	}
//}

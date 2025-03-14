////using Microsoft.AspNetCore.Mvc;
////using KafkaConsumerWebAPI.Services;
////using System;
////using System.Collections.Generic;
////using System.Linq;       // 記得用
////using MongoDB.Bson;
////using MongoDB.Driver;
////using System.Threading.Tasks;

////namespace KafkaConsumerWebAPI.Controllers
////{
////	[ApiController]
////	[Route("api/[controller]")]
////	public class EnergyController : ControllerBase
////	{
////		private readonly MongoDBService _mongoDBService;

////		public EnergyController(MongoDBService mongoDBService)
////		{
////			_mongoDBService = mongoDBService;
////		}

////		[HttpGet("daily-consumption")]
////		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
////		{
////			if (string.IsNullOrEmpty(date))
////			{
////				return BadRequest("請提供日期，例如 ?date=2025-02-07");
////			}

////			try
////			{
////				//DateTime dt = DateTime.Parse(date);
////				//// 假設資料庫存的是 UTC 時間 => dt.Date.AddHours(-8) => 當地 0:00
////				//DateTime startUtc = dt.Date.AddHours(-8);
////				//DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);
////				// 假設 date = "2025-02-10"
////				// 這裡我們直接把它視為「UTC 的 2025-02-10」，
////				// 查詢範圍就是 2025-02-10 00:00:00 ~ 2025-02-10 23:59:59 (UTC)
////				DateTime parsedDate = DateTime.Parse(date); // 會得到 2025/02/10 00:00 (本地), 但我們要確定用 UTC
////															// 1) 先把 parsedDate 的年月日取出，並指定成 DateTimeKind.Utc
////				DateTime startUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);
////				DateTime endUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Utc);



////				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
////				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

////				// 如果查無資料 => 回傳空結果，而非 NotFound
////				if (docs == null || docs.Count == 0)
////				{
////					var emptyResult = new
////					{
////						labels = new List<string>(),
////						data = new Dictionary<string, List<double>>()
////					};
////					return Ok(emptyResult);
////				}


////				//250220新增===================================================================================
////				// 事先準備：用一個 Dictionary<string, double> 來儲存「每個 source 上次的 EnergyUsed」。
////				var lastValueMap = new Dictionary<string, double>();
////				//250220新增===================================================================================

////				// 你的三種來源
////				var sources = new string[] {
////					"CFX.A00.SO20050832.Trough",
////					"CFX.A00.SO20050832.Preheat",
////					"CFX.A00.SO20050832.Power"
////				};

////				// 先用小時分組
////				var grouped = new Dictionary<int, Dictionary<string, double>>();

////				Console.WriteLine($"startUtc={startUtc:o}, endUtc={endUtc:o}");

////				foreach (var doc in docs)
////				{
////					try
////					{
////						//250220新增===================================================
////						var tsElement = doc["Data"]["Data"]["RawData"]["TimeStamp"];
////						DateTime utcTime;
////						if (tsElement.IsBsonDateTime)
////						{
////							// 這筆資料已是 BsonDateTime 了
////							// 直接轉成 C# DateTime
////							utcTime = tsElement.ToUniversalTime();
////						}
////						else
////						{
////							// 還是字串
////							utcTime = DateTime.Parse(tsElement.AsString).ToUniversalTime();
////						}
////						//250220新增===================================================



////						//var localTime = utcTime.AddHours(8); // 轉成本地時間
////						// 如果你想要用「本地時間的小時」來畫圖，可以加上 .ToLocalTime()
////						// 不過這樣 "21:29 UTC" 會是 "05:29 (隔天)"，就會是 hour = 5
////						var localTime = utcTime.ToLocalTime();
////						int hour = localTime.Hour;

////						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
////						if (!sources.Contains(source)) continue;

////						var energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();


////						//250220新增===================================================================================
////						// 4) 取出「上一次」的累計量 (若沒有，就預設 0)
////						double lastVal = 0.0;
////						if (lastValueMap.ContainsKey(source))
////						{
////							lastVal = lastValueMap[source];
////						}

////						// 5) 「本小時用電量」= (本次累計) - (上次累計)
////						double hourUsage = energyUsed - lastVal;
////						// 如果要避免出現負值或不合理值，可以再判斷 hourUsage < 0 就當作 0 或跳過

////						// 更新「上次累計量」
////						lastValueMap[source] = energyUsed;
////						//250220新增===================================================================================

////						if (!grouped.ContainsKey(hour))
////							grouped[hour] = new Dictionary<string, double>();

////						grouped[hour][source] = hourUsage;
////					}
////					catch (Exception ex)
////					{
////						Console.WriteLine($"解析失敗: {ex.Message}");
////						// 也可加 stacktrace ex.StackTrace
////						// 再 decide 要不要 rethrow
////					}
////				}

////				// 準備回傳資料
////				var result = new
////				{
////					labels = grouped.Keys.OrderBy(x => x).Select(x => $"{x}:00").ToList(),
////					data = new Dictionary<string, List<double>>()
////				};

////				// 依照 source 順序產生對應的數值陣列
////				foreach (var src in sources)
////				{
////					result.data[src] = grouped
////						.Keys
////						.OrderBy(x => x)
////						.Select(x => grouped[x].ContainsKey(src) ? grouped[x][src] : 0)
////						.ToList();
////				}

////				return Ok(result);
////			}
////			catch (Exception ex)
////			{
////				return StatusCode(500, $"發生錯誤: {ex.Message}");
////			}
////		}
////	}
////}




////using Microsoft.AspNetCore.Mvc;
////using KafkaConsumerWebAPI.Services;
////using System;
////using System.Collections.Generic;
////using System.Linq;       // 記得用
////using MongoDB.Bson;
////using MongoDB.Driver;
////using System.Threading.Tasks;

////namespace KafkaConsumerWebAPI.Controllers
////{
////	[ApiController]
////	[Route("api/[controller]")]
////	public class EnergyController : ControllerBase
////	{
////		private readonly MongoDBService _mongoDBService;

////		public EnergyController(MongoDBService mongoDBService)
////		{
////			_mongoDBService = mongoDBService;
////		}

////		[HttpGet("daily-consumption")]
////		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
////		{
////			if (string.IsNullOrEmpty(date))
////			{
////				return BadRequest("請提供日期，例如 ?date=2025-02-07");
////			}

////			try
////			{
////				//DateTime dt = DateTime.Parse(date);
////				//// 假設資料庫存的是 UTC 時間 => dt.Date.AddHours(-8) => 當地 0:00
////				//DateTime startUtc = dt.Date.AddHours(-8);
////				//DateTime endUtc = dt.Date.AddDays(1).AddHours(-8).AddTicks(-1);
////				// 假設 date = "2025-02-10"
////				// 這裡我們直接把它視為「UTC 的 2025-02-10」，
////				// 查詢範圍就是 2025-02-10 00:00:00 ~ 2025-02-10 23:59:59 (UTC)
////				DateTime parsedDate = DateTime.Parse(date); // 會得到 2025/02/10 00:00 (本地), 但我們要確定用 UTC
////															// 1) 先把 parsedDate 的年月日取出，並指定成 DateTimeKind.Utc
////				DateTime startUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);
////				DateTime endUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Utc);



////				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
////				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

////				// 如果查無資料 => 回傳空結果，而非 NotFound
////				if (docs == null || docs.Count == 0)
////				{
////					var emptyResult = new
////					{
////						labels = new List<string>(),
////						data = new Dictionary<string, List<double>>()
////					};
////					return Ok(emptyResult);
////				}


////				//250220新增===================================================================================
////				// 事先準備：用一個 Dictionary<string, double> 來儲存「每個 source 上次的 EnergyUsed」。
////				var lastValueMap = new Dictionary<string, double>();
////				//250220新增===================================================================================

////				// 你的三種來源
////				var sources = new string[] {
////					"CFX.A00.SO20050832.Trough",
////					"CFX.A00.SO20050832.Preheat",
////					"CFX.A00.SO20050832.Power"
////				};

////				// 先用小時分組
////				var grouped = new Dictionary<int, Dictionary<string, double>>();

////				foreach (var source in sources)
////				{
////					// 過濾該 source 的資料
////					var sourceDocs = docs.Where(d => d["Data"]["Data"]["Meta"]["Source"].AsString == source).ToList();

////					if (sourceDocs.Count == 0) continue; // 該來源沒資料

////					// 取得當前小時的第一筆與最後一筆資料
////					var firstRecord = sourceDocs.FirstOrDefault();
////					var lastRecord = sourceDocs.LastOrDefault();

////					if (firstRecord != null && lastRecord != null)
////					{
////						DateTime firstTime = firstRecord["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
////						DateTime lastTime = lastRecord["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();

////						//int hour = firstTime.ToLocalTime().Hour; // 依據第一筆數據確定小時
////						int hour = firstTime.Hour; // 這樣 hour 才是 UTC 時間


////						double firstEnergyUsed = firstRecord["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
////						double lastEnergyUsed = lastRecord["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
////						double hourlyConsumption = lastEnergyUsed - firstEnergyUsed; // 正確計算當小時用電

////						if (!grouped.ContainsKey(hour))
////							grouped[hour] = new Dictionary<string, double>();

////						//250220新增============
////						if (!grouped[hour].ContainsKey(source))
////							grouped[hour][source] = 0; // 初始化
////						//250220新增============

////						//grouped[hour][source] = hourlyConsumption;
////						//250220新增============
////						grouped[hour][source] += hourlyConsumption; // 累加該小時內的消耗值
////						//250220新增============
////					}
////				}

////				// 準備回傳資料
////				var result = new
////				{
////					labels = grouped.Keys.OrderBy(x => x).Select(x => $"{x}:00").ToList(),
////					data = new Dictionary<string, List<double>>()
////				};

////				// 依照 source 順序產生對應的數值陣列
////				foreach (var src in sources)
////				{
////					result.data[src] = grouped
////						.Keys
////						.OrderBy(x => x)
////						.Select(x => grouped[x].ContainsKey(src) ? grouped[x][src] : 0)
////						.ToList();
////				}

////				return Ok(result);
////			}
////			catch (Exception ex)
////			{
////				return StatusCode(500, $"發生錯誤: {ex.Message}");
////			}
////		}
////	}
////}




////using Microsoft.AspNetCore.Mvc;
////using KafkaConsumerWebAPI.Services;
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using MongoDB.Bson;
////using MongoDB.Driver;
////using System.Threading.Tasks;

////namespace KafkaConsumerWebAPI.Controllers
////{
////	[ApiController]
////	[Route("api/[controller]")]
////	public class EnergyController : ControllerBase
////	{
////		private readonly MongoDBService _mongoDBService;

////		public EnergyController(MongoDBService mongoDBService)
////		{
////			_mongoDBService = mongoDBService;
////		}

////		[HttpGet("daily-consumption")]
////		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
////		{
////			if (string.IsNullOrEmpty(date))
////			{
////				return BadRequest("請提供日期，例如 ?date=2025-02-07");
////			}

////			try
////			{
////				// 將 date 視為 2025-02-11 (UTC 00:00:00 ~ 23:59:59.999)
////				DateTime parsedDate = DateTime.Parse(date);
////				DateTime startUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Utc);
////				DateTime endUtc = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Utc);

////				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
////				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

////				// 若無資料，回傳空陣列
////				if (docs == null || docs.Count == 0)
////				{
////					var emptyResult = new
////					{
////						labels = new List<string>(),
////						data = new Dictionary<string, List<double>>() // source => [小時0..23的kWh]
////					};
////					return Ok(emptyResult);
////				}

////				// 3 個來源
////				var sources = new string[] {
////					"CFX.A00.SO20050832.Trough",
////					"CFX.A00.SO20050832.Preheat",
////					"CFX.A00.SO20050832.Power"
////				};

////				// 建立一個結構: grouped[hour][source] = (第一筆EnergyUsed, 最後一筆EnergyUsed)
////				// 先全部初始化為 (null, null)，以確保 0~23 小時都有
////				var grouped = new Dictionary<int, Dictionary<string, (double? first, double? last)>>();

////				// 初始化 0..23 每小時 -> 每 source
////				for (int h = 0; h < 24; h++)
////				{
////					grouped[h] = new Dictionary<string, (double?, double?)>();
////					foreach (var s in sources)
////					{
////						grouped[h][s] = (null, null);
////					}
////				}

////				// 將資料依照「Local 時間的小時 + source」填入 first / last
////				foreach (var doc in docs)
////				{
////					try
////					{
////						// 取得 source
////						var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
////						if (!sources.Contains(source))
////							continue; // 不在我們關心的 source 清單裡

////						// 取得 EnergyUsed
////						double energyUsed = doc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

////						// 取得時間 => 轉成本地時間 => 取當前小時
////						// (若要以 UTC 分組，則改成 ToUniversalTime().Hour)
////						DateTime utcTime = doc["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
////						int hour = utcTime.ToLocalTime().Hour;

////						// 取出現在 grouped[hour][source] 的 (first, last)
////						var tuple = grouped[hour][source];
////						var currentFirst = tuple.first;
////						var currentLast = tuple.last;

////						// 如果 first == null => 表示這是該小時該 source 的第一筆資料
////						if (currentFirst == null)
////						{
////							currentFirst = energyUsed;
////						}

////						// 不斷更新最後一筆
////						currentLast = energyUsed;

////						// 更新回 grouped
////						grouped[hour][source] = (currentFirst, currentLast);
////					}
////					catch
////					{
////						// 這裡可以 log 錯誤，或忽略
////					}
////				}

////				// 計算每小時的消耗: last - first
////				// 準備要回傳給前端的結構：
////				// labels: ["0:00", "1:00", ..., "23:00"]
////				// data[source] = [hour0耗能, hour1耗能, ..., hour23耗能]
////				var result = new
////				{
////					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
////					data = new Dictionary<string, List<double>>()
////				};

////				// 先為每個 source 準備一個 24 小時的列表
////				foreach (var s in sources)
////				{
////					result.data[s] = new List<double>(new double[24]); // 預設 24 個 0
////				}

////				// 逐小時 + 逐 source 計算
////				for (int h = 0; h < 24; h++)
////				{
////					foreach (var s in sources)
////					{
////						var (firstVal, lastVal) = grouped[h][s];
////						if (firstVal.HasValue && lastVal.HasValue)
////						{
////							double consumption = lastVal.Value - firstVal.Value;
////							// 若消耗量小於 0，代表資料不正確，可視情況歸 0
////							if (consumption < 0)
////								consumption = 0;

////							result.data[s][h] = consumption;
////						}
////						else
////						{
////							// 該小時沒資料 => 0
////							result.data[s][h] = 0;
////						}
////					}
////				}

////				return Ok(result);
////			}
////			catch (Exception ex)
////			{
////				return StatusCode(500, $"發生錯誤: {ex.Message}");
////			}
////		}
////	}
////}


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

		[HttpGet("daily-consumption")]
		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return BadRequest("請提供日期，例如 ?date=2025-02-07");
			}

			try
			{
				// 1) 將前端傳來的 date 視為「本地時間」的當天 0:00 ~ 23:59:59.999
				DateTime parsedDate = DateTime.Parse(date);
				// 本地時間的該天起點 (0:00) & 結束 (23:59:59.999)
				DateTime localDayStart = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Local);
				DateTime localDayEnd = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Local);

				// 轉成 UTC 用來查資料庫
				DateTime startUtc = localDayStart.ToUniversalTime();
				DateTime endUtc = localDayEnd.ToUniversalTime();

				// 查詢 MongoDB
				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
				//var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyCleaned_20250211";
				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

				// 若無資料，回傳空結果
				if (docs == null || docs.Count == 0)
				{
					var emptyResult = new
					{
						labels = new List<string>(),
						data = new Dictionary<string, List<double>>()
					};
					return Ok(emptyResult);
				}

				// 2) 這裡定義 3 種來源
				var sources = new string[]
				{
					"CFX.A00.SO20050832.Trough",
					"CFX.A00.SO20050832.Preheat",
					"CFX.A00.SO20050832.Power"
				};

				// 3) 準備一個結構 grouped[source][hour] => List<BsonDocument> 
				//    先把所有紀錄分到「本地小時」的桶子裡
				var grouped = new Dictionary<string, Dictionary<int, List<BsonDocument>>>();
				foreach (var s in sources)
				{
					grouped[s] = new Dictionary<int, List<BsonDocument>>();
					for (int h = 0; h < 24; h++)
					{
						grouped[s][h] = new List<BsonDocument>();
					}
				}

				// 4) 遍歷所有查到的紀錄，按「本地時間」的 Hour 分桶
				foreach (var doc in docs)
				{
					var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
					if (!sources.Contains(source))
						continue;

					// 取出 TimeStamp (UTC) -> 轉成本地時間
					var timeStamp = doc["Data"]["Data"]["RawData"]["TimeStamp"];
					DateTime docUtc = timeStamp.ToUniversalTime();
					DateTime docLocal = docUtc.ToLocalTime();

					// 確認是不是同一天 (避免跨天)
					if (docLocal.Date != localDayStart.Date)
						continue;

					int hour = docLocal.Hour; // 0..23
					grouped[source][hour].Add(doc);
				}

				// 5) 設定回傳格式 => labels = 0..23, data[source] = 24個小時的耗能
				var result = new
				{
					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
					data = new Dictionary<string, List<double>>()
				};
				// 先初始化 data[source] = 24個 0
				foreach (var s in sources)
				{
					result.data[s] = new List<double>(new double[24]);
				}

				// 6) 對於每個小時、每個 source，計算「最早一筆 & 最晚一筆」的差值
				for (int h = 0; h < 24; h++)
				{
					foreach (var s in sources)
					{
						var docsInHour = grouped[s][h];
						if (docsInHour.Count < 2)
						{
							// 只有 0 or 1 筆 => 無法計算消耗 => 就是 0
							result.data[s][h] = 0;
							continue;
						}

						// 按照 TimeStamp 升冪排序
						docsInHour.Sort((a, b) =>
						{
							DateTime aTime = a["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
							DateTime bTime = b["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
							return aTime.CompareTo(bTime);
						});

						double firstEnergyUsed = docsInHour.First()["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
						double lastEnergyUsed = docsInHour.Last()["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

						double consumption = lastEnergyUsed - firstEnergyUsed;
						if (consumption < 0) consumption = 0; // 若出現負值就當作 0

						result.data[s][h] = consumption;
					}
				}

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"發生錯誤: {ex.Message}");
			}
		}


		//		[HttpGet("daily-consumption")]
		//		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
		//		{
		//			if (string.IsNullOrEmpty(date))
		//			{
		//				return BadRequest("請提供日期，例如 ?date=2025-02-07");
		//			}

		//			try
		//			{
		//				// 1) 解析日期，確保是本地時間
		//				DateTime parsedDate = DateTime.Parse(date);
		//				DateTime localDayStart = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Local);
		//				DateTime localDayEnd = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Local);

		//				// 轉換成 UTC 查詢
		//				DateTime startUtc = localDayStart.ToUniversalTime();
		//				DateTime endUtc = localDayEnd.ToUniversalTime();

		//				// 新的 MongoDB Collection，已經預處理為 "FirstDoc" & "LastDoc" 形式
		//				var collectionName = "EnergyCleaned_20250211";
		//				//var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

		//				var filter = Builders<BsonDocument>.Filter.And(
		//							Builders<BsonDocument>.Filter.Gte("Hour", 0),
		//							Builders<BsonDocument>.Filter.Lte("Hour", 23)
		//						);

		//				var docs = await _mongoDBService.QueryCollectionAsync(collectionName, filter);




		//				// 若無資料，回傳空
		//				if (docs == null || docs.Count == 0)
		//				{
		//					var emptyResult = new
		//					{
		//						labels = new List<string>(),
		//						data = new Dictionary<string, List<double>>()
		//					};
		//					return Ok(emptyResult);
		//				}

		//				// 2) 定義 3 個 Source
		//				var sources = new string[]
		//				{
		//			"CFX.A00.SO20050832.Trough",
		//			"CFX.A00.SO20050832.Preheat",
		//			"CFX.A00.SO20050832.Power"
		//				};

		//				// 3) 先初始化 24 小時的結構
		//				var result = new
		//				{
		//					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
		//					data = new Dictionary<string, List<double>>()
		//				};

		//				// 初始化 data[source] = 24 個 0
		//				foreach (var s in sources)
		//				{
		//					result.data[s] = new List<double>(new double[24]);
		//				}

		//				// 4) 遍歷 MongoDB 數據，按照 "Hour" & "Source" 分類
		//				foreach (var doc in docs)
		//				{
		//					try
		//					{
		//						// 取得時間 & Source
		//						int hour = doc["Hour"].ToInt32();
		//						string source = doc["Source"].AsString;

		//						// 只處理已定義的 Source
		//						if (!sources.Contains(source))
		//							continue;

		//						// 取得當小時的最早 & 最晚數據
		//						double firstEnergyUsed = doc["FirstEnergyUsed"].ToDouble();
		//						double lastEnergyUsed = doc["LastEnergyUsed"].ToDouble();

		//						// 計算當小時消耗
		//						double consumption = lastEnergyUsed - firstEnergyUsed;
		//						if (consumption < 0) consumption = 0; // 避免負數

		//						// 存入結果
		//						result.data[source][hour] = consumption;
		//					}
		//					catch (Exception ex)
		//					{
		//						Console.WriteLine($"解析失敗: {ex.Message}");
		//					}
		//				}

		//				return Ok(result);
		//			}
		//			catch (Exception ex)
		//			{
		//				return StatusCode(500, $"發生錯誤: {ex.Message}");
		//			}
		//		}



	}
}






//250221更新資料庫部分===============================================================
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
//			// 1. 如果 date 沒帶，回傳錯誤
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期，例如 ?date=2025-02-07");
//			}

//			try
//			{
//				// 2. 解析前端傳來的 date，視為「本地時間」該日的 0:00 ~ 23:59:59.999
//				DateTime parsedDate = DateTime.Parse(date);

//				DateTime localDayStart = new DateTime(
//					parsedDate.Year,
//					parsedDate.Month,
//					parsedDate.Day,
//					0, 0, 0, DateTimeKind.Local);

//				DateTime localDayEnd = new DateTime(
//					parsedDate.Year,
//					parsedDate.Month,
//					parsedDate.Day,
//					23, 59, 59, 999, DateTimeKind.Local);

//				// 查詢時，轉成 UTC
//				DateTime startUtc = localDayStart.ToUniversalTime();
//				DateTime endUtc = localDayEnd.ToUniversalTime();

//				// 3. 查詢 MongoDB 內同一天(UTC 範圍)的紀錄
//				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyCleaned_20250211";
//				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(
//					collectionName, startUtc, endUtc);

//				// 若無資料 => 回傳空結構
//				if (docs == null || docs.Count == 0)
//				{
//					var emptyResult = new
//					{
//						labels = new List<string>(),
//						data = new Dictionary<string, List<double>>()
//					};
//					return Ok(emptyResult);
//				}

//				// 4. 定義我們關心的三個來源
//				var sources = new string[]
//				{
//					"CFX.A00.SO20050832.Trough",
//					"CFX.A00.SO20050832.Preheat",
//					"CFX.A00.SO20050832.Power"
//				};

//				// 5. 準備結構 grouped[source][hour] => List<BsonDocument> (該小時所有紀錄)
//				//    先建立好 (0..23) 每個小時都對應一個空的 list
//				var grouped = new Dictionary<string, Dictionary<int, List<BsonDocument>>>();
//				foreach (var s in sources)
//				{
//					grouped[s] = new Dictionary<int, List<BsonDocument>>();
//					for (int h = 0; h < 24; h++)
//					{
//						grouped[s][h] = new List<BsonDocument>();
//					}
//				}

//				// 6. 分配紀錄 => 依「本地時間」的 hour，丟到 grouped[source][hour]
//				foreach (var doc in docs)
//				{
//					// 取出 source
//					var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
//					if (!sources.Contains(source))
//						continue;  // 不在三個來源之中就跳過

//					// 取出 timeStamp (UTC)，再轉成本地時間
//					DateTime utcTime = doc["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
//					DateTime localTime = utcTime.ToLocalTime();

//					// 確認是不是當天 (避免碰到跨天的紀錄)
//					// localTime.Date 和 localDayStart.Date 都是該天日期
//					if (localTime.Date != localDayStart.Date)
//						continue;  // 不在同一天就跳過

//					// hour = 0..23
//					int hour = localTime.Hour;

//					// 放到 grouped 裡
//					grouped[source][hour].Add(doc);
//				}

//				// 7. 建立回傳結構: 24 小時
//				var result = new
//				{
//					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
//					data = new Dictionary<string, List<double>>()
//				};
//				// 先初始化 data[source] = 24 個 0
//				foreach (var s in sources)
//				{
//					result.data[s] = new List<double>(new double[24]);
//				}

//				// 8. 計算該小時的「第一筆」與「最後一筆」
//				//    => 只要把 grouped[source][hour] 按 TimeStamp 排序即可
//				for (int hour = 0; hour < 24; hour++)
//				{
//					foreach (var s in sources)
//					{
//						var docsInHour = grouped[s][hour];
//						if (docsInHour.Count < 2)
//						{
//							// 0 or 1 筆資料 => 無法計算差值 => 0
//							result.data[s][hour] = 0;
//							continue;
//						}

//						// 按照 TimeStamp (UTC) 升冪排序
//						docsInHour.Sort((a, b) =>
//						{
//							DateTime aUtc = a["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
//							DateTime bUtc = b["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
//							return aUtc.CompareTo(bUtc);
//						});

//						// 取出第一筆與最後一筆
//						var firstDoc = docsInHour.First();
//						var lastDoc = docsInHour.Last();

//						double firstEnergyUsed = firstDoc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
//						double lastEnergyUsed = lastDoc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

//						double consumption = lastEnergyUsed - firstEnergyUsed;
//						if (consumption < 0) consumption = 0;

//						result.data[s][hour] = consumption;
//					}
//				}

//				// 9. 回傳 JSON 結果
//				return Ok(result);

//			}


//			catch (Exception ex)
//			{
//				return StatusCode(500, $"發生錯誤: {ex.Message}");
//			}


//		}
//	}
//}




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
//			// 1. 檢查參數
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期，例如 ?date=2025-02-07");
//			}

//			try
//			{
//				// 2. 解析前端傳來的 date (yyyy-MM-dd)，視為「本地時間」的該日 0:00 ~ 23:59:59.999
//				DateTime parsedDate = DateTime.Parse(date);

//				// 本地日開始/結束
//				DateTime localDayStart = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Local);
//				DateTime localDayEnd = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Local);

//				// 查詢 MongoDB 時，轉成 UTC
//				DateTime startUtc = localDayStart.ToUniversalTime();
//				DateTime endUtc = localDayEnd.ToUniversalTime();

//				// 3. 從 MongoDB 查詢
//				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyCleaned_20250211";
//				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);

//				// 若無資料 => 回傳空
//				if (docs == null || docs.Count == 0)
//				{
//					var emptyResult = new
//					{
//						labels = new List<string>(),
//						data = new Dictionary<string, List<double>>()
//					};
//					return Ok(emptyResult);
//				}

//				// 4. 定義三個來源
//				var sources = new string[]
//				{
//					"CFX.A00.SO20050832.Trough",
//					"CFX.A00.SO20050832.Preheat",
//					"CFX.A00.SO20050832.Power"
//				};

//				// 5. 準備一個結構：grouped[source][hour] => List<BsonDocument>
//				//    先初始化 (0..23) 每個小時 => 空 list
//				var grouped = new Dictionary<string, Dictionary<int, List<BsonDocument>>>();
//				foreach (var s in sources)
//				{
//					grouped[s] = new Dictionary<int, List<BsonDocument>>();
//					for (int h = 0; h < 24; h++)
//					{
//						grouped[s][h] = new List<BsonDocument>();
//					}
//				}

//				// 6. 逐筆文件，按「本地小時 + source」歸類
//				foreach (var doc in docs)
//				{
//					// 取出 source
//					var source = doc["Data"]["Data"]["Meta"]["Source"].AsString;
//					if (!sources.Contains(source))
//						continue; // 不在三個來源裡就跳過

//					// 取出 timeStamp => 轉成 UTC => 再轉本地
//					var tsElement = doc["Data"]["Data"]["RawData"]["TimeStamp"];
//					DateTime utcTime = tsElement.IsBsonDateTime
//						? tsElement.ToUniversalTime()
//						: DateTime.Parse(tsElement.AsString).ToUniversalTime();

//					DateTime localTime = utcTime.ToLocalTime();
//					// 確認同一天
//					if (localTime.Date != localDayStart.Date)
//						continue;

//					int hour = localTime.Hour; // 0..23
//					grouped[source][hour].Add(doc);
//				}

//				// 7. 建立回傳結構 => 24 小時
//				var result = new
//				{
//					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
//					data = new Dictionary<string, List<double>>()
//				};

//				// 先初始化 data[source] => 24個 0
//				foreach (var s in sources)
//				{
//					result.data[s] = new List<double>(new double[24]);
//				}

//				// 8. 在每小時內，找「第一筆 & 最後一筆」EnergyUsed 差值
//				for (int h = 0; h < 24; h++)
//				{
//					foreach (var s in sources)
//					{
//						var docsInHour = grouped[s][h];
//						if (docsInHour.Count < 2)
//						{
//							// 0 或 1 筆 => 差值=0
//							result.data[s][h] = 0;
//							continue;
//						}

//						// 按 TimeStamp (UTC) 排序
//						docsInHour.Sort((a, b) =>
//						{
//							DateTime aUtc = a["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
//							DateTime bUtc = b["Data"]["Data"]["RawData"]["TimeStamp"].ToUniversalTime();
//							return aUtc.CompareTo(bUtc);
//						});

//						// 取第一筆、最後一筆
//						var firstDoc = docsInHour.First();
//						var lastDoc = docsInHour.Last();

//						double firstEnergyUsed = firstDoc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
//						double lastEnergyUsed = lastDoc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();

//						double consumption = lastEnergyUsed - firstEnergyUsed;
//						if (consumption < 0) consumption = 0;

//						// 寫回
//						result.data[s][h] = consumption;
//					}
//				}

//				// 9. 回傳
//				return Ok(result);
//			}
//			catch (Exception ex)
//			{
//				return StatusCode(500, $"發生錯誤: {ex.Message}");
//			}
//		}
//	}
//}

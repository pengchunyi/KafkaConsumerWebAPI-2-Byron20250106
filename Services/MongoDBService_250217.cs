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
//			// (1) 檢查參數
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期，例如 ?date=2025-02-07");
//			}

//			try
//			{
//				// (2) 解析前端傳來的 date (yyyy-MM-dd)，視為「本地時間」該日 0:00 ~ 23:59:59.999
//				DateTime parsedDate = DateTime.Parse(date);

//				// 本地日開始/結束
//				DateTime localDayStart = new DateTime(
//					parsedDate.Year, parsedDate.Month, parsedDate.Day,
//					0, 0, 0, DateTimeKind.Local);
//				DateTime localDayEnd = new DateTime(
//					parsedDate.Year, parsedDate.Month, parsedDate.Day,
//					23, 59, 59, 999, DateTimeKind.Local);

//				// 查詢 MongoDB 時，轉成 UTC
//				DateTime startUtc = localDayStart.ToUniversalTime();
//				DateTime endUtc = localDayEnd.ToUniversalTime();

//				// (3) 查詢 MongoDB => 指定你要的集合 "EnergyCleaned_20250211"
//				var collectionName = "EnergyCleaned_20250211";  // 這裡改為你想查的 collection
//				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(
//					collectionName, startUtc, endUtc);

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

//				// (4) 定義三個來源
//				var sources = new string[]
//				{
//					"CFX.A00.SO20050832.Trough",
//					"CFX.A00.SO20050832.Preheat",
//					"CFX.A00.SO20050832.Power"
//				};

//				// (5) 準備一個結構：grouped[source][hour] => List<BsonDocument>
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

//				// (6) 逐筆文件，按「本地小時 + source」歸類
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

//				// (7) 建立回傳結構 => 24 小時
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

//				// (8) 在每小時內，找「第一筆 & 最後一筆」EnergyUsed 差值
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

//				// (9) 回傳
//				return Ok(result);
//			}
//			catch (Exception ex)
//			{
//				return StatusCode(500, $"發生錯誤: {ex.Message}");
//			}
//		}
//	}
//}




////using MongoDB.Bson;
////using MongoDB.Driver;
////using Microsoft.Extensions.Configuration;
////using System;
////using System.Threading.Tasks;

////namespace KafkaConsumerWebAPI.Services
////{
////	public class MongoDBService
////	{
////		private readonly IMongoDatabase _database;

////		public MongoDBService(IConfiguration config)
////		{
////			// 初始化 MongoDB 連線
////			var client = new MongoClient(config["MongoDB:ConnectionString"]);
////			_database = client.GetDatabase(config["MongoDB:DatabaseName"]);

////			// 只需建立一次索引即可 (可放在系統啟動時)
////			// 以某個集合示範, e.g. EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed
////			var collection = _database.GetCollection<BsonDocument>("EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed");
////			var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("Data.Data.RawData.UniqueID");
////			var createIndexOptions = new CreateIndexOptions { Unique = true };
////			try
////			{
////				collection.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeys, createIndexOptions));
////			}
////			catch (Exception ex)
////			{
////				Console.WriteLine("建立唯一索引失敗: " + ex.Message);
////			}
////		}

////		public async Task InsertMessageToCollectionAsync(string topicName, string originalJsonString)
////		{
////			var collection = _database.GetCollection<BsonDocument>(topicName);
////			var doc = BsonDocument.Parse(originalJsonString);

////			try
////			{
////				await collection.InsertOneAsync(doc);
////			}
////			catch (MongoWriteException ex)
////			{
////				if (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
////				{
////					Console.WriteLine("偵測到重複的 UniqueID，跳過插入。");
////				}
////				else
////				{
////					throw;
////				}
////			}
////		}


////		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================
////		public async Task<BsonDocument> GetLatestMessageAsync(string collectionName)
////		{
////			var collection = _database.GetCollection<BsonDocument>(collectionName);
////			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");
////			return await collection.Find(FilterDefinition<BsonDocument>.Empty)
////									.Sort(sort)
////									.Limit(3)
////									.FirstOrDefaultAsync();
////		}
////		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================



////		/// <summary>
////		/// 從 MongoDB 查詢指定時間範圍 (UTC時間) 的 EnergyConsumed 消息，
////		/// 並依照 Source & 小時取出 EnergyUsed。
////		/// </summary>
////		public async Task<List<BsonDocument>> QueryEnergyConsumedByTimeRangeAsync(string collectionName, DateTime startUtc, DateTime endUtc)
////		{
////			var collection = _database.GetCollection<BsonDocument>(collectionName);

////			// 過濾：RawData.TimeStamp 在指定區間內
////			var filterBuilder = Builders<BsonDocument>.Filter;
////			var filter = filterBuilder.Gte("Data.Data.RawData.TimeStamp", startUtc) &
////						 filterBuilder.Lte("Data.Data.RawData.TimeStamp", endUtc);

////			// 查詢
////			var results = await collection.Find(filter)
////										  .Sort(Builders<BsonDocument>.Sort.Ascending("Data.Data.RawData.TimeStamp"))
////										  .ToListAsync();
////			return results;
////		}
////	}
////}

//using MongoDB.Bson;
//using MongoDB.Driver;
//using Microsoft.Extensions.Configuration;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace KafkaConsumerWebAPI.Services
//{
//	public class MongoDBService
//	{
//		private readonly IMongoDatabase _database;

//		public MongoDBService(IConfiguration config)
//		{
//			// 初始化 MongoDB 連線
//			var client = new MongoClient(config["MongoDB:ConnectionString"]);
//			_database = client.GetDatabase(config["MongoDB:DatabaseName"]);

//			// 只需建立一次索引即可 (可放在系統啟動時)
//			var collection = _database.GetCollection<BsonDocument>("EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed");
//			var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("Data.Data.RawData.UniqueID");
//			var createIndexOptions = new CreateIndexOptions { Unique = true };
//			try
//			{
//				collection.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeys, createIndexOptions));
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine("建立唯一索引失敗: " + ex.Message);
//			}
//		}

//		// [原有]：插入資料
//		public async Task InsertMessageToCollectionAsync(string topicName, string originalJsonString)
//		{
//			var collection = _database.GetCollection<BsonDocument>(topicName);
//			var doc = BsonDocument.Parse(originalJsonString);

//			try
//			{
//				// 取出 TimeStamp 字串
//				var timeStampStr = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
//				// 解析成 C# DateTime
//				var dt = DateTime.Parse(timeStampStr);
//				// 如果希望存成 UTC，可加上 dt = dt.ToUniversalTime();
//				dt = dt.ToUniversalTime();
//				// 如果希望存成 UTC，可加上 dt = dt.ToUniversalTime();
//				// 將 TimeStamp 欄位換成 BSON DateTime 型別
//				doc["Data"]["Data"]["RawData"]["TimeStamp"] = new BsonDateTime(dt);
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine("轉換 TimeStamp 失敗: " + ex.Message);
//			}

//			try
//			{
//				await collection.InsertOneAsync(doc);
//			}
//			catch (MongoWriteException ex)
//			{
//				if (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
//				{
//					Console.WriteLine("偵測到重複的 UniqueID，跳過插入。");
//				}
//				else
//				{
//					throw;
//				}
//			}
//		}

//		// [原有]：取最新一筆 (不分 Source)
//		public async Task<BsonDocument> GetLatestMessageAsync(string collectionName)
//		{
//			var collection = _database.GetCollection<BsonDocument>(collectionName);
//			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");



//			return await collection.Find(FilterDefinition<BsonDocument>.Empty)
//								   .Sort(sort)
//								   .Limit(1)
//								   .FirstOrDefaultAsync();
//		}

//		// [新增1]：依 Source 取最新一筆
//		public async Task<BsonDocument> GetLatestEnergyConsumedBySourceAsync(string collectionName, string source)
//		{
//			var collection = _database.GetCollection<BsonDocument>(collectionName);

//			// 過濾: Data.Data.Meta.Source == source
//			var filter = Builders<BsonDocument>.Filter.Eq("Data.Data.Meta.Source", source);

//			// 按照 TimeStamp DESC，取最新一筆
//			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");




//			return await collection.Find(filter)
//								   .Sort(sort)
//								   .Limit(1)
//								   .FirstOrDefaultAsync();
//		}



//		// [新增2]：一次取 Power/Preheat/Trough 三筆最新
//		public async Task<List<BsonDocument>> GetLatestEnergyConsumedAllSourcesAsync()
//		{
//			var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";

//			var sources = new string[]
//			{
//				"CFX.A00.SO20050832.Power",
//				"CFX.A00.SO20050832.Preheat",
//				"CFX.A00.SO20050832.Trough"
//			};

//			var results = new List<BsonDocument>();
//			foreach (var src in sources)
//			{
//				var doc = await GetLatestEnergyConsumedBySourceAsync(collectionName, src);
//				if (doc != null)
//				{
//					results.Add(doc);
//				}
//			}


//			return results;
//		}

//		// [原有]：時間範圍查詢
//		public async Task<List<BsonDocument>> QueryEnergyConsumedByTimeRangeAsync(string collectionName, DateTime startUtc, DateTime endUtc)
//		{
//			var collection = _database.GetCollection<BsonDocument>(collectionName);

//			var filterBuilder = Builders<BsonDocument>.Filter;
//			var filter = filterBuilder.Gte("Data.Data.RawData.TimeStamp", startUtc) &
//						 filterBuilder.Lte("Data.Data.RawData.TimeStamp", endUtc);

//			var results = await collection.Find(filter)
//										  .Sort(Builders<BsonDocument>.Sort.Ascending("Data.Data.RawData.TimeStamp"))
//										  .ToListAsync();


//			return results;
//		}
//	}
//}




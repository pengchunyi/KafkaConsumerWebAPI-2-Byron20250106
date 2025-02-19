//using MongoDB.Bson;
//using MongoDB.Driver;
//using Microsoft.Extensions.Configuration;
//using System;
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
//			// 以某個集合示範, e.g. EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed
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

//		public async Task InsertMessageToCollectionAsync(string topicName, string originalJsonString)
//		{
//			var collection = _database.GetCollection<BsonDocument>(topicName);
//			var doc = BsonDocument.Parse(originalJsonString);

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


//		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================
//		public async Task<BsonDocument> GetLatestMessageAsync(string collectionName)
//		{
//			var collection = _database.GetCollection<BsonDocument>(collectionName);
//			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");
//			return await collection.Find(FilterDefinition<BsonDocument>.Empty)
//									.Sort(sort)
//									.Limit(3)
//									.FirstOrDefaultAsync();
//		}
//		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================



//		/// <summary>
//		/// 從 MongoDB 查詢指定時間範圍 (UTC時間) 的 EnergyConsumed 消息，
//		/// 並依照 Source & 小時取出 EnergyUsed。
//		/// </summary>
//		public async Task<List<BsonDocument>> QueryEnergyConsumedByTimeRangeAsync(string collectionName, DateTime startUtc, DateTime endUtc)
//		{
//			var collection = _database.GetCollection<BsonDocument>(collectionName);

//			// 過濾：RawData.TimeStamp 在指定區間內
//			var filterBuilder = Builders<BsonDocument>.Filter;
//			var filter = filterBuilder.Gte("Data.Data.RawData.TimeStamp", startUtc) &
//						 filterBuilder.Lte("Data.Data.RawData.TimeStamp", endUtc);

//			// 查詢
//			var results = await collection.Find(filter)
//										  .Sort(Builders<BsonDocument>.Sort.Ascending("Data.Data.RawData.TimeStamp"))
//										  .ToListAsync();
//			return results;
//		}
//	}
//}

using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KafkaConsumerWebAPI.Services
{
	public class MongoDBService
	{
		private readonly IMongoDatabase _database;

		public MongoDBService(IConfiguration config)
		{
			// 初始化 MongoDB 連線
			var client = new MongoClient(config["MongoDB:ConnectionString"]);
			_database = client.GetDatabase(config["MongoDB:DatabaseName"]);

			// 只需建立一次索引即可 (可放在系統啟動時)
			var collection = _database.GetCollection<BsonDocument>("EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed");
			var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("Data.Data.RawData.UniqueID");
			var createIndexOptions = new CreateIndexOptions { Unique = true };
			try
			{
				collection.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeys, createIndexOptions));
			}
			catch (Exception ex)
			{
				Console.WriteLine("建立唯一索引失敗: " + ex.Message);
			}
		}

		// [原有]：插入資料
		public async Task InsertMessageToCollectionAsync(string topicName, string originalJsonString)
		{
			var collection = _database.GetCollection<BsonDocument>(topicName);
			var doc = BsonDocument.Parse(originalJsonString);

			try
			{
				// 取出 TimeStamp 字串
				var timeStampStr = doc["Data"]["Data"]["RawData"]["TimeStamp"].AsString;
				// 解析成 C# DateTime
				var dt = DateTime.Parse(timeStampStr);
				// 如果希望存成 UTC，可加上 dt = dt.ToUniversalTime();
				// 將 TimeStamp 欄位換成 BSON DateTime 型別
				doc["Data"]["Data"]["RawData"]["TimeStamp"] = new BsonDateTime(dt);
			}
			catch (Exception ex)
			{
				Console.WriteLine("轉換 TimeStamp 失敗: " + ex.Message);
			}

			try
			{
				await collection.InsertOneAsync(doc);
			}
			catch (MongoWriteException ex)
			{
				if (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
				{
					Console.WriteLine("偵測到重複的 UniqueID，跳過插入。");
				}
				else
				{
					throw;
				}
			}
		}

		// [原有]：取最新一筆 (不分 Source)
		public async Task<BsonDocument> GetLatestMessageAsync(string collectionName)
		{
			var collection = _database.GetCollection<BsonDocument>(collectionName);
			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");
			return await collection.Find(FilterDefinition<BsonDocument>.Empty)
								   .Sort(sort)
								   .Limit(1)
								   .FirstOrDefaultAsync();
		}

		// [新增1]：依 Source 取最新一筆
		public async Task<BsonDocument> GetLatestEnergyConsumedBySourceAsync(string collectionName, string source)
		{
			var collection = _database.GetCollection<BsonDocument>(collectionName);

			// 過濾: Data.Data.Meta.Source == source
			var filter = Builders<BsonDocument>.Filter.Eq("Data.Data.Meta.Source", source);

			// 按照 TimeStamp DESC，取最新一筆
			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");
			return await collection.Find(filter)
								   .Sort(sort)
								   .Limit(1)
								   .FirstOrDefaultAsync();
		}

		// [新增2]：一次取 Power/Preheat/Trough 三筆最新
		public async Task<List<BsonDocument>> GetLatestEnergyConsumedAllSourcesAsync()
		{
			var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";

			var sources = new string[]
			{
				"CFX.A00.SO20050832.Power",
				"CFX.A00.SO20050832.Preheat",
				"CFX.A00.SO20050832.Trough"
			};

			var results = new List<BsonDocument>();
			foreach (var src in sources)
			{
				var doc = await GetLatestEnergyConsumedBySourceAsync(collectionName, src);
				if (doc != null)
				{
					results.Add(doc);
				}
			}
			return results;
		}

		// [原有]：時間範圍查詢
		public async Task<List<BsonDocument>> QueryEnergyConsumedByTimeRangeAsync(string collectionName, DateTime startUtc, DateTime endUtc)
		{
			var collection = _database.GetCollection<BsonDocument>(collectionName);

			var filterBuilder = Builders<BsonDocument>.Filter;
			var filter = filterBuilder.Gte("Data.Data.RawData.TimeStamp", startUtc) &
						 filterBuilder.Lte("Data.Data.RawData.TimeStamp", endUtc);

			var results = await collection.Find(filter)
										  .Sort(Builders<BsonDocument>.Sort.Ascending("Data.Data.RawData.TimeStamp"))
										  .ToListAsync();
			return results;
		}
	}
}




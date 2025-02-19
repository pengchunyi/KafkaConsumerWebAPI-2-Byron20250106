//using MongoDB.Driver;
//using Microsoft.Extensions.Configuration;

//namespace KafkaConsumerWebAPI.Services
//{
//	public class MongoDBService
//	{
//		private readonly IMongoClient _client;
//		private readonly IMongoDatabase _database;

//		public MongoDBService(IConfiguration configuration)
//		{
//			var mongoConfig = configuration.GetSection("MongoDB");
//			_client = new MongoClient(mongoConfig["ConnectionString"]);
//			_database = _client.GetDatabase(mongoConfig["DatabaseName"]);
//		}

//		// 插入數據到指定的集合
//		public async Task InsertMessageToCollectionAsync(string collectionName, string message)
//		{
//			var collection = _database.GetCollection<dynamic>(collectionName);
//			var document = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<dynamic>(message);
//			await collection.InsertOneAsync(document);
//		}
//	}
//}


using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using System;
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
			// 以某個集合示範, e.g. EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed
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

		public async Task InsertMessageToCollectionAsync(string topicName, string originalJsonString)
		{
			var collection = _database.GetCollection<BsonDocument>(topicName);
			var doc = BsonDocument.Parse(originalJsonString);

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


		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================
		public async Task<BsonDocument> GetLatestMessageAsync(string collectionName)
		{
			var collection = _database.GetCollection<BsonDocument>(collectionName);
			var sort = Builders<BsonDocument>.Sort.Descending("Data.Data.RawData.TimeStamp");
			return await collection.Find(FilterDefinition<BsonDocument>.Empty)
									.Sort(sort)
									.Limit(1)
									.FirstOrDefaultAsync();
		}
		//250217 新增：讀取指定集合中最新的消息（依 TimeStamp 排序取第一筆）==========================



		/// <summary>
		/// 從 MongoDB 查詢指定時間範圍 (UTC時間) 的 EnergyConsumed 消息，
		/// 並依照 Source & 小時取出 EnergyUsed。
		/// </summary>
		public async Task<List<BsonDocument>> QueryEnergyConsumedByTimeRangeAsync(string collectionName, DateTime startUtc, DateTime endUtc)
		{
			var collection = _database.GetCollection<BsonDocument>(collectionName);

			// 過濾：RawData.TimeStamp 在指定區間內
			var filterBuilder = Builders<BsonDocument>.Filter;
			var filter = filterBuilder.Gte("Data.Data.RawData.TimeStamp", startUtc) &
						 filterBuilder.Lte("Data.Data.RawData.TimeStamp", endUtc);

			// 查詢
			var results = await collection.Find(filter)
										  .Sort(Builders<BsonDocument>.Sort.Ascending("Data.Data.RawData.TimeStamp"))
										  .ToListAsync();
			return results;
		}
	}
}






//using MongoDB.Driver; // 引入 MongoDB 驅動程式
//using Microsoft.Extensions.Configuration; // 引入配置管理功能
//using Newtonsoft.Json; // 引入 Newtonsoft.Json 庫，用於 JSON 序列化和反序列化
//using System.Threading.Tasks; // 引入非同步任務功能

//namespace KafkaConsumerWebAPI.Services
//{
//	public class MongoDBService
//	{
//		// MongoDB 客戶端，用於連接 MongoDB 伺服器
//		private readonly IMongoClient _client;

//		// MongoDB 資料庫實例，用於操作指定的資料庫
//		private readonly IMongoDatabase _database;

//		// 構造函數，初始化 MongoDB 連接
//		public MongoDBService(IConfiguration configuration)
//		{
//			// 從配置文件中讀取 MongoDB 相關設定
//			var mongoConfig = configuration.GetSection("MongoDB");

//			// 使用連接字符串創建 MongoDB 客戶端
//			_client = new MongoClient(mongoConfig["ConnectionString"]);

//			// 獲取指定的資料庫
//			_database = _client.GetDatabase(mongoConfig["DatabaseName"]);
//		}

//		// 插入數據到指定的集合（JSON 格式化）
//		public async Task InsertMessageToCollectionAsync(string collectionName, string message)
//		{
//			// 獲取指定集合的引用
//			var collection = _database.GetCollection<dynamic>(collectionName);

//			// 將傳入的 JSON 字符串反序列化為動態對象
//			var document = JsonConvert.DeserializeObject<dynamic>(message);

//			// 將反序列化後的對象插入到 MongoDB 集合中
//			await collection.InsertOneAsync(document);
//		}

//		// 從指定的集合中讀取數據並格式化為 JSON
//		public async Task<string> GetMessagesAsJsonAsync(string collectionName, int limit = 10)
//		{
//			// 獲取指定集合的引用
//			var collection = _database.GetCollection<dynamic>(collectionName);

//			// 查詢數據，並按 _id 降序排序（獲取最新數據）
//			var documents = await collection.Find(_ => true) // 查詢所有文檔
//										   .SortByDescending(d => d["_id"]) // 按 _id 降序排序
//										   .Limit(limit) // 限制返回的文檔數量
//										   .ToListAsync(); // 將結果轉換為列表

//			// 將查詢結果序列化為格式化的 JSON 字符串
//			var json = JsonConvert.SerializeObject(documents, Formatting.Indented);

//			// 返回格式化後的 JSON 字符串
//			return json;
//		}
//	}
//}





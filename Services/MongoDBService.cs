//using MongoDB.Driver;
//using Microsoft.Extensions.Configuration;

//namespace KafkaConsumerWebAPI.Services
//{
//	public class MongoDBService
//	{
//		private readonly IMongoCollection<dynamic> _collection;

//		public MongoDBService(IConfiguration configuration)
//		{
//			var mongoConfig = configuration.GetSection("MongoDB");
//			var client = new MongoClient(mongoConfig["ConnectionString"]);
//			var database = client.GetDatabase(mongoConfig["DatabaseName"]);
//			_collection = database.GetCollection<dynamic>(mongoConfig["CollectionName"]);
//		}

//		public async Task InsertMessageAsync(dynamic message)
//		{
//			await _collection.InsertOneAsync(message);
//		}
//	}
//}



using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace KafkaConsumerWebAPI.Services
{
	public class MongoDBService
	{
		private readonly IMongoClient _client;
		private readonly IMongoDatabase _database;

		public MongoDBService(IConfiguration configuration)
		{
			var mongoConfig = configuration.GetSection("MongoDB");
			_client = new MongoClient(mongoConfig["ConnectionString"]);
			_database = _client.GetDatabase(mongoConfig["DatabaseName"]);
		}

		// 插入數據到指定的集合
		public async Task InsertMessageToCollectionAsync(string collectionName, string message)
		{
			var collection = _database.GetCollection<dynamic>(collectionName);
			var document = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<dynamic>(message);
			await collection.InsertOneAsync(document);
		}
	}
}

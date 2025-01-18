using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace KafkaConsumerWebAPI.Services
{
	public class MongoDBService
	{
		private readonly IMongoCollection<dynamic> _collection;

		public MongoDBService(IConfiguration configuration)
		{
			var mongoConfig = configuration.GetSection("MongoDB");
			var client = new MongoClient(mongoConfig["ConnectionString"]);
			var database = client.GetDatabase(mongoConfig["DatabaseName"]);
			_collection = database.GetCollection<dynamic>(mongoConfig["CollectionName"]);
		}

		public async Task InsertMessageAsync(dynamic message)
		{
			await _collection.InsertOneAsync(message);
		}
	}
}

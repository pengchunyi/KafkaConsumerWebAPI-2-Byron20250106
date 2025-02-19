//using Microsoft.AspNetCore.Mvc;
//using MongoDB.Bson;
//using KafkaConsumerWebAPI.Services;
//using System.Threading.Tasks;
//using MongoDB.Bson.IO;

//namespace KafkaConsumerWebAPI.Controllers
//{
//	[ApiController]
//	[Route("api/[controller]")]
//	public class LatestMessageController : ControllerBase
//	{
//		private readonly MongoDBService _mongoDBService;

//		public LatestMessageController(MongoDBService mongoDBService)
//		{
//			_mongoDBService = mongoDBService;
//		}

//		// GET api/LatestMessage/{topic}
//		//[HttpGet("{topic}")]
//		//public async Task<IActionResult> GetLatestMessage(string topic)
//		//{
//		//	var doc = await _mongoDBService.GetLatestMessageAsync(topic);
//		//	if (doc == null)
//		//		return NotFound("No message found.");
//		//	// 回傳 JSON 格式的字串
//		//	return Ok(doc.ToJson());
//		//}






//		//250217_update=============================================================
//		[HttpGet("{topic}")]
//		public async Task<IActionResult> GetLatestMessage(string topic)
//		{
//			var doc = await _mongoDBService.GetLatestMessageAsync(topic);
//			if (doc == null)
//				return NotFound("No message found.");

//			//// 直接回傳 BsonDocument，ASP.NET Core 會自動序列化成 JSON
//			//return Ok(doc);

//			// 1) 用 MongoDB Driver 提供的 ToJson()，自行轉成 JSON 字串
//			//    可以指定 JsonWriterSettings.OutputMode = Strict 或 Shell
//			//    預設 Strict 通常就能把 BsonObjectId 轉成類似 { "$oid": "xxxxx" }
//			//var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.Shell };
//			//var json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.Strict });
//			//var json = doc.ToJson(settings);
//			var json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson });


//			// 2) 以字串形式回傳，並指定 Content-Type = application/json
//			return Content(json, "application/json");


//		}
//		//250217_update=============================================================





//	}
//}



using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using KafkaConsumerWebAPI.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KafkaConsumerWebAPI.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class LatestMessageController : ControllerBase
	{
		private readonly MongoDBService _mongoDBService;

		public LatestMessageController(MongoDBService mongoDBService)
		{
			_mongoDBService = mongoDBService;
		}

		// [原有] 單一筆
		[HttpGet("{topic}")]
		public async Task<IActionResult> GetLatestMessage(string topic)
		{
			var doc = await _mongoDBService.GetLatestMessageAsync(topic);
			if (doc == null) 
				return NotFound("No message found.");

			// 把 BsonDocument 轉成 JSON 字串
			var json = doc.ToJson();
			// 或如果你想要完整的 "$oid" 格式，可用 JsonWriterSettings
			// var json = doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });

			// 以字串形式回傳
			return Content(json, "application/json");
		}

		// [新增] 一次取三筆
		//[HttpGet("latest3")]
		//public async Task<IActionResult> GetLatest3()
		//{
		//	// 拿到 Power, Preheat, Trough 各自最新一筆
		//	List<BsonDocument> docs = await _mongoDBService.GetLatestEnergyConsumedAllSourcesAsync();
		//	// 直接回傳 List<BsonDocument>，ASP.NET Core 會自動轉成 JSON
		//	return Ok(docs);
		//}

		[HttpGet("latest3")]
		public async Task<IActionResult> GetLatest3()
		{
			var docs = await _mongoDBService.GetLatestEnergyConsumedAllSourcesAsync();
			if (docs == null || docs.Count == 0)
				return NotFound("No message found.");

			// 將每筆 BsonDocument -> JSON 字串，再組合成 JSON Array
			var jsonArray = "[" + string.Join(",", docs.Select(d => d.ToJson())) + "]";
			return Content(jsonArray, "application/json");
		}




	}
}

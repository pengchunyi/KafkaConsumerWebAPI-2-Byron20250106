﻿using Microsoft.AspNetCore.Mvc;
using KafkaConsumerWebAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
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

		/// <summary>
		/// 計算每日每個來源的用電量（以當天累計值之間的差值來計算）。
		/// 假設資料中每個來源每天每小時只有一筆紀錄，
		/// 則每筆紀錄代表該時刻的累計值，用當前紀錄與前一筆紀錄的差值當作該筆的耗電量，
		/// 並將該耗電量歸屬到當前紀錄的本地小時中。
		/// 若該來源某小時只有一筆（當天第一筆或缺少前一筆），則當小時耗能為 0。
		/// </summary>
		[HttpGet("daily-consumption")]
		public async Task<IActionResult> GetDailyConsumption([FromQuery] string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return BadRequest("請提供日期，例如 ?date=2025-02-11");
			}

			try
			{
				// 1. 解析前端日期，視為本地時間的該日 0:00 ~ 23:59:59.999
				DateTime parsedDate = DateTime.Parse(date);
				DateTime localDayStart = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0, DateTimeKind.Local);
				DateTime localDayEnd = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 23, 59, 59, 999, DateTimeKind.Local);
				DateTime startUtc = localDayStart.ToUniversalTime();
				DateTime endUtc = localDayEnd.ToUniversalTime();

				// 2. 請根據你的需求查詢正確的集合（例如：EnergyCleaned_20250211）
				var collectionName = "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed";
				// 若你改為 EnergyCleaned_20250211，請將 collectionName 改為：
				//var collectionName = "EnergyLastRecord_20250211";

				var docs = await _mongoDBService.QueryEnergyConsumedByTimeRangeAsync(collectionName, startUtc, endUtc);
				if (docs == null || docs.Count == 0)
				{
					var emptyResult = new
					{
						labels = new List<string>(),
						data = new Dictionary<string, List<double>>()
					};
					return Ok(emptyResult);
				}

				// 3. 定義我們關心的三個來源
				var sources = new string[]
				{
					"CFX.A00.SO20050832.Trough",
					"CFX.A00.SO20050832.Preheat",
					"CFX.A00.SO20050832.Power"
				};

				// 4. 為每個來源建立一個列表，用來存放該來源當天所有紀錄（依時間排序）
				var sourceDocs = new Dictionary<string, List<BsonDocument>>();
				foreach (var src in sources)
				{
					sourceDocs[src] = docs
						.Where(doc => doc["Data"]["Data"]["Meta"]["Source"].AsString == src)
						.OrderBy(doc =>
						{
							var ts = doc["Data"]["Data"]["RawData"]["TimeStamp"];
							return ts.IsBsonDateTime ? ts.ToUniversalTime() : DateTime.Parse(ts.AsString).ToUniversalTime();
						})
						.ToList();
				}

				// 5. 建立 24 小時的結果結構：對每個來源建立一個長度為 24 的陣列（預設值 0）
				var resultData = new Dictionary<string, List<double>>();
				foreach (var src in sources)
				{
					resultData[src] = Enumerable.Repeat(0.0, 24).ToList();
				}

				// 6. 針對每個來源，依照排序後的紀錄計算每筆與前筆的差值
				//    並把該差值歸到當前紀錄所屬的本地小時中
				foreach (var src in sources)
				{
					var list = sourceDocs[src];
					// 如果沒有至少兩筆資料，無法計算差值，則該來源全為 0
					if (list.Count < 2)
						continue;

					// 遍歷從第二筆開始
					double prevReading = list.First()["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
					// 你也可以記錄該來源的第一筆所在的小時，但通常第一筆不計算耗能（因為沒有前一筆）
					for (int i = 1; i < list.Count; i++)
					{
						var currentDoc = list[i];
						double currentReading = currentDoc["Data"]["Data"]["RawData"]["MessageBody"]["EnergyUsed"].ToDouble();
						double consumption = currentReading - prevReading;
						if (consumption < 0)
							consumption = 0;
						// 取得該文件的本地時間小時
						var tsElement = currentDoc["Data"]["Data"]["RawData"]["TimeStamp"];
						DateTime utcTime = tsElement.IsBsonDateTime
							? tsElement.ToUniversalTime()
							: DateTime.Parse(tsElement.AsString).ToUniversalTime();
						int hour = utcTime.ToLocalTime().Hour;
						// 若同一小時已有多筆（理論上不會），則累加
						resultData[src][hour] += consumption;
						prevReading = currentReading;
					}
				}

				// 7. 準備回傳結果：labels 固定 0:00～23:00
				var result = new
				{
					labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToList(),
					data = resultData
				};

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"發生錯誤: {ex.Message}");
			}
		}
	}
}

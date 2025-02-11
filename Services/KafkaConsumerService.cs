//using System.Collections.Concurrent;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Confluent.Kafka;
//using KafkaConsumerWebAPI.Hubs;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Hosting;
//using System.Text.Json;
//using Newtonsoft.Json;
//using System;
//using System.Linq;
//using System.Collections.Generic;

//namespace KafkaConsumerWebAPI.Services
//{
//	// Kafka 消費者服務，繼承自 BackgroundService 以在後台運行
//	public class KafkaConsumerService : BackgroundService
//	{
//		private readonly ConcurrentQueue<string> _messageQueue;
//		private readonly IConfiguration _configuration;
//		private readonly IHubContext<MessageHub> _hubContext;
//		private readonly MongoDBService _mongoDBService;

//		private IConsumer<Ignore, byte[]>? _consumer;

//		public KafkaConsumerService(
//			ConcurrentQueue<string> messageQueue,
//			IConfiguration configuration,
//			IHubContext<MessageHub> hubContext,
//			MongoDBService mongoDBService)
//		{
//			_messageQueue = messageQueue;
//			_configuration = configuration;
//			_hubContext = hubContext;
//			_mongoDBService = mongoDBService;
//		}

//		/// <summary>
//		/// 範例：如要回溯 2025-02-07 一整天，可使用 OffsetsForTimes。
//		/// 這裡示範 Earliest + SeekToTargetDateOffsets => 回溯指定日期
//		/// </summary>
//		private void SeekToTargetDateOffsets(IConsumer<Ignore, byte[]> consumer, DateTime targetStartUtc)
//		{
//			var partitions = consumer.Assignment;
//			// 為每個分區建立一個查詢時間點
//			var timestampsToSearch = partitions
//				.Select(tp => new TopicPartitionTimestamp(tp, new Timestamp(targetStartUtc, TimestampType.CreateTime)))
//				.ToList();
//			// 根據查詢時間取得各分區的 offset
//			var offsetsForTimes = consumer.OffsetsForTimes(timestampsToSearch, TimeSpan.FromSeconds(10));
//			foreach (var tpOffset in offsetsForTimes)
//			{
//				if (tpOffset.Offset != Offset.Unset)
//				{
//					consumer.Seek(tpOffset);
//				}
//			}
//		}

//		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//		{
//			var kafkaConfig = _configuration.GetSection("Kafka");

//			var consumerConfig = new ConsumerConfig
//			{
//				GroupId = kafkaConfig["GroupId"],
//				BootstrapServers = kafkaConfig["BootstrapServers"],
//				SecurityProtocol = SecurityProtocol.SaslPlaintext,
//				SaslMechanism = SaslMechanism.Plain,
//				SaslUsername = kafkaConfig["SaslUsername"],
//				SaslPassword = kafkaConfig["SaslPassword"],
//				// 若要從最早消息開始 => Earliest
//				// 若只要接收未來最新 => Latest
//				// 若要精準回溯 -> Earliest + SeekToTargetOffsets
//				AutoOffsetReset = AutoOffsetReset.Earliest,
//				EnableAutoCommit = false
//			};

//			var topics = new List<string>
//			{
//				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
//				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
//			};

//			_consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
//				.SetValueDeserializer(Deserializers.ByteArray)
//				.Build();

//			_consumer.Subscribe(topics);
//			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

//			// 【若要回溯 2025-02-07 (UTC+8) 的整天 => UTC+0: 2025-02-06 16:00:00
//			var targetStartUtc = new DateTime(2025, 2, 7).Date.AddHours(-8); // 2025-02-06 16:00:00
//																			 // 調用方法 => 調整 offset 到指定日期
//																			 // （只示範，可自行決定要不要呼叫）
//			await Task.Delay(1000); // 等待 consumer Assign 分區
//			SeekToTargetDateOffsets(_consumer, targetStartUtc);

//			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
//		}

//		private async void ConsumeMessages(CancellationToken stoppingToken)
//		{
//			// 設定目標時間範圍 => (UTC+0) 2025-02-06 16:00:00 ~ 2025-02-07 15:59:59
//			DateTime targetStartUtc = new DateTime(2025, 2, 7).Date.AddHours(-8);
//			DateTime targetEndUtc = new DateTime(2025, 2, 7).Date.AddDays(1).AddHours(-8).AddTicks(-1);

//			try
//			{
//				while (!stoppingToken.IsCancellationRequested)
//				{
//					try
//					{
//						var result = _consumer!.Consume(stoppingToken);
//						if (result.Message?.Value != null)
//						{
//							string topicName = result.Topic;
//							string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

//							// 從 JSON 提取 TimeStamp (UTC+0)
//							DateTime messageTimestamp = ExtractTimestamp(originalJsonString);
//							if (messageTimestamp == DateTime.MinValue)
//							{
//								// 代表提取失敗
//								Console.WriteLine("未能提取 TimeStamp，略過。");
//								continue;
//							}

//							// 檢查是否在目標日期範圍內
//							if (messageTimestamp >= targetStartUtc && messageTimestamp <= targetEndUtc)
//							{
//								// 解析成三種來源
//								string relevantData = ExtractRelevantData(topicName, originalJsonString);
//								if (!string.IsNullOrEmpty(relevantData) && relevantData != "數據提取失敗")
//								{
//									// 儲存消息到 MongoDB (有去重機制)
//									await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

//									// (選擇性) 用 SignalR 推送
//									await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);

//									Console.WriteLine($"成功消費消息，Topic: {topicName}");
//									Console.WriteLine("Original JSON: ");
//									Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(originalJsonString), Formatting.Indented));
//								}
//							}
//							else
//							{
//								Console.WriteLine($"消息時間 {messageTimestamp} 不在目標範圍 {targetStartUtc:yyyy-MM-dd HH:mm:ss} ~ {targetEndUtc:yyyy-MM-dd HH:mm:ss} 內。");
//							}
//						}
//					}
//					catch (ConsumeException e)
//					{
//						Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
//					}
//					catch (Exception ex)
//					{
//						Console.WriteLine($"處理消息時發生錯誤: {ex.Message}");
//					}
//				}
//			}
//			catch (OperationCanceledException)
//			{
//				Console.WriteLine("消費服務已被取消");
//			}
//			finally
//			{
//				_consumer?.Close();
//			}
//		}

//		/// <summary>
//		/// 從 JSON 中提取 TimeStamp (Data -> Data -> RawData -> TimeStamp)
//		/// </summary>
//		private DateTime ExtractTimestamp(string json)
//		{
//			try
//			{
//				using var doc = JsonDocument.Parse(json);
//				var root = doc.RootElement;

//				if (root.TryGetProperty("Data", out var dataEl) &&
//					dataEl.TryGetProperty("Data", out var innerDataEl) &&
//					innerDataEl.TryGetProperty("RawData", out var rawDataEl) &&
//					rawDataEl.TryGetProperty("TimeStamp", out var tsEl))
//				{
//					string tsString = tsEl.GetString();
//					if (!string.IsNullOrEmpty(tsString))
//					{
//						return DateTime.Parse(tsString); // 假設為 UTC+0
//					}
//				}
//			}
//			catch { }
//			return DateTime.MinValue;
//		}

//		/// <summary>
//		/// 根據 Topic 解析不同資料 => EnergyConsumed or StationParametersModified
//		/// </summary>
//		private string ExtractRelevantData(string topic, string jsonString)
//		{
//			try
//			{
//				using var doc = JsonDocument.Parse(jsonString);
//				var root = doc.RootElement;

//				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
//				{
//					return ParseEnergyConsumed(root);
//				}
//				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
//				{
//					return ParseStationParams(root);
//				}
//				else
//				{
//					// 不在三種來源
//					return null;
//				}
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
//				return "數據提取失敗";
//			}
//		}

//		/// <summary>
//		/// 解析 EnergyConsumed 主題: 只取 [Preheat, Trough, Power] 三種 Source
//		/// </summary>
//		private string ParseEnergyConsumed(JsonElement root)
//		{
//			var dataEl = root.GetProperty("Data").GetProperty("Data");
//			var rawDataEl = dataEl.GetProperty("RawData");
//			var messageBody = rawDataEl.GetProperty("MessageBody");

//			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
//												.EnumerateArray()
//												.Select(x => x.GetDouble())
//												.ToArray();
//			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
//											  .EnumerateArray()
//											  .Select(x => x.GetDouble())
//											  .ToArray();
//			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

//			var metaEl = dataEl.GetProperty("Meta");
//			string source = metaEl.GetProperty("Source").GetString() ?? "";

//			// 若不屬於三種，就直接 return 空
//			if (source != "CFX.A00.SO20050832.Trough" &&
//				source != "CFX.A00.SO20050832.Preheat" &&
//				source != "CFX.A00.SO20050832.Power")
//			{
//				return null; // 不處理
//			}

//			// 簡單組裝一下
//			string component = source switch
//			{
//				"CFX.A00.SO20050832.Trough" => "錫槽",
//				"CFX.A00.SO20050832.Preheat" => "預熱",
//				"CFX.A00.SO20050832.Power" => "總電源",
//				_ => source
//			};

//			return $"{component} => 三相電流(A): {string.Join(", ", currentNowRYB)}, " +
//				   $"有功功率(W): {string.Join(", ", powerNowRYB)}, " +
//				   $"用電量(kWh): {energyUsed}";
//		}

//		/// <summary>
//		/// 解析 StationParametersModified
//		/// </summary>
//		private string ParseStationParams(JsonElement root)
//		{
//			try
//			{
//				var dataEl = root.GetProperty("Data").GetProperty("Data");
//				var rawDataEl = dataEl.GetProperty("RawData");
//				var messageBody = rawDataEl.GetProperty("MessageBody");

//				if (!messageBody.TryGetProperty("ModifiedParameters", out JsonElement modifiedParams))
//				{
//					return null;
//				}

//				var dict = new Dictionary<string, string>();
//				foreach (var param in modifiedParams.EnumerateArray())
//				{
//					string name = param.GetProperty("Name").GetString() ?? "";
//					string value = param.GetProperty("Value").GetString() ?? "";

//					// 若要再篩 Source == Trough/Preheat/Power，可以在這裡繼續判斷
//					// 這裡先保留原本邏輯: 只處理指定欄位
//					if (name.StartsWith("OV_MainTemperature") ||
//						name.StartsWith("OV_PreheatTemperature") ||
//						name.StartsWith("OV_TinBathTemperature") ||
//						name.StartsWith("MSP_SwitchStatus"))
//					{
//						dict[name] = value;
//					}
//				}

//				if (dict.Count == 0)
//					return null;

//				string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
//				string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
//				string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
//				string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

//				return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
//				return "數據提取失敗";
//			}
//		}
//	}
//}

//////using System.Collections.Concurrent;
//////using System.Text;
//////using System.Threading;
//////using System.Threading.Tasks;
//////using Confluent.Kafka;
//////using KafkaConsumerWebAPI.Hubs;
//////using Microsoft.AspNetCore.SignalR;
//////using Microsoft.Extensions.Configuration;
//////using Microsoft.Extensions.Hosting;
//////using System.Text.Json;
//////using Newtonsoft.Json;
//////using System;
//////using System.Linq;
//////using System.Collections.Generic;

//////namespace KafkaConsumerWebAPI.Services
//////{
//////	public class KafkaConsumerService : BackgroundService
//////	{
//////		private readonly ConcurrentQueue<string> _messageQueue;
//////		private readonly IConfiguration _configuration;
//////		private readonly IHubContext<MessageHub> _hubContext;
//////		private readonly MongoDBService _mongoDBService;

//////		private IConsumer<Ignore, byte[]>? _consumer;

//////		public KafkaConsumerService(
//////			ConcurrentQueue<string> messageQueue,
//////			IConfiguration configuration,
//////			IHubContext<MessageHub> hubContext,
//////			MongoDBService mongoDBService)
//////		{
//////			_messageQueue = messageQueue;
//////			_configuration = configuration;
//////			_hubContext = hubContext;
//////			_mongoDBService = mongoDBService;
//////		}

//////		/// <summary>
//////		/// 根據指定的 targetStart 時間（例如 2025-02-07 00:00:00），
//////		/// 使用 OffsetsForTimes 將每個分區的 offset 調整到該時間點附近。
//////		/// </summary>
//////		private void SeekToTargetDateOffsets(IConsumer<Ignore, byte[]> consumer, DateTime targetStart)
//////		{
//////			var partitions = consumer.Assignment;
//////			// 為每個分區建立一個查詢時間點
//////			var timestampsToSearch = partitions
//////				.Select(tp => new TopicPartitionTimestamp(tp, new Timestamp(targetStart, TimestampType.CreateTime)))
//////				.ToList();
//////			// 根據查詢時間取得各分區的 offset
//////			var offsetsForTimes = consumer.OffsetsForTimes(timestampsToSearch, TimeSpan.FromSeconds(10));
//////			foreach (var tpOffset in offsetsForTimes)
//////			{
//////				if (tpOffset.Offset != Offset.Unset)
//////				{
//////					consumer.Seek(tpOffset);
//////				}
//////			}
//////		}

//////		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//////		{
//////			var kafkaConfig = _configuration.GetSection("Kafka");

//////			var consumerConfig = new ConsumerConfig
//////			{
//////				GroupId = kafkaConfig["GroupId"],
//////				BootstrapServers = kafkaConfig["BootstrapServers"],
//////				SecurityProtocol = SecurityProtocol.SaslPlaintext,
//////				SaslMechanism = SaslMechanism.Plain,
//////				SaslUsername = kafkaConfig["SaslUsername"],
//////				SaslPassword = kafkaConfig["SaslPassword"],
//////				AutoOffsetReset = AutoOffsetReset.Earliest, // 改成 Earliest 以便讀取舊數據
//////				EnableAutoCommit = false
//////			};

//////			var topics = new List<string>
//////			{
//////				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
//////				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
//////			};

//////			_consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
//////				.SetValueDeserializer(Deserializers.ByteArray)
//////				.Build();

//////			_consumer.Subscribe(topics);
//////			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

//////			// 設定目標日期：2025-02-07
//////			DateTime targetDate = new DateTime(2025, 2, 7);
//////			DateTime targetStart = targetDate.Date; // 2025-02-07 00:00:00

//////			// 調整 offset 到指定日期的起始位置
//////			SeekToTargetDateOffsets(_consumer, targetStart);

//////			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
//////		}

//////		private async void ConsumeMessages(CancellationToken stoppingToken)
//////		{
//////			// 設置目標日期：2025-02-07整天
//////			DateTime targetDate = new DateTime(2025, 2, 7);
//////			DateTime targetStart = targetDate.Date; // 2025-02-07 00:00:00
//////			DateTime targetEnd = targetDate.Date.AddDays(1).AddTicks(-1); // 2025-02-07 23:59:59.9999999

//////			try
//////			{
//////				while (!stoppingToken.IsCancellationRequested)
//////				{
//////					try
//////					{
//////						var result = _consumer!.Consume(stoppingToken);
//////						if (result.Message?.Value != null)
//////						{
//////							string topicName = result.Topic;
//////							string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

//////							// 使用 JsonDocument 安全提取 TimeStamp
//////							DateTime messageTimestamp;
//////							using (JsonDocument doc = JsonDocument.Parse(originalJsonString))
//////							{
//////								JsonElement root = doc.RootElement;
//////								string timestampString = null;
//////								// 嘗試從 Data -> Data -> RawData -> TimeStamp 中取得
//////								if (root.TryGetProperty("Data", out JsonElement dataEl) &&
//////									dataEl.TryGetProperty("Data", out JsonElement innerDataEl) &&
//////									innerDataEl.TryGetProperty("RawData", out JsonElement rawDataEl) &&
//////									rawDataEl.TryGetProperty("TimeStamp", out JsonElement tsEl))
//////								{
//////									timestampString = tsEl.GetString();
//////								}

//////								if (string.IsNullOrEmpty(timestampString))
//////								{
//////									Console.WriteLine("未能提取 TimeStamp");
//////									continue;
//////								}
//////								messageTimestamp = DateTime.Parse(timestampString);
//////							}

//////							// 檢查消息是否在目標日期範圍內
//////							if (messageTimestamp >= targetStart && messageTimestamp <= targetEnd)
//////							{
//////								// 解析並提取關鍵數據
//////								string relevantData = ExtractRelevantData(topicName, originalJsonString);

//////								if (!string.IsNullOrEmpty(relevantData) && relevantData != "數據提取失敗")
//////								{
//////									// 儲存到 MongoDB
//////									await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

//////									// 通過 SignalR 發送到前端
//////									await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);

//////									Console.WriteLine("成功消費消息，Topic: " + topicName);
//////									Console.WriteLine("Original JSON: ");
//////									Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(originalJsonString), Formatting.Indented));
//////								}
//////							}
//////							else
//////							{
//////								Console.WriteLine($"消息時間 {messageTimestamp} 不在目標日期 {targetDate:yyyy-MM-dd} 內。");
//////							}
//////						}
//////					}
//////					catch (ConsumeException e)
//////					{
//////						Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
//////					}
//////					catch (Exception ex)
//////					{
//////						Console.WriteLine($"處理消息時發生錯誤: {ex.Message}");
//////					}
//////				}
//////			}
//////			catch (OperationCanceledException)
//////			{
//////				Console.WriteLine("消費服務已被取消");
//////			}
//////			finally
//////			{
//////				_consumer?.Close();
//////			}
//////		}

//////		private string ParseEnergyConsumed(JsonElement root)
//////		{
//////			// 1. 定位到 Data -> Data -> RawData -> MessageBody
//////			var dataEl = root.GetProperty("Data").GetProperty("Data");
//////			var rawDataEl = dataEl.GetProperty("RawData");
//////			var messageBody = rawDataEl.GetProperty("MessageBody");

//////			// 2. 取得需要的欄位
//////			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
//////												.EnumerateArray()
//////												.Select(x => x.GetDouble())
//////												.ToArray();
//////			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
//////											  .EnumerateArray()
//////											  .Select(x => x.GetDouble())
//////											  .ToArray();
//////			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

//////			// 3. 根據 Meta.Source 判斷部件位置 (錫槽 / 預熱 / 總電源)
//////			var metaEl = dataEl.GetProperty("Meta");
//////			string source = metaEl.GetProperty("Source").GetString() ?? "";
//////			string component = source switch
//////			{
//////				"CFX.A00.SO20050832.Trough" => "錫槽",
//////				"CFX.A00.SO20050832.Preheat" => "預熱",
//////				"CFX.A00.SO20050832.Power" => "總電源",
//////				_ => source
//////			};

//////			// 4. 組裝字串
//////			return $"{component} => 三相電流(A): {string.Join(", ", currentNowRYB)}, " +
//////				   $"有功功率(W): {string.Join(", ", powerNowRYB)}, " +
//////				   $"用電量(kWh): {energyUsed}";
//////		}

//////		private string ExtractRelevantData(string topic, string jsonString)
//////		{
//////			try
//////			{
//////				using var doc = JsonDocument.Parse(jsonString);
//////				JsonElement root = doc.RootElement;

//////				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
//////				{
//////					return ParseEnergyConsumed(root);
//////				}
//////				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
//////				{
//////					return ParseStationParams(root);
//////				}
//////				else
//////				{
//////					return null;
//////				}
//////			}
//////			catch (Exception ex)
//////			{
//////				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
//////				return "數據提取失敗";
//////			}
//////		}

//////		private string ParseStationParams(JsonElement root)
//////		{
//////			try
//////			{
//////				// 取得 Data -> Data -> RawData -> MessageBody
//////				var dataEl = root.GetProperty("Data").GetProperty("Data");
//////				var rawDataEl = dataEl.GetProperty("RawData");
//////				var messageBody = rawDataEl.GetProperty("MessageBody");

//////				if (!messageBody.TryGetProperty("ModifiedParameters", out JsonElement modifiedParams))
//////				{
//////					return null;
//////				}

//////				var dict = new Dictionary<string, string>();
//////				foreach (var param in modifiedParams.EnumerateArray())
//////				{
//////					string name = param.GetProperty("Name").GetString() ?? "";
//////					string value = param.GetProperty("Value").GetString() ?? "";

//////					// 收集我們需要的參數
//////					if (name.StartsWith("OV_MainTemperature") ||
//////						name.StartsWith("OV_PreheatTemperature") ||
//////						name.StartsWith("OV_TinBathTemperature") ||
//////						name.StartsWith("MSP_SwitchStatus"))
//////					{
//////						dict[name] = value;
//////					}
//////				}

//////				if (dict.Count == 0)
//////				{
//////					return null;
//////				}

//////				string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
//////				string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
//////				string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
//////				string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

//////				return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
//////			}
//////			catch (Exception ex)
//////			{
//////				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
//////				return "數據提取失敗";
//////			}
//////		}
//////	}
//////}




////using System.Collections.Concurrent;
////using System.Text;
////using System.Threading;
////using System.Threading.Tasks;
////using Confluent.Kafka;
////using KafkaConsumerWebAPI.Hubs;
////using Microsoft.AspNetCore.SignalR;
////using Microsoft.Extensions.Configuration;
////using Microsoft.Extensions.Hosting;
////using System.Text.Json;
////using Newtonsoft.Json;
////using System;
////using System.Linq;
////using System.Collections.Generic;

////namespace KafkaConsumerWebAPI.Services
////{
////	// Kafka 消費者服務，繼承自 BackgroundService 以在後台運行
////	public class KafkaConsumerService : BackgroundService
////	{
////		// 消息隊列，用於存儲待處理消息（此處未使用，但可作擴展）
////		private readonly ConcurrentQueue<string> _messageQueue;
////		// 配置設定，例如 Kafka 相關配置
////		private readonly IConfiguration _configuration;
////		// SignalR 上下文，通過它可以將消息推送到前端
////		private readonly IHubContext<MessageHub> _hubContext;
////		// MongoDB 服務，用於將接收到的消息存入資料庫
////		private readonly MongoDBService _mongoDBService;

////		// Kafka 消費者對象，負責連接並消費 Kafka 消息
////		private IConsumer<Ignore, byte[]>? _consumer;

////		// 構造函數，初始化所有依賴項
////		public KafkaConsumerService(
////			ConcurrentQueue<string> messageQueue,    // 消息隊列
////			IConfiguration configuration,              // 配置設定
////			IHubContext<MessageHub> hubContext,         // SignalR 上下文
////			MongoDBService mongoDBService)             // MongoDB 服務
////		{
////			_messageQueue = messageQueue;      // 初始化消息隊列
////			_configuration = configuration;    // 初始化配置設定
////			_hubContext = hubContext;          // 初始化 SignalR 上下文
////			_mongoDBService = mongoDBService;  // 初始化 MongoDB 服務
////		}

////		/// <summary>
////		/// 根據指定的 targetStartUtc 時間（例如 2025-02-06 16:00:00 UTC），
////		/// 使用 OffsetsForTimes 方法將每個分區的 offset 調整到該時間點附近，
////		/// 以便從指定時間開始消費消息。
////		/// </summary>
////		/// <param name="consumer">Kafka 消費者</param>
////		/// <param name="targetStartUtc">指定的 UTC 時間起始點</param>
////		private void SeekToTargetDateOffsets(IConsumer<Ignore, byte[]> consumer, DateTime targetStartUtc)
////		{
////			// 獲取消費者分區列表
////			var partitions = consumer.Assignment;
////			// 為每個分區建立一個查詢時間點，目標時間以 UTC 計
////			var timestampsToSearch = partitions
////				.Select(tp => new TopicPartitionTimestamp(tp, new Timestamp(targetStartUtc, TimestampType.CreateTime)))
////				.ToList();
////			// 使用 OffsetsForTimes 根據目標時間查詢每個分區的 offset
////			var offsetsForTimes = consumer.OffsetsForTimes(timestampsToSearch, TimeSpan.FromSeconds(10));
////			foreach (var tpOffset in offsetsForTimes)
////			{
////				// 如果該分區找到了有效 offset (非 Unset)
////				if (tpOffset.Offset != Offset.Unset)
////				{
////					// 設置該分區的 offset 為查詢結果
////					consumer.Seek(tpOffset);
////				}
////			}
////		}

////		/// <summary>
////		/// BackgroundService 的主入口，負責啟動 Kafka 消費者，訂閱 Topics 並開始消費消息
////		/// </summary>
////		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
////		{
////			// 從配置中讀取 Kafka 配置節點
////			var kafkaConfig = _configuration.GetSection("Kafka");

////			// 定義 Kafka 消費者的配置參數
////			var consumerConfig = new ConsumerConfig
////			{
////				GroupId = kafkaConfig["GroupId"],                    // 消費者群組 ID
////				BootstrapServers = kafkaConfig["BootstrapServers"],  // Kafka 伺服器地址
////				SecurityProtocol = SecurityProtocol.SaslPlaintext,     // 安全協議
////				SaslMechanism = SaslMechanism.Plain,                   // SASL 認證機制
////				SaslUsername = kafkaConfig["SaslUsername"],            // SASL 用戶名
////				SaslPassword = kafkaConfig["SaslPassword"],            // SASL 密碼
////				AutoOffsetReset = AutoOffsetReset.Earliest,            // 若沒有有效偏移量，從最早消息開始消費
////				EnableAutoCommit = false                               // 禁用自動提交偏移量
////			};

////			// 定義訂閱的 Kafka Topics
////			var topics = new List<string>
////			{
////				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
////				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
////			};

////			// 建立 Kafka 消費者，並設定消息值的反序列化器（轉換為 byte[]）
////			_consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
////				.SetValueDeserializer(Deserializers.ByteArray)
////				.Build();

////			// 訂閱指定的 Kafka Topics
////			_consumer.Subscribe(topics);
////			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

////			// 設定搜尋的目標日期（UTC+8 的 2025-02-07），轉換為 UTC 時間如下：
////			// 2025-02-07 00:00:00 (UTC+8)  -> 2025-02-06 16:00:00 UTC
////			DateTime targetStartUtc = new DateTime(2025, 2, 7).Date.AddHours(-8);

////			// 調用方法根據目標時間調整各分區的 offset
////			SeekToTargetDateOffsets(_consumer, targetStartUtc);

////			// 開始在新的執行緒中消費消息
////			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
////		}

////		/// <summary>
////		/// 消費消息，並過濾出 UTC 時間在 2025-02-06 16:00:00 到 2025-02-07 15:59:59 之間的消息
////		/// (這個範圍對應於 UTC+8 時區下 2025-02-07 的整天)
////		/// </summary>
////		private async void ConsumeMessages(CancellationToken stoppingToken)
////		{
////			// 設定目標時間範圍（UTC+0）：
////			// 2025-02-07 00:00:00 (UTC+8)  -> 2025-02-06 16:00:00 UTC (起始)
////			// 2025-02-07 23:59:59 (UTC+8)  -> 2025-02-07 15:59:59 UTC (結束)
////			DateTime targetStartUtc = new DateTime(2025, 2, 7).Date.AddHours(-8); // 2025-02-06 16:00:00 UTC
////			DateTime targetEndUtc = new DateTime(2025, 2, 7).Date.AddDays(1).AddHours(-8).AddTicks(-1); // 2025-02-07 15:59:59.9999999 UTC

////			try
////			{
////				// 持續消費直到取消
////				while (!stoppingToken.IsCancellationRequested)
////				{
////					try
////					{
////						// 消費一條 Kafka 消息
////						var result = _consumer!.Consume(stoppingToken);
////						if (result.Message?.Value != null)
////						{
////							// 取得消息所在的 Topic
////							string topicName = result.Topic;
////							// 將消息的 byte[] 轉換成字串（通常是 JSON 格式）
////							string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

////							// 使用 JsonDocument 解析 JSON，並提取 TimeStamp (該字段為 UTC+0 時間)
////							DateTime messageTimestamp;
////							using (JsonDocument doc = JsonDocument.Parse(originalJsonString))
////							{
////								JsonElement root = doc.RootElement;
////								string timestampString = null;
////								// 嘗試從 JSON 結構 Data -> Data -> RawData -> TimeStamp 提取時間戳
////								if (root.TryGetProperty("Data", out JsonElement dataEl) &&
////									dataEl.TryGetProperty("Data", out JsonElement innerDataEl) &&
////									innerDataEl.TryGetProperty("RawData", out JsonElement rawDataEl) &&
////									rawDataEl.TryGetProperty("TimeStamp", out JsonElement tsEl))
////								{
////									timestampString = tsEl.GetString();
////								}
////								if (string.IsNullOrEmpty(timestampString))
////								{
////									Console.WriteLine("未能提取 TimeStamp");
////									continue; // 無法取得時間戳則跳過該消息
////								}
////								// 解析時間戳字串為 DateTime (假設其為 UTC+0 時間)
////								messageTimestamp = DateTime.Parse(timestampString);
////							}

////							// 檢查消息是否在目標時間範圍內
////							if (messageTimestamp >= targetStartUtc && messageTimestamp <= targetEndUtc)
////							{
////								// 根據 Topic 提取關鍵數據
////								string relevantData = ExtractRelevantData(topicName, originalJsonString);

////								// 如果數據提取成功，則進行後續處理
////								if (!string.IsNullOrEmpty(relevantData) && relevantData != "數據提取失敗")
////								{
////									// 儲存消息到 MongoDB
////									await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

////									// 通過 SignalR 將消息推送到前端
////									await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);

////									// 打印成功消費消息的日誌
////									Console.WriteLine("成功消費消息，Topic: " + topicName);
////									Console.WriteLine("Original JSON: ");
////									Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(originalJsonString), Formatting.Indented));
////								}
////							}
////							else
////							{
////								// 如果消息不在目標時間範圍內，則打印提示日誌
////								Console.WriteLine($"消息時間 {messageTimestamp} 不在目標日期範圍 {targetStartUtc:yyyy-MM-dd HH:mm:ss} - {targetEndUtc:yyyy-MM-dd HH:mm:ss} 內。");
////							}
////						}
////					}
////					catch (ConsumeException e)
////					{
////						// 捕獲 Kafka 消費異常，並打印錯誤訊息
////						Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
////					}
////					catch (Exception ex)
////					{
////						// 捕獲其他異常，並打印錯誤訊息
////						Console.WriteLine($"處理消息時發生錯誤: {ex.Message}");
////					}
////				}
////			}
////			catch (OperationCanceledException)
////			{
////				// 當操作被取消時，打印提示
////				Console.WriteLine("消費服務已被取消");
////			}
////			finally
////			{
////				// 最後關閉 Kafka 消費者連線
////				_consumer?.Close();
////			}
////		}

////		/// <summary>
////		/// 解析 EnergyConsumed 主題的消息，從 JSON 中提取三相電流、功率及用電量，
////		/// 並根據 Meta.Source 判斷該消息屬於哪個部件（例如 錫槽、預熱或總電源）
////		/// </summary>
////		private string ParseEnergyConsumed(JsonElement root)
////		{
////			// 定位到 JSON 中的 Data -> Data 部分
////			var dataEl = root.GetProperty("Data").GetProperty("Data");
////			// 定位到 RawData 部分
////			var rawDataEl = dataEl.GetProperty("RawData");
////			// 定位到 MessageBody 部分，這裡包含實際數據
////			var messageBody = rawDataEl.GetProperty("MessageBody");

////			// 從 MessageBody 中提取三相電流數組
////			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
////												.EnumerateArray()
////												.Select(x => x.GetDouble())
////												.ToArray();
////			// 從 MessageBody 中提取三相功率數組
////			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
////											  .EnumerateArray()
////											  .Select(x => x.GetDouble())
////											  .ToArray();
////			// 從 MessageBody 中提取用電量
////			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

////			// 根據 Meta.Source 判斷部件位置
////			var metaEl = dataEl.GetProperty("Meta");
////			string source = metaEl.GetProperty("Source").GetString() ?? "";
////			string component = source switch
////			{
////				"CFX.A00.SO20050832.Trough" => "錫槽",
////				"CFX.A00.SO20050832.Preheat" => "預熱",
////				"CFX.A00.SO20050832.Power" => "總電源",
////				_ => source
////			};

////			// 組裝並返回包含部件、電流、功率和用電量的字串
////			return $"{component} => 三相電流(A): {string.Join(", ", currentNowRYB)}, " +
////				   $"有功功率(W): {string.Join(", ", powerNowRYB)}, " +
////				   $"用電量(kWh): {energyUsed}";
////		}

////		/// <summary>
////		/// 根據 Topic 選擇不同解析方法，提取消息中關鍵數據
////		/// </summary>
////		private string ExtractRelevantData(string topic, string jsonString)
////		{
////			try
////			{
////				// 解析 JSON 字串
////				using var doc = JsonDocument.Parse(jsonString);
////				JsonElement root = doc.RootElement;

////				// 如果是 EnergyConsumed 主題，則解析能耗數據
////				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
////				{
////					return ParseEnergyConsumed(root);
////				}
////				// 如果是 StationParametersModified 主題，則解析站點參數
////				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
////				{
////					return ParseStationParams(root);
////				}
////				else
////				{
////					return null;
////				}
////			}
////			catch (Exception ex)
////			{
////				// 捕獲解析錯誤，並打印日誌
////				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
////				return "數據提取失敗";
////			}
////		}

////		/// <summary>
////		/// 解析 StationParametersModified 主題的消息，從 JSON 中提取 ModifiedParameters，
////		/// 並組裝需要的參數顯示字串。
////		/// </summary>
////		private string ParseStationParams(JsonElement root)
////		{
////			try
////			{
////				// 定位到 JSON 中的 Data -> Data 部分
////				var dataEl = root.GetProperty("Data").GetProperty("Data");
////				// 定位到 RawData 部分
////				var rawDataEl = dataEl.GetProperty("RawData");
////				// 定位到 MessageBody 部分
////				var messageBody = rawDataEl.GetProperty("MessageBody");

////				// 嘗試安全提取 ModifiedParameters 屬性，若不存在則返回 null
////				if (!messageBody.TryGetProperty("ModifiedParameters", out JsonElement modifiedParams))
////				{
////					return null;
////				}

////				// 建立字典存放需要的參數
////				var dict = new Dictionary<string, string>();
////				foreach (var param in modifiedParams.EnumerateArray())
////				{
////					// 提取參數名稱
////					string name = param.GetProperty("Name").GetString() ?? "";
////					// 提取參數值
////					string value = param.GetProperty("Value").GetString() ?? "";

////					// 只收集我們需要的參數（例如溫度、開關狀態等）
////					if (name.StartsWith("OV_MainTemperature") ||
////						name.StartsWith("OV_PreheatTemperature") ||
////						name.StartsWith("OV_TinBathTemperature") ||
////						name.StartsWith("MSP_SwitchStatus"))
////					{
////						dict[name] = value;
////					}
////				}

////				// 若字典中沒有任何參數，則返回 null
////				if (dict.Count == 0)
////				{
////					return null;
////				}

////				// 組裝各項參數的顯示字串
////				string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
////				string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
////				string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
////				string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

////				// 返回組裝好的參數顯示字串
////				return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
////			}
////			catch (Exception ex)
////			{
////				// 捕獲解析錯誤，並打印錯誤信息
////				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
////				return "數據提取失敗";
////			}
////		}
////	}
////}



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
//		/// （如果需要針對日期回溯，則需實作 OffsetsForTimes。但此處示範即時最新模式即可。）
//		/// </summary>
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
//				AutoOffsetReset = AutoOffsetReset.Latest, // 僅示範即時最新資料
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

//			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
//		}

//		private async void ConsumeMessages(CancellationToken stoppingToken)
//		{
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

//							// 1) 將消息存入 MongoDB （含重複檢查）
//							await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

//							// 2) (選擇性)用 SignalR 推送前端
//							await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);

//							// 3) 印出日誌
//							Console.WriteLine($"成功消費消息，Topic: {topicName}");
//							Console.WriteLine("Original JSON: ");
//							Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(originalJsonString), Formatting.Indented));
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
//	}
//}

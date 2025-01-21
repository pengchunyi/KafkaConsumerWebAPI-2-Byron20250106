using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using KafkaConsumerWebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace KafkaConsumerWebAPI.Services
{
	public class KafkaConsumerService : BackgroundService
	{
		private readonly ConcurrentQueue<string> _messageQueue;
		private readonly IConfiguration _configuration;
		private readonly IHubContext<MessageHub> _hubContext;
		private readonly MongoDBService _mongoDBService;

		private IConsumer<Ignore, byte[]>? _consumer;

		public KafkaConsumerService(
			ConcurrentQueue<string> messageQueue,
			IConfiguration configuration,
			IHubContext<MessageHub> hubContext,
			MongoDBService mongoDBService)
		{
			_messageQueue = messageQueue;
			_configuration = configuration;
			_hubContext = hubContext;
			_mongoDBService = mongoDBService;
		}

		// 調整 Consumer Group 偏移量到最新
		private void SeekToLatestOffsets(IConsumer<Ignore, byte[]> consumer)
		{
			foreach (var partition in consumer.Assignment)
			{
				try
				{
					// 查詢分區的最新偏移量
					var offsets = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(10));

					// 將消費者偏移量移動到最新的偏移量
					consumer.Seek(new TopicPartitionOffset(partition, offsets.High));
				}
				catch (Exception ex)
				{
					Console.WriteLine($"調整偏移量時發生錯誤，分區: {partition.Partition.Value}, 錯誤: {ex.Message}");
				}
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var kafkaConfig = _configuration.GetSection("Kafka");

			var consumerConfig = new ConsumerConfig
			{
				GroupId = kafkaConfig["GroupId"],
				BootstrapServers = kafkaConfig["BootstrapServers"],
				SecurityProtocol = SecurityProtocol.SaslPlaintext,
				SaslMechanism = SaslMechanism.Plain,
				SaslUsername = kafkaConfig["SaslUsername"],
				SaslPassword = kafkaConfig["SaslPassword"],
				AutoOffsetReset = AutoOffsetReset.Latest, // 修改為從最新的數據開始
				EnableAutoCommit = false
			};

			var topics = new List<string>
			{
				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
			};

			_consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
				.SetValueDeserializer(Deserializers.ByteArray)
				.Build();

			_consumer.Subscribe(topics);
			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

			// 調整偏移量到最新
			SeekToLatestOffsets(_consumer);

			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
		}

		private async void ConsumeMessages(CancellationToken stoppingToken)
		{
			try
			{
				while (!stoppingToken.IsCancellationRequested)
				{
					try
					{
						var result = _consumer!.Consume(stoppingToken);
						if (result.Message?.Value != null)
						{
							string topicName = result.Topic;
							string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

							// 解析 & 提取關鍵數據
							string relevantData = ExtractRelevantData(topicName, originalJsonString);

							// 如果 relevantData 不為空，才視為「成功消費」
							if (!string.IsNullOrEmpty(relevantData) && relevantData != "數據提取失敗")
							{
								// 可以在這裡先儲存到 MongoDB
								await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

								// 再透過 SignalR 推送前端
								//await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, relevantData, stoppingToken);
								await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);


								// 最後才印出日誌
								//Console.WriteLine($"成功消費消息，Topic: {topicName}, Relevant Data: {relevantData}");

								//250121_修改為直接印出json格式的log
								Console.WriteLine($"成功消費消息，Topic: {topicName}, Original JSON: {originalJsonString}");



							}
							// else: 代表沒有興趣的數據，就「啥都不做」，不印也不存
						}
					}
					catch (ConsumeException e)
					{
						Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"處理消息時發生錯誤: {ex.Message}");
					}
				}
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("消費服務已被取消");
			}
			finally
			{
				_consumer?.Close();
			}
		}




		private string ParseEnergyConsumed(JsonElement root)
		{
			// 和你原本在 ExtractRelevantData 中
			// 針對 topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed"
			// 所使用的解析邏輯一樣

			// 1. 定位到 Data -> Data -> RawData -> MessageBody
			var dataEl = root.GetProperty("Data").GetProperty("Data");
			var rawDataEl = dataEl.GetProperty("RawData");
			var messageBody = rawDataEl.GetProperty("MessageBody");

			// 2. 取你需要的欄位
			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
												.EnumerateArray()
												.Select(x => x.GetDouble())
												.ToArray();
			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
											  .EnumerateArray()
											  .Select(x => x.GetDouble())
											  .ToArray();
			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

			// 3. 根據 Meta.Source 判斷部件位置 (錫槽 / 預熱 / 總電源)
			var metaEl = dataEl.GetProperty("Meta");
			string source = metaEl.GetProperty("Source").GetString() ?? "";
			string component = source switch
			{
				"CFX.A00.SO20050832.Trough" => "錫槽",
				"CFX.A00.SO20050832.Preheat" => "預熱",
				"CFX.A00.SO20050832.Power" => "總電源",
				_ => source
			};

			// 4. 組裝要顯示的字串
			// 這裡示範格式: 錫槽 => Current(A): 5.6,5.8,5.8, Power(W): 679,770,727, EnergyUsed(kWh): 507.27
			// 你可以依需求調整
			return $"{component} => 三相電流(A): {string.Join(", ", currentNowRYB)}, " +
				   $"有功功率(W): {string.Join(", ", powerNowRYB)}, " +
				   $"用電量(kWh): {energyUsed}";
		}

		private string ExtractRelevantData(string topic, string jsonString)
		{
			try
			{
				using var doc = JsonDocument.Parse(jsonString);
				JsonElement root = doc.RootElement;

				// 依照 Topic 來判斷
				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
				{
					// ... 這段保持原本，你的 EnergyConsumed 解析
					return ParseEnergyConsumed(root);
				}
				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
				{
					// 先把 relevantData 解析出來
					string relevantData = ParseStationParams(root);

					// 如果 relevantData 為 null 或空字串，表示不相關
					return relevantData;
				}
				else
				{
					// 其他 Topic 不處理，回傳空
					return null;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
				return "數據提取失敗";
			}
		}


		// 解析 StationParametersModified 的方法
		private string ParseStationParams(JsonElement root)
		{
			// 1. 取得 RawData.MessageBody.ModifiedParameters (別忘了一層 Data->Data)
			var dataEl = root.GetProperty("Data").GetProperty("Data");
			var rawDataEl = dataEl.GetProperty("RawData");
			var messageBody = rawDataEl.GetProperty("MessageBody");
			var modifiedParams = messageBody.GetProperty("ModifiedParameters").EnumerateArray();

			// 2. 過濾出我們關心的參數
			//    （假設我們只關心 "OV_MainTemperatureX"、"OV_PreheatTemperatureX"、"OV_TinBathTemperatureX"、"MSP_SwitchStatusX" 等）
			//    可以用 Dictionary<string, string> 存放。若找不到，預設就不塞入。
			var dict = new Dictionary<string, string>();
			foreach (var param in modifiedParams)
			{
				string name = param.GetProperty("Name").GetString() ?? "";
				string value = param.GetProperty("Value").GetString() ?? "";

				// 只有符合需求的才收集
				if (name.StartsWith("OV_MainTemperature") ||
					name.StartsWith("OV_PreheatTemperature") ||
					name.StartsWith("OV_TinBathTemperature") ||
					name.StartsWith("MSP_SwitchStatus"))
				{
					dict[name] = value;
				}
			}

			// 3. 如果 dict 裡面完全沒包含我們想要的欄位，代表這則消息「不相關」，直接 return null 或空字串
			if (dict.Count == 0)
			{
				return null;  // null 代表這則不顯示
			}

			// 4. 若有資料，就跟之前一樣組裝成一段文字
			string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
			string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
			string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
			string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

			return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
		}




	}
}

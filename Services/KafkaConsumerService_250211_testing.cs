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
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;

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
					var offsets = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(10));
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
			// 1. 讀取 Kafka 設定
			var kafkaConfig = _configuration.GetSection("Kafka");
			var consumerConfig = new ConsumerConfig
			{
				GroupId = kafkaConfig["GroupId"] ?? "default-consumer-group",
				BootstrapServers = kafkaConfig["BootstrapServers"],

				//250314測試3廠kafka========================================
				//SecurityProtocol = SecurityProtocol.SaslPlaintext,
				//SaslMechanism = SaslMechanism.Plain,
				//SaslUsername = kafkaConfig["SaslUsername"],
				//SaslPassword = kafkaConfig["SaslPassword"],
				//250314測試3廠kafka========================================
				AutoOffsetReset = AutoOffsetReset.Latest, // 最新的數據
				//AutoOffsetReset = AutoOffsetReset.Earliest, // 最舊的數據
				EnableAutoCommit = false
				//EnableAutoCommit = true
			};

			// 2. 訂閱 Topic
			var topics = new List<string>
			{
				//"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
				//"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified",
				//"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.FaultOccurred"

				////250314測試3廠kafka========================================
				"EAP.DG3.SV.S01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed"
				////250314測試3廠kafka========================================
			};

			_consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
				.SetValueDeserializer(Deserializers.ByteArray)
				.Build();

			_consumer.Subscribe(topics);
			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

			// 3. 調整 offset 到最新
			SeekToLatestOffsets(_consumer);

			// 4. 開始消費
			await Task.Run(() => ConsumeMessages(stoppingToken), stoppingToken);
		}

		private async void ConsumeMessages(CancellationToken stoppingToken)
		{
			try
			{
				while (!stoppingToken.IsCancellationRequested)
				{
					var result = _consumer!.Consume(stoppingToken);
					if (result.Message?.Value != null)
					{
						string topicName = result.Topic;
						string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

						// 解析
						string relevantData = ExtractRelevantData(topicName, originalJsonString);

						// 若 relevantData 不為空 => 寫 DB + SignalR 推送
						if (!string.IsNullOrEmpty(relevantData) && relevantData != "數據提取失敗")
						{
							// 寫入 MongoDB (有唯一索引做重複檢查)
							await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

							// 用 SignalR 即時推送到前端
							await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, originalJsonString, stoppingToken);

							// 印出日誌
							Console.WriteLine("成功消費消息，Topic: " + topicName);
							Console.WriteLine("Original JSON: ");
							Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(originalJsonString), Formatting.Indented));
						}
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

		// 解析 EnergyConsumed
		private string ParseEnergyConsumed(JsonElement root)
		{
			var dataEl = root.GetProperty("Data").GetProperty("Data");
			var rawDataEl = dataEl.GetProperty("RawData");
			var messageBody = rawDataEl.GetProperty("MessageBody");

			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
												.EnumerateArray()
												.Select(x => x.GetDouble())
												.ToArray();
			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
											  .EnumerateArray()
											  .Select(x => x.GetDouble())
											  .ToArray();
			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

			var metaEl = dataEl.GetProperty("Meta");
			string source = metaEl.GetProperty("Source").GetString() ?? "";
			string component = source switch
			{
				"CFX.A00.SO20050832.Trough" => "錫槽",
				"CFX.A00.SO20050832.Preheat" => "預熱",
				"CFX.A00.SO20050832.Power" => "總電源",
				_ => null
			};

			// 如果不是 Trough/Preheat/Power => 不處理
			if (component == null)
			{
				return null;
			}

			return $"{component} => Current(A): {string.Join(", ", currentNowRYB)}, " +
				   $"Power(W): {string.Join(", ", powerNowRYB)}, " +
				   $"EnergyUsed(kWh): {energyUsed}";
		}

		// 解析 StationParametersModified
		private string ParseStationParams(JsonElement root)
		{
			try
			{
				var dataEl = root.GetProperty("Data").GetProperty("Data");
				var rawDataEl = dataEl.GetProperty("RawData");
				var messageBody = rawDataEl.GetProperty("MessageBody");

				if (!messageBody.TryGetProperty("ModifiedParameters", out JsonElement modifiedParams))
				{
					return null;
				}

				var dict = new Dictionary<string, string>();
				foreach (var param in modifiedParams.EnumerateArray())
				{
					string name = param.GetProperty("Name").GetString() ?? "";
					string value = param.GetProperty("Value").GetString() ?? "";

					if (name.StartsWith("OV_MainTemperature") ||
						name.StartsWith("OV_PreheatTemperature") ||
						name.StartsWith("OV_TinBathTemperature") ||

						//250217_新增===========================================================
						name.StartsWith("OV_SwitchStatusMain") ||
						name.StartsWith("OV_SwitchStatusPreHeat") ||
						name.StartsWith("OV_SwitchStatusTinBath")
						//250217_新增===========================================================
						)
					{
						dict[name] = value;
					}
				}

				if (dict.Count == 0) return null;

				string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
				string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
				string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
				string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

				return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ParseStationParams Error: {ex.Message}");
				return "數據提取失敗";
			}
		}


		//250217_新增===========================================
		// 解析 FaultOccurred
		private string ParseFaultOccurred(JsonElement root)
		{
			try
			{
				var dataEl = root.GetProperty("Data").GetProperty("Data");
				var rawDataEl = dataEl.GetProperty("RawData");
				var messageBody = rawDataEl.GetProperty("MessageBody");

				// 取出 Fault 裡面的資訊
				var faultEl = messageBody.GetProperty("Fault");
				string faultCode = faultEl.GetProperty("FaultCode").GetString() ?? "";
				string severity = faultEl.GetProperty("Severity").GetString() ?? "";
				string description = faultEl.TryGetProperty("Description", out JsonElement descEl) ? descEl.GetString() : null;

				// 如果 Source = "CFX.A00.SO20050832"，代表需要在前端顯示「異常」
				var metaEl = dataEl.GetProperty("Meta");
				string source = metaEl.GetProperty("Source").GetString() ?? "";

				// 這裡可以依需求回傳字串，或直接回傳 null
				// 只要讓前端知道 runStatus = "異常" 即可
				// 這裡先回傳個描述
				return $"FaultOccurred => FaultCode: {faultCode}, Severity: {severity}, Desc: {description}, Source: {source}";
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ParseFaultOccurred Error: {ex.Message}");
				return "數據提取失敗";
			}


		}
		//250217_新增===========================================


		// 主解析入口
		private string ExtractRelevantData(string topic, string jsonString)
		{
			try
			{
				using var doc = JsonDocument.Parse(jsonString);
				JsonElement root = doc.RootElement;

				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
				{
					return ParseEnergyConsumed(root);
				}
				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
				{
					return ParseStationParams(root);
				}

				//250217_新增===========================================
				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.FaultOccurred")
				{
					return ParseFaultOccurred(root);
				}
				//250217_新增===========================================
				else
				{
					return null;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
				return "數據提取失敗";
			}
		}
	}
}

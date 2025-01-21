//using System.Collections.Concurrent;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Confluent.Kafka;
//using Confluent.Kafka.Admin;
//using KafkaConsumerWebAPI.Hubs;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Hosting;
//using static Confluent.Kafka.ConfigPropertyNames;


//using MongoDB.Bson;
//using System.Text.Json;

//namespace KafkaConsumerWebAPI.Services
//{
//	public class KafkaConsumerService : BackgroundService
//	{
//		private readonly ConcurrentQueue<string> _messageQueue;
//		private readonly IConfiguration _configuration;
//		private readonly IHubContext<MessageHub> _hubContext; // 新增 SignalR HubContext
//		//250115新增=====================================
//		private readonly MongoDBService _mongoDBService;


//		public KafkaConsumerService(
//			ConcurrentQueue<string> messageQueue, 
//			IConfiguration configuration, 
//			IHubContext<MessageHub> hubContext,
//			MongoDBService mongoDBService
//			)
//		{
//            _messageQueue = messageQueue;
//			_configuration = configuration;
//			_hubContext = hubContext; // 注入 HubContext
//			//250115新增=====================================
//			_mongoDBService = mongoDBService;
//		}

//		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//		{
//			var kafkaConfig = _configuration.GetSection("Kafka");
//			var adminConfig = new AdminClientConfig
//			{
//				BootstrapServers = kafkaConfig["BootstrapServers"],
//				SecurityProtocol = SecurityProtocol.SaslPlaintext,
//				SaslMechanism = SaslMechanism.Plain,
//				SaslUsername = kafkaConfig["SaslUsername"],
//				SaslPassword = kafkaConfig["SaslPassword"]
//			};

//			var consumerConfig = new ConsumerConfig
//			{
//				GroupId = kafkaConfig["GroupId"],
//				BootstrapServers = kafkaConfig["BootstrapServers"],
//				SecurityProtocol = SecurityProtocol.SaslPlaintext,
//				SaslMechanism = SaslMechanism.Plain,
//				SaslUsername = kafkaConfig["SaslUsername"],
//				SaslPassword = kafkaConfig["SaslPassword"],
//				AutoOffsetReset = AutoOffsetReset.Earliest,
//				SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
//				EnableAutoCommit = false

//			};

//			//250106_update===============================================
//			// 定義兩個要訂閱的 Kafka topics
//			var topics = new List<string>
//			{
//				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
//				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
//			};

//			//250106_update===============================================

//			consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
//				.SetValueDeserializer(Deserializers.ByteArray)
//				.Build();

//			consumer.Subscribe(topics);

//			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

//			taskA = Task.Run(() =>
//			{
//				test1(consumer, stoppingToken);
//			});

//        }
//		IConsumer<Ignore, byte[]> consumer;
//        Task taskA;

//        private async void test1(IConsumer<Ignore, byte[]> consumer, CancellationToken stoppingToken)
//		{
//            try
//            {

//                while (!stoppingToken.IsCancellationRequested)
//                {
//                    try
//                    {
//                        // 這裡放入您的新增代碼，處理每次消費時的消息處理和錯誤記錄
//                        var result = consumer.Consume(stoppingToken);
//                        Console.WriteLine($"成功消費消息: {result.Message.Value}");

//                        if (result.Message?.Value != null)
//                        {
//                            // 解碼消息
//                            var decodedMessage = DecodeMessage(result.Message.Value);

//                            // 緩存消息
//                            var formattedMessage = $"Topic: {result.Topic}, Decoded Message: {decodedMessage}";
//							// 解析 Kafka 消息的 JSON 格式
//							//var kafkaMessageJson = JsonSerializer.Deserialize<JsonElement>(utf8Message);
//							var originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

//							_messageQueue.Enqueue(formattedMessage);

//                            Console.WriteLine(formattedMessage);

//                            // 通過 SignalR 推送到前端
//                            await _hubContext.Clients.All.SendAsync("ReceiveMessage", result.Topic, decodedMessage, stoppingToken);



//							// 構造 MongoDB 插入資料
//							//var mongoDocument = new BsonDocument
//							//{
//							//	{ "Topic", result.Topic },
//							//	{ "Message", kafkaMessageJson }
//							//};
//							// 250115_將數據存入 MongoDB
//							//await _mongoDBService.InsertMessageAsync(formattedMessage);
//							// 插入資料
//							//await _mongoDBService.InsertMessageAsync(mongoDocument);
//							await _mongoDBService.InsertMessageAsync(originalJsonString);

//						}
//                    }
//                    catch (ConsumeException e)
//                    {
//                        Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"消費消息失敗: {ex.Message}");
//                    }
//					//250106_byron哥新增的=========================================
//					Thread.SpinWait(10);
//					//250106_byron哥新增的=========================================
//				}



//			}
//            catch (OperationCanceledException)
//            {
//                Console.WriteLine("消費服務已被取消");
//            }
//            finally
//            {
//                consumer.Close();
//            }
//        }


//		private string DecodeMessage(byte[] messageValue)
//		{
//			try
//			{
//				// 將 Byte 陣列轉為 UTF-8 字串
//				string utf8Value = Encoding.UTF8.GetString(messageValue);

//				// 嘗試解析 JSON
//				var json = System.Text.Json.JsonDocument.Parse(utf8Value);

//				// 用於儲存結果的 StringBuilder
//				var resultBuilder = new System.Text.StringBuilder();

//				// 遞迴方法，用於提取所有鍵值對
//				void ExtractKeyValuePairs(JsonElement element, string prefix = "")
//				{
//					foreach (var property in element.EnumerateObject())
//					{
//						var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

//						if (property.Value.ValueKind == JsonValueKind.Object)
//						{
//							// 如果值是物件，遞迴處理
//							ExtractKeyValuePairs(property.Value, key);
//						}
//						else if (property.Value.ValueKind == JsonValueKind.Array)
//						{
//							// 如果值是陣列，逐一處理
//							int index = 0;
//							foreach (var arrayElement in property.Value.EnumerateArray())
//							{
//								if (arrayElement.ValueKind == JsonValueKind.Object)
//								{
//									ExtractKeyValuePairs(arrayElement, $"{key}[{index}]");
//								}
//								else
//								{
//									resultBuilder.AppendLine($"{key}[{index}]: {arrayElement}".Trim());
//								}
//								index++;
//							}
//						}
//						else
//						{
//							// 如果值是基本類型，直接添加鍵值對
//							resultBuilder.AppendLine($"{key}: {property.Value}".Trim());
//						}
//					}
//				}

//				// 從根開始提取鍵值對
//				ExtractKeyValuePairs(json.RootElement);

//				// 返回格式化結果
//				return resultBuilder.ToString().Trim();
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"解碼失敗: {ex.Message}");
//				return "解碼失敗";
//			}
//		}





//	}
//}



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

//		private void SeekToLatestOffsets(IConsumer<Ignore, byte[]> consumer)
//		{
//			foreach (var partition in consumer.Assignment)
//			{
//				try
//				{
//					// 查詢分區的最新偏移量
//					var offsets = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(10));

//					// 將消費者偏移量移動到最新的偏移量
//					consumer.Seek(new TopicPartitionOffset(partition, offsets.High));
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"調整偏移量時發生錯誤，分區: {partition.Partition.Value}, 錯誤: {ex.Message}");
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
//				AutoOffsetReset = AutoOffsetReset.Latest,
//				EnableAutoCommit = false
//			};

//			var adminConfig = new AdminClientConfig
//			{
//				BootstrapServers = kafkaConfig["BootstrapServers"],
//				SecurityProtocol = SecurityProtocol.SaslPlaintext,
//				SaslMechanism = SaslMechanism.Plain,
//				SaslUsername = kafkaConfig["SaslUsername"],
//				SaslPassword = kafkaConfig["SaslPassword"]
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

//			await SeekToLatestOffsets(_consumer, topics, adminConfig);
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

//							string relevantData = ExtractRelevantData(topicName, originalJsonString);
//							await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);
//							await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, relevantData, stoppingToken);

//							Console.WriteLine($"成功消費消息，Topic: {topicName}, Relevant Data: {relevantData}");
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

//		private string ExtractRelevantData(string topic, string jsonString)
//		{
//			try
//			{
//				var json = JsonDocument.Parse(jsonString);

//				if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
//				{
//					var messageBody = json.RootElement.GetProperty("Data")
//													  .GetProperty("RawData")
//													  .GetProperty("MessageBody");

//					var currentNowRYB = messageBody.GetProperty("CurrentNowRYB").EnumerateArray().Select(x => x.GetDouble()).ToArray();
//					var powerNowRYB = messageBody.GetProperty("PowerNowRYB").EnumerateArray().Select(x => x.GetDouble()).ToArray();
//					var energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

//					return $"CurrentNowRYB: {string.Join(", ", currentNowRYB)}, PowerNowRYB: {string.Join(", ", powerNowRYB)}, EnergyUsed: {energyUsed}";
//				}
//				else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
//				{
//					var messageBody = json.RootElement.GetProperty("Data")
//													  .GetProperty("RawData")
//													  .GetProperty("MessageBody");

//					var modifiedParameters = messageBody.GetProperty("ModifiedParameters")
//														.EnumerateArray()
//														.Select(param => $"{param.GetProperty("Name").GetString()}: {param.GetProperty("Value").GetString()}")
//														.ToArray();

//					return string.Join("; ", modifiedParameters);
//				}

//				return "未知的 Topic";
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
//				return "數據提取失敗";
//			}
//		}
//	}
//}




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

		//private async void ConsumeMessages(CancellationToken stoppingToken)
		//{
		//	try
		//	{
		//		while (!stoppingToken.IsCancellationRequested)
		//		{
		//			try
		//			{
		//				var result = _consumer!.Consume(stoppingToken);
		//				if (result.Message?.Value != null)
		//				{
		//					string topicName = result.Topic;
		//					string originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

		//					// 提取關鍵數據
		//					string relevantData = ExtractRelevantData(topicName, originalJsonString);

		//					// 存入對應的 MongoDB 集合
		//					await _mongoDBService.InsertMessageToCollectionAsync(topicName, originalJsonString);

		//					// 通過 SignalR 推送到前端
		//					await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, relevantData, stoppingToken);

		//					Console.WriteLine($"成功消費消息，Topic: {topicName}, Relevant Data: {relevantData}");
		//				}
		//			}
		//			catch (ConsumeException e)
		//			{
		//				Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
		//			}
		//			catch (Exception ex)
		//			{
		//				Console.WriteLine($"處理消息時發生錯誤: {ex.Message}");
		//			}
		//		}
		//	}
		//	catch (OperationCanceledException)
		//	{
		//		Console.WriteLine("消費服務已被取消");
		//	}
		//	finally
		//	{
		//		_consumer?.Close();
		//	}
		//}

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
								await _hubContext.Clients.All.SendAsync("ReceiveMessage", topicName, relevantData, stoppingToken);

								// 最後才印出日誌
								Console.WriteLine($"成功消費消息，Topic: {topicName}, Relevant Data: {relevantData}");
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


		//private string ExtractRelevantData(string topic, string jsonString)
		//{
		//	try
		//	{
		//		var json = JsonDocument.Parse(jsonString);

		//		if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
		//		{
		//			var messageBody = json.RootElement
		//								  .GetProperty("Data")
		//								  .GetProperty("Data")        // <-- 要多這一層
		//								  .GetProperty("RawData")
		//								  .GetProperty("MessageBody");


		//			// 提取數據
		//			var currentNowRYB = messageBody.GetProperty("CurrentNowRYB").EnumerateArray().Select(x => x.GetDouble()).ToArray();
		//			var powerNowRYB = messageBody.GetProperty("PowerNowRYB").EnumerateArray().Select(x => x.GetDouble()).ToArray();
		//			var energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

		//			return $"CurrentNowRYB: {string.Join(", ", currentNowRYB)}, PowerNowRYB: {string.Join(", ", powerNowRYB)}, EnergyUsed: {energyUsed}";
		//		}
		//		else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
		//		{
		//			var messageBody = json.RootElement
		//								  .GetProperty("Data")
		//								  .GetProperty("Data")        // <-- 要多這一層
		//								  .GetProperty("RawData")
		//								  .GetProperty("MessageBody");

		//			// 提取 ModifiedParameters 資料
		//			var modifiedParameters = messageBody.GetProperty("ModifiedParameters")
		//												.EnumerateArray()
		//												.Select(param => $"{param.GetProperty("Name").GetString()}: {param.GetProperty("Value").GetString()}")
		//												.ToArray();

		//			return string.Join("; ", modifiedParameters);
		//		}

		//		return "未知的 Topic";
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
		//		return "數據提取失敗";
		//	}
		//}


		//private string ExtractRelevantData(string topic, string jsonString)
		//{
		//	try
		//	{
		//		using var doc = JsonDocument.Parse(jsonString);
		//		var root = doc.RootElement;

		//		// 1) 先鎖定到 Data -> Data -> RawData
		//		JsonElement dataElement = root.GetProperty("Data").GetProperty("Data");
		//		JsonElement metaElement = dataElement.GetProperty("Meta");
		//		JsonElement rawDataElement = dataElement.GetProperty("RawData");

		//		// 2) 讀取 Source 或 MessageName，來判斷不同設備、不同用途
		//		string source = metaElement.GetProperty("Source").GetString() ?? "";
		//		string messageName = rawDataElement.GetProperty("MessageName").GetString() ?? "";

		//		// 3) 依照 Kafka Topic 進行大分類
		//		//    但其實可以直接看 messageName (因為 EnergyConsumed 對應 CFX.ResourcePerformance.EnergyConsumed)
		//		//    StationParametersModified 對應 CFX.ResourcePerformance.StationParametersModified
		//		if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed")
		//		{
		//			// 3.1) 讀取 MessageBody
		//			JsonElement messageBody = rawDataElement.GetProperty("MessageBody");

		//			// 取得三相電流、三相功率、用電量 (kWh)
		//			double[] currentNowRYB = messageBody.GetProperty("CurrentNowRYB")
		//												.EnumerateArray()
		//												.Select(x => x.GetDouble())
		//												.ToArray(); // [A, B, C]
		//			double[] powerNowRYB = messageBody.GetProperty("PowerNowRYB")
		//											  .EnumerateArray()
		//											  .Select(x => x.GetDouble())
		//											  .ToArray();  // [A, B, C]
		//			double energyUsed = messageBody.GetProperty("EnergyUsed").GetDouble();

		//			// 3.2) 依照不同 Source (Trough / Preheat / Power) 給予不同描述
		//			//      你可以把這些對應放在字典或 switch 也行
		//			string component = source switch
		//			{
		//				"CFX.A00.SO20050832.Trough" => "錫槽",
		//				"CFX.A00.SO20050832.Preheat" => "預熱",
		//				"CFX.A00.SO20050832.Power" => "總電源",
		//				_ => source  // 其餘維持原值
		//			};

		//			// 3.3) 組裝你想要的輸出字串
		//			//      例如： 錫槽三相電流(A): 5.6,5.8,5.8 / 有功功率(W): 679,770,727 / 用電量(kWh): 507.27
		//			//      下方只是範例，你可自行微調格式
		//			return $"{component} => 三相電流(A): {string.Join(", ", currentNowRYB)}, " +
		//				   $"有功功率(W): {string.Join(", ", powerNowRYB)}, " +
		//				   $"當天用電量(kWh): {energyUsed}";
		//		}
		//		else if (topic == "EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified")
		//		{
		//			// 3.4) 同樣先取得 MessageBody
		//			JsonElement messageBody = rawDataElement.GetProperty("MessageBody");

		//			// StationParametersModified 裡面會有 ModifiedParameters array
		//			var modifiedParameters = messageBody.GetProperty("ModifiedParameters")
		//												.EnumerateArray()
		//												.ToList();

		//			// 用來存放我們感興趣的參數
		//			// 例如 OV_MainTemperatureA, B, C / OV_PreheatTemperatureA, B, C / OV_TinBathTemperatureA, B, C / MSP_SwitchStatus1..3 等
		//			var dict = new Dictionary<string, string>();

		//			foreach (var param in modifiedParameters)
		//			{
		//				string name = param.GetProperty("Name").GetString() ?? "";
		//				string value = param.GetProperty("Value").GetString() ?? "";

		//				// 只要是我們感興趣的就加到 dict
		//				if (name.StartsWith("OV_MainTemperature") ||
		//					name.StartsWith("OV_PreheatTemperature") ||
		//					name.StartsWith("OV_TinBathTemperature") ||
		//					name.StartsWith("MSP_SwitchStatus") ||
		//					name.StartsWith("MSP_Temperature"))
		//				{
		//					dict[name] = value;
		//				}
		//			}

		//			// 3.5) 你可以把想要的溫度/開關狀態組裝起來
		//			//      例如：主開關 (A=xx°C, B=yy°C, C=zz°C), 預熱 (A=..)
		//			//      以下只是示範做法
		//			string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, " +
		//							  $"B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, " +
		//							  $"C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";

		//			string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, " +
		//								 $"B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, " +
		//								 $"C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";

		//			string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, " +
		//								 $"B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, " +
		//								 $"C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";

		//			string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, " +
		//								  $"S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, " +
		//								  $"S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

		//			// 3.6) 自行組合成想要的輸出
		//			return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
		//		}
		//		else
		//		{
		//			return "未知的 Topic";
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"資料提取時發生錯誤: {ex.Message}");
		//		return "數據提取失敗";
		//	}
		//}

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

		//// 解析 StationParametersModified 的方法
		//private string ParseStationParams(JsonElement root)
		//{
		//	// 1. 取得 RawData.MessageBody.ModifiedParameters (別忘了一層 Data->Data)
		//	var dataEl = root.GetProperty("Data").GetProperty("Data");
		//	var rawDataEl = dataEl.GetProperty("RawData");
		//	var messageBody = rawDataEl.GetProperty("MessageBody");
		//	var modifiedParams = messageBody.GetProperty("ModifiedParameters").EnumerateArray();

		//	// 2. 過濾出我們關心的參數
		//	//    （假設我們只關心 "OV_MainTemperatureX"、"OV_PreheatTemperatureX"、"OV_TinBathTemperatureX"、"MSP_SwitchStatusX" 等）
		//	//    可以用 Dictionary<string, string> 存放。若找不到，預設就不塞入。
		//	var dict = new Dictionary<string, string>();
		//	foreach (var param in modifiedParams)
		//	{
		//		string name = param.GetProperty("Name").GetString() ?? "";
		//		string value = param.GetProperty("Value").GetString() ?? "";

		//		// 只有符合需求的才收集
		//		if (name.StartsWith("OV_MainTemperature") ||
		//			name.StartsWith("OV_PreheatTemperature") ||
		//			name.StartsWith("OV_TinBathTemperature") ||
		//			name.StartsWith("MSP_SwitchStatus"))
		//		{
		//			dict[name] = value;
		//		}
		//	}

		//	// 3. 如果 dict 裡面完全沒包含我們想要的欄位，代表這則消息「不相關」，直接 return null 或空字串
		//	if (dict.Count == 0)
		//	{
		//		return null;  // null 代表這則不顯示
		//	}

		//	// 4. 若有資料，就跟之前一樣組裝成一段文字
		//	string mainTemp = $"主開關溫度(°C) => A:{dict.GetValueOrDefault("OV_MainTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_MainTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_MainTemperatureC", "-")}";
		//	string preheatTemp = $"預熱溫度(°C) => A:{dict.GetValueOrDefault("OV_PreheatTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_PreheatTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_PreheatTemperatureC", "-")}";
		//	string tinBathTemp = $"錫槽溫度(°C) => A:{dict.GetValueOrDefault("OV_TinBathTemperatureA", "-")}, B:{dict.GetValueOrDefault("OV_TinBathTemperatureB", "-")}, C:{dict.GetValueOrDefault("OV_TinBathTemperatureC", "-")}";
		//	string switchStatus = $"開關狀態 => S1:{dict.GetValueOrDefault("MSP_SwitchStatus1", "-")}, S2:{dict.GetValueOrDefault("MSP_SwitchStatus2", "-")}, S3:{dict.GetValueOrDefault("MSP_SwitchStatus3", "-")}";

		//	return $"{mainTemp} | {preheatTemp} | {tinBathTemp} | {switchStatus}";
		//}
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

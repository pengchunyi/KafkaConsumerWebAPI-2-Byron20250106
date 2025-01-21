using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using KafkaConsumerWebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using static Confluent.Kafka.ConfigPropertyNames;


using MongoDB.Bson;
using System.Text.Json;

namespace KafkaConsumerWebAPI.Services
{
	public class KafkaConsumerService : BackgroundService
	{
		private readonly ConcurrentQueue<string> _messageQueue;
		private readonly IConfiguration _configuration;
		private readonly IHubContext<MessageHub> _hubContext; // 新增 SignalR HubContext
		//250115新增=====================================
		private readonly MongoDBService _mongoDBService;


		public KafkaConsumerService(
			ConcurrentQueue<string> messageQueue, 
			IConfiguration configuration, 
			IHubContext<MessageHub> hubContext,
			MongoDBService mongoDBService
			)
		{
            _messageQueue = messageQueue;
			_configuration = configuration;
			_hubContext = hubContext; // 注入 HubContext
			//250115新增=====================================
			_mongoDBService = mongoDBService;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var kafkaConfig = _configuration.GetSection("Kafka");
			var adminConfig = new AdminClientConfig
			{
				BootstrapServers = kafkaConfig["BootstrapServers"],
				SecurityProtocol = SecurityProtocol.SaslPlaintext,
				SaslMechanism = SaslMechanism.Plain,
				SaslUsername = kafkaConfig["SaslUsername"],
				SaslPassword = kafkaConfig["SaslPassword"]
			};

			var consumerConfig = new ConsumerConfig
			{
				GroupId = kafkaConfig["GroupId"],
				BootstrapServers = kafkaConfig["BootstrapServers"],
				SecurityProtocol = SecurityProtocol.SaslPlaintext,
				SaslMechanism = SaslMechanism.Plain,
				SaslUsername = kafkaConfig["SaslUsername"],
				SaslPassword = kafkaConfig["SaslPassword"],
				AutoOffsetReset = AutoOffsetReset.Earliest,
				SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
				EnableAutoCommit = false

			};

			//250106_update===============================================
			// 定義兩個要訂閱的 Kafka topics
			var topics = new List<string>
			{
				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.EnergyConsumed",
				"EAP.DG2.IPS.I01.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified"
			};

			//250106_update===============================================

			consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
				.SetValueDeserializer(Deserializers.ByteArray)
				.Build();

			consumer.Subscribe(topics);

			Console.WriteLine($"已訂閱以下 Kafka Topics: {string.Join(", ", topics)}");

			taskA = Task.Run(() =>
			{
				test1(consumer, stoppingToken);
			});

        }
		IConsumer<Ignore, byte[]> consumer;
        Task taskA;

        private async void test1(IConsumer<Ignore, byte[]> consumer, CancellationToken stoppingToken)
		{
            try
            {

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // 這裡放入您的新增代碼，處理每次消費時的消息處理和錯誤記錄
                        var result = consumer.Consume(stoppingToken);
                        Console.WriteLine($"成功消費消息: {result.Message.Value}");

                        if (result.Message?.Value != null)
                        {
                            // 解碼消息
                            var decodedMessage = DecodeMessage(result.Message.Value);

                            // 緩存消息
                            var formattedMessage = $"Topic: {result.Topic}, Decoded Message: {decodedMessage}";
							// 解析 Kafka 消息的 JSON 格式
							//var kafkaMessageJson = JsonSerializer.Deserialize<JsonElement>(utf8Message);
							var originalJsonString = Encoding.UTF8.GetString(result.Message.Value);

							_messageQueue.Enqueue(formattedMessage);

                            Console.WriteLine(formattedMessage);

                            // 通過 SignalR 推送到前端
                            await _hubContext.Clients.All.SendAsync("ReceiveMessage", result.Topic, decodedMessage, stoppingToken);



							// 構造 MongoDB 插入資料
							//var mongoDocument = new BsonDocument
							//{
							//	{ "Topic", result.Topic },
							//	{ "Message", kafkaMessageJson }
							//};
							// 250115_將數據存入 MongoDB
							//await _mongoDBService.InsertMessageAsync(formattedMessage);
							// 插入資料
							//await _mongoDBService.InsertMessageAsync(mongoDocument);
							await _mongoDBService.InsertMessageAsync(originalJsonString);

						}
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Kafka 消費錯誤: {e.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"消費消息失敗: {ex.Message}");
                    }
					//250106_byron哥新增的=========================================
					Thread.SpinWait(10);
					//250106_byron哥新增的=========================================
				}



			}
            catch (OperationCanceledException)
            {
                Console.WriteLine("消費服務已被取消");
            }
            finally
            {
                consumer.Close();
            }
        }


		private string DecodeMessage(byte[] messageValue)
		{
			try
			{
				// 將 Byte 陣列轉為 UTF-8 字串
				string utf8Value = Encoding.UTF8.GetString(messageValue);

				// 嘗試解析 JSON
				var json = System.Text.Json.JsonDocument.Parse(utf8Value);

				// 用於儲存結果的 StringBuilder
				var resultBuilder = new System.Text.StringBuilder();

				// 遞迴方法，用於提取所有鍵值對
				void ExtractKeyValuePairs(JsonElement element, string prefix = "")
				{
					foreach (var property in element.EnumerateObject())
					{
						var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

						if (property.Value.ValueKind == JsonValueKind.Object)
						{
							// 如果值是物件，遞迴處理
							ExtractKeyValuePairs(property.Value, key);
						}
						else if (property.Value.ValueKind == JsonValueKind.Array)
						{
							// 如果值是陣列，逐一處理
							int index = 0;
							foreach (var arrayElement in property.Value.EnumerateArray())
							{
								if (arrayElement.ValueKind == JsonValueKind.Object)
								{
									ExtractKeyValuePairs(arrayElement, $"{key}[{index}]");
								}
								else
								{
									resultBuilder.AppendLine($"{key}[{index}]: {arrayElement}".Trim());
								}
								index++;
							}
						}
						else
						{
							// 如果值是基本類型，直接添加鍵值對
							resultBuilder.AppendLine($"{key}: {property.Value}".Trim());
						}
					}
				}

				// 從根開始提取鍵值對
				ExtractKeyValuePairs(json.RootElement);

				// 返回格式化結果
				return resultBuilder.ToString().Trim();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"解碼失敗: {ex.Message}");
				return "解碼失敗";
			}
		}





	}
}



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

namespace KafkaConsumerWebAPI.Services
{
	public class KafkaConsumerService : BackgroundService
	{
		private readonly ConcurrentQueue<string> _messageQueue;
		private readonly IConfiguration _configuration;
		private readonly IHubContext<MessageHub> _hubContext; // 新增 SignalR HubContext

		public KafkaConsumerService(ConcurrentQueue<string> messageQueue, IConfiguration configuration, IHubContext<MessageHub> hubContext)
		{
            _messageQueue = messageQueue;
			_configuration = configuration;
			_hubContext = hubContext; // 注入 HubContext
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
			// 指定要訂閱的特定 topic
			string specificTopic = "EAP.WJ3.CDP.D-ASSY-02.DEVICE_CFX.CFX.ResourcePerformance.StationParametersModified";
			//250106_update===============================================

			// 獲取所有 topics
			//List<string> topics;
			//using (var adminClient = new AdminClientBuilder(adminConfig).Build())
			//{
			//	var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
			//	topics = metadata.Topics.Select(t => t.Topic).ToList();
			//}



			//using var consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
			//	.SetValueDeserializer(Deserializers.ByteArray)
			//	.Build();
			consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
				.SetValueDeserializer(Deserializers.ByteArray)
				.Build();

			//consumer.Subscribe(topics);
			consumer.Subscribe(specificTopic);

			//Console.WriteLine("已訂閱所有 Kafka Topics");
			Console.WriteLine($"已訂閱特定 Kafka Topic: {specificTopic}");

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
                            _messageQueue.Enqueue(formattedMessage);

                            Console.WriteLine(formattedMessage);

                            // 通過 SignalR 推送到前端
                            await _hubContext.Clients.All.SendAsync("ReceiveMessage", result.Topic, decodedMessage, stoppingToken);
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
					//250106_哥新增的=========================================
					Thread.SpinWait(10);
					//250106_哥新增的=========================================
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


		//private string DecodeMessage(byte[] messageValue)
		//{
		//	try
		//	{
		//		// 嘗試以 UTF-8 解碼
		//		string utf8Value = Encoding.UTF8.GetString(messageValue);
		//		return utf8Value;
		//	}
		//	catch
		//	{
		//		// 如果 UTF-8 解碼失敗，嘗試其他格式
		//		string hexValue = BitConverter.ToString(messageValue);
		//		return $"Raw (Hex): {hexValue}";
		//	}
		//}

		private string DecodeMessage(byte[] messageValue)
		{
			try
			{
				string utf8Value = Encoding.UTF8.GetString(messageValue);

				// 嘗試解析 JSON
				var json = System.Text.Json.JsonDocument.Parse(utf8Value);
				var messageBody = json.RootElement.GetProperty("MessageBody");
				var modifiedParameters = messageBody.GetProperty("ModifiedParameters")[0];
				string name = modifiedParameters.GetProperty("Name").GetString();
				string value = modifiedParameters.GetProperty("Value").GetString();

				// 將提取的 Name 和 Value 作為格式化字符串返回
				return $"{name}:{value}";
			}
			catch (Exception ex)
			{
				Console.WriteLine($"解碼失敗: {ex.Message}");
				return "解碼失敗";
			}
		}



	}
}


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

//namespace KafkaConsumerWebAPI.Services
//{
//	public class KafkaConsumerService : BackgroundService
//	{
//		private readonly ConcurrentQueue<string> _messageQueue;
//		private readonly IConfiguration _configuration;
//		private readonly IHubContext<MessageHub> _hubContext; // SignalR HubContext

//		public KafkaConsumerService(ConcurrentQueue<string> messageQueue, IConfiguration configuration, IHubContext<MessageHub> hubContext)
//		{
//			_messageQueue = messageQueue;
//			_configuration = configuration;
//			_hubContext = hubContext; // 注入 HubContext
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
//				EnableAutoCommit = false
//			};

//			// 获取所有 topics
//			List<string> topics;
//			using (var adminClient = new AdminClientBuilder(adminConfig).Build())
//			{
//				var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
//				topics = metadata.Topics.Select(t => t.Topic).ToList();
//			}

//			using var consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
//				.SetValueDeserializer(Deserializers.ByteArray)
//				.Build();

//			consumer.Subscribe(topics);
//			Console.WriteLine("已订阅所有 Kafka Topics");

//			try
//			{
//				while (!stoppingToken.IsCancellationRequested)
//				{
//					try
//					{
//						var result = consumer.Consume(stoppingToken);

//						if (result.Message?.Value != null)
//						{
//							// 解码消息
//							string decodedMessage = DecodeMessage(result.Message.Value);

//							// 缓存消息
//							string formattedMessage = $"Topic: {result.Topic}, Decoded Message: {decodedMessage}";
//							_messageQueue.Enqueue(formattedMessage);

//							Console.WriteLine(formattedMessage);

//							// 推送到前端
//							await _hubContext.Clients.All.SendAsync("ReceiveMessage", result.Topic, decodedMessage, stoppingToken);
//						}
//					}
//					catch (ConsumeException e)
//					{
//						Console.WriteLine($"Kafka 消费错误: {e.Error.Reason}");
//					}
//				}
//			}
//			catch (OperationCanceledException)
//			{
//				Console.WriteLine("消费服务已被取消");
//			}
//			finally
//			{
//				consumer.Close();
//			}
//		}

//		private string DecodeMessage(byte[] messageValue)
//		{
//			try
//			{
//				// 将消息解码为 HEX 格式
//				string hexValue = BitConverter.ToString(messageValue);

//				// 尝试以 UTF-8 解码
//				try
//				{
//					string utf8Value = Encoding.UTF8.GetString(messageValue);
//					Console.WriteLine($"Decoded Value (UTF-8): {utf8Value}");
//					return utf8Value;
//				}
//				catch
//				{
//					Console.WriteLine("无法用 UTF-8 解码该消息。");
//				}

//				// 自定义解码逻辑
//				DecodeCustomMessage(messageValue);

//				return hexValue;
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"解码失败: {ex.Message}");
//				return "解码失败";
//			}
//		}

//		private void DecodeCustomMessage(byte[] messageValue)
//		{
//			try
//			{
//				// 示例：解码前4个字节为整数（大端序）
//				if (messageValue.Length >= 4)
//				{
//					int decodedInt = BitConverter.ToInt32(messageValue, 0);
//					Console.WriteLine($"Decoded Integer (前4字节): {decodedInt}");
//				}

//				// 示例：将消息转为 Base64
//				string base64Value = Convert.ToBase64String(messageValue);
//				Console.WriteLine($"Base64 Value: {base64Value}");
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"自定义解码失败: {ex.Message}");
//			}
//		}
//	}
//}







////本地數據模擬:
//using System.Collections.Concurrent;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using KafkaConsumerWebAPI.Hubs;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Hosting;

//namespace KafkaConsumerWebAPI.Services
//{
//	public class KafkaConsumerService : BackgroundService
//	{
//		private readonly ConcurrentQueue<string> _messageQueue;
//		private readonly IHubContext<MessageHub> _hubContext;

//		public KafkaConsumerService(ConcurrentQueue<string> messageQueue, IHubContext<MessageHub> hubContext)
//		{
//			_messageQueue = messageQueue;
//			_hubContext = hubContext;
//		}

//		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//		{
//			// 模擬 Kafka 消息
//			var topics = new[] { "Topic1", "Topic2", "Topic3" };

//			try
//			{
//				while (!stoppingToken.IsCancellationRequested)
//				{
//					foreach (var topic in topics)
//					{
//						// 模擬一條消息
//						var simulatedMessage = $"Message from {topic} at {DateTime.Now}";

//						// 緩存消息
//						_messageQueue.Enqueue(simulatedMessage);

//						// 推送到前端
//						await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", simulatedMessage);

//						// 日志輸出
//						Console.WriteLine($"Simulated Kafka Message: {simulatedMessage}");

//						// 模擬消息間隔
//						await Task.Delay(2000, stoppingToken);
//					}
//				}
//			}
//			catch (OperationCanceledException)
//			{
//				Console.WriteLine("消息生成服務已被取消");
//			}
//		}
//	}
//}

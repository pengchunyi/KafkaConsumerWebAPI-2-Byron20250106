using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
	private readonly ConcurrentQueue<string> _messageQueue;

	public MessagesController(ConcurrentQueue<string> messageQueue)
	{
		_messageQueue = messageQueue;
	}



	[HttpGet]
	public IActionResult GetMessages()
	{
		try
		{
			var messages = new List<string>();

			while (_messageQueue.TryDequeue(out var message))
			{
				messages.Add(message);
			}

			if (messages.Count == 0)
			{
				return NoContent(); // 返回 HTTP 204，表示沒有數據
			}

			return Ok(messages); // 返回 HTTP 200 和消息列表
		}
		catch (Exception ex)
		{
			Console.WriteLine($"錯誤: {ex.Message}");
			return StatusCode(500, "服務器內部錯誤");
		}
	}


	[HttpGet("parsed")]
	public IActionResult GetParsedMessages()
	{
		try
		{
			var messages = new List<string>();

			while (_messageQueue.TryDequeue(out var message))
			{
				messages.Add(message);
			}

			if (messages.Count == 0)
			{
				return NoContent();
			}

			return Ok(messages);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"錯誤: {ex.Message}");
			return StatusCode(500, "服務器內部錯誤");
		}
	}

}

//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;
//using System.Linq;

//namespace KafkaConsumerWebAPI.Controllers
//{
//	[Route("api/schedule")]
//	[ApiController]
//	public class ProductionScheduleController : ControllerBase
//	{
//		private readonly HttpClient _httpClient;

//		public ProductionScheduleController(HttpClient httpClient)
//		{
//			_httpClient = httpClient;
//		}

//		[HttpGet]
//		public async Task<IActionResult> GetSchedule([FromQuery] string date)
//		{
//			if (string.IsNullOrEmpty(date))
//			{
//				return BadRequest("請提供日期，例如 ?date=20241228");
//			}

//			//MES測試環境
//			//string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

//			//MES正式環境
//			string apiUrl = "http://10.148.192.37:10101/QueryData/procedure/GetEchelon";


//			var requestBody = new
//			{
//				FACTORY = "DG2",
//				DATA = new
//				{
//					PROD_AREA_CODE = "",
//					LINE_NAME = "S23",
//					DATE_FROM = date,
//					DATE_TO = date
//				}
//			};

//			string jsonContent = JsonConvert.SerializeObject(requestBody);
//			var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

//			_httpClient.DefaultRequestHeaders.Clear();
//			_httpClient.DefaultRequestHeaders.Add("tokenID", "299A964FF3C3D5A8E0630CCA940A1E93");

//			try
//			{
//				HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);
//				if (!response.IsSuccessStatusCode)
//				{
//					return StatusCode((int)response.StatusCode, "MES API 回應錯誤");
//				}

//				string responseString = await response.Content.ReadAsStringAsync();
//				JObject jsonResponse = JObject.Parse(responseString);

//				if (jsonResponse["Result"]?.ToString() != "OK")
//				{
//					return BadRequest("MES API 返回失敗");
//				}

//				var scheduleList = new List<object>();

//				foreach (var message in jsonResponse["Message"])
//				{
//					if (message["SECTION_DETAILS"] == null || !message["SECTION_DETAILS"].HasValues)
//						continue;

//					var sectionDetails = message["SECTION_DETAILS"]
//						.OrderBy(s => (string)s["SECTION_FROM"]) // 按時間排序
//						.ToList();

//					foreach (var section in sectionDetails)
//					{
//						// 基本欄位
//						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
//						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";

//						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
//						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;

//						string valid = section["VALID"]?.ToString() ?? "N";

//						// 休息欄位
//						string restFrom = section["REST_FROM"]?.ToString() ?? "";
//						string restTo = section["REST_TO"]?.ToString() ?? "";

//						// 若欄位不完整，跳過
//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue;

//						// -------------------------
//						// 無效班次 (VALID == "N")
//						// -------------------------
//						if (valid == "N")
//						{
//							scheduleList.Add(new
//							{
//								startTime = sectionFrom,
//								endTime = sectionTo,
//								duration = duration,
//								type = "R"
//							});
//							continue;
//						}

//						// -------------------------
//						// 有效班次 (VALID == "Y")
//						// -------------------------
//						if (!string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo))
//						{
//							// 1. 休息時間段
//							scheduleList.Add(new
//							{
//								startTime = restFrom,
//								endTime = restTo,
//								duration = duration - workTime, // 休息時間
//								type = "R"
//							});

//							// 2. 上班時間（休息後開始工作）
//							scheduleList.Add(new
//							{
//								startTime = restTo,
//								endTime = sectionTo,
//								duration = workTime, // 生產時長
//								type = "P"
//							});
//						}
//						else
//						{
//							// 如果沒有休息時間，只顯示上班時間
//							scheduleList.Add(new
//							{
//								startTime = sectionFrom,
//								endTime = sectionTo,
//								duration = duration,
//								type = "P"
//							});
//						}
//					}
//				}

//				if (scheduleList.Count == 0)
//				{
//					return Ok(new { Message = "無數據" });
//				}

//				return Ok(scheduleList);
//			}
//			catch (HttpRequestException ex)
//			{
//				return StatusCode(500, $"請求 MES API 失敗: {ex.Message}");
//			}
//			catch (JsonException ex)
//			{
//				return StatusCode(500, $"JSON 解析失敗: {ex.Message}");
//			}
//		}
//	}
//}


using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace KafkaConsumerWebAPI.Controllers
{
	[Route("api/schedule")]
	[ApiController]
	public class ProductionScheduleController : ControllerBase
	{
		private readonly HttpClient _httpClient;

		public ProductionScheduleController(HttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		[HttpGet]
		public async Task<IActionResult> GetSchedule([FromQuery] string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return BadRequest("請提供日期，例如 ?date=20241228");
			}

			// MES 環境選擇
			string apiUrl = "http://10.148.192.37:10101/QueryData/procedure/GetEchelon";

			var requestBody = new
			{
				FACTORY = "DG2",
				DATA = new
				{
					PROD_AREA_CODE = "",
					LINE_NAME = "S23",
					DATE_FROM = date,
					DATE_TO = date
				}
			};

			string jsonContent = JsonConvert.SerializeObject(requestBody);
			var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

			_httpClient.DefaultRequestHeaders.Clear();
			_httpClient.DefaultRequestHeaders.Add("tokenID", "299A964FF3C3D5A8E0630CCA940A1E93");

			try
			{
				HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);
				if (!response.IsSuccessStatusCode)
				{
					return StatusCode((int)response.StatusCode, "MES API 回應錯誤");
				}

				string responseString = await response.Content.ReadAsStringAsync();
				JObject jsonResponse = JObject.Parse(responseString);

				if (jsonResponse["Result"]?.ToString() != "OK")
				{
					return BadRequest("MES API 返回失敗");
				}

				var scheduleList = new List<object>();

				foreach (var message in jsonResponse["Message"])
				{
					if (message["SECTION_DETAILS"] == null || !message["SECTION_DETAILS"].HasValues)
						continue;

					var sectionDetails = message["SECTION_DETAILS"]
						.OrderBy(s => (string)s["SECTION_FROM"]) // 按時間排序
						.ToList();

					foreach (var section in sectionDetails)
					{
						// 解析基本欄位
						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;
						string valid = section["VALID"]?.ToString() ?? "N";
						string restFrom = section["REST_FROM"]?.ToString();
						string restTo = section["REST_TO"]?.ToString();

						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
							continue;

						int startMinutes = ConvertToMinutes(sectionFrom);
						int endMinutes = ConvertToMinutes(sectionTo);

						if (endMinutes <= startMinutes) endMinutes += 1440; // 處理跨日問題

						if (valid == "N")
						{
							scheduleList.Add(new
							{
								startTime = FormatTime(startMinutes),
								endTime = FormatTime(endMinutes),
								duration = duration,
								type = "R" // "R" 代表休息
							});
							continue;
						}

						// 處理休息時間拆分
						if (!string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo))
						{
							int restStart = ConvertToMinutes(restFrom);
							int restEnd = ConvertToMinutes(restTo);

							if (restEnd <= restStart) restEnd += 1440;

							if (startMinutes < restStart)
							{
								scheduleList.Add(new
								{
									startTime = FormatTime(startMinutes),
									endTime = FormatTime(restStart),
									duration = restStart - startMinutes,
									type = "P" // "P" 代表生產
								});
							}

							scheduleList.Add(new
							{
								startTime = FormatTime(restStart),
								endTime = FormatTime(restEnd),
								duration = restEnd - restStart,
								type = "R"
							});

							if (restEnd < endMinutes)
							{
								scheduleList.Add(new
								{
									startTime = FormatTime(restEnd),
									endTime = FormatTime(endMinutes),
									duration = endMinutes - restEnd,
									type = "P"
								});
							}
						}
						else
						{
							scheduleList.Add(new
							{
								startTime = FormatTime(startMinutes),
								endTime = FormatTime(endMinutes),
								duration = duration,
								type = "P"
							});
						}
					}
				}

				if (scheduleList.Count == 0)
				{
					return Ok(new { Message = "無數據" });
				}

				return Ok(scheduleList);
			}
			catch (HttpRequestException ex)
			{
				return StatusCode(500, $"請求 MES API 失敗: {ex.Message}");
			}
			catch (JsonException ex)
			{
				return StatusCode(500, $"JSON 解析失敗: {ex.Message}");
			}
		}

		private int ConvertToMinutes(string time)
		{
			if (string.IsNullOrEmpty(time)) return -1;
			if (time == "2400") return 1440;
			return int.Parse(time.Substring(0, 2)) * 60 + int.Parse(time.Substring(2, 2));
		}

		private string FormatTime(int minutes)
		{
			minutes %= 1440;
			return $"{minutes / 60:D2}:{minutes % 60:D2}";
		}
	}
}

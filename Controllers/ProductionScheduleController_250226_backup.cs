//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;

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
//		public async Task<IActionResult> GetSchedule()
//		{
//			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

//			var requestBody = new
//			{
//				FACTORY = "DG2",
//				DATA = new
//				{
//					PROD_AREA_CODE = "",
//					LINE_NAME = "S23",
//					DATE_FROM = "20241228",
//					DATE_TO = "20241228"
//				}
//			};

//			string jsonContent = JsonConvert.SerializeObject(requestBody);
//			var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

//			// 設置請求標頭
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

//				var scheduleList = new List<object>(); // 確保是 **非空** 陣列

//				foreach (var message in jsonResponse["Message"])
//				{
//					if (message["SECTION_DETAILS"] == null || !message["SECTION_DETAILS"].HasValues)
//					{
//						continue;
//					}

//					foreach (var section in message["SECTION_DETAILS"])
//					{
//						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
//						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
//						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
//						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;

//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue; // 避免空數據導致無效輸出

//						scheduleList.Add(new
//						{
//							StartTime = sectionFrom,
//							EndTime = sectionTo,
//							Duration = duration,
//							Type = workTime > 0 ? "P" : "R" // P=生產, R=休息
//						});
//					}
//				}

//				return Ok(scheduleList.Count > 0 ? scheduleList : new { Message = "無數據" });
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


//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;

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

//			// 這裡假設傳入的 date 格式為 "20241228"
//			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

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
//					{
//						continue;
//					}

//					foreach (var section in message["SECTION_DETAILS"])
//					{
//						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
//						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
//						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
//						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;

//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue;

//						scheduleList.Add(new
//						{
//							startTime = sectionFrom,
//							endTime = sectionTo,
//							duration = duration,
//							type = workTime > 0 ? "P" : "R" // 生產 or 休息
//						});
//					}
//				}

//				return Ok(scheduleList.Count > 0 ? scheduleList : new { Message = "無數據" });
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

//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;

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

//			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

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

//					foreach (var section in message["SECTION_DETAILS"])
//					{
//						// 基本欄位
//						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
//						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
//						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
//						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;

//						// 休息欄位
//						string restFrom = section["REST_FROM"]?.ToString();
//						string restTo = section["REST_TO"]?.ToString();
//						bool hasRest = !string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo)
//									   && restFrom != "null" && restTo != "null";

//						// 若必填欄位都空，就跳過
//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue;

//						// -------------------------
//						// 核心邏輯：依 workTime 判斷
//						// -------------------------
//						if (workTime == 0)
//						{
//							// ❗ workTime=0 => 直接用 restFrom, restTo 做整段休息
//							//   若 restFrom, restTo 都存在
//							if (hasRest)
//							{
//								scheduleList.Add(new
//								{
//									startTime = restFrom,
//									endTime = restTo,
//									duration = duration, // 整段都休息
//									type = "R"
//								});
//							}
//							else
//							{
//								// 若沒有 REST_FROM/REST_TO, 就以 SECTION_FROM ~ SECTION_TO 當作休息
//								scheduleList.Add(new
//								{
//									startTime = sectionFrom,
//									endTime = sectionTo,
//									duration = duration,
//									type = "R"
//								});
//							}
//						}
//						else
//						{
//							// ❗ workTime≠0 => 舊的拆分邏輯 (生產 + 休息)
//							if (hasRest)
//							{
//								// 生產行
//								scheduleList.Add(new
//								{
//									startTime = sectionFrom,
//									endTime = restFrom,
//									duration = workTime, // 生產時長
//									type = "P"
//								});

//								// 休息行
//								int restDuration = duration - workTime;
//								if (restDuration > 0)
//								{
//									scheduleList.Add(new
//									{
//										startTime = restFrom,
//										endTime = restTo,
//										duration = restDuration,
//										type = "R"
//									});
//								}
//							}
//							else
//							{
//								// 沒有休息 => 單行 (生產)
//								scheduleList.Add(new
//								{
//									startTime = sectionFrom,
//									endTime = sectionTo,
//									duration = workTime,
//									type = "P"
//								});
//							}
//						}
//					}
//				}

//				if (scheduleList.Count == 0)
//					return Ok(new { Message = "無數據" });

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


//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;

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

//			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

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

//					foreach (var section in message["SECTION_DETAILS"])
//					{
//						// 基本欄位
//						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
//						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
//						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
//						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;
//						string valid = section["VALID"]?.ToString() ?? "N";

//						// 休息欄位
//						string restFrom = section["REST_FROM"]?.ToString();
//						string restTo = section["REST_TO"]?.ToString();
//						bool hasRest = !string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo)
//									   && restFrom != "null" && restTo != "null";

//						// 若必填欄位都空，就跳過
//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue;

//						// -------------------------
//						// 核心邏輯：依 WORK_TIME 和 VALID 判斷
//						// -------------------------
//						if (valid == "N")
//						{
//							// 如果無效班次：直接顯示 1 行
//							scheduleList.Add(new
//							{
//								startTime = sectionFrom,
//								endTime = sectionTo,
//								duration = duration,
//								type = "休息"
//							});


//						}
//						else
//						{
//							// 若有有效班次，拆成生產和休息兩行
//							if (workTime > 0)
//							{
//								// 生產行
//								scheduleList.Add(new
//								{
//									//startTime = sectionFrom,
//									//endTime = restFrom ?? sectionTo,

//									startTime = restTo,
//									endTime = sectionTo,
//									duration = workTime,
//									type = "生產"
//								});

//							}
//							// 休息行
//							//if (hasRest)
//							//{
//							//這個都會是
//							int restDuration = duration - workTime;
//								//if (restDuration > restDuration)
//								//{
//									scheduleList.Add(new
//										{
//											startTime = restFrom,
//											endTime = restTo,
//											duration = restDuration,
//											type = "休息"
//										});
//								//}
//								//}

//							//else{
//							//	// 如果 WORK_TIME 是 0：直接用 REST_FROM 和 REST_TO 作為休息時間
//							//	scheduleList.Add(new
//							//	{
//							//		startTime = restFrom,
//							//		endTime = restTo,
//							//		duration = duration,
//							//		type = "休息"
//							//	});
//							//}
//						}
//					}
//				}

//				return Ok(scheduleList.Count > 0 ? scheduleList : new { Message = "無數據" });
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


//using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using System.Collections.Generic;

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

//			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

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

//					foreach (var section in message["SECTION_DETAILS"])
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

//						// 若必填欄位都空，就跳過
//						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
//							continue;

//						// -------------------------
//						// 無效班次 (VALID == "N")
//						// -------------------------
//						if (valid == "N")
//						{
//							// 只顯示一行，標記為「無效」
//							scheduleList.Add(new
//							{
//								startTime = sectionFrom,
//								endTime = sectionTo,
//								duration = duration,
//								type = "無效"
//							});
//							continue;
//						}

//						// -------------------------
//						// 有效班次 (VALID == "Y")
//						// -------------------------
//						if (workTime > 0)
//						{
//							// 生產行
//							scheduleList.Add(new
//							{
//								startTime = sectionFrom,
//								endTime = restFrom,      // 這裡表示生產結束於休息開始
//								duration = workTime,      // 直接使用 WORK_TIME
//								type = "生產"
//							});

//							// 休息行
//							int restDuration = duration - workTime;
//							if (!string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo) && restDuration > 0)
//							{
//								scheduleList.Add(new
//								{
//									startTime = restFrom,
//									endTime = restTo,
//									duration = restDuration, // DURATION - WORK_TIME
//									type = "休息"
//								});
//							}
//						}
//						else
//						{
//							// WORK_TIME == 0
//							// 整段視為休息
//							// 若 REST_FROM/REST_TO 有值，直接使用
//							if (!string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo))
//							{
//								scheduleList.Add(new
//								{
//									startTime = restFrom,
//									endTime = restTo,
//									duration = duration,
//									type = "休息"
//								});
//							}
//							else
//							{
//								// 若沒有 restFrom / restTo，就用 SECTION_FROM ~ SECTION_TO
//								scheduleList.Add(new
//								{
//									startTime = sectionFrom,
//									endTime = sectionTo,
//									duration = duration,
//									type = "休息"
//								});
//							}
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

			string apiUrl = "http://10.148.200.28:10101/QueryData/procedure/GetEchelon";

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
						// 基本欄位
						string sectionFrom = section["SECTION_FROM"]?.ToString() ?? "";
						string sectionTo = section["SECTION_TO"]?.ToString() ?? "";
						int duration = section["DURATION"]?.ToObject<int>() ?? 0;
						int workTime = section["WORK_TIME"]?.ToObject<int>() ?? 0;
						string valid = section["VALID"]?.ToString() ?? "N";

						// 休息欄位
						string restFrom = section["REST_FROM"]?.ToString() ?? "";
						string restTo = section["REST_TO"]?.ToString() ?? "";

						// 若欄位不完整，跳過
						if (string.IsNullOrEmpty(sectionFrom) || string.IsNullOrEmpty(sectionTo))
							continue;

						// -------------------------
						// 無效班次 (VALID == "N")
						// -------------------------
						if (valid == "N")
						{
							scheduleList.Add(new
							{
								startTime = sectionFrom,
								endTime = sectionTo,
								duration = duration,
								type = "R"
							});
							continue;
						}

						// -------------------------
						// 有效班次 (VALID == "Y")
						// -------------------------
						if (!string.IsNullOrEmpty(restFrom) && !string.IsNullOrEmpty(restTo))
						{
							// 1. 休息時間段
							scheduleList.Add(new
							{
								startTime = restFrom,
								endTime = restTo,
								duration = duration - workTime, // 休息時間
								type = "R"
							});

							// 2. 上班時間（休息後開始工作）
							scheduleList.Add(new
							{
								startTime = restTo,
								endTime = sectionTo,
								duration = workTime, // 生產時長
								type = "P"
							});
						}
						else
						{
							// 如果沒有休息時間，只顯示上班時間
							scheduleList.Add(new
							{
								startTime = sectionFrom,
								endTime = sectionTo,
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
	}
}

//using System.Collections.Concurrent;
//using KafkaConsumerWebAPI.Hubs;
//using KafkaConsumerWebAPI.Services;

//var builder = WebApplication.CreateBuilder(args);

//builder.WebHost.UseUrls("http://localhost:5000");

//// 註冊服務
//builder.Services.AddControllers();

////250115_這是註冊mongoDB======================================
//builder.Services.AddSingleton<MongoDBService>();


////250103新增=====================
//builder.Services.AddSignalR(); // 添加 SignalR 支持
////250103新增=====================
//builder.Services.AddSingleton<ConcurrentQueue<string>>(); // 緩存 Kafka 消息
//builder.Services.AddHostedService<KafkaConsumerService>(); // Kafka 消費者服務


////250103新增=====================
//// 添加 CORS 配置
//builder.Services.AddCors(options =>
//{
//	options.AddDefaultPolicy(builder =>
//	{
//		builder.AllowAnyOrigin()
//			   .AllowAnyMethod()
//			   .AllowAnyHeader();
//	});
//});
////250103新增=====================


//var app = builder.Build();
////250103新增=====================

////===================================================================
//app.UseDefaultFiles();
//// 配置靜態文件支持
//app.UseStaticFiles(); // 允許服務靜態文件
////250103新增=====================


//// 使用 CORS
//app.UseCors(); // 必須在 UseRouting() 之前調用

//// 配置路由
//app.UseRouting();
////app.MapControllers();

////20250110新增================================================
//// 設置登入頁的預設路徑
//app.Use(async (context, next) =>
//{
//	// 檢查是否是根路徑並跳轉到 login.html
//	if (context.Request.Path == "/")
//	{
//		context.Response.Redirect("/login.html");
//		return;
//	}
//	await next();
//});
////20250110新增================================================


////250103新增=====================
//app.UseEndpoints(endpoints =>
//{
//	endpoints.MapControllers(); // API 路由
//	endpoints.MapHub<MessageHub>("/hub/messageHub"); // SignalR Hub 路由
//});

////250103新增=====================

////上面這一個是用來給其他人連上我的主機的設定
////app.Run("http://10.149.120.37:5000");

//app.Run();





using System.Collections.Concurrent;
using KafkaConsumerWebAPI.Hubs;
using KafkaConsumerWebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");




//250224_update====================================================================
// 註冊 HttpClient（解決 `HttpClient` 無法解析的錯誤）
builder.Services.AddHttpClient();

// ✅ 註冊服務
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // 確保 API 端點可用
builder.Services.AddSwaggerGen(); // Swagger 文檔 (可測試 API)

// ✅ 註冊 MongoDB
//builder.Services.AddSingleton<MongoDBService>();
// ✅ 註冊 MongoDB 並初始化
builder.Services.AddSingleton<MongoDBService>(provider =>
{
	string mongoConnectionString = "mongodb://localhost:27017"; // 你的 MongoDB 連線字串
	string databaseName = "KafkaMessagesDB"; // 替換為你的 MongoDB 資料庫名稱
	return new MongoDBService(mongoConnectionString, databaseName);
});




// ✅ 註冊 Kafka 消費者
builder.Services.AddSingleton<ConcurrentQueue<string>>(); // 緩存 Kafka 消息
builder.Services.AddHostedService<KafkaConsumerService>(); // Kafka 消費者服務

// ✅ 註冊 SignalR
builder.Services.AddSignalR(); // 用於即時推送數據

// ✅ 啟用 CORS
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(builder =>
	{
		builder.AllowAnyOrigin()
			   .AllowAnyMethod()
			   .AllowAnyHeader();
	});
});

var app = builder.Build();

// ✅ 啟用 Swagger (開發模式)
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// ✅ 啟用靜態文件 (確保 `daily_energy_usage.html` 可以被加載)
app.UseDefaultFiles();
app.UseStaticFiles();

// ✅ 啟用 CORS
app.UseCors();

// ✅ 設置 API 路由
app.UseRouting();
app.MapControllers(); // 🔥 這行很重要，確保 API 能夠運行

// ✅ 設置登入頁面自動跳轉
app.Use(async (context, next) =>
{
	if (context.Request.Path == "/")
	{
		context.Response.Redirect("/login.html");
		return;
	}
	await next();
});

// ✅ 啟用 SignalR Hub
app.UseEndpoints(endpoints =>
{
	endpoints.MapControllers();
	endpoints.MapHub<MessageHub>("/hub/messageHub");
});

// ✅ 啟動應用
app.Run();

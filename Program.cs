using System.Collections.Concurrent;
using KafkaConsumerWebAPI.Hubs;
using KafkaConsumerWebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");

// 註冊服務
builder.Services.AddControllers();

//250103新增=====================
builder.Services.AddSignalR(); // 添加 SignalR 支持
//250103新增=====================
builder.Services.AddSingleton<ConcurrentQueue<string>>(); // 緩存 Kafka 消息
builder.Services.AddHostedService<KafkaConsumerService>(); // Kafka 消費者服務


//250103新增=====================
// 添加 CORS 配置
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(builder =>
	{
		builder.AllowAnyOrigin()
			   .AllowAnyMethod()
			   .AllowAnyHeader();
	});
});
//250103新增=====================




var app = builder.Build();
//250103新增=====================

//===================================================================
app.UseDefaultFiles();
// 配置靜態文件支持
app.UseStaticFiles(); // 允許服務靜態文件
//250103新增=====================


// 使用 CORS
app.UseCors(); // 必須在 UseRouting() 之前調用

// 配置路由
app.UseRouting();
//app.MapControllers();

//20250110新增================================================
// 設置登入頁的預設路徑
app.Use(async (context, next) =>
{
	// 檢查是否是根路徑並跳轉到 login.html
	if (context.Request.Path == "/")
	{
		context.Response.Redirect("/login.html");
		return;
	}
	await next();
});
//20250110新增================================================


//250103新增=====================
app.UseEndpoints(endpoints =>
{
	endpoints.MapControllers(); // API 路由
	endpoints.MapHub<MessageHub>("/hub/messageHub"); // SignalR Hub 路由
});

//250103新增=====================


app.Run();

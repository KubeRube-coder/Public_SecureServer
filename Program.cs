using Microsoft.EntityFrameworkCore;
using SecureServer.data;
using SecureServer.Data;
using SecureServer.Middleware;
using Serilog;
using Serilog.Events;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Секретики

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Sink(new LogSocketHandler(null))
    .Enrich.FromLogContext()
    .Filter.ByExcluding(log => log.Properties.ContainsKey("SourceContext") &&
                               log.Properties["SourceContext"].ToString().Contains("Microsoft.EntityFrameworkCore"))
    .CreateLogger();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"), // ������������ � ��
        new MySqlServerVersion(new Version(8, 0, 32))
    )
);

builder.Services.AddScoped<BalanceHandler>();

builder.Host.UseSerilog();

builder.Services.AddHostedService<DailyTaskRefresher>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

var app = builder.Build();

var portsArg = args.FirstOrDefault(a => a.StartsWith("-ports:"));
if (portsArg != null && int.TryParse(portsArg.Replace("-ports:", ""), out var port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}
else
{
    app.Urls.Add("http://0.0.0.0:5000"); // fallback
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    );

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("It seems there was an error. Contact us: https://discord.gg/qqXKhxAYAE");
    });
});

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering(); // ��������� ������ ���� ������� ��������
    Console.WriteLine("----------------------NEW-BLOCK----------------------");

    if (context.Request.Path.Value.StartsWith("/api/upload") || context.Request.Path.Value.StartsWith("/api/update"))
    {
        var contentType = context.Request.ContentType ?? "";

        if (contentType.Contains("application/json") || contentType.Contains("text/"))
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            Console.WriteLine($"{context.Request.Path} � Request Body:\n{body}");
        }
        else
        {
            Console.WriteLine($"{context.Request.Path} � [Request Body Skipped: {context.Request.ContentType}]");
        }
    }

    await next.Invoke();
    Console.WriteLine("----------------------END-BLOCK----------------------");
});


app.UseRouting();
app.UseWebSockets();

app.UseEndpoints(endpoints =>
{
    endpoints.Map("ws/log", async context =>
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var socket = await context.WebSockets.AcceptWebSocketAsync();

            LogSocketHandler.ConnectedSockets.Add(socket);

            foreach (var line in LogSocketHandler.LogBuffer)
            {
                var buffer = Encoding.UTF8.GetBytes(line);
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            var bufferSize = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(bufferSize), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
            }

            LogSocketHandler.ConnectedSockets.Remove(socket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    });
});

app.UseMiddleware<TokenValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

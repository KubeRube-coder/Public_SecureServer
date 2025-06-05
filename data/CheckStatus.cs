using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Management.Infrastructure;
using System.Diagnostics;
using System.Threading;


namespace SecureServer.Data
{
    [ApiController]
    [Route("api/check")]
    public class CheckStatus : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;

        public CheckStatus(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var report = await _healthCheckService.CheckHealthAsync();
            var status = report.Status.ToString();
            var details = report.Entries.Select(e => new
            {
                Component = e.Key,
                Status = e.Value.Status.ToString(),
                avgRespone = report.TotalDuration.TotalMilliseconds.ToString() + " ms",
                Description = e.Value.Description
            });

            return Ok(new
            {
                OverallStatus = status,
                Components = details
            });
        }

        // Эндпоинт для отправки данных о загруженности
        [HttpGet("load")]
        public IActionResult GetLoad()
        {
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();

            // Возвращаем данные о загруженности
            return Ok(new
            {
                cpuUsage,
                memoryUsage
            });
        }

        private double GetCpuUsage()
        {
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            {
                cpuCounter.NextValue(); // Первый вызов всегда 0
                Thread.Sleep(1000); // Подождать, чтобы получить реальное значение
                return Math.Round(cpuCounter.NextValue(), 2); // Возвращаем значение с округлением
            }
        }

        private double GetMemoryUsage()
        {
            var session = CimSession.Create(null); // локальная сессия
            var memoryInfo = session.QueryInstances(@"root\cimv2", "WQL",
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem")
                .FirstOrDefault();

            if (memoryInfo != null)
            {
                ulong total = (ulong)memoryInfo.CimInstanceProperties["TotalVisibleMemorySize"].Value;
                ulong free = (ulong)memoryInfo.CimInstanceProperties["FreePhysicalMemory"].Value;
                ulong used = total - free;

                return Math.Round((double)used / total * 100, 2); // % загруженности
            }

            return 0;
        }


    [HttpGet("help")]
        public async Task<IActionResult> help()
        {
            // Test request
            return Ok(new
            {
                help = "Данный запрос существует для ознакомления с ошибками",
                statusCode400 = "Сервер не может или не будет обрабатывать запрос из-за чего-то, что воспринимается как ошибка клиента (например, неправильный синтаксис, формат или маршрутизация запроса).",
                statusCode401 = "Сервер получил неправильные данные для авторизации.",
                statusCode403 = "Клиент не имеет права доступа к запрашиваемому контенту.",
                statusCode404 = "Сервер не может найти запрашиваемый ресурс",
                statusCode418 = "Шутка. Сервер отклоняет попытку заварить кофе в чайнике :))",
                statusCode429 = "Клиент отправил слишком много запросов в определённый промежуток времени. (Запросы будут заблокированы на некоторое время)",
                statusCode500 = "Внутренняя ошибка сервера. В основном проблема с базой данных"
            });
        }
    }
}

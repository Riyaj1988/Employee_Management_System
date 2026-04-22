using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using System.Net.Http.Json;
using Shared.Utilities;

namespace Shared.Logging
{

    public class CentralLogger : ILogger
    {
        private static readonly HttpClient Http = new(); // One courier for the whole app
        private readonly string _category;
        private readonly IHttpContextAccessor _access;
        private readonly string _service;
        private readonly string _url;

        public CentralLogger(string category, IHttpContextAccessor access, string service, string url)
        {
            _category = category;
            _access = access;
            _service = service;
            _url = url;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Log Application Information, but only Warning/Error for System/Infra logs
        public bool IsEnabled(LogLevel level)
        {
            // Suppress Information logs for internal infrastructure to keep the DB clean
            if (_category.StartsWith("Microsoft") || 
                _category.StartsWith("MassTransit") || 
                _category.StartsWith("System"))
            {
                return level >= LogLevel.Warning;
            }

            return level >= LogLevel.Information;
        }



        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
        {
            if (!IsEnabled(level)) return;

            var log = new LogEntryDto
            {
                ServiceName = _service,
                LogLevel = level.ToString(),
                Message = $"[{_category}] {fmt(state, ex)}",
                Exception = ex?.ToString(),
                CorrelationId = _access.HttpContext?.Request.Headers["X-Correlation-Id"].ToString(),
                UserName = _access.HttpContext?.User?.Identity?.Name 
                    ?? _access.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? _access.HttpContext?.User?.FindFirst("sub")?.Value,
                Timestamp = TimeHelper.GetIstNow()
            };

            // Send in the background without blocking the app
            _ = Task.Run(() => Http.PostAsJsonAsync($"{_url.TrimEnd('/')}/logs", log));
        }
    }


    public class CentralLoggerProvider : ILoggerProvider
    {
        private readonly IHttpContextAccessor _access;
        private readonly string _service;
        private readonly string _url;

        public CentralLoggerProvider(IHttpContextAccessor access, string service, string url)
        {
            _access = access;
            _service = service;
            _url = url;
        }

        public ILogger CreateLogger(string category) => new CentralLogger(category, _access, _service, _url);
        public void Dispose() { }
    }

    public static class CentralLoggerExtensions
    {
        public static ILoggingBuilder AddCentralLogger(this ILoggingBuilder builder, string serviceName, string loggingUrl)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<ILoggerProvider>(sp =>
                new CentralLoggerProvider(sp.GetRequiredService<IHttpContextAccessor>(), serviceName, loggingUrl));
            return builder;
        }
    }
}

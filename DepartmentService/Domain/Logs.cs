using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net;

namespace DepartmentService.Domain
{
    public class Logs : ILogs
    {
        private readonly HttpClient _httpClient;
        public Logs(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendLogAsync(object log)
        {
            await _httpClient.PostAsJsonAsync("/logs", log);
        }
    }

    public interface ILogs
    {
        Task SendLogAsync(object log);
    }
}

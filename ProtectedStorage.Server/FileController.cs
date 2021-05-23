using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProtectedStorage.Server
{
    [ApiController]
    public class FileController : ControllerBase
    {
        private static readonly object _lock = new();
        private static DateTimeOffset? _lastInvalidPasswordTime;

        private readonly IConfiguration _configuration;
        private readonly ILogger<FileController> _logger;

        public FileController(IConfiguration configuration, ILogger<FileController> logger)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPut("file")]
        public async Task<IActionResult> PutFile()
        {
            if (await TryAuthenticate(upload: true) is ObjectResult errorResult)
            {
                return errorResult;
            }

            if (!TryGetSetting("FilePath", out var filePath, out var maybeErrorResult))
            {
                return maybeErrorResult;
            }

            if (Path.GetDirectoryName(filePath) is string path && path.Length > 0)
            {
                Directory.CreateDirectory(path);
            }

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            using var stream = System.IO.File.OpenWrite(filePath);
            await Request.Body.CopyToAsync(stream);

            await SendNotifications("File has been updated.");

            return NoContent();
        }

        [HttpGet("file")]
        public async Task<IActionResult> GetFile()
        {
            if (await TryAuthenticate(upload: false) is ObjectResult errorResult)
            {
                return errorResult;
            }

            if (!TryGetSetting("FilePath", out var filePath, out var maybeErrorResult))
            {
                return maybeErrorResult;
            }

            if (!System.IO.File.Exists(filePath))
            {
                return BadRequest("File not found.");
            }

            await SendNotifications("Serving file...");

            return File(System.IO.File.OpenRead(filePath), "application/octet-stream");
        }

        private async Task<ObjectResult?> TryAuthenticate(bool upload)
        {
            if (!Request.Headers.TryGetValue("Authorization", out var value))
            {
                return Unauthorized("No Authorization header specified.");
            }

            if (!TryGetSetting(upload ? "UploadPassword" : "DownloadPassword", out var password, out var errorResult))
            {
                return errorResult;
            }

            lock (_lock)
            {
                if (_lastInvalidPasswordTime is DateTimeOffset lastTime)
                {
                    var timeLeft = lastTime.AddMinutes(5).Subtract(DateTimeOffset.Now);

                    if (timeLeft > TimeSpan.Zero)
                    {
                        return BadRequest($"Please wait {timeLeft.TotalSeconds} seconds.");
                    }
                }
            }

            if (value != password)
            {
                lock (_lock)
                {
                    _lastInvalidPasswordTime = DateTimeOffset.Now;
                }

                await SendNotifications($"An invalid {(upload ? "upload" : "download")} password has been submitted.");

                return Unauthorized($"Invalid {(upload ? "upload" : "download")} password.");
            }

            return null;
        }

        private bool TryGetSetting(string key, [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out ObjectResult? errorResult)
        {
            if (_configuration.GetValue<string>(key) is string foundValue)
            {
                value = foundValue;
                errorResult = null;

                return true;
            }

            value = null;
            errorResult = StatusCode(500, $"Setting '{key}' not found.");

            return false;
        }

        private async Task SendNotifications(string message)
        {
            var urls = _configuration.GetValue<string?>("SlackWebhookUrls");

            if (string.IsNullOrWhiteSpace(urls))
            {
                return;
            }

            foreach (var rawUrl in urls.Split(','))
            {
                try
                {
                    var url = rawUrl.Trim();

                    var client = new HttpClient();
                    var json = JsonSerializer.Serialize(new
                    {
                        text = $"{message} IP={Request.HttpContext.Connection.RemoteIpAddress} URL={Request.GetDisplayUrl()}",
                    });
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send notification.");
                }
            }
        }
    }
}

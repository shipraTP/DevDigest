using System.Net.Http.Headers;
using System.Text.Json;
using DevDigest.App.Models;
using Microsoft.Extensions.Logging;

namespace DevDigest.App.Services;

public interface IDeliveryService
{
    Task SendDigestEmailAsync(string toEmail, Dictionary<string, string> categorySummaries, CancellationToken cancellationToken = default);
}

public class DeliveryService : IDeliveryService
{
    private readonly EmailSettings _emailSettings;
    private readonly DigestConfig _config;
    private readonly ILogger<DeliveryService> _logger;
    private readonly HttpClient _httpClient;

    public DeliveryService(HttpClient httpClient, DigestConfig config, ILogger<DeliveryService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _emailSettings = config.Email;
        _logger = logger;
    }

    public async Task SendDigestEmailAsync(string toEmail, Dictionary<string, string> categorySummaries, CancellationToken cancellationToken = default)
    {
        // Get access token using password grant flow
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("Failed to obtain access token for Microsoft Graph API");
        }

        var htmlBody = GenerateHtmlEmail(categorySummaries);
        
        // Send email via Microsoft Graph API
        var request = new
        {
            message = new
            {
                subject = $"DevDigest - {DateTime.UtcNow:MMMM dd, yyyy}",
                body = new { contentType = "HTML", content = htmlBody },
                toRecipients = new[]
                {
                    new { emailAddress = new { address = toEmail } }
                }
            },
            saveToSentItems = true
        };

        var requestJson = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/sendMail");
        message.Content = content;
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send email via Graph API: {error}");
        }

        _logger.LogInformation("Digest email sent successfully to {ToEmail}", toEmail);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        
        var body = new Dictionary<string, string>
        {
            { "client_id", _config.Graph.ClientId },
            { "scope", "https://graph.microsoft.com/.default" },
            { "client_secret", _emailSettings.Password },
            { "username", _emailSettings.Username },
            { "password", _emailSettings.Password }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        request.Content = new FormUrlEncodedContent(body);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get access token: {Error}", error);
            return string.Empty;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        
        return doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static string GenerateHtmlEmail(Dictionary<string, string> categorySummaries)
    {
        var categoriesHtml = string.Join("\n", categorySummaries.Select(kvp => $@"
        <div style='margin-bottom: 30px;'>
            <h2 style='color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>
                {EscapeHtml(kvp.Key)}
            </h2>
            <div style='line-height: 1.8;'>
                {kvp.Value.Replace("\n", "<br/>")}
            </div>
        </div>
        "));

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>DevDigest</title>
</head>
<body style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>
    <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
        <h1 style='color: #2c3e50; text-align: center; margin-bottom: 10px;'>
            🚀 DevDigest
        </h1>
        <p style='text-align: center; color: #7f8c8d; margin-bottom: 30px;'>
            Your Daily Developer Newsletter - {DateTime.UtcNow:MMMM dd, yyyy}
        </p>
        {categoriesHtml}
        <div style='margin-top: 40px; padding-top: 20px; border-top: 1px solid #ecf0f1; text-align: center; color: #95a5a6; font-size: 12px;'>
            <p>Generated by DevDigest • Built with .NET 8</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
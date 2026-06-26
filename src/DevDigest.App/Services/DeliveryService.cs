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
    private readonly SendGridSettings _sendGridSettings;
    private readonly ILogger<DeliveryService> _logger;
    private readonly HttpClient _httpClient;

    public DeliveryService(HttpClient httpClient, DigestConfig config, ILogger<DeliveryService> logger)
    {
        _httpClient = httpClient;
        _sendGridSettings = config.SendGrid;
        _logger = logger;
    }

    public async Task SendDigestEmailAsync(string toEmail, Dictionary<string, string> categorySummaries, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_sendGridSettings.ApiKey))
        {
            _logger.LogWarning("No SendGrid API key configured. Skipping email delivery.");
            return;
        }

        var htmlBody = GenerateHtmlEmail(categorySummaries);
        var subject = $"DevDigest - {DateTime.UtcNow:MMMM dd, yyyy}";

        var request = new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = toEmail } } }
            },
            from = new { email = _sendGridSettings.FromEmail, name = "DevDigest" },
            subject = subject,
            content = new[]
            {
                new { type = "text/html", value = htmlBody }
            }
        };

        var requestJson = JsonSerializer.Serialize(request);
        using var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        message.Content = content;
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sendGridSettings.ApiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send email via SendGrid: {error}");
        }

        _logger.LogInformation("Digest email sent successfully to {ToEmail}", toEmail);
    }

    private static string GenerateHtmlEmail(Dictionary<string, string> categorySummaries)
    {
        var dateStr = DateTime.UtcNow.ToString("dddd, MMMM dd, yyyy");
        
        var categoriesHtml = string.Join("\n", categorySummaries.Select(kvp => $@"
        <div style='margin-bottom: 40px; background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.06);'>
            <div style='display: flex; align-items: center; margin-bottom: 20px;'>
                <div style='width: 40px; height: 40px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 10px; display: flex; align-items: center; justify-content: center; margin-right: 12px;'>
                    <span style='color: white; font-size: 20px;'>📚</span>
                </div>
                <h2 style='color: #1a1a2e; margin: 0; font-size: 20px; font-weight: 700;'>
                    {EscapeHtml(kvp.Key)}
                </h2>
            </div>
            <div style='font-size: 15px; line-height: 1.8; color: #333;'>
                {ConvertToHtml(kvp.Value)}
            </div>
        </div>
        "));

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>DevDigest - Daily Developer Newsletter</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f0f2f5; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;'>
    <div style='max-width: 680px; margin: 0 auto; padding: 20px;'>
        
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%); border-radius: 16px; padding: 32px; margin-bottom: 24px; text-align: center;'>
            <div style='display: inline-block; background: rgba(255,255,255,0.1); border-radius: 50px; padding: 6px 16px; margin-bottom: 16px;'>
                <span style='color: #a855f7; font-size: 12px; font-weight: 600; letter-spacing: 1px;'>DAILY DIGEST</span>
            </div>
            <h1 style='color: #ffffff; margin: 0 0 8px 0; font-size: 32px; font-weight: 800;'>
                🚀 DevDigest
            </h1>
            <p style='color: #94a3b8; margin: 0; font-size: 14px;'>
                {dateStr} • Curated for Developers
            </p>
        </div>
        
        <!-- Quick Stats -->
        <div style='display: flex; gap: 12px; margin-bottom: 24px;'>
            <div style='flex: 1; background: white; border-radius: 12px; padding: 16px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.04);'>
                <div style='font-size: 24px; font-weight: 700; color: #667eea;'>{categorySummaries.Count}</div>
                <div style='font-size: 12px; color: #64748b;'>Categories</div>
            </div>
            <div style='flex: 1; background: white; border-radius: 12px; padding: 16px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.04);'>
                <div style='font-size: 24px; font-weight: 700; color: #10b981;'>{categorySummaries.Values.Sum(v => v.Split(new[] { "🔗" }, StringSplitOptions.None).Length - 1)}</div>
                <div style='font-size: 12px; color: #64748b;'>Articles</div>
            </div>
            <div style='flex: 1; background: white; border-radius: 12px; padding: 16px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.04);'>
                <div style='font-size: 24px; font-weight: 700; color: #f59e0b;'>5 min</div>
                <div style='font-size: 12px; color: #64748b;'>Read Time</div>
            </div>
        </div>
        
        <!-- Categories -->
        {categoriesHtml}
        
        <!-- Footer -->
        <div style='background: #1e293b; border-radius: 12px; padding: 24px; text-align: center; margin-top: 24px;'>
            <p style='color: #94a3b8; font-size: 13px; margin: 0 0 12px 0;'>
                Delivered by <span style='color: #ffffff; font-weight: 600;'>DevDigest</span> • Built with .NET 8
            </p>
            <p style='color: #64748b; font-size: 11px; margin: 0;'>
                You're receiving this because you subscribed to developer updates.<br>
                © 2024 DevDigest • Auto-generated daily
            </p>
        </div>
        
    </div>
</body>
</html>";
    }

    private static string ConvertToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        
        // Convert code blocks first (before other processing)
        var html = System.Text.RegularExpressions.Regex.Replace(
            markdown,
            @"```[\w]*\n?([\s\S]*?)```",
            "<pre style='background: #1e293b; color: #e2e8f0; padding: 16px; border-radius: 8px; overflow-x: auto; font-family: Monaco, Consolas, monospace; font-size: 13px; line-height: 1.6; margin: 16px 0;'><code>$1</code></pre>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert TL;DR section
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"## TL;DR\s*\n+(.+)",
            "<div style='background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%); border-left: 4px solid #f59e0b; padding: 16px; border-radius: 8px; margin: 16px 0;'><strong style='color: #92400e;'>💡 TL;DR</strong><p style='color: #78350f; margin: 8px 0 0 0;'>$1</p></div>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert Quick Tip section
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"## 💡 Quick Tip\s*\n+(.+)",
            "<div style='background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%); border-left: 4px solid #3b82f6; padding: 16px; border-radius: 8px; margin: 16px 0;'><strong style='color: #1e40af;'>💡 Quick Tip</strong><p style='color: #1e3a8a; margin: 8px 0 0 0;'>$1</p></div>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert Tool of the Day section
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"## 🛠️ Tool of the Day\s*\n+(.+)",
            "<div style='background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%); border-left: 4px solid #10b981; padding: 16px; border-radius: 8px; margin: 16px 0;'><strong style='color: #065f46;'>🛠️ Tool of the Day</strong><div style='color: #064e3b; margin: 8px 0 0 0;'>$1</div></div>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert Code Snippet section
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"## 📌 Code Snippet\s*\n+(.+)",
            "<div style='background: #f8fafc; border: 1px solid #e2e8f0; padding: 16px; border-radius: 8px; margin: 16px 0;'><strong style='color: #475569;'>📌 Code Snippet</strong><div style='margin-top: 8px;'>$1</div></div>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert ## Top Stories header
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"## Top Stories\s*\n*",
            "<h3 style='color: #1e293b; font-size: 18px; margin: 24px 0 16px 0; padding-bottom: 8px; border-bottom: 2px solid #e2e8f0;'>📰 Top Stories</h3>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert ## headers to h3
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"^## (.+)$",
            "<h3 style='color: #334155; margin: 20px 0 10px 0; font-size: 16px;'>📌 $1</h3>",
            System.Text.RegularExpressions.RegexOptions.Multiline
        );
        
        // Convert markdown links to styled HTML
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"\[([^\]]+)\]\(([^)]+)\)",
            "<a href='$2' style='display: inline-block; background: #667eea; color: white; padding: 4px 12px; border-radius: 4px; text-decoration: none; font-size: 13px; font-weight: 500; margin-top: 8px;'>🔗 Read More</a>"
        );
        
        // Convert bold
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong style='color: #1a1a2e;'>$1</strong>");
        
        // Convert newlines
        html = html.Replace("\n\n", "</p><p style='margin: 8px 0;'>").Replace("\n", "<br>");
        
        return html;
    }

    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
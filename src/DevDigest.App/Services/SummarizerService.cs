using System.Net.Http.Headers;
using System.Text.Json;
using DevDigest.App.Models;
using Microsoft.Extensions.Logging;

namespace DevDigest.App.Services;

public interface ISummarizerService
{
    Task<string> SummarizeCategoryAsync(string category, List<FeedItem> items, CancellationToken cancellationToken = default);
}

public class SummarizerService : ISummarizerService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<SummarizerService> _logger;

    public SummarizerService(HttpClient httpClient, DigestConfig config, ILogger<SummarizerService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config.OpenAI.ApiKey;
        _model = config.OpenAI.Model;
        _logger = logger;
    }

    public async Task<string> SummarizeCategoryAsync(string category, List<FeedItem> items, CancellationToken cancellationToken = default)
    {
        // If no API key, return simple text summary
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("No OpenAI API key configured. Using basic summary.");
            return GenerateBasicSummary(category, items);
        }

        var itemsText = string.Join("\n", items.Select((item, index) =>
            $"{index + 1}. **{item.Title}**\n   URL: {item.Url}\n   Summary: {TruncateText(item.Summary, 200)}"));

        var prompt = $@"You are a tech newsletter editor. Create a 5-bullet-point summary of the following {category} articles for a developer digest.

Articles:
{itemsText}

Provide exactly 5 bullet points that capture the most important highlights. Format each bullet as:
• [Brief insightful summary of the topic] (Source: source-name)

Keep each bullet concise and informative, focusing on key takeaways for developers.";

        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful tech newsletter editor." },
                new { role = "user", content = prompt }
            },
            max_tokens = 1000
        };

        var requestJson = JsonSerializer.Serialize(request);
        using var requestContent = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        requestMessage.Content = requestContent;
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonDocument.Parse(responseJson);

        return responseObj.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "No summary available.";
    }

    private static string GenerateBasicSummary(string category, List<FeedItem> items)
    {
        var summaries = items.Take(5).Select(item =>
            $"• {item.Title} - {TruncateText(item.Summary, 100)}").ToList();

        return string.Join("\n", summaries);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        // Remove HTML tags
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", string.Empty);
        
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
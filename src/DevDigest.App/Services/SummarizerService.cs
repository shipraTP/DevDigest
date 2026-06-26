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
            $"Title: {item.Title}\nURL: {item.Url}\nSummary: {TruncateText(item.Summary, 300)}\n---"));

        var prompt = $@"You are a senior developer writing a concise daily digest for fellow developers.

Create a well-structured summary of these {category} articles with:
1. A brief intro sentence about what's trending in {category}
2. 3-5 key highlights, each as a short paragraph (2-3 sentences) with:
   - What the news/story is about
   - Why it matters to developers
   - A clear takeaway or action item
   
Format each highlight as:
**[Headline/Topic]**
Explanation of the news and why it matters. Key takeaway for developers.
🔗 [Read More](URL)

Keep tone professional but friendly. Focus on actionable insights developers can use.";

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
        
        // Retry logic for rate limiting
        var maxRetries = 3;
        var retryDelay = 5000; // 5 seconds
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var requestContent = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                requestMessage.Content = requestContent;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("OpenAI rate limit hit. Waiting {Delay}ms before retry...", retryDelay);
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay *= 2; // Exponential backoff
                    continue;
                }
                
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseObj = JsonDocument.Parse(responseJson);

                return responseObj.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "No summary available.";
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt < maxRetries - 1)
                {
                    _logger.LogWarning("OpenAI rate limit hit. Waiting {Delay}ms before retry...", retryDelay);
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay *= 2;
                }
                else
                {
                    _logger.LogWarning("Max retries reached. Using basic summary instead.");
                    return GenerateBasicSummary(category, items);
                }
            }
        }
        
        return GenerateBasicSummary(category, items);
    }

    private static string GenerateBasicSummary(string category, List<FeedItem> items)
    {
        var summaries = items.Take(5).Select(item => $@"## {TruncateText(item.Title, 80)}
{TruncateText(item.Summary, 150)}
🔗 [{item.Url}]({item.Url})").ToList();

        return string.Join("\n\n", summaries);
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
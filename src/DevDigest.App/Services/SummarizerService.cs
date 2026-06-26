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
            $"Title: {item.Title}\nURL: {item.Url}\nSummary: {TruncateText(item.Summary, 400)}\n---"));

        var prompt = $@"You are a senior developer writing a daily newsletter for fellow developers.

Create a comprehensive summary of these {category} articles with these sections:

## TL;DR
One sentence overview of the most important thing in this category today.

## Top Stories (3-5 items)
For each story, provide:
- A clear headline/title
- 2-3 sentences explaining what happened and why it matters
- One specific takeaway or action item developers should know
- A link: 🔗 [Read More](URL)

## 💡 Quick Tip
A useful {category}-related tip, trick, or productivity hack developers can use today.

## 🛠️ Tool of the Day
One useful tool, library, or resource related to {category} that developers should know about. Include the tool name, what it does, and a link.

## 📌 Code Snippet (if any article contains code)
Include a relevant code example from the articles. Format as:
```
// Language and context
code here
```
Include brief comments explaining what the code does and when to use it.

Keep tone professional but friendly. Focus on actionable insights developers can use immediately.";

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
{TruncateText(item.Summary, 200)}
🔗 [{GetDomain(item.Url)}]({item.Url})").ToList();

        return $@"## TL;DR
Latest updates in {category} - {items.Count} articles curated for you.

## Top Stories
{string.Join("\n\n", summaries)}

## 💡 Quick Tip
Check out the full articles for detailed insights and code examples.

## 🛠️ Tool Suggestion
Explore the linked resources to discover new tools and libraries in the {category} space.";

    }

    private static string GetDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Replace("www.", "");
        }
        catch
        {
            return "Source";
        }
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
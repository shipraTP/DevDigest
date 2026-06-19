using DevDigest.App.Models;
using DevDigest.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

class Program
{
    static async Task Main(string[] args)
    {
        var serviceProvider = ConfigureServices();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var config = serviceProvider.GetRequiredService<DigestConfig>();

        logger.LogInformation("DevDigest starting...");

        try
        {
            await RunDigestAsync(serviceProvider, config, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running DevDigest");
            Environment.Exit(1);
        }

        logger.LogInformation("DevDigest completed successfully.");
    }

    static async Task RunDigestAsync(IServiceProvider serviceProvider, DigestConfig config, ILogger logger)
    {
        var feedFetcher = serviceProvider.GetRequiredService<IFeedFetcherService>();
        var summarizer = serviceProvider.GetRequiredService<ISummarizerService>();
        var delivery = serviceProvider.GetRequiredService<IDeliveryService>();

        var categorySummaries = new Dictionary<string, string>();

        var enabledCategories = config.Digest.Categories.Where(c => c.Enabled).ToList();
        logger.LogInformation("Processing {Count} enabled categories", enabledCategories.Count);

        foreach (var category in enabledCategories)
        {
            logger.LogInformation("Fetching feeds for category: {Category}", category.Name);

            var items = await feedFetcher.FetchFeedsAsync(category);
            
            if (items.Count == 0)
            {
                logger.LogWarning("No items found for category: {Category}", category.Name);
                categorySummaries[category.Name] = "No items found for this category.";
                continue;
            }

            var limitedItems = items.Take(config.Digest.MaxItemsPerCategory).ToList();
            logger.LogInformation("Fetched {Count} items for {Category}", limitedItems.Count, category.Name);

            var summary = await summarizer.SummarizeCategoryAsync(category.Name, limitedItems);
            categorySummaries[category.Name] = summary;

            logger.LogInformation("Generated summary for {Category}", category.Name);
        }

        logger.LogInformation("Sending digest email to {ToEmail}", config.Digest.ToEmail);
        await delivery.SendDigestEmailAsync(config.Digest.ToEmail, categorySummaries);
    }

    static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Bind configuration to DigestConfig
        var config = new DigestConfig();
        configuration.Bind(config);
        services.AddSingleton(config);

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Register HttpClient for FeedFetcherService
        services.AddHttpClient<IFeedFetcherService, FeedFetcherService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "DevDigest/1.0");
        });

        // Register HttpClient for SummarizerService
        services.AddHttpClient<ISummarizerService, SummarizerService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Register DeliveryService
        services.AddSingleton<IDeliveryService, DeliveryService>();

        return services.BuildServiceProvider();
    }
}
# DevDigest

A .NET 8 Console Application that generates and delivers a daily developer newsletter by aggregating content from RSS feeds, Hacker News, and more.

## Features

- **Feed Aggregation**: Fetches content from RSS/Atom feeds using `System.ServiceModel.Syndication`
- **Hacker News Integration**: Pulls top stories from the Hacker News Firebase API
- **AI-Powered Summarization**: Uses OpenAI's GPT API to generate concise 5-bullet summaries per category
- **Email Delivery**: Sends beautifully formatted HTML emails using MailKit
- **GitHub Actions CI/CD**: Scheduled daily runs at 7 AM IST (1:30 AM UTC)

## Project Structure

```
DevDigest/
├── src/
│   └── DevDigest.App/
│       ├── Models/
│       │   ├── FeedItem.cs
│       │   └── DigestConfig.cs
│       ├── Services/
│       │   ├── FeedFetcherService.cs
│       │   ├── SummarizerService.cs
│       │   └── DeliveryService.cs
│       ├── Program.cs
│       ├── appsettings.json
│       └── DevDigest.App.csproj
├── .github/
│   └── workflows/
│       └── digest.yml
└── README.md
```

## Prerequisites

- .NET 8 SDK
- OpenAI API Key (for AI summarization) - Free tier available
- SMTP credentials (for email delivery)

## Configuration

Edit `appsettings.json` with your settings:

```json
{
  "Digest": {
    "ToEmail": "your-email@example.com",
    "MaxItemsPerCategory": 10,
    "Categories": [
      {
        "Name": "HLD",
        "Enabled": true,
        "Sources": [
          "https://learn.microsoft.com/en-us/blog/rss.xml",
          "https://aws.amazon.com/blogs/architecture/feed/"
        ]
      }
    ]
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4o-mini"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

### Getting API Keys

**OpenAI API Key (Free Tier Available):**
1. Sign up at [OpenAI Platform](https://platform.openai.com/)
2. Go to API Keys section (https://platform.openai.com/api-keys)
3. Click "Create new secret key"
4. Copy the key and add it as a GitHub Secret

**Note:** OpenAI offers $5 free credits for new users. The app uses `gpt-4o-mini` which is very affordable (~$0.075/1M tokens).

**Gmail App Password (for SMTP):**
1. Enable 2-Factor Authentication on your Google Account
2. Go to Security > App Passwords
3. Generate a new app password for "Mail"
4. Use this 16-character password in your config

## Local Development

```bash
# Clone the repository
git clone <your-repo-url>
cd DevDigest

# Restore dependencies
dotnet restore src/DevDigest.App/DevDigest.App.csproj

# Run the application
dotnet run --project src/DevDigest.App/DevDigest.App.csproj
```

## GitHub Actions Setup

### Required Secrets

Add the following secrets to your GitHub repository:

| Secret Name | Description |
|-------------|-------------|
| `OPENAI_API_KEY` | Your OpenAI API key |
| `EMAIL_USERNAME` | SMTP username (email address) |
| `EMAIL_PASSWORD` | SMTP password or app password |

### Optional Variables

| Variable Name | Description |
|---------------|-------------|
| `DIGEST_TO_EMAIL` | Recipient email address (used in workflow) |

### Setting Secrets

1. Go to your repository on GitHub
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add each required secret

## Schedule

The workflow runs daily at:
- **7:00 AM IST** (India Standard Time)
- **1:30 AM UTC** (Coordinated Universal Time)

This is configured in `.github/workflows/digest.yml`:
```yaml
schedule:
  - cron: '30 1 * * *'  # 1:30 AM UTC daily
```

## Manual Trigger

You can manually trigger the workflow:
1. Go to the Actions tab in your GitHub repository
2. Select "DevDigest Daily Email"
3. Click "Run workflow"
4. Optionally specify log level

## Adding New Categories

Edit the `Categories` array in `appsettings.json`:

```json
{
  "Categories": [
    {
      "Name": "Your Category Name",
      "Enabled": true,
      "Sources": [
        "https://example.com/feed.xml",
        "https://hacker-news.firebaseio.com/v0/topstories.json"
      ]
    }
  ]
}
```

### Source Types

- **RSS/Atom feeds**: Standard feed URLs (`.xml`, `.rss`, `.atom`)
- **Hacker News**: Use `https://hacker-news.firebaseio.com/v0/topstories.json`

## Building

```bash
# Build for development
dotnet build src/DevDigest.App/DevDigest.App.csproj

# Build for release
dotnet build src/DevDigest.App/DevDigest.App.csproj --configuration Release
```

## Dependencies

- **MailKit** (4.3.0) - Email sending
- **Microsoft.Extensions.Configuration.Json** (8.0.0) - JSON configuration
- **Microsoft.Extensions.DependencyInjection** (8.0.0) - Dependency injection
- **Microsoft.Extensions.Http** (8.0.0) - HTTP client factory
- **System.ServiceModel.Syndication** - RSS/Atom feed parsing (built-in .NET 8)

## License

MIT License
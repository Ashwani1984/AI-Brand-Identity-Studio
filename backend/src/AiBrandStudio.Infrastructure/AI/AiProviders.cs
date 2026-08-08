using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiBrandStudio.Application.Common;
using Microsoft.Extensions.Configuration;

namespace AiBrandStudio.Infrastructure.AI;

public class MockTextAiProvider : ITextAiProvider
{
    public string Name => "Mock";

    public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(userPrompt) ? "{}" : userPrompt);
        var root = doc.RootElement;
        var business = root.TryGetProperty("BusinessName", out var bn) ? bn.GetString() : "Brand";
        var industry = root.TryGetProperty("Industry", out var ind) ? ind.GetString() : "Technology";
        var text =
            $"NAME: {business}\nTAGLINE: Build from the core.\nSTORY: {business} was created to bring clarity to {industry} brands using AI-assisted identity systems.\nVOICE: Premium, modern, precise, and calm.\nMISSION: Help teams ship recognizable brands without design friction.\nVISION: Become the default AI brand studio for modern companies.";
        return Task.FromResult(text);
    }
}

public class MockImageAiProvider : IImageAiProvider
{
    public string Name => "Mock";

    public Task<(byte[] Bytes, string ContentType, string? Svg)> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        var svg =
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
              <rect width="512" height="512" fill="#F7F8FA"/>
              <rect x="96" y="96" width="320" height="320" rx="48" fill="none" stroke="#0F766E" stroke-width="28"/>
              <circle cx="256" cy="256" r="64" fill="#0B1220"/>
              <circle cx="256" cy="256" r="28" fill="#0F766E"/>
            </svg>
            """;
        return Task.FromResult<(byte[] Bytes, string ContentType, string? Svg)>((Encoding.UTF8.GetBytes(svg), "image/svg+xml", svg));
    }
}

public class OpenAiTextProvider(IHttpClientFactory httpClientFactory, IConfiguration config) : ITextAiProvider
{
    public string Name => "OpenAI";

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var apiKey = config["AI:OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return await new MockTextAiProvider().GenerateTextAsync(systemPrompt, userPrompt, ct);

        var client = httpClientFactory.CreateClient("openai");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var model = config["AI:OpenAI:Model"] ?? "gpt-4o-mini";
        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        var res = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
               ?? string.Empty;
    }
}

public class GeminiTextProvider(IHttpClientFactory httpClientFactory, IConfiguration config) : ITextAiProvider
{
    public string Name => "Gemini";

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var apiKey = config["AI:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return await new MockTextAiProvider().GenerateTextAsync(systemPrompt, userPrompt, ct);

        var model = config["AI:Gemini:Model"] ?? "gemini-2.0-flash";
        var client = httpClientFactory.CreateClient("gemini");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            }
        };
        var res = await client.PostAsJsonAsync(url, payload, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
               ?? string.Empty;
    }
}

public class IdeogramImageProvider(IHttpClientFactory httpClientFactory, IConfiguration config) : IImageAiProvider
{
    public string Name => "Ideogram";

    public async Task<(byte[] Bytes, string ContentType, string? Svg)> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        var apiKey = config["AI:Ideogram:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return await new MockImageAiProvider().GenerateImageAsync(prompt, ct);

        // Placeholder: Ideogram API varies by account; fall back to mock SVG on failure.
        try
        {
            var client = httpClientFactory.CreateClient("ideogram");
            client.DefaultRequestHeaders.Add("Api-Key", apiKey);
            var res = await client.PostAsJsonAsync("https://api.ideogram.ai/generate", new { image_request = new { prompt } }, ct);
            if (!res.IsSuccessStatusCode)
                return await new MockImageAiProvider().GenerateImageAsync(prompt, ct);
            // Real response parsing would download image URL; keep mock for robustness in Phase 1.
            return await new MockImageAiProvider().GenerateImageAsync(prompt, ct);
        }
        catch
        {
            return await new MockImageAiProvider().GenerateImageAsync(prompt, ct);
        }
    }
}

public class AiProviderFactory(
    IConfiguration config,
    OpenAiTextProvider openAi,
    GeminiTextProvider gemini,
    MockTextAiProvider mockText,
    IdeogramImageProvider ideogram,
    MockImageAiProvider mockImage) : IAiProviderFactory
{
    public ITextAiProvider GetTextProvider()
    {
        var name = (config["AI:DefaultTextProvider"] ?? "Mock").Trim();
        return name.ToLowerInvariant() switch
        {
            "openai" => openAi,
            "gemini" => gemini,
            _ => mockText
        };
    }

    public IImageAiProvider GetImageProvider()
    {
        var name = (config["AI:DefaultImageProvider"] ?? "Mock").Trim();
        return name.ToLowerInvariant() switch
        {
            "ideogram" => ideogram,
            _ => mockImage
        };
    }
}

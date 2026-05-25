using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ComputerBuddy.App.Services.OpenAi;

public sealed class OpenAiChatClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.openai.com/")
    };

    public async Task<string> SendAsync(string apiKey, string model, IReadOnlyList<(string role, string content)> messages, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.role, content = m.content })
        };

        var json = JsonSerializer.Serialize(payload);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI request failed ({(int)resp.StatusCode}): {respJson}");
        }

        using var doc = JsonDocument.Parse(respJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "";
    }
}


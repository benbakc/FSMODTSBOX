using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameHanBox.Models;

namespace GameHanBox.Services;

public class TranslationService : IDisposable
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly string _targetLangName;
    private bool _disposed;

    public TranslationService(AppSettings settings, string targetLangName = "简体中文")
    {
        _http = new HttpClient
        {
            // API 请求单次超时 90 秒（默认 100 秒，30 秒对某些模型太短）
            Timeout = TimeSpan.FromSeconds(90)
        };
        _settings = settings;
        _targetLangName = targetLangName;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _http?.CancelPendingRequests();
            _http?.Dispose();
        }
    }

    public string? LastError { get; private set; }

    public async Task TranslateBatchAsync(List<FoundString> strings, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        LastError = null;

        // 增大每批条数到 200，大幅减少 API 调用次数
        const int BATCH_SIZE = 200;

        var pending = strings
            .Where(s => !string.IsNullOrEmpty(s.OriginalText) && string.IsNullOrEmpty(s.TranslatedText))
            .ToList();

        if (pending.Count == 0) return;

        var batches = pending
            .Select((s, i) => new { s, i })
            .GroupBy(x => x.i / BATCH_SIZE)
            .Select(g => g.Select(x => x.s).ToList())
            .ToList();

        var totalBatches = batches.Count;
        var completedBatches = 0;
        int consecutiveErrors = 0;

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await TranslateBatchInternal(batch, cancellationToken).ConfigureAwait(false);

            if (result != null && result.Count == batch.Count)
            {
                consecutiveErrors = 0;
                for (int i = 0; i < batch.Count; i++)
                    batch[i].TranslatedText = result[i];
            }
            else
            {
                consecutiveErrors++;
                // 连续 2 批失败 → 停止翻译，避免在挂死的 API 上白白等待
                if (consecutiveErrors >= 2)
                {
                    progress?.Report((completedBatches * 100) / totalBatches);
                    break;
                }
            }

            completedBatches++;
            progress?.Report((completedBatches * 100) / totalBatches);
        }
    }

    private async Task<List<string>?> TranslateBatchInternal(List<FoundString> batch, CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.ResolvedApiKey;
        var apiUrl = _settings.ResolvedApiUrl;
        var model = _settings.ResolvedModel;

        if (string.IsNullOrEmpty(apiKey))
        {
            return batch.Select(s => s.OriginalText).ToList();
        }

        try
        {
            var texts = batch.Select(s => s.OriginalText).ToList();
            var textList = string.Join("\n", texts.Select((t, i) => $"{i + 1}. {t}"));

            var payload = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["role"] = "system",
                        ["content"] = $"你是一个游戏本地化翻译专家。将以下英文游戏文本翻译成{_targetLangName}。保持游戏原意，符合游戏风格。只返回翻译结果，每行一个，保持序号格式。"
                    },
                    new Dictionary<string, string>
                    {
                        ["role"] = "user",
                        ["content"] = $"请翻译以下游戏文本：\n{textList}"
                    }
                },
                ["temperature"] = 0.3,
                ["max_tokens"] = 4096
            };

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            // OpenRouter needs additional headers
            if (_settings.Provider == "OpenRouter")
            {
                request.Headers.Add("HTTP-Referer", "https://github.com/game-han-box");
                request.Headers.Add("X-Title", "FSMODTSBOX");
            }

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LastError = $"API 返回错误 ({(int)response.StatusCode}): {errorBody}";
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            string? content = null;

            // Try standard OpenAI-compatible format
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c))
                {
                    content = c.GetString();
                }
                // Some providers use "text" instead of "message.content"
                else if (first.TryGetProperty("text", out var t))
                {
                    content = t.GetString();
                }
            }

            if (content == null)
            {
                LastError = "API 返回了空内容，可能是模型不支持或返回格式异常";
                return null;
            }

            var result = new List<string>();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\d+[\.\:\)]\s*(.*)");
                if (match.Success)
                    result.Add(match.Groups[1].Value.Trim());
                else
                    result.Add(trimmed);
            }

            if (result.Count == batch.Count) return result;
            if (result.Count > batch.Count) return result.TakeLast(batch.Count).ToList();
            while (result.Count < batch.Count)
                result.Add(batch[result.Count].OriginalText);
            return result;
        }
        catch (Exception ex)
        {
            LastError = $"请求异常: {ex.Message}";
            return null;
        }
    }
}

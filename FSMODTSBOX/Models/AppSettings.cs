using System.Text.Json.Serialization;

namespace GameHanBox.Models;

public class AppSettings
{
    // 内置 Agnes API 密钥（免费开源，内置使用）
    public const string BuiltinAgnesKey = "sk-nsRhFSBZaJ1EflpWZAgPeBQXCSqXiqklrnMbyQx3aM65FG5v";

    public string ApiMode { get; set; } = "own";
    public string Provider { get; set; } = "Agnes";
    public string CurrentGame { get; set; } = "nightreign";
    public string TargetLanguage { get; set; } = "zhocn";
    public string CustomApiUrl { get; set; } = "";
    public string CustomModelName { get; set; } = "";
    public string OwnApiKey { get; set; } = "";
    public string UILanguage { get; set; } = "";

    [JsonIgnore] public string ResolvedApiKey =>
        !string.IsNullOrEmpty(OwnApiKey) ? OwnApiKey : BuiltinAgnesKey;

    [JsonIgnore] public string ResolvedApiUrl =>
        Provider == "自定义" && !string.IsNullOrEmpty(CustomApiUrl)
            ? CustomApiUrl
            : ProviderInfo.TryGetValue(Provider, out var info) ? info.url : ProviderInfo["Agnes"].url;

    [JsonIgnore] public string ResolvedModel =>
        Provider == "自定义" ? CustomModelName
            : ProviderInfo.TryGetValue(Provider, out var info) ? info.model : ProviderInfo["Agnes"].model;

    [JsonIgnore]
    public int ProviderIndex
    {
        get
        {
            var all = new[] { "DeepSeek", "ChatGPT", "OpenRouter", "硅基流动", "智谱 GLM", "MiniMax", "月之暗面 Kimi", "小米 MiMo", "豆包", "通义千问", "百度文心", "阶跃星辰 Step", "自定义" };
            int idx = Array.IndexOf(all, Provider);
            return idx >= 0 ? idx : 0;
        }
    }

    public static readonly string[] Games = new[]
    {
        "nightreign",
        "elden-ring",
        "dark-souls-3",
    };

    public static string GameDisplayName(string game) => game switch
    {
        "nightreign" => "艾尔登法环：黑夜君临",
        "elden-ring" => "艾尔登法环",
        "dark-souls-3" => "黑暗之魂3",
        _ => game,
    };

    public static string GameDisplayNameEn(string game) => game switch
    {
        "nightreign" => "Elden Ring: Nightreign",
        "elden-ring" => "Elden Ring",
        "dark-souls-3" => "Dark Souls 3",
        _ => game,
    };

    public static readonly Dictionary<string, string> Languages = new()
    {
        ["zhocn"] = "简体中文",
        ["zhotw"] = "繁体中文",
        ["engus"] = "English",
        ["jpnjp"] = "日本語",
        ["korkr"] = "한국어",
        ["frafr"] = "Français",
        ["deude"] = "Deutsch",
        ["itait"] = "Italiano",
        ["spaes"] = "Español (España)",
        ["spaar"] = "Español (LATAM)",
        ["porbr"] = "Português (Brasil)",
        ["rusru"] = "Русский",
        ["polpl"] = "Polski",
        ["araae"] = "العربية",
        ["thath"] = "ไทย",
    };

    /// <summary>
    /// 语言代码 → LLM 翻译指令中的语言名称
    /// </summary>
    public static readonly Dictionary<string, string> LanguagePrompts = new()
    {
        ["zhocn"] = "简体中文",
        ["zhotw"] = "繁体中文",
        ["engus"] = "English",
        ["jpnjp"] = "日语",
        ["korkr"] = "韩语",
        ["frafr"] = "法语",
        ["deude"] = "德语",
        ["itait"] = "意大利语",
        ["spaes"] = "西班牙语（西班牙）",
        ["spaar"] = "西班牙语（拉丁美洲）",
        ["porbr"] = "葡萄牙语（巴西）",
        ["rusru"] = "俄语",
        ["polpl"] = "波兰语",
        ["araae"] = "阿拉伯语",
        ["thath"] = "泰语",
    };

    public static readonly Dictionary<string, (string url, string model)> ProviderInfo = new()
    {
        ["DeepSeek"] = ("https://api.deepseek.com/v1/chat/completions", "deepseek-chat"),
        ["ChatGPT"] = ("https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
        ["OpenRouter"] = ("https://openrouter.ai/api/v1/chat/completions", "openai/gpt-4o-mini"),
        ["硅基流动"] = ("https://api.siliconflow.cn/v1/chat/completions", "deepseek-ai/DeepSeek-V3"),
        ["智谱 GLM"] = ("https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-flash"),
        ["MiniMax"] = ("https://api.minimax.chat/v1/chat/completions", "MiniMax-Text-01"),
        ["月之暗面 Kimi"] = ("https://api.moonshot.cn/v1/chat/completions", "moonshot-v1-8k"),
        ["小米 MiMo"] = ("https://api.mimo.mi.com/v1/chat/completions", "mimo-v2.5-pro"),
        ["豆包"] = ("https://ark.cn-beijing.volces.com/api/v3/chat/completions", "doubao-pro-32k"),
        ["通义千问"] = ("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-turbo"),
        ["百度文心"] = ("https://qianfan.baidupcs.com/v2/chat/completions", "ernie-4.5-turbo-128k"),
        ["阶跃星辰 Step"] = ("https://api.stepfun.com/v1/chat/completions", "step-1-flash"),
        // Agnes 为内置默认模型，不在下拉列表显示
        ["Agnes"] = ("https://apihub.agnes-ai.com/v1/chat/completions", "agnes-2.0-flash"),
    };

    // Provider display name → internal key mapping for UI language switch
    private static readonly Dictionary<string, string> _providerDisplayToKey = new()
    {
        ["DeepSeek"] = "DeepSeek",
        ["ChatGPT"] = "ChatGPT",
        ["OpenRouter"] = "OpenRouter",
        ["硅基流动"] = "硅基流动",
        ["SiliconFlow"] = "硅基流动",
        ["智谱 GLM"] = "智谱 GLM",
        ["Zhipu GLM"] = "智谱 GLM",
        ["MiniMax"] = "MiniMax",
        ["月之暗面 Kimi"] = "月之暗面 Kimi",
        ["Moonshot Kimi"] = "月之暗面 Kimi",
        ["小米 MiMo"] = "小米 MiMo",
        ["Xiaomi MiMo"] = "小米 MiMo",
        ["豆包"] = "豆包",
        ["Doubao"] = "豆包",
        ["通义千问"] = "通义千问",
        ["Tongyi Qwen"] = "通义千问",
        ["百度文心"] = "百度文心",
        ["Baidu ERNIE"] = "百度文心",
        ["阶跃星辰 Step"] = "阶跃星辰 Step",
        ["StepFun Step"] = "阶跃星辰 Step",
        ["自定义"] = "自定义",
        ["Custom"] = "自定义",
    };

    public static string ProviderDisplayName(string key, string lang)
    {
        if (lang == "en")
            return key switch
            {
                "DeepSeek" => "DeepSeek",
                "ChatGPT" => "ChatGPT",
                "OpenRouter" => "OpenRouter",
                "硅基流动" => "SiliconFlow",
                "智谱 GLM" => "Zhipu GLM",
                "MiniMax" => "MiniMax",
                "月之暗面 Kimi" => "Moonshot Kimi",
                "小米 MiMo" => "Xiaomi MiMo",
                "豆包" => "Doubao",
                "通义千问" => "Tongyi Qwen",
                "百度文心" => "Baidu ERNIE",
                "阶跃星辰 Step" => "StepFun Step",
                "自定义" => "Custom",
                _ => key,
            };
        return key;
    }

    public static string ProviderKeyFromDisplay(string displayName)
    {
        return _providerDisplayToKey.TryGetValue(displayName, out var key) ? key : displayName;
    }
}

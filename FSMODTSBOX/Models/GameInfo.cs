namespace GameHanBox.Models;

public class GameInfo
{
    public string ExePath { get; set; } = "";
    public string GameName { get; set; } = "";
    public string DataDir { get; set; } = "";
    public bool IsUnityGame { get; set; }
    public string EngineType { get; set; } = "未知";
    public string ManagedDir { get; set; } = "";
    public List<string> AssetFiles { get; set; } = new();
}

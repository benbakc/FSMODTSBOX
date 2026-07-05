using System.IO;
using GameHanBox.Models;

namespace GameHanBox.Services;

public class UnityDetector
{
    public GameInfo Detect(string exePath)
    {
        var info = new GameInfo
        {
            ExePath = exePath,
            GameName = Path.GetFileNameWithoutExtension(exePath)
        };

        var exeDir = Path.GetDirectoryName(exePath);
        if (exeDir == null) return info;

        // Unity game detection: check for UnityPlayer.dll and _Data folder
        var dataDir = Path.Combine(exeDir, info.GameName + "_Data");
        var unityPlayerDll = Path.Combine(exeDir, "UnityPlayer.dll");
        var dataDirAlt = Path.Combine(exeDir, info.GameName + "_data");

        if (File.Exists(unityPlayerDll) && Directory.Exists(dataDir))
        {
            info.IsUnityGame = true;
            info.EngineType = "Unity (Mono)";
            info.DataDir = dataDir;
        }
        else if (File.Exists(unityPlayerDll) && Directory.Exists(dataDirAlt))
        {
            info.IsUnityGame = true;
            info.EngineType = "Unity (Mono)";
            info.DataDir = dataDirAlt;
        }
        // IL2CPP Unity (GameAssembly.dll instead of mono)
        else if (File.Exists(Path.Combine(exeDir, "GameAssembly.dll")) && Directory.Exists(dataDir))
        {
            info.IsUnityGame = true;
            info.EngineType = "Unity (IL2CPP)";
            info.DataDir = dataDir;
        }
        else
        {
            info.IsUnityGame = false;
            info.EngineType = DetectGeneric(exeDir);
            return info;
        }

        // Find Managed dir
        var managedDir = Path.Combine(info.DataDir, "Managed");
        if (Directory.Exists(managedDir))
            info.ManagedDir = managedDir;

        // Find asset files to scan
        if (Directory.Exists(info.DataDir))
        {
            var assetPatterns = new[] { "*.assets", "level*", "*.resource" };
            foreach (var pattern in assetPatterns)
            {
                info.AssetFiles.AddRange(
                    Directory.GetFiles(info.DataDir, pattern)
                        .OrderBy(f => new FileInfo(f).Length)
                        .ToList());
            }
        }

        return info;
    }

    private static string DetectGeneric(string dir)
    {
        if (File.Exists(Path.Combine(dir, "UnityCrashHandler64.exe")))
            return "Unity";
        if (Directory.GetFiles(dir, "*.uproject").Length > 0)
            return "Unreal Engine";
        if (Directory.GetFiles(dir, "*.pck").Length > 0 || Directory.GetFiles(dir, "*.exe").Any(f => Path.GetFileName(f)?.StartsWith("Godot") == true))
            return "Godot";
        if (Directory.GetFiles(dir, "*.nw").Length > 0 || File.Exists(Path.Combine(dir, "package.json")))
            return "Electron / NW.js";
        return "未知引擎";
    }
}

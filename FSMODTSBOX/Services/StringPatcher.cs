using System.IO;
using System.Text;
using GameHanBox.Models;

namespace GameHanBox.Services;

public class StringPatcher
{
    private readonly string _dataDir;

    public StringPatcher(string dataDir)
    {
        _dataDir = dataDir;
    }

    public async Task<int> ApplyTranslationsAsync(List<FoundString> strings, IProgress<int>? progress = null)
    {
        var total = strings.Count;
        var applied = 0;

        var byFile = strings
            .Where(s => !string.IsNullOrEmpty(s.TranslatedText) && s.TranslatedText != s.OriginalText)
            .GroupBy(s => s.SourceFile)
            .ToList();

        foreach (var group in byFile)
        {
            var filePath = FindFile(group.Key);
            if (filePath == null) continue;

            var backupPath = filePath + ".bak";
            if (!File.Exists(backupPath))
                File.Copy(filePath, backupPath, true);

            var data = File.ReadAllBytes(filePath);

            foreach (var fs in group.OrderByDescending(s => s.Offset))
            {
                if (TryPatchString(data, fs))
                {
                    applied++;
                    progress?.Report((applied * 100) / total);
                }
            }

            File.WriteAllBytes(filePath, data);
        }

        return await Task.FromResult(applied);
    }

    private bool TryPatchString(byte[] data, FoundString fs)
    {
        long offset = fs.Offset;
        if (offset < 0 || offset >= data.Length)
            return false;

        var translatedBytes = Encoding.Unicode.GetBytes(fs.TranslatedText + "\0");
        var originalBytes = Encoding.Unicode.GetBytes(fs.OriginalText + "\0");

        int newLen = translatedBytes.Length;
        int originalLen = originalBytes.Length;

        // Case 1: Same length or shorter
        if (newLen <= originalLen)
        {
            Array.Copy(translatedBytes, 0, data, offset, newLen);
            for (int i = newLen; i < originalLen; i++)
                data[offset + i] = 0;
            return true;
        }

        // Case 2: Need more space - try length-prefixed format
        if (offset >= 4)
        {
            int lenFieldOff = (int)offset - 4;
            int storedLength = BitConverter.ToInt32(data, lenFieldOff);
            int actualTextLen = (storedLength - 2) * 2;

            if (Math.Abs(actualTextLen - originalLen + 2) <= 2)
            {
                int endPos = (int)offset + storedLength;
                int nullSpace = 0;
                for (int i = endPos; i < Math.Min(endPos + 128, data.Length); i += 2)
                {
                    if (data[i] == 0 && data[i + 1] == 0)
                        nullSpace += 2;
                    else
                        break;
                }

                int availableSpace = storedLength + nullSpace;
                if (newLen <= availableSpace)
                {
                    int newStoredLength = (newLen / 2) + 1;
                    BitConverter.GetBytes(newStoredLength).CopyTo(data, lenFieldOff);
                    Array.Copy(translatedBytes, 0, data, offset, newLen);
                    for (int i = newLen; i < availableSpace; i++)
                        data[offset + i] = 0;
                    return true;
                }
            }
        }

        return false;
    }

    private string? FindFile(string fileName)
    {
        var path = Path.Combine(_dataDir, fileName);
        if (File.Exists(path)) return path;

        var managedPath = Path.Combine(_dataDir, "Managed", fileName);
        if (File.Exists(managedPath)) return managedPath;

        return null;
    }
}

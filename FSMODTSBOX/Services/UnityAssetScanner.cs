using System.Collections.Concurrent;
using System.IO;
using System.Text;
using GameHanBox.Models;

namespace GameHanBox.Services;

public class UnityAssetScanner
{
    private static readonly string[] SkipExtensions = { ".resS", ".resource" };

    public async Task<List<FoundString>> ScanAllAsync(GameInfo gameInfo, IProgress<int>? progress = null)
    {
        var allStrings = new ConcurrentBag<FoundString>();

        // Filter out known binary-only files, sort by size (small first = smoother progress)
        var files = gameInfo.AssetFiles
            .Where(f => !SkipExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => new FileInfo(f).Length)
            .ToList();

        var totalFiles = files.Count;
        var processed = 0;

        // Process files sequentially with per-file chunk progress
        foreach (var file in files)
        {
            var fileProgress = new Progress<int>(pct =>
            {
                // Each file contributes equally to overall progress
                int overall = (processed * 100 + pct) / totalFiles;
                progress?.Report(Math.Min(99, overall));
            });

            try
            {
                var strings = await ScanFileAsync(file, fileProgress);
                foreach (var s in strings)
                    allStrings.Add(s);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning {file}: {ex.Message}");
            }

            Interlocked.Increment(ref processed);
            // Report completion of this file
            progress?.Report((processed * 100) / totalFiles);
        }

        // Also scan Assembly-CSharp.dll if available
        if (!string.IsNullOrEmpty(gameInfo.ManagedDir))
        {
            var asmPath = Path.Combine(gameInfo.ManagedDir, "Assembly-CSharp.dll");
            if (File.Exists(asmPath))
            {
                var asmStrings = ScanAssemblyCSharp(asmPath);
                foreach (var s in asmStrings)
                    allStrings.Add(s);
            }
        }

        var result = allStrings
            .Where(s => !string.IsNullOrWhiteSpace(s.OriginalText) && s.OriginalText.Length >= 4)
            .DistinctBy(s => (s.OriginalText, s.SourceFile, s.Offset))
            .OrderBy(s => s.SourceFile)
            .ThenBy(s => s.Offset)
            .ToList();

        progress?.Report(100);
        return result;
    }

    private async Task<List<FoundString>> ScanFileAsync(string filePath, IProgress<int>? progress = null)
    {
        var fileName = Path.GetFileName(filePath);
        var fi = new FileInfo(filePath);

        // For files > 5MB, use memory-mapped + chunked scanning
        if (fi.Length > 5 * 1024 * 1024)
            return await ScanLargeFileAsync(filePath, fileName, fi.Length, progress);

        // For smaller files, read whole file
        return await Task.Run(() =>
        {
            var data = File.ReadAllBytes(filePath);
            return ScanBytes(data, fileName);
        });
    }

    private async Task<List<FoundString>> ScanLargeFileAsync(string filePath, string fileName, long fileSize, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            var result = new List<FoundString>();
            var seenStrings = new HashSet<string>();

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            long bufferSize = Math.Min(fileSize, 4 * 1024 * 1024); // 4MB chunks
            byte[] buffer = new byte[bufferSize];
            byte[] overlap = new byte[6]; // preserve last 6 bytes for scanning continuity

            int overlapLen = 0;
            long totalRead = 0;

            while (totalRead < fileSize)
            {
                long toRead = Math.Min(bufferSize - overlapLen, fileSize - totalRead);
                // Copy overlap to start of buffer
                if (overlapLen > 0)
                    Array.Copy(overlap, 0, buffer, 0, overlapLen);

                int bytesRead = fs.Read(buffer, overlapLen, (int)toRead);
                if (bytesRead == 0) break;

                int effectiveLen = overlapLen + bytesRead;
                totalRead += bytesRead;

                // Scan this chunk
                var chunkStrings = ScanBytes(buffer.AsSpan(0, effectiveLen), fileName, seenStrings);
                result.AddRange(chunkStrings);

                // Save overlap for next chunk
                overlapLen = Math.Min(6, effectiveLen);
                if (overlapLen > 0)
                    Array.Copy(buffer, effectiveLen - overlapLen, overlap, 0, overlapLen);

                // Report progress
                int pct = (int)(totalRead * 100 / fileSize);
                progress?.Report(pct);
            }

            return result;
        });
    }

    private List<FoundString> ScanBytes(ReadOnlySpan<byte> data, string fileName)
    {
        return ScanBytes(data, fileName, new HashSet<string>());
    }

    private List<FoundString> ScanBytes(ReadOnlySpan<byte> data, string fileName, HashSet<string> seenStrings)
    {
        var result = new List<FoundString>();

        // Single pass: scan for UTF-16 LE strings
        int offset = 0;
        while (offset < data.Length - 4)
        {
            // Check for potential 4-byte length prefix
            int potentialLen = BitConverter.ToInt32(data.Slice(offset, 4));
            if (potentialLen >= 6 && potentialLen <= 500 && potentialLen % 2 == 0 &&
                offset + 4 + potentialLen <= data.Length)
            {
                // Try to decode as length-prefixed UTF-16 string
                int strStart = offset + 4;
                int strLen = potentialLen - 2; // minus null terminator
                if (TryDecodeUtf16(data.Slice(strStart, strLen), out var text, out _))
                {
                    if (IsGameText(text) && !seenStrings.Contains(text))
                    {
                        seenStrings.Add(text);
                        result.Add(new FoundString
                        {
                            SourceFile = fileName,
                            Offset = offset,
                            OriginalLength = potentialLen,
                            OriginalText = text
                        });
                    }
                    offset += 4 + potentialLen;
                    continue;
                }
            }

            // Check for sequential ASCII chars in UTF-16 LE
            if (data[offset] >= 0x20 && data[offset] <= 0x7E && data[offset + 1] == 0)
            {
                int start = offset;
                int end = start;
                while (end < data.Length - 1 && data[end] >= 0x20 && data[end] <= 0x7E && data[end + 1] == 0)
                    end += 2;

                int charLen = (end - start) / 2;
                if (charLen >= 6 && charLen <= 200)
                {
                    // Check that it actually terminates (null terminator or non-ASCII)
                    if (end >= data.Length || (data[end] == 0 && data[end + 1] == 0))
                    {
                        var chars = new char[charLen];
                        for (int j = 0; j < charLen; j++)
                            chars[j] = (char)data[start + j * 2];
                        var text = new string(chars);

                        if (IsGameText(text) && !seenStrings.Contains(text))
                        {
                            seenStrings.Add(text);
                            result.Add(new FoundString
                            {
                                SourceFile = fileName,
                                Offset = start,
                                OriginalLength = charLen * 2 + 2,
                                OriginalText = text
                            });
                        }
                    }
                }

                offset = end;
                continue;
            }

            offset += 2;
        }

        return result;
    }

    private static bool TryDecodeUtf16(ReadOnlySpan<byte> data, out string text, out bool allPrintable)
    {
        text = "";
        allPrintable = true;
        if (data.Length < 2 || data.Length % 2 != 0)
            return false;

        int charCount = data.Length / 2;
        var chars = new char[charCount];

        for (int i = 0; i < charCount; i++)
        {
            char c = (char)(data[i * 2] | (data[i * 2 + 1] << 8));
            if (c < 0x20 && c != '\n' && c != '\r' && c != '\t')
            {
                allPrintable = false;
                return false;
            }
            chars[i] = c;
        }

        text = new string(chars);
        return text.Any(char.IsLetter);
    }

    private List<FoundString> ScanAssemblyCSharp(string dllPath)
    {
        var result = new List<FoundString>();
        var fileName = Path.GetFileName(dllPath);

        try
        {
            var data = File.ReadAllBytes(dllPath);
            int bsjbIdx = IndexOfBytes(data, new byte[] { 0x42, 0x53, 0x4A, 0x42 });
            if (bsjbIdx < 0) return result;

            int verEnd = bsjbIdx + 16;
            while (verEnd < data.Length && data[verEnd] != 0) verEnd++;
            if (verEnd >= data.Length) return result;

            int padPos = verEnd + 1;
            while ((padPos - bsjbIdx) % 4 != 0) padPos++;
            if (padPos + 4 >= data.Length) return result;

            int numStreams = BitConverter.ToUInt16(data, padPos + 2);
            int hdrPos = padPos + 4;
            int? usOffset = null, usSize = null;

            for (int s = 0; s < numStreams; s++)
            {
                if (hdrPos + 8 >= data.Length) break;
                int off = BitConverter.ToInt32(data, hdrPos);
                int sz = BitConverter.ToInt32(data, hdrPos + 4);
                int nameStart = hdrPos + 8;
                int nameEnd = nameStart;
                while (nameEnd < data.Length && data[nameEnd] != 0) nameEnd++;
                string name = Encoding.ASCII.GetString(data, nameStart, nameEnd - nameStart);

                if (name == "#US") { usOffset = off; usSize = sz; break; }

                int next = nameEnd + 1;
                while ((next - hdrPos) % 4 != 0) next++;
                hdrPos = next;
            }

            if (usOffset == null || usSize == null) return result;

            int usAbs = bsjbIdx + usOffset.Value;
            int usEnd = usAbs + usSize.Value;
            int pos = usAbs;
            var seen = new HashSet<string>();

            while (pos < usEnd)
            {
                byte b = data[pos];
                if (b == 0) { pos++; continue; }

                int length;
                if ((b & 0x80) == 0) { length = b; pos++; }
                else if ((b & 0xC0) == 0x80) { length = ((b & 0x3F) << 8) | data[pos + 1]; pos += 2; }
                else { length = ((b & 0x1F) << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]; pos += 4; }

                int actualLen = length >> 1;
                bool isWide = (length & 1) == 1;

                if (actualLen <= 0 || pos + (isWide ? actualLen * 2 : actualLen) > usEnd) break;

                string text;
                if (isWide)
                    text = Encoding.Unicode.GetString(data, pos, actualLen * 2);
                else
                    text = Encoding.ASCII.GetString(data, pos, actualLen);

                pos += isWide ? actualLen * 2 : actualLen;

                if (actualLen >= 4 && !seen.Contains(text) && IsGameText(text))
                {
                    seen.Add(text);
                    result.Add(new FoundString
                    {
                        SourceFile = fileName,
                        Offset = pos - (isWide ? actualLen * 2 : actualLen),
                        OriginalLength = isWide ? actualLen * 2 + 2 : actualLen + 1,
                        OriginalText = text
                    });
                }
            }
        }
        catch { }

        return result;
    }

    private bool IsGameText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return false;
        if (!text.Any(char.IsLetter))
            return false;
        if (text.Contains('<') && text.Contains('>')) return false;
        if (text.Contains("//") || text.Contains("\\\\")) return false;
        if (text.StartsWith("UnityEngine.") || text.StartsWith("UnityEditor.")) return false;
        if (text.StartsWith("System.") || text.StartsWith("Microsoft.")) return false;
        if (text.StartsWith("mscorlib") || text.StartsWith("netstandard")) return false;
        if (text.All(c => char.IsUpper(c) || c == '_')) return false;
        if (text.Length >= 2 && text[0] == '<' && text[^1] == '>') return false;

        bool hasLower = text.Any(char.IsLower);
        bool hasUpper = text.Any(char.IsUpper);
        if (!hasLower && !hasUpper) return false;
        if (text.All(c => c >= 0x4E00 && c <= 0x9FFF)) return false;

        return true;
    }

    private static int IndexOfBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { found = false; break; }
            if (found) return i;
        }
        return -1;
    }
}

using System.Collections.Concurrent;
using System.Text;
using SongConverter.Models;
using SongConverter.Utils;

namespace SongConverter.Core;

public static class SongSorterCore
{
    public static string OrganizeSongs(string tempSongsDir, string destRootDir, string runId, Action<string>? logAction = null)
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var exportDir = Path.Combine(exeDir, "Export");
        if (!Directory.Exists(exportDir))
            throw new InvalidOperationException("Export フォルダが見つかりません。先に曲名リストを出力してください。");

        if (!Directory.Exists(tempSongsDir))
            throw new DirectoryNotFoundException("TempSongs フォルダが見つかりません: " + tempSongsDir);

        var songsRoot = ResolveSongsRoot(destRootDir);
        Directory.CreateDirectory(songsRoot);

        int totalCopied = 0;
        int totalSkipped = 0;
        int totalUnmatched = 0;
        var copyPathClaims = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        
        var mappings = new[]
        {
            new { Source = "01 Pop",               Dest = "00 ポップス",           Export = "ポップス",           BoxTitle = "ポップス",           BoxGenre = "ポップス",           BoxExplanation = "ポップスの曲をあつめたよ!" },
            new { Source = "04 Children and Folk", Dest = "01 キッズ",             Export = "キッズ",             BoxTitle = "キッズ",             BoxGenre = "キッズ",             BoxExplanation = "キッズの曲をあつめたよ!" },
            new { Source = "02 Anime",             Dest = "02 アニメ",             Export = "アニメ",             BoxTitle = "アニメ",             BoxGenre = "アニメ",             BoxExplanation = "アニメの曲をあつめたよ!" },
            new { Source = "03 Vocaloid",          Dest = "03 ボーカロイド曲",     Export = "ボーカロイド曲",     BoxTitle = "ボーカロイド™曲", BoxGenre = "ボーカロイド",     BoxExplanation = "ボーカロイド™の曲をあつめたよ!" },
            new { Source = "07 Game Music",        Dest = "04 ゲームミュージック", Export = "ゲームミュージック", BoxTitle = "ゲームミュージック", BoxGenre = "ゲームミュージック", BoxExplanation = "ゲームミュージックの曲をあつめたよ!" },
            new { Source = "05 Variety",           Dest = "05 バラエティ",         Export = "バラエティ",         BoxTitle = "バラエティ",         BoxGenre = "バラエティ",         BoxExplanation = "バラエティの曲をあつめたよ!" },
            new { Source = "09 Namco Original",    Dest = "07 ナムコオリジナル",   Export = "ナムコオリジナル",   BoxTitle = "ナムコオリジナル",   BoxGenre = "ナムコオリジナル",   BoxExplanation = "ナムコオリジナルの曲をあつめたよ!" },
            new { Source = "06 Classical",         Dest = "06 クラシック",         Export = "クラシック",         BoxTitle = "クラシック",         BoxGenre = "クラシック",         BoxExplanation = "クラシックの曲をあつめたよ!" },
        };

        var exportGroups = LoadExportIndexes(exportDir);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Math.Min(Environment.ProcessorCount * 2, 16)) };

        foreach (var sourceMap in mappings)
        {
            var srcCatDir = Path.Combine(tempSongsDir, sourceMap.Source);
            if (!Directory.Exists(srcCatDir)) continue;

            var songDirs = Directory.GetDirectories(srcCatDir);
            Parallel.ForEach(songDirs, parallelOptions, songDir =>
            {
                var tjaPaths = Directory.GetFiles(songDir, "*.tja", SearchOption.AllDirectories);
                if (tjaPaths.Length == 0) { Interlocked.Increment(ref totalUnmatched); return; }

                var candidates = new List<(string Path, SongDetail Info, string TitleNorm, string SubtitleNorm, string FullTitleNorm)>();
                foreach (var path in tjaPaths)
                {
                    var info = ReadSongInfo(path);
                    if (info == null) continue;
                    candidates.Add((path, info, NormalizationUtils.NormalizeTitle(info.Title), NormalizationUtils.NormalizeSubtitle(info.Subtitle), NormalizationUtils.NormalizeTitle(info.FullTitle ?? info.Title)));
                }

                if (candidates.Count == 0) { Interlocked.Increment(ref totalUnmatched); return; }

                var matchedAnyInFolder = false;
                foreach (var target in mappings)
                {
                    if (!exportGroups.TryGetValue(target.Export, out var songsByTitle)) continue;

                    foreach (var candidate in candidates)
                    {
                        List<(string SubtitleNorm, int Index)>? versions = null;
                        var lookupKeys = BuildTitleLookupKeys(candidate.TitleNorm, candidate.SubtitleNorm, candidate.FullTitleNorm);
                        foreach (var titleKey in lookupKeys)
                        {
                            if (songsByTitle.TryGetValue(titleKey, out var found)) { versions = found; break; }
                        }

                        if (versions == null) continue;
                        matchedAnyInFolder = true;

                        var match = versions.FirstOrDefault(v => v.SubtitleNorm == candidate.SubtitleNorm);
                        if (match.Index == 0 && versions.Count == 1) match = versions[0];
                        if (match.Index == 0) continue;

                        var num = match.Index.ToString("000");
                        var dstGenreDir = Path.Combine(songsRoot, target.Dest);
                        var folderName = NormalizationUtils.SanitizeFolderName($"{num} {candidate.Info.FolderTitle}");
                        var dstSongDir = Path.Combine(dstGenreDir, folderName);

                        if (Directory.Exists(dstSongDir)) { Interlocked.Increment(ref totalSkipped); continue; }
                        if (!copyPathClaims.TryAdd(dstSongDir, 0)) { Interlocked.Increment(ref totalSkipped); continue; }

                        EnsureBoxDef(dstGenreDir, target.BoxTitle, target.BoxGenre, target.BoxExplanation);
                        CopyDirectory(songDir, dstSongDir);
                        Interlocked.Increment(ref totalCopied);
                    }
                }
                if (!matchedAnyInFolder) Interlocked.Increment(ref totalUnmatched);
            });
        }

        return $"コピー完了 {totalCopied} 曲（既設 {totalSkipped} 曲 / 未マッチ {totalUnmatched} 件）";
    }

    private static string ResolveSongsRoot(string selectedFolder)
    {
        try
        {
            var name = new DirectoryInfo(selectedFolder).Name;
            if (string.Equals(name, "Songs", StringComparison.OrdinalIgnoreCase))
                return selectedFolder;
        }
        catch { }
        return Path.Combine(selectedFolder, "Songs");
    }

    private static Dictionary<string, Dictionary<string, List<(string SubtitleNorm, int Index)>>> LoadExportIndexes(string exportDir)
    {
        var result = new Dictionary<string, Dictionary<string, List<(string SubtitleNorm, int Index)>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in SongListBase.Categories)
        {
            var filePath = Path.Combine(exportDir, $"songlist_{cat.DisplayName}.txt");
            if (!File.Exists(filePath)) continue;

            var songsByTitle = new Dictionary<string, List<(string SubtitleNorm, int Index)>>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split('\t');
                if (parts.Length < 2) continue;
                var idStr = parts[0];
                var title = parts[1];
                var subtitle = parts.Length > 2 ? parts[2] : "";

                var titleNorm = NormalizationUtils.NormalizeTitle(title);
                var subNorm = NormalizationUtils.NormalizeSubtitle(subtitle);
                var idx = int.TryParse(idStr, out var n) ? n : 0;

                foreach (var key in NormalizationUtils.ExpandTitleMatchKeys(titleNorm))
                {
                    if (!songsByTitle.TryGetValue(key, out var v)) { v = new(); songsByTitle[key] = v; }
                    v.Add((subNorm, idx));
                }
            }
            result[cat.DisplayName] = songsByTitle;
        }
        return result;
    }

    private static string[] BuildTitleLookupKeys(string titleNorm, string subtitleNorm, string fullTitleNorm)
    {
        var keys = new List<string>();
        foreach (var key in NormalizationUtils.ExpandTitleMatchKeys(titleNorm)) keys.Add(key);
        if (!string.IsNullOrEmpty(fullTitleNorm) && fullTitleNorm != titleNorm)
            foreach (var key in NormalizationUtils.ExpandTitleMatchKeys(fullTitleNorm)) keys.Add(key);
        return keys.Distinct().ToArray();
    }

    private static SongDetail? ReadSongInfo(string tjaPath)
    {
        // Retry logic for robustness
        for (int i = 0; i < 3; i++)
        {
            try
            {
                var lines = File.ReadAllLines(tjaPath, Encoding.UTF8);
                if (lines.Any(l => l.Contains('\uFFFD'))) 
                {
                    // Fallback to Shift-JIS
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    lines = File.ReadAllLines(tjaPath, Encoding.GetEncoding(932));
                }
                
                string? title = null, titleJa = null, sub = null, subJa = null;
                foreach (var l in lines)
                {
                    var trim = l.Trim();
                    if (trim.StartsWith("TITLEJA:", StringComparison.OrdinalIgnoreCase)) titleJa = trim["TITLEJA:".Length..].Trim();
                    else if (trim.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase)) title = trim["TITLE:".Length..].Trim();
                    else if (trim.StartsWith("SUBTITLEJA:", StringComparison.OrdinalIgnoreCase)) subJa = trim["SUBTITLEJA:".Length..].Trim();
                    else if (trim.StartsWith("SUBTITLE:", StringComparison.OrdinalIgnoreCase)) sub = trim["SUBTITLE:".Length..].Trim();
                }
                var resT = titleJa ?? title;
                if (resT == null) return null;
                return new SongDetail(resT, subJa ?? sub ?? "", resT, titleJa ?? resT);
            }
            catch (IOException) { Thread.Sleep(50); }
            catch { return null; }
        }
        return null;
    }

    private static void EnsureBoxDef(string dir, string title, string genre, string explanation)
    {
        var path = Path.Combine(dir, "box.def");
        if (File.Exists(path)) return;
        Directory.CreateDirectory(dir);
        File.WriteAllLines(path, new[] { "#TITLE:" + title, "#GENRE:" + genre, "#EXPLANATION:" + explanation, "#BGCOLOR:#ff0000", "#TEXTCOLOR:#ffffff" });
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
        {
            var dFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, dFile, true);
        }
        foreach (var d in Directory.GetDirectories(src))
        {
            CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }
    }
}

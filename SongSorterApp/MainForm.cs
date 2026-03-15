using System.Text;
using System.Text.Json;

namespace SongSorterApp;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
        SetStatus("準備完了", showProgress: false);
    }

    async Task<(int fileCount, int totalTitles)> ExportSongListsAsync()
    {
        var exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
        var exportDir = Path.Combine(exeDir, "Export");
        Directory.CreateDirectory(exportDir);

        int fileCount = 0;
        int totalTitles = 0;

        var totalCats = SongListFetcher.Categories.Length;
        SetStatus("曲リスト取得中…", showProgress: true, progressStyle: ProgressBarStyle.Blocks, progressMax: totalCats, progressValue: 0);

        int done = 0;
        foreach (var cat in SongListFetcher.Categories)
        {
            statusLabel.Text = $"取得中… {cat.DisplayName}";
            var titles = await SongListFetcher.FetchTitlesAsync(cat.FileName);
            if (titles.Count == 0)
                continue;

            var fileName = $"songlist_{cat.DisplayName}.txt";
            var filePath = Path.Combine(exportDir, fileName);
            await File.WriteAllLinesAsync(filePath, titles, Encoding.UTF8);

            fileCount++;
            totalTitles += titles.Count;

            done++;
            statusProgress.Value = Math.Min(statusProgress.Maximum, done);
        }

        SetStatus($"曲リスト取得完了（{fileCount} 件 / {totalTitles} 曲）", showProgress: false);
        return (fileCount, totalTitles);
    }

    async void btnOrganize_Click(object? sender, EventArgs e)
    {
        var recent = LoadRecentPaths();
        using var dlgTemp = new FolderBrowserDialog
        {
            Description = "コピー元のフォルダを選択",
            SelectedPath = GetExistingPathOrEmpty(recent.TempSongs)
        };
        if (dlgTemp.ShowDialog() != DialogResult.OK)
            return;

        using var dlgRoot = new FolderBrowserDialog
        {
            Description = "コピー後のフォルダを選択",
            SelectedPath = GetExistingPathOrEmpty(recent.TaikoRoot)
        };
        if (dlgRoot.ShowDialog() != DialogResult.OK)
            return;

        var tempSongsDir = dlgTemp.SelectedPath;
        var destRootDir = dlgRoot.SelectedPath;
        SaveRecentPaths(new RecentPaths
        {
            TempSongs = tempSongsDir,
            TaikoRoot = destRootDir
        });

        btnOrganize.Enabled = false;
        try
        {
            await ExportSongListsAsync();

            SetStatus("Songs フォルダへコピー中…", showProgress: true, progressStyle: ProgressBarStyle.Marquee);
            var summary = await Task.Run(() => OrganizeSongs(tempSongsDir, destRootDir));
            SetStatus(summary, showProgress: false);
        }
        catch (Exception ex)
        {
            SetStatus("エラー: " + ex.Message, showProgress: false);
            MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnOrganize.Enabled = true;
        }
    }

    static string OrganizeSongs(string tempSongsDir, string destRootDir)
    {
        var exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
        var exportDir = Path.Combine(exeDir, "Export");
        if (!Directory.Exists(exportDir))
            throw new InvalidOperationException("Export フォルダが見つかりません。先に曲名リストを出力してください。");

        if (!Directory.Exists(tempSongsDir))
            throw new DirectoryNotFoundException("TempSongs フォルダが見つかりません: " + tempSongsDir);

        var songsRoot = ResolveSongsRoot(destRootDir);
        Directory.CreateDirectory(songsRoot);

        int totalCopied = 0;
        int totalUnmatched = 0;
        var unmatchedLogs = new List<string>();

        var mappings = new[]
        {
            new { Source = "01 Pop",               Dest = "00 ポップス",           Export = "ポップス",           BoxTitle = "ポップス",           BoxGenre = "ポップス",           BoxExplanation = "ポップスの曲をあつめたよ!" },
            new { Source = "04 Children and Folk", Dest = "01 キッズ",             Export = "キッズ",             BoxTitle = "キッズ",             BoxGenre = "キッズ",             BoxExplanation = "キッズの曲をあつめたよ!" },
            new { Source = "02 Anime",             Dest = "02 アニメ",             Export = "アニメ",             BoxTitle = "アニメ",             BoxGenre = "アニメ",             BoxExplanation = "アニメの曲をあつめたよ!" },
            new { Source = "03 Vocaloid",          Dest = "03 ボーカロイド曲",     Export = "ボーカロイド曲",     BoxTitle = "ボーカロイド™曲", BoxGenre = "ボーカロイド",     BoxExplanation = "ボーカロイド™の曲をあつめたよ!" },
            new { Source = "07 Game Music",        Dest = "04 ゲームミュージック", Export = "ゲームミュージック", BoxTitle = "ゲームミュージック", BoxGenre = "ゲームミュージック", BoxExplanation = "ゲームミュージックの曲をあつめたよ!" },
            new { Source = "05 Variety",           Dest = "05 バラエティ",         Export = "バラエティ",         BoxTitle = "バラエティ",         BoxGenre = "バラエティ",         BoxExplanation = "バラエティの曲をあつめたよ!" },
            new { Source = "06 Classical",         Dest = "06 クラシック",         Export = "クラシック",         BoxTitle = "クラシック",         BoxGenre = "クラシック",         BoxExplanation = "クラシックの曲をあつめたよ!" },
            new { Source = "09 Namco Original",    Dest = "07 ナムコオリジナル",   Export = "ナムコオリジナル",   BoxTitle = "ナムコオリジナル",   BoxGenre = "ナムコオリジナル",   BoxExplanation = "ナムコオリジナルの曲をあつめたよ!" },
        };

        var exportIndexes = LoadExportIndexes(exportDir);

        foreach (var m in mappings)
        {
            var srcCatDir = Path.Combine(tempSongsDir, m.Source);
            if (!Directory.Exists(srcCatDir))
                continue;

            var targets = mappings;

            foreach (var songDir in Directory.GetDirectories(srcCatDir))
            {
                var tjaPath = Directory.GetFiles(songDir, "*.tja", SearchOption.AllDirectories).FirstOrDefault();
                if (tjaPath is null)
                {
                    totalUnmatched++;
                    unmatchedLogs.Add($"[NO_TJA] {songDir}");
                    continue;
                }

                var titleJaRaw = ReadTitleJa(tjaPath);
                var titleJa = NormalizeTitle(titleJaRaw ?? string.Empty);
                if (string.IsNullOrEmpty(titleJa))
                {
                    totalUnmatched++;
                    unmatchedLogs.Add($"[NO_TITLE] {songDir}");
                    continue;
                }

                var matchedAny = false;
                foreach (var target in targets)
                {
                    if (!exportIndexes.TryGetValue(target.Export, out var indexByTitle))
                        continue;
                    if (!indexByTitle.TryGetValue(titleJa, out var idx))
                        continue;

                    matchedAny = true;
                    var dstCatDir = Path.Combine(songsRoot, target.Dest);
                    Directory.CreateDirectory(dstCatDir);
                    EnsureBoxDef(dstCatDir, target.BoxTitle, target.BoxGenre, target.BoxExplanation);

                    var num = idx.ToString("000");
                    var safeTitle = SanitizeFolderName(titleJa);
                    var newFolderName = $"{num} {safeTitle}";
                    var dstSongDir = Path.Combine(dstCatDir, newFolderName);
                    if (Directory.Exists(dstSongDir))
                        continue;

                    CopyDirectory(songDir, dstSongDir);
                    totalCopied++;
                }

                if (!matchedAny)
                {
                    totalUnmatched++;
                    unmatchedLogs.Add($"[NO_MATCH] {titleJa} ({songDir})");
                }
            }
        }

        if (unmatchedLogs.Count > 0)
        {
            var logPath = Path.Combine(exportDir, "log.txt");
            File.WriteAllLines(logPath, unmatchedLogs, Encoding.UTF8);
        }

        return $"コピー完了: {totalCopied} 曲 (未マッチ {totalUnmatched} 件)";
    }

    static Dictionary<string, Dictionary<string, int>> LoadExportIndexes(string exportDir)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in SongListFetcher.Categories)
        {
            var fileName = $"songlist_{cat.DisplayName}.txt";
            var filePath = Path.Combine(exportDir, fileName);
            if (!File.Exists(filePath))
                continue;

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            var indexByTitle = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Length; i++)
            {
                var title = NormalizeTitle(lines[i]);
                if (string.IsNullOrEmpty(title))
                    continue;
                if (!indexByTitle.ContainsKey(title))
                    indexByTitle[title] = i + 1;
            }

            result[cat.DisplayName] = indexByTitle;
        }

        return result;
    }

    static string? ReadTitleJa(string tjaPath)
    {
        string text;
        try
        {
            var sjis = Encoding.GetEncoding(932);
            text = File.ReadAllText(tjaPath, sjis);
        }
        catch
        {
            text = File.ReadAllText(tjaPath, Encoding.UTF8);
        }

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.TrimStart();
            if (line.StartsWith("TITLEJA:", StringComparison.OrdinalIgnoreCase))
            {
                return line["TITLEJA:".Length..].Trim();
            }
        }

        return null;
    }

    static string NormalizeTitle(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var work = s.Trim().Replace('　', ' ');
        work = string.Join(" ", work.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
        return work.ToUpperInvariant();
    }

    static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name);
        foreach (var c in invalid)
        {
            sb.Replace(c.ToString(), "");
        }

        var result = sb.ToString().TrimEnd(' ', '.');
        return string.IsNullOrWhiteSpace(result) ? "NoName" : result;
    }

    static void EnsureBoxDef(string dstCatDir, string title, string genre, string explanation)
    {
        var destBox = Path.Combine(dstCatDir, "box.def");
        if (File.Exists(destBox))
            return;

        var lines = new[]
        {
            $"#TITLE:{title}",
            $"#GENRE:{genre}",
            $"#EXPLANATION:{explanation}"
        };
        File.WriteAllLines(destBox, lines, Encoding.UTF8);
    }

    static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destDir, name);
            File.Copy(file, dest, overwrite: false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var destSub = Path.Combine(destDir, name);
            CopyDirectory(dir, destSub);
        }
    }

    sealed class RecentPaths
    {
        public string? TempSongs { get; set; }
        public string? TaikoRoot { get; set; }
    }

    static string GetRecentPathsFile()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SongSorterApp");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "recent_paths.json");
    }

    static RecentPaths LoadRecentPaths()
    {
        var path = GetRecentPathsFile();
        if (!File.Exists(path))
            return new RecentPaths();
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<RecentPaths>(json) ?? new RecentPaths();
        }
        catch
        {
            return new RecentPaths();
        }
    }

    static void SaveRecentPaths(RecentPaths paths)
    {
        var path = GetRecentPathsFile();
        var json = JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    static string GetExistingPathOrEmpty(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : string.Empty;
    }

    static string ResolveSongsRoot(string selectedFolder)
    {
        try
        {
            var name = new DirectoryInfo(selectedFolder).Name;
            if (string.Equals(name, "Songs", StringComparison.OrdinalIgnoreCase))
                return selectedFolder;
        }
        catch
        {
            // fallback to combine below
        }

        return Path.Combine(selectedFolder, "Songs");
    }

    void SetStatus(string text, bool showProgress, ProgressBarStyle progressStyle = ProgressBarStyle.Marquee, int? progressMax = null, int? progressValue = null)
    {
        statusLabel.Text = text;
        statusProgress.Visible = showProgress;
        statusProgress.Style = progressStyle;
        if (progressMax.HasValue)
            statusProgress.Maximum = Math.Max(1, progressMax.Value);
        if (progressValue.HasValue)
            statusProgress.Value = Math.Min(statusProgress.Maximum, Math.Max(0, progressValue.Value));
    }
}

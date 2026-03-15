using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;

namespace SongSorterApp;

/// <summary>
/// 太鼓の達人 楽曲リストページから曲名を抽出する
/// </summary>
public static class SongListFetcher
{
    const string BaseUrl = "https://taiko.namco-ch.net/taiko/songlist/";
    static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" } }
    };

    static readonly HashSet<string> SkipTitles = new(StringComparer.Ordinal) { "曲名", "難易度" };

    /// <summary>
    /// カテゴリ一覧（表示名, ファイル名）
    /// </summary>
    public static ImmutableArray<(string DisplayName, string FileName)> Categories { get; } = ImmutableArray.Create(
        ("ナムコオリジナル", "namco.php"),
        ("ポップス", "pops.php"),
        ("キッズ", "kids.php"),
        ("アニメ", "anime.php"),
        ("ボーカロイド曲", "vocaloid.php"),
        ("ゲームミュージック", "game.php"),
        ("バラエティ", "variety.php"),
        ("クラシック", "classic.php")
    );

    public static string GetUrl(string fileName) => BaseUrl + fileName;

    /// <summary>
    /// 指定カテゴリのHTMLを取得し、曲名リストを返す
    /// </summary>
    public static async Task<List<string>> FetchTitlesAsync(string fileName, CancellationToken ct = default)
    {
        var url = GetUrl(fileName);
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var encoding = response.Content.Headers.ContentType?.CharSet != null
            ? Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
            : Encoding.UTF8;
        var html = encoding.GetString(bytes);

        return ExtractTitles(html);
    }

    /// <summary>
    /// HTMLから &lt;th&gt; の最初のテキストノードのみを曲名として抽出
    /// </summary>
    public static List<string> ExtractTitles(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var titles = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var th in doc.DocumentNode.SelectNodes("//th") ?? Enumerable.Empty<HtmlNode>())
        {
            var title = GetFirstTextFromTh(th);
            if (string.IsNullOrWhiteSpace(title)) continue;
            title = title.Trim();
            if (SkipTitles.Contains(title)) continue;
            if (seen.Add(title))
                titles.Add(title);
        }

        return titles;
    }

    static string? GetFirstTextFromTh(HtmlNode th)
    {
        foreach (var node in th.ChildNodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = node.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            else
                break; // 最初の要素タグで終了（span, p などは曲名に含めない）
        }
        return null;
    }
}

using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;
using SongConverter.Models;
using SongConverter.Utils;

namespace SongConverter.Core;

public static class SongListBase
{
    public const string BaseUrl = "https://taiko.namco-ch.net/taiko/songlist/";
    
    public static readonly ImmutableArray<(string DisplayName, string FileName)> Categories = ImmutableArray.Create(
        ("ナムコオリジナル", "namco.php"),
        ("ポップス", "pops.php"),
        ("キッズ", "kids.php"),
        ("アニメ", "anime.php"),
        ("ボーカロイド曲", "vocaloid.php"),
        ("ゲームミュージック", "game.php"),
        ("バラエティ", "variety.php"),
        ("クラシック", "classic.php")
    );
}

public static class SongListFetcher
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" } }
    };

    private static readonly HashSet<string> SkipTitles = new(StringComparer.Ordinal) { "曲名", "難易度" };

    public static async Task<List<SongInfo>> FetchSongsAsync(string fileName, CancellationToken ct = default)
    {
        var url = SongListBase.BaseUrl + fileName;
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var encoding = response.Content.Headers.ContentType?.CharSet != null
            ? Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet)
            : Encoding.UTF8;
        var html = encoding.GetString(bytes);

        return ExtractSongs(html);
    }

    public static List<SongInfo> ExtractSongs(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var songs = new List<SongInfo>();
        var seenNormalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var th in doc.DocumentNode.SelectNodes("//th") ?? Enumerable.Empty<HtmlNode>())
        {
            var title = GetTitleFromTh(th);
            if (string.IsNullOrWhiteSpace(title)) continue;
            title = NormalizeCellText(title);
             
            if (SkipTitles.Contains(title)) continue;
            if (title.Contains("ショップ") || title.Contains("AIバトル") || title.Contains("アイコンの説明") || title.Contains("どんメダル")) continue;
            if (title == "曲名" || title == "難易度" || title.StartsWith("各アイコン")) continue;

            var subtitleNode = th.SelectSingleNode(".//p");
            var subtitle = subtitleNode != null ? NormalizeCellText(subtitleNode.InnerText) : string.Empty;

            var normKey = $"{NormalizationUtils.NormalizeTitle(title)}|{NormalizationUtils.NormalizeSubtitle(subtitle)}";
             
            if (seenNormalized.Add(normKey))
            {
                songs.Add(new SongInfo(title, subtitle));
            }
        }

        return songs;
    }

    private static string? GetTitleFromTh(HtmlNode th)
    {
        var pieces = new List<string>();
        foreach (var node in th.ChildNodes)
        {
            if (node.Name.Equals("p", StringComparison.OrdinalIgnoreCase))
                break; 

            if (ShouldSkipTitleNode(node))
                continue;

            var text = NormalizeCellText(node.InnerText);
            if (string.IsNullOrEmpty(text))
                continue;

            pieces.Add(text);
        }

        var res = NormalizeCellText(string.Join(" ", pieces));
        return string.IsNullOrEmpty(res) ? null : res;
    }

    private static bool ShouldSkipTitleNode(HtmlNode node)
    {
        if (!node.Name.Equals("span", StringComparison.OrdinalIgnoreCase))
            return false;

        var className = node.GetAttributeValue("class", string.Empty);
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return className.Contains("new", StringComparison.OrdinalIgnoreCase)
               || className.Contains("ico", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCellText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var decoded = System.Net.WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        return string.Join(" ", decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

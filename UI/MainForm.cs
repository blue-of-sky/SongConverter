using System.Text;
using System.Text.Json;
using SongConverter.Core;

namespace SongConverter.UI;

public partial class MainForm : Form
{
    private string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public MainForm()
    {
        InitializeComponent();
        if (File.Exists("SongConverter.ico"))
        {
            this.Icon = new Icon("SongConverter.ico");
        }
        logBox.Items.Add("ここにログが表示されます。");
        LoadSettings();
        
        btnBrowseTemp.Click += (s, e) => BrowseFolder(txtTempSongs);
        btnBrowseRoot.Click += (s, e) => BrowseFolder(txtTaikoRoot);
        btnBrowseDanSongs.Click += (s, e) => BrowseFolder(txtDanSongsPath);
        
        btnFetchLists.Click += async (s, e) => await OnFetchListsClick();
        btnOrganize.Click += async (s, e) => await OnOrganizeClick();
        btnGenerateDan.Click += async (s, e) => await OnGenerateDanClick();
    }

    private void BrowseFolder(TextBox target)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            target.Text = fbd.SelectedPath;
            SaveSettings();
        }
    }

    private void Log(string msg)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => Log(msg)));
            return;
        }
        logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        logBox.SelectedIndex = logBox.Items.Count - 1;
    }

    private void SetStatus(string msg, bool showProgress = false)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => SetStatus(msg, showProgress)));
            return;
        }
        statusLabel.Text = msg;
        progressBar.Visible = showProgress;
    }

    private async Task OnFetchListsClick()
    {
        btnFetchLists.Enabled = false;
        SetStatus("曲リスト取得中...", true);
        Log("公式楽曲リストの取得を開始します...");

        try
        {
            var exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
            Directory.CreateDirectory(exportDir);

            foreach (var cat in SongListBase.Categories)
            {
                Log($"取得中: {cat.DisplayName}");
                var songs = await SongListFetcher.FetchSongsAsync(cat.FileName);
                var filePath = Path.Combine(exportDir, $"songlist_{cat.DisplayName}.txt");
                var lines = songs.Select((s, i) => $"{i + 1:000}\t{s.Title}\t{s.Subtitle}");
                await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
            }
            Log("全ての楽曲リストを取得・保存しました。");
            MessageBox.Show("楽曲リストの更新が完了しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
            MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFetchLists.Enabled = true;
            SetStatus("準備完了");
        }
    }

    private async Task OnOrganizeClick()
    {
        if (string.IsNullOrWhiteSpace(txtTempSongs.Text) || string.IsNullOrWhiteSpace(txtTaikoRoot.Text))
        {
            MessageBox.Show("文件夹パスを指定してください。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnOrganize.Enabled = false;
        SetStatus("整理実行中...", true);
        Log("楽曲の整理・コピーを開始します...");

        try
        {
            string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string result = await Task.Run(() => SongSorterCore.OrganizeSongs(txtTempSongs.Text, txtTaikoRoot.Text, runId, Log));
            Log(result);
            MessageBox.Show(result, "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
            MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnOrganize.Enabled = true;
            SetStatus("準備完了");
        }
    }

    private async Task OnGenerateDanClick()
    {
        btnGenerateDan.Enabled = false;
        SetStatus("Dan.json 生成中...", true);
        Log("段位データの生成を開始します...");

        try
        {
            string subDir = txtDanOutputSub.Text.Trim();
            if (string.IsNullOrEmpty(subDir)) subDir = "Default";
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DanLists", subDir);
            
            await DanGeneratorCore.GenerateAsync(txtWikiUrl.Text, outputDir, txtDanSongsPath.Text, Log);
            Log("段位データの生成が完了しました。");
            Log($"保存先: {outputDir}");
            MessageBox.Show("Dan.json の生成が完了しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
            MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnGenerateDan.Enabled = true;
            SetStatus("準備完了");
        }
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            TempSongs = txtTempSongs.Text,
            TaikoRoot = txtTaikoRoot.Text,
            DanSongs = txtDanSongsPath.Text,
            WikiUrl = txtWikiUrl.Text,
            OutputSub = txtDanOutputSub.Text
        };
        string json = JsonSerializer.Serialize(settings);
        File.WriteAllText(SettingsPath, json);
    }

    private void LoadSettings()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    txtTempSongs.Text = settings.TempSongs;
                    txtTaikoRoot.Text = settings.TaikoRoot;
                    txtDanSongsPath.Text = settings.DanSongs;
                    txtWikiUrl.Text = settings.WikiUrl ?? txtWikiUrl.Text;
                    txtDanOutputSub.Text = settings.OutputSub ?? txtDanOutputSub.Text;
                }
            }
            catch { }
        }
    }

    class AppSettings
    {
        public string TempSongs { get; set; } = "";
        public string TaikoRoot { get; set; } = "";
        public string DanSongs { get; set; } = "";
        public string WikiUrl { get; set; } = "";
        public string OutputSub { get; set; } = "";
    }
}

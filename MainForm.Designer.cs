namespace SongSorterApp;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        btnOrganize = new Button();
        txtInstructions = new TextBox();
        lblStatus = new Label();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        statusProgress = new ToolStripProgressBar();
        SuspendLayout();
        //
        // btnOrganize
        //
        btnOrganize.Location = new Point(84, 20);
        btnOrganize.Name = "btnOrganize";
        btnOrganize.Size = new Size(353, 36);
        btnOrganize.TabIndex = 0;
        btnOrganize.Text = "並び替え";
        btnOrganize.UseVisualStyleBackColor = true;
        btnOrganize.Click += btnOrganize_Click;
        //
        // txtInstructions
        //
        txtInstructions.Location = new Point(45, 70);
        txtInstructions.Name = "txtInstructions";
        txtInstructions.ReadOnly = true;
        txtInstructions.Multiline = true;
        txtInstructions.Enabled = true;
        txtInstructions.Cursor = Cursors.IBeam;
        txtInstructions.ScrollBars = ScrollBars.None;
        txtInstructions.WordWrap = true;
        txtInstructions.ShortcutsEnabled = true;
        txtInstructions.TabStop = false;
        txtInstructions.BorderStyle = BorderStyle.None;
        txtInstructions.Size = new Size(430, 150);
        txtInstructions.BackColor = SystemColors.Control;
        txtInstructions.TabIndex = 1;
                        txtInstructions.Text = "1. \u597d\u304d\u306a\u6240\u3067\u30b3\u30de\u30f3\u30c9\u30d7\u30ed\u30f3\u30d7\u30c8\u3092\u958b\u3044\u3066\r\n" +
                                "   git clone --depth 1 -b master https://ese.tjadataba.se/ESE/ESE.git Songs \u3092\u5b9f\u884c\u3057\u307e\u3059\r\n" +
                                "2. \u4e26\u3073\u66ff\u3048\u30dc\u30bf\u30f3\u3092\u62bc\u3057\u3066\u5148\u307b\u3069\u4f5c\u3063\u305fSongs\u30d5\u30a9\u30eb\u30c0\u3092\u9078\u629e\u3057\u307e\u3059\r\n" +
                                "3. \u66f2\u3092\u30b3\u30d4\u30fc\u3057\u305f\u3044\u592a\u9f13\u30b7\u30df\u30e5\u306e\u30d5\u30a9\u30eb\u30c0(TaikoNautsやOpenTaikoフ\u30a9\u30eb\u30c0)\u3092\u9078\u629e\u3057\u307e\u3059\r\n" +
                                "\u3059\u308b\u3068\u66f2\u306e\u9806\u756a\u304c\u672c\u5bb6\u901a\u308a\u306b\u306a\u308a\u307e\u3059";
        //
        // lblStatus
        //
        lblStatus.AutoSize = true;
        lblStatus.Location = new Point(20, 235);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(80, 15);
        lblStatus.TabIndex = 2;
        lblStatus.Text = "";
        lblStatus.Visible = false;
        //
        // statusStrip
        //
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });
        statusStrip.Location = new Point(0, 258);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(520, 22);
        statusStrip.TabIndex = 3;
        statusStrip.Text = "statusStrip";
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(56, 17);
        statusLabel.Text = "準備完了";
        //
        // statusProgress
        //
        statusProgress.Name = "statusProgress";
        statusProgress.Size = new Size(100, 16);
        statusProgress.Style = ProgressBarStyle.Marquee;
        statusProgress.Visible = false;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 280);
        Controls.Add(statusStrip);
        Controls.Add(lblStatus);
        Controls.Add(txtInstructions);
        Controls.Add(btnOrganize);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimumSize = new Size(530, 300);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "曲本家化アプリ";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Button btnOrganize;
    private TextBox txtInstructions;
    private Label lblStatus;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
    private ToolStripProgressBar statusProgress;
}

#nullable enable
using Microsoft.Web.WebView2.WinForms;

namespace GitHubAutoApprove;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private ToolStrip toolStrip = null!;
    private ToolStripLabel lblOwner = null!;
    private ToolStripTextBox txtOwner = null!;
    private ToolStripLabel lblRepo = null!;
    private ToolStripTextBox txtRepo = null!;
    private ToolStripLabel lblInterval = null!;
    private ToolStripTextBox txtInterval = null!;
    private ToolStripButton btnSave = null!;
    private ToolStripSeparator sep1 = null!;
    private ToolStripButton btnStart = null!;
    private ToolStripButton btnStop = null!;
    private ToolStripButton btnRunOnce = null!;
    private ToolStripSeparator sep2 = null!;
    private ToolStripButton btnOpenActions = null!;
    private ToolStripButton btnLogin = null!;
    private ToolStripButton btnClearLog = null!;
    private ToolStripLabel lblStatus = null!;

    private SplitContainer splitContainer = null!;
    private WebView2 webView = null!;
    private TextBox txtLog = null!;
    private System.Windows.Forms.Timer pollTimer = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        toolStrip = new ToolStrip();
        lblOwner = new ToolStripLabel("Owner:");
        txtOwner = new ToolStripTextBox { Size = new Size(120, 23) };
        lblRepo = new ToolStripLabel("Repo:");
        txtRepo = new ToolStripTextBox { Size = new Size(140, 23) };
        lblInterval = new ToolStripLabel("间隔(秒):");
        txtInterval = new ToolStripTextBox { Size = new Size(50, 23), Text = "30" };
        btnSave = new ToolStripButton("保存配置");
        sep1 = new ToolStripSeparator();
        btnStart = new ToolStripButton("▶ 开始轮询");
        btnStop = new ToolStripButton("■ 停止") { Enabled = false };
        btnRunOnce = new ToolStripButton("立即扫描一次");
        sep2 = new ToolStripSeparator();
        btnOpenActions = new ToolStripButton("打开 Actions 页");
        btnLogin = new ToolStripButton("打开 GitHub 登录");
        btnClearLog = new ToolStripButton("清空日志");
        lblStatus = new ToolStripLabel("状态: 未启动") { Alignment = ToolStripItemAlignment.Right };

        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            lblOwner, txtOwner, lblRepo, txtRepo, lblInterval, txtInterval, btnSave,
            sep1, btnStart, btnStop, btnRunOnce,
            sep2, btnOpenActions, btnLogin, btnClearLog, lblStatus
        });

        splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 500
        };

        webView = new WebView2 { Dock = DockStyle.Fill };
        splitContainer.Panel1.Controls.Add(webView);

        txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f),
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            WordWrap = false
        };
        splitContainer.Panel2.Controls.Add(txtLog);

        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel("就绪");
        statusStrip.Items.Add(statusLabel);

        pollTimer = new System.Windows.Forms.Timer(components) { Interval = 30_000 };

        SuspendLayout();
        ClientSize = new Size(1200, 800);
        Controls.Add(splitContainer);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
        Text = "GitHub Actions 自动审批工具 (WebView2)";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        ResumeLayout(false);
        PerformLayout();
    }
}

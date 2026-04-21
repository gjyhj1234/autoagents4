using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace GitHubAutoApprove;

/// <summary>
/// 主窗体。内嵌 WebView2 浏览器用于登录 GitHub，轮询 Actions 中
/// "waiting" 状态的 workflow run 并自动点击 "Approve and run"。
/// </summary>
public partial class MainForm : Form
{
    private AppSettings _settings;
    private bool _busy;                        // 防止轮询重入
    private readonly HashSet<string> _handledRunIds = new();   // 当前会话已处理过的 run
    private string _logFile;

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHubAutoApprove",
            $"log-{DateTime.Now:yyyyMMdd}.txt");

        txtOwner.Text = _settings.RepositoryOwner;
        txtRepo.Text = _settings.RepositoryName;
        txtInterval.Text = _settings.PollIntervalSeconds.ToString();

        btnSave.Click += (_, _) => SaveSettings();
        btnStart.Click += (_, _) => StartPolling();
        btnStop.Click += (_, _) => StopPolling();
        btnRunOnce.Click += async (_, _) => await PollOnceAsync();
        btnOpenActions.Click += (_, _) => NavigateToActionsPage();
        btnLogin.Click += (_, _) => webView.CoreWebView2?.Navigate("https://github.com/login");
        btnClearLog.Click += (_, _) => txtLog.Clear();

        pollTimer.Tick += async (_, _) => await PollOnceAsync();

        Load += async (_, _) => await InitializeWebViewAsync();
        FormClosing += (_, _) => SaveSettings();
    }

    // ---------- WebView2 ----------

    private async Task InitializeWebViewAsync()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.WebView2UserDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppSettings.WebView2UserDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Navigate("https://github.com/login");
            Log("WebView2 初始化完成，已导航到 GitHub 登录页。");
            Log("请在上方浏览器中完成登录（支持 2FA / passkey），登录状态会被持久化。");

            if (_settings.AutoStartPolling &&
                !string.IsNullOrWhiteSpace(_settings.RepositoryOwner) &&
                !string.IsNullOrWhiteSpace(_settings.RepositoryName))
            {
                StartPolling();
            }
        }
        catch (Exception ex)
        {
            Log($"[错误] WebView2 初始化失败: {ex.Message}");
            MessageBox.Show(
                "WebView2 Runtime 未安装或初始化失败。\n" +
                "请访问 https://developer.microsoft.com/microsoft-edge/webview2/ 下载并安装 Evergreen Runtime。\n\n" +
                $"详细错误: {ex.Message}",
                "WebView2 初始化失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------- 配置 ----------

    private void SaveSettings()
    {
        _settings.RepositoryOwner = txtOwner.Text.Trim();
        _settings.RepositoryName = txtRepo.Text.Trim();
        if (int.TryParse(txtInterval.Text, out var sec) && sec >= 5)
        {
            _settings.PollIntervalSeconds = sec;
            pollTimer.Interval = sec * 1000;
        }
        _settings.Save();
        Log($"配置已保存: {_settings.RepositoryOwner}/{_settings.RepositoryName}, " +
            $"间隔 {_settings.PollIntervalSeconds}s");
    }

    // ---------- 轮询控制 ----------

    private void StartPolling()
    {
        if (string.IsNullOrWhiteSpace(txtOwner.Text) || string.IsNullOrWhiteSpace(txtRepo.Text))
        {
            MessageBox.Show("请先填写仓库 Owner 和 Repo。", "提示");
            return;
        }

        SaveSettings();
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        lblStatus.Text = "状态: 轮询中";
        pollTimer.Interval = _settings.PollIntervalSeconds * 1000;
        pollTimer.Start();
        Log($"开始轮询，间隔 {_settings.PollIntervalSeconds}s");
        _ = PollOnceAsync();
    }

    private void StopPolling()
    {
        pollTimer.Stop();
        btnStart.Enabled = true;
        btnStop.Enabled = false;
        lblStatus.Text = "状态: 已停止";
        Log("已停止轮询。");
    }

    private void NavigateToActionsPage()
    {
        if (webView.CoreWebView2 == null) return;
        if (string.IsNullOrWhiteSpace(txtOwner.Text) || string.IsNullOrWhiteSpace(txtRepo.Text))
        {
            MessageBox.Show("请先填写 Owner 和 Repo。");
            return;
        }
        var url = $"https://github.com/{txtOwner.Text.Trim()}/{txtRepo.Text.Trim()}/actions";
        webView.CoreWebView2.Navigate(url);
    }

    // ---------- 核心逻辑: 扫描 + 审批 ----------

    /// <summary>
    /// 执行一次扫描：
    /// 1) 导航到 /actions?query=is:waiting
    /// 2) 用 JS 抓取所有等待审批的 run 链接
    /// 3) 依次打开每个 run 页面，点击 "Approve and run" 按钮
    /// </summary>
    private async Task PollOnceAsync()
    {
        if (_busy) return;
        if (webView.CoreWebView2 == null) return;
        if (string.IsNullOrWhiteSpace(_settings.RepositoryOwner) ||
            string.IsNullOrWhiteSpace(_settings.RepositoryName))
        {
            return;
        }

        _busy = true;
        try
        {
            var listUrl = $"https://github.com/{_settings.RepositoryOwner}/" +
                          $"{_settings.RepositoryName}/actions?query=is%3Awaiting";

            Log($"[扫描] 打开 {listUrl}");
            await NavigateAndWaitAsync(listUrl);

            // 未登录检测
            var currentUrl = webView.CoreWebView2.Source ?? "";
            if (currentUrl.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                Log("[警告] 检测到当前未登录 GitHub，请在浏览器中完成登录后再继续。");
                return;
            }

            // 抓取所有 run 链接
            // GitHub Actions 列表中每行都有 href="/owner/repo/actions/runs/<id>" 的 a 标签
            var script = @"
                (() => {
                    const links = Array.from(document.querySelectorAll('a[href*=""/actions/runs/""]'));
                    const ids = new Set();
                    for (const a of links) {
                        const m = a.getAttribute('href').match(/\/actions\/runs\/(\d+)/);
                        if (m) ids.add(m[1]);
                    }
                    return JSON.stringify(Array.from(ids));
                })();
            ";
            var json = await webView.CoreWebView2.ExecuteScriptAsync(script);
            // ExecuteScriptAsync 返回的是 JSON 编码后的字符串，要二次解码
            var inner = JsonSerializer.Deserialize<string>(json) ?? "[]";
            var runIds = JsonSerializer.Deserialize<List<string>>(inner) ?? new List<string>();

            if (runIds.Count == 0)
            {
                Log("[扫描] 当前无 waiting 的工作流运行。");
                return;
            }

            Log($"[扫描] 发现 {runIds.Count} 个可能等待审批的 run: {string.Join(", ", runIds)}");

            foreach (var id in runIds)
            {
                if (_handledRunIds.Contains(id))
                {
                    Log($"  跳过 run #{id}（本会话已处理）。");
                    continue;
                }

                var approved = await TryApproveRunAsync(id);
                if (approved)
                {
                    _handledRunIds.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[错误] 轮询异常: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 打开指定 run 的页面，查找并点击 "Approve and run" 按钮。
    /// 返回 true 表示本轮已处理（不论点击成功或按钮不存在），以避免无意义重复访问。
    /// </summary>
    private async Task<bool> TryApproveRunAsync(string runId)
    {
        var runUrl = $"https://github.com/{_settings.RepositoryOwner}/" +
                     $"{_settings.RepositoryName}/actions/runs/{runId}";
        Log($"[审批] 打开 {runUrl}");
        await NavigateAndWaitAsync(runUrl);

        // 尝试匹配多种审批按钮：
        //   - Fork PR 首次贡献者审批: "Approve and run"
        //   - Environment 需审批:      "Approve and deploy"
        //   - 通用包含 approve 的按钮
        var clickScript = @"
            (() => {
                const texts = ['Approve and run', 'Approve and deploy', 'Review deployments', 'Approve'];
                const all = Array.from(document.querySelectorAll('button, a, summary'));
                for (const t of texts) {
                    const el = all.find(e => (e.innerText || '').trim().toLowerCase().startsWith(t.toLowerCase()));
                    if (el) {
                        el.scrollIntoView({block:'center'});
                        el.click();
                        return 'clicked:' + t;
                    }
                }
                return 'not_found';
            })();
        ";

        var result = await webView.CoreWebView2!.ExecuteScriptAsync(clickScript);
        result = JsonSerializer.Deserialize<string>(result) ?? result;

        if (result.StartsWith("clicked:"))
        {
            Log($"  ✔ run #{runId} 点击了 [{result.Substring(8)}] 按钮。等待确认对话框...");
            // 有的审批流程点击后会弹出 "Approve and deploy" 的确认按钮；再点一次。
            await Task.Delay(1500);
            var confirmScript = @"
                (() => {
                    const btns = Array.from(document.querySelectorAll('button'));
                    const confirm = btns.find(b => /^(Approve and deploy|Approve and run|Confirm)/i.test((b.innerText||'').trim()));
                    if (confirm) { confirm.click(); return 'confirmed'; }
                    return 'no_confirm_needed';
                })();
            ";
            var r2 = await webView.CoreWebView2.ExecuteScriptAsync(confirmScript);
            r2 = JsonSerializer.Deserialize<string>(r2) ?? r2;
            Log($"  确认阶段: {r2}");
            return true;
        }

        Log($"  run #{runId} 未找到审批按钮（可能已自动批准 / 无权限 / 页面加载中）。");
        return true; // 本轮不再重试同一个 run
    }

    /// <summary>
    /// 导航到指定 URL 并等待页面加载完成。
    /// </summary>
    private async Task NavigateAndWaitAsync(string url)
    {
        var tcs = new TaskCompletionSource<bool>();

        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            webView.CoreWebView2!.NavigationCompleted -= Handler;
            tcs.TrySetResult(e.IsSuccess);
        }
        webView.CoreWebView2!.NavigationCompleted += Handler;
        webView.CoreWebView2.Navigate(url);

        // 最长等 20 秒
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(20_000));
        if (completed != tcs.Task)
        {
            webView.CoreWebView2.NavigationCompleted -= Handler;
            Log("  [警告] 导航超时（>20s）。");
        }

        // 再给页面脚本一点时间渲染
        await Task.Delay(1500);
    }

    // ---------- 日志 ----------

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (txtLog.InvokeRequired)
        {
            txtLog.BeginInvoke(new Action(() => AppendLog(line)));
        }
        else
        {
            AppendLog(line);
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            File.AppendAllText(_logFile, line + Environment.NewLine);
        }
        catch { /* 忽略日志写文件失败 */ }
    }

    private void AppendLog(string line)
    {
        txtLog.AppendText(line + Environment.NewLine);
        statusLabel.Text = line;
    }
}

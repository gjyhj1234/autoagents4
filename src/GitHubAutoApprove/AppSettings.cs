using System;
using System.IO;
using System.Text.Json;

namespace GitHubAutoApprove;

/// <summary>
/// 持久化的应用设置。保存到 %AppData%\GitHubAutoApprove\settings.json。
/// </summary>
public class AppSettings
{
    public string RepositoryOwner { get; set; } = "";
    public string RepositoryName { get; set; } = "";

    /// <summary>
    /// 轮询间隔（秒）。默认 30 秒，GitHub 未授权页面请求频繁可能触发限流。
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 是否在启动时自动开始轮询。
    /// </summary>
    public bool AutoStartPolling { get; set; } = false;

    // ---------- 持久化 ----------

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHubAutoApprove");

    private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // 配置损坏时回退到默认配置
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// WebView2 的用户数据目录，用于持久化 Cookie/登录状态。
    /// </summary>
    public static string WebView2UserDataFolder =>
        Path.Combine(ConfigDir, "WebView2");
}

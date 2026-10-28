using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace PolyStock.WPF.Services;

public static class UpdateService
{
    public const string GitHubRepoUrl = "https://github.com/progressivesystems2-bit/Polystock";

    public static bool IsAppJustRestartedAfterUpdate { get; set; }
    public static string? JustUpdatedVersion { get; set; }
    public static bool IsFirstRunAfterInstall { get; set; }

    private static GithubSource CreateGithubSource()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN", EnvironmentVariableTarget.Machine);
        return new GithubSource(GitHubRepoUrl, string.IsNullOrWhiteSpace(token) ? null : token, false);
    }

    public static string CurrentVersionString
    {
        get
        {
            try
            {
                var mgr = new UpdateManager(CreateGithubSource());
                if (mgr.IsInstalled && mgr.CurrentVersion != null)
                {
                    return mgr.CurrentVersion.ToFullString();
                }
            }
            catch
            {
                // Fallback to assembly version
            }

            var asmVersion = Assembly.GetExecutingAssembly().GetName().Version;
            return asmVersion != null ? $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}" : "1.0.0";
        }
    }

    public static async Task CheckOnStartupAsync(Window? ownerWindow = null)
    {
        // 1. Post-Update Notification
        if (IsAppJustRestartedAfterUpdate)
        {
            IsAppJustRestartedAfterUpdate = false;
            var ver = JustUpdatedVersion ?? CurrentVersionString;
            MessageBox.Show(
                ownerWindow,
                $"🎉 PolyStock has been successfully updated to version {ver}!\n\nAll your data and database settings have been preserved.",
                "PolyStock Updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else if (IsFirstRunAfterInstall)
        {
            IsFirstRunAfterInstall = false;
            MessageBox.Show(
                ownerWindow,
                $"Welcome to PolyStock (v{CurrentVersionString})!\n\nPolyStock is installed and ready to manage your polymer and bag inventory.",
                "Welcome to PolyStock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // 2. Startup Check (Runs quietly without bothering if offline or in dev mode)
        try
        {
            var mgr = new UpdateManager(CreateGithubSource());

            // In dev mode, don't nag on startup
            if (!mgr.IsInstalled)
            {
                return;
            }

            var updateInfo = await mgr.CheckForUpdatesAsync().ConfigureAwait(true);
            if (updateInfo == null)
            {
                return;
            }

            var newVersion = updateInfo.TargetFullRelease.Version.ToFullString();
            var promptResult = MessageBox.Show(
                ownerWindow,
                $"A new version of PolyStock (v{newVersion}) is available!\n\nWould you like to download and install this update now?",
                "PolyStock Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (promptResult == MessageBoxResult.Yes)
            {
                await DownloadAndApplyUpdateAsync(mgr, updateInfo, ownerWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Startup check skipped or failed: {ex.Message}");
        }
    }

    public static async Task CheckForUpdatesManualAsync(Window? ownerWindow = null)
    {
        try
        {
            var mgr = new UpdateManager(CreateGithubSource());

            if (!mgr.IsInstalled)
            {
                MessageBox.Show(
                    ownerWindow,
                    $"PolyStock is currently running in Development / Portable mode (v{CurrentVersionString}).\n\nAutomatic self-updates from GitHub are enabled in installed releases.",
                    "Update Check (Dev Mode)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var updateInfo = await mgr.CheckForUpdatesAsync().ConfigureAwait(true);
            if (updateInfo == null)
            {
                MessageBox.Show(
                    ownerWindow,
                    $"You are using the latest version of PolyStock (v{CurrentVersionString}).\nNo updates are needed.",
                    "PolyStock is Up to Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var newVersion = updateInfo.TargetFullRelease.Version.ToFullString();
            var promptResult = MessageBox.Show(
                ownerWindow,
                $"A newer version (v{newVersion}) is available! (Current: v{CurrentVersionString})\n\nWould you like to download and install this update now?",
                "New Version Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (promptResult == MessageBoxResult.Yes)
            {
                await DownloadAndApplyUpdateAsync(mgr, updateInfo, ownerWindow);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ownerWindow,
                $"Could not check for updates at this time:\n\n{ex.Message}\n\nPlease check your internet connection or verify the repository.",
                "Update Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static async Task DownloadAndApplyUpdateAsync(UpdateManager mgr, UpdateInfo updateInfo, Window? ownerWindow)
    {
        try
        {
            // Download update packages
            await mgr.DownloadUpdatesAsync(updateInfo).ConfigureAwait(true);

            var restartResult = MessageBox.Show(
                ownerWindow,
                $"Update v{updateInfo.TargetFullRelease.Version.ToFullString()} has been downloaded successfully.\n\nRestart PolyStock now to complete the update?",
                "Update Ready to Install",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (restartResult == MessageBoxResult.Yes)
            {
                mgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ownerWindow,
                $"An error occurred while downloading the update:\n{ex.Message}",
                "Download Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Studio;

public partial class StudioWindow
{
    internal const string PlatformUrl = "https://ai-arch.ir/";
    WebView2? platform;
    bool openingPlatform;
    bool closingStudio;

    void ShowChat()
    {
        PlatformPane.Visibility = Visibility.Collapsed;
        WorkspaceRoot.Visibility = Visibility.Visible;
        Title = "AI Archi-Tec Studio · Workspace";
    }

    void ReturnToChat(object sender, RoutedEventArgs e) => ShowChat();
    async void PlatformClick(object sender, RoutedEventArgs e) => await OpenPlatform();

    internal async Task OpenPlatform()
    {
        WorkspaceRoot.Visibility = Visibility.Collapsed;
        PlatformPane.Visibility = Visibility.Visible;
        Title = "AI Archi-Tec Studio · Web workspace";
        if (platform != null || openingPlatform) return;
        openingPlatform = true;
        WebStatus.Text = "Opening ai-arch.ir…";
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AIArchiTec", "Studio", "WebWorkspace", "WebProfile");
            platform = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = profile },
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(16, 17, 21)
            };
            WebHost.Children.Add(platform);
            await platform.EnsureCoreWebView2Async();
            if (closingStudio) return;
            var core = platform.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.HistoryChanged += (_, _) => BrowserBack.IsEnabled = core.CanGoBack;
            core.NavigationStarting += (_, args) =>
            {
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                    WebStatus.Text = "Opening " + uri.Host + "…";
                else
                {
                    args.Cancel = true;
                    WebStatus.Text = "This workspace opens HTTPS pages.";
                }
            };
            core.NavigationCompleted += (_, args) =>
                WebStatus.Text = args.IsSuccess ? new Uri(core.Source).Host :
                    "Page could not load · " + args.WebErrorStatus + " · Reload to try again.";
            core.Navigate(PlatformUrl);
        }
        catch (Exception ex)
        {
            if (closingStudio) return;
            WebHost.Children.Clear();
            platform?.Dispose();
            platform = null;
            WebStatus.Text = "Web workspace unavailable. Check Microsoft Edge WebView2 Runtime, then reload. " + ex.Message;
        }
        finally { openingPlatform = false; }
    }

    void BackInPlatform(object sender, RoutedEventArgs e)
    {
        if (platform?.CoreWebView2?.CanGoBack == true) platform.GoBack();
    }

    async void ReloadPlatform(object sender, RoutedEventArgs e)
    {
        if (platform?.CoreWebView2 != null) platform.Reload();
        else await OpenPlatform();
    }

    void ExternalPlatform(object sender, RoutedEventArgs e) => OpenLink(PlatformUrl);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkCaption(new WindowInteropHelper(this).Handle);
    }

    internal static void DarkCaption(IntPtr hwnd)
    {
        int enabled = 1, caption = 0x151110;
        DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int));
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
    }
}

using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using Studio;
internal static class PlatformNavigation
{
    internal static void Verify()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var app = new Application();
            var window = new StudioWindow { ShowInTaskbar = false, ShowActivated = false };
            window.ContentRendered += async (_, _) =>
            {
                try
                {
                    var prompt = (TextBox)window.FindName("Prompt");
                    prompt.Text = "Review my current selection";
                    var logo = (Button)window.FindName("PlatformButton");
                    logo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var host = (Grid)window.FindName("WebHost");
                    var status = (TextBlock)window.FindName("WebStatus");
                    var deadline = DateTime.UtcNow.AddSeconds(75);
                    var rendered = false;
                    while (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(300);
                        if (status.Text.Contains("unavailable") || status.Text.Contains("could not load"))
                            throw new Exception(status.Text);
                        if (host.Children.Cast<WebView2>().FirstOrDefault()?.CoreWebView2 is not { } core) continue;
                        var ready = await core.ExecuteScriptAsync("location.hostname === 'ai-arch.ir' && document.readyState !== 'loading' && !!document.querySelector('#root button, #root input, #root a')");
                        if (ready == "true") { rendered = true; break; }
                    }
                    if (!rendered)
                    {
                        var core=host.Children.Cast<WebView2>().FirstOrDefault()?.CoreWebView2;
                        var diagnostics=core==null?"Browser not initialized":await core.ExecuteScriptAsync("JSON.stringify({origin:location.origin,state:document.readyState,title:document.title,root:!!document.querySelector('#root'),buttons:document.querySelectorAll('button').length})");
                        throw new TimeoutException("The live platform UI did not render. " + status.Text+" "+diagnostics);
                    }
                    var browser = host.Children[0];
                    ((Button)window.FindName("DesktopChatButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (prompt.Text != "Review my current selection" || !prompt.IsVisible)
                        throw new Exception("Returning lost the chat draft.");
                    logo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (!ReferenceEquals(host.Children[0], browser) || !browser.IsVisible)
                        throw new Exception("Web workspace was recreated.");
                    Console.WriteLine("PASS brand navigation, live platform DOM, chat draft and browser session reuse.");
                }
                catch (Exception ex) { error = ex; }
                finally { window.Close(); }
            };
            app.Run(window);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }
}

using Microsoft.UI.Xaml;
using System;
using QRCoder;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

namespace YTMHub;

public sealed partial class MainWindow : Window
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    private LocalServer _server;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppDragRegion);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Forcefully set the taskbar icon to bypass Windows caching/unpackaged bugs
        try
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            IntPtr hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon != IntPtr.Zero)
            {
                SendMessage(hWnd, WM_SETICON, (IntPtr)ICON_BIG, hIcon);
                SendMessage(hWnd, WM_SETICON, (IntPtr)ICON_SMALL, hIcon);
            }
        }
        catch { }

        _server = new LocalServer();
        _server.PlayPauseRequested += (s, e) => ExecuteJs("document.querySelector('#play-pause-button')?.click();");
        _server.NextRequested += (s, e) => ExecuteJs("document.querySelector('.next-button')?.click();");
        _server.PreviousRequested += (s, e) => ExecuteJs("document.querySelector('.previous-button')?.click();");
        _server.VolumeRequested += (s, level) => ExecuteJs($@"
            var player = document.getElementById('movie_player');
            if (player) {{
                var targetLevel = {level.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                
                if (targetLevel === 0) {{
                    if (!player.isMuted()) {{
                        var muteBtn = document.querySelector('ytmusic-player-bar .volume');
                        if (muteBtn) muteBtn.click();
                        else player.mute();
                    }}
                }} else {{
                    if (player.isMuted()) {{
                        var muteBtn = document.querySelector('ytmusic-player-bar .volume');
                        if (muteBtn) muteBtn.click();
                        else player.unMute();
                    }}
                    player.setVolume(targetLevel * 100);
                }}
                
                var slider = document.querySelector('#volume-slider');
                if (slider) {{
                    slider.value = targetLevel * 100;
                }}
            }}
        ");

        MainWebView.NavigationCompleted += MainWebView_NavigationCompleted;
        MainWebView.WebMessageReceived += MainWebView_WebMessageReceived;
        
        // Turn the remote server on by default at startup
        ServerToggle.IsOn = true;
    }

    private void MainWebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        string js = @"
            let lastIsPlaying = null;
            let lastVolume = null;
            setInterval(() => {
                const player = document.getElementById('movie_player');
                if (player && typeof player.getPlayerState === 'function') {
                    const isPlaying = player.getPlayerState() === 1 || player.getPlayerState() === 3;
                    const volume = player.isMuted() ? 0 : player.getVolume() / 100;
                    if (isPlaying !== lastIsPlaying || volume !== lastVolume) {
                        lastIsPlaying = isPlaying;
                        lastVolume = volume;
                        window.chrome.webview.postMessage(JSON.stringify({ isPlaying: isPlaying, volume: volume }));
                    }
                }
            }, 1000);
        ";
        ExecuteJs(js);
    }

    private void MainWebView_WebMessageReceived(Microsoft.UI.Xaml.Controls.WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            string json = args.TryGetWebMessageAsString();
            if (!string.IsNullOrEmpty(json))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("isPlaying", out var isPlayingProp))
                    _server.IsPlaying = isPlayingProp.GetBoolean();
                if (doc.RootElement.TryGetProperty("volume", out var volumeProp))
                    _server.Volume = volumeProp.GetDouble();
            }
        }
        catch { }
    }

    private void ServerToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (ServerToggle.IsOn)
        {
            bool started = _server.Start();
            if (!started)
            {
                // Revert toggle state if it failed to start (e.g. port in use)
                ServerToggle.IsOn = false;
            }
            else if (QrCodePanel != null)
            {
                // If it successfully turned on while flyout is open, show the QR code
                RemoteFlyout_Opened(this, null);
            }
        }
        else
        {
            _server.Stop();
            if (QrCodePanel != null)
            {
                QrCodePanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void ExecuteJs(string script)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (MainWebView.CoreWebView2 != null)
            {
                await MainWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        });
    }

    private string GetLocalIpAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage> GenerateQrCodeAsync(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new BitmapByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(5);

        var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(qrCodeBytes.AsBuffer());
        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    private async void RemoteFlyout_Opened(object sender, object e)
    {
        if (ServerToggle.IsOn)
        {
            QrCodePanel.Visibility = Visibility.Visible;
            string ip = GetLocalIpAddress();
            string url = $"http://{ip}:9456";
            ServerUrlText.Text = url;
            try
            {
                QrCodeImage.Source = await GenerateQrCodeAsync(url);
            }
            catch
            {
                // Ignore QR generation errors
            }
        }
        else
        {
            QrCodePanel.Visibility = Visibility.Collapsed;
        }
    }
}

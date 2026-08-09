using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YTMHub;

public class LocalServer
{
    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private bool _isRunning = false;

    public bool IsPlaying { get; set; } = false;
    public double Volume { get; set; } = 0.5;

    public event EventHandler PlayPauseRequested;
    public event EventHandler NextRequested;
    public event EventHandler PreviousRequested;
    public event EventHandler<double> VolumeRequested;

    public bool Start(int port = 9456)
    {
        if (_isRunning) return true;

        try
        {
            // Using TcpListener with IPAddress.Any bypasses HTTP.sys URL ACLs!
            // This means NO ADMIN RIGHTS required.
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start TcpListener: {ex.Message}");
            return false;
        }
        
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenAsync(_cts.Token));
        return true;
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            client.ReceiveTimeout = 3000;
            client.SendTimeout = 3000;
            
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
            {
                // Read the first line of the HTTP request
                string requestLine = await reader.ReadLineAsync(token);
                if (string.IsNullOrEmpty(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                string method = parts[0];
                string path = parts[1].ToLower();

                // Consume remaining headers to avoid connection reset issues on client
                string headerLine;
                int contentLength = 0;
                while (!string.IsNullOrWhiteSpace(headerLine = await reader.ReadLineAsync(token))) 
                { 
                    if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(headerLine.Substring(15).Trim(), out int len))
                        {
                            contentLength = len;
                        }
                    }
                }

                // Consume body if present (e.g. POST requests) to prevent TCP RST on close
                if (contentLength > 0)
                {
                    char[] buffer = new char[contentLength];
                    await reader.ReadAsync(buffer, 0, contentLength);
                }

                if (method == "GET" && (path == "/" || path == "/index.html"))
                {
                    string html = GetMobileHtml();
                    int byteCount = Encoding.UTF8.GetByteCount(html);

                    await writer.WriteAsync("HTTP/1.1 200 OK\r\n");
                    await writer.WriteAsync("Content-Type: text/html; charset=UTF-8\r\n");
                    await writer.WriteAsync($"Content-Length: {byteCount}\r\n");
                    await writer.WriteAsync("Connection: close\r\n\r\n");
                    await writer.WriteAsync(html);
                    await writer.FlushAsync();
                }
                else if (method == "GET" && path == "/api/state")
                {
                    string json = $"{{\"isPlaying\":{IsPlaying.ToString().ToLower()},\"volume\":{Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
                    int byteCount = Encoding.UTF8.GetByteCount(json);

                    await writer.WriteAsync("HTTP/1.1 200 OK\r\n");
                    await writer.WriteAsync("Content-Type: application/json; charset=UTF-8\r\n");
                    await writer.WriteAsync("Access-Control-Allow-Origin: *\r\n");
                    await writer.WriteAsync($"Content-Length: {byteCount}\r\n");
                    await writer.WriteAsync("Connection: close\r\n\r\n");
                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                }
                else if (method == "POST" && path.StartsWith("/api/"))
                {
                    if (path.StartsWith("/api/playpause")) PlayPauseRequested?.Invoke(this, EventArgs.Empty);
                    else if (path.StartsWith("/api/next")) NextRequested?.Invoke(this, EventArgs.Empty);
                    else if (path.StartsWith("/api/previous")) PreviousRequested?.Invoke(this, EventArgs.Empty);
                    else if (path.StartsWith("/api/volume?level="))
                    {
                        string levelStr = path.Substring("/api/volume?level=".Length);
                        if (double.TryParse(levelStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double level))
                        {
                            Volume = level;
                            VolumeRequested?.Invoke(this, level);
                        }
                    }

                    await writer.WriteAsync("HTTP/1.1 200 OK\r\n");
                    await writer.WriteAsync("Access-Control-Allow-Origin: *\r\n");
                    await writer.WriteAsync("Content-Length: 2\r\n");
                    await writer.WriteAsync("Connection: close\r\n\r\n");
                    await writer.WriteAsync("OK");
                    await writer.FlushAsync();
                }
                else
                {
                    await writer.WriteAsync("HTTP/1.1 404 Not Found\r\n");
                    await writer.WriteAsync("Content-Length: 0\r\n");
                    await writer.WriteAsync("Connection: close\r\n\r\n");
                    await writer.FlushAsync();
                }
                
                // Graceful close
                client.Client.Shutdown(SocketShutdown.Both);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling client: {ex.Message}");
        }
    }

    private string GetMobileHtml()
    {
        try
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(basePath, "Assets", "remote.html");
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }
        catch { }

        return "<html><head><title>YTMHub Remote</title></head><body><h1>Error: remote.html not found in Assets folder.</h1></body></html>";
    }
}

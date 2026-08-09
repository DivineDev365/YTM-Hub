using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;

namespace YTMHub;

[ComVisible(true)]
[Guid("E039474B-6B5E-4B07-A5AE-79BDC949D504")]
public class WidgetProvider : IWidgetProvider
{
    public WidgetProvider()
    {
        Log("WidgetProvider constructor");
    }

    private void Log(string msg)
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logPath = System.IO.Path.Combine(folder, "YTMHub_Widget_Log.txt");
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: {msg}\n");
        }
        catch { }
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        Log($"CreateWidget: {widgetContext.Id}");
        UpdateWidget(widgetContext.Id);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
    }

    public async void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        string verb = actionInvokedArgs.Verb;
        if (verb == "playpause" || verb == "next" || verb == "previous")
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                await client.PostAsync($"http://127.0.0.1:9456/api/{verb}", null);
            }
            catch { }
            UpdateWidget(actionInvokedArgs.WidgetContext.Id);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        UpdateWidget(contextChangedArgs.WidgetContext.Id);
    }

    public void Activate(WidgetContext widgetContext)
    {
        UpdateWidget(widgetContext.Id);
    }

    public void Deactivate(string widgetId)
    {
    }

    private async void UpdateWidget(string widgetId)
    {
        Log($"UpdateWidget called for {widgetId}");
        try
        {
            bool isPlaying = false;
            bool isRunning = false;
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(1);
                var res = await client.GetStringAsync("http://127.0.0.1:9456/api/state");
                if (!string.IsNullOrEmpty(res))
                {
                    using var doc = JsonDocument.Parse(res);
                    if (doc.RootElement.TryGetProperty("isPlaying", out var isPlayingProp))
                    {
                        isPlaying = isPlayingProp.GetBoolean();
                        isRunning = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"State fetch failed (expected if closed): {ex.Message}");
            }

            string template = WidgetTemplates.GetTemplate();
            string data = WidgetTemplates.GetData(isRunning, isPlaying);
            
            Log($"Template: {template}");
            Log($"Data: {data}");

            var updateOptions = new WidgetUpdateRequestOptions(widgetId);
            updateOptions.Template = template;
            updateOptions.Data = data;
            
            WidgetManager.GetDefault().UpdateWidget(updateOptions);
            Log("WidgetManager.UpdateWidget success");
        }
        catch (Exception ex)
        {
            Log($"UpdateWidget Exception: {ex}");
        }
    }
}

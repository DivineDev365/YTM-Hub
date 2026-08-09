using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;

namespace YTMHub;

public static class Program
{
    [DllImport("ole32.dll")]
    static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    private static WidgetProviderFactory _factory;
    private static uint _cookie;

    public static void Log(string msg)
    {
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logPath = System.IO.Path.Combine(folder, "YTMHub", "factory_log.txt");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        /*
        WinRT.ComWrappersSupport.InitializeComWrappers();

        bool isCOMCall = false;
        foreach (var arg in args)
        {
            if (arg.Contains("-RegisterProcessAsComServer"))
            {
                isCOMCall = true;
                break;
            }
        }

        // Register widget provider COM server
        var clsid = Guid.Parse("E039474B-6B5E-4B07-A5AE-79BDC949D504");
        _factory = new WidgetProviderFactory();
        
        Log($"Registering COM Server (isCOMCall={isCOMCall})...");
        
        // CLSCTX_LOCAL_SERVER = 4, REGCLS_MULTIPLEUSE = 1
        CoRegisterClassObject(clsid, _factory, 4, 1, out _cookie);
        
        Log($"CoRegisterClassObject success, cookie={_cookie}");

        if (isCOMCall)
        {
            Log("Waiting for COM requests...");
            // Keep the process alive to serve the widget headlessly
            AutoResetEvent waitHandle = new AutoResetEvent(false);
            waitHandle.WaitOne();
        }
        else
        */
        {
            // Configure WebView2 to store its user data (cache, cookies, etc.) in LocalAppData
            // so it doesn't pollute the directory where the portable .exe is located.
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string webView2DataFolder = System.IO.Path.Combine(localAppData, "YTMHub", "WebView2");
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webView2DataFolder);

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}

[ComImport, ComVisible(false), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
public interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
    [PreserveSig]
    int LockServer(bool fLock);
}

[ComVisible(true)]
public class WidgetProviderFactory : IClassFactory
{
    private static WidgetProvider _provider;

    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        Program.Log($"CreateInstance called! riid={riid}");

        if (pUnkOuter != IntPtr.Zero)
        {
            Program.Log("CreateInstance failed: pUnkOuter != 0");
            Marshal.ThrowExceptionForHR(-2147221232); // CLASS_E_NOAGGREGATION
        }

        try
        {
            _provider ??= new WidgetProvider();
            Program.Log("WidgetProvider instance created or retrieved.");
            IntPtr pInspectable = WinRT.MarshalInspectable<Microsoft.Windows.Widgets.Providers.IWidgetProvider>.FromManaged(_provider);
            int hr = Marshal.QueryInterface(pInspectable, ref riid, out ppvObject);
            Marshal.Release(pInspectable);
            
            Program.Log($"QueryInterface returned HR={hr}, ptr={ppvObject}");
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
        catch (Exception ex)
        {
            Program.Log($"CreateInstance exception: {ex}");
            Marshal.ThrowExceptionForHR(-2147467262); // E_NOINTERFACE
        }

        return 0;
    }

    public int LockServer(bool fLock)
    {
        return 0;
    }
}

using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using System.Net;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    // Set of blocked websites (loaded from files)
    static HashSet<string> blockedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Counter to track the number of requests processed
    static int requestCount = 0;

    // Log file path for storing blocked URLs
    static readonly string logFilePath = $"blocked_urls_{DateTime.Now:yyyyMMdd_HHmmss}.log";

    // Proxy server and endpoint instances
    static ProxyServer proxyServer;
    static ExplicitProxyEndPoint explicitEndPoint;

    static async Task Main(string[] args)
    {
        // Log memory usage at startup
        MemoryProfiler.LogMemoryUsage("Startup Memory Usage");

        // Read blocked websites from the provided files
        string[] files = {
            "fakenews.txt",
            "nsfw.txt",
            "socialmedia.txt",
            "gambling.txt",
            "malware.txt"
        };

        // Load blocked sites into a hash set
        blockedSites = ReadBlockedSitesFromFiles(files);
        MemoryProfiler.LogMemoryUsage("After Loading Blocklist");

        // Initialize proxy server instance
        proxyServer = new ProxyServer();

        // Hook cleanup actions to process exit and Ctrl+C events
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            // Subscribe to the request processing event
            proxyServer.BeforeRequest += OnRequest;

            // Set up explicit proxy endpoint on port 8888
            explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8888, true);

            // Handle HTTPS connections (optional SSL decryption)
            explicitEndPoint.BeforeTunnelConnectRequest += async (sender, e) =>
            {
                if (IsBlockedUrl(e.HttpClient.Request.RequestUri.Host))
                {
                    e.DecryptSsl = true; // Enable SSL decryption for blocked sites
                }
                await Task.CompletedTask;
            };

            // Add the endpoint to the proxy server
            proxyServer.AddEndPoint(explicitEndPoint);

            // Generate and trust a root certificate for intercepting SSL traffic
            proxyServer.CertificateManager.CreateRootCertificate(true);
            proxyServer.CertificateManager.TrustRootCertificate(true);

            // Start the proxy server
            proxyServer.Start();

            // Set Edge browser proxy to use the proxy server
            SetEdgeProxy("localhost:8888");

            Console.WriteLine("Web Filter Proxy started on http://localhost:8888");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Unable to start the proxy server. {ex.Message}");
        }
        finally
        {
            // Perform cleanup actions (stop proxy and reset settings)
            CleanupResources();
        }
    }

    // Event handler for process exit
    private static void OnProcessExit(object sender, EventArgs e)
    {
        Console.WriteLine("Process exiting. Cleaning up resources...");
        CleanupResources();
    }

    // Event handler for Ctrl+C key press
    private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("Cancel key pressed. Cleaning up resources...");
        e.Cancel = true; // Prevent immediate termination
        CleanupResources();
        Environment.Exit(0);
    }

    // Centralized method to clean up resources
    private static void CleanupResources()
    {
        try
        {
            if (proxyServer != null)
            {
                proxyServer.Stop();
                proxyServer.Dispose();
                Console.WriteLine("Proxy server stopped and disposed.");
            }

            // Reset Edge browser proxy settings
            ResetEdgeProxy();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    // Event handler for processing requests
    private static async Task OnRequest(object sender, SessionEventArgs e)
    {
        var session = e.WebSession;
        var requestUrl = session.Request.Url;

        // Increment request count in a thread-safe manner
        Interlocked.Increment(ref requestCount);

        // Check if the requested URL is blocked
        if (IsBlockedUrl(requestUrl))
        {
            Console.WriteLine($"Blocked: {requestUrl}");

            // Respond with a custom blocked message
            e.Ok("<html><body><h1><center>Access to this website is blocked.</center></h1></body></html>");
            LogBlockedUrl(requestUrl);
        }
        else
        {
            await Task.CompletedTask; // Allow other requests to proceed
        }

        // Log memory usage every 100 requests
        if (requestCount % 100 == 0)
        {
            Console.WriteLine($"Requests Handled: {requestCount}");
            MemoryProfiler.LogMemoryUsage($"After Handling {requestCount} Requests");
        }
    }

    // Check if a URL is in the blocked list
    private static bool IsBlockedUrl(string url)
    {
        foreach (var site in blockedSites)
        {
            if (url.IndexOf(site, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    // Log blocked URLs to a file
    private static void LogBlockedUrl(string url)
    {
        try
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Blocked: {url}";
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }

    // Load blocked sites from multiple files into a HashSet
    public static HashSet<string> ReadBlockedSitesFromFiles(string[] filePaths)
    {
        var sites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    foreach (var line in File.ReadLines(filePath))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            sites.Add(line.Trim());
                        }
                    }
                    Console.WriteLine($"Loaded {sites.Count} sites from {filePath}");
                }
                else
                {
                    Console.WriteLine($"FILE NOT FOUND: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            }
        }
        return sites;
    }

    // Configure Edge browser proxy settings
    public static void SetEdgeProxy(string proxyAddress)
    {
        string proxyKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(proxyKey, true))
            {
                if (key != null)
                {
                    key.SetValue("ProxyServer", proxyAddress);
                    key.SetValue("ProxyEnable", 1);
                    Console.WriteLine("Edge proxy has been set.");
                }
                else
                {
                    Console.WriteLine("Failed to open registry key.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while modifying registry: {ex.Message}");
        }
    }

    // Reset Edge browser proxy settings to default
    public static void ResetEdgeProxy()
    {
        string proxyKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(proxyKey, true))
            {
                if (key != null)
                {
                    key.SetValue("ProxyEnable", 0);
                    key.DeleteValue("ProxyServer", false);
                    key.DeleteValue("ProxyOverride", false);
                }
            }
            Console.WriteLine("Edge proxy settings reset to default.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while resetting registry: {ex.Message}");
        }
    }

    // Utility class for logging memory usage
    public static class MemoryProfiler
    {
        public static void LogMemoryUsage(string label = "Memory Usage")
        {
            Process currentProcess = Process.GetCurrentProcess();

            long privateMemory = currentProcess.PrivateMemorySize64;
            long workingSet = currentProcess.WorkingSet64;

            Console.WriteLine($"[{label}]");
            Console.WriteLine($"  - Private Memory: {privateMemory / 1024 / 1024} MB");
            Console.WriteLine($"  - Working Set: {workingSet / 1024 / 1024} MB");
            Console.WriteLine();
        }
    }
}

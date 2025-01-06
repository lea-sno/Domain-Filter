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
    static HashSet<string> blockedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static int requestCount = 0; // Request counter
    static readonly string logFilePath = $"blocked_urls_{DateTime.Now:yyyyMMdd_HHmmss}.log";
    static async Task Main(string[] args)
    {
        MemoryProfiler.LogMemoryUsage("Startup Memory Usage");

        // Read blocked websites from the files
        string[] files = {
            "fakenews.txt",
            "nsfw.txt",
            "socialmedia.txt",
            "gambling.txt",
            "malware.txt"
        }; // Add all file names here
           // comment out any category to allow access

        blockedSites = ReadBlockedSitesFromFiles(files);
        MemoryProfiler.LogMemoryUsage("After Loading Blocklist");

        // Create the proxy server
        var proxyServer = new ProxyServer();

        try
        {
            // Configure proxy settings
            proxyServer.BeforeRequest += OnRequest;

            // Add an explicit endpoint for the proxy to listen on port 8888
            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8888, true);

            // Enable SSL/TLS interception
            explicitEndPoint.BeforeTunnelConnectRequest += async (sender, e) =>
            {
                if (IsBlockedUrl(e.HttpClient.Request.RequestUri.Host))
                {
                    e.DecryptSsl = true; // Intercept HTTPS traffic
                }
                await Task.CompletedTask;
            };

            proxyServer.AddEndPoint(explicitEndPoint);

            // Generate and trust a root certificate for HTTPS handling
            proxyServer.CertificateManager.CreateRootCertificate(true);
            proxyServer.CertificateManager.TrustRootCertificate(true);

            // Start the proxy server
            proxyServer.Start();
            SetEdgeProxy("localhost:8888");

            Console.WriteLine("Web Filter Proxy started on http://localhost:8888");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Unable to start the proxy server. {ex.Message}");
            Console.WriteLine("Check if the port is already in use or if the network configuration is valid.");
        }
        finally
        {
            // Cleanup resources
            ResetEdgeProxy();
            proxyServer.Stop();
            proxyServer.Dispose();
            Console.WriteLine("Proxy server stopped and resources cleaned up.");
        }
    }

    // Event handler to intercept the HTTP request
    private static async Task OnRequest(object sender, SessionEventArgs e)
    {
        var session = e.WebSession;
        var requestUrl = session.Request.Url;

        // Increment the request counter in a thread-safe way
        Interlocked.Increment(ref requestCount);

        // Block specific URLs or domains
        if (IsBlockedUrl(requestUrl))
        {
            Console.WriteLine($"Blocked: {requestUrl}");
            e.Ok("<html><body><h1><center>Access to this website is blocked.</center></h1></body></html>");

            // Log the blocked URL to the log file
            LogBlockedUrl(requestUrl);
        }
        else
        {
            await Task.CompletedTask; // Allow other requests
        }

        // Log memory usage after every 100 requests
        if (requestCount % 100 == 0)
        {
            Console.WriteLine($"Requests Handled: {requestCount}");
            MemoryProfiler.LogMemoryUsage($"After Handling {requestCount} Requests");
        }
    }

    // Helper method to check if a URL should be blocked
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

    // Method to log blocked URLs to a file
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

    // Method to read blocked sites from multiple files
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
                    // Display total number of loaded blocked sites
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
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("ERROR: Insufficient permissions to modify the registry. Please run as administrator.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while modifying registry: {ex.Message}");
        }
    }

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

    public static class MemoryProfiler
    {
        // Get current memory usage
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

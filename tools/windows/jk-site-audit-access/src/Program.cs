using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

record AuditResult(
    string Hostname,
    string Username,
    DateTime TimestampUtc,
    string[] LocalIPs,
    Dictionary<string, object> Checks
);

class Program
{
    static int Main()
    {
        try
        {
            var checks = new Dictionary<string, object>();

            checks["open_ports"] = RunCommand("netstat", "-ano");
            checks["smb_shares"] = RunPowerShell("Get-SmbShare | Select Name,Path,Description,ScopeName | ConvertTo-Json -Depth 5");
            checks["smb_sessions"] = RunPowerShell("Get-SmbSession | Select ClientComputerName,ClientUserName,NumOpens | ConvertTo-Json -Depth 5");
            checks["dns_client_servers"] = RunPowerShell("Get-DnsClientServerAddress | Select InterfaceAlias,ServerAddresses | ConvertTo-Json -Depth 6");
            checks["dns_tests"] = new Dictionary<string, string>
            {
                ["resolve_google"] = RunPowerShell("Resolve-DnsName google.com -ErrorAction SilentlyContinue | Select Name,Type,IPAddress | ConvertTo-Json -Depth 4"),
                ["nslookup_google"] = RunCommand("nslookup", "google.com")
            };
            checks["firewall_profiles"] = RunPowerShell("Get-NetFirewallProfile | Select Name,Enabled,DefaultInboundAction,DefaultOutboundAction | ConvertTo-Json -Depth 4");
            checks["network_profiles"] = RunPowerShell("Get-NetConnectionProfile | Select Name,NetworkCategory,IPv4Connectivity,IPv6Connectivity | ConvertTo-Json -Depth 4");

            var hostIps = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString())
                .ToArray();

            var result = new AuditResult(
                Hostname: Environment.MachineName,
                Username: Environment.UserName,
                TimestampUtc: DateTime.UtcNow,
                LocalIPs: hostIps,
                Checks: checks
            );

            var outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "JKCyber", "SiteAudit");
            Directory.CreateDirectory(outDir);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var baseName = $"audit-{Environment.MachineName}-{stamp}";
            var jsonPath = Path.Combine(outDir, baseName + ".json");
            var txtPath = Path.Combine(outDir, baseName + ".txt");

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);

            using (var sw = new StreamWriter(txtPath))
            {
                sw.WriteLine("JK Cyber Site Audit (Read-only)");
                sw.WriteLine($"Hostname: {result.Hostname}");
                sw.WriteLine($"User: {result.Username}");
                sw.WriteLine($"UTC: {result.TimestampUtc:O}");
                sw.WriteLine($"IPs: {string.Join(", ", result.LocalIPs)}");
                sw.WriteLine(new string('-', 80));
                foreach (var kv in checks)
                {
                    sw.WriteLine($"[{kv.Key}]");
                    sw.WriteLine(kv.Value?.ToString());
                    sw.WriteLine();
                }
            }

            Console.WriteLine("Audit complete.");
            Console.WriteLine($"JSON: {jsonPath}");
            Console.WriteLine($"TXT : {txtPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Audit failed: " + ex.Message);
            return 1;
        }
    }

    static string RunCommand(string fileName, string args)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return string.IsNullOrWhiteSpace(err) ? output : output + "\n[stderr]\n" + err;
    }

    static string RunPowerShell(string command)
    {
        return RunCommand("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "`\"")}\"");
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

record Finding(
    string Risk,
    string Issue,
    string WhyItMatters,
    string RecommendedAction,
    bool ApprovalNeeded,
    bool BackupFirst,
    string Evidence
);

record AuditResult(
    string ToolVersion,
    string Hostname,
    string Username,
    DateTime TimestampUtc,
    string[] LocalIPs,
    Dictionary<string, object> Checks,
    List<Finding> Findings
);

class Program
{
    static readonly HashSet<string> DefaultAdminShares = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN$", "C$", "IPC$", "PRINT$"
    };

    static readonly HashSet<string> AllowedDomainDns = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common local DNS resolver ranges; tune per-client policy if needed.
        "10.", "172.16.", "172.17.", "172.18.", "172.19.", "172.20.", "172.21.", "172.22.", "172.23.",
        "172.24.", "172.25.", "172.26.", "172.27.", "172.28.", "172.29.", "172.30.", "172.31.", "192.168.", "127.", "::1"
    };

    static int Main(string[] args)
    {
        try
        {
            string? expectedDomain = null;
            if (args.Length >= 2 && args[0].Equals("--domain", StringComparison.OrdinalIgnoreCase))
            {
                expectedDomain = args[1];
            }

            var checks = new Dictionary<string, object>();

            var openPorts = RunCommand("netstat", "-ano");
            var smbSharesJson = RunPowerShell("Get-SmbShare | Select Name,Path,Description,ScopeName | ConvertTo-Json -Depth 5");
            var smbSessionsJson = RunPowerShell("Get-SmbSession | Select ClientComputerName,ClientUserName,NumOpens | ConvertTo-Json -Depth 5");
            var dnsClientJson = RunPowerShell("Get-DnsClientServerAddress -AddressFamily IPv4 | Select InterfaceAlias,ServerAddresses | ConvertTo-Json -Depth 6");
            var firewallProfilesJson = RunPowerShell("Get-NetFirewallProfile | Select Name,Enabled,DefaultInboundAction,DefaultOutboundAction | ConvertTo-Json -Depth 4");
            var networkProfilesJson = RunPowerShell("Get-NetConnectionProfile | Select Name,NetworkCategory,IPv4Connectivity,IPv6Connectivity | ConvertTo-Json -Depth 4");
            var domainInfoJson = RunPowerShell("Get-CimInstance Win32_ComputerSystem | Select Domain,PartOfDomain | ConvertTo-Json -Depth 3");

            checks["open_ports"] = openPorts;
            checks["smb_shares"] = smbSharesJson;
            checks["smb_sessions"] = smbSessionsJson;
            checks["dns_client_servers"] = dnsClientJson;
            checks["dns_tests"] = new Dictionary<string, string>
            {
                ["resolve_google"] = RunPowerShell("Resolve-DnsName google.com -ErrorAction SilentlyContinue | Select Name,Type,IPAddress | ConvertTo-Json -Depth 4"),
                ["nslookup_google"] = RunCommand("nslookup", "google.com")
            };
            checks["firewall_profiles"] = firewallProfilesJson;
            checks["network_profiles"] = networkProfilesJson;
            checks["domain_info"] = domainInfoJson;

            var findings = BuildFindings(openPorts, smbSharesJson, dnsClientJson, firewallProfilesJson, networkProfilesJson, domainInfoJson, expectedDomain);

            var hostIps = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString())
                .ToArray();

            var result = new AuditResult(
                ToolVersion: "1.1.0",
                Hostname: Environment.MachineName,
                Username: Environment.UserName,
                TimestampUtc: DateTime.UtcNow,
                LocalIPs: hostIps,
                Checks: checks,
                Findings: findings
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
                sw.WriteLine($"Tool version: {result.ToolVersion}");
                sw.WriteLine($"Hostname: {result.Hostname}");
                sw.WriteLine($"User: {result.Username}");
                sw.WriteLine($"UTC: {result.TimestampUtc:O}");
                sw.WriteLine($"IPs: {string.Join(", ", result.LocalIPs)}");
                sw.WriteLine($"Expected domain: {(string.IsNullOrWhiteSpace(expectedDomain) ? "not provided" : expectedDomain)}");
                sw.WriteLine(new string('-', 80));

                sw.WriteLine("FINDINGS SUMMARY");
                foreach (var grp in findings.GroupBy(f => f.Risk).OrderBy(g => RiskOrder(g.Key)))
                {
                    sw.WriteLine($"- {grp.Key}: {grp.Count()}");
                }
                if (!findings.Any()) sw.WriteLine("- INFO: No immediate issues detected by current checks.");
                sw.WriteLine(new string('-', 80));

                foreach (var f in findings.OrderBy(f => RiskOrder(f.Risk)))
                {
                    sw.WriteLine($"[{f.Risk}] {f.Issue}");
                    sw.WriteLine($"Why: {f.WhyItMatters}");
                    sw.WriteLine($"Action: {f.RecommendedAction}");
                    sw.WriteLine($"Approval needed: {(f.ApprovalNeeded ? "YES" : "NO")}");
                    sw.WriteLine($"Backup first: {(f.BackupFirst ? "YES" : "NO")}");
                    sw.WriteLine($"Evidence: {f.Evidence}");
                    sw.WriteLine();
                }

                sw.WriteLine(new string('-', 80));
                sw.WriteLine("RAW CHECKS");
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
            Console.WriteLine($"Findings: {findings.Count}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Audit failed: " + ex.Message);
            return 1;
        }
    }

    static List<Finding> BuildFindings(string openPorts, string smbSharesJson, string dnsClientJson, string firewallProfilesJson, string networkProfilesJson, string domainInfoJson, string? expectedDomain)
    {
        var findings = new List<Finding>();

        // Open RDP port (3389)
        if (Regex.IsMatch(openPorts, @":3389\s+.*LISTENING", RegexOptions.IgnoreCase))
        {
            findings.Add(new Finding(
                Risk: "HIGH",
                Issue: "RDP port 3389 is listening on this endpoint.",
                WhyItMatters: "RDP exposure increases brute-force and lateral movement risk.",
                RecommendedAction: "Confirm access requirement, restrict by firewall/VPN, and enforce MFA/lockout policy.",
                ApprovalNeeded: true,
                BackupFirst: false,
                Evidence: "netstat shows LISTENING on :3389"
            ));
        }

        // Non-default SMB shares
        foreach (var share in ReadJsonArray(smbSharesJson))
        {
            var name = share["Name"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name) && !DefaultAdminShares.Contains(name))
            {
                findings.Add(new Finding(
                    Risk: "MEDIUM",
                    Issue: $"Non-default SMB share detected: {name}",
                    WhyItMatters: "Extra shares can expose sensitive data if ACLs are weak.",
                    RecommendedAction: "Review share ACLs and business need. Remove unused shares after backup and approval.",
                    ApprovalNeeded: true,
                    BackupFirst: true,
                    Evidence: $"Get-SmbShare returned share '{name}'"
                ));
            }
        }

        // Firewall profile disabled or inbound allow
        foreach (var fp in ReadJsonArray(firewallProfilesJson))
        {
            var name = fp["Name"]?.ToString() ?? "unknown";
            var enabled = fp["Enabled"]?.ToString() ?? "";
            var inbound = fp["DefaultInboundAction"]?.ToString() ?? "";

            if (enabled.Equals("False", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding(
                    Risk: "CRITICAL",
                    Issue: $"Windows Firewall profile '{name}' is disabled.",
                    WhyItMatters: "Disabled host firewall significantly increases exposure to lateral movement and malware spread.",
                    RecommendedAction: "Re-enable firewall profile with policy-aligned rules. Test in maintenance window first.",
                    ApprovalNeeded: true,
                    BackupFirst: false,
                    Evidence: $"Get-NetFirewallProfile shows Enabled=False for {name}"
                ));
            }
            else if (inbound.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding(
                    Risk: "HIGH",
                    Issue: $"Firewall profile '{name}' default inbound action is ALLOW.",
                    WhyItMatters: "Default-allow inbound weakens endpoint segmentation and attack surface control.",
                    RecommendedAction: "Set default inbound to Block and explicitly allow required services.",
                    ApprovalNeeded: true,
                    BackupFirst: false,
                    Evidence: $"Get-NetFirewallProfile shows DefaultInboundAction=Allow for {name}"
                ));
            }
        }

        // Network profile public
        foreach (var np in ReadJsonArray(networkProfilesJson))
        {
            var name = np["Name"]?.ToString() ?? "unknown";
            var category = np["NetworkCategory"]?.ToString() ?? "";
            if (category.Equals("Public", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding(
                    Risk: "MEDIUM",
                    Issue: $"Network profile '{name}' is set to Public.",
                    WhyItMatters: "Domain-managed systems on Public profile may have inconsistent policy application.",
                    RecommendedAction: "Validate domain connectivity and set to DomainAuthenticated/Private as appropriate.",
                    ApprovalNeeded: true,
                    BackupFirst: false,
                    Evidence: $"Get-NetConnectionProfile shows NetworkCategory=Public for {name}"
                ));
            }
        }

        // DNS server hygiene
        foreach (var dnsEntry in ReadJsonArray(dnsClientJson))
        {
            var iface = dnsEntry["InterfaceAlias"]?.ToString() ?? "unknown";
            var servers = dnsEntry["ServerAddresses"];
            if (servers is JsonArray arr)
            {
                foreach (var s in arr)
                {
                    var server = s?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(server)) continue;
                    if (!AllowedDomainDns.Any(prefix => server.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        findings.Add(new Finding(
                            Risk: "HIGH",
                            Issue: $"Possible public/non-local DNS server configured on {iface}: {server}",
                            WhyItMatters: "Domain endpoints should use controlled internal DNS to avoid auth and policy resolution issues.",
                            RecommendedAction: "Set DNS to approved internal resolvers (typically domain controllers) and retest name resolution.",
                            ApprovalNeeded: true,
                            BackupFirst: false,
                            Evidence: $"Get-DnsClientServerAddress reported {server} on {iface}"
                        ));
                    }
                }
            }
        }

        // Domain mismatch check
        if (!string.IsNullOrWhiteSpace(expectedDomain))
        {
            var domainArr = ReadJsonArray(domainInfoJson);
            var domain = domainArr.FirstOrDefault()?["Domain"]?.ToString();
            if (!string.IsNullOrWhiteSpace(domain) && !domain.Equals(expectedDomain, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new Finding(
                    Risk: "HIGH",
                    Issue: $"Endpoint domain mismatch. Expected '{expectedDomain}', detected '{domain}'.",
                    WhyItMatters: "Wrong domain membership can break policy enforcement and trust boundaries.",
                    RecommendedAction: "Confirm device assignment; if incorrect, schedule controlled domain rejoin.",
                    ApprovalNeeded: true,
                    BackupFirst: true,
                    Evidence: $"Win32_ComputerSystem.Domain={domain}"
                ));
            }
        }

        return findings;
    }

    static IEnumerable<JsonObject> ReadJsonArray(string json)
    {
        var list = new List<JsonObject>();
        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JsonObject o) list.Add(o);
                }
            }
            else if (node is JsonObject obj)
            {
                list.Add(obj);
            }
        }
        catch
        {
            // ignore parse failures; caller handles empty list
        }

        return list;
    }

    static int RiskOrder(string risk) => risk.ToUpperInvariant() switch
    {
        "CRITICAL" => 0,
        "HIGH" => 1,
        "MEDIUM" => 2,
        "LOW" => 3,
        _ => 4
    };

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

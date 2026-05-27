using System;
using System.Diagnostics;
using System.IO;

class RunnerProgram
{
    static int Main(string[] args)
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var auditExe = Path.Combine(exeDir, "JKSiteAudit.exe");

            if (!File.Exists(auditExe))
            {
                Console.Error.WriteLine("JKSiteAudit.exe not found next to runner.");
                Console.Error.WriteLine("Place JKSiteAuditRunner.exe in same folder as JKSiteAudit.exe.");
                return 1;
            }

            var passArgs = string.Join(" ", args);
            var psi = new ProcessStartInfo(auditExe, passArgs)
            {
                UseShellExecute = false
            };

            Console.WriteLine("Starting JKSiteAudit...");
            using var p = Process.Start(psi)!;
            p.WaitForExit();

            Console.WriteLine($"JKSiteAudit exited with code {p.ExitCode}.");
            Console.WriteLine("Output folder: C:\\ProgramData\\JKCyber\\SiteAudit");
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Runner failed: " + ex.Message);
            return 1;
        }
    }
}

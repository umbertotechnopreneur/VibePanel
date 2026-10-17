// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Diagnostics;
using System.Runtime.InteropServices;
using VibePanel.Photino;

internal static class SetupGateChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint SetupAction(nint msi);

    internal static int Run(string[] args)
    {
        var path = Path.GetFullPath(args[1]);
        var library = NativeLibrary.Load(path);
        var begin = Marshal.GetDelegateForFunctionPointer<SetupAction>(NativeLibrary.GetExport(library, "BeginSetup"));
        var end = Marshal.GetDelegateForFunctionPointer<SetupAction>(NativeLibrary.GetExport(library, "EndSetup"));
        if (args[0] == "--setup-gate-owner")
        {
            if (begin(0) != 0) { return 1; }
            Console.WriteLine("gate-ready"); Console.Out.Flush();
            Console.ReadLine(); // Exit without EndSetup to simulate an installer-host crash.
            return 0;
        }
        if (WindowsSetupGate.IsInstalling()) { throw new InvalidOperationException("A real setup is already running; check refused."); }
        try
        {
            if (begin(0) != 0 || !WindowsSetupGate.IsInstalling()) { throw new InvalidOperationException("Setup did not block launches."); }
            if (end(0) != 0 || WindowsSetupGate.IsInstalling()) { throw new InvalidOperationException("Commit/rollback did not release the gate."); }
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("--setup-gate-owner"); start.ArgumentList.Add(path);
            using var child = Process.Start(start)!;
            try
            {
                if (child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult() != "gate-ready"
                    || !WindowsSetupGate.IsInstalling()) { throw new InvalidOperationException("The child did not own the gate."); }
                child.StandardInput.Close();
                if (!child.WaitForExit(5000) || child.ExitCode != 0 || WindowsSetupGate.IsInstalling())
                { throw new InvalidOperationException("The gate survived its owner."); }
            }
            finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(5000); } }
            Console.WriteLine("Setup gate checks passed: launch gate, commit/rollback release, and owner-process exit. No installer, app shutdown or driver action executed.");
            return 0;
        }
        finally { end(0); NativeLibrary.Free(library); }
    }
}

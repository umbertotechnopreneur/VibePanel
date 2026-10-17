// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace VibePanel.Photino;

/// <summary>A separate, UI-independent watchdog for the studio and all children it starts.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsMemoryGuard
{
    public const string Argument = "--memory-guard";
    private const ulong MaximumBytes = 1_000_000_000;
    private const int PollMilliseconds = 500;
    private const int NoticeMilliseconds = 2000;
    private const uint MemoryExitCode = 137;
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    /// <summary>Finish the handshake before creating the engine or WebView children.</summary>
    public static Session Start()
    {
        var token = Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(PipeName(token), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var current = Process.GetCurrentProcess();
        var executable = Environment.ProcessPath ?? throw new IOException("The app path is unavailable.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        // Also support the developer's `dotnet VibePanel.dll` invocation.
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        { start.ArgumentList.Add(typeof(WindowsMemoryGuard).Assembly.Location); }
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(current.Id.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(token);
        var guard = Process.Start(start) ?? throw new IOException("The memory guard could not start.");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            pipe.WaitForConnectionAsync(timeout.Token).GetAwaiter().GetResult();
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var clientId) || clientId != guard.Id)
            { throw new IOException("The memory guard handshake failed."); }
            var ready = new byte[1];
            pipe.ReadExactlyAsync(ready, timeout.Token).AsTask().GetAwaiter().GetResult();
            if (ready[0] != 1) { throw new IOException("The memory guard is unavailable."); }
            return new Session(guard, pipe);
        }
        catch { pipe.Dispose(); guard.Dispose(); throw; }
    }

    /// <summary>Internal child mode. Never creates a studio, engine, tray icon or USB session.</summary>
    public static int Run(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[1], CultureInfo.InvariantCulture, out var processId) || processId <= 0
            || !long.TryParse(args[2], CultureInfo.InvariantCulture, out var startedAt)
            || !Guid.TryParseExact(args[3], "N", out _)) { return 2; }
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(args[3]), PipeDirection.InOut,
                PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
            pipe.Connect(10_000);
            // A command-line PID alone never authorizes attaching a job to another process.
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverId) || serverId != processId) { return 2; }
            using var studio = Process.GetProcessById(processId);
            _ = studio.Handle; // Retain the process identity even if its PID is later reused.
            if (studio.StartTime.ToUniversalTime().Ticks != startedAt
                || !string.Equals(studio.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) { return 2; }

            // The guard already exists, so it is outside this new job. Future studio children
            // inherit membership; none can break away. Only this guard owns the job handle.
            using var job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) { throw new Win32Exception(Marshal.GetLastWin32Error()); }
            var limits = new JobExtendedLimits { Basic = new JobBasicLimits { Flags = KillOnJobClose } };
            var size = (uint)Marshal.SizeOf<JobExtendedLimits>();
            if (!SetInformationJobObject(job, ExtendedLimitInformation, ref limits, size)
                || !AssignProcessToJobObject(job, studio.SafeHandle))
            { throw new Win32Exception(Marshal.GetLastWin32Error()); }
            studio.Refresh();
            var initialPrivateBytes = (ulong)studio.PrivateMemorySize64;
            if (!QueryInformationJobObject(job, ExtendedLimitInformation, out var initialUsage, size, IntPtr.Zero))
            { throw new Win32Exception(Marshal.GetLastWin32Error()); }
            // Account conservatively for memory committed before the studio joined the job.
            var initialPeak = initialUsage.PeakJobMemoryUsed.ToUInt64();
            var priorBytes = initialPrivateBytes - Math.Min(initialPrivateBytes, initialPeak);
            pipe.WriteByte(1);
            pipe.Flush();

            using var restartLifetime = new CancellationTokenSource();
            // This existing guard is outside the studio's kill-on-close job. Launching
            // here prevents the replacement from being killed with the previous studio.
            _ = ServeRestartAsync(pipe, processId, startedAt, restartLifetime.Token);

            try
            {
                while (!studio.WaitForExit(PollMilliseconds))
                {
                    // Windows tracks the aggregate peak, including short spikes between polls.
                    if (!QueryInformationJobObject(job, ExtendedLimitInformation, out var usage, size, IntPtr.Zero))
                    {
                        if (studio.HasExited) { return 0; }
                        RecordStop("memory.query.failed");
                        return StopWithNotice(job, "The memory guard stopped working.\nVibe Panel will close in 2 seconds to protect your PC.");
                    }
                    if (priorBytes > MaximumBytes || usage.PeakJobMemoryUsed.ToUInt64() > MaximumBytes - priorBytes)
                    {
                        RecordStop("memory.limit", usage.PeakJobMemoryUsed.ToUInt64(), priorBytes);
                        return StopWithNotice(job, "Vibe Panel went over its 1 GB memory limit.\nThe app and its child processes will stop in 2 seconds.");
                    }
                }
            }
            finally { restartLifetime.Cancel(); }
            // Closing the job also stops any engine, Python or WebView left after a normal exit.
            return 0;
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or ArgumentException)
        { return 1; } // The studio refuses to open without a successful handshake.
    }

    private static async Task ServeRestartAsync(NamedPipeClientStream pipe, int studioId, long studioStart, CancellationToken token)
    {
        var command = new byte[1];
        try
        {
            while (await pipe.ReadAsync(command, token) == 1)
            {
                if (command[0] != 1) { return; }
                byte result;
                try
                {
                    using var replacement = Process.Start(WindowsElevation.CreateStartInfo(studioId, studioStart));
                    result = replacement is null ? (byte)0 : (byte)1;
                }
                catch (Win32Exception error) when (error.NativeErrorCode == 1223) { result = 2; }
                catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException) { result = 0; }
                await pipe.WriteAsync(new byte[] { result }, token);
                await pipe.FlushAsync(token);
                if (result == 1) { return; }
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    internal sealed class Session(Process guard, NamedPipeServerStream pipe) : IDisposable
    {
        internal async Task<bool> RestartAsAdministratorAsync()
        {
            await pipe.WriteAsync(new byte[] { 1 });
            await pipe.FlushAsync();
            var response = new byte[1];
            await pipe.ReadExactlyAsync(response);
            return response[0] switch
            {
                1 => true,
                2 => false,
                _ => throw new IOException("Windows could not start the administrator instance.")
            };
        }

        public void Dispose() { pipe.Dispose(); guard.Dispose(); }
    }

    public static void ShowUnavailable() => ShowNotice("The memory guard could not start.\nVibe Panel will not open without memory protection.");

    private static string PipeName(string token) => "VibePanel.MemoryGuard." + token;

    /// <summary>One bounded numeric record, only when the existing local diagnostics are enabled.</summary>
    private static void RecordStop(string reason, ulong jobPeakBytes = 0, ulong priorBytes = 0)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibePanel", "logs");
            var settings = new FileInfo(Path.Combine(directory, "diagnostics.json"));
            if (!settings.Exists || settings.Length > 256) { return; }
            using var stream = settings.OpenRead();
            using var json = JsonDocument.Parse(stream);
            if (!json.RootElement.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.True) { return; }
            File.WriteAllText(Path.Combine(directory, "memory-guard.json"), JsonSerializer.Serialize(new
            { atUtc = DateTimeOffset.UtcNow, reason, limitBytes = MaximumBytes, jobPeakBytes, priorBytes }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
    }

    private static int StopWithNotice(SafeJobHandle job, string message)
    {
        var stopped = 0;
        void Stop()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0) { return; }
            // This cannot be cancelled by the UI. Closing the handle is a second kill path.
            _ = TerminateJobObject(job, MemoryExitCode);
            job.Dispose();
        }
        var deadline = new Thread(() => { Thread.Sleep(NoticeMilliseconds); Stop(); })
        { IsBackground = true, Name = "Vibe Panel memory shutdown" };
        deadline.Start();
        try { ShowNotice(message); }
        finally { Stop(); deadline.Join(); }
        return (int)MemoryExitCode;
    }

    private static void ShowNotice(string message) => _ = MessageBox(IntPtr.Zero, message,
        "Vibe Panel - memory protection", 0x00051010); // Error icon, foreground and topmost; no UI owner.

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimits
    {
        public long PerProcessUserTime;
        public long PerJobUserTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet;
        public UIntPtr MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperations;
        public ulong WriteOperations;
        public ulong OtherOperations;
        public ulong ReadBytes;
        public ulong WriteBytes;
        public ulong OtherBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedLimits
    {
        public JobBasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeJobHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeJobHandle job, int informationClass, ref JobExtendedLimits limits, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeJobHandle job, int informationClass, out JobExtendedLimits limits, uint length, IntPtr returnedLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeJobHandle job, uint exitCode);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}

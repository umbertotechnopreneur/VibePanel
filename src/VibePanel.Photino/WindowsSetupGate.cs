// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VibePanel.Photino;

[SupportedOSPlatform("windows")]
internal static class WindowsSetupGate
{
    internal static bool IsInstalling()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var handle = OpenEvent(0x00100000, false, @"Global\UmbertoGiacobbi.VibePanel.SetupGate." + identity.User!.Value);
        if (handle == IntPtr.Zero) { return Marshal.GetLastWin32Error() != 2; }
        try { return WaitForSingleObject(handle, 0) != 258; }
        finally { CloseHandle(handle); }
    }

    internal static void ShowBusy() => MessageBox(IntPtr.Zero,
        "Vibe Panel setup is running. Open the app after installation or removal finishes.", "Vibe Panel", 0x40);

    [DllImport("kernel32.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenEvent(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);
    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string title, uint type);
}

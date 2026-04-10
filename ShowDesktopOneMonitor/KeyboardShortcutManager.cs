using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    internal sealed class KeyboardShortcutManager : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_D = 0x44;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int LLKHF_INJECTED = 0x10;

        private readonly SynchronizationContext syncContext;
        private readonly LowLevelKeyboardProc hookProc;
        private readonly IntPtr hookHandle;

        private bool leftWinDown;
        private bool rightWinDown;
        private bool consumeDKeyUp;
        private bool suppressStartMenuOnWinKeyUp;
        private bool shortcutTriggered;

        public KeyboardShortcutManager ()
        {
            syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            hookProc = HookCallback;
            hookHandle = SetHook(hookProc);
            if (hookHandle == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to install the Win + D keyboard hook.");
            }
        }

        public event EventHandler ShortcutPressed;

        public void Dispose ()
        {
            if (hookHandle != IntPtr.Zero) {
                UnhookWindowsHookEx(hookHandle);
            }
        }

        private bool IsWindowsKeyDown => leftWinDown || rightWinDown;

        private IntPtr HookCallback (int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) {
                return CallNextHookEx(hookHandle, nCode, wParam, lParam);
            }

            int message = wParam.ToInt32();
            KBDLLHOOKSTRUCT keyboardData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((keyboardData.flags & LLKHF_INJECTED) != 0) {
                return CallNextHookEx(hookHandle, nCode, wParam, lParam);
            }

            switch (message) {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    return HandleKeyDown(keyboardData.vkCode, nCode, wParam, lParam);

                case WM_KEYUP:
                case WM_SYSKEYUP:
                    return HandleKeyUp(keyboardData.vkCode, nCode, wParam, lParam);

                default:
                    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
            }
        }

        private IntPtr HandleKeyDown (int virtualKeyCode, int nCode, IntPtr wParam, IntPtr lParam)
        {
            switch (virtualKeyCode) {
                case VK_LWIN:
                    leftWinDown = true;
                    break;

                case VK_RWIN:
                    rightWinDown = true;
                    break;

                case VK_D:
                    if (IsWindowsKeyDown && !HasExtraModifiers()) {
                        consumeDKeyUp = true;
                        suppressStartMenuOnWinKeyUp = true;

                        if (!shortcutTriggered) {
                            shortcutTriggered = true;
                            syncContext.Post(_ => ShortcutPressed?.Invoke(this, EventArgs.Empty), null);
                        }

                        return (IntPtr)1;
                    }
                    break;
            }

            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private IntPtr HandleKeyUp (int virtualKeyCode, int nCode, IntPtr wParam, IntPtr lParam)
        {
            switch (virtualKeyCode) {
                case VK_LWIN:
                    leftWinDown = false;
                    HandleWinKeyReleaseSideEffects();
                    break;

                case VK_RWIN:
                    rightWinDown = false;
                    HandleWinKeyReleaseSideEffects();
                    break;

                case VK_D:
                    shortcutTriggered = false;
                    if (consumeDKeyUp) {
                        consumeDKeyUp = false;
                        return (IntPtr)1;
                    }
                    break;
            }

            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private static bool HasExtraModifiers ()
        {
            return IsKeyDown(VK_SHIFT) || IsKeyDown(VK_CONTROL) || IsKeyDown(VK_MENU);
        }

        private static bool IsKeyDown (int virtualKeyCode)
        {
            return (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
        }

        private void HandleWinKeyReleaseSideEffects ()
        {
            if (!suppressStartMenuOnWinKeyUp || IsWindowsKeyDown) {
                return;
            }

            // Tell the shell this Win press participated in a shortcut so it
            // doesn't treat the release as "open Start menu".
            keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            suppressStartMenuOnWinKeyUp = false;
        }

        private static IntPtr SetHook (LowLevelKeyboardProc hookCallback)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule) {
                return SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc (int nCode, IntPtr wParam, IntPtr lParam);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx (int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx (IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx (IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState (int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event (byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle (string lpModuleName);
    }
}

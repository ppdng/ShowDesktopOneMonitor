using System;
using System.Runtime.InteropServices;

namespace ShowDesktopOneMonitor
{
    internal static class DpiAwarenessHelper
    {
        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new IntPtr(-4);

        public static void Enable ()
        {
            if (TrySetPerMonitorV2()) {
                return;
            }

            if (TrySetPerMonitor()) {
                return;
            }

            TrySetSystemAware();
        }

        private static bool TrySetPerMonitorV2 ()
        {
            try {
                return SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
            }
            catch (EntryPointNotFoundException) {
                return false;
            }
        }

        private static bool TrySetPerMonitor ()
        {
            try {
                return SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware) == 0;
            }
            catch (DllNotFoundException) {
                return false;
            }
            catch (EntryPointNotFoundException) {
                return false;
            }
        }

        private static void TrySetSystemAware ()
        {
            try {
                SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException) {
            }
        }

        private enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext (IntPtr dpiFlag);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness (ProcessDpiAwareness value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware ();
    }
}

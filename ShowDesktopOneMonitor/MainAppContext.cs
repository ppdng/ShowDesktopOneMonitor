using FrigoTab;
using ShowDesktopOneMonitor.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    public class MainAppContext : ApplicationContext
    {
        private readonly KeyboardShortcutManager keyboardShortcutManager;
        private readonly NotifyIcon trayIcon;
        private List<DesktopWindowID>[] PrevStateByScreen = new List<DesktopWindowID>[0];

        public MainAppContext ()
        {
            Application.ThreadException += this.Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;

            trayIcon = new NotifyIcon() {
                Icon = Resources.sde,
                ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Exit", (s, e) => ExitThread()),
                }),
                Visible = true,
                Text = "Show Desktop Enhanced",
            };
            PrevStateByScreen = new List<DesktopWindowID>[Screen.AllScreens.Length];

            keyboardShortcutManager = new KeyboardShortcutManager();
            keyboardShortcutManager.ShortcutPressed += OnShortcutPressed;
        }

        private void OnShortcutPressed (object sender, EventArgs e)
        {
            OnShowDesktopKeyComb();
        }

        private void OnShowDesktopKeyComb ()
        {
            Console.WriteLine("Toogling hide windows on screen...");

            Screen currentScreen = Screen.FromPoint(Cursor.Position);
            int screenIdx = Array.IndexOf(Screen.AllScreens, currentScreen);
            List<WindowHandle> windows = GetWindowsOnScreen(currentScreen);
            List<DesktopWindowID> newWindowIDs = ConvertWindowsToIDs(windows);

            Array.Resize(ref PrevStateByScreen, Screen.AllScreens.Length);

            if (newWindowIDs.All(x => x.WindowStyle != WindowStyles.Visible) && PrevStateByScreen[screenIdx] != null
                                        && DoesPrevStateDiffersOnlyByWindowsStyle(newWindowIDs, screenIdx)) {
                restoreAllWindows(screenIdx);
            }
            else {
                minimizeAllWindows(newWindowIDs, screenIdx);
            }
        }

        private void minimizeAllWindows (List<DesktopWindowID> windowList, int screenIdx)
        {
            windowList = windowList.Select(x => new { window = x, zOrder = WindowApi.GetWindowZOrder(x.WindowHandle) })
                                                            .OrderByDescending(x => x.zOrder).Select(x => x.window).ToList();
            foreach (var window in windowList) {
                if (window.WindowStyle == WindowStyles.Visible) {
                    window.SourceHandleObj.SetMinimizeWindow();
                }
            }

            PrevStateByScreen[screenIdx] = windowList;
        }

        private void restoreAllWindows (int screenIdx)
        {
            if (PrevStateByScreen[screenIdx] != null) {
                foreach (var window in PrevStateByScreen[screenIdx].Reverse<DesktopWindowID>()) {
                    if (window.WindowStyle == WindowStyles.Visible) {
                        window.SourceHandleObj.SetRestoreWindow();
                    }
                }
            }

            PrevStateByScreen[screenIdx] = null;
        }

        private List<WindowHandle> GetWindowsOnScreen (Screen screen)
        {
            WindowFinder finder = new WindowFinder();
            return finder.Windows.Where(x => x.GetScreen().Equals(screen)).ToList();
        }

        private List<DesktopWindowID> ConvertWindowsToIDs (List<WindowHandle> windows)
        {
            List<DesktopWindowID> list = new List<DesktopWindowID>(windows.Count);
            for (int i = 0; i < windows.Count; i++) {
                list.Add(new DesktopWindowID(windows[i]));
            }
            return list;
        }

        private bool DoesPrevStateDiffersOnlyByWindowsStyle (List<DesktopWindowID> newList, int screenIdx)
        {
            if (PrevStateByScreen[screenIdx] == null) return false;
            if (newList.Count != PrevStateByScreen[screenIdx].Count) return false;
            if (false == newList.All(x => PrevStateByScreen[screenIdx].Contains(x))) return false;

            return false == newList.All(x => PrevStateByScreen[screenIdx].First(y => y == x).WindowStyle.Equals(x.WindowStyle));
        }

        protected override void ExitThreadCore ()
        {
            keyboardShortcutManager?.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            SettingsManager.Save();
            base.ExitThreadCore();
        }

        private void Application_ThreadException (object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("Необработанное исключение: " + e.Exception.ToString(), "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Необработанное исключение: {e.ExceptionObject as Exception}", "Show Desktop Enhanced", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public class DesktopWindowID : IEquatable<DesktopWindowID>
    {
        public WindowHandle SourceHandleObj;

        public IntPtr WindowHandle = IntPtr.Zero;
        public WindowStyles WindowStyle = WindowStyles.Disabled;

        public DesktopWindowID (WindowHandle sourceHandleObj)
        {
            this.SourceHandleObj = sourceHandleObj;
            this.WindowHandle = this.SourceHandleObj.GetHandle();
            this.WindowStyle = WindowStyles.Disabled;

            var wndStyle = this.SourceHandleObj.GetWindowStyles();
            if (wndStyle.HasFlag(WindowStyles.Minimize))
                this.WindowStyle = WindowStyles.Minimize;
            else if (wndStyle.HasFlag(WindowStyles.Visible))
                this.WindowStyle = WindowStyles.Visible;
        }

        public bool Equals (DesktopWindowID other)
        {
            return Equals((object)other);
        }

        public override int GetHashCode ()
        {
            return this.WindowHandle.GetHashCode();
        }

        public override bool Equals (object obj)
        {
            if (obj == null) return false;
            return this.GetHashCode() == obj.GetHashCode();
        }

        public static bool operator ==(DesktopWindowID obj1, DesktopWindowID obj2)
        {
            if (ReferenceEquals(obj1, null)) {
                return ReferenceEquals(obj2, null);
            }
            return obj1.Equals(obj2);
        }

        public static bool operator !=(DesktopWindowID obj1, DesktopWindowID obj2)
        {
            return (obj1 == obj2) == false;
        }
    }
}

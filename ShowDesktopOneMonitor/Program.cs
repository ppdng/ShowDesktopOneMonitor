using System;
using System.Windows.Forms;

namespace ShowDesktopOneMonitor
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main ()
        {
            DpiAwarenessHelper.Enable();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainAppContext());
        }
    }
}

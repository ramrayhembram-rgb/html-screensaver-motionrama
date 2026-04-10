using System;
using System.Windows.Forms;

namespace HtmlScreensaver
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = args.Length > 0 ? args[0].Trim().ToLower() : "/s";

            // /s  — run screensaver
            // /p  — preview (tiny window inside Screen Saver Settings dialog)
            // /c  — configure (settings dialog)

            if (mode.StartsWith("/c"))
            {
                Application.Run(new SettingsForm());
            }
            else if (mode.StartsWith("/p"))
            {
                // Preview handle is passed as second arg
                if (args.Length > 1 && long.TryParse(args[1], out long hwnd))
                {
                    Application.Run(new PreviewForm(new IntPtr(hwnd)));
                }
            }
            else // /s or no arg
            {
                // Launch one fullscreen window per monitor
                foreach (Screen screen in Screen.AllScreens)
                {
                    var form = new ScreensaverForm(screen.Bounds);
                    form.Show();
                }
                Application.Run();
            }
        }
    }
}

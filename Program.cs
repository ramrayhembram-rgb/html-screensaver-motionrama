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

            if (mode.StartsWith("/c"))
            {
                // Show settings dialog and exit immediately when it closes (OK or Cancel)
                using (var form = new SettingsForm())
                {
                    form.ShowDialog();
                }
                // Application exits here automatically — no Application.Run()
            }
            else if (mode.StartsWith("/p"))
            {
                if (args.Length > 1 && long.TryParse(args[1], out long hwnd))
                {
                    Application.Run(new PreviewForm(new IntPtr(hwnd)));
                }
            }
            else // /s or no arg
            {
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

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace HtmlScreensaver
{
    /// <summary>
    /// Renders into the tiny preview pane inside Windows' Screen Saver Settings dialog.
    /// Windows passes the HWND of the preview pane via /p <handle>.
    /// </summary>
    public class PreviewForm : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private WebView2 _webView;

        public PreviewForm(IntPtr previewHandle)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;

            // Size to the preview pane
            if (GetClientRect(previewHandle, out RECT rc))
                this.Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
            else
                this.Size = new Size(200, 150);

            // Reparent into the preview pane
            this.Load += (s, e) => SetParent(this.Handle, previewHandle);

            _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(_webView);

            this.Load += OnLoad;
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            var settings = ScreensaverSettings.Load();
            try
            {
                await _webView.EnsureCoreWebView2Async(null);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                string url = settings.UseLocalhost
                    ? $"http://localhost:{settings.LocalhostPort}/"
                    : (!string.IsNullOrWhiteSpace(settings.HtmlPath)
                        ? "file:///" + settings.HtmlPath.Replace('\\', '/')
                        : null);

                if (url != null)
                    _webView.CoreWebView2.Navigate(url);
                else
                    _webView.CoreWebView2.NavigateToString(
                        "<body style='background:#000;color:#444;font:12px sans-serif;" +
                        "display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>" +
                        "No file set</body>");
            }
            catch { }
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace HtmlScreensaver
{
    public class ScreensaverForm : Form
    {
        // ── Fields ───────────────────────────────────────────────
        private readonly ScreensaverSettings _settings;
        private WebView2 _webView;
        private ExitButton _exitButton;
        private System.Windows.Forms.Timer _hoverFadeTimer;

        private bool _isFirstMouseEvent = true;
        private Point _firstMousePos;
        private bool _exitButtonVisible = false;

        // ── Constructor ──────────────────────────────────────────
        public ScreensaverForm(Rectangle bounds)
        {
            _settings = ScreensaverSettings.Load();

            // Form setup
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = bounds;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Cursor = Cursors.None;
            this.ShowInTaskbar = false;

            InitWebView();
            InitExitButton();
            InitHoverTimer();

            this.Load += OnLoad;
        }

        // ── Initialisation ───────────────────────────────────────
        private void InitWebView()
        {
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            Controls.Add(_webView);
        }

        private void InitExitButton()
        {
            _exitButton = new ExitButton();
            _exitButton.Clicked += (s, e) => ExitScreensaver();
            Controls.Add(_exitButton);
            _exitButton.BringToFront();

            PositionExitButton();

            // Initial visibility based on mode
            switch (_settings.ExitMode)
            {
                case ExitButtonMode.Hidden:
                    _exitButton.Visible = false;
                    break;
                case ExitButtonMode.AlwaysVisible:
                    _exitButton.Visible = true;
                    _exitButton.SetOpacity(1f);
                    break;
                case ExitButtonMode.HoverVisible:
                    _exitButton.Visible = false;
                    _exitButton.SetOpacity(0f);
                    break;
            }
        }

        private void InitHoverTimer()
        {
            _hoverFadeTimer = new System.Windows.Forms.Timer
            {
                Interval = _settings.HoverFadeDelaySecs * 1000
            };
            _hoverFadeTimer.Tick += (s, e) =>
            {
                _hoverFadeTimer.Stop();
                if (_settings.ExitMode == ExitButtonMode.HoverVisible)
                    FadeOutExitButton();
            };
        }

        private void PositionExitButton()
        {
            const int margin = 20;
            int w = _exitButton.Width;
            int h = _exitButton.Height;

            Point pos = _settings.ButtonCorner switch
            {
                1 => new Point(margin, margin),                                          // TopLeft
                2 => new Point(this.Width - w - margin, this.Height - h - margin),      // BottomRight
                3 => new Point(margin, this.Height - h - margin),                       // BottomLeft
                _ => new Point(this.Width - w - margin, margin)                         // TopRight (default)
            };

            _exitButton.Location = pos;
        }

        // ── Load ─────────────────────────────────────────────────
        private async void OnLoad(object sender, EventArgs e)
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);

                // Disable context menu and devtools in screensaver mode
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                string url = BuildUrl();
                if (!string.IsNullOrEmpty(url))
                    _webView.CoreWebView2.Navigate(url);
                else
                    _webView.CoreWebView2.NavigateToString(FallbackHtml());
            }
            catch
            {
                _webView.CoreWebView2?.NavigateToString(FallbackHtml());
            }
        }

        private string BuildUrl()
        {
            if (_settings.UseLocalhost)
                return $"http://localhost:{_settings.LocalhostPort}/";

            if (!string.IsNullOrWhiteSpace(_settings.HtmlPath))
                return "file:///" + _settings.HtmlPath.Replace('\\', '/');

            return null;
        }

        private static string FallbackHtml() => @"
<!DOCTYPE html><html><body style='margin:0;background:#000;display:flex;
align-items:center;justify-content:center;height:100vh;'>
<p style='color:#555;font-family:sans-serif;font-size:18px;'>
  No HTML file configured. Right-click the screensaver in Screen Saver Settings → Settings.
</p></body></html>";

        // ── Input handling ───────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            HandleMouseActivity(e.Location);
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Only exit on raw mouse click if no button is shown
            if (_settings.ExitMode == ExitButtonMode.Hidden)
                ExitScreensaver();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || _settings.ExitMode == ExitButtonMode.Hidden)
                ExitScreensaver();
            base.OnKeyDown(e);
        }

        private void HandleMouseActivity(Point pos)
        {
            // First event — record position, don't exit yet (Windows fires one immediately)
            if (_isFirstMouseEvent)
            {
                _isFirstMouseEvent = false;
                _firstMousePos = pos;
                return;
            }

            int dx = Math.Abs(pos.X - _firstMousePos.X);
            int dy = Math.Abs(pos.Y - _firstMousePos.Y);

            switch (_settings.ExitMode)
            {
                case ExitButtonMode.Hidden:
                    if (dx > _settings.MouseMoveThresholdPx || dy > _settings.MouseMoveThresholdPx)
                        ExitScreensaver();
                    break;

                case ExitButtonMode.AlwaysVisible:
                    // Mouse movement alone doesn't exit — user must click the button or press Esc
                    break;

                case ExitButtonMode.HoverVisible:
                    // Show button on any movement, restart the fade timer
                    if (dx > 2 || dy > 2)
                        ShowExitButton();
                    break;
            }
        }

        // ── Exit button visibility ───────────────────────────────
        private void ShowExitButton()
        {
            _hoverFadeTimer.Stop();
            if (!_exitButtonVisible)
            {
                _exitButton.Visible = true;
                _exitButton.FadeIn();
                _exitButtonVisible = true;
                this.Cursor = Cursors.Default;
            }
            _hoverFadeTimer.Start();
        }

        private void FadeOutExitButton()
        {
            _exitButton.FadeOut(() =>
            {
                _exitButton.Visible = false;
                _exitButtonVisible = false;
                this.Cursor = Cursors.None;
            });
        }

        // ── Exit ─────────────────────────────────────────────────
        private void ExitScreensaver()
        {
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hoverFadeTimer?.Dispose();
                _webView?.Dispose();
                _exitButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

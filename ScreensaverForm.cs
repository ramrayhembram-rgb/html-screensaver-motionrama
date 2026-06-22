using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace HtmlScreensaver
{
    public class ScreensaverForm : Form
    {
        private readonly ScreensaverSettings _settings;
        private WebView2 _webView;
        private ExitButton _exitButton;
        private System.Windows.Forms.Timer _hoverFadeTimer;
        private System.Windows.Forms.Timer _cursorHideTimer;

        // Blank cursor for when we want to hide the OS cursor
        private static readonly Cursor _blankCursor = CreateBlankCursor();

        private bool _isFirstMouseEvent = true;
        private Point _firstMousePos;
        private bool _exitButtonVisible = false;

        private static Cursor CreateBlankCursor()
        {
            var bmp = new Bitmap(1, 1);
            bmp.SetPixel(0, 0, Color.Transparent);
            return new Cursor(bmp.GetHicon());
        }

        public ScreensaverForm(Rectangle bounds)
        {
            _settings = ScreensaverSettings.Load();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Bounds = bounds;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.ShowInTaskbar = false;

            // Default cursor based on mode
            // Hidden: always hide OS cursor (comet is drawn in WebView JS)
            // AlwaysVisible: always show OS cursor (comet drawn in WebView JS)
            // HoverVisible: hide until mouse moves (comet drawn in WebView JS)
            this.Cursor = _settings.ExitMode == ExitButtonMode.Hidden
                ? _blankCursor
                : Cursors.Default;

            _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(_webView);

            _exitButton = new ExitButton();
            _exitButton.Clicked += (s, e) => ExitScreensaver();
            Controls.Add(_exitButton);
            _exitButton.BringToFront();
            PositionExitButton();

            // Exit button initial state
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
                    this.Cursor = _blankCursor; // start hidden, show on move
                    break;
            }

            // Timer to fade out exit button after idle (HoverVisible)
            _hoverFadeTimer = new System.Windows.Forms.Timer
            {
                Interval = _settings.HoverFadeDelaySecs * 1000
            };
            _hoverFadeTimer.Tick += (s, e) =>
            {
                _hoverFadeTimer.Stop();
                if (_settings.ExitMode == ExitButtonMode.HoverVisible)
                {
                    FadeOutExitButton();
                    // Also hide OS cursor when idle in HoverVisible mode
                    this.Cursor = _blankCursor;
                    // Tell the WebView to hide the comet too
                    _webView.CoreWebView2?.ExecuteScriptAsync("window.__cometHide && window.__cometHide()");
                }
            };

            this.Load += OnLoad;
        }

        private void PositionExitButton()
        {
            const int margin = 20;
            int w = _exitButton.Width;
            int h = _exitButton.Height;
            Point pos = _settings.ButtonCorner switch
            {
                1 => new Point(margin, margin),
                2 => new Point(this.Width - w - margin, this.Height - h - margin),
                3 => new Point(margin, this.Height - h - margin),
                _ => new Point(this.Width - w - margin, margin)
            };
            _exitButton.Location = pos;
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                // Inject comet cursor script after page loads
                _webView.CoreWebView2.DOMContentLoaded += async (ws, we) =>
                {
                    string cometMode = _settings.ExitMode switch
                    {
                        ExitButtonMode.Hidden => "always",        // comet always visible
                        ExitButtonMode.AlwaysVisible => "always", // comet always visible
                        ExitButtonMode.HoverVisible => "hover",   // comet shown on move, hides when idle
                        _ => "always"
                    };
                    await _webView.CoreWebView2.ExecuteScriptAsync(GetCometScript(cometMode, _settings.HoverFadeDelaySecs));
                };

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

        /// <summary>
        /// Returns JS that draws a physics comet cursor on a canvas overlay.
        /// mode = "always" | "hover"
        /// </summary>
        private static string GetCometScript(string mode, int fadeDelaySecs = 3) => $@"
(function() {{
    // Remove any previous instance
    var old = document.getElementById('__cometCanvas');
    if (old) old.remove();

    var canvas = document.createElement('canvas');
    canvas.id = '__cometCanvas';
    canvas.style.cssText = 'position:fixed;top:0;left:0;width:100vw;height:100vh;pointer-events:none;z-index:999999;';
    canvas.width  = window.innerWidth;
    canvas.height = window.innerHeight;
    document.body.appendChild(canvas);

    var ctx = canvas.getContext('2d');
    var mode = '{mode}';

    var mx = window.innerWidth / 2;
    var my = window.innerHeight / 2;
    var visible = (mode === 'always');

    // Tail particles
    var TAIL = 28;
    var tail = [];
    for (var i = 0; i < TAIL; i++) {{
        tail.push({{ x: mx, y: my, vx: 0, vy: 0 }});
    }}

    // Head position (smooth follow)
    var hx = mx, hy = my;
    var pvx = 0, pvy = 0;

    var hideTimer = null;

    document.addEventListener('mousemove', function(e) {{
        mx = e.clientX;
        my = e.clientY;
        if (mode === 'hover') {{
            visible = true;
            clearTimeout(hideTimer);
            hideTimer = setTimeout(function() {{
                visible = false;
            }}, {fadeDelaySecs} * 1000);
        }}
    }});

    // Called from C# to hide comet
    window.__cometHide = function() {{ visible = false; }};

    function lerp(a, b, t) {{ return a + (b - a) * t; }}

    function draw() {{
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        if (!visible) {{
            requestAnimationFrame(draw);
            return;
        }}

        // Smooth head movement with spring physics
        var tx = mx, ty = my;
        var ax = (tx - hx) * 0.22;
        var ay = (ty - hy) * 0.22;
        pvx = pvx * 0.72 + ax;
        pvy = pvy * 0.72 + ay;
        hx += pvx;
        hy += pvy;

        // Speed for glow intensity
        var speed = Math.hypot(pvx, pvy);
        var glow = Math.min(1, speed / 18);

        // Tail follows head with physics chain
        tail[0].x = lerp(tail[0].x, hx, 0.55);
        tail[0].y = lerp(tail[0].y, hy, 0.55);
        for (var i = 1; i < TAIL; i++) {{
            tail[i].x = lerp(tail[i].x, tail[i-1].x, 0.45 - i * 0.008);
            tail[i].y = lerp(tail[i].y, tail[i-1].y, 0.45 - i * 0.008);
        }}

        // Draw tail
        for (var i = TAIL - 1; i >= 0; i--) {{
            var t = 1 - i / TAIL;
            var alpha = t * t * (0.35 + glow * 0.5);
            var r = (1 - i / TAIL) * 5 + 1;

            // Tail gradient colour: orange → white at head
            var red   = Math.round(lerp(255, 255, t));
            var green = Math.round(lerp(80,  220, t));
            var blue  = Math.round(lerp(0,   180, t));

            ctx.beginPath();
            ctx.arc(tail[i].x, tail[i].y, r, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(' + red + ',' + green + ',' + blue + ',' + alpha + ')';
            ctx.fill();
        }}

        // Head glow layers
        var g1 = ctx.createRadialGradient(hx, hy, 0, hx, hy, 22 + glow * 14);
        g1.addColorStop(0,   'rgba(255,255,255,' + (0.9 + glow * 0.1) + ')');
        g1.addColorStop(0.3, 'rgba(255,220,120,' + (0.6 + glow * 0.3) + ')');
        g1.addColorStop(1,   'rgba(255,100,0,0)');
        ctx.beginPath();
        ctx.arc(hx, hy, 22 + glow * 14, 0, Math.PI * 2);
        ctx.fillStyle = g1;
        ctx.fill();

        // Hard bright core
        ctx.beginPath();
        ctx.arc(hx, hy, 3.5, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(255,255,255,0.98)';
        ctx.fill();

        requestAnimationFrame(draw);
    }}

    draw();
}})();
";

        private string BuildUrl()
        {
            if (_settings.UseLocalhost)
                return $"http://localhost:{_settings.LocalhostPort}/";
            if (!string.IsNullOrWhiteSpace(_settings.HtmlPath))
                return "file:///" + _settings.HtmlPath.Replace('\\', '/');
            return null;
        }

        private static string FallbackHtml() =>
            "<body style='margin:0;background:#000;display:flex;align-items:center;" +
            "justify-content:center;height:100vh;'><p style='color:#555;font-family:sans-serif;" +
            "font-size:18px;'>No HTML file configured. Open Screen Saver Settings → Settings.</p></body>";

        protected override void OnMouseMove(MouseEventArgs e)
        {
            HandleMouseActivity(e.Location);
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
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
                    // Never exit on mouse move; only Esc or X button exits
                    break;

                case ExitButtonMode.HoverVisible:
                    if (dx > 2 || dy > 2)
                        ShowExitButton();
                    break;
            }
        }

        private void ShowExitButton()
        {
            _hoverFadeTimer.Stop();
            // Show OS cursor when active in HoverVisible mode
            this.Cursor = Cursors.Default;
            if (!_exitButtonVisible)
            {
                _exitButton.Visible = true;
                _exitButton.FadeIn();
                _exitButtonVisible = true;
            }
            _hoverFadeTimer.Start();
        }

        private void FadeOutExitButton()
        {
            _exitButton.FadeOut(() =>
            {
                _exitButton.Visible = false;
                _exitButtonVisible = false;
            });
        }

        private void ExitScreensaver() => Application.Exit();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hoverFadeTimer?.Dispose();
                _cursorHideTimer?.Dispose();
                _webView?.Dispose();
                _exitButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

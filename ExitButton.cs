using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HtmlScreensaver
{
    /// <summary>
    /// A self-contained, owner-drawn exit button that supports smooth
    /// opacity fade-in / fade-out animation without requiring a layered form.
    /// It renders onto a transparent-background Panel using per-pixel alpha
    /// via a Timer-driven repaint cycle.
    /// </summary>
    public class ExitButton : Panel
    {
        // ── Events ───────────────────────────────────────────────
        public event EventHandler Clicked;

        // ── Sizing / layout ──────────────────────────────────────
        private const int ButtonSize = 44;   // outer circle diameter
        private const int IconSize = 14;     // X cross arm length

        // ── Fade animation ───────────────────────────────────────
        private float _opacity = 0f;         // 0..1
        private float _targetOpacity = 0f;
        private const float FadeStep = 0.08f; // per tick (~16 ms → ~8 frames to full)
        private System.Windows.Forms.Timer _fadeTimer;

        // ── Hover state ──────────────────────────────────────────
        private bool _isHovered = false;
        private Action _fadeOutCallback;

        // ── Constructor ──────────────────────────────────────────
        public ExitButton()
        {
            this.Size = new Size(ButtonSize, ButtonSize);
            this.BackColor = Color.Transparent;

            // Enable transparent background painting
            this.SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += OnFadeTick;

            this.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            this.MouseClick += (s, e) => Clicked?.Invoke(this, EventArgs.Empty);
            this.Cursor = Cursors.Hand;
        }

        // ── Public API ───────────────────────────────────────────
        public void SetOpacity(float value)
        {
            _opacity = Math.Max(0f, Math.Min(1f, value));
            Invalidate();
        }

        public void FadeIn()
        {
            _fadeOutCallback = null;
            _targetOpacity = 1f;
            if (!_fadeTimer.Enabled) _fadeTimer.Start();
        }

        public void FadeOut(Action onComplete = null)
        {
            _fadeOutCallback = onComplete;
            _targetOpacity = 0f;
            if (!_fadeTimer.Enabled) _fadeTimer.Start();
        }

        // ── Fade tick ────────────────────────────────────────────
        private void OnFadeTick(object sender, EventArgs e)
        {
            if (_opacity < _targetOpacity)
            {
                _opacity = Math.Min(_opacity + FadeStep, _targetOpacity);
            }
            else if (_opacity > _targetOpacity)
            {
                _opacity = Math.Max(_opacity - FadeStep, _targetOpacity);
            }

            Invalidate();

            if (Math.Abs(_opacity - _targetOpacity) < 0.001f)
            {
                _opacity = _targetOpacity;
                _fadeTimer.Stop();

                if (_targetOpacity == 0f && _fadeOutCallback != null)
                {
                    var cb = _fadeOutCallback;
                    _fadeOutCallback = null;
                    cb.Invoke();
                }
            }
        }

        // ── Painting ─────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_opacity <= 0f) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int alpha = (int)(_opacity * (_isHovered ? 220 : 160));
            alpha = Math.Max(0, Math.Min(255, alpha));

            // Circle background
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(alpha, 20, 20, 20)))
            {
                g.FillEllipse(bg, 2, 2, ButtonSize - 4, ButtonSize - 4);
            }

            // Circle border
            int borderAlpha = Math.Min(255, alpha + 40);
            using (Pen border = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 1f))
            {
                g.DrawEllipse(border, 2, 2, ButtonSize - 4, ButtonSize - 4);
            }

            // X cross
            int cx = ButtonSize / 2;
            int cy = ButtonSize / 2;
            int half = IconSize / 2;
            int crossAlpha = Math.Min(255, (int)(_opacity * 255));

            using (Pen cross = new Pen(Color.FromArgb(crossAlpha, 255, 255, 255), 2f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(cross, cx - half, cy - half, cx + half, cy + half);
                g.DrawLine(cross, cx + half, cy - half, cx - half, cy + half);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _fadeTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}

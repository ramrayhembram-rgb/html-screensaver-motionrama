using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HtmlScreensaver
{
    public class SettingsForm : Form
    {
        private ScreensaverSettings _settings;

        // Controls
        private TextBox _pathBox;
        private Button _browseBtn;
        private CheckBox _useLocalhostChk;
        private NumericUpDown _portSpinner;
        private RadioButton _modeHiddenRadio;
        private RadioButton _modeAlwaysRadio;
        private RadioButton _modeHoverRadio;
        private NumericUpDown _fadeDelaySpinner;
        private NumericUpDown _mouseMoveSpinner;
        private ComboBox _cornerCombo;
        private Label _fadeDelayLabel;
        private Button _okBtn;
        private Button _cancelBtn;
        private Button _testBtn;

        public SettingsForm()
        {
            _settings = ScreensaverSettings.Load();
            BuildUI();
            LoadSettingsIntoUI();
        }

        private void BuildUI()
        {
            this.Text = "HTML Screensaver — Settings";
            this.Size = new Size(480, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            int y = 16;
            int lx = 16, cx = 140, w = 290;

            // ── HTML File ─────────────────────────────────────────
            AddLabel("HTML file or folder:", lx, y);
            y += 22;
            _pathBox = new TextBox { Left = lx, Top = y, Width = w - 36, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            _browseBtn = new Button { Text = "...", Left = lx + w - 32, Top = y - 1, Width = 32, Height = 23 };
            _browseBtn.Click += OnBrowse;
            Controls.Add(_pathBox);
            Controls.Add(_browseBtn);
            y += 30;

            _useLocalhostChk = new CheckBox { Text = "Use localhost server instead", Left = lx, Top = y, AutoSize = true };
            _useLocalhostChk.CheckedChanged += (s, e) => UpdateLocalhostUI();
            Controls.Add(_useLocalhostChk);
            y += 24;

            AddLabel("Port:", lx + 20, y);
            _portSpinner = new NumericUpDown { Left = cx, Top = y - 2, Width = 80, Minimum = 1, Maximum = 65535 };
            Controls.Add(_portSpinner);
            y += 32;

            // Separator
            AddSeparator(y); y += 16;

            // ── Exit Button Mode ──────────────────────────────────
            AddLabel("Exit button:", lx, y, bold: true); y += 24;

            _modeHiddenRadio = new RadioButton
            {
                Text = "Hidden — mouse movement or Esc key exits",
                Left = lx + 8, Top = y, AutoSize = true
            };
            Controls.Add(_modeHiddenRadio);
            y += 22;

            _modeAlwaysRadio = new RadioButton
            {
                Text = "Always visible — shown in corner (press Esc or click X to exit)",
                Left = lx + 8, Top = y, AutoSize = true
            };
            Controls.Add(_modeAlwaysRadio);
            y += 22;

            _modeHoverRadio = new RadioButton
            {
                Text = "Hover reveal — fades in on mouse move, fades out when idle",
                Left = lx + 8, Top = y, AutoSize = true
            };
            _modeHoverRadio.CheckedChanged += (s, e) => UpdateModeUI();
            Controls.Add(_modeHoverRadio);
            y += 28;

            _fadeDelayLabel = AddLabel("Fade-out delay (seconds):", lx + 20, y);
            _fadeDelaySpinner = new NumericUpDown { Left = cx + 80, Top = y - 2, Width = 60, Minimum = 1, Maximum = 30 };
            Controls.Add(_fadeDelaySpinner);
            y += 26;

            AddLabel("Mouse move threshold (px):", lx, y);
            _mouseMoveSpinner = new NumericUpDown { Left = cx + 80, Top = y - 2, Width = 60, Minimum = 1, Maximum = 200 };
            Controls.Add(_mouseMoveSpinner);
            y += 32;

            // Separator
            AddSeparator(y); y += 16;

            // ── Corner ───────────────────────────────────────────
            AddLabel("Button corner:", lx, y, bold: true);
            _cornerCombo = new ComboBox
            {
                Left = cx, Top = y - 2, Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cornerCombo.Items.AddRange(new[] { "Top right", "Top left", "Bottom right", "Bottom left" });
            Controls.Add(_cornerCombo);
            y += 40;

            // ── Buttons ──────────────────────────────────────────
            _testBtn = new Button { Text = "Test (5 sec)", Left = lx, Top = y, Width = 100, Height = 28 };
            _testBtn.Click += OnTest;
            Controls.Add(_testBtn);

            _okBtn = new Button { Text = "OK", Left = 280, Top = y, Width = 80, Height = 28, DialogResult = DialogResult.OK };
            _okBtn.Click += OnOK;
            Controls.Add(_okBtn);

            _cancelBtn = new Button { Text = "Cancel", Left = 368, Top = y, Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
            Controls.Add(_cancelBtn);

            this.AcceptButton = _okBtn;
            this.CancelButton = _cancelBtn;
        }

        private Label AddLabel(string text, int x, int y, bool bold = false)
        {
            var lbl = new Label
            {
                Text = text, Left = x, Top = y, AutoSize = true,
                Font = bold ? new Font("Segoe UI", 9f, FontStyle.Bold) : this.Font
            };
            Controls.Add(lbl);
            return lbl;
        }

        private void AddSeparator(int y)
        {
            var sep = new Panel { Left = 8, Top = y, Width = this.ClientSize.Width - 16, Height = 1, BackColor = Color.LightGray };
            Controls.Add(sep);
        }

        private void LoadSettingsIntoUI()
        {
            _pathBox.Text = _settings.HtmlPath;
            _useLocalhostChk.Checked = _settings.UseLocalhost;
            _portSpinner.Value = _settings.LocalhostPort;
            _fadeDelaySpinner.Value = _settings.HoverFadeDelaySecs;
            _mouseMoveSpinner.Value = _settings.MouseMoveThresholdPx;
            _cornerCombo.SelectedIndex = Math.Max(0, Math.Min(3, _settings.ButtonCorner));

            _modeHiddenRadio.Checked = _settings.ExitMode == ExitButtonMode.Hidden;
            _modeAlwaysRadio.Checked = _settings.ExitMode == ExitButtonMode.AlwaysVisible;
            _modeHoverRadio.Checked = _settings.ExitMode == ExitButtonMode.HoverVisible;
            if (!_modeHiddenRadio.Checked && !_modeAlwaysRadio.Checked)
                _modeHoverRadio.Checked = true;

            UpdateLocalhostUI();
            UpdateModeUI();
        }

        private void UpdateLocalhostUI()
        {
            bool useLoc = _useLocalhostChk.Checked;
            _pathBox.Enabled = !useLoc;
            _browseBtn.Enabled = !useLoc;
            _portSpinner.Enabled = useLoc;
        }

        private void UpdateModeUI()
        {
            bool isHover = _modeHoverRadio.Checked;
            _fadeDelayLabel.Enabled = isHover;
            _fadeDelaySpinner.Enabled = isHover;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select HTML file",
                Filter = "HTML files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
                FileName = _pathBox.Text
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                _pathBox.Text = dlg.FileName;
        }

        private void OnOK(object sender, EventArgs e)
        {
            SaveUIToSettings();
            _settings.Save();
        }

        private void SaveUIToSettings()
        {
            _settings.HtmlPath = _pathBox.Text.Trim();
            _settings.UseLocalhost = _useLocalhostChk.Checked;
            _settings.LocalhostPort = (int)_portSpinner.Value;
            _settings.HoverFadeDelaySecs = (int)_fadeDelaySpinner.Value;
            _settings.MouseMoveThresholdPx = (int)_mouseMoveSpinner.Value;
            _settings.ButtonCorner = _cornerCombo.SelectedIndex;

            if (_modeHiddenRadio.Checked) _settings.ExitMode = ExitButtonMode.Hidden;
            else if (_modeAlwaysRadio.Checked) _settings.ExitMode = ExitButtonMode.AlwaysVisible;
            else _settings.ExitMode = ExitButtonMode.HoverVisible;
        }

        private void OnTest(object sender, EventArgs e)
        {
            SaveUIToSettings();
            _settings.Save();

            // Launch screensaver for 5 seconds then auto-close
            var testForm = new ScreensaverForm(Screen.PrimaryScreen.Bounds);
            var killTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            killTimer.Tick += (ts, te) => { killTimer.Stop(); testForm.Close(); };
            testForm.Show();
            killTimer.Start();
        }
    }
}

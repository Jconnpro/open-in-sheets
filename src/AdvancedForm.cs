using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenInSheets
{
    /// <summary>
    /// Backend choice plus the Apps Script fields. Deliberately out of the main flow -
    /// almost nobody needs it, and the people who do go looking for it.
    /// </summary>
    class AdvancedForm : Form
    {
        readonly Config _config;
        readonly RadioButton _useGoogle;
        readonly RadioButton _useAppsScript;
        readonly TextBox _endpoint;
        readonly TextBox _secret;
        readonly FlatButton _test;
        readonly Label _endpointLabel;
        readonly Label _secretLabel;
        readonly NumericUpDown _maxMB;

        public AdvancedForm(Config config)
        {
            _config = config;

            Text = "Advanced settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Page;
            Font = Theme.Body;
            ClientSize = new Size(500, 482);

            Controls.Add(Theme.Text("Where your files go", Theme.Title, Theme.Ink, 28, 24, 440, 32));

            Card backend = new Card();
            backend.SetBounds(28, 70, 444, 186);

            _useGoogle = Radio("Sign in with Google", 20, 18, !config.UsesAppsScript);
            _useAppsScript = Radio("Use my own Apps Script", 20, 84, config.UsesAppsScript);
            _useGoogle.CheckedChanged += delegate { SyncEnabled(); };

            backend.Controls.Add(_useGoogle);
            backend.Controls.Add(Theme.Text("Nothing to set up. Uses the narrowest Drive permission\r\nthere is, so it only ever sees files it made.",
                                            Theme.Small, Theme.Muted, 40, 40, 390, 36));
            backend.Controls.Add(_useAppsScript);
            backend.Controls.Add(Theme.Text("Runs entirely under your own Google account. Setup steps\r\nare in apps-script/Code.gs in the project.",
                                            Theme.Small, Theme.Muted, 40, 106, 390, 36));
            Controls.Add(backend);

            _endpointLabel = Theme.Text("Web app address (ends in /exec)", Theme.Small, Theme.Ink, 28, 274, 300, 18);
            _endpoint = Field(config.AppsScriptEndpoint, 28, 294, 444);

            _secretLabel = Theme.Text("Secret", Theme.Small, Theme.Ink, 28, 334, 200, 18);
            _secret = Field(config.AppsScriptSecret, 28, 354, 340);

            _test = FlatButton.Secondary("Test");
            _test.SetBounds(380, 352, 92, 30);
            _test.Click += delegate
            {
                string problem;
                if (AppsScriptClient.Ping(_endpoint.Text.Trim(), out problem))
                    Ui.Info("That address answered correctly.");
                else
                    Ui.Error("No luck.\r\n\r\n" + problem);
            };

            Controls.Add(Theme.Text("Largest file to upload (MB)", Theme.Small, Theme.Ink, 28, 402, 180, 18));
            _maxMB = new NumericUpDown();
            _maxMB.Minimum = 1;
            _maxMB.Maximum = 100;
            _maxMB.Value = Math.Max(1, Math.Min(100, config.MaxMB));
            _maxMB.BorderStyle = BorderStyle.FixedSingle;
            _maxMB.Font = Theme.Body;
            _maxMB.SetBounds(210, 399, 64, 26);

            FlatButton save = FlatButton.Primary("Save");
            save.SetBounds(292, 434, 86, 34);
            save.DialogResult = DialogResult.OK;
            save.Click += delegate { Save(); };

            FlatButton cancel = FlatButton.Secondary("Cancel");
            cancel.SetBounds(386, 434, 86, 34);
            cancel.DialogResult = DialogResult.Cancel;

            AcceptButton = save;
            CancelButton = cancel;

            Controls.Add(_endpointLabel);
            Controls.Add(_endpoint);
            Controls.Add(_secretLabel);
            Controls.Add(_secret);
            Controls.Add(_test);
            Controls.Add(_maxMB);
            Controls.Add(save);
            Controls.Add(cancel);

            SyncEnabled();
        }

        /// <summary>The Apps Script fields mean nothing unless that backend is chosen.</summary>
        void SyncEnabled()
        {
            bool script = _useAppsScript.Checked;
            _endpoint.Enabled = script;
            _secret.Enabled = script;
            _test.Enabled = script;
            _endpointLabel.ForeColor = script ? Theme.Ink : Theme.Muted;
            _secretLabel.ForeColor = script ? Theme.Ink : Theme.Muted;
        }

        RadioButton Radio(string text, int x, int y, bool chosen)
        {
            RadioButton radio = new RadioButton();
            radio.Text = text;
            radio.Font = Theme.Heading;
            radio.ForeColor = Theme.Ink;
            radio.BackColor = Theme.Surface;
            radio.Checked = chosen;
            radio.Cursor = Cursors.Hand;
            radio.SetBounds(x, y, 400, 22);
            return radio;
        }

        TextBox Field(string value, int x, int y, int w)
        {
            TextBox box = new TextBox();
            box.Text = value;
            box.Font = Theme.Body;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SetBounds(x, y, w, 26);
            return box;
        }

        void Save()
        {
            _config.Backend = _useAppsScript.Checked ? Config.BackendAppsScript : Config.BackendGoogle;
            _config.AppsScriptEndpoint = _endpoint.Text.Trim();
            _config.AppsScriptSecret = _secret.Text.Trim();
            _config.MaxMB = (int)_maxMB.Value;
            Store.SaveConfig(_config);
        }
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace OpenInSheets
{
    /// <summary>
    /// The whole setup experience: connect an account, claim the .csv association.
    /// Written for someone who has never opened a terminal.
    /// </summary>
    class SetupForm : Form
    {
        const int Width_ = 540;
        const int Gutter = 28;
        const int CardWidth = 484;
        const int Card2Top = 256;
        const int Card2Short = 128;
        const int Card2Tall = 244;

        Config _config;

        Dot _accountDot;
        Label _accountStatus;
        FlatButton _signIn;
        FlatButton _signOut;

        Card _fileCard;
        Dot _fileDot;
        Label _fileStatus;
        FlatButton _register;
        Callout _callout;
        FlatButton _openDefaults;

        Label[] _footer;

        public SetupForm()
        {
            _config = Store.LoadConfig();

            Text = Ui.Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Page;
            Font = Theme.Body;
            ClientSize = new Size(Width_, 446);

            Controls.Add(new Mark { Location = new Point(Gutter, 26) });
            Controls.Add(Theme.Text("Open in Sheets", Theme.Title, Theme.Ink, 80, 24, 430, 32));
            Controls.Add(Theme.Text("Double-click a CSV file and it opens as a Google Sheet.",
                                    Theme.Small, Theme.Muted, 80, 60, 430, 20));

            Controls.Add(BuildAccountCard());
            Controls.Add(BuildFileCard());
            BuildFooter();

            UpdateState();
        }

        Card BuildAccountCard()
        {
            Card card = new Card();
            card.SetBounds(Gutter, 100, CardWidth, 140);

            _accountDot = new Dot();
            _accountDot.Location = new Point(22, 26);

            _accountStatus = Theme.Text("", Theme.Small, Theme.Muted, 42, 44, 420, 36);

            _signIn = FlatButton.Primary("Sign in with Google");
            _signIn.SetBounds(42, 90, 180, 36);
            _signIn.Click += delegate { StartSignIn(); };

            _signOut = FlatButton.Secondary("Sign out");
            _signOut.SetBounds(232, 90, 110, 36);
            _signOut.Click += delegate { SignOut(); };

            card.Controls.Add(_accountDot);
            card.Controls.Add(Theme.Text("Google account", Theme.Heading, Theme.Ink, 42, 18, 300, 22));
            card.Controls.Add(_accountStatus);
            card.Controls.Add(_signIn);
            card.Controls.Add(_signOut);
            return card;
        }

        Card BuildFileCard()
        {
            _fileCard = new Card();
            _fileCard.SetBounds(Gutter, Card2Top, CardWidth, Card2Short);

            _fileDot = new Dot();
            _fileDot.Location = new Point(22, 26);

            _fileStatus = Theme.Text("", Theme.Small, Theme.Muted, 42, 44, 420, 20);

            _register = FlatButton.Primary("Set up double-click for .csv");
            _register.SetBounds(42, 74, 220, 36);
            _register.Click += delegate { Register(); };

            _callout = new Callout();
            _callout.SetBounds(42, 122, 418, 62);

            _openDefaults = FlatButton.Secondary("Open Windows default apps");
            _openDefaults.SetBounds(42, 194, 220, 34);
            _openDefaults.Click += delegate { OpenDefaultAppsSettings(); };

            _fileCard.Controls.Add(_fileDot);
            _fileCard.Controls.Add(Theme.Text("CSV files", Theme.Heading, Theme.Ink, 42, 18, 300, 22));
            _fileCard.Controls.Add(_fileStatus);
            _fileCard.Controls.Add(_register);
            _fileCard.Controls.Add(_callout);
            _fileCard.Controls.Add(_openDefaults);
            return _fileCard;
        }

        void BuildFooter()
        {
            TextLink advanced = new TextLink("Advanced settings", Theme.Muted);
            advanced.Click += delegate
            {
                using (AdvancedForm dialog = new AdvancedForm(_config))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _config = Store.LoadConfig();
                        UpdateState();
                    }
                }
            };

            TextLink logs = new TextLink("Open log folder", Theme.Muted);
            logs.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + Store.DataDir + "\""); }
                catch (Exception ex) { Ui.Error(ex.Message); }
            };

            TextLink remove = new TextLink("Remove from this PC", Theme.Danger);
            remove.Click += delegate { RemoveEverything(); };

            _footer = new Label[] { advanced, logs, remove };
            foreach (Label link in _footer) Controls.Add(link);
        }

        /// <summary>Re-reads live state, repaints every status line, and resizes to fit.</summary>
        void UpdateState()
        {
            if (_config.UsesAppsScript)
            {
                bool ready = !string.IsNullOrEmpty(_config.AppsScriptEndpoint);
                _accountDot.Color = ready ? Theme.Accent : Theme.Idle;
                _accountStatus.Text = ready
                    ? "Using your own Apps Script. Change it under Advanced settings."
                    : "Using your own Apps Script, but no address is set yet.\r\nAdd one under Advanced settings.";
                _signIn.Visible = false;
                _signOut.Visible = false;
            }
            else if (OAuth.IsSignedIn)
            {
                _accountDot.Color = Theme.Accent;
                _accountStatus.Text = string.IsNullOrEmpty(_config.AccountEmail)
                    ? "Connected to your Google account."
                    : "Connected as " + _config.AccountEmail;
                _signIn.Visible = true;
                _signIn.Text = "Reconnect";
                _signIn.Enabled = true;
                _signIn.Style(false);
                _signOut.Visible = true;
            }
            else
            {
                _accountDot.Color = Theme.Idle;
                _accountStatus.Text = "Not connected yet. Open in Sheets only ever sees the files it\r\n" +
                                      "creates for you, never the rest of your Drive.";
                _signIn.Visible = true;
                _signIn.Text = "Sign in with Google";
                _signIn.Enabled = true;
                _signIn.Style(true);
                _signOut.Visible = false;
            }

            bool registered = Association.IsRegistered;
            string blocking = registered ? Association.NeedsManualDefault() : null;
            bool needsManual = registered && blocking != null;

            _fileDot.Color = !registered ? Theme.Idle : (needsManual ? Theme.WarnInk : Theme.Accent);
            _fileStatus.Text = registered
                ? "Right-click any CSV and you'll see \"Open in Google Sheets\"."
                : "Not set up yet.";
            _register.Text = registered ? "Set up again" : "Set up double-click for .csv";
            // When Windows is still holding the default, the outstanding action is the
            // settings trip, not running setup again - so the emphasis belongs there.
            _register.Style(!registered);
            _openDefaults.Style(true);

            _callout.Visible = needsManual;
            _openDefaults.Visible = needsManual;
            if (needsManual)
            {
                _callout.Message = "Windows still opens CSV files with another app, and only you can " +
                                   "change that. In Default apps, search for .csv and pick Open in Sheets.";
            }

            _fileCard.Height = needsManual ? Card2Tall : Card2Short;

            int footerTop = _fileCard.Bottom + 22;
            int x = Gutter;
            foreach (Label link in _footer)
            {
                link.Location = new Point(x, footerTop);
                x += link.PreferredWidth + 26;
            }
            ClientSize = new Size(Width_, footerTop + 40);
        }

        // --- actions ----------------------------------------------------------

        void StartSignIn()
        {
            _signIn.Enabled = false;
            _signIn.Text = "Waiting for your browser...";

            Thread worker = new Thread(delegate()
            {
                string email = null;
                Exception failure = null;
                try { email = OAuth.SignIn(); }
                catch (Exception ex) { failure = ex; }

                string capturedEmail = email;
                Exception capturedFailure = failure;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (capturedFailure == null)
                        {
                            _config.AccountEmail = capturedEmail == null ? "" : capturedEmail;
                            _config.Backend = Config.BackendGoogle;
                            Store.SaveConfig(_config);
                        }
                        else
                        {
                            Ui.Error(capturedFailure.Message);
                        }
                        UpdateState();
                    });
                }
                catch { /* window closed while we waited */ }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        void SignOut()
        {
            if (!Ui.Confirm("Disconnect your Google account from Open in Sheets?\r\n\r\n" +
                            "Spreadsheets already in your Drive are left alone.")) return;
            OAuth.SignOut();
            _config.AccountEmail = "";
            Store.SaveConfig(_config);
            UpdateState();
        }

        void Register()
        {
            try
            {
                Association.Register();
                string blocking = Association.NeedsManualDefault();
                UpdateState();

                if (blocking == null)
                    Ui.Info("Done. Double-click any CSV file to open it in Google Sheets.");
                else
                    Ui.Info("Almost there.\r\n\r\n" +
                            "Windows still opens CSV files with another app, and that setting is " +
                            "protected so only you can change it.\r\n\r\n" +
                            "Click \"Open Windows default apps\", search for .csv, and choose " +
                            "Open in Sheets.\r\n\r\n" +
                            "Until then, right-click a CSV and choose \"Open in Google Sheets\".");
            }
            catch (Exception ex)
            {
                Ui.Error("Could not set up the file association.\r\n\r\n" + ex.Message);
            }
        }

        void OpenDefaultAppsSettings()
        {
            try { Process.Start("ms-settings:defaultapps"); }
            catch (Exception ex) { Ui.Error("Could not open Windows Settings.\r\n\r\n" + ex.Message); }
        }

        void RemoveEverything()
        {
            if (!Ui.Confirm("Remove Open in Sheets from this PC?\r\n\r\n" +
                            "This undoes the file association and forgets your sign-in. " +
                            "Spreadsheets already in your Drive are left alone, and you can " +
                            "delete the app file itself afterwards.")) return;
            try
            {
                Association.Unregister();
                OAuth.SignOut();
                Ui.Info("Removed. You can delete the Open in Sheets file now.");
                Close();
            }
            catch (Exception ex)
            {
                Ui.Error(ex.Message);
            }
        }
    }
}

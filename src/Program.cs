using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace OpenInSheets
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                if (args.Length == 0 || args[0] == "--setup")
                {
                    Application.Run(new SetupForm());
                    return 0;
                }
                if (args[0] == "--register") { Association.Register(); return 0; }
                if (args[0] == "--unregister") { Association.Unregister(); return 0; }

                return OpenCsv(args[0]);
            }
            catch (Exception ex)
            {
                Store.Log("ERR " + ex);
                Ui.Error(ex.Message);
                return 1;
            }
        }

        static int OpenCsv(string path)
        {
            Config config = Store.LoadConfig();

            if (!config.UsesAppsScript && !OAuth.IsSignedIn)
            {
                Ui.Info("Open in Sheets isn't connected to your Google account yet.\r\n\r\n" +
                        "Settings will open so you can connect it - then try your file again.");
                Application.Run(new SetupForm());
                return 1;
            }

            FileInfo file = new FileInfo(path);
            if (!file.Exists)
            {
                Ui.Error("That file no longer exists:\r\n\r\n" + path);
                return 1;
            }
            if (file.Length == 0)
            {
                Ui.Error(file.Name + " is empty, so there is nothing to open.");
                return 1;
            }
            if (file.Length > config.MaxMB * 1024L * 1024L)
            {
                Ui.Error(string.Format(
                    "{0} is {1:0.0} MB, which is over the {2} MB limit.\r\n\r\n" +
                    "Google Sheets also stops at 10 million cells, so very large files may not " +
                    "open even if the limit is raised in settings.",
                    file.Name, file.Length / 1048576.0, config.MaxMB));
                return 1;
            }

            byte[] csv = StripBom(File.ReadAllBytes(path));
            string displayName = Path.GetFileNameWithoutExtension(path);
            string knownFileId = Store.GetFileId(path);

            Upload result = null;
            Exception failure = null;

            SplashForm splash = new SplashForm("Opening " + file.Name + " in Google Sheets...");
            Thread worker = new Thread(delegate()
            {
                try
                {
                    result = config.UsesAppsScript
                        ? AppsScriptClient.Send(config, displayName, csv, knownFileId)
                        : DriveClient.Send(config, displayName, csv, knownFileId);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                try { splash.BeginInvoke(new Action(splash.Close)); }
                catch { /* already gone */ }
            });
            worker.IsBackground = true;
            splash.Shown += delegate { worker.Start(); };
            Application.Run(splash);

            if (failure is NotSignedInException)
            {
                Store.Log("ERR " + failure.Message);
                Ui.Info(failure.Message + "\r\n\r\nSettings will open so you can sign in again.");
                Application.Run(new SetupForm());
                return 1;
            }
            if (failure != null)
            {
                Store.Log("ERR " + failure);
                Ui.Error(failure.Message);
                return 1;
            }

            Store.SetFileId(path, result.FileId);
            Store.Log("ok " + path + " -> " + result.Url);
            OpenBrowser(result.Url, config);
            return 0;
        }

        /// <summary>Excel writes a UTF-8 BOM; left in place it becomes junk in the first header cell.</summary>
        static byte[] StripBom(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
            {
                byte[] trimmed = new byte[data.Length - 3];
                Array.Copy(data, 3, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            return data;
        }

        static void OpenBrowser(string url, Config config)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.BrowserPath) && File.Exists(config.BrowserPath))
                    Process.Start(config.BrowserPath, "\"" + url + "\"");
                else
                    Process.Start(url); // whatever the system opens https:// with
            }
            catch (Exception ex)
            {
                Store.Log("could not open a browser: " + ex.Message);
                Ui.Error("Your spreadsheet is ready, but a browser could not be opened.\r\n\r\n" + url);
            }
        }
    }
}

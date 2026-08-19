using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OpenInSheets
{
    /// <summary>
    /// Registers the app as a handler for .csv. Everything is written under HKCU, so
    /// this never needs administrator rights and never affects other user accounts.
    /// </summary>
    static class Association
    {
        const string ProgId = "OpenInSheets.csv";
        const string AppExe = "open-in-sheets.exe";
        const string Classes = @"Software\Classes\";

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        const int SHCNE_ASSOCCHANGED = 0x08000000;

        public static string ExePath
        {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        public static void Register()
        {
            string command = "\"" + ExePath + "\" \"%1\"";
            string icon = ExePath + ",0";

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + ProgId))
                k.SetValue("", "CSV (opens in Google Sheets)");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + ProgId + @"\DefaultIcon"))
                k.SetValue("", icon);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + ProgId + @"\shell\open\command"))
                k.SetValue("", command);

            // Puts a readable name in the "Open with" list.
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + @"Applications\" + AppExe))
                k.SetValue("FriendlyAppName", "Open in Sheets");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + @"Applications\" + AppExe + @"\shell\open\command"))
                k.SetValue("", command);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + @"Applications\" + AppExe + @"\SupportedTypes"))
                k.SetValue(".csv", "");

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + @".csv\OpenWithProgids"))
                k.SetValue(ProgId, "");

            // Right-click entry, so the tool works even before the default is switched.
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                Classes + @"SystemFileAssociations\.csv\shell\OpenInGoogleSheets"))
            {
                k.SetValue("", "Open in Google Sheets");
                k.SetValue("Icon", icon);
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                Classes + @"SystemFileAssociations\.csv\shell\OpenInGoogleSheets\command"))
                k.SetValue("", command);

            // Claim the default only where nothing else holds it. Where Windows has a
            // UserChoice it wins over this, and only the user can change it (see NeedsManualDefault).
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Classes + ".csv"))
            {
                object previous = k.GetValue("");
                if (previous != null && previous.ToString().Length > 0 && previous.ToString() != ProgId)
                    k.SetValue("OpenInSheets.previous", previous.ToString());
                k.SetValue("", ProgId);
            }

            Refresh();
            Store.Log("registered .csv handler at " + ExePath);
        }

        public static void Unregister()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Classes + ".csv", true))
            {
                if (k != null)
                {
                    object previous = k.GetValue("OpenInSheets.previous");
                    if (previous != null) k.SetValue("", previous.ToString());
                    else if (ProgId.Equals(k.GetValue("") as string)) k.DeleteValue("", false);
                    k.DeleteValue("OpenInSheets.previous", false);

                    using (RegistryKey progids = k.OpenSubKey("OpenWithProgids", true))
                        if (progids != null) progids.DeleteValue(ProgId, false);
                }
            }

            Delete(Classes + ProgId);
            Delete(Classes + @"Applications\" + AppExe);
            Delete(Classes + @"SystemFileAssociations\.csv\shell\OpenInGoogleSheets");

            Refresh();
            Store.Log("unregistered .csv handler");
        }

        public static bool IsRegistered
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Classes + ProgId + @"\shell\open\command"))
                {
                    if (k == null) return false;
                    string command = k.GetValue("") as string;
                    return command != null && command.IndexOf(ExePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        /// <summary>
        /// Windows hash-protects the current default app for an extension, so no installer
        /// can take it over. Returns the ProgId holding it, or null when we already have it.
        /// </summary>
        public static string NeedsManualDefault()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.csv\UserChoice"))
            {
                if (k == null) return null;
                string current = k.GetValue("ProgId") as string;
                return string.IsNullOrEmpty(current) || current == ProgId ? null : current;
            }
        }

        static void Delete(string path)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
            catch (Exception ex) { Store.Log("could not remove " + path + ": " + ex.Message); }
        }

        static void Refresh()
        {
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }
    }
}

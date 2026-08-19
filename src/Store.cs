using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenInSheets
{
    /// <summary>
    /// Everything that lives on disk: settings, the path-to-spreadsheet index, the
    /// refresh token, and the log. All under %LOCALAPPDATA% so the .exe itself can
    /// sit anywhere - Downloads, a USB stick, wherever the user dropped it.
    /// </summary>
    static class Store
    {
        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "open-in-sheets");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static string ConfigPath { get { return Path.Combine(DataDir, "settings.json"); } }
        static string IndexPath { get { return Path.Combine(DataDir, "index.json"); } }
        static string CredsPath { get { return Path.Combine(DataDir, "creds.dat"); } }
        public static string LogPath { get { return Path.Combine(DataDir, "open-in-sheets.log"); } }

        // --- settings ---------------------------------------------------------

        public static Config LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath)) return Config.FromJson(ReadText(ConfigPath));
            }
            catch (Exception ex)
            {
                Log("config unreadable, using defaults: " + ex.Message);
            }
            return new Config();
        }

        public static void SaveConfig(Config config)
        {
            WriteText(ConfigPath, config.ToJson());
        }

        // --- path -> spreadsheet id -------------------------------------------

        public static string GetFileId(string csvPath)
        {
            Dictionary<string, object> index = LoadIndex();
            return Json.Str(index, Key(csvPath));
        }

        public static void SetFileId(string csvPath, string fileId)
        {
            try
            {
                Dictionary<string, object> index = LoadIndex();
                index[Key(csvPath)] = fileId;
                WriteText(IndexPath, Json.Serialize(index));
            }
            catch (Exception ex)
            {
                Log("could not save index: " + ex.Message); // costs at most one duplicate sheet
            }
        }

        static Dictionary<string, object> LoadIndex()
        {
            try
            {
                if (File.Exists(IndexPath)) return Json.Parse(ReadText(IndexPath));
            }
            catch { }
            return new Dictionary<string, object>();
        }

        static string Key(string csvPath)
        {
            return Path.GetFullPath(csvPath).ToLowerInvariant();
        }

        // --- refresh token ----------------------------------------------------

        // DPAPI, CurrentUser scope: the file is unreadable by other Windows accounts
        // and does not survive being copied to another machine.
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("open-in-sheets/v1");

        public static void SaveRefreshToken(string token)
        {
            byte[] cipher = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CredsPath, cipher);
        }

        public static string LoadRefreshToken()
        {
            try
            {
                if (!File.Exists(CredsPath)) return null;
                byte[] plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(CredsPath), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                Log("stored credentials unreadable: " + ex.Message);
                return null;
            }
        }

        public static void ClearRefreshToken()
        {
            try { if (File.Exists(CredsPath)) File.Delete(CredsPath); }
            catch { }
        }

        // --- log --------------------------------------------------------------

        public static void Log(string line)
        {
            try
            {
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
                    File.Delete(LogPath);
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + Environment.NewLine);
            }
            catch { }
        }

        // --- text i/o ---------------------------------------------------------

        /// <summary>Reads UTF-8, UTF-16 LE/BE, or BOM-prefixed text - users edit these files.</summary>
        public static string ReadText(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length >= 3 && b[0] == 0xef && b[1] == 0xbb && b[2] == 0xbf)
                return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xff && b[1] == 0xfe)
                return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xfe && b[1] == 0xff)
                return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            return Encoding.UTF8.GetString(b);
        }

        /// <summary>UTF-8 with no BOM. A BOM here breaks anything that parses these as JSON.</summary>
        public static void WriteText(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}

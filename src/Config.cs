using System;
using System.Collections.Generic;

namespace OpenInSheets
{
    class Config
    {
        public const string BackendGoogle = "google";
        public const string BackendAppsScript = "appsscript";

        /// <summary>"google" = sign in with Google (default). "appsscript" = the user's own web app.</summary>
        public string Backend = BackendGoogle;

        public string AppsScriptEndpoint = "";
        public string AppsScriptSecret = "";

        public string FolderName = "CSV Quick Open";
        public string FolderId = "";

        /// <summary>Display only, so the settings window can say which account is connected.</summary>
        public string AccountEmail = "";

        /// <summary>Empty means "whatever the system opens https:// links with".</summary>
        public string BrowserPath = "";

        public int MaxMB = 10;

        public bool UsesAppsScript
        {
            get { return string.Equals(Backend, BackendAppsScript, StringComparison.OrdinalIgnoreCase); }
        }

        public static Config FromJson(string json)
        {
            Dictionary<string, object> d = Json.Parse(json);
            Config c = new Config();
            c.Backend = Or(Json.Str(d, "backend"), c.Backend);
            c.AppsScriptEndpoint = Or(Json.Str(d, "appsScriptEndpoint"), "");
            c.AppsScriptSecret = Or(Json.Str(d, "appsScriptSecret"), "");
            c.FolderName = Or(Json.Str(d, "folderName"), c.FolderName);
            c.FolderId = Or(Json.Str(d, "folderId"), "");
            c.AccountEmail = Or(Json.Str(d, "accountEmail"), "");
            c.BrowserPath = Or(Json.Str(d, "browserPath"), "");

            int max;
            if (int.TryParse(Json.Str(d, "maxMB"), out max) && max > 0) c.MaxMB = max;
            return c;
        }

        public string ToJson()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["backend"] = Backend;
            d["appsScriptEndpoint"] = AppsScriptEndpoint;
            d["appsScriptSecret"] = AppsScriptSecret;
            d["folderName"] = FolderName;
            d["folderId"] = FolderId;
            d["accountEmail"] = AccountEmail;
            d["browserPath"] = BrowserPath;
            d["maxMB"] = MaxMB;
            return Json.Serialize(d);
        }

        static string Or(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}

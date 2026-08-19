using System;
using System.Collections.Generic;
using System.Text;

namespace OpenInSheets
{
    /// <summary>
    /// The alternate backend: the user's own Apps Script web app, deployed under their
    /// own Google account. Nothing about this build - not even its OAuth client - is
    /// involved. Slower to set up, but it answers "why should I trust your app?" with
    /// "you don't have to".
    /// </summary>
    static class AppsScriptClient
    {
        public static Upload Send(Config config, string displayName, byte[] csv, string knownFileId)
        {
            if (string.IsNullOrEmpty(config.AppsScriptEndpoint))
                throw new Exception("No Apps Script address is set. Open the app's settings to add one.");
            if (string.IsNullOrEmpty(config.AppsScriptSecret) || config.AppsScriptSecret.Length < 24)
                throw new Exception("The Apps Script secret is missing or too short. Open the app's settings to fix it.");

            // Built by hand rather than serialized: the base64 payload can run to tens
            // of megabytes and there is no reason to copy it around twice.
            StringBuilder body = new StringBuilder();
            body.Append("{\"secret\":\"").Append(Json.Escape(config.AppsScriptSecret)).Append('"');
            body.Append(",\"name\":\"").Append(Json.Escape(displayName)).Append('"');
            if (!string.IsNullOrEmpty(knownFileId))
                body.Append(",\"fileId\":\"").Append(Json.Escape(knownFileId)).Append('"');
            body.Append(",\"data\":\"").Append(Convert.ToBase64String(csv)).Append("\"}");

            string raw = Http.PostJson(config.AppsScriptEndpoint, body.ToString(), null);

            Dictionary<string, object> reply;
            try
            {
                reply = Json.Parse(raw);
            }
            catch
            {
                throw new Exception(
                    "The Apps Script address did not reply with data.\r\n\r\n" +
                    "Check that its deployment is a Web app set to \"Execute as: Me\" and " +
                    "\"Who has access: Anyone\", and that the address ends in /exec.");
            }

            string error = Json.Str(reply, "error");
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            Upload result = new Upload();
            result.FileId = Json.Str(reply, "id");
            result.Url = Json.Str(reply, "url");
            if (string.IsNullOrEmpty(result.Url))
                throw new Exception("The Apps Script did not return a spreadsheet address.");
            return result;
        }

        /// <summary>Cheap reachability check for the settings window.</summary>
        public static bool Ping(string endpoint, out string problem)
        {
            problem = null;
            try
            {
                Dictionary<string, object> reply = Json.Parse(Http.Get(endpoint, null));
                if (!"True".Equals(Json.Str(reply, "ok"), StringComparison.OrdinalIgnoreCase))
                {
                    problem = "That address answered, but it isn't an Open in Sheets script.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                problem = ex.Message;
                return false;
            }
        }
    }
}

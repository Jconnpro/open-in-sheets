using System;
using System.Collections.Generic;

namespace OpenInSheets
{
    class Upload
    {
        public string FileId;
        public string Url;
    }

    /// <summary>
    /// Talks to the Drive REST API with the signed-in user's token.
    ///
    /// Everything here stays inside the drive.file scope: the app creates a folder and
    /// spreadsheets, and can only ever revisit files it created itself.
    /// </summary>
    static class DriveClient
    {
        const string SheetsMime = "application/vnd.google-apps.spreadsheet";
        const string FolderMime = "application/vnd.google-apps.folder";
        const string Api = "https://www.googleapis.com/drive/v3/files";
        const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";

        public static Upload Send(Config config, string displayName, byte[] csv, string knownFileId)
        {
            string token = OAuth.GetAccessToken();
            string folderId = EnsureFolder(config, token);

            string fileId = null;
            if (!string.IsNullOrEmpty(knownFileId)) fileId = TryUpdate(token, knownFileId, csv);
            if (fileId == null) fileId = Create(token, folderId, displayName, csv);

            Upload result = new Upload();
            result.FileId = fileId;
            result.Url = "https://docs.google.com/spreadsheets/d/" + fileId + "/edit";
            return result;
        }

        static string EnsureFolder(Config config, string token)
        {
            if (!string.IsNullOrEmpty(config.FolderId))
            {
                try
                {
                    Dictionary<string, object> existing = Json.Parse(Http.Get(
                        Api + "/" + config.FolderId + "?fields=id,trashed", token));
                    if (!string.Equals(Json.Str(existing, "trashed"), "True", StringComparison.OrdinalIgnoreCase))
                        return config.FolderId;
                }
                catch (HttpError ex)
                {
                    Store.Log("folder " + config.FolderId + " unusable (" + ex.Status + "), making a new one");
                }
            }

            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata["name"] = config.FolderName;
            metadata["mimeType"] = FolderMime;

            Dictionary<string, object> created = Json.Parse(
                Http.PostJson(Api + "?fields=id", Json.Serialize(metadata), token));

            config.FolderId = Json.Str(created, "id");
            Store.SaveConfig(config);
            return config.FolderId;
        }

        static string Create(string token, string folderId, string displayName, byte[] csv)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata["name"] = displayName;
            metadata["mimeType"] = SheetsMime;               // asks Drive to convert on the way in
            metadata["parents"] = new string[] { folderId };

            Dictionary<string, object> created = Json.Parse(Http.UploadMultipart(
                UploadApi + "?uploadType=multipart&fields=id",
                "POST", Json.Serialize(metadata), csv, "text/csv", token));

            string id = Json.Str(created, "id");
            if (string.IsNullOrEmpty(id)) throw new Exception("Drive did not return a spreadsheet id.");
            return id;
        }

        /// <summary>
        /// Re-imports over the spreadsheet made for this file last time, so re-opening a
        /// CSV refreshes one sheet at a stable URL instead of piling up copies.
        /// Returns null when the caller should just make a new one.
        /// </summary>
        static string TryUpdate(string token, string fileId, byte[] csv)
        {
            try
            {
                Dictionary<string, object> metadata = new Dictionary<string, object>();
                metadata["mimeType"] = SheetsMime;

                Dictionary<string, object> updated = Json.Parse(Http.UploadMultipart(
                    UploadApi + "/" + fileId + "?uploadType=multipart&fields=id,mimeType,trashed",
                    "PATCH", Json.Serialize(metadata), csv, "text/csv", token));

                if (string.Equals(Json.Str(updated, "trashed"), "True", StringComparison.OrdinalIgnoreCase))
                    return null;

                // If the re-import demoted it out of Sheets, don't hand back something broken.
                if (!SheetsMime.Equals(Json.Str(updated, "mimeType")))
                {
                    Store.Log("update left " + fileId + " as " + Json.Str(updated, "mimeType") + "; creating fresh");
                    return null;
                }
                return Json.Str(updated, "id");
            }
            catch (HttpError ex)
            {
                Store.Log("update of " + fileId + " failed (" + ex.Status + "); creating fresh");
                return null; // deleted, or no longer ours
            }
        }
    }
}

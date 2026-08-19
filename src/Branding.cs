namespace OpenInSheets
{
    /// <summary>
    /// The Google OAuth client this build signs in with.
    ///
    /// These are placeholders in source. `build.ps1` substitutes real values at compile
    /// time from `client.local.txt`, which is never committed - not because the values
    /// are confidential (Google's guidance is that an installed app's client secret is
    /// not, and anyone can read it out of the compiled binary anyway) but to keep
    /// automated secret scanners from flagging the repository and potentially rotating
    /// a credential that shipped binaries depend on.
    ///
    /// Building from a clone? Create a free Desktop client at
    /// https://console.cloud.google.com/apis/credentials and either drop it in
    /// client.local.txt or pass it to build.ps1. Sign-in stays disabled until you do,
    /// with a message saying so; Apps Script mode needs no client at all.
    /// </summary>
    static class Branding
    {
        public const string ClientId = "PASTE_CLIENT_ID_HERE.apps.googleusercontent.com";
        public const string ClientSecret = "PASTE_CLIENT_SECRET_HERE";

        /// <summary>Substring test, so substituting the constants above cannot break this check.</summary>
        public static bool IsConfigured
        {
            get { return ClientId.IndexOf("PASTE_") < 0 && ClientSecret.IndexOf("PASTE_") < 0; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OpenInSheets
{
    /// <summary>Raised when the user needs to sign in (or sign in again).</summary>
    class NotSignedInException : Exception
    {
        public NotSignedInException(string message) : base(message) { }
    }

    /// <summary>
    /// The OAuth flow Google specifies for installed desktop apps: authorization code
    /// with PKCE, redirecting to a loopback address.
    ///
    /// This listens with a raw TcpListener rather than HttpListener on purpose -
    /// HttpListener needs an admin-registered URL reservation, and this tool must work
    /// for a user with no special rights.
    /// </summary>
    static class OAuth
    {
        const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

        // drive.file is the narrow one: this app can only ever see files it created
        // itself. It cannot read anything else in the user's Drive.
        const string Scopes = "https://www.googleapis.com/auth/drive.file openid email";

        const int SignInTimeoutMs = 5 * 60 * 1000;

        static string _accessToken;
        static DateTime _accessTokenExpires = DateTime.MinValue;

        public static bool IsSignedIn
        {
            get { return Store.LoadRefreshToken() != null; }
        }

        /// <summary>Runs the consent flow. Returns the account's email address.</summary>
        public static string SignIn()
        {
            if (!Branding.IsConfigured)
                throw new Exception(
                    "This build has no Google client configured, so signing in is not possible.\r\n\r\n" +
                    "Either use a release build from the project's Releases page, or add your own " +
                    "OAuth client in src/Branding.cs and rebuild.");

            string verifier = RandomUrlSafe(32);
            string challenge = Base64Url(Sha256(Encoding.ASCII.GetBytes(verifier)));
            string state = RandomUrlSafe(16);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                string redirectUri = "http://127.0.0.1:" + port;

                string authUrl = AuthEndpoint
                    + "?client_id=" + Uri.EscapeDataString(Branding.ClientId)
                    + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                    + "&response_type=code"
                    + "&scope=" + Uri.EscapeDataString(Scopes)
                    + "&code_challenge=" + challenge
                    + "&code_challenge_method=S256"
                    + "&state=" + state
                    + "&access_type=offline"
                    + "&prompt=consent";

                try { Process.Start(authUrl); }
                catch (Exception ex)
                {
                    throw new Exception("Could not open your browser for sign-in: " + ex.Message);
                }

                string code = WaitForCode(listener, state);

                Dictionary<string, string> form = new Dictionary<string, string>();
                form["client_id"] = Branding.ClientId;
                form["client_secret"] = Branding.ClientSecret;
                form["code"] = code;
                form["code_verifier"] = verifier;
                form["grant_type"] = "authorization_code";
                form["redirect_uri"] = redirectUri;

                Dictionary<string, object> token = Json.Parse(Http.PostForm(TokenEndpoint, form));

                string refresh = Json.Str(token, "refresh_token");
                if (string.IsNullOrEmpty(refresh))
                    throw new Exception("Google did not return a refresh token. Try signing in again.");

                Store.SaveRefreshToken(refresh);
                _accessToken = Json.Str(token, "access_token");
                _accessTokenExpires = DateTime.UtcNow.AddSeconds(Seconds(token, "expires_in") - 60);

                string email = EmailFromIdToken(Json.Str(token, "id_token"));
                Store.Log("signed in" + (email == null ? "" : " as " + email));
                return email;
            }
            finally
            {
                try { listener.Stop(); } catch { }
            }
        }

        public static void SignOut()
        {
            string refresh = Store.LoadRefreshToken();
            if (!string.IsNullOrEmpty(refresh))
            {
                try
                {
                    Dictionary<string, string> form = new Dictionary<string, string>();
                    form["token"] = refresh;
                    Http.PostForm(RevokeEndpoint, form);
                }
                catch (Exception ex)
                {
                    Store.Log("revoke failed (clearing locally anyway): " + ex.Message);
                }
            }
            Store.ClearRefreshToken();
            _accessToken = null;
            _accessTokenExpires = DateTime.MinValue;
        }

        public static string GetAccessToken()
        {
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpires) return _accessToken;

            string refresh = Store.LoadRefreshToken();
            if (string.IsNullOrEmpty(refresh))
                throw new NotSignedInException("You are not signed in to Google yet.");

            Dictionary<string, string> form = new Dictionary<string, string>();
            form["client_id"] = Branding.ClientId;
            form["client_secret"] = Branding.ClientSecret;
            form["refresh_token"] = refresh;
            form["grant_type"] = "refresh_token";

            Dictionary<string, object> token;
            try
            {
                token = Json.Parse(Http.PostForm(TokenEndpoint, form));
            }
            catch (HttpError ex)
            {
                // The user revoked access, changed password, or the grant simply expired.
                if (ex.Body != null && ex.Body.IndexOf("invalid_grant") >= 0)
                {
                    Store.ClearRefreshToken();
                    throw new NotSignedInException(
                        "Your Google sign-in expired or was revoked. Please sign in again.");
                }
                throw;
            }

            _accessToken = Json.Str(token, "access_token");
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(Seconds(token, "expires_in") - 60);
            if (string.IsNullOrEmpty(_accessToken))
                throw new NotSignedInException("Google did not return an access token. Please sign in again.");
            return _accessToken;
        }

        // --- loopback listener ------------------------------------------------

        static string WaitForCode(TcpListener listener, string expectedState)
        {
            IAsyncResult pending = listener.BeginAcceptTcpClient(null, null);
            if (!pending.AsyncWaitHandle.WaitOne(SignInTimeoutMs))
                throw new Exception("Timed out waiting for sign-in to finish in the browser.");

            using (TcpClient client = listener.EndAcceptTcpClient(pending))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[8192];
                int read = stream.Read(buffer, 0, buffer.Length);
                string requestLine = Encoding.UTF8.GetString(buffer, 0, read).Split('\n')[0];

                Dictionary<string, string> query = ParseQuery(requestLine);
                string error = Get(query, "error");
                string code = Get(query, "code");
                string state = Get(query, "state");

                bool ok = error == null && code != null && state == expectedState;
                Respond(stream, ok);

                if (error != null)
                    throw new Exception(error == "access_denied"
                        ? "Sign-in was cancelled."
                        : "Google reported an error during sign-in: " + error);
                if (code == null) throw new Exception("Google did not send an authorization code back.");
                if (state != expectedState) throw new Exception("Sign-in state did not match. Please try again.");

                return code;
            }
        }

        static void Respond(Stream stream, bool ok)
        {
            string title = ok ? "You're all set" : "Sign-in failed";
            string detail = ok
                ? "Open in Sheets is connected to your Google account. You can close this tab."
                : "Nothing was connected. Close this tab and try again from the app.";

            string html =
                "<!doctype html><meta charset=utf-8><title>" + title + "</title>" +
                "<div style=\"font:16px/1.6 system-ui,sans-serif;max-width:32em;margin:20vh auto;padding:0 1.5em\">" +
                "<h1 style=\"font-size:1.4em;margin:0 0 .4em\">" + title + "</h1>" +
                "<p style=\"margin:0;color:#444\">" + detail + "</p></div>";

            byte[] body = Encoding.UTF8.GetBytes(html);
            byte[] head = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
                "Content-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");

            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        static Dictionary<string, string> ParseQuery(string requestLine)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            int start = requestLine.IndexOf('?');
            if (start < 0) return result;

            int end = requestLine.IndexOf(" HTTP", start);
            string query = end < 0 ? requestLine.Substring(start + 1) : requestLine.Substring(start + 1, end - start - 1);

            foreach (string pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                if (eq < 0) result[Uri.UnescapeDataString(pair)] = "";
                else result[Uri.UnescapeDataString(pair.Substring(0, eq))] =
                        Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
            }
            return result;
        }

        static string Get(Dictionary<string, string> d, string key)
        {
            string v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        // --- helpers ----------------------------------------------------------

        static string EmailFromIdToken(string idToken)
        {
            try
            {
                if (string.IsNullOrEmpty(idToken)) return null;
                string[] parts = idToken.Split('.');
                if (parts.Length < 2) return null;

                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                Dictionary<string, object> claims =
                    Json.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                return Json.Str(claims, "email");
            }
            catch
            {
                return null; // cosmetic only - it just labels the setup window
            }
        }

        static double Seconds(Dictionary<string, object> token, string key)
        {
            double v;
            if (double.TryParse(Json.Str(token, key), out v)) return v;
            return 3600;
        }

        static byte[] Sha256(byte[] input)
        {
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(input);
        }

        static string RandomUrlSafe(int bytes)
        {
            byte[] b = new byte[bytes];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(b);
            return Base64Url(b);
        }

        static string Base64Url(byte[] b)
        {
            return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}

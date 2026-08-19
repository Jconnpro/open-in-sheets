using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace OpenInSheets
{
    /// <summary>An HTTP status the server explained in its body.</summary>
    class HttpError : Exception
    {
        public readonly int Status;
        public readonly string Body;

        public HttpError(int status, string body, string message) : base(message)
        {
            Status = status;
            Body = body;
        }
    }

    static class Http
    {
        const int TimeoutMs = 120000;

        public static string Get(string url, string bearer)
        {
            HttpWebRequest req = New(url, "GET", bearer);
            return Send(req, null);
        }

        public static string PostForm(string url, Dictionary<string, string> fields)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in fields)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
            }
            HttpWebRequest req = New(url, "POST", null);
            req.ContentType = "application/x-www-form-urlencoded";
            return Send(req, Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public static string PostJson(string url, string json, string bearer)
        {
            HttpWebRequest req = New(url, "POST", bearer);
            req.ContentType = "application/json; charset=UTF-8";
            return Send(req, Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// multipart/related upload: a JSON metadata part followed by the file bytes.
        /// This is the shape Drive wants when it should convert the upload on the way in.
        /// </summary>
        public static string UploadMultipart(string url, string method, string metadataJson,
                                             byte[] media, string mediaType, string bearer)
        {
            string boundary = "oisb" + Guid.NewGuid().ToString("N");
            MemoryStream body = new MemoryStream();

            Append(body, "--" + boundary + "\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n");
            Append(body, metadataJson + "\r\n");
            Append(body, "--" + boundary + "\r\nContent-Type: " + mediaType + "\r\n\r\n");
            body.Write(media, 0, media.Length);
            Append(body, "\r\n--" + boundary + "--\r\n");

            HttpWebRequest req = New(url, method, bearer);
            req.ContentType = "multipart/related; boundary=" + boundary;
            return Send(req, body.ToArray());
        }

        static void Append(Stream s, string text)
        {
            byte[] b = Encoding.UTF8.GetBytes(text);
            s.Write(b, 0, b.Length);
        }

        static HttpWebRequest New(string url, string method, string bearer)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            req.UserAgent = "open-in-sheets";
            req.AllowAutoRedirect = true; // Apps Script /exec answers with a redirect
            if (!string.IsNullOrEmpty(bearer)) req.Headers["Authorization"] = "Bearer " + bearer;
            return req;
        }

        static string Send(HttpWebRequest req, byte[] body)
        {
            if (body != null)
            {
                req.ContentLength = body.Length;
                using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            }

            try
            {
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(res.GetResponseStream()))
                    return r.ReadToEnd();
            }
            catch (WebException ex)
            {
                HttpWebResponse res = ex.Response as HttpWebResponse;
                if (res == null)
                    throw new HttpError(0, "", "Could not reach Google. Check your internet connection.");

                string text = "";
                try
                {
                    using (StreamReader r = new StreamReader(res.GetResponseStream())) text = r.ReadToEnd();
                }
                catch { }

                throw new HttpError((int)res.StatusCode, text, Explain((int)res.StatusCode, text));
            }
        }

        /// <summary>Pull the human-readable bit out of Google's error JSON, if there is one.</summary>
        static string Explain(int status, string body)
        {
            try
            {
                Dictionary<string, object> d = Json.Parse(body);
                Dictionary<string, object> err = Json.Obj(d, "error");
                if (err != null)
                {
                    string message = Json.Str(err, "message");
                    if (!string.IsNullOrEmpty(message)) return message;
                }
                string description = Json.Str(d, "error_description");
                if (!string.IsNullOrEmpty(description)) return description;
                string simple = Json.Str(d, "error");
                if (!string.IsNullOrEmpty(simple)) return simple;
            }
            catch { }
            return "The server replied with HTTP " + status + ".";
        }
    }
}

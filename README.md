# Open in Sheets

Double-click a CSV on Windows and it opens as a Google Sheet.

No Excel, no import wizard, no "upload to Drive, find it, right-click, open with".
Double-click the file, and a second later it's a spreadsheet in your browser.

## Install

1. Download `open-in-sheets.exe` from [Releases](../../releases) and put it anywhere
   you like — Documents, Desktop, wherever.
2. Run it. Windows will warn that it doesn't recognise the app (see below).
3. Click **Sign in with Google**.
4. Click **Set up double-click for .csv**.

That's it. There's nothing to install, no runtime to download, and no admin rights
needed — it's a single 51 KB file that runs on the .NET Framework already built into
Windows 10 and 11.

### "Windows protected your PC"

You'll see this the first time. It appears for any program that hasn't been signed
with a paid certificate, regardless of what the program does. Click **More info**,
then **Run anyway**.

If you'd rather not take that on faith, [build it yourself](#build-it-yourself) — it's
one command and needs nothing installed.

### One step Windows won't let the app do

If your CSVs currently open in Excel, Windows keeps that setting locked in a way no
installer can change — genuinely, by design. The app will tell you when this applies
and give you a button straight to the right settings page. Search for `.csv` there and
pick **Open in Sheets**.

Until you do, right-click any CSV and choose **Open in Google Sheets**. On Windows 11
that's under *Show more options*.

## What it can see

Open in Sheets asks for one Google permission: `drive.file`. That is the narrowest
Drive scope that exists. It means the app can only ever see **files it created itself**.

- It cannot read, list, or open anything else in your Drive.
- It cannot see your existing spreadsheets, documents, or photos.
- Revoking access at [myaccount.google.com/permissions](https://myaccount.google.com/permissions)
  cuts it off instantly.

Your files go straight from your PC to your own Google Drive. They don't pass through
any server belonging to this project — there isn't one.

Everything the app stores lives in `%LOCALAPPDATA%\open-in-sheets`: your settings, a
list of which CSV maps to which spreadsheet, a log, and your Google refresh token
(encrypted with Windows DPAPI, so it's tied to your Windows account and useless if
copied to another machine).

## How it works

Google Sheets can only open files that live in Drive — there's no local-file mode. So
the app uploads the CSV, asks Drive to convert it to a Sheet on the way in, and opens
the result:

```
double-click sales.csv
  → upload to your Drive, converting to a Sheet
  → open docs.google.com/spreadsheets/d/… in your browser
```

Uploads land in a Drive folder called **CSV Quick Open**. Re-opening the same file
refreshes the same spreadsheet rather than making another copy, so the URL stays
stable and your Drive doesn't fill up with duplicates.

## Prefer not to use this app's Google client?

Fair. There's a second mode where the app talks to a Google Apps Script that *you*
deploy under your own account, so nothing here is involved at all — not even the OAuth
client.

Paste [`apps-script/Code.gs`](apps-script/Code.gs) into a new project at
[script.google.com](https://script.google.com), follow the steps in the comment at the
top, then in **Advanced settings** choose *Use my own Apps Script* and fill in the
address and secret.

It takes about three minutes and is more fiddly. It's here because "you don't have to
trust me" is a better answer than "trust me".

## Limits

- **One way.** Edits you make in Sheets stay in Sheets. Use *File → Download → CSV* to
  get them back on disk. Two-way sync would need something running in the background
  all the time; that's not what this is.
- Files over 10 MB are refused by default (raise it in Advanced settings). Google
  Sheets itself stops at 10 million cells, so very large CSVs may not open regardless.
- A UTF-8 byte order mark is stripped automatically. CSVs saved in older Windows
  encodings with accented characters may import with mangled glyphs — re-save as UTF-8
  if that bites.
- Windows only. The Sheets side would work anywhere; the double-click plumbing is
  entirely Windows-specific.

## Build it yourself

```powershell
git clone https://github.com/Jconnpro/open-in-sheets
cd open-in-sheets
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

No SDK, no Visual Studio, no NuGet, no package manager. `build.ps1` uses the C#
compiler that ships inside Windows as part of the .NET Framework, so a clone builds on
a stock Windows install with nothing added.

### Signing in from your own build

[`src/Branding.cs`](src/Branding.cs) holds placeholders, not real credentials, so a
fresh clone builds and runs but reports that sign-in isn't configured. To enable it,
create a free **Desktop** OAuth client at the
[Google Cloud console](https://console.cloud.google.com/apis/credentials), enable the
Google Drive API on the same project, add the `drive.file` scope to the consent screen,
then either:

```powershell
.\build.ps1 -ClientId "...apps.googleusercontent.com" -ClientSecret "GOCSPX-..."
```

or drop a `client.local.txt` next to `build.ps1` (it's gitignored):

```
client_id=...apps.googleusercontent.com
client_secret=GOCSPX-...
```

`build.ps1` substitutes them at compile time from a staging copy, so real credentials
never touch your working tree.

To be clear about why: a desktop app's client secret **isn't** confidential — Google's
own guidance says so, and anyone can read it straight out of any compiled binary,
including the official releases. Keeping it out of the repo isn't hiding it. It's to
stop automated secret scanners flagging the project and possibly rotating a credential
that already-shipped binaries depend on.

Don't want to bother? **Apps Script mode needs no OAuth client at all** — see above.

### Layout

| Path | What it is |
| --- | --- |
| `src/Program.cs` | entry point and the open-a-CSV path |
| `src/SetupForm.cs` | the setup window |
| `src/AdvancedForm.cs` | backend choice and Apps Script fields |
| `src/Theme.cs` | palette, type scale, and the custom-drawn controls |
| `src/Ui.cs` | dialogs and the upload progress card |
| `src/OAuth.cs` | sign-in: authorization code + PKCE over a loopback socket |
| `src/DriveClient.cs` | upload and convert via the Drive REST API |
| `src/AppsScriptClient.cs` | the self-hosted alternative |
| `src/Http.cs` | request plumbing and error extraction |
| `src/Association.cs` | the `.csv` file association (HKCU only) |
| `src/Store.cs` | settings, index, token storage, log |
| `src/Config.cs`, `src/Json.cs` | settings model and JSON helpers |
| `src/Branding.cs` | the OAuth client this build signs in with |
| `apps-script/Code.gs` | the optional self-hosted backend |

## Troubleshooting

Every run appends to `%LOCALAPPDATA%\open-in-sheets\open-in-sheets.log`. The setup
window has an **Open log folder** link.

- **Nothing happens on double-click** — the association didn't take. Open the app and
  click *Set up double-click for .csv* again, then use the right-click entry to check
  it works at all.
- **"Your Google sign-in expired or was revoked"** — sign in again from the setup
  window. This happens if you revoke access or change your Google password.
- **Apps Script mode returns an HTTP error instead of data** — the deployment isn't set
  to *Anyone*, or the address isn't the `/exec` URL of the current version. Re-deploying
  creates a **new** address unless you edit the existing deployment.

## Status

Early. Sign-in, upload, conversion, and the file association all work and have been
used on a real machine. What hasn't happened yet:

- **No signed binary.** Every download will trip SmartScreen until a code-signing
  certificate is in place.
- **No release build published yet.** Build from source in the meantime.
- **Tested on one machine**, at 100% display scaling. The window layout uses fixed
  pixel positions and the process is DPI-unaware, so Windows bitmap-scales it — correct
  proportions on a high-DPI screen, but slightly soft. Bug reports welcome.

## License

MIT — see [LICENSE](LICENSE).

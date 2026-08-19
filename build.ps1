#Requires -Version 5.1
<#
  Builds open-in-sheets.exe.

  No SDK, no Visual Studio, no NuGet: this compiles with the C# compiler that ships
  inside Windows as part of .NET Framework, which is present on every Windows 10/11
  machine. Clone the repo and run this.

  OAuth credentials are substituted at compile time rather than stored in source.
  Supply them either way:

    build.ps1 -ClientId "...apps.googleusercontent.com" -ClientSecret "GOCSPX-..."

  or put them in client.local.txt next to this script (which .gitignore excludes):

    client_id=...apps.googleusercontent.com
    client_secret=GOCSPX-...

  Without them the app still builds and runs; sign-in reports that the build has no
  client configured, and Apps Script mode works regardless.
#>
[CmdletBinding()]
param(
  [switch]$Run,
  [string]$ClientId,
  [string]$ClientSecret
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $root 'open-in-sheets.exe'
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) {
  $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
  throw 'Could not find the .NET Framework C# compiler. Install .NET Framework 4.x and try again.'
}

# --- credentials --------------------------------------------------------------
$credFile = Join-Path $root 'client.local.txt'
if ((-not $ClientId -or -not $ClientSecret) -and (Test-Path $credFile)) {
  foreach ($rawLine in [IO.File]::ReadAllLines($credFile)) {
    $line = $rawLine.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
    $split = $line.IndexOf('=')
    if ($split -lt 1) { continue }
    $key = $line.Substring(0, $split).Trim()
    $value = $line.Substring($split + 1).Trim()
    if ($key -eq 'client_id' -and -not $ClientId) { $ClientId = $value }
    if ($key -eq 'client_secret' -and -not $ClientSecret) { $ClientSecret = $value }
  }
}

# Compile from a staging copy when substituting, so the working tree is never
# left holding real credentials that could be committed by accident.
$sourceDir = Join-Path $root 'src'
$staging = $null

if ($ClientId -and $ClientSecret) {
  $staging = Join-Path ([IO.Path]::GetTempPath()) ('ois-build-' + [Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Force $staging | Out-Null
  Copy-Item (Join-Path $sourceDir '*.cs') $staging

  $branding = Join-Path $staging 'Branding.cs'
  $text = [IO.File]::ReadAllText($branding)
  $text = $text.Replace('PASTE_CLIENT_ID_HERE.apps.googleusercontent.com', $ClientId)
  $text = $text.Replace('PASTE_CLIENT_SECRET_HERE', $ClientSecret)
  [IO.File]::WriteAllText($branding, $text, (New-Object Text.UTF8Encoding $false))

  $sourceDir = $staging
  Write-Host 'Building with OAuth credentials.'
} else {
  Write-Host 'Building without OAuth credentials - sign-in will be disabled in this build.' -ForegroundColor Yellow
}

# --- compile ------------------------------------------------------------------
try {
  $cscArgs = @(
    '/target:winexe'
    "/out:$out"
    '/platform:anycpu'
    '/nologo'
    '/optimize+'
    '/warn:4'
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    '/r:System.Security.dll'
    '/r:System.Web.Extensions.dll'
    (Join-Path $sourceDir '*.cs')
  )

  Write-Host "Compiling with $csc"
  & $csc $cscArgs
  if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
}
finally {
  if ($staging -and (Test-Path $staging)) {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
  }
}

Write-Host ("Built {0} ({1:N0} bytes)" -f $out, (Get-Item $out).Length) -ForegroundColor Green

if ($Run) { & $out }

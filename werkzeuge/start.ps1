# Startet den HLKS-CAD-Arbeitsplatz (lokaler Server + Oberflaeche).
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\start.ps1 [-Port 5130] [-Projekt <e_KICAD-Ordner>] [-Akteur CS] [-Geraet PC] [-Bauen] [-Browser] [-Fenster]
#   -Browser  oeffnet die Oberflaeche als eigenes App-Fenster (Edge, sonst Chrome) ohne Adressleiste im Vollbild,
#             sobald der Server antwortet. F11 wechselt in ein normales Fenster.
#   -Fenster  statt Vollbild maximiert starten.
# Eigenes Browser-Profil unter %LOCALAPPDATA%\HLKS_CAD\fenster: so gelten die Startoptionen auch bei bereits offenem
# Browser, und das Fenster erscheint als eigene App in der Taskleiste.
# Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Port = 5130,
  [string]$Projekt = '',
  [string]$Akteur = '',
  [string]$Geraet = '',
  [switch]$Bauen,
  [switch]$OhneBau,   # nie bauen (Desktop-Start, solange am Code gearbeitet wird: letzter Build)
  [switch]$Browser,
  [switch]$Fenster
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }   # Meldungen mit Umlauten
$root = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$basis = if ($env:HLKS_BUILD) { $env:HLKS_BUILD } else { "$env:USERPROFILE\HLKS_CAD_build" }
$dll = "$basis\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"

# Neu bauen, wenn der Build fehlt oder eine C#-Quelle bzw. ein Projekt neuer ist als er (sonst laeuft der Server
# unbemerkt mit altem Stand; Befund 17.10.2026). Oberflaeche und Kataloge liest der Server ohnehin aus den Quellen.
# Seit 19.10.2026 (Christian: "aktuellen Stand starten") auch der MCP-Server der KI-Auftraege; Regeln in kicad_stand.ps1.
. "$PSScriptRoot\kicad_stand.ps1"
$stand = Kicad-Stand $root $basis
if ($OhneBau -and (Test-Path $dll)) { $stand['Hlks.Web'].Veraltet = $false; $stand['Hlks.Mcp'].Veraltet = $false }
if ($Bauen -or $stand['Hlks.Web'].Veraltet) {
  & $dotnet build "$root\src\Hlks.Web\Hlks.Web.csproj" -nologo -v q
  if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen' }
}
if ($Bauen -or $stand['Hlks.Mcp'].Veraltet) {
  & $dotnet build "$root\src\Hlks.Mcp\Hlks.Mcp.csproj" -nologo -v q
  if ($LASTEXITCODE -ne 0) { Write-Warning 'MCP-Server liess sich nicht bauen - KI-Auftraege laufen mit dem letzten Build.' }
}

$a = @($dll, '--port', $Port)
if ($Projekt) { $a += @('--projekt', $Projekt) }
if ($Akteur) { $a += @('--akteur', $Akteur) }
if ($Geraet) { $a += @('--geraet', $Geraet) }

if (-not $Browser) { & $dotnet @a; exit $LASTEXITCODE }

function Finde-Browser {
  $kandidaten = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe")
  foreach ($k in $kandidaten) { if ($k -and (Test-Path $k)) { return $k } }
  return $null
}

# Start-Process setzt die Argumente in PowerShell 5.1 ohne Anfuehrungszeichen zusammen: Pfade mit Leerzeichen selbst quoten
$zeile = ($a | ForEach-Object { $s = [string]$_; if ($s -match '[\s"]') { '"' + $s.Replace('"', '\"') + '"' } else { $s } }) -join ' '
$server = Start-Process -FilePath $dotnet -ArgumentList $zeile -NoNewWindow -PassThru
$url = "http://localhost:$Port"
$bereit = $false
for ($i = 0; $i -lt 60 -and -not $server.HasExited; $i++) {
  try { Invoke-WebRequest "$url/api/status" -UseBasicParsing -TimeoutSec 2 | Out-Null; $bereit = $true; break }
  catch { Start-Sleep -Milliseconds 500 }
}

if ($bereit) {
  $exe = Finde-Browser
  if ($exe) {
    $profil = "$env:LOCALAPPDATA\HLKS_CAD\fenster"
    $groesse = if ($Fenster) { '--start-maximized' } else { '--start-fullscreen' }
    Start-Process -FilePath $exe -ArgumentList @("--app=$url", $groesse, "--user-data-dir=`"$profil`"", '--no-first-run', '--no-default-browser-check')
  } else {
    Start-Process $url
  }
} elseif (-not $server.HasExited) {
  Write-Warning "Server antwortet nach 30 s nicht auf $url - Oberflaeche nicht geoeffnet."
}

$server.WaitForExit()
exit $server.ExitCode

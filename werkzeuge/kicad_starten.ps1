# Desktop-Start fuer KICAD (Christian 19.10.2026: "Desktop-Icon zum Starten"; "den aktuellen Stand starten"; nach den
# Fehlschlaegen im Fenster: Versionsverwaltung, "damit der Desktop immer den letzten geprueften Stand startet und Agenten
# getrennt arbeiten").
# Der Desktop laeuft NIE aus dem Arbeitsordner auf OneDrive (dort arbeiten Agenten), sondern aus einer eigenen Arbeitskopie
# %USERPROFILE%\HLKS_CAD_desktop auf dem Zweig "geprueft" (letzter Stand, dessen Tests gruen waren; gesetzt von
# werkzeuge\stand_pruefen.ps1). Build: %USERPROFILE%\HLKS_CAD_build\desktop. Oberflaeche, Kataloge und KI-Vorlagen kommen aus
# dieser Arbeitskopie (HLKS_WEBROOT, HLKS_KATALOG, HLKS_KI), der MCP-Server der KI-Auftraege aus diesem Build.
# Ablauf beim Klick auf "KICAD":
# - Laeuft der Desktop-Server schon mit dem neuesten geprueften Stand: nur das Fenster mit dem Startbildschirm (?start=1).
# - Gibt es einen neueren geprueften Stand: Arbeitskopie nachziehen, neu bauen, Server neu starten - im Projekt, das offen
#   war (/api/status "ort"). Arbeitet KICAD gerade (/api/status "beschaeftigt", z. B. KI-Auftrag): kein Neustart, Hinweis.
# - Laeuft kein Server: bauen falls noetig und ohne Projekt starten -> Startbildschirm.
# - Schlaegt der Build fehl: zurueck auf den vorigen Stand und dessen Build.
# - -Neu: in jedem Fall neu starten (Verknuepfung "KICAD aktualisieren").
# Gibt es noch keinen Zweig "geprueft", gilt main (mit Hinweis).
# Protokoll: %LOCALAPPDATA%\HLKS_CAD\start.log. Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Port = 5130,
  [switch]$Neu,
  [string]$Projekt = '',
  [string]$Akteur = 'CS'
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$wt = "$env:USERPROFILE\HLKS_CAD_desktop"
$bau = "$env:USERPROFILE\HLKS_CAD_build\desktop"
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$dll = "$bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$mcp = "$bau\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$zustandDatei = "$bau\desktop.json"
$url = "http://localhost:$Port"
$log = "$env:LOCALAPPDATA\HLKS_CAD\start.log"
New-Item -ItemType Directory -Force (Split-Path $log), $bau | Out-Null
function Log([string]$t) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$ico = Join-Path $PSScriptRoot 'kicad.ico'

function GitAufruf([string[]]$a) { $o = & git.exe @a 2>&1; if ($LASTEXITCODE -ne 0) { throw "git $($a -join ' '): $o" }; return ($o | Out-String).Trim() }
function Status { try { return (Invoke-WebRequest "$url/api/status" -UseBasicParsing -TimeoutSec 3).Content | ConvertFrom-Json } catch { return $null } }
function Laeuft { return $null -ne (Status) }

function Fenster([switch]$Start) {
  $ziel = if ($Start) { "$url/?start=1" } else { $url }
  $kandidaten = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe")
  $exe = $kandidaten | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
  if ($exe) {
    $profil = "$env:LOCALAPPDATA\HLKS_CAD\fenster"
    Start-Process -FilePath $exe -ArgumentList @("--app=$ziel", '--start-maximized', "--user-data-dir=`"$profil`"", '--no-first-run', '--no-default-browser-check')
  } else { Start-Process $ziel }
}

function Hinweis([string]$text) {
  # nicht blockierend: eigener Prozess (Befehl Base64, damit Zeilenumbrueche heil ankommen)
  $t = $text.Replace("'", "''")
  $cmd = "Add-Type -AssemblyName System.Windows.Forms; [void][System.Windows.Forms.MessageBox]::Show('$t', 'KICAD')"
  $enc = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($cmd))
  Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-WindowStyle', 'Hidden', '-EncodedCommand', $enc) -WindowStyle Hidden | Out-Null
}

function Laufender {
  $c = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $c) { return $null }
  $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($c.OwningProcess)"
  if (-not $p -or $p.CommandLine -notmatch 'Hlks\.Web\.dll') { return $null }
  return [pscustomobject]@{ Pid = [int]$c.OwningProcess }
}
function Beenden($srv) {
  Log "beende Server $($srv.Pid) auf $Port"
  Stop-Process -Id $srv.Pid -Force
  for ($i = 0; $i -lt 30 -and (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
}
function Zustand { if (Test-Path $zustandDatei) { return Get-Content -Raw $zustandDatei | ConvertFrom-Json } else { return $null } }
function ZustandMerken([string]$sha, [int]$serverPid) { @{ sha = $sha; pid = $serverPid; zeit = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content -Path $zustandDatei -Encoding UTF8 }
function Auschecken([string]$sha) {
  if (-not (Test-Path "$wt\.git")) {
    if (Test-Path $wt) { Remove-Item $wt -Recurse -Force }
    GitAufruf @('-C', $repo, 'worktree', 'add', '--detach', $wt, $sha) | Out-Null
  } else {
    GitAufruf @('-C', $wt, 'checkout', '-q', '--detach', $sha) | Out-Null
    GitAufruf @('-C', $wt, 'reset', '-q', '--hard', $sha) | Out-Null
    GitAufruf @('-C', $wt, 'clean', '-qfd') | Out-Null
  }
}

# ------------------------------------------------------------------ Stand bestimmen
$ohneGeprueft = $false
try { $sha = GitAufruf @('-C', $repo, 'rev-parse', '--verify', 'geprueft^{commit}') }
catch { $sha = GitAufruf @('-C', $repo, 'rev-parse', '--verify', 'main^{commit}'); $ohneGeprueft = $true }
$kurz = $sha.Substring(0, 8)
$wtSha = if (Test-Path "$wt\.git") { try { GitAufruf @('-C', $wt, 'rev-parse', 'HEAD') } catch { '' } } else { '' }
$zu = Zustand
$srv = Laufender
$st = Status

if ($srv -and $st) {
  $aktuell = $zu -and ([int]$zu.pid -eq $srv.Pid) -and ($zu.sha -eq $sha) -and ($wtSha -eq $sha)
  if (-not $Projekt -and $st.ort) { $Projekt = [string]$st.ort }
  if ($aktuell -and -not $Neu) { Log "Desktop auf geprueftem Stand $kurz - Startbildschirm"; Fenster -Start; exit 0 }
  if ($st.beschaeftigt) {
    Log "neuer Stand $kurz, Server beschaeftigt - kein Neustart"
    Fenster -Start
    Hinweis "Es gibt einen neueren geprueften Stand von KICAD, aber KICAD arbeitet gerade (z. B. ein KI-Auftrag).`r`nDas Fenster zeigt den laufenden Stand. Nach Abschluss nochmals auf das Icon klicken."
    exit 0
  }
  Log "Neustart auf geprueften Stand $kurz (laufend: $(if ($zu) { $zu.sha.Substring(0,8) } else { 'unbekannt' }))"
  Beenden $srv
} elseif ($srv) {
  Beenden $srv   # Prozess ohne Antwort: haengt - neu starten
}

# ------------------------------------------------------------------ Arbeitskopie nachziehen und starten
$vorher = $wtSha
try { if ($wtSha -ne $sha) { Auschecken $sha; Log "Arbeitskopie Desktop auf $kurz" } }
catch { Log "Auschecken fehlgeschlagen: $($_.Exception.Message)"; [System.Windows.Forms.MessageBox]::Show("KICAD: Arbeitskopie liess sich nicht nachziehen.`r`n$($_.Exception.Message)", 'KICAD') | Out-Null; exit 1 }

$env:HLKS_BUILD = $bau
$env:HLKS_WEBROOT = "$wt\src\Hlks.Web\wwwroot"
$env:HLKS_KATALOG = "$wt\kataloge"
$env:HLKS_KI = "$wt\ki"
$env:HLKS_MCP_DLL = $mcp
$arg = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$wt\werkzeuge\start.ps1`"", '-Port', $Port, '-Akteur', $Akteur)
if ($Projekt) { $arg += @('-Projekt', "`"$Projekt`"") }
Log "starte $wt\werkzeuge\start.ps1 (Port $Port, Projekt '$Projekt', Stand $kurz)"
$ps = Start-Process -FilePath 'powershell.exe' -ArgumentList $arg -WindowStyle Hidden -PassThru

$baut = ($vorher -ne $sha) -or -not (Test-Path $dll)
$hinweis = New-Object System.Windows.Forms.Form
$hinweis.Text = 'KICAD'; $hinweis.Width = 460; $hinweis.Height = 130; $hinweis.StartPosition = 'CenterScreen'; $hinweis.TopMost = $true
$hinweis.FormBorderStyle = 'FixedDialog'; $hinweis.MaximizeBox = $false; $hinweis.MinimizeBox = $false
if (Test-Path $ico) { $hinweis.Icon = New-Object System.Drawing.Icon($ico) }
$text = New-Object System.Windows.Forms.Label; $text.Dock = 'Fill'; $text.TextAlign = 'MiddleCenter'
$zeile2 = if ($baut) { "Neuer gepruefter Stand $kurz wird gebaut (einige Minuten)." } else { "Gepruefter Stand $kurz, Start ohne Neubau." }
$text.Text = "KICAD startet ...`r`n$zeile2"
$hinweis.Controls.Add($text); $hinweis.Show(); [System.Windows.Forms.Application]::DoEvents()

$bereit = $false; $uhr = [System.Diagnostics.Stopwatch]::StartNew()
while ($uhr.Elapsed.TotalMinutes -lt 10) {
  if (Laeuft) { $bereit = $true; break }
  if ($ps.HasExited) { break }
  $text.Text = "KICAD startet ... ({0:0} s)`r`n{1}" -f $uhr.Elapsed.TotalSeconds, $zeile2
  for ($j = 0; $j -lt 10; $j++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
}
$hinweis.Close()

$zurueck = $false
if (-not $bereit -and $vorher -and $vorher -ne $sha -and (Test-Path $dll)) {
  Log "Start mit $kurz fehlgeschlagen - zurueck auf $($vorher.Substring(0,8))"
  try { Auschecken $vorher } catch { Log "Zurueck fehlgeschlagen: $($_.Exception.Message)" }
  $a = @("`"$dll`"", '--port', $Port, '--akteur', $Akteur)
  if ($Projekt) { $a += @('--projekt', "`"$Projekt`"") }
  Start-Process -FilePath $dotnet -ArgumentList $a -WindowStyle Hidden | Out-Null
  for ($i = 0; $i -lt 120 -and -not (Laeuft); $i++) { Start-Sleep -Milliseconds 500 }
  $bereit = Laeuft; $zurueck = $bereit
}

if ($bereit) {
  $neu = Laufender
  $gilt = if ($zurueck) { $vorher } else { $sha }
  if ($neu) { ZustandMerken $gilt $neu.Pid }
  Log "Server bereit (Stand $($gilt.Substring(0,8)))"
  Fenster
  if ($zurueck) { Hinweis "Der neue gepruefte Stand $kurz liess sich nicht starten. KICAD laeuft mit dem vorigen Stand $($vorher.Substring(0,8)).`r`nProtokoll: $log" }
  elseif ($ohneGeprueft) { Hinweis "Es gibt noch keinen geprueften Stand (Zweig geprueft). KICAD laeuft mit dem Arbeitsstand $kurz (main)." }
  exit 0
}
Log 'Server antwortet nicht'
[System.Windows.Forms.MessageBox]::Show("KICAD startet nicht (Port $Port). Protokoll: $log", 'KICAD') | Out-Null
exit 1

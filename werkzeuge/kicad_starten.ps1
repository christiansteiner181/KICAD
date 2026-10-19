# Desktop-Start fuer KICAD (Christian 19.10.2026: "Desktop-Icon zum Starten"; "so einrichten, dass ich ueber das Desktop-Icon
# den aktuellen Stand starte").
# Ablauf beim Klick auf "KICAD":
# - Laeuft der Server schon UND ist er auf dem aktuellen Stand (Build neuer als jede C#-Quelle, Prozess nach dem Build
#   gestartet): nur das App-Fenster oeffnen (schnell).
# - Laeuft er mit einem aelteren Stand: beenden und mit dem aktuellen Stand neu starten - im selben Projekt, das er offen
#   hatte (/api/status "ort"). Arbeitet er gerade (/api/status "beschaeftigt", z. B. KI-Auftrag), wird NICHT neu gestartet:
#   Fenster mit dem laufenden Stand und Hinweis.
# - Laeuft keiner: Server unsichtbar starten (start.ps1 baut neu, wenn Quellen neuer sind als der Build). Schlaegt der Build
#   fehl (z. B. weil gerade am Code gearbeitet wird), startet der letzte lauffaehige Build und ein Hinweis nennt dessen Stand.
# Oberflaeche (wwwroot) und Kataloge liest der Server ohnehin aus den Quellen; neu gebaut wird nur fuer C#-Aenderungen.
# - -Neu: in jedem Fall neu starten (auch auf aktuellem Stand); Verknuepfung "KICAD aktualisieren".
# Projekt: beim Neustart das Projekt des laufenden Servers; sonst ohne Projekt -> Startbildschirm mit den letzten
# Projekten (wie Revit, Christian 19.10.2026; Spezifikation entwurf/startbildschirm.md). Das Fenster oeffnet mit ?start=1,
# ausser nach einem Neustart wegen neuem Stand. Protokoll: %LOCALAPPDATA%\HLKS_CAD\start.log.
# Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Port = 5130,
  [switch]$Neu,
  [string]$Projekt = '',
  [string]$Akteur = 'CS'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$dll = "$env:USERPROFILE\HLKS_CAD_build\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$url = "http://localhost:$Port"
$log = "$env:LOCALAPPDATA\HLKS_CAD\start.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
function Log([string]$t) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$ico = Join-Path $PSScriptRoot 'kicad.ico'

function Status {
  try { return (Invoke-WebRequest "$url/api/status" -UseBasicParsing -TimeoutSec 3).Content | ConvertFrom-Json } catch { return $null }
}
function Laeuft { return $null -ne (Status) }

function Fenster([switch]$Start) {
  # -Start: Startbildschirm zeigen, auch wenn ein Projekt offen ist (Christian 19.10.2026: "immer Startbildschirm";
  # nur nach einem Neustart wegen neuem Stand direkt das bisherige Projekt)
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
  # nicht blockierend: eigener Prozess (Befehl Base64, damit Zeilenumbrueche heil ankommen), das Skript endet sofort
  $t = $text.Replace("'", "''")
  $cmd = "Add-Type -AssemblyName System.Windows.Forms; [void][System.Windows.Forms.MessageBox]::Show('$t', 'KICAD')"
  $enc = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($cmd))
  Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-WindowStyle', 'Hidden', '-EncodedCommand', $enc) -WindowStyle Hidden | Out-Null
}

# Stand des Builds gegen die Quellen (Web-Server und MCP-Server der KI-Auftraege; Regeln in kicad_stand.ps1)
. "$PSScriptRoot\kicad_stand.ps1"
$stand = Kicad-Stand $root "$env:USERPROFILE\HLKS_CAD_build"
$veraltet = $stand['Hlks.Web'].Veraltet -or $stand['Hlks.Mcp'].Veraltet

# Fester Stand fuer den Desktop (Befund 19.10.2026, Christian "ich sehe viele Fehlschlaege"): die Oberflaeche kam live aus
# dem Quellordner - arbeitete ein Agent daran, zeigte KICAD halbfertigen Code. Jetzt werden Oberflaeche, Kataloge und
# KI-Vorlagen beim Uebernehmen eines neuen Stands nach %USERPROFILE%\HLKS_CAD_build\stand kopiert; der Server liest nur von
# dort (HLKS_WEBROOT, HLKS_KATALOG, HLKS_KI). Ein neuer Stand wird nur uebernommen, wenn seit 2 Minuten keine Quelle mehr
# geaendert wurde (sonst arbeitet gerade jemand am Code); bis dahin laeuft der letzte feste Stand weiter.
$standOrdner = "$env:USERPROFILE\HLKS_CAD_build\stand"
$standDatei = Join-Path $standOrdner 'stand.json'
function NeuesteAenderung {
  $neu = [datetime]::MinValue
  foreach ($o in @("$root\src", "$root\kataloge", "$root\ki")) {
    $f = Get-ChildItem $o -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules)\\' } |
      Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($f -and $f.LastWriteTimeUtc -gt $neu) { $neu = $f.LastWriteTimeUtc }
  }
  return $neu
}
function Spiegeln([string]$von, [string]$nach) {
  if (-not (Test-Path $von)) { return }
  & robocopy $von $nach /MIR /NJH /NJS /NFL /NDL /NP /R:1 /W:1 | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "Kopie $von -> $nach fehlgeschlagen ($LASTEXITCODE)" }
}
function StandErneuern {
  if (Test-Path $standOrdner) { Spiegeln $standOrdner "$standOrdner`_alt" }
  Spiegeln "$root\src\Hlks.Web\wwwroot" "$standOrdner\wwwroot"
  Spiegeln "$root\kataloge" "$standOrdner\kataloge"
  Spiegeln "$root\ki" "$standOrdner\ki"
  @{ quellen = $neueste.ToString('o'); erstellt = (Get-Date).ToUniversalTime().ToString('o'); pid = 0 } | ConvertTo-Json | Set-Content -Path $standDatei -Encoding UTF8
  Log ("fester Stand erneuert (Quellen {0:yyyy-MM-dd HH:mm})" -f $neueste.ToLocalTime())
}
function StandZurueck {
  if (Test-Path "$standOrdner`_alt") { Spiegeln "$standOrdner`_alt" $standOrdner; Log 'fester Stand zurueckgesetzt (Build fehlgeschlagen)' }
}
function StandPidMerken([int]$p) {
  if (-not (Test-Path $standDatei)) { return }
  $i = Get-Content -Raw $standDatei | ConvertFrom-Json
  @{ quellen = $i.quellen; erstellt = $i.erstellt; pid = $p } | ConvertTo-Json | Set-Content -Path $standDatei -Encoding UTF8
}
$neueste = NeuesteAenderung
$ruhig = $neueste -lt (Get-Date).ToUniversalTime().AddMinutes(-2)
$standInfo = if (Test-Path $standDatei) { Get-Content -Raw $standDatei | ConvertFrom-Json } else { $null }
$standZeit = if ($standInfo) { [datetime]::Parse($standInfo.quellen, $null, [System.Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime() } else { [datetime]::MinValue }
$neuerStand = ($null -eq $standInfo) -or $veraltet -or ($neueste -gt $standZeit.AddSeconds(1))
$uebernehmen = $neuerStand -and ($ruhig -or $null -eq $standInfo)
$inArbeit = $neuerStand -and -not $ruhig
$env:HLKS_WEBROOT = "$standOrdner\wwwroot"; $env:HLKS_KATALOG = "$standOrdner\kataloge"; $env:HLKS_KI = "$standOrdner\ki"
$hinweisArbeit = "Am Code von KICAD wird gerade gearbeitet. KICAD laeuft mit dem festen Stand vom {0:dd.MM.yyyy HH:mm}.`r`nDer neue Stand kommt beim naechsten Klick auf das Icon, sobald zwei Minuten lang nichts mehr geaendert wurde."

# Laufender KICAD-Server auf dem Port (nur Hlks.Web.dll): Prozess, Startzeit, DLL
function Laufender {
  $c = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $c) { return $null }
  $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($c.OwningProcess)"
  if (-not $p -or $p.CommandLine -notmatch 'Hlks\.Web\.dll') { return $null }
  $start = (Get-Process -Id $c.OwningProcess).StartTime.ToUniversalTime()
  $d = if ($p.CommandLine -match '"?([^"]*Hlks\.Web\.dll)"?') { $Matches[1] } else { $dll }
  return [pscustomobject]@{ Pid = $c.OwningProcess; Start = $start; Dll = $d }
}

function Beenden($srv) {
  Log "beende Server $($srv.Pid) auf $Port"
  Stop-Process -Id $srv.Pid -Force
  for ($i = 0; $i -lt 30 -and (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
}

$srv = Laufender
$st = Status
if ($srv -and $st) {
  # aktuell = der Server laeuft mit dem festen Stand (von diesem Skript gestartet) und es gibt keinen neuen zu uebernehmen
  $dllZeit = if ($stand['Hlks.Web'].Stand) { $stand['Hlks.Web'].Stand } else { [datetime]::MinValue }
  $quelle = $neueste
  $festerServer = $standInfo -and ([int]$standInfo.pid -eq [int]$srv.Pid)
  $aktuell = $festerServer -and -not $uebernehmen
  if (-not $Projekt -and $st.ort) { $Projekt = [string]$st.ort }
  if ($aktuell -and -not $Neu) {
    Log $(if ($inArbeit) { 'Server auf festem Stand, neuer Stand in Arbeit - Startbildschirm' } else { 'Server auf aktuellem Stand - Startbildschirm' })
    Fenster -Start
    if ($inArbeit) { Hinweis ($hinweisArbeit -f $standZeit.ToLocalTime()) }
    exit 0
  }
  if ($st.beschaeftigt) {
    Log 'Server veraltet, aber beschaeftigt - kein Neustart'
    Fenster -Start
    Hinweis "Es gibt einen neueren Stand von KICAD, aber KICAD arbeitet gerade (z. B. ein KI-Auftrag).`r`nDas Fenster zeigt den laufenden Stand. Nach Abschluss nochmals auf das Icon klicken."
    exit 0
  }
  Log ("Server veraltet (Build {0:yyyy-MM-dd HH:mm}, Quellen {1:yyyy-MM-dd HH:mm}, gestartet {2:yyyy-MM-dd HH:mm}) - Neustart" -f $dllZeit.ToLocalTime(), $quelle.ToLocalTime(), $srv.Start.ToLocalTime())
  Beenden $srv
} elseif ($srv) {
  Beenden $srv   # Prozess ohne Antwort: haengt - neu starten
}

# Kein Vorgabeprojekt mehr (Startbildschirm, Christian 19.10.2026): ohne laufenden Server startet KICAD ohne Projekt;
# nur ein Neustart wegen neuem Stand oeffnet das bisherige Projekt ($Projekt aus /api/status oben).

# Fester Stand: neuen nur uebernehmen, wenn Ruhe ist (sonst letzter Build und letzter fester Stand)
if ($uebernehmen) { StandErneuern }

# Server starten: ueber start.ps1 (baut bei Bedarf neu, ausser es wird gerade gearbeitet), unsichtbar; die Umgebung
# (HLKS_WEBROOT, HLKS_KATALOG, HLKS_KI auf den festen Stand) erbt der Prozess
$arg = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$root\werkzeuge\start.ps1`"", '-Port', $Port, '-Akteur', $Akteur)
if ($Projekt) { $arg += @('-Projekt', "`"$Projekt`"") }
if (-not $uebernehmen) { $arg += '-OhneBau'; $veraltet = $false }
Log "starte start.ps1 (Port $Port, Projekt '$Projekt')"
$ps = Start-Process -FilePath 'powershell.exe' -ArgumentList $arg -WindowStyle Hidden -PassThru

# Hinweisfenster, solange gebaut bzw. gestartet wird
$baut = $veraltet
$hinweis = New-Object System.Windows.Forms.Form
$hinweis.Text = 'KICAD'; $hinweis.Width = 440; $hinweis.Height = 130; $hinweis.StartPosition = 'CenterScreen'; $hinweis.TopMost = $true
$hinweis.FormBorderStyle = 'FixedDialog'; $hinweis.MaximizeBox = $false; $hinweis.MinimizeBox = $false
if (Test-Path $ico) { $hinweis.Icon = New-Object System.Drawing.Icon($ico) }
$text = New-Object System.Windows.Forms.Label; $text.Dock = 'Fill'; $text.TextAlign = 'MiddleCenter'
$zeile2 = if ($baut) { 'Neuer Stand wird gebaut (einige Minuten).' } else { 'Aktueller Stand, Start ohne Neubau.' }
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

$alterStand = $false
if (-not $bereit -and (Test-Path $dll)) {
  Log 'start.ps1 beendet ohne Server (Build fehlgeschlagen?) - starte letzten lauffaehigen Build'
  if ($uebernehmen) { StandZurueck }   # Oberflaeche passend zum alten Build
  $a = @("`"$dll`"", '--port', $Port, '--akteur', $Akteur)
  if ($Projekt) { $a += @('--projekt', "`"$Projekt`"") }
  Start-Process -FilePath $dotnet -ArgumentList $a -WindowStyle Hidden | Out-Null
  for ($i = 0; $i -lt 120 -and -not (Laeuft); $i++) { Start-Sleep -Milliseconds 500 }
  $bereit = Laeuft
  $alterStand = $bereit
}

if ($bereit) {
  $neu = Laufender; if ($neu) { StandPidMerken $neu.Pid }
  Log ($(if ($alterStand) { 'Server bereit (letzter lauffaehiger Build)' } elseif ($inArbeit) { 'Server bereit (fester Stand, neuer in Arbeit)' } else { 'Server bereit (aktueller Stand)' }))
  Fenster
  if ($inArbeit -and -not $alterStand) { Hinweis ($hinweisArbeit -f $standZeit.ToLocalTime()) }
  if ($alterStand) {
    Hinweis ("Der neueste Stand liess sich nicht bauen (vermutlich wird gerade am Code gearbeitet).`r`nKICAD laeuft mit dem letzten lauffaehigen Stand vom {0:dd.MM.yyyy HH:mm}.`r`nSpaeter nochmals auf das Icon klicken." -f (Get-Item $dll).LastWriteTime)
  }
  exit 0
}
Log 'Server antwortet nicht'
[System.Windows.Forms.MessageBox]::Show("KICAD startet nicht (Port $Port). Protokoll: $log", 'KICAD') | Out-Null
exit 1

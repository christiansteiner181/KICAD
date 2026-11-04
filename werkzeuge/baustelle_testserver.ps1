# Testserver BIM to field (Christian 04.11.2026: "wo stehen wir mit BIm to field? ich habe noch kein testserver mit icon."):
# eigener KICAD-Server mit dem Baustellen-Zugang im Bueronetz (src\Hlks.Web\BaustelleZugang.cs), damit die Baustellen-Webseite am Tablet ueber
# http://<PC>:<BaustellePort>/baustelle/ laeuft, mit Icon auf den Startbildschirm kann und Rueckmeldungen direkt an KICAD sendet.
# Ablauf beim Klick auf das Icon "KICAD Baustelle (Test)":
# - Laeuft der Testserver schon (Port $Port): nur die Uebersicht oeffnen und die Adressen zeigen; -Neu startet ihn neu.
# - Sonst: Testprojekt anlegen, falls es fehlt (fiktives Beispielprojekt "MFH Musterweg", Hlks.Mcp --beispiel), Server starten mit diesem Projekt,
#   Webseite fuer die Bauleitung (Arbeitsstand) erzeugen, falls noch keine da ist, Uebersicht im Browser oeffnen, Adressen fuer das Tablet zeigen.
# Laeuft aus dem Build des Desktops (%USERPROFILE%\HLKS_CAD_build\desktop, Zweig "geprueft"; der Desktop baut ihn beim Klick auf "KICAD").
# Firewall: Beim ersten Start fragt Windows evtl., ob dotnet.exe im Netz erreichbar sein darf - "Private Netzwerke" zulassen (macht Christian).
# Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Port = 5134,
  [int]$BaustellePort = 5135,
  [string]$Projekt = "$env:USERPROFILE\HLKS_CAD_baustelle_test\e_KICAD",
  [string]$Bau = "$env:USERPROFILE\HLKS_CAD_build\desktop",
  [string]$Arbeitskopie = "$env:USERPROFILE\HLKS_CAD_desktop",
  [string]$Akteur = 'CS',
  [switch]$Neu,
  [switch]$OhneFenster
)

$ErrorActionPreference = 'Continue'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$dll = "$Bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$mcp = "$Bau\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$url = "http://localhost:$Port"
$log = "$Bau\baustelle_testserver.log"
function Log([string]$t) { try { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 } catch { } }
Add-Type -AssemblyName System.Windows.Forms

function Hinweis([string]$text) { [void][System.Windows.Forms.MessageBox]::Show($text, 'KICAD Baustelle (Test)') }
function Status { try { return (Invoke-WebRequest "$url/api/status" -UseBasicParsing -TimeoutSec 3).Content | ConvertFrom-Json } catch { return $null } }
function Laufender {
  $c = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $c) { return $null }
  $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($c.OwningProcess)"
  if (-not $p -or $p.CommandLine -notmatch 'Hlks\.Web\.dll') { return $null }
  return [int]$c.OwningProcess
}
function Adressen {
  Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.AddressState -eq 'Preferred' } |
    ForEach-Object { "http://$($_.IPAddress):$BaustellePort/baustelle/" }
}

# Der Baustellen-Zugang kam mit c348819 (04.11.2026). Aeltere Builds ignorieren --baustelle-port und oeffnen Port $BaustellePort nie (Befund 04.11.2026:
# "startet nicht"). Erkennbar am Klassennamen BaustelleZugang in den Metadaten des Programms. Kennt der Desktop-Build ihn noch nicht, gilt ein eigener
# Build des Arbeitsstands in %USERPROFILE%\HLKS_CAD_build\baustelle (vom Hauptlauf gebaut), bis der Desktop nachgezogen ist.
function HatZugang([string]$d) { (Test-Path $d) -and [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($d)).Contains('BaustelleZugang') }
if (-not (HatZugang $dll)) {
  $ersatz = "$env:USERPROFILE\HLKS_CAD_build\baustelle"
  if (HatZugang "$ersatz\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll") {
    Log "Desktop-Build ohne Baustellen-Zugang - nehme $ersatz"
    $Bau = $ersatz; $Arbeitskopie = (Resolve-Path "$PSScriptRoot\..").Path
    $dll = "$Bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"; $mcp = "$Bau\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
  } else {
    Log "Build $Bau ohne Baustellen-Zugang - nicht gestartet"
    Hinweis "Der KICAD-Stand auf diesem Rechner kennt den Baustellen-Testserver noch nicht.`n`nEr kommt mit dem naechsten geprueften Stand. Danach einmal 'KICAD aktualisieren' starten, dann dieses Icon."
    exit 1
  }
}
if (-not (Test-Path $mcp)) { $mcp = "$env:USERPROFILE\HLKS_CAD_build\desktop\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll" }

# Umgebung wie der Desktop (kicad_starten.ps1): Oberflaeche, Kataloge, KI-Vorlagen aus der Arbeitskopie
$env:HLKS_BUILD = $Bau
$env:HLKS_WEBROOT = "$Arbeitskopie\src\Hlks.Web\wwwroot"
$env:HLKS_KATALOG = "$Arbeitskopie\kataloge"
$env:HLKS_KI = "$Arbeitskopie\ki"
$env:HLKS_MCP_DLL = $mcp
$env:HLKS_BAUSTELLE_PORT = "$BaustellePort"

$srv = Laufender
# Laeuft ein Testserver ohne Baustellen-Port (alter Stand), wird er neu gestartet
if ($srv -and -not $Neu -and -not (Get-NetTCPConnection -State Listen -LocalPort $BaustellePort -ErrorAction SilentlyContinue)) { $Neu = $true }
if ($srv -and $Neu) {
  Log "beende Testserver $srv"
  Stop-Process -Id $srv -Force
  for ($i = 0; $i -lt 30 -and (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
  $srv = $null
}

if (-not $srv) {
  if (-not (Test-Path (Join-Path $Projekt 'projekt.json'))) {
    Log "lege Testprojekt an: $Projekt"
    New-Item -ItemType Directory -Force (Split-Path $Projekt) | Out-Null
    & $dotnet $mcp --beispiel $Projekt 2>&1 | ForEach-Object { Log "$_" }
    if (-not (Test-Path (Join-Path $Projekt 'projekt.json'))) { Hinweis "Das Testprojekt liess sich nicht anlegen. Protokoll: $log"; exit 1 }
  }
  Log "starte Testserver $Port (Baustelle $BaustellePort) mit $Projekt"
  $a = @("`"$dll`"", '--port', $Port, '--akteur', $Akteur, '--baustelle-port', $BaustellePort, '--projekt', "`"$Projekt`"")
  Start-Process -FilePath $dotnet -ArgumentList $a -WindowStyle Hidden | Out-Null
  $st = $null
  for ($i = 0; $i -lt 90 -and -not ($st = Status); $i++) { Start-Sleep -Milliseconds 700 }
  if (-not $st) { Hinweis "Der Testserver startet nicht (Port $Port). Protokoll: $log"; exit 1 }
}

# Eine Webseite muss es geben: sonst fuer die Bauleitung als Arbeitsstand erzeugen (Monteur braucht einen freigegebenen Stand - das gibt der Mensch frei)
$ausgabe = Join-Path $Projekt 'ausgabe\baustelle'
if (-not (Get-ChildItem -Path $ausgabe -Filter *.html -ErrorAction SilentlyContinue)) {
  Log 'erzeuge Baustellen-Webseite (bauleitung, arbeitsstand)'
  try {
    $antwort = Invoke-WebRequest "$url/api/abfrage/baustelle_export" -Method Post -ContentType 'application/json' -Body '{"rolle":"bauleitung","stand":"arbeitsstand"}' -UseBasicParsing -TimeoutSec 300
    Log ("Webseite: " + $antwort.Content.Substring(0, [Math]::Min(300, $antwort.Content.Length)))
  } catch { Log "Webseite nicht erzeugt: $($_.Exception.Message)" }
}

$liste = @(Adressen)
Log ("Adressen: " + ($liste -join ', '))
if (-not $OhneFenster) {
  Start-Process "http://localhost:$BaustellePort/baustelle/"
  $text = "Testserver laeuft.`n`nAm Tablet (gleiches Netz wie dieser PC) im Browser oeffnen:`n" + ($liste -join "`n") +
    "`n`nDann im Browsermenue 'Zum Startbildschirm hinzufuegen'.`nKICAD des Testservers (Testprojekt): http://localhost:$Port - dort Datei > Ausgabe > 'Baustelle (Webseite)' fuer neue Webseiten`n" +
    "und 'Rueckmeldung Baustelle' fuer die Meldungen vom Tablet.`n`nKeine Verbindung? Windows-Firewall: dotnet.exe fuer private Netzwerke zulassen."
  Hinweis $text
}

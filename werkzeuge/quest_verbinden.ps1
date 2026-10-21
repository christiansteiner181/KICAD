# KICAD auf der Meta Quest ohne Meta Link (Modul XR, Christian 21.10.2026): Der Browser der Quest oeffnet KICAD als
# http://localhost:<Port>; adb leitet den Port der Quest auf diesen PC weiter (adb reverse). KICAD bleibt nur lokal erreichbar,
# WebXR gilt auf localhost als sicher. Details: entwurf/modul-xr.md, Abschnitt "Start ohne Link-faehigen PC".
#
# Voraussetzungen (einmal): Entwicklermodus der Quest (Meta-Horizon-App), Android Platform Tools (adb) entpackt, Quest per USB
# verbunden und "USB-Debugging erlauben" in der Brille bestaetigt.
#
# Aufruf:
#   powershell -File werkzeuge\quest_verbinden.ps1                 Weiterleitung fuer Port 5130 (Desktop-KICAD) setzen
#   powershell -File werkzeuge\quest_verbinden.ps1 -Wlan           zusaetzlich auf ADB ueber WLAN umstellen (Kabel danach abziehbar)
#   powershell -File werkzeuge\quest_verbinden.ps1 -Ip 192.168.1.50   ueber WLAN mit der Quest verbinden (ohne Kabel, nach -Wlan)
#   powershell -File werkzeuge\quest_verbinden.ps1 -Adb C:\platform-tools\adb.exe   adb-Pfad angeben
#   powershell -File werkzeuge\quest_verbinden.ps1 -Trennen         Weiterleitung entfernen
# Laedt nichts herunter. Datei bewusst nur ASCII (PowerShell 5.1).
param(
  [int]$Port = 5130,
  [switch]$Wlan,
  [string]$Ip = '',
  [string]$Adb = '',
  [switch]$Trennen
)
$ErrorActionPreference = 'Stop'

function Meldung([string]$t) { Write-Host $t }
function Fehler([string]$t) { Write-Host "FEHLER: $t" -ForegroundColor Red; exit 1 }

# ---- adb finden
if ($Adb -and -not (Test-Path $Adb)) { Fehler "adb.exe unter '$Adb' nicht gefunden." }
if (-not $Adb) {
  $cmd = Get-Command adb.exe -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($cmd) { $Adb = $cmd.Source }
}
if (-not $Adb) {
  $kandidaten = @(
    "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    "$env:USERPROFILE\platform-tools\adb.exe",
    "$env:USERPROFILE\Downloads\platform-tools\adb.exe",
    "$env:USERPROFILE\OneDrive\Downloads\platform-tools\adb.exe",
    "C:\platform-tools\adb.exe",
    "C:\Android\platform-tools\adb.exe"
  )
  $Adb = $kandidaten | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Adb) {
  Fehler ("adb.exe nicht gefunden. Android Platform Tools von der offiziellen Android-Seite herunterladen, entpacken " +
    "(z. B. nach C:\platform-tools) und das Skript erneut starten oder mit -Adb <Pfad zu adb.exe> aufrufen.")
}
Meldung "adb: $Adb"

function AdbAufruf([string[]]$a) {
  $o = & $Adb @a 2>&1
  return ($o | Out-String).Trim()
}

# ---- Trennen
if ($Trennen) {
  Meldung (AdbAufruf @('reverse', '--remove', "tcp:$Port"))
  Meldung "Weiterleitung fuer Port $Port entfernt."
  exit 0
}

# ---- KICAD auf diesem PC?
$laeuft = $null -ne (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1)
if (-not $laeuft) {
  Meldung "Hinweis: Auf Port $Port laeuft gerade kein KICAD. Zuerst KICAD ueber das Icon starten (die Weiterleitung wird trotzdem gesetzt)."
}

# ---- Verbindung zur Quest
[void](AdbAufruf @('start-server'))
if ($Ip) {
  $ziel = if ($Ip -match ':') { $Ip } else { "$($Ip):5555" }
  Meldung (AdbAufruf @('connect', $ziel))
}
$geraete = (AdbAufruf @('devices')) -split "`r?`n" | Where-Object { $_ -match '\tdevice$' }
$ungeprueft = (AdbAufruf @('devices')) -split "`r?`n" | Where-Object { $_ -match '\tunauthorized$' }
if ($ungeprueft) { Fehler "Die Quest ist verbunden, aber nicht freigegeben: in der Brille 'USB-Debugging erlauben' bestaetigen und erneut starten." }
if (-not $geraete) { Fehler "Keine Quest gefunden. USB-Kabel pruefen (oder mit -Ip <Adresse der Quest> ueber WLAN verbinden), Entwicklermodus aktiv?" }
if (@($geraete).Count -gt 1 -and -not $Ip) { Meldung "Mehrere Geraete verbunden: $(@($geraete) -join '; ') - es wird das erste verwendet." }
$serial = ((@($geraete)[0]) -split "`t")[0]
Meldung "Quest: $serial"

# ---- auf WLAN umstellen (einmal mit Kabel)
if ($Wlan) {
  $ipQuest = ((AdbAufruf @('-s', $serial, 'shell', 'ip', '-f', 'inet', 'addr', 'show', 'wlan0')) -split "`r?`n" |
    Where-Object { $_ -match 'inet (\d+\.\d+\.\d+\.\d+)' } | ForEach-Object { $matches[1] } | Select-Object -First 1)
  Meldung (AdbAufruf @('-s', $serial, 'tcpip', '5555'))
  Start-Sleep -Seconds 2
  if ($ipQuest) {
    Meldung (AdbAufruf @('connect', "$($ipQuest):5555"))
    $serial = "$($ipQuest):5555"
    Meldung "WLAN aktiv. Das Kabel kann ab. Naechstes Mal: quest_verbinden.ps1 -Ip $ipQuest"
  } else {
    Meldung "Adresse der Quest nicht gefunden (WLAN an?). Spaeter mit -Ip <Adresse> verbinden."
  }
}

# ---- Weiterleitung
$r = AdbAufruf @('-s', $serial, 'reverse', "tcp:$Port", "tcp:$Port")
if ($LASTEXITCODE -ne 0 -and $r) { Fehler "adb reverse fehlgeschlagen: $r" }
$liste = AdbAufruf @('-s', $serial, 'reverse', '--list')
if ($liste -notmatch "tcp:$Port") { Fehler "Weiterleitung nicht aktiv. adb meldet: $liste" }
Meldung ""
Meldung "Bereit. In der Quest den Browser oeffnen und eingeben:  http://localhost:$Port"
Meldung "Dann im Grundriss 'Raum begehen' und 'In VR begehen'. Beenden der Weiterleitung: quest_verbinden.ps1 -Trennen"
exit 0

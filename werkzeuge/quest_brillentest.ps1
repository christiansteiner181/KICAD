# Brillentest der Quest App (Modul XR, Interface-Labor P4a), bis P7 das in die Oberflaeche bringt:
#   1. KICAD-Server dieses Stands auf Port 5130 (nur dort oeffnet sich der Netzzugang) mit einer Arbeitskopie von Test_praxis
#      (Christian 23.10.2026: "fuer tests im echten projekt das test_praxis projekt verwenden", "ohne einschraenkung").
#   2. Netzzugang dieses PCs einschalten (%LOCALAPPDATA%\HLKS_CAD\xr\zugang.json), Kopplungspaket fuer die Brille erzeugen.
#   3. App per adb installieren, Paket nach /sdcard/Android/data/ch.hkplanung.kicad.quest/files/kopplung.json legen (die App uebernimmt
#      es beim Start in ihren privaten Speicher und loescht es dort), App starten. Das Paket liegt am PC nie auf der Platte.
#   4. Optional VR-Auftrag: Begehen im ersten Raum des Modells.
# Der Server laeuft danach weiter (Fenster ausgeblendet). Ende: -Beenden widerruft die Kopplung, schaltet den Zugang aus, beendet den Server.
# Voraussetzungen: Desktop-KICAD beendet (Port 5130 frei); Firewall-Regeln (werkzeuge\xr_firewall.ps1, Christian als Administrator);
# Brille per adb verbunden (Kabel oder WLAN) und frei (nicht in Gebrauch, ausser Christian ist bereit).
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\quest_brillentest.ps1 [-Projekt <ordner>] [-OhneAuftrag] [-Beenden]
# Datei bewusst nur ASCII (PowerShell 5.1). Laedt nichts herunter.
param(
  [string]$Projekt = '',
  [switch]$OhneAuftrag,
  [switch]$Kabel,
  [switch]$Beenden
)
# -Kabel: ohne WLAN-Zugang ueber die Firewall (Netz "Oeffentlich" oder Regeln fehlen) - adb reverse leitet den HTTPS-Port der Brille ueber das
# Kabel auf diesen PC, und das Paket nennt 127.0.0.1 als erste Adresse (Christian 23.10.2026: "Jetzt ueber Kabel"). K3 (WLAN) bleibt offen.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$adb = 'C:\Users\chris\platform-tools\adb.exe'
$port = 5130
if (-not $env:HLKS_BUILD) { $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build" }
$bau = $env:HLKS_BUILD
$web = "$bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$apk = "$bau\Hlks.Quest.Android\bin\Release\net10.0-android\publish\ch.hkplanung.kicad.quest-Signed.apk"
$ablage = "$env:LOCALAPPDATA\HLKS_CAD\xr\brillentest"
$zustand = "$ablage\zustand.json"
$basis = "http://localhost:$port"
$utf8 = New-Object System.Text.UTF8Encoding $false
New-Item -ItemType Directory -Force $ablage | Out-Null
function Aus([string]$t) { $z = "{0:HH:mm:ss} {1}" -f (Get-Date), $t; Write-Host $z; Add-Content -Path "$ablage\protokoll.log" -Value $z -Encoding UTF8 }
function Post([string]$pfad, $koerper) {
  $json = $koerper | ConvertTo-Json -Depth 8 -Compress
  (Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$basis$pfad" -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($json) -TimeoutSec 300).Content
}
function Get([string]$pfad) { (Invoke-WebRequest -UseBasicParsing -Uri "$basis$pfad" -TimeoutSec 60).Content | ConvertFrom-Json }

if ($Beenden) {
  if (Test-Path $zustand) {
    $s = Get-Content $zustand -Raw | ConvertFrom-Json
    try { Post '/api/xr/kopplung/widerrufen' @{ kopplung = $s.kopplung } | Out-Null; Aus 'Kopplung widerrufen.' } catch { Aus 'Widerruf nicht moeglich (Server weg?).' }
    try { Post '/api/xr/zugang' @{ an = $false } | Out-Null; Aus 'Netzzugang aus.' } catch { }
    if ($s.serverPid) { Stop-Process -Id $s.serverPid -Force -ErrorAction SilentlyContinue; Aus "Server $($s.serverPid) beendet." }
    if ($s.serie) { & $adb -s $s.serie reverse --remove-all 2>$null | Out-Null }
    Remove-Item $zustand -Force
  } else { Aus 'Kein laufender Brillentest.' }
  exit 0
}

if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { Aus "Port $port belegt (Desktop-KICAD?). Zuerst beenden."; exit 2 }
foreach ($d in $web, $apk) { if (-not (Test-Path $d)) { Aus "Build fehlt: $d"; exit 2 } }
$geraet = (& $adb devices) | Where-Object { $_ -match '\tdevice$' } | Select-Object -First 1
if (-not $geraet) { Aus 'Keine Brille per adb verbunden (adb devices).'; exit 2 }
$serie = ($geraet -split '\t')[0]
$modell = (& $adb -s $serie shell getprop ro.product.model).Trim()
Aus "Brille: $modell ($serie)"

if (-not $Projekt) {
  $Projekt = "$env:USERPROFILE\HLKS_CAD_testdaten\test_praxis_arbeit"
  if (-not (Test-Path "$Projekt\projekt.json")) {
    Aus "Lege Arbeitskopie von Test_praxis an: $Projekt"
    & robocopy.exe "$env:USERPROFILE\HLKS_CAD_testdaten\test_praxis" $Projekt /E /NFL /NDL /NJH /NJS /NP | Out-Null
    Get-ChildItem $Projekt -Recurse -File | ForEach-Object { $_.IsReadOnly = $false }
  }
}
if (-not (Test-Path "$Projekt\projekt.json")) { Aus "Kein Projekt in $Projekt"; exit 2 }

$tmp = Join-Path $env:TEMP 'hlks_brillentest'
New-Item -ItemType Directory -Force "$tmp\benutzer", "$tmp\zuletzt" | Out-Null
# Server losgeloest starten (werkzeuge\losgeloest.ps1, WMI): sonst erbt er die Ausgabe dieses Skripts, und der Aufrufer wartet, bis der Server
# endet (Befund Brillentest 23.10.2026). Umgebungsvariablen gehen ueber -Umgebung mit; die PID kommt danach vom Port.
Aus "Starte KICAD-Server ($web) auf Port $port ..."
$startSkript = "$ablage\server_start.ps1"
Set-Content -Path $startSkript -Encoding ASCII -Value ("& '{0}' '{1}' --port {2} --geraet brillentest --akteur brillentest" -f $dotnet, $web, $port)
$umgebung = "HLKS_BENUTZER=$tmp\benutzer;HLKS_ZULETZT=$tmp\zuletzt;HLKS_WEBROOT=$repo\src\Hlks.Web\wwwroot"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\losgeloest.ps1" -Skript $startSkript -Log "$ablage\server.log" -Umgebung $umgebung | Out-Null
$bis = (Get-Date).AddSeconds(90)
while ($true) {
  try { Get '/api/status' | Out-Null; break } catch { }
  if ((Get-Date) -gt $bis) { Aus "Der Server antwortet nicht (Protokoll: $ablage\server.log)."; exit 2 }
  Start-Sleep -Milliseconds 300
}
$server = [pscustomobject]@{ Id = (Get-NetTCPConnection -State Listen -LocalPort $port | Select-Object -First 1).OwningProcess }
Post '/api/projekt/oeffnen' @{ ordner = $Projekt } | Out-Null
Aus "Projekt geoeffnet: $Projekt"
Post '/api/xr/zugang' @{ an = $true } | Out-Null
$bis = (Get-Date).AddSeconds(30)
while (-not (Get '/api/xr/zugang').aktiv) {
  if ((Get-Date) -gt $bis) { Aus ('Netzzugang startet nicht: ' + ((Get '/api/xr/zugang') | ConvertTo-Json -Compress)); exit 2 }
  Start-Sleep -Milliseconds 200
}
$z = Get '/api/xr/zugang'
Aus "Netzzugang aktiv: HTTPS Port $($z.port), Suche UDP $($z.suchPort), Fingerabdruck $($z.fingerabdruckKurz)"
$inhalt = Post '/api/xr/kopplung/paket' @{ brille = $serie; name = "$modell Brillentest" }
if ($Kabel) {
  & $adb -s $serie reverse "tcp:$($z.port)" "tcp:$($z.port)" | Out-Null
  $o = $inhalt | ConvertFrom-Json
  $o.paket.ips = @('127.0.0.1') + @($o.paket.ips | Where-Object { $_ -ne '127.0.0.1' })
  $inhalt = $o | ConvertTo-Json -Depth 10 -Compress
  Aus "Kabel: adb reverse tcp:$($z.port), 127.0.0.1 als erste Adresse im Paket."
}
$kopplung = ($inhalt | ConvertFrom-Json).paket.kopplung
Aus ('Paket fuer IPs: ' + ((($inhalt | ConvertFrom-Json).paket.ips) -join ', '))
@{ serverPid = $server.Id; kopplung = $kopplung; serie = $serie } | ConvertTo-Json | Set-Content -Path $zustand -Encoding UTF8

Aus 'Installiere die App ...'
& $adb -s $serie install -r $apk | ForEach-Object { Aus "  $_" }
$ziel = '/sdcard/Android/data/ch.hkplanung.kicad.quest/files'
& $adb -s $serie shell mkdir -p $ziel | Out-Null
$datei = Join-Path $tmp 'kopplung.json'
try {
  [IO.File]::WriteAllText($datei, $inhalt, $utf8)
  & $adb -s $serie push $datei "$ziel/kopplung.json" | Out-Null
} finally { Remove-Item $datei -Force -ErrorAction SilentlyContinue }
Aus 'Kopplungspaket in der Brille abgelegt (am PC geloescht).'

if (-not $OhneAuftrag) {
  $koerper = (Invoke-WebRequest -UseBasicParsing -Uri "$basis/api/koerper" -TimeoutSec 300).Content
  $treffer = [regex]::Match($koerper, '"id":"([^"]+)","ebene":"raum"')
  if ($treffer.Success) { Post '/api/vr/auftrag' @{ modus = 'begehen'; raum = $treffer.Groups[1].Value } | Out-Null; Aus 'VR-Auftrag: begehen im ersten Raum.' }
}
& $adb -s $serie logcat -c
$akt = (& $adb -s $serie shell cmd package resolve-activity --brief ch.hkplanung.kicad.quest | Select-Object -Last 1).Trim()
& $adb -s $serie shell am start -n $akt | Out-Null
Aus "App gestartet ($akt). Protokoll: adb logcat -s DOTNET  |  Ende: quest_brillentest.ps1 -Beenden"

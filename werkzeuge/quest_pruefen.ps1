# Quest App am PC pruefen, mit Modell (Quest App P4a): ein KICAD-Testserver auf diesem PC, ein Kopplungspaket, dann die PC-Fassung der App im Simulator
# zweimal mit --pruefen - einmal mit PC (verbinden, Modell laden, Kanal) und einmal ohne PC (Start aus dem Zwischenspeicher).
#   - Server: Hlks.Web aus dem Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build), im Testlauf (HLKS_TEST=1): Benutzereinstellungen, Liste
#     "zuletzt" und XR-Ablage nur unter %TEMP%, Netzzugang nur auf 127.0.0.1 mit freiem Port. Der Netzzugang oeffnet sich nur im KICAD auf dem
#     Desktop-Port 5130 (XrEinstellungen.DesktopPort) - deshalb laeuft der Testserver auf 5130; ist der Port belegt (Desktop-KICAD laeuft), bricht
#     das Skript ab.
#   - Projekt: das Beispielprojekt "MFH Musterweg" (Vorgabe) oder mit -Projekt eine KOPIE des angegebenen Projektordners unter %TEMP% (das Original
#     wird nur gelesen; z. B. %USERPROFILE%\HLKS_CAD_testdaten\test_praxis). Echte Daten bleiben lokal, die App schreibt nur Zahlen.
#   - Die GLB holt die App ueber den Netzzugang; gibt er sie (noch) nicht frei (404), ueber http://localhost:<Port> (--modell-lokal).
# P4b: Lauf 3 "Sitzungsraum" - VR-Auftrag "Im Sitzungsraum zeigen" (modus sitzungsraum, Raum) vom PC; die App startet im Sitzungsraum und misst drei
# Abschnitte (Etagenschnitt des Auftrags, alle Etagen, Raum zeigen) mit Bildzeit, Dreiecken und Aufrufen; dazu die Regeln (Griffe, Tisch antippen,
# Passthrough im Simulator nicht verfuegbar, Shader "schnitt") und die Ebenen (Probe "fliessrichtung"). -OhneSitzungsraum laesst ihn weg; -Pruefbilder <ordner>
# legt je Abschnitt zwei JPEG des Sitzungsraums ab (nur lokal, nie ins Repository - bei echten Projekten zeigen sie echte Daten).
# Am Ende: Kopplung widerrufen, Server beenden, temporaere Ordner loeschen (Protokolle bleiben in <Build>\quest_pruefen).
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\quest_pruefen.ps1 [-Bilder 300] [-Projekt <ordner>] [-OhneBau] [-OhneUnterlage] [-OhneSitzungsraum] [-Pruefbilder <ordner>]
# Exit 0 = alle Laeufe "P4 ok", 1 = ein Lauf nicht ok, 2 = Fehler im Ablauf. Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Bilder = 300,
  [string]$Projekt = '',
  [switch]$OhneBau,
  [switch]$OhneUnterlage,
  [switch]$OhneSitzungsraum,
  [string]$Pruefbilder = ''
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$port = 5130
if (-not $env:HLKS_BUILD) { $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build" }
$bau = $env:HLKS_BUILD
$logs = "$bau\quest_pruefen"
New-Item -ItemType Directory -Force $logs | Out-Null
function Aus([string]$t) { $z = "{0:HH:mm:ss} {1}" -f (Get-Date), $t; Write-Host $z; Add-Content -Path "$logs\protokoll.log" -Value $z -Encoding UTF8 }

if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
  Aus "Port $port ist belegt (laeuft das Desktop-KICAD?). Der Netzzugang oeffnet sich nur auf diesem Port - zuerst KICAD beenden."
  exit 2
}
if ($Projekt -and -not (Test-Path (Join-Path $Projekt 'projekt.json'))) { Aus "Kein Projekt in $Projekt (projekt.json fehlt)."; exit 2 }

if (-not $OhneBau) {
  Aus "Baue Hlks.Web und Hlks.Quest ($bau) ..."
  & $dotnet build "$repo\src\Hlks.Web\Hlks.Web.csproj" -v q -nologo | Out-Null
  if ($LASTEXITCODE -ne 0) { Aus 'Bau Hlks.Web fehlgeschlagen'; exit 2 }
  & $dotnet build "$repo\src\Hlks.Quest\Hlks.Quest.csproj" -c Release -v q -nologo | Out-Null
  if ($LASTEXITCODE -ne 0) { Aus 'Bau Hlks.Quest fehlgeschlagen'; exit 2 }
}
$web = "$bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$quest = "$bau\Hlks.Quest\bin\Release\net10.0\Hlks.Quest.dll"
foreach ($d in $web, $quest) { if (-not (Test-Path $d)) { Aus "Build fehlt: $d"; exit 2 } }

$tmp = Join-Path $env:TEMP ('hlks_quest_pruefen_' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force "$tmp\benutzer", "$tmp\zuletzt" | Out-Null
$env:HLKS_TEST = '1'
$env:HLKS_BENUTZER = "$tmp\benutzer"
$env:HLKS_ZULETZT = "$tmp\zuletzt"
$env:HLKS_WEBROOT = "$repo\src\Hlks.Web\wwwroot"
$basis = "http://localhost:$port"
$utf8 = New-Object System.Text.UTF8Encoding $false

function Post([string]$pfad, $koerper) {
  $json = $koerper | ConvertTo-Json -Depth 8 -Compress
  $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$basis$pfad" -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($json) -TimeoutSec 300
  return $r.Content
}
function Get([string]$pfad) { (Invoke-WebRequest -UseBasicParsing -Uri "$basis$pfad" -TimeoutSec 60).Content | ConvertFrom-Json }

$server = $null
$ergebnis = 2
$kopplung = $null
try {
  Aus "Starte KICAD-Testserver auf Port $port ..."
  $server = Start-Process -FilePath $dotnet -ArgumentList @("`"$web`"", '--port', "$port", '--geraet', 'questpruefung', '--akteur', 'pruefung') `
    -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logs\server.log" -RedirectStandardError "$logs\server_fehler.log"
  $bis = (Get-Date).AddSeconds(90)
  while ($true) {
    try { Get '/api/status' | Out-Null; break } catch { }
    if ((Get-Date) -gt $bis -or $server.HasExited) { throw 'Der Testserver antwortet nicht.' }
    Start-Sleep -Milliseconds 300
  }

  if ($Projekt) {
    Aus "Kopiere $Projekt nach $tmp\projekt (nur gelesen) ..."
    & robocopy.exe $Projekt "$tmp\projekt" /E /NFL /NDL /NJH /NJS /NP | Out-Null
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Post '/api/projekt/oeffnen' @{ ordner = "$tmp\projekt" } | Out-Null
    Aus ("Projekt geoeffnet in {0:0.0} s" -f $sw.Elapsed.TotalSeconds)
  } else {
    Post '/api/projekt/anlegen' @{ ordner = "$tmp\projekt\e_KICAD"; name = 'MFH Musterweg'; beispiel = $true } | Out-Null
    Aus 'Beispielprojekt MFH Musterweg angelegt.'
  }

  Post '/api/xr/zugang' @{ an = $true } | Out-Null
  $bis = (Get-Date).AddSeconds(30)
  while (-not (Get '/api/xr/zugang').aktiv) {
    if ((Get-Date) -gt $bis) { throw ('Der Netzzugang startet nicht: ' + ((Get '/api/xr/zugang') | ConvertTo-Json -Compress)) }
    Start-Sleep -Milliseconds 200
  }
  $z = Get '/api/xr/zugang'
  Aus "Netzzugang aktiv: HTTPS 127.0.0.1:$($z.port), Suche UDP $($z.suchPort), Fingerabdruck $($z.fingerabdruckKurz)"
  $inhalt = Post '/api/xr/kopplung/paket' @{ brille = 'PC-PRUEFUNG'; name = 'Pruefung am PC' }
  $kopplung = ($inhalt | ConvertFrom-Json).paket.kopplung
  [IO.File]::WriteAllText("$tmp\kopplung.json", $inhalt, $utf8)

  # VR-Auftrag wie aus dem Kontextmenue eines Raums (Id des ersten Raumkoerpers): Lauf 1 muss in diesem Raum starten (Auftrag=Nr 1 ...)
  $koerper = (Invoke-WebRequest -UseBasicParsing -Uri "$basis/api/koerper" -TimeoutSec 300).Content
  $treffer = [regex]::Match($koerper, '"id":"([^"]+)","ebene":"raum"')
  if ($treffer.Success) {
    Post '/api/vr/auftrag' @{ modus = 'begehen'; raum = $treffer.Groups[1].Value } | Out-Null
    Aus 'VR-Auftrag gesetzt: begehen, erster Raum des Modells.'
  } else { Aus 'Kein Raum fuer einen VR-Auftrag gefunden.' }

  $zusatz = @()
  if ($OhneUnterlage) { $zusatz += '--ohne-unterlage' }
  $muster = '^P4 (ok|Fehler|Laden)|Modell |Start |Netzzugang gibt|Auftrag vom PC|Ebenen |Passthrough'
  Aus "Lauf 1: mit PC ($Bilder Bilder) ..."
  $a1 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung.json" --speicher "$tmp\app" --modell-lokal $port @zusatz 2>&1
  $c1 = $LASTEXITCODE
  $a1 | Out-File -FilePath "$logs\lauf1.log" -Encoding utf8
  $a1 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
  Aus "Lauf 2: ohne PC, Start aus dem Zwischenspeicher ($Bilder Bilder) ..."
  $a2 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung.json" --speicher "$tmp\app" --ohne-pc @zusatz 2>&1
  $c2 = $LASTEXITCODE
  $a2 | Out-File -FilePath "$logs\lauf2.log" -Encoding utf8
  $a2 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
  $auftragOk = (-not $treffer.Success) -or ($a1 -match 'Auftrag=Nr 1 ')
  if (-not $auftragOk) { Aus 'Lauf 1: der VR-Auftrag vom PC wurde nicht angewandt.' }
  $c3 = 0
  $sitzungOk = $true
  if (-not $OhneSitzungsraum -and $treffer.Success) {
    # P4b: "Im Sitzungsraum zeigen (VR)" wie aus dem Kontextmenue eines Raums: modus sitzungsraum mit Raum (Etagenschnitt im Geschoss des Raums)
    Post '/api/vr/auftrag' @{ modus = 'sitzungsraum'; raum = $treffer.Groups[1].Value } | Out-Null
    Aus "Lauf 3: Sitzungsraum mit PC ($Bilder Bilder, Auftrag sitzungsraum) ..."
    $bildArg = @()
    if ($Pruefbilder) { $bildArg = @('--bilder', $Pruefbilder) }
    $a3 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung.json" --speicher "$tmp\app" --modell-lokal $port @zusatz @bildArg 2>&1
    $c3 = $LASTEXITCODE
    $a3 | Out-File -FilePath "$logs\lauf3.log" -Encoding utf8
    $a3 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
    $sitzungOk = ($a3 -match 'Sitzungsraum=ja') -and ($a3 -match 'Auftrag=Nr 2 Sitzungsraum')
    if (-not $sitzungOk) { Aus 'Lauf 3: die App war nicht im Sitzungsraum des Auftrags.' }
  }
  $ergebnis = if ($c1 -eq 0 -and $c2 -eq 0 -and $c3 -eq 0 -and $auftragOk -and $sitzungOk) { 0 } else { 1 }
} catch {
  Aus ('Fehler: ' + $_.Exception.Message)
  $ergebnis = 2
} finally {
  if ($kopplung) { try { Post '/api/xr/kopplung/widerrufen' @{ kopplung = $kopplung } | Out-Null } catch { } }
  if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500 }
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
  foreach ($v in 'HLKS_TEST', 'HLKS_BENUTZER', 'HLKS_ZULETZT', 'HLKS_WEBROOT') { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
}
Aus ("Ergebnis: " + @('ok', 'nicht ok', 'Fehler im Ablauf')[$ergebnis] + " (Protokolle: $logs)")
exit $ergebnis

# Quest App am PC pruefen, mit Modell (Quest App P4a): ein KICAD-Testserver auf diesem PC, ein Kopplungspaket, dann die PC-Fassung der App im Simulator
# zweimal mit --pruefen - einmal mit PC (verbinden, Modell laden, Kanal) und einmal ohne PC (Start aus dem Zwischenspeicher).
#   - Server: Hlks.Web aus dem Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build), im Testlauf (HLKS_TEST=1): Benutzereinstellungen, Liste
#     "zuletzt" und XR-Ablage nur unter %TEMP%, Netzzugang nur auf 127.0.0.1 mit freiem Port. Der Netzzugang oeffnet sich nur im KICAD auf dem
#     Desktop-Port (XrEinstellungen.DesktopPort, 5130). Der Testserver laeuft auf 5130, wenn der Port frei ist. Ist er belegt (Desktop-KICAD laeuft),
#     nimmt das Skript selbstaendig den ersten freien Port von 5139 abwaerts bis 5132 (nie 5131, 5140/5141 Zugang und Suche des Desktops, 5149-5153
#     Abnahme) und setzt fuer den Testserver HLKS_XR_DESKTOP_PORT auf diesen Port: im Testlauf (HLKS_TEST=1) oeffnet sich der Zugang dann dort, nur auf
#     127.0.0.1 und mit der Ablage unter %TEMP% - zugang.json, Kopplungen und Zertifikat des Desktop-KICAD (%LOCALAPPDATA%\HLKS_CAD\xr) bleiben unberuehrt,
#     das Desktop-KICAD laeuft weiter. -Port <n> waehlt den Port fest (belegt = Abbruch).
#   - Projekt: das Beispielprojekt "MFH Musterweg" (Vorgabe) oder mit -Projekt eine KOPIE des angegebenen Projektordners unter %TEMP% (das Original
#     wird nur gelesen; z. B. %USERPROFILE%\HLKS_CAD_testdaten\test_praxis). Echte Daten bleiben lokal, die App schreibt nur Zahlen.
#   - Die GLB holt die App ueber den Netzzugang; gibt er sie (noch) nicht frei (404), ueber http://localhost:<Port> (--modell-lokal).
# P4b: Lauf 3 "Sitzungsraum" - VR-Auftrag "Im Sitzungsraum zeigen" (modus sitzungsraum, Raum) vom PC; die App startet im Sitzungsraum und misst drei
# Abschnitte (Etagenschnitt des Auftrags, alle Etagen, Raum zeigen) mit Bildzeit, Dreiecken und Aufrufen; dazu die Regeln (Griffe, Tisch antippen,
# Passthrough im Simulator nicht verfuegbar, Shader "schnitt") und die Ebenen (Probe "fliessrichtung"). -OhneSitzungsraum laesst ihn weg; -Pruefbilder <ordner>
# legt je Abschnitt zwei JPEG des Sitzungsraums ab (nur lokal, nie ins Repository - bei echten Projekten zeigen sie echte Daten).
# P5a: jeder Lauf prueft dazu die Bedienung (Zeile "P5a: Stellschrauben, Apps, Etage" in "P4 ok"); -Pruefbilder legt auch Bilder der schwebenden Apps ab
# (begehen_blick.jpg, apps_begehen.jpg, apps_sitzung.jpg, apps_etagen.jpg).
# P5b: der Server nimmt die Kataloge dieser Arbeitskopie (HLKS_KATALOG = <repo>\kataloge). Jeder Lauf prueft dazu das Labor (Zeile "P5b: Fassung, Katalog,
# Grenzen, Aufgaben, Roentgen bzw. Flache Hand" in "P4 ok"): Lauf 1 holt den Katalog vom PC und spielt die Pruefaufgaben "drehen-90" (abgebrochen) und
# "teleport-markierung" (Erfolg), Lauf 2 nimmt den abgelegten Katalog, Lauf 3 spielt "modell-greifen-zoomen". Das Skript prueft am PC (GET
# /api/xr/labor/auswertung), dass die Messprotokolle von Lauf 1 und 3 angekommen sind. -Pruefbilder legt zusaetzlich aufgabe_karte.jpg, roentgen.jpg und
# sitzung_flache_hand.jpg ab.
# V1a: jeder Lauf prueft dazu Glas, Umgebung, Fang, Messen und Marker (Zeile "V1a: ..." in "P4 ok"), Lauf 1 auch "messen-wand-wand" (Erfolg). Lauf 1 setzt einen
# Problem-Marker (mit PC), Lauf 2 einen ohne PC (er wartet), Lauf 3 sendet den wartenden und setzt einen am Tisch: das Skript prueft am PC (GET /api/xr/marker),
# dass danach 3 Marker mehr im Projekt sind. -Pruefbilder legt zusaetzlich glas_apps.jpg, glas_marker.jpg, messen.jpg, marker.jpg, glas_apps_sitzung.jpg,
# glas_tisch.jpg, marker_sitzung.jpg und sitzung_umgebung.jpg ab.
# V1c: Lauf 4 "Desktop in der Brille" (--desktop): die App zieht mit einer gedachten Hand den Bildschirm des PCs herein (der Testserver erfasst im Testlauf nur das
# Testbild, nie den echten Bildschirm), misst Bildzeit, Hochladen und Dekodieren und wischt ihn wieder weg (Zeile "P4 ok: Desktop: ..."). Das Skript prueft am PC
# (GET /api/xr/desktop/zustand), dass KICAD die mitsehende Brille angezeigt hat. -Pruefbilder legt desktop_*.jpg (Geste halb, eingeblendet mit Glasrahmen, nah,
# Leiste nah, Symbolfeld seitlich daneben, Wegwischen halb) und desktop_7_anzeige_pc.png (KICAD im Edge ueber CDP, werkzeuge\seitenbild.mjs, solange die Brille
# mitsieht) ab. -NurDesktop: nur Lauf 4; -OhneDesktop: ohne Lauf 4.
# Am Ende: Kopplung widerrufen, Server beenden, temporaere Ordner loeschen (Protokolle bleiben in <Build>\quest_pruefen).
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\quest_pruefen.ps1 [-Bilder 300] [-Projekt <ordner>] [-OhneBau] [-OhneUnterlage] [-OhneSitzungsraum] [-Pruefbilder <ordner>] [-Port <n>] [-NurDesktop] [-OhneDesktop]
# Exit 0 = alle Laeufe "P4 ok", 1 = ein Lauf nicht ok, 2 = Fehler im Ablauf. Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Bilder = 300,
  [string]$Projekt = '',
  [switch]$OhneBau,
  [switch]$OhneUnterlage,
  [switch]$OhneSitzungsraum,
  [string]$Pruefbilder = '',
  [int]$port = 0,
  [switch]$NurDesktop,
  [switch]$OhneDesktop
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not $env:HLKS_BUILD) { $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build" }
$bau = $env:HLKS_BUILD
$logs = "$bau\quest_pruefen"
New-Item -ItemType Directory -Force $logs | Out-Null
function Aus([string]$t) { $z = "{0:HH:mm:ss} {1}" -f (Get-Date), $t; Write-Host $z; Add-Content -Path "$logs\protokoll.log" -Value $z -Encoding UTF8 }
function Belegt([int]$p) { [bool](Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction SilentlyContinue) }

# Port des Testservers ($port, in PowerShell dieselbe Variable wie der Parameter -Port): 5130, wenn frei; sonst der erste freie Ersatzport (das Desktop-KICAD
# laeuft weiter); mit -Port fest
$gesperrt = @(5013, 5098, 5131, 5140, 5141, 5149, 5150, 5151, 5152, 5153)
if ($port -ne 0) {
  if ($gesperrt -contains $port -or $port -lt 1024 -or $port -gt 65535) { Aus "Port $port ist fuer den Pruefserver nicht erlaubt."; exit 2 }
  if (Belegt $port) { Aus "Port $port ist belegt."; exit 2 }
} elseif (-not (Belegt 5130)) {
  $port = 5130
} else {
  foreach ($p in 5139, 5138, 5137, 5136, 5135, 5134, 5133, 5132) { if (-not (Belegt $p)) { $port = $p; break } }
  if ($port -eq 0) { Aus 'Port 5130 ist belegt (Desktop-KICAD) und kein Ersatzport 5132-5139 ist frei.'; exit 2 }
  Aus "Port 5130 ist belegt (Desktop-KICAD laeuft weiter) - der Pruefserver nimmt Port $port."
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
$env:HLKS_KATALOG = "$repo\kataloge"
# Netzzugang auf dem Port des Pruefservers (wirkt nur im Testlauf; XrEinstellungen.DesktopPortVariable)
$env:HLKS_XR_DESKTOP_PORT = "$port"
$basis = "http://localhost:$port"
$utf8 = New-Object System.Text.UTF8Encoding $false

function Post([string]$pfad, $koerper) {
  $json = $koerper | ConvertTo-Json -Depth 8 -Compress
  $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$basis$pfad" -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($json) -TimeoutSec 300
  return $r.Content
}
function Get([string]$pfad) { (Invoke-WebRequest -UseBasicParsing -Uri "$basis$pfad" -TimeoutSec 60).Content | ConvertFrom-Json }
# P5b: Laeufe einer Pruefaufgabe in den Messprotokollen am PC (0, wenn keine)
function Laeufe([string]$id) {
  try { $a = Get '/api/xr/labor/auswertung'; $z = $a.aufgaben | Where-Object { $_.aufgabe -eq $id } | Select-Object -First 1; if ($z -and $z.laeufe) { return [int]$z.laeufe } } catch { }
  return 0
}
# V1a: Problem-Marker im Projekt am PC (0, wenn keine)
function MarkerZahl {
  try { $a = Get '/api/xr/marker'; if ($a.marker) { return @($a.marker).Count } } catch { }
  return 0
}

# V1c: Lauf 4 "Desktop in der Brille" - die App laeuft im Hintergrund, das Skript liest waehrenddessen die Anzeige am PC (und fotografiert KICAD im Edge)
function DesktopLauf {
  Aus "Lauf 4: Desktop in der Brille (Testbild des Servers) ..."
  $dArg = @("`"$quest`"", '--pruefen', [Math]::Max($Bilder, 600), '--desktop', '--kopplung', "`"$tmp\kopplung.json`"", '--speicher', "`"$tmp\app`"", '--modell-lokal', "$port")
  if ($OhneUnterlage) { $dArg += '--ohne-unterlage' }
  if ($Pruefbilder) { New-Item -ItemType Directory -Force $Pruefbilder | Out-Null; $dArg += @('--bilder', "`"$Pruefbilder`"") }
  $ap = Start-Process -FilePath $dotnet -ArgumentList $dArg -PassThru -NoNewWindow -RedirectStandardOutput "$logs\lauf4.log" -RedirectStandardError "$logs\lauf4_fehler.log"
  $null = $ap.Handle   # sonst liefert ExitCode in PowerShell 5.1 nach dem Ende nichts
  $edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
  $gesehen = $null; $foto = $false; $seit = $null
  while (-not $ap.HasExited) {
    try {
      $dz = Get '/api/xr/desktop/zustand'
      if ($dz.aktiv -ge 1) {
        if (-not $seit) { $seit = Get-Date }
        # die Anzeige mit den meisten Bildern behalten (gleich nach dem Verbinden zeigt sie noch 0 Bilder/s)
        if (-not $gesehen -or [long]@($dz.stroeme)[0].bilder -ge [long]@($gesehen.stroeme)[0].bilder) { $gesehen = $dz }
      }
    } catch { }
    if ($seit -and ((Get-Date) - $seit).TotalSeconds -ge 2 -and $Pruefbilder -and -not $foto -and (Test-Path $edge)) {
      # KICAD im Edge (CDP, werkzeuge/seitenbild.mjs): "msedge --screenshot --virtual-time-budget" schrieb hier kein Bild, die Oberflaeche fragt laufend den Server
      $foto = $true
      $bild = Join-Path $Pruefbilder 'desktop_7_anzeige_pc.png'
      $ep = Start-Process -FilePath 'node' -ArgumentList @("`"$repo\werkzeuge\seitenbild.mjs`"", "$basis/", "`"$bild`"", '6000', '1600', '950') -PassThru -WindowStyle Hidden
      if (-not $ep.WaitForExit(40000)) { Stop-Process -Id $ep.Id -Force -ErrorAction SilentlyContinue }
      if (-not (Test-Path $bild)) { Aus 'Lauf 4: kein Bild der Anzeige am PC (werkzeuge/seitenbild.mjs).' }
    }
    Start-Sleep -Milliseconds 250
  }
  $ap.WaitForExit()
  $a4 = Get-Content "$logs\lauf4.log" -Encoding UTF8
  $a4 | Where-Object { "$_" -match '^P4 (ok|Fehler|Laden)' } | ForEach-Object { Aus "  $_" }
  if ($gesehen) {
    $s = @($gesehen.stroeme)[0]
    Aus ("Lauf 4: Anzeige am PC: Brille {0} sieht {1} ({2} x {3}), {4} Bilder/s, {5} kbit/s, erfassen {6} ms, kodieren {7} ms, Bildrate {8} / ruhig {9} - ok" -f $s.name, $s.monitorName, $s.breite, $s.hoehe, $s.bilderJeSekunde, $s.kbitJeSekunde, $s.erfassMs, $s.kodierMs, $s.bildrate, $s.ruheBildrate)
  } else { Aus 'Lauf 4: KICAD hat keine mitsehende Brille angezeigt (GET /api/xr/desktop/zustand) - FEHLT' }
  if ($ap.ExitCode -ne 0) { Aus "Lauf 4: die App endete mit $($ap.ExitCode)." }
  return ($ap.ExitCode -eq 0) -and [bool]$gesehen
}

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
  if ($NurDesktop) { $ergebnis = if (DesktopLauf) { 0 } else { 1 } } else {   # V1c: nur Lauf 4

  # VR-Auftrag wie aus dem Kontextmenue eines Raums (Id des ersten Raumkoerpers): Lauf 1 muss in diesem Raum starten (Auftrag=Nr 1 ...)
  $koerper = (Invoke-WebRequest -UseBasicParsing -Uri "$basis/api/koerper" -TimeoutSec 300).Content
  $treffer = [regex]::Match($koerper, '"id":"([^"]+)","ebene":"raum"')
  if ($treffer.Success) {
    Post '/api/vr/auftrag' @{ modus = 'begehen'; raum = $treffer.Groups[1].Value } | Out-Null
    Aus 'VR-Auftrag gesetzt: begehen, erster Raum des Modells.'
  } else { Aus 'Kein Raum fuer einen VR-Auftrag gefunden.' }

  $zusatz = @()
  if ($OhneUnterlage) { $zusatz += '--ohne-unterlage' }
  $muster = '^P4 (ok|Fehler|Laden)|Modell |Start |Netzzugang gibt|Auftrag vom PC|Ebenen |Passthrough|Katalog des Labors|Messprotokoll '
  $t0 = Laeufe 'teleport-markierung'; $d0 = Laeufe 'drehen-90'; $z0 = Laeufe 'modell-greifen-zoomen'; $w0 = Laeufe 'messen-wand-wand'; $m0 = MarkerZahl
  Aus "Lauf 1: mit PC ($Bilder Bilder) ..."
  # P5a: mit -Pruefbilder auch zwei Bilder beim Begehen (Blick nach vorn, die schwebenden Apps)
  $bild1 = @()
  if ($Pruefbilder) { $bild1 = @('--bilder', $Pruefbilder) }
  $a1 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung.json" --speicher "$tmp\app" --modell-lokal $port @zusatz @bild1 2>&1
  $c1 = $LASTEXITCODE
  $a1 | Out-File -FilePath "$logs\lauf1.log" -Encoding utf8
  $a1 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
  # P5b: die Messprotokolle der Pruefaufgaben von Lauf 1 sind am PC angekommen
  $t1 = Laeufe 'teleport-markierung'; $d1 = Laeufe 'drehen-90'; $w1 = Laeufe 'messen-wand-wand'
  $laborOk = ($t1 -ge $t0 + 1) -and ($d1 -ge $d0 + 1)
  Aus ("Lauf 1: Messprotokolle am PC: teleport-markierung {0} -> {1}, drehen-90 {2} -> {3}, messen-wand-wand {4} -> {5} - {6}" -f $t0, $t1, $d0, $d1, $w0, $w1, @('FEHLT', 'ok')[[int]$laborOk])
  # V1a: der Problem-Marker von Lauf 1 ist im Projekt am PC (Element ProblemMarker)
  $m1 = MarkerZahl
  $markerOk = $m1 -ge $m0 + 1
  Aus ("Lauf 1: Problem-Marker am PC: {0} -> {1} - {2}" -f $m0, $m1, @('FEHLT', 'ok')[[int]$markerOk])
  $v1aOk = [bool]($a1 -match '^P4 ok: .*V1a: Glas=ok')
  if (-not $v1aOk) { Aus 'Lauf 1: die Zeile "V1a" fehlt.' }
  if ($a1 -match 'messen-wand-wand ohne Wandpaar') { Aus 'Lauf 1: messen-wand-wand ohne Wandpaar (am Startpunkt kein Raum mit zwei gegenueberliegenden Waenden) - nicht geprueft.' }
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
    # P5b: das Messprotokoll von "modell-greifen-zoomen" ist am PC angekommen
    $z1 = Laeufe 'modell-greifen-zoomen'
    $zoomOk = $z1 -ge $z0 + 1
    Aus ("Lauf 3: Messprotokolle am PC: modell-greifen-zoomen {0} -> {1} - {2}" -f $z0, $z1, @('FEHLT', 'ok')[[int]$zoomOk])
    if (-not $zoomOk) { $laborOk = $false }
    # V1a: der wartende Marker aus Lauf 2 (ohne PC) und der am Tisch aus Lauf 3 sind angekommen
    $m3 = MarkerZahl
    $marker3Ok = $m3 -ge $m1 + 2
    Aus ("Lauf 3: Problem-Marker am PC: {0} -> {1} (wartender aus Lauf 2 und einer am Tisch) - {2}" -f $m1, $m3, @('FEHLT', 'ok')[[int]$marker3Ok])
    if (-not $marker3Ok) { $markerOk = $false }
    # P4b-Nachtrag: dasselbe Bild mit gewoehnlichem Material und ganz mit dem Shader "schnitt" - die hellen Pixel (Modell) muessen gleich hell sein
    $vi = ''; $vs = ''
    if ($Pruefbilder) { $vi = Join-Path $Pruefbilder 'vergleich_innen.png'; $vs = Join-Path $Pruefbilder 'vergleich_schnitt.png' }
    if ($Pruefbilder -and (Test-Path $vi) -and (Test-Path $vs)) {
      Add-Type -AssemblyName System.Drawing
      $ba = [Drawing.Bitmap]::FromFile($vi); $bb = [Drawing.Bitmap]::FromFile($vs)
      $ha = 0.0; $hb = 0.0; $nh = 0
      for ($y = 0; $y -lt $ba.Height; $y += 2) { for ($x = 0; $x -lt $ba.Width; $x += 2) {
        $p = $ba.GetPixel($x, $y); $q = $bb.GetPixel($x, $y); $la = ($p.R + $p.G + $p.B) / 3.0
        if ($la -gt 60) { $ha += $la; $hb += ($q.R + $q.G + $q.B) / 3.0; $nh++ } } }
      $ba.Dispose(); $bb.Dispose()
      if ($nh -gt 0) {
        $hellOk = [Math]::Abs($ha / $nh - $hb / $nh) -le 3
        Aus ("Lauf 3: Helligkeit Modell gewoehnlich {0:0.0}, mit Shader schnitt {1:0.0} ({2} Pixel) - {3}" -f ($ha / $nh), ($hb / $nh), $nh, @('ABWEICHUNG', 'gleich')[[int]$hellOk])
        if (-not $hellOk) { $sitzungOk = $false }
      }
    }
  }
  $ergebnis = if ($c1 -eq 0 -and $c2 -eq 0 -and $c3 -eq 0 -and $auftragOk -and $sitzungOk -and $laborOk -and $markerOk -and $v1aOk) { 0 } else { 1 }
  if (-not $OhneDesktop -and -not (DesktopLauf)) { $ergebnis = 1 }   # V1c: Lauf 4
  }   # V1c: Ende von "nicht nur Lauf 4"
} catch {
  Aus ('Fehler: ' + $_.Exception.Message)
  $ergebnis = 2
} finally {
  if ($kopplung) { try { Post '/api/xr/kopplung/widerrufen' @{ kopplung = $kopplung } | Out-Null } catch { } }
  if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 500 }
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
  foreach ($v in 'HLKS_TEST', 'HLKS_BENUTZER', 'HLKS_ZULETZT', 'HLKS_WEBROOT', 'HLKS_KATALOG', 'HLKS_XR_DESKTOP_PORT') { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
}
Aus ("Ergebnis: " + @('ok', 'nicht ok', 'Fehler im Ablauf')[$ergebnis] + " (Protokolle: $logs)")
exit $ergebnis

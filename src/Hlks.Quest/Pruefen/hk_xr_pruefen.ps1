# H+K XR E1 (entwurf/xr-empfaenger.md, Nachweis E1): Prueflaeufe der App "H+K XR" am PC im Simulator mit Werkzeugen im Testlauf - nie gegen die echte Brille,
# nie mit echten Projektdaten. Die KICAD-Laeufe (Begehen, ohne PC, Sitzungsraum, Desktop) macht weiter werkzeuge\quest_pruefen.ps1.
#   -Lauf Labor    XR-Labor-Server (src\Hlks.Labor, Testraum) im Testlauf, Kopplung "xrlabor", die App nur mit dieser Kopplung (startet direkt im Labor):
#                  Lauf L1 Begehen (Pruefaufgaben im Testraum: messen-wand-wand, drehen-90, etage-wechseln, tablet-wert-ablesen, teleport-markierung) und
#                  Lauf L2 Sitzungsraum (--auftrag sitzungsraum testraum-eg: raum-ar-oeffnen, modell-greifen-zoomen). Erwartet je "P4 ok" mit "Testraum:" und am
#                  Labor-PC die Messprotokolle der sieben Aufgaben (GET /api/xr/labor/auswertung).
#   -Lauf Wechsel  KICAD-Testserver (Beispielprojekt MFH Musterweg) und Labor-Server, je eine Kopplung, die App mit beiden (--wechsel, QuestApp.PruefenWechsel.cs):
#                  Startseite -> KICAD waehlen -> das Skript schickt "labor.aufgabe" (drehen-90) an den inaktiven Kanal des Labors -> die App wechselt ins Labor
#                  -> ueber "Werkzeuge" zurueck zu KICAD. Das Skript prueft dazwischen am PC die Teilnehmer (aktiv ja/nein, Posen) und am Ende das Messprotokoll
#                  der abgebrochenen Aufgabe am Labor-PC.
#   -Lauf Spielwiese  XR-Labor S1: Labor-Server, Kopplung "xrlabor", die App mit --spielwiese (QuestApp.PruefenSpielwiese.cs): Platzhalter-Apps vom Labor-PC,
#                  Hebegeste, Dokument waehlen, blaettern, wegwischen, Liste waehlen und rollen, Hebegeste bei offener App (V1i), die uebrigen Apps offen; auf die
#                  Zeile "P4 Spielwiese: Stellschraube bereit" setzt das Skript am Labor-PC symbole.abstand_m = 0.7 (POST /api/xr/stellschraube).
# Pruefbilder (-Pruefbilder, Vorgabe %USERPROFILE%\HLKS_CAD_testdaten\hk_xr\e1; nie ins Repository): Labor: labor\testraum_*.jpg, aufgabe_karte.jpg ...;
# Wechsel: startseite.jpg, wechsel_meldung.jpg, startseite_zurueck.jpg. Spielwiese: Vorgabe %USERPROFILE%\HLKS_CAD_testdaten\hk_xr\s1 (mit -Pruefbilder dessen
# Unterordner s1): symbolfeld.jpg, app_*.jpg, dokument_seite2.jpg, liste_gerollt.jpg, nach_wegwischen.jpg, hebe_bei_offener_app.jpg, stellschraube_vorher/nachher.jpg.
# Aufruf (Repository-Wurzel): powershell -ExecutionPolicy Bypass -File src\Hlks.Quest\Pruefen\hk_xr_pruefen.ps1 [-Lauf Labor|Wechsel|Spielwiese|Alle] [-Bilder 300]
#   [-Pruefbilder <ordner>] [-OhneBau]
# Exit 0 = alle Laeufe ok, 1 = ein Lauf nicht ok, 2 = Fehler im Ablauf. Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [ValidateSet('Labor', 'Wechsel', 'Spielwiese', 'Alle')][string]$Lauf = 'Alle',
  [int]$Bilder = 300,
  [string]$Pruefbilder = "$env:USERPROFILE\HLKS_CAD_testdaten\hk_xr\e1",
  [switch]$OhneBau
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not $env:HLKS_BUILD) { $env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build" }
$bau = $env:HLKS_BUILD
$logs = "$bau\hk_xr_pruefen"
New-Item -ItemType Directory -Force $logs | Out-Null
function Aus([string]$t) { $z = "{0:HH:mm:ss} {1}" -f (Get-Date), $t; Write-Host $z; Add-Content -Path "$logs\protokoll.log" -Value $z -Encoding UTF8 }
function Belegt([int]$p) { [bool](Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction SilentlyContinue) }
function FreierPort([int[]]$kandidaten) { foreach ($p in $kandidaten) { if (-not (Belegt $p)) { return $p } }; return 0 }
if ($Pruefbilder -and $Pruefbilder.ToLower().StartsWith($repo.ToLower())) { Aus 'Pruefbilder nie im Repository.'; exit 2 }
$sBilder = if ($PSBoundParameters.ContainsKey('Pruefbilder')) { Join-Path $Pruefbilder 's1' } else { "$env:USERPROFILE\HLKS_CAD_testdaten\hk_xr\s1" }

if (-not $OhneBau) {
  Aus "Baue Hlks.Web, Hlks.Labor und Hlks.Quest ($bau) ..."
  foreach ($p in "src\Hlks.Web\Hlks.Web.csproj", "src\Hlks.Labor\Hlks.Labor.csproj") {
    & $dotnet build "$repo\$p" -v q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Aus "Bau $p fehlgeschlagen"; exit 2 }
  }
  & $dotnet build "$repo\src\Hlks.Quest\Hlks.Quest.csproj" -c Release -v q -nologo | Out-Null
  if ($LASTEXITCODE -ne 0) { Aus 'Bau Hlks.Quest fehlgeschlagen'; exit 2 }
}
$web = "$bau\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$laborDll = "$bau\Hlks.Labor\bin\Debug\net10.0\Hlks.Labor.dll"
$quest = "$bau\Hlks.Quest\bin\Release\net10.0\Hlks.Quest.dll"
foreach ($d in $web, $laborDll, $quest) { if (-not (Test-Path $d)) { Aus "Build fehlt: $d"; exit 2 } }

# Ports der Testserver: nie die des Desktops (KICAD 5130, Labor 5430) und nie die Netzzugaenge (5140/5141, 5340/5341)
$kport = FreierPort @(5139, 5138, 5137, 5136, 5135, 5134, 5133, 5132)
$lport = FreierPort @(5439, 5438, 5437, 5436, 5435, 5434, 5433, 5432)
if ($kport -eq 0 -or $lport -eq 0) { Aus 'Kein freier Port fuer die Testserver (KICAD 5132-5139, Labor 5432-5439).'; exit 2 }

$tmp = Join-Path $env:TEMP ('hlks_hk_xr_pruefen_' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force "$tmp\benutzer", "$tmp\zuletzt", "$tmp\labor_daten" | Out-Null
$env:HLKS_TEST = '1'
$env:HLKS_BENUTZER = "$tmp\benutzer"
$env:HLKS_ZULETZT = "$tmp\zuletzt"
$env:HLKS_WEBROOT = "$repo\src\Hlks.Web\wwwroot"
$env:HLKS_KATALOG = "$repo\kataloge"
$env:HLKS_LABOR_DATEN = "$tmp\labor_daten"
$env:HLKS_XR_DESKTOP_PORT = "$kport"      # Netzzugang von KICAD nur auf dem Port des Testservers (Testlauf)
$env:HLKS_LABOR_DESKTOP_PORT = "$lport"   # ebenso fuer das Labor
$utf8 = New-Object System.Text.UTF8Encoding $false

function Post([string]$basis, [string]$pfad, $koerper) {
  $json = $koerper | ConvertTo-Json -Depth 8 -Compress
  $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$basis$pfad" -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($json) -TimeoutSec 300
  return $r.Content
}
function Get([string]$basis, [string]$pfad) { (Invoke-WebRequest -UseBasicParsing -Uri "$basis$pfad" -TimeoutSec 60).Content | ConvertFrom-Json }
function Warten([string]$basis, $prozess, [string]$name) {
  $bis = (Get-Date).AddSeconds(90)
  while ($true) {
    try { Get $basis '/api/status' | Out-Null; return } catch { }
    if ((Get-Date) -gt $bis -or $prozess.HasExited) { throw "$name antwortet nicht." }
    Start-Sleep -Milliseconds 300
  }
}
# Netzzugang ein, Kopplungspaket (Fassung 2 mit Werkzeug) unter dem Namen seines Eingangs kopplung_<werkzeug>.json ablegen; Rueckgabe: Kennung der Kopplung
function Koppeln([string]$basis, [string]$werkzeug) {
  Post $basis '/api/xr/zugang' @{ an = $true } | Out-Null
  $bis = (Get-Date).AddSeconds(30)
  while (-not (Get $basis '/api/xr/zugang').aktiv) {
    if ((Get-Date) -gt $bis) { throw "Der Netzzugang ($werkzeug) startet nicht." }
    Start-Sleep -Milliseconds 200
  }
  $inhalt = Post $basis '/api/xr/kopplung/paket' @{ brille = 'PC-PRUEFUNG'; name = 'Pruefung am PC' }
  $p = ($inhalt | ConvertFrom-Json).paket
  if ($p.werkzeug -ne $werkzeug) { throw "Das Paket nennt das Werkzeug '$($p.werkzeug)' statt '$werkzeug' (Fassung $($p.fassung))." }
  [IO.File]::WriteAllText("$tmp\kopplung_$werkzeug.json", $inhalt, $utf8)
  Aus "Gekoppelt mit $werkzeug ($basis): Fassung $($p.fassung), Port $($p.port), Kopplung $($p.kopplung)"
  return $p.kopplung
}
function Laeufe([string]$basis, [string]$id) {
  try { $a = Get $basis '/api/xr/labor/auswertung'; $z = $a.aufgaben | Where-Object { $_.aufgabe -eq $id } | Select-Object -First 1; if ($z -and $z.laeufe) { return [int]$z.laeufe } } catch { }
  return 0
}
# Teilnehmer der App an einem Server (der mit marke = werkzeug): { aktiv, pose }
function Teilnehmer([string]$basis, [string]$werkzeug) {
  try { return (Get $basis '/api/xr/teilnehmer').teilnehmer | Where-Object { $_.marke -eq $werkzeug } | Select-Object -First 1 } catch { return $null }
}
function TeilnehmerText($t) { if (-not $t) { return 'nicht verbunden' }; return ('aktiv={0}, Pose={1}' -f $t.aktiv, [bool]$t.pose) }
# Ausgabe der laufenden App lesen (Start-Process haelt die Datei offen: nur mit geteiltem Zugriff lesbar)
function LogLesen([string]$datei) {
  try {
    $fs = [IO.File]::Open($datei, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try { return (New-Object IO.StreamReader($fs, [Text.Encoding]::UTF8)).ReadToEnd() } finally { $fs.Dispose() }
  } catch { return '' }
}

$laborServer = $null; $kicadServer = $null; $kicadKopplung = $null; $laborKopplung = $null
$lbasis = "http://localhost:$lport"; $kbasis = "http://localhost:$kport"
$ergebnis = 2
$muster = '^P4 (ok|Fehler|Wechsel|Spielwiese)|Werkzeug |Startseite|Wechsel zu|Testraum mit|Katalog des Labors|Spielwiese (an|aus|pc|vorgabe|zwischen)'
try {
  Aus "Starte Labor-Testserver auf Port $lport ..."
  $laborServer = Start-Process -FilePath $dotnet -ArgumentList @("`"$laborDll`"", '--port', "$lport") -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput "$logs\labor_server.log" -RedirectStandardError "$logs\labor_server_fehler.log"
  Warten $lbasis $laborServer 'Der Labor-Testserver'
  $laborKopplung = Koppeln $lbasis 'xrlabor'
  $alleOk = $true

  if ($Lauf -in 'Labor', 'Alle') {
    $ids = 'messen-wand-wand', 'drehen-90', 'etage-wechseln', 'tablet-wert-ablesen', 'teleport-markierung', 'raum-ar-oeffnen', 'modell-greifen-zoomen'
    $vorher = @{}; foreach ($id in $ids) { $vorher[$id] = Laeufe $lbasis $id }
    $bl = Join-Path $Pruefbilder 'labor'
    New-Item -ItemType Directory -Force $bl | Out-Null
    Aus "Lauf L1: Labor, Begehen im Testraum ($Bilder Bilder) ..."
    $l1 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung_xrlabor.json" --speicher "$tmp\app_labor" --bilder $bl 2>&1
    $c1 = $LASTEXITCODE
    $l1 | Out-File -FilePath "$logs\labor_l1.log" -Encoding utf8
    $l1 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
    $l1ok = ($c1 -eq 0) -and [bool]($l1 -match '^P4 ok: .*Testraum: ') -and -not [bool]($l1 -match '^P4 ok: .*FEHLER')
    Aus "Lauf L2: Labor, Sitzungsraum im Testraum ($Bilder Bilder, Auftrag sitzungsraum testraum-eg) ..."
    $l2 = & $dotnet $quest --pruefen $Bilder --kopplung "$tmp\kopplung_xrlabor.json" --speicher "$tmp\app_labor" --bilder $bl `
      --auftrag '{\"modus\":\"sitzungsraum\",\"raum\":\"testraum-eg\",\"nr\":1}' 2>&1
    $c2 = $LASTEXITCODE
    $l2 | Out-File -FilePath "$logs\labor_l2.log" -Encoding utf8
    $l2 | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
    $l2ok = ($c2 -eq 0) -and [bool]($l2 -match 'Sitzungsraum=ja') -and [bool]($l2 -match 'raum-ar-oeffnen Erfolg')
    $n = 0; $text = @()
    foreach ($id in $ids) { $j = Laeufe $lbasis $id; if ($j -gt $vorher[$id]) { $n++ }; $text += ('{0} {1}->{2}' -f $id, $vorher[$id], $j) }
    Aus ("Labor: Messprotokolle am Labor-PC fuer {0}/7 Aufgaben ({1})" -f $n, ($text -join ', '))
    $laborOk = $l1ok -and $l2ok -and ($n -eq 7)
    Aus ("Lauf Labor: L1 {0}, L2 {1}, Aufgaben {2}/7 - {3}" -f @('FEHLER', 'ok')[[int]$l1ok], @('FEHLER', 'ok')[[int]$l2ok], $n, @('NICHT OK', 'ok')[[int]$laborOk])
    if (-not $laborOk) { $alleOk = $false }
  }

  if ($Lauf -in 'Spielwiese', 'Alle') {
    New-Item -ItemType Directory -Force $sBilder | Out-Null
    $sStart = Get-Date
    Aus 'Lauf S: Spielwiese (Platzhalter-Apps, Hebegeste, waehlen, blaettern, rollen, wegwischen, Stellschraube live) ...'
    $sArg = @("`"$quest`"", '--pruefen', '5000', '--spielwiese', '--kopplung', "`"$tmp\kopplung_xrlabor.json`"", '--speicher', "`"$tmp\app_spielwiese`"",
      '--bilder', "`"$sBilder`"")
    $slog = "$logs\spielwiese.log"
    $sp = Start-Process -FilePath $dotnet -ArgumentList $sArg -PassThru -NoNewWindow -RedirectStandardOutput $slog -RedirectStandardError "$logs\spielwiese_fehler.log"
    $null = $sp.Handle
    $gesetzt = $false
    $bis = (Get-Date).AddMinutes(12)
    while (-not $sp.HasExited -and (Get-Date) -lt $bis) {
      Start-Sleep -Milliseconds 400
      if ($gesetzt) { continue }
      $inhalt = LogLesen $slog
      if ($inhalt -match 'P4 Spielwiese: Stellschraube bereit') {
        try {
          $r = Post $lbasis '/api/xr/stellschraube' @{ schluessel = 'symbole.abstand_m'; wert = 0.7 } | ConvertFrom-Json
          $gesetzt = $true
          Aus "  Labor-PC: symbole.abstand_m = 0.7 an $($r.gesendet) Teilnehmer gesendet (live)."
        } catch { Aus ('  Labor-PC: Stellschraube nicht gesendet: ' + $_.Exception.Message) }
      }
    }
    if (-not $sp.HasExited) { Aus 'Lauf S: die App endete nicht in 12 Minuten.'; Stop-Process -Id $sp.Id -Force -ErrorAction SilentlyContinue }
    $sp.WaitForExit()
    $s = (LogLesen $slog) -split "`r?`n"
    $s | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
    $neu = @(Get-ChildItem -Path $sBilder -Filter '*.jpg' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $sStart }).Count
    $sOk = ($sp.ExitCode -eq 0) -and [bool]($s -match '^P4 ok: Spielwiese') -and -not [bool]($s -match '^P4 ok: .*FEHLER') -and $gesetzt
    Aus ("Lauf Spielwiese: App {0}, Stellschraube vom Labor-PC {1}, Pruefbilder {2} in {3} - {4}" -f $sp.ExitCode, @('nein', 'ja')[[int]$gesetzt], $neu, $sBilder,
      @('NICHT OK', 'ok')[[int]$sOk])
    if (-not $sOk) { $alleOk = $false }
  }

  if ($Lauf -in 'Wechsel', 'Alle') {
    Aus "Starte KICAD-Testserver auf Port $kport ..."
    $kicadServer = Start-Process -FilePath $dotnet -ArgumentList @("`"$web`"", '--port', "$kport", '--geraet', 'hkxrpruefung', '--akteur', 'pruefung') -PassThru -WindowStyle Hidden `
      -RedirectStandardOutput "$logs\kicad_server.log" -RedirectStandardError "$logs\kicad_server_fehler.log"
    Warten $kbasis $kicadServer 'Der KICAD-Testserver'
    Post $kbasis '/api/projekt/anlegen' @{ ordner = "$tmp\projekt\e_KICAD"; name = 'MFH Musterweg'; beispiel = $true } | Out-Null
    Aus 'Beispielprojekt MFH Musterweg angelegt.'
    $kicadKopplung = Koppeln $kbasis 'kicad'
    $d0 = Laeufe $lbasis 'drehen-90'
    New-Item -ItemType Directory -Force $Pruefbilder | Out-Null
    Aus 'Lauf W: Wechsel KICAD <-> Labor (Startseite, Auftrag vom Labor, zurueck ueber Werkzeuge) ...'
    $wArg = @("`"$quest`"", '--pruefen', '5000', '--wechsel', '--kopplung', "`"$tmp\kopplung_kicad.json`"", '--kopplung', "`"$tmp\kopplung_xrlabor.json`"",
      '--speicher', "`"$tmp\app_wechsel`"", '--modell-lokal', "$kport", '--bilder', "`"$Pruefbilder`"")
    $wlog = "$logs\wechsel.log"
    $ap = Start-Process -FilePath $dotnet -ArgumentList $wArg -PassThru -NoNewWindow -RedirectStandardOutput $wlog -RedirectStandardError "$logs\wechsel_fehler.log"
    $null = $ap.Handle
    $gesendet = $false; $pc = @()
    $stufen = @{ kicad = $false; labor = $false; zurueck = $false }
    $bis = (Get-Date).AddMinutes(12)
    while (-not $ap.HasExited -and (Get-Date) -lt $bis) {
      Start-Sleep -Milliseconds 400
      $inhalt = LogLesen $wlog
      if (-not $inhalt) { continue }
      if (-not $stufen.kicad -and $inhalt -match 'P4 Wechsel: KICAD bereit') {
        $stufen.kicad = $true
        $tk = Teilnehmer $kbasis 'kicad'; $tl = Teilnehmer $lbasis 'xrlabor'
        $ok = $tk -and $tk.aktiv -and $tk.pose -and $tl -and (-not $tl.aktiv) -and (-not $tl.pose)
        $pc += ('KICAD bereit: KICAD {0}, Labor {1} - {2}' -f (TeilnehmerText $tk), (TeilnehmerText $tl), @('FEHLER', 'ok')[[int][bool]$ok])
        Aus "  PC: $($pc[-1])"
        try {
          $r = Post $lbasis '/api/xr/labor/aufgabe' @{ aufgabe = 'drehen-90' } | ConvertFrom-Json
          $gesendet = $true
          Aus "  Labor: Aufgabe drehen-90 an $($r.gesendet) Teilnehmer gesendet (an den inaktiven Kanal)."
        } catch { Aus ('  Labor: Aufgabe nicht gesendet: ' + $_.Exception.Message) }
      }
      if (-not $stufen.labor -and $inhalt -match 'P4 Wechsel: Labor bereit') {
        $stufen.labor = $true
        $tk = Teilnehmer $kbasis 'kicad'; $tl = Teilnehmer $lbasis 'xrlabor'
        $ok = $tl -and $tl.aktiv -and $tl.pose -and $tk -and (-not $tk.aktiv)
        $pc += ('Labor bereit: KICAD {0}, Labor {1} - {2}' -f (TeilnehmerText $tk), (TeilnehmerText $tl), @('FEHLER', 'ok')[[int][bool]$ok])
        Aus "  PC: $($pc[-1])"
      }
      if (-not $stufen.zurueck -and $inhalt -match 'P4 Wechsel: zur') {
        $stufen.zurueck = $true
        $tk = Teilnehmer $kbasis 'kicad'; $tl = Teilnehmer $lbasis 'xrlabor'
        $ok = $tk -and $tk.aktiv -and $tl -and (-not $tl.aktiv)
        $pc += ('zurueck bei KICAD: KICAD {0}, Labor {1} - {2}' -f (TeilnehmerText $tk), (TeilnehmerText $tl), @('FEHLER', 'ok')[[int][bool]$ok])
        Aus "  PC: $($pc[-1])"
      }
    }
    if (-not $ap.HasExited) { Aus 'Lauf W: die App endete nicht in 12 Minuten.'; Stop-Process -Id $ap.Id -Force -ErrorAction SilentlyContinue }
    $ap.WaitForExit()
    $w = (LogLesen $wlog) -split "`r?`n"
    $w | Where-Object { "$_" -match $muster } | ForEach-Object { Aus "  $_" }
    $d1 = Laeufe $lbasis 'drehen-90'
    $protokollOk = $d1 -ge $d0 + 1
    Aus ("Lauf W: Messprotokoll der abgebrochenen Aufgabe am Labor-PC: drehen-90 {0} -> {1} - {2}" -f $d0, $d1, @('FEHLT', 'ok')[[int]$protokollOk])
    $wOk = ($ap.ExitCode -eq 0) -and [bool]($w -match '^P4 ok: Wechsel') -and $gesendet -and $stufen.kicad -and $stufen.labor -and $stufen.zurueck `
      -and -not ($pc -match 'FEHLER') -and $protokollOk
    Aus ("Lauf Wechsel: App {0}, PC-Pruefungen {1}/3 - {2}" -f $ap.ExitCode, @($pc | Where-Object { $_ -notmatch 'FEHLER' }).Count, @('NICHT OK', 'ok')[[int]$wOk])
    if (-not $wOk) { $alleOk = $false }
  }
  $ergebnis = if ($alleOk) { 0 } else { 1 }
} catch {
  Aus ('Fehler: ' + $_.Exception.Message)
  $ergebnis = 2
} finally {
  if ($laborKopplung) { try { Post $lbasis '/api/xr/kopplung/widerrufen' @{ kopplung = $laborKopplung } | Out-Null } catch { } }
  if ($kicadKopplung) { try { Post $kbasis '/api/xr/kopplung/widerrufen' @{ kopplung = $kicadKopplung } | Out-Null } catch { } }
  foreach ($s in $laborServer, $kicadServer) { if ($s -and -not $s.HasExited) { Stop-Process -Id $s.Id -Force -ErrorAction SilentlyContinue } }
  Start-Sleep -Milliseconds 500
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
  foreach ($v in 'HLKS_TEST', 'HLKS_BENUTZER', 'HLKS_ZULETZT', 'HLKS_WEBROOT', 'HLKS_KATALOG', 'HLKS_LABOR_DATEN', 'HLKS_XR_DESKTOP_PORT', 'HLKS_LABOR_DESKTOP_PORT') {
    Remove-Item "Env:$v" -ErrorAction SilentlyContinue
  }
}
Aus ("Ergebnis: " + @('ok', 'nicht ok', 'Fehler im Ablauf')[$ergebnis] + " (Protokolle: $logs)")
exit $ergebnis

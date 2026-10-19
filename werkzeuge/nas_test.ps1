# NAS-Test HLKS-CAD (Modul 0b): misst auf der echten Projektablage
#   - Anlegen des Beispielprojekts (Kommandozeile) und Oeffnen mit bzw. ohne Standabbild (Arbeitsplatz A, REST),
#   - wie lange eine Aenderung von Arbeitsplatz B (Kommandozeile Hlks.Mcp, eigener Prozess, wie KI oder zweiter PC)
#     braucht, bis Arbeitsplatz A (Hlks.Web) sie zeigt  -> Abnahme-Grenze 3 s,
#   - ob der Dateiwaechter auf dem Laufwerk meldet oder nur der Takt (2 s) greift,
#   - den Reservierungskonflikt ueber die Ablage (A reserviert, B abgelehnt, nach Freigeben angenommen),
#   - Schliessen (Reservierungen frei).
# Ergebnis als Tabelle und als JSON nach berichte\nas-test-<zeit>.json.
#
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\nas_test.ps1 [-Ordner <e_KICAD-Ordner>] [-Aufraeumen]
#         [-Vorhandenes] [-Wiederholungen 5] [-Port 5132] [-Bauen]
#   -Ordner        Ordner e_KICAD des Testprojekts (Vorgabe: Testprojekt auf W:)
#   -Aufraeumen    am Ende die Projektdateien loeschen - nur die dieses Laufs (bzw. mit -Vorhandenes uebernommene) und
#                  nur, wenn dort das Beispielprojekt liegt; Elternordner bleiben stehen
#   -Vorhandenes   ein schon vorhandenes Beispielprojekt im Ordner verwenden statt abzubrechen
# Datei bewusst nur ASCII (PowerShell 5.1); das ae im Standardpfad entsteht ueber [char]0x00E4.

param(
  [string]$Ordner = ('W:\02_Projektdateien Objekte\_Test HLKS-CAD\c_Pl' + [char]0x00E4 + 'ne und Modelle\e_KICAD'),
  [switch]$Aufraeumen,
  [switch]$Vorhandenes,
  [int]$Wiederholungen = 5,
  [int]$Port = 5132,
  [switch]$Bauen
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$basis = if ($env:HLKS_BUILD) { $env:HLKS_BUILD } else { "$env:USERPROFILE\HLKS_CAD_build" }
$webDll = "$basis\Hlks.Web\bin\Debug\net10.0\Hlks.Web.dll"
$mcpDll = "$basis\Hlks.Mcp\bin\Debug\net10.0\Hlks.Mcp.dll"
$url = "http://localhost:$Port"
$beispielName = 'MFH Musterweg (Beispiel, fiktiv)'
$Ordner = $Ordner.Trim().TrimEnd('\')
$startZeit = Get-Date
$stempel = $startZeit.ToString('yyyyMMdd-HHmmss')
$arbeit = Join-Path $env:TEMP "hlks_nas_test_$stempel"
New-Item -ItemType Directory -Force $arbeit | Out-Null

$zeilen = New-Object System.Collections.ArrayList
$ergebnis = [ordered]@{}
function Zeile([string]$messung, $wert, [string]$einheit = '', [string]$bemerkung = '') {
  [void]$zeilen.Add([pscustomobject]@{ Messung = $messung; Wert = $wert; Einheit = $einheit; Bemerkung = $bemerkung })
}
function Schritt([string]$text) { Write-Host ("[{0}] {1}" -f (Get-Date).ToString('HH:mm:ss'), $text) }

# ------------------------------------------------------------------ Hilfen: HTTP (ohne Proxy), Kommandozeile, Zeit

Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.UseProxy = $false
$http = New-Object System.Net.Http.HttpClient($handler)
$http.Timeout = [TimeSpan]::FromSeconds(180)

function Antwort($r) {
  $text = $r.Content.ReadAsStringAsync().Result
  $json = $null
  if ($text) { try { $json = ConvertFrom-Json $text } catch { $json = $null } }
  return [pscustomobject]@{ Code = [int]$r.StatusCode; Json = $json; Text = $text }
}
function ApiGet([string]$pfad) { return Antwort ($http.GetAsync("$url$pfad").Result) }
function ApiPost([string]$pfad, [string]$json) {
  $inhalt = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, 'application/json')
  return Antwort ($http.PostAsync("$url$pfad", $inhalt).Result)
}
function JsonText($objekt) { return (ConvertTo-Json $objekt -Compress -Depth 5) }

function PortFrei([int]$p) {
  $c = New-Object System.Net.Sockets.TcpClient
  try { $c.Connect('127.0.0.1', $p); return $false } catch { return $true } finally { $c.Close() }
}

# Einmal-Modus der Kommandozeile; Ausgabe UTF-8 auf stdout (auch Fehler)
function HlksCli([string[]]$argumente) {
  $uhr = [Diagnostics.Stopwatch]::StartNew()
  $aus = (& $dotnet $mcpDll @argumente | Out-String)
  $code = $LASTEXITCODE
  $uhr.Stop()
  $json = $null
  try { $json = ConvertFrom-Json $aus } catch { $json = $null }
  return [pscustomobject]@{ Code = $code; Text = $aus.Trim(); Json = $json; Ms = $uhr.ElapsedMilliseconds; Ende = [DateTime]::UtcNow }
}

function ParameterDatei([string]$name, $objekt) {
  $datei = Join-Path $arbeit "$name.json"
  [System.IO.File]::WriteAllText($datei, (JsonText $objekt), (New-Object System.Text.UTF8Encoding($false)))
  return $datei
}

# Zeit eines Pakets aus seiner Id (UTC, ms), gesetzt unmittelbar bevor das Paket geschrieben wird
function PaketZeit([string]$id) {
  $stil = [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal
  return [DateTime]::ParseExact($id.Substring(0, 19), "yyyyMMdd'T'HHmmssfff'Z'", [Globalization.CultureInfo]::InvariantCulture, $stil)
}

# Fragt A ab, bis das Element den erwarteten Namen hat; liefert die Zeit (UTC) oder $null
function WartenAufName([string]$kennzeichen, [string]$name, [int]$grenzeMs = 30000) {
  $uhr = [Diagnostics.Stopwatch]::StartNew()
  $koerper = '{"ids":["' + $kennzeichen + '"]}'
  while ($uhr.ElapsedMilliseconds -lt $grenzeMs) {
    $r = ApiPost '/api/elemente' $koerper
    if ($r.Code -eq 200 -and $r.Json -and @($r.Json.elemente).Count -gt 0 -and @($r.Json.elemente)[0].name -eq $name) { return [DateTime]::UtcNow }
    Start-Sleep -Milliseconds 50
  }
  return $null
}

function Sperren([string]$geraet) {
  $o = Join-Path $Ordner 'sperren'
  if (-not (Test-Path -LiteralPath $o)) { return @() }
  return @(Get-ChildItem -LiteralPath $o -Filter '*.json' | Where-Object { -not $_.Name.StartsWith('.') } | ForEach-Object {
      try { ConvertFrom-Json ([System.IO.File]::ReadAllText($_.FullName)) } catch { $null } } | Where-Object { $_ -and $_.geraet -eq $geraet })
}

function Statistik([double[]]$werte) {
  if (-not $werte -or $werte.Count -eq 0) { return $null }
  $s = $werte | Sort-Object
  $median = if ($s.Count % 2) { $s[[int][Math]::Floor($s.Count / 2)] } else { ($s[$s.Count / 2 - 1] + $s[$s.Count / 2]) / 2 }
  return [ordered]@{ min = [Math]::Round($s[0]); median = [Math]::Round($median); max = [Math]::Round($s[-1]); werte = @($werte | ForEach-Object { [Math]::Round($_) }) }
}

# ------------------------------------------------------------------ Ablauf

$alteKodierung = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$prozessA = $null
$standAus = Join-Path $Ordner 'stand_nastest_aus'
$ordnerWarDa = Test-Path -LiteralPath $Ordner
$angelegt = $false
$uebernommen = $false
$fehler = $null

try {
  Schritt "Ordner: $Ordner"
  $ergebnis.ordner = $Ordner
  $ergebnis.rechner = $env:COMPUTERNAME
  $ergebnis.start = $startZeit.ToString('s')

  # 1. Bauen, falls noetig
  if ($Bauen -or -not (Test-Path $webDll) -or -not (Test-Path $mcpDll)) {
    Schritt 'Hlks.Web und Hlks.Mcp bauen'
    foreach ($proj in @("$root\src\Hlks.Web\Hlks.Web.csproj", "$root\src\Hlks.Mcp\Hlks.Mcp.csproj")) {
      & $dotnet build $proj -nologo -v q
      if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen: $proj" }
    }
  }
  if (-not (PortFrei $Port)) { throw "Port $Port ist belegt. Anderen Port mit -Port waehlen (nicht 5130/5131)." }

  # 2. Kommandozeile ohne Projekt (Startzeit dotnet + Katalog als Vergleich)
  $leer = HlksCli @('--ki-auftrag')
  Zeile 'Kommandozeile Start ohne Projekt' $leer.Ms 'ms' 'dotnet-Start, Vergleichswert'

  # 3. Beispielprojekt anlegen
  $kopf = Join-Path $Ordner 'projekt.json'
  if (Test-Path -LiteralPath $kopf) {
    if (-not $Vorhandenes) { throw "In $Ordner gibt es schon ein Projekt. -Vorhandenes angeben, um es zu verwenden, oder den Ordner leeren." }
    $name = (ConvertFrom-Json ([System.IO.File]::ReadAllText($kopf))).name
    if ($name -ne $beispielName) { throw "In $Ordner liegt nicht das Beispielprojekt ($name). Abbruch, um kein echtes Projekt zu veraendern." }
    $uebernommen = $true
    Zeile 'Anlegen Beispielprojekt' '-' '' 'vorhandenes Projekt verwendet'
  } else {
    Schritt 'Beispielprojekt anlegen (Kommandozeile, Geraet nas-b)'
    $r = HlksCli @('--beispiel', $Ordner, '--geraet', 'nas-b')
    if ($r.Code -ne 0) { throw "Anlegen fehlgeschlagen: $($r.Text)" }
    $angelegt = $true
    Zeile 'Anlegen Beispielprojekt (Kommandozeile)' $r.Ms 'ms' 'inkl. dotnet-Start'
  }
  $pakete = @(Get-ChildItem -LiteralPath (Join-Path $Ordner 'aenderungen') -Filter '*.json').Count
  Zeile 'Pakete in der Ablage' $pakete '' ''

  # 4. Arbeitsplatz A starten (Hlks.Web mit --projekt, Geraet nas-a)
  Schritt "Arbeitsplatz A starten (Port $Port, Geraet nas-a)"
  $logA = Join-Path $arbeit 'arbeitsplatz_a.log'
  $argA = @("`"$webDll`"", '--port', "$Port", '--projekt', "`"$Ordner`"", '--geraet', 'nas-a', '--akteur', 'NAS-A')
  $uhr = [Diagnostics.Stopwatch]::StartNew()
  $prozessA = Start-Process -FilePath $dotnet -ArgumentList $argA -PassThru -NoNewWindow -RedirectStandardOutput $logA -RedirectStandardError "$logA.err"
  $offen = $false
  while ($uhr.Elapsed.TotalSeconds -lt 180 -and -not $prozessA.HasExited) {
    try { $s = ApiGet '/api/status'; if ($s.Code -eq 200 -and $s.Json.offen) { $offen = $true; break } } catch { }
    Start-Sleep -Milliseconds 200
  }
  if (-not $offen) { throw "Arbeitsplatz A hat das Projekt nicht geoeffnet. Log: $logA" }
  Zeile 'Start A bis Projekt offen' $uhr.ElapsedMilliseconds 'ms' 'inkl. dotnet-Start und Oeffnen mit Standabbild'

  # 5. Oeffnen mit und ohne Standabbild (auf A, REST, ohne Programmstart)
  $koerperOeffnen = JsonText @{ ordner = $Ordner }
  $mitStand = @()
  for ($i = 0; $i -lt 2; $i++) {
    $uhr = [Diagnostics.Stopwatch]::StartNew()
    $r = ApiPost '/api/projekt/oeffnen' $koerperOeffnen
    if ($r.Code -ne 200) { throw "Oeffnen fehlgeschlagen: $($r.Text)" }
    $mitStand += $uhr.ElapsedMilliseconds
  }
  Zeile 'Oeffnen mit Standabbild' ($mitStand -join ', ') 'ms' 'A, REST, zwei Mal'
  $ohneStand = @()
  Schritt 'Oeffnen ohne Standabbild (Ordner stand voruebergehend umbenannt)'
  Rename-Item -LiteralPath (Join-Path $Ordner 'stand') -NewName 'stand_nastest_aus'
  try {
    for ($i = 0; $i -lt 2; $i++) {
      $uhr = [Diagnostics.Stopwatch]::StartNew()
      $r = ApiPost '/api/projekt/oeffnen' $koerperOeffnen
      if ($r.Code -ne 200) { throw "Oeffnen ohne Standabbild fehlgeschlagen: $($r.Text)" }
      $ohneStand += $uhr.ElapsedMilliseconds
    }
  } finally {
    $neuStand = Join-Path $Ordner 'stand'
    if (Test-Path -LiteralPath $neuStand) {
      Get-ChildItem -LiteralPath $neuStand | Move-Item -Destination $standAus -Force
      Remove-Item -LiteralPath $neuStand -Force -Recurse
    }
    Rename-Item -LiteralPath $standAus -NewName 'stand'
  }
  Zeile 'Oeffnen ohne Standabbild' ($ohneStand -join ', ') 'ms' "A, REST, alle $pakete Pakete anwenden"
  $r = ApiPost '/api/projekt/oeffnen' $koerperOeffnen    # wieder normal geoeffnet
  if ($r.Code -ne 200) { throw "Oeffnen fehlgeschlagen: $($r.Text)" }
  $ergebnis.oeffnenMitStandMs = $mitStand
  $ergebnis.oeffnenOhneStandMs = $ohneStand

  # 6. Fremde Aenderung: B (Kommandozeile) aendert, A zeigt sie nach ... ms
  Start-Sleep -Milliseconds 500
  $statusVorher = (ApiGet '/api/status').Json
  $waechterVorher = [int]$statusVorher.abgleich.waechterMeldungen
  $abPaket = @(); $abCliEnde = @(); $cliMs = @(); $laeufe = @()
  for ($i = 1; $i -le $Wiederholungen; $i++) {
    $name = "NAS-Test $i " + (Get-Date).ToString('HHmmss')
    $datei = ParameterDatei "aendern_$i" @{ element = 'WC-E.02'; name = $name }
    Schritt "Durchgang $i/$Wiederholungen : B aendert WC-E.02"
    $r = HlksCli @('--projekt', $Ordner, '--geraet', 'nas-b', '--akteur', 'NAS-B', '--befehl', 'element_aendern', "@$datei")
    if ($r.Code -ne 0 -or -not $r.Json -or -not $r.Json.angenommen) { throw "B abgelehnt: $($r.Text)" }
    $gesehen = WartenAufName 'WC-E.02' $name 30000
    $lauf = [ordered]@{ durchgang = $i; paket = $r.Json.paketId; cliMs = $r.Ms; abPaketMs = $null; abCliEndeMs = $null }
    if ($gesehen) {
      $lauf.abPaketMs = [Math]::Round(($gesehen - (PaketZeit $r.Json.paketId)).TotalMilliseconds)
      $lauf.abCliEndeMs = [Math]::Round(($gesehen - $r.Ende).TotalMilliseconds)
      $abPaket += $lauf.abPaketMs; $abCliEnde += $lauf.abCliEndeMs
    }
    $cliMs += $r.Ms
    $laeufe += [pscustomobject]$lauf
    Start-Sleep -Milliseconds (Get-Random -Minimum 200 -Maximum 1500)   # nicht immer im gleichen Abstand zum Takt
  }
  $statusNachher = (ApiGet '/api/status').Json
  $waechterMeldungen = [int]$statusNachher.abgleich.waechterMeldungen - $waechterVorher
  $verpasst = @($laeufe | Where-Object { $null -eq $_.abPaketMs }).Count
  $stat = Statistik $abPaket
  $ergebnis.aenderungen = $laeufe
  $ergebnis.fremdeAenderungAbPaketMs = $stat
  $ergebnis.fremdeAenderungAbCliEndeMs = Statistik $abCliEnde
  $ergebnis.cliAenderungMs = Statistik $cliMs
  $ergebnis.waechter = [ordered]@{ ordner = $statusNachher.abgleich.waechter; meldungen = $waechterMeldungen; taktSekunden = $statusNachher.abgleich.taktSekunden }
  $ergebnis.letzteAbgleichDauerMs = $statusNachher.abgleich.letzteDauerMs
  Zeile 'B aendert (Kommandozeile, gesamt)' ("{0} / {1} / {2}" -f $ergebnis.cliAenderungMs.min, $ergebnis.cliAenderungMs.median, $ergebnis.cliAenderungMs.max) 'ms' 'min/median/max, inkl. dotnet-Start und Oeffnen'
  if ($stat) {
    Zeile 'Fremde Aenderung bis A (ab Paket)' ("{0} / {1} / {2}" -f $stat.min, $stat.median, $stat.max) 'ms' "min/median/max, $Wiederholungen Durchgaenge, Abnahme < 3000"
    $s2 = $ergebnis.fremdeAenderungAbCliEndeMs
    Zeile 'Fremde Aenderung bis A (ab Ende B)' ("{0} / {1} / {2}" -f $s2.min, $s2.median, $s2.max) 'ms' 'min/median/max'
  }
  Zeile 'Nicht angekommen (30 s)' $verpasst '' ''
  Zeile 'Dateiwaechter' $(if ($statusNachher.abgleich.waechter) { 'aktiv' } else { 'aus' }) '' "$waechterMeldungen Meldungen waehrend der Durchgaenge (0 = nur Takt)"
  Zeile 'Dauer letzter Abgleich' $statusNachher.abgleich.letzteDauerMs 'ms' 'Paketliste + neue Pakete lesen und anwenden'
  Zeile 'Abgleich-Fehler' $(if ($statusNachher.abgleichFehler) { $statusNachher.abgleichFehler } else { 'keiner' }) '' ''
  $abnahme = ($verpasst -eq 0) -and $stat -and ($stat.max -lt 3000)
  $ergebnis.abnahme3s = [bool]$abnahme

  # 7. Reservierungskonflikt ueber die Ablage
  Schritt 'Reservierung: A aendert WC-E.03, B versucht dasselbe'
  $rA = ApiPost '/api/befehl/element_aendern' (JsonText @{ element = 'WC-E.03'; name = 'A reserviert' })
  $aOk = ($rA.Code -eq 200 -and $rA.Json.angenommen)
  $sperrenA = @(Sperren 'nas-a').Count
  $datei = ParameterDatei 'konflikt' @{ element = 'WC-E.03'; name = 'B will auch' }
  $uhr = [Diagnostics.Stopwatch]::StartNew()
  $rB = HlksCli @('--projekt', $Ordner, '--geraet', 'nas-b', '--akteur', 'NAS-B', '--befehl', 'element_aendern', "@$datei")
  $konflikt = ($rB.Code -eq 1 -and $rB.Json -and -not $rB.Json.angenommen -and @($rB.Json.gesperrt).Count -gt 0)
  $rF = ApiPost '/api/freigeben' '{}'
  $freigegeben = ($rF.Code -eq 200 -and $rF.Json.freigegeben)
  $sperrenNachFreigabe = @(Sperren 'nas-a').Count
  $rB2 = HlksCli @('--projekt', $Ordner, '--geraet', 'nas-b', '--akteur', 'NAS-B', '--befehl', 'element_aendern', "@$datei")
  $nachFreigabe = ($rB2.Code -eq 0 -and $rB2.Json -and $rB2.Json.angenommen)
  $gesehen = if ($nachFreigabe) { WartenAufName 'WC-E.03' 'B will auch' 30000 } else { $null }
  $ergebnis.reservierung = [ordered]@{
    aAendert = [bool]$aOk; sperrdateienA = $sperrenA; bAbgelehnt = [bool]$konflikt; meldungB = $(if ($rB.Json) { $rB.Json.fehler } else { $rB.Text })
    freigegeben = [bool]$freigegeben; sperrdateienANachFreigabe = $sperrenNachFreigabe; bNachFreigabeAngenommen = [bool]$nachFreigabe
    aSiehtB = [bool]$gesehen
  }
  Zeile 'Reservierung: A aendert WC-E.03' $(if ($aOk) { 'ja' } else { 'NEIN' }) '' "$sperrenA Sperrdatei(en) von nas-a"
  Zeile 'Reservierung: B abgelehnt' $(if ($konflikt) { 'ja' } else { 'NEIN' }) '' $ergebnis.reservierung.meldungB
  Zeile 'Reservierung: A gibt frei' $(if ($freigegeben -and $sperrenNachFreigabe -eq 0) { 'ja' } else { 'NEIN' }) '' "$sperrenNachFreigabe Sperrdatei(en) danach"
  Zeile 'Reservierung: B danach angenommen' $(if ($nachFreigabe) { 'ja' } else { 'NEIN' }) '' $(if ($gesehen) { 'A zeigt die Aenderung' } else { 'A zeigt sie NICHT' })

  # 8. Schliessen
  Schritt 'A: Projekt schliessen'
  $null = ApiPost '/api/befehl/element_aendern' (JsonText @{ element = 'WC-1.02'; name = 'vor dem Schliessen' })
  $uhr = [Diagnostics.Stopwatch]::StartNew()
  $rS = ApiPost '/api/projekt/schliessen' '{}'
  $schliessenMs = $uhr.ElapsedMilliseconds
  $frei = (@(Sperren 'nas-a').Count -eq 0)
  $ergebnis.schliessen = [ordered]@{ ms = $schliessenMs; offen = $rS.Json.offen; reservierungenFrei = $frei }
  Zeile 'Schliessen' $schliessenMs 'ms' $(if ($frei) { 'Reservierungen von nas-a frei' } else { 'Reservierungen von nas-a NICHT frei' })
}
catch {
  $fehler = $_.Exception.Message
  Write-Host "FEHLER: $fehler" -ForegroundColor Red
  Zeile 'FEHLER' $fehler '' ''
}
finally {
  if ($prozessA -and -not $prozessA.HasExited) {
    try { $null = ApiPost '/api/projekt/schliessen' '{}' } catch { }
    Stop-Process -Id $prozessA.Id -Force -ErrorAction SilentlyContinue
    $prozessA.WaitForExit(10000) | Out-Null
  }
  if ((Test-Path -LiteralPath $standAus) -and -not (Test-Path -LiteralPath (Join-Path $Ordner 'stand'))) {
    Rename-Item -LiteralPath $standAus -NewName 'stand'
  }
  # Nur loeschen, was dieser Lauf angelegt oder ausdruecklich (-Vorhandenes) uebernommen hat, und nur das Beispielprojekt
  if ($Aufraeumen -and -not ($angelegt -or $uebernommen)) {
    Write-Host 'Nicht aufgeraeumt: das Projekt im Ordner stammt nicht von diesem Lauf.' -ForegroundColor Yellow
  }
  elseif ($Aufraeumen -and (Test-Path -LiteralPath (Join-Path $Ordner 'projekt.json'))) {
    $name = (ConvertFrom-Json ([System.IO.File]::ReadAllText((Join-Path $Ordner 'projekt.json')))).name
    if ($name -eq $beispielName) {
      Schritt 'Aufraeumen: Projektdateien des Tests loeschen'
      foreach ($teil in @('aenderungen', 'stand', 'sperren', 'unterlagen', 'stand_nastest_aus', 'projekt.json')) {
        $p = Join-Path $Ordner $teil
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
      }
      if (-not $ordnerWarDa -and @(Get-ChildItem -LiteralPath $Ordner -Force).Count -eq 0) { Remove-Item -LiteralPath $Ordner -Force }
      $ergebnis.aufgeraeumt = $true
    } else {
      Write-Host "Nicht aufgeraeumt: in $Ordner liegt nicht das Beispielprojekt." -ForegroundColor Yellow
    }
  }
  [Console]::OutputEncoding = $alteKodierung
  $http.Dispose()
}

# ------------------------------------------------------------------ Ergebnis

$ergebnis.fehler = $fehler
$ergebnis.ende = (Get-Date).ToString('s')
$ergebnis.tabelle = $zeilen
$berichte = Join-Path $root 'berichte'
New-Item -ItemType Directory -Force $berichte | Out-Null
$jsonDatei = Join-Path $berichte "nas-test-$stempel.json"
[System.IO.File]::WriteAllText($jsonDatei, (ConvertTo-Json $ergebnis -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
Remove-Item -LiteralPath $arbeit -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'NAS-Test HLKS-CAD' -ForegroundColor Cyan
$zeilen | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host
if ($ergebnis.Contains('abnahme3s')) {
  if ($ergebnis.abnahme3s) { Write-Host 'Abnahme 3 s: erfuellt' -ForegroundColor Green } else { Write-Host 'Abnahme 3 s: NICHT erfuellt' -ForegroundColor Red }
}
Write-Host "Bericht: $jsonDatei"
if ($fehler) { exit 1 } else { exit 0 }

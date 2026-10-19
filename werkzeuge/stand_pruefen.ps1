# Stand pruefen und als "geprueft" markieren (Versionsverwaltung seit 19.10.2026, Christian: "damit der Desktop immer den
# letzten geprueften Stand startet und Agenten getrennt arbeiten").
# Ablauf:
#   1. Commit (Vorgabe main) in einer eigenen Arbeitskopie %USERPROFILE%\HLKS_CAD_pruefung auschecken (git worktree, detached).
#   2. Kern-, MCP- und Web-Tests dieses Commits bauen und laufen lassen (Build %USERPROFILE%\HLKS_CAD_build\pruefung,
#      Kataloge, KI-Vorlagen und Oberflaeche aus der Arbeitskopie), dazu die Node-Tests (*.test.mjs; Tests, die einen
#      Server brauchen, ueberspringen sich selbst). Browser-Reihen laufen hier nicht.
#   3. Fehlgeschlagene .NET-Tests einzeln wiederholen (Zeitgrenzen unter Last), hoechstens -Wiederholungen mal.
#   4. Alles gruen: Zweig "geprueft" auf den Commit setzen und Marke "geprueft-JJJJMMTT-hhmm". Der Desktop-Start
#      (kicad_starten.ps1) uebernimmt diesen Stand beim naechsten Klick.
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\stand_pruefen.ps1 [-Commit main] [-Wiederholungen 2] [-OhneNode] [-NurPruefen]
# Exit 0 = geprueft (bzw. -NurPruefen gruen), 1 = nicht gruen, 2 = Fehler im Ablauf. Protokoll: %USERPROFILE%\HLKS_CAD_build\pruefung\protokoll.log
# Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [string]$Commit = 'main',
  [int]$Wiederholungen = 2,
  [switch]$OhneNode,
  [switch]$NurPruefen
)

# Continue: git und dotnet schreiben Hinweise auf stderr (z. B. "Preparing worktree"); in PowerShell 5.1 waeren das mit
# Stop abbrechende Fehler. Fehler werden ueber Exitcodes geprueft (Git wirft selbst).
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$wt = "$env:USERPROFILE\HLKS_CAD_pruefung"
$bau = "$env:USERPROFILE\HLKS_CAD_build\pruefung"
$erg = "$bau\ergebnisse"
$log = "$bau\protokoll.log"
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
New-Item -ItemType Directory -Force $erg | Out-Null
function Log([string]$t) { $z = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t; Add-Content -Path $log -Value $z -Encoding UTF8; Write-Host $z }
function GitAufruf([string[]]$a) { $o = & git.exe @a 2>&1; if ($LASTEXITCODE -ne 0) { throw "git $($a -join ' '): $o" }; return $o }

try {
  $sha = (GitAufruf @('-C', $repo, 'rev-parse', '--verify', "$Commit^{commit}")).Trim()
  $kurz = $sha.Substring(0, 8)
  Log "Pruefe $Commit ($kurz)"

  # 1. Arbeitskopie
  if (-not (Test-Path "$wt\.git")) {
    if (Test-Path $wt) { Remove-Item $wt -Recurse -Force }
    GitAufruf @('-C', $repo, 'worktree', 'add', '--detach', $wt, $sha) | Out-Null
  } else {
    GitAufruf @('-C', $wt, 'checkout', '-q', '--detach', $sha) | Out-Null
    GitAufruf @('-C', $wt, 'reset', '-q', '--hard', $sha) | Out-Null
    GitAufruf @('-C', $wt, 'clean', '-qfd') | Out-Null
  }

  # Umgebung: alles aus der Arbeitskopie, Build getrennt; Testvariablen loeschen (Grundzustand setzt TestUmgebung selbst)
  $env:HLKS_BUILD = $bau
  $env:HLKS_KATALOG = "$wt\kataloge"
  $env:HLKS_KI = "$wt\ki"
  $env:HLKS_WEBROOT = "$wt\src\Hlks.Web\wwwroot"
  foreach ($v in 'HLKS_FAMILIENBIBLIOTHEK', 'HLKS_BIBLIOTHEK_AUSSTEHEND', 'HLKS_PROJEKTABLAGE', 'HLKS_ZULETZT', 'HLKS_BENUTZER', 'HLKS_MCP_DLL') { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
  $env:HLKS_TEST = '1'

  # 2./3. .NET-Tests
  $gruen = $true
  $zusammen = @()
  foreach ($p in 'Hlks.Kern.Tests', 'Hlks.Mcp.Tests', 'Hlks.Web.Tests') {
    $trx = "$erg\$p.trx"; Remove-Item $trx -ErrorAction SilentlyContinue
    $uhr = [System.Diagnostics.Stopwatch]::StartNew()
    & $dotnet test "$wt\tests\$p" --nologo -v q --logger "trx;LogFileName=$p.trx" --results-directory $erg 2>&1 | Out-Null
    $fehl = @()
    if (Test-Path $trx) {
      [xml]$x = Get-Content -Raw $trx
      $alle = @($x.TestRun.Results.UnitTestResult)
      $fehl = @($alle | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { ($_.testName -replace '\(.*$', '') } | Sort-Object -Unique)
      $anzahl = $alle.Count
    } else { $anzahl = 0; $fehl = @('BUILD') }
    $zeile = "{0}: {1} Tests, {2} rot ({3:0} s)" -f $p, $anzahl, $fehl.Count, $uhr.Elapsed.TotalSeconds
    Log $zeile
    for ($w = 1; $w -le $Wiederholungen -and $fehl.Count -gt 0 -and $fehl[0] -ne 'BUILD'; $w++) {
      $filter = ($fehl | ForEach-Object { "FullyQualifiedName=$_" }) -join '|'
      $trx2 = "$erg\$p.w$w.trx"; Remove-Item $trx2 -ErrorAction SilentlyContinue
      & $dotnet test "$wt\tests\$p" --nologo -v q --no-build --filter $filter --logger "trx;LogFileName=$p.w$w.trx" --results-directory $erg 2>&1 | Out-Null
      if (Test-Path $trx2) { [xml]$x2 = Get-Content -Raw $trx2; $fehl = @($x2.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { ($_.testName -replace '\(.*$', '') } | Sort-Object -Unique) }
      Log ("  Wiederholung {0}: {1} rot" -f $w, $fehl.Count)
    }
    if ($fehl.Count -gt 0) { $gruen = $false; foreach ($f in $fehl) { Log "  ROT: $f" } }
    $zusammen += $zeile
  }

  # Node-Tests
  if (-not $OhneNode) {
    $dateien = @(Get-ChildItem "$wt\src\Hlks.Web\wwwroot\js" -Recurse -Filter *.test.mjs | ForEach-Object { $_.FullName })
    $env:HLKS_TEST_SERVER = ''; $env:HLKS_TESTSERVER = ''   # nie einen fremden Server (z. B. den Desktop) abfragen
    $aus = & node --test --test-reporter=tap @dateien 2>&1 | Out-String
    $f = if ($aus -match '(?m)^# fail (\d+)') { [int]$Matches[1] } else { -1 }
    $t = if ($aus -match '(?m)^# tests (\d+)') { [int]$Matches[1] } else { 0 }
    Log ("Node: {0} Tests, {1} rot" -f $t, $f)
    if ($f -ne 0) { $gruen = $false; ($aus -split "`n" | Where-Object { $_ -match '^not ok' } | Select-Object -First 20) | ForEach-Object { Log "  ROT: $($_.Trim())" } }
  }

  if (-not $gruen) { Log "NICHT gruen - $kurz bleibt ungeprueft"; exit 1 }
  if ($NurPruefen) { Log "gruen ($kurz), -NurPruefen: nichts gesetzt"; exit 0 }
  $marke = "geprueft-{0:yyyyMMdd-HHmm}" -f (Get-Date)
  GitAufruf @('-C', $repo, 'branch', '-f', 'geprueft', $sha) | Out-Null
  GitAufruf @('-C', $repo, 'tag', '-f', $marke, $sha) | Out-Null
  Log "GEPRUEFT: $kurz -> Zweig geprueft, Marke $marke"
  exit 0
} catch {
  Log "FEHLER: $($_.Exception.Message)"
  exit 2
}

# Desktop-Start fuer das XR-Labor (Christian 26.10.2026: "die labor app dann auch gleich mit desktop icon"; Spezifikation
# entwurf\labor-app.md, Abschnitt "Ports und Prozesse", Stufe L0). Gegenstueck zu kicad_starten.ps1, gleiche Regeln:
# - Das Labor laeuft NIE aus dem Arbeitsordner auf OneDrive, sondern aus der Arbeitskopie %USERPROFILE%\HLKS_CAD_desktop (dieselbe wie
#   KICAD) auf dem Zweig "geprueft" (sonst main, mit Hinweis). Gebaut wird nur src\Hlks.Labor (mit seinen Projekten Kern, Xr, Web) in den
#   eigenen Ordner %USERPROFILE%\HLKS_CAD_build\labor: ein Neubau von KICAD (Ordner desktop) trifft das laufende Labor nie und umgekehrt.
#   Zustand labor.json, Protokoll start.log und server.log liegen dort. Oberflaeche (HLKS_LABOR_WEBROOT) und Kataloge (HLKS_KATALOG)
#   liest der Server live aus der Arbeitskopie.
# Ablauf beim Klick auf "XR-Labor":
# - Laeuft das Labor (/api/status mit app = xr-labor) schon mit dem neuesten geprueften Stand: nur das Fenster.
# - Gibt es einen neueren geprueften Stand: Arbeitskopie nachziehen, bauen, Server neu starten. Arbeitet das Labor gerade
#   (/api/status "beschaeftigt"): kein Neustart, Hinweis.
# - Laeuft kein Server: bauen falls noetig und starten.
# - Schlaegt Bau oder Start fehl: zurueck auf den vorigen Stand, dessen Bau und Start.
# - -Neu: in jedem Fall neu starten (arbeitet das Labor, fragt ein Fenster nach).
# Gemeinsame Arbeitskopie mit KICAD (Entscheid 26.10.2026): KICAD liest Oberflaeche, Kataloge und KI-Vorlagen LIVE aus
# %USERPROFILE%\HLKS_CAD_desktop (HLKS_WEBROOT, HLKS_KATALOG, HLKS_KI), nur das Programm kommt aus seinem Bauordner. Ein Nachziehen
# auf einen neueren Stand wuerde die Oberflaeche eines laufenden, aelteren KICAD-Servers unter ihm austauschen (JS und Programm
# passten nicht mehr zusammen). Darum zieht das Labor die Arbeitskopie nur nach, wenn KICAD nicht laeuft (nichts auf Port 5130) und
# kicad_starten.ps1 nicht gerade selbst nachzieht oder baut (Prozess). Sonst startet bzw. belaesst das Labor den Stand, der in der
# Arbeitskopie liegt (= der von KICAD), und sagt, dass der neuere Stand kommt, sobald KICAD neu gestartet oder beendet ist.
# Umgekehrt (KICAD zieht nach, waehrend das Labor laeuft) erkennt dieses Skript beim naechsten Klick, dass die Arbeitskopie nicht mehr
# zum laufenden Labor passt, und startet es neu (ausser es arbeitet gerade).
# Test ohne Christians Desktop-Stand: -Arbeitskopie, -Bau und -Port umleiten (z. B. unter %TEMP%), -Trocken meldet nur die Entscheidung,
# -OhneFenster oeffnet kein Fenster und zeigt keine Meldungsfenster.
# Datei bewusst nur ASCII (PowerShell 5.1).

param(
  [int]$Port = 5430,
  [switch]$Neu,
  [string]$Arbeitskopie = '',
  [string]$Bau = '',
  [int]$KicadPort = 5130,
  [switch]$Trocken,
  [switch]$OhneFenster
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot\..").Path
$wt = if ($Arbeitskopie) { $Arbeitskopie } else { "$env:USERPROFILE\HLKS_CAD_desktop" }
$bau = if ($Bau) { $Bau } else { "$env:USERPROFILE\HLKS_CAD_build\labor" }
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$bin = "$bau\Hlks.Labor\bin\Debug\net10.0"
$dll = "$bin\Hlks.Labor.dll"
$zustandDatei = "$bau\labor.json"
$url = "http://localhost:$Port"
$log = "$bau\start.log"
$projektRel = 'src\Hlks.Labor\Hlks.Labor.csproj'
New-Item -ItemType Directory -Force $bau | Out-Null
function Log([string]$t) { Add-Content -Path $log -Value ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8; if ($Trocken) { Write-Host $t } }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$ico = Join-Path $PSScriptRoot 'labor.ico'
. "$PSScriptRoot\kicad_stand.ps1"   # Kicad-NeuesteQuelle, Kicad-BuildStand (Regeln "Build aktuell" wie bei KICAD)

function GitAufruf([string[]]$a) { $o = & git.exe @a 2>&1; if ($LASTEXITCODE -ne 0) { throw "git $($a -join ' '): $o" }; return ($o | Out-String).Trim() }
function Status([int]$p) { try { return (Invoke-WebRequest "http://localhost:$p/api/status" -UseBasicParsing -TimeoutSec 3).Content | ConvertFrom-Json } catch { return $null } }
function LaborStatus { $s = Status $Port; if ($s -and $s.app -eq 'xr-labor') { return $s } else { return $null } }
function LaborLaeuft { return $null -ne (LaborStatus) }

function Fenster {
  if ($OhneFenster) { Log 'Fenster: uebersprungen (-OhneFenster)'; return }
  $kandidaten = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe")
  $exe = $kandidaten | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
  if ($exe) {
    # eigenes Fensterprofil (KICAD nutzt ...\HLKS_CAD\fenster): eigenes App-Fenster und eigener Eintrag in der Taskleiste
    $profil = "$env:LOCALAPPDATA\HLKS_CAD\fenster_labor"
    Start-Process -FilePath $exe -ArgumentList @("--app=$url/", '--start-maximized', "--user-data-dir=`"$profil`"", '--no-first-run', '--no-default-browser-check')
  } else { Start-Process "$url/" }
}

function Hinweis([string]$text) {
  Log "Hinweis: $($text -replace '\r?\n', ' | ')"
  if ($Trocken -or $OhneFenster) { return }
  # nicht blockierend: eigener Prozess (Befehl Base64, damit Zeilenumbrueche heil ankommen)
  $t = $text.Replace("'", "''")
  $cmd = "Add-Type -AssemblyName System.Windows.Forms; [void][System.Windows.Forms.MessageBox]::Show('$t', 'XR-Labor')"
  $enc = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($cmd))
  Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-WindowStyle', 'Hidden', '-EncodedCommand', $enc) -WindowStyle Hidden | Out-Null
}
function Fehlerfenster([string]$text) {
  Log "Fehler: $($text -replace '\r?\n', ' | ')"
  if ($Trocken -or $OhneFenster) { return }
  [System.Windows.Forms.MessageBox]::Show($text, 'XR-Labor') | Out-Null
}

function Laufender([int]$p) {
  $c = Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $c) { return $null }
  $pr = Get-CimInstance Win32_Process -Filter "ProcessId=$($c.OwningProcess)"
  return [pscustomobject]@{ Pid = [int]$c.OwningProcess; Labor = [bool]($pr -and $pr.CommandLine -match 'Hlks\.Labor\.dll') }
}
function Beenden($srv) {
  Log "beende Server $($srv.Pid) auf $Port"
  Stop-Process -Id $srv.Pid -Force
  for ($i = 0; $i -lt 30 -and (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
}
function Zustand { if (Test-Path $zustandDatei) { try { return Get-Content -Raw $zustandDatei | ConvertFrom-Json } catch { return $null } } else { return $null } }
# fehler: Stand, der sich nicht bauen/starten liess und auf den vorigen zurueckgefallen ist (kein erneuter Versuch bei jedem Klick; -Neu versucht es)
function ZustandMerken([string]$sha, [int]$serverPid, [string]$fehler = '') {
  $z = [ordered]@{ sha = $sha; pid = $serverPid; port = $Port; zeit = (Get-Date).ToString('o') }
  if ($fehler) { $z['fehler'] = $fehler }
  $z | ConvertTo-Json | Set-Content -Path $zustandDatei -Encoding UTF8
}
function Kurz([string]$s) { if ($s -and $s.Length -ge 8) { return $s.Substring(0, 8) } elseif ($s) { return $s } else { return '-' } }
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

# KICAD (Port 5130): laeuft es, oder zieht kicad_starten.ps1 gerade die gemeinsame Arbeitskopie nach bzw. baut?
function KicadLaeuft { return $null -ne (Get-NetTCPConnection -State Listen -LocalPort $KicadPort -ErrorAction SilentlyContinue | Select-Object -First 1) }
function KicadStartetGerade {
  $m = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'kicad_starten\.ps1' -and $_.ProcessId -ne $PID }
  return $null -ne ($m | Select-Object -First 1)
}

# Bau von src\Hlks.Labor in den eigenen Ordner; neu, wenn er fehlt oder eine Quelle der beteiligten Projekte neuer ist
function BauVeraltet {
  if (-not (Test-Path $dll)) { return $true }
  $stand = Kicad-BuildStand $bin 'Hlks.Labor'
  foreach ($n in @('Hlks.Web', 'Hlks.Xr')) { $f = "$bin\$n.dll"; if ((Test-Path $f) -and (Get-Item $f).LastWriteTimeUtc -gt $stand) { $stand = (Get-Item $f).LastWriteTimeUtc } }
  return (Kicad-NeuesteQuelle $wt @('Hlks.Kern', 'Hlks.Xr', 'Hlks.Web', 'Hlks.Labor')) -gt $stand
}
function Bauen($anzeige) {
  $proj = Join-Path $wt $projektRel
  if (-not (Test-Path $proj)) { Log "Bau: $proj fehlt"; return $false }
  $env:HLKS_BUILD = $bau
  $aus = Join-Path $bau 'build.out.txt'; $fehl = Join-Path $bau 'build.err.txt'
  Log "Bau: dotnet build $projektRel (HLKS_BUILD=$bau)"
  $p = Start-Process -FilePath $dotnet -ArgumentList @('build', "`"$proj`"", '-nologo', '-v', 'q') -WindowStyle Hidden -PassThru -RedirectStandardOutput $aus -RedirectStandardError $fehl
  $null = $p.Handle
  $uhr = [System.Diagnostics.Stopwatch]::StartNew()
  while (-not $p.HasExited) {
    if ($uhr.Elapsed.TotalMinutes -gt 20) { try { $p.Kill() } catch { }; Log 'Bau: Zeitueberschreitung (20 min) - abgebrochen'; return $false }
    if ($anzeige) { $anzeige.Text = ("XR-Labor startet ... ({0:0} s)`r`nBau laeuft (einige Minuten)." -f $uhr.Elapsed.TotalSeconds) }
    for ($j = 0; $j -lt 10; $j++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
  }
  foreach ($d in @($aus, $fehl)) { if (Test-Path $d) { Get-Content $d -Encoding UTF8 -ErrorAction SilentlyContinue | Where-Object { $_ } | Select-Object -Last 15 | ForEach-Object { Log "Bau: $_" } } }
  if ($p.ExitCode -ne 0) { Log "Bau: fehlgeschlagen (Exit $($p.ExitCode))"; return $false }
  if (-not (Test-Path $dll)) { Log "Bau: $dll fehlt trotz Exit 0"; return $false }
  Log "Bau: fertig ($([int]$uhr.Elapsed.TotalSeconds) s)"
  return $true
}

# Server losgeloest wie bei KICAD: eigener Prozess ohne Bindung an dieses Skript (Start-Process ohne Umleitung der Handles); die Ausgabe
# des Servers schreibt das kurzlebige Huellen-PowerShell in server.log.
function ServerStarten {
  $env:HLKS_BUILD = $bau
  $env:HLKS_LABOR_WEBROOT = "$wt\src\Hlks.Labor\wwwroot"
  $env:HLKS_KATALOG = "$wt\kataloge"
  $q = { param($s) $s.Replace("'", "''") }
  $cmd = "& '{0}' '{1}' --port {2} --katalog '{3}' *> '{4}'" -f (& $q $dotnet), (& $q $dll), $Port, (& $q "$wt\kataloge"), (& $q "$bau\server.log")
  Log "starte Server: dotnet Hlks.Labor.dll --port $Port (Oberflaeche $env:HLKS_LABOR_WEBROOT)"
  return Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-Command', "`"$cmd`"") -WindowStyle Hidden -PassThru
}
function AufServerWarten($ps, $anzeige, [int]$sekunden) {
  $uhr = [System.Diagnostics.Stopwatch]::StartNew()
  while ($uhr.Elapsed.TotalSeconds -lt $sekunden) {
    if (LaborLaeuft) { return $true }
    if ($ps.HasExited -and -not (Laufender $Port)) { return $false }
    if ($anzeige) { $anzeige.Text = ("XR-Labor startet ... ({0:0} s)`r`nServer wird gestartet." -f $uhr.Elapsed.TotalSeconds) }
    for ($j = 0; $j -lt 10; $j++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
  }
  return $false
}

# ------------------------------------------------------------------ nur ein Start gleichzeitig (Doppelklick)
$mutex = New-Object System.Threading.Mutex($false, 'Local\HLKS_LABOR_STARTEN')
$habe = $false
try { $habe = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $habe = $true }
if (-not $habe -and -not $Trocken) {
  Log 'Ein anderer Start des XR-Labors laeuft schon - nur Fenster, falls es antwortet'
  if (LaborLaeuft) { Fenster }
  exit 0
}

# ------------------------------------------------------------------ Stand bestimmen
$ohneGeprueft = $false
try { $sha = GitAufruf @('-C', $repo, 'rev-parse', '--verify', 'geprueft^{commit}') }
catch { $sha = GitAufruf @('-C', $repo, 'rev-parse', '--verify', 'main^{commit}'); $ohneGeprueft = $true }
$kurz = Kurz $sha
$wtSha = if (Test-Path "$wt\.git") { try { GitAufruf @('-C', $wt, 'rev-parse', 'HEAD') } catch { '' } } else { '' }
$zu = Zustand
$srv = Laufender $Port
$st = LaborStatus

if ($srv -and -not $srv.Labor -and -not $st) {
  Fehlerfenster "Port $Port ist von einem anderen Programm belegt (Prozess $($srv.Pid)). Das XR-Labor kann nicht starten.`r`nPort mit -Port aendern oder das Programm beenden.`r`nProtokoll: $log"
  exit 1
}

$kicadLaeuft = KicadLaeuft
$kicadStartet = KicadStartetGerade
$darfNachziehen = (-not $wtSha) -or ((-not $kicadLaeuft) -and (-not $kicadStartet))
$gescheitert = [bool]($zu -and $zu.fehler -and $zu.fehler -eq $sha -and $wtSha -and -not $Neu)   # dieser Stand liess sich schon einmal nicht starten
$ziel = if ($gescheitert) { $wtSha } elseif ($wtSha -eq $sha -or $darfNachziehen) { $sha } else { $wtSha }
$verzoegert = ($ziel -ne $sha)
$zielKurz = Kurz $ziel
Log ("Klick: geprueft {0}, Arbeitskopie {1}, Ziel {2}, Server {3}, gemerkt {4}/{5}, KICAD laeuft={6} startet={7}, Neu={8}, beschaeftigt={9}" -f $kurz, (Kurz $wtSha), $zielKurz,
  $(if ($srv) { $srv.Pid } else { '-' }), $(if ($zu) { $zu.pid } else { '-' }), $(if ($zu) { Kurz $zu.sha } else { '-' }), $kicadLaeuft, $kicadStartet, [bool]$Neu, $(if ($st) { $st.beschaeftigt } else { '-' }))

$hinweisVerzoegert = if ($gescheitert) {
  "Der gepruefte Stand $kurz liess sich beim letzten Versuch nicht bauen oder starten. Das XR-Labor laeuft mit dem vorigen Stand ($zielKurz).`r`nNeuer Versuch mit dem Icon 'XR-Labor aktualisieren' (Schalter -Neu), sobald es einen neuen geprueften Stand gibt.`r`nProtokoll: $log"
} else {
  "Es gibt einen neueren geprueften Stand ($kurz), aber KICAD laeuft (oder startet gerade) und teilt sich die Arbeitskopie mit dem Labor.`r`nDas Labor laeuft mit dem Stand der Arbeitskopie ($zielKurz). Der neue Stand kommt, sobald KICAD beendet oder neu gestartet ist (dann nochmals auf das Icon klicken)."
}

if ($Trocken) {
  Log "Trocken: wuerde $(if ($srv -and $st) { 'laufenden Server pruefen' } else { 'bauen/starten' }); nachziehen=$($wtSha -ne $ziel); bauen=$(($wtSha -ne $ziel) -or (BauVeraltet)); Stand $zielKurz"
  exit 0
}

# ------------------------------------------------------------------ laeuft das Labor schon?
if ($srv -and $st) {
  $aktuell = $zu -and ([int]$zu.pid -eq $srv.Pid) -and ($zu.sha -eq $ziel) -and ($wtSha -eq $ziel)
  if ($aktuell -and -not $Neu) {
    Log "Labor laeuft auf Stand $zielKurz - Fenster"
    Fenster
    if ($verzoegert) { Hinweis $hinweisVerzoegert }
    exit 0
  }
  if ($st.beschaeftigt) {
    if ($Neu -and -not $OhneFenster) {
      $antwort = [System.Windows.Forms.MessageBox]::Show("Das XR-Labor arbeitet gerade (z. B. eine Messung).`r`nTrotzdem neu starten? Nicht Gespeichertes geht verloren.", 'XR-Labor', 'YesNo', 'Warning')
      if ($antwort -ne 'Yes') { Log 'Neustart abgelehnt (beschaeftigt)'; Fenster; exit 0 }
    } else {
      Log "Server beschaeftigt - kein Neustart (Ziel $zielKurz)"
      Fenster
      Hinweis "Es gibt einen neueren Stand oder einen Neustart wurde verlangt, aber das XR-Labor arbeitet gerade (z. B. eine Messung).`r`nDas Fenster zeigt den laufenden Stand. Nach Abschluss nochmals auf das Icon klicken."
      exit 0
    }
  }
  Log "Neustart (laufend: $(if ($zu) { Kurz $zu.sha } else { 'unbekannt' }), Ziel $zielKurz)"
  Beenden $srv
} elseif ($srv) {
  Beenden $srv   # Prozess ohne Antwort: haengt - neu starten
}

# ------------------------------------------------------------------ Arbeitskopie nachziehen, bauen, starten
$vorher = $wtSha
$projektDa = { Test-Path (Join-Path $wt $projektRel) }
$hinweis = New-Object System.Windows.Forms.Form
$hinweis.Text = 'XR-Labor'; $hinweis.Width = 460; $hinweis.Height = 130; $hinweis.StartPosition = 'CenterScreen'; $hinweis.TopMost = $true
$hinweis.FormBorderStyle = 'FixedDialog'; $hinweis.MaximizeBox = $false; $hinweis.MinimizeBox = $false
if (Test-Path $ico) { try { $hinweis.Icon = New-Object System.Drawing.Icon($ico) } catch { } }
$text = New-Object System.Windows.Forms.Label; $text.Dock = 'Fill'; $text.TextAlign = 'MiddleCenter'
$text.Text = "XR-Labor startet ...`r`nStand $zielKurz"
$hinweis.Controls.Add($text)
if (-not $OhneFenster) { $hinweis.Show(); [System.Windows.Forms.Application]::DoEvents() }

try { if ($wtSha -ne $ziel) { Auschecken $ziel; Log "Arbeitskopie auf $zielKurz" } }
catch { $hinweis.Close(); Fehlerfenster "XR-Labor: Arbeitskopie liess sich nicht nachziehen.`r`n$($_.Exception.Message)"; exit 1 }

$bereit = $false; $zurueck = $false; $ps = $null
if (-not (& $projektDa)) {
  Log "Stand $zielKurz enthaelt $projektRel noch nicht"
} else {
  $gebaut = $true
  if (($vorher -ne $ziel) -or (BauVeraltet)) { $gebaut = Bauen $text } else { Log "Bau aktuell (Stand $zielKurz) - kein Neubau" }
  if ($gebaut) {
    $ps = ServerStarten
    $bereit = AufServerWarten $ps $text 90
    if (-not $bereit) { Log "Server antwortet nicht (Stand $zielKurz), siehe $bau\server.log" }
  }
}

# Rueckfall auf den vorigen Stand: dessen Quellen, dessen Bau, dessen Start
if (-not $bereit -and $vorher -and $vorher -ne $ziel) {
  Log "Start mit $zielKurz fehlgeschlagen - zurueck auf $(Kurz $vorher)"
  $text.Text = "Start fehlgeschlagen - zurueck auf den vorigen Stand ($(Kurz $vorher))."
  $s2 = Laufender $Port; if ($s2 -and $s2.Labor) { Beenden $s2 }
  try { Auschecken $vorher } catch { Log "Zurueck fehlgeschlagen: $($_.Exception.Message)" }
  if (& $projektDa) {
    if (Bauen $text) {
      $ps = ServerStarten
      $bereit = AufServerWarten $ps $text 90
      $zurueck = $bereit
    }
  } else { Log "Voriger Stand $(Kurz $vorher) enthaelt $projektRel nicht - kein Rueckfall moeglich" }
}
$hinweis.Close()

if ($bereit) {
  $srvJetzt = Laufender $Port
  $gilt = if ($zurueck) { $vorher } else { $ziel }
  if ($srvJetzt) { ZustandMerken $gilt $srvJetzt.Pid $(if ($zurueck) { $ziel } elseif ($gescheitert) { $sha } else { '' }) }
  Log "Server bereit (Stand $(Kurz $gilt))"
  Fenster
  if ($zurueck) { Hinweis "Der neue gepruefte Stand $kurz liess sich nicht starten. Das XR-Labor laeuft mit dem vorigen Stand $(Kurz $vorher).`r`nProtokoll: $log" }
  elseif ($verzoegert) { Hinweis $hinweisVerzoegert }
  elseif ($ohneGeprueft) { Hinweis "Es gibt noch keinen geprueften Stand (Zweig geprueft). Das XR-Labor laeuft mit dem Arbeitsstand $kurz (main)." }
  exit 0
}
Log 'Server antwortet nicht'
Fehlerfenster "Das XR-Labor startet nicht (Port $Port).`r`nProtokoll: $log`r`nServer-Ausgabe: $bau\server.log"
exit 1

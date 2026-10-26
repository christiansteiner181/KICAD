# Legt die Desktop-Verknuepfung "XR-Labor" an (Christian 26.10.2026: "die labor app dann auch gleich mit desktop icon").
# Gleiche Bauart wie die Verknuepfung "KICAD" auf Christians Desktop (dort gelesen 26.10.2026): Ziel powershell.exe mit
#   -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<Repo>\werkzeuge\labor_starten.ps1"
# Arbeitsordner <Repo>\werkzeuge, Fensterstil minimiert (7), Symbol <Repo>\werkzeuge\labor.ico. <Repo> ist das Repository, aus dem dieses
# Skript aufgerufen wird (bei "KICAD" der OneDrive-Ordner ...\Claude_Code\HLKS_CAD, in dem main ausgecheckt ist); gestartet wird
# der Stand der Zweigspitze "geprueft" aus der gemeinsamen Arbeitskopie, nicht der Arbeitsstand des Ordners.
# Aufruf: powershell -ExecutionPolicy Bypass -File werkzeuge\labor_verknuepfung.ps1 [-Ordner <ordner>] [-MitAktualisieren] [-Entfernen]
#   -Ordner            Zielordner der Verknuepfung (Vorgabe: der Desktop des Benutzers, auch wenn OneDrive ihn verwaltet)
#   -MitAktualisieren  zusaetzlich "XR-Labor aktualisieren" (startet mit -Neu in jedem Fall neu), wie "KICAD aktualisieren"
#   -Entfernen         loescht die Verknuepfung(en) wieder
# Aendert nichts ausser den Dateien "XR-Labor.lnk" (und "XR-Labor aktualisieren.lnk") im Zielordner. Datei bewusst nur ASCII (PowerShell 5.1).
param(
  [string]$Ordner = '',
  [switch]$MitAktualisieren,
  [switch]$Entfernen
)
$ErrorActionPreference = 'Stop'
$werkzeuge = $PSScriptRoot
$skript = Join-Path $werkzeuge 'labor_starten.ps1'
$symbol = Join-Path $werkzeuge 'labor.ico'
if (-not (Test-Path $skript)) { Write-Host "FEHLER: $skript fehlt" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $symbol)) { Write-Host "FEHLER: $symbol fehlt (node werkzeuge\labor_symbol_erzeugen.mjs)" -ForegroundColor Red; exit 1 }
if (-not $Ordner) { $Ordner = [Environment]::GetFolderPath('Desktop') }
if (-not (Test-Path $Ordner)) { Write-Host "FEHLER: Zielordner fehlt: $Ordner" -ForegroundColor Red; exit 1 }
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

$eintraege = @(
  @{ Name = 'XR-Labor'; Zusatz = ''; Beschreibung = 'XR-Labor oeffnen (startet den Server, falls er nicht laeuft)' }
)
if ($MitAktualisieren) { $eintraege += @{ Name = 'XR-Labor aktualisieren'; Zusatz = ' -Neu'; Beschreibung = 'XR-Labor neu starten mit dem neuesten Stand' } }

$sh = New-Object -ComObject WScript.Shell
foreach ($e in $eintraege) {
  $datei = Join-Path $Ordner ($e.Name + '.lnk')
  if ($Entfernen) {
    if (Test-Path $datei) { Remove-Item $datei -Force; Write-Host "entfernt: $datei" } else { Write-Host "nicht vorhanden: $datei" }
    continue
  }
  $l = $sh.CreateShortcut($datei)
  $l.TargetPath = $powershell
  $l.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $skript + '"' + $e.Zusatz
  $l.WorkingDirectory = $werkzeuge
  $l.IconLocation = "$symbol,0"
  $l.WindowStyle = 7
  $l.Description = $e.Beschreibung
  $l.Save()
  Write-Host "angelegt: $datei"
  Write-Host "  Ziel:   $($l.TargetPath) $($l.Arguments)"
  Write-Host "  Symbol: $($l.IconLocation)"
}

# Firewall-Regeln fuer den XR-Zugang der Quest App (Modul XR, Quest App P3; Entscheid Christian 23.10.2026: "Sofort WLAN mit Kopplung und TLS").
# KICAD oeffnet den Zugang nur, wenn der Schalter dieses PCs an ist (%LOCALAPPDATA%\HLKS_CAD\xr\zugang.json, Vorgabe aus; je PC, nicht ueber OneDrive) und nur im KICAD auf Port 5130 (Desktop):
#   - TCP 5140  HTTPS, nur /api/xr, nur gekoppelte Brillen (src/Hlks.Web/XrZugang.cs)
#   - UDP 5141  Suche des PCs im Bueronetz; Antwort nur auf signierte Anfragen gekoppelter Brillen (src/Hlks.Web/XrSuche.cs)
# Dieses Skript legt dafuer zwei eingehende Regeln an: nur Profil "Privat", nur aus dem eigenen Subnetz (LocalSubnet). Gruppe "KICAD XR".
# Es aendert sonst nichts (kein Netzprofil, keine anderen Regeln). KICAD selbst aendert nie Firewall- oder Systemeinstellungen.
# XR-Labor (Entwurf entwurf\labor-app.md, Stufe L0; Christian 26.10.2026): dazu kommen zwei gleichartige Regeln fuer die Labor-Brille,
# Gruppe "XR-Labor", Regelnamen "XR-Labor ...":
#   - TCP 5340  HTTPS, Zugang der Labor-App (src/Hlks.Labor, gleiche Technik wie 5140, eigene Kopplung je Brille)
#   - UDP 5341  Suche des PCs durch die Labor-App
# Wie die Regeln fuer KICAD: nur Profil "Privat", nur aus dem eigenen Subnetz; zusaetzlich nur fuer das Programm dotnet.exe
# (C:\Program Files\dotnet\dotnet.exe), das den Labor-Server ausfuehrt. Das Labor oeffnet den Zugang nur, wenn der Schalter dieses PCs an ist.
#
# Aufruf (PowerShell als Administrator, Christian fuehrt es selbst aus):
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_firewall.ps1                  Regeln anlegen bzw. erneuern (KICAD: TCP 5140, UDP 5141; XR-Labor: TCP 5340, UDP 5341)
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_firewall.ps1 -Port 5240 -SuchPort 5241   andere Ports (wie xr.port / xr.suchport)
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_firewall.ps1 -LaborPort 5440 -LaborSuchPort 5441   andere Ports fuer das XR-Labor
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_firewall.ps1 -Entfernen       Regeln der Gruppen "KICAD XR" und "XR-Labor" entfernen
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_firewall.ps1 -Liste           nur anzeigen (Regeln beider Gruppen, Netzprofil, Sperren fuer dotnet.exe)
# Hinweis: Fragt Windows beim ersten Einschalten des Zugangs "Zugriff zulassen?" fuer dotnet.exe, nur "Private Netzwerke" waehlen.
# "Abbrechen" legt Sperrregeln fuer dotnet.exe an, die Vorrang vor diesen Regeln haben (-Liste zeigt sie).
# Datei bewusst nur ASCII (PowerShell 5.1). Laedt nichts herunter.
param(
  [ValidateRange(1024, 65535)][int]$Port = 5140,
  [ValidateRange(1024, 65535)][int]$SuchPort = 5141,
  [ValidateRange(1024, 65535)][int]$LaborPort = 5340,
  [ValidateRange(1024, 65535)][int]$LaborSuchPort = 5341,
  [switch]$Entfernen,
  [switch]$Liste
)
$ErrorActionPreference = 'Stop'
$Gruppe = 'KICAD XR'
$LaborGruppe = 'XR-Labor'
$Dotnet = 'C:\Program Files\dotnet\dotnet.exe'

function Meldung([string]$t) { Write-Host $t }
function Fehler([string]$t) { Write-Host "FEHLER: $t" -ForegroundColor Red; exit 1 }

function Anzeigen {
  foreach ($g in @($Gruppe, $LaborGruppe)) {
    $regeln = @(Get-NetFirewallRule -Group $g -ErrorAction SilentlyContinue)
    if ($regeln.Count -eq 0) { Meldung "Keine Regeln der Gruppe '$g'." }
    foreach ($r in $regeln) {
      $p = $r | Get-NetFirewallPortFilter
      $a = $r | Get-NetFirewallAddressFilter
      $prog = ($r | Get-NetFirewallApplicationFilter).Program
      $progText = ''; if ($prog -and $prog -ne 'Any') { $progText = ", Programm $prog" }
      Meldung ("  {0}: {1} {2} {3}, Profil {4}, von {5}, aktiv {6}{7}" -f $r.DisplayName, $r.Direction, $p.Protocol, $p.LocalPort, $r.Profile, ($a.RemoteAddress -join ','), $r.Enabled, $progText)
    }
  }
  $profile = @(Get-NetConnectionProfile -ErrorAction SilentlyContinue)
  foreach ($n in $profile) {
    $farbe = 'Gray'; if ($n.NetworkCategory -ne 'Private') { $farbe = 'Yellow' }
    Write-Host ("  Netz '{0}' ({1}): {2}" -f $n.Name, $n.InterfaceAlias, $n.NetworkCategory) -ForegroundColor $farbe
  }
  if (@($profile | Where-Object { $_.NetworkCategory -eq 'Private' }).Count -eq 0) {
    Write-Host "  Achtung: kein Netz mit Profil 'Privat' - die Regeln greifen erst, wenn das Bueronetz als privat eingestuft ist." -ForegroundColor Yellow
  }
  # Sperrregeln fuer dotnet.exe (z. B. nach "Abbrechen" in der Windows-Abfrage) haben Vorrang vor Freigaben
  $sperren = @(Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue | Where-Object { $_.Program -match 'dotnet\.exe$|Hlks\.Web\.exe$' } | Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object { $_.Action -eq 'Block' -and $_.Direction -eq 'Inbound' -and $_.Enabled -eq 'True' })
  foreach ($s in $sperren) { Write-Host ("  Sperre: '{0}' (Profil {1}) blockiert KICAD bzw. das XR-Labor eingehend." -f $s.DisplayName, $s.Profile) -ForegroundColor Yellow }
}

if ($Liste) { Anzeigen; exit 0 }

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Fehler "Bitte PowerShell als Administrator starten (Rechtsklick > Als Administrator ausfuehren)." }
if ($Port -eq $SuchPort) { Fehler "Port und Suchport muessen verschieden sein." }
if ($LaborPort -eq $LaborSuchPort) { Fehler "Labor-Port und Labor-Suchport muessen verschieden sein." }
if (@($Port, $SuchPort) -contains $LaborPort -or @($Port, $SuchPort) -contains $LaborSuchPort) { Fehler "Die Ports von KICAD und vom XR-Labor muessen verschieden sein." }
if (-not $Entfernen -and -not (Test-Path $Dotnet)) { Fehler "dotnet.exe nicht gefunden: $Dotnet (die Regeln des XR-Labors gelten nur fuer dieses Programm)." }

# vorhandene Regeln der Gruppen entfernen (auch vor dem Anlegen: so gibt es nie doppelte oder veraltete Ports)
foreach ($g in @($Gruppe, $LaborGruppe)) {
  $alt = @(Get-NetFirewallRule -Group $g -ErrorAction SilentlyContinue)
  if ($alt.Count -gt 0) {
    $alt | Remove-NetFirewallRule
    Meldung "$($alt.Count) Regel(n) der Gruppe '$g' entfernt."
  }
}
if ($Entfernen) { Anzeigen; exit 0 }

New-NetFirewallRule -DisplayName "KICAD XR Zugang (HTTPS TCP $Port)" -Group $Gruppe -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port `
  -Profile Private -RemoteAddress LocalSubnet -Description 'KICAD: Zugang der Quest App im Bueronetz (nur /api/xr, nur gekoppelte Brillen). Angelegt mit werkzeuge\xr_firewall.ps1.' | Out-Null
New-NetFirewallRule -DisplayName "KICAD XR Suche (UDP $SuchPort)" -Group $Gruppe -Direction Inbound -Action Allow -Protocol UDP -LocalPort $SuchPort `
  -Profile Private -RemoteAddress LocalSubnet -Description 'KICAD: Suche des PCs durch die Quest App (Antwort nur auf signierte Anfragen). Angelegt mit werkzeuge\xr_firewall.ps1.' | Out-Null
Meldung "Regeln angelegt: TCP $Port und UDP $SuchPort eingehend, nur Profil 'Privat', nur aus dem eigenen Subnetz."
New-NetFirewallRule -DisplayName "XR-Labor Zugang (HTTPS TCP $LaborPort)" -Group $LaborGruppe -Direction Inbound -Action Allow -Protocol TCP -LocalPort $LaborPort `
  -Profile Private -RemoteAddress LocalSubnet -Program $Dotnet -Description 'XR-Labor: Zugang der Labor-App auf der Brille im Bueronetz (nur gekoppelte Brillen). Angelegt mit werkzeuge\xr_firewall.ps1.' | Out-Null
New-NetFirewallRule -DisplayName "XR-Labor Suche (UDP $LaborSuchPort)" -Group $LaborGruppe -Direction Inbound -Action Allow -Protocol UDP -LocalPort $LaborSuchPort `
  -Profile Private -RemoteAddress LocalSubnet -Program $Dotnet -Description 'XR-Labor: Suche des PCs durch die Labor-App (Antwort nur auf signierte Anfragen). Angelegt mit werkzeuge\xr_firewall.ps1.' | Out-Null
Meldung "Regeln fuer das XR-Labor angelegt: TCP $LaborPort und UDP $LaborSuchPort eingehend, nur Profil 'Privat', nur aus dem eigenen Subnetz, nur dotnet.exe."
Anzeigen

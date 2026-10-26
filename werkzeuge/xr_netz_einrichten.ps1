# Netz fuer die Brille einrichten (Christian 26.10.2026: "mache ein skript, dass der nutzer ausloesen kann, um die beiden hinweise zu
# erfuellen"). Erfuellt die zwei Hinweise im Teil "Quest App" bzw. "Brille" (KICAD und XR-Labor, QuestAppNetz.cs):
#   1. Das WLAN bzw. LAN ist als "Oeffentlich" eingestuft -> es wird als "Privat" eingestuft (Set-NetConnectionProfile).
#   2. Die Firewall-Regeln der Gruppe "KICAD XR" fehlen -> werkzeuge\xr_firewall.ps1 legt sie an (KICAD TCP 5140/UDP 5141,
#      XR-Labor TCP 5340/UDP 5341; nur Profil "Privat", nur eigenes Subnetz).
# Eingestuft werden nur Verbindungen ueber echte Netzwerkkarten (Get-NetAdapter -Physical: WLAN, LAN). VPN und virtuelle Adapter (z. B.
# EndianConnect, Hyper-V) bleiben, wie sie sind; Domaenennetze ebenso (die stuft Windows selbst ein).
# Ablauf: zeigt, was es tun wuerde, fragt (J/N) und holt sich dann die Administratorrechte (Windows fragt nach). KICAD und das XR-Labor
# selbst aendern nie Netz- oder Firewall-Einstellungen; dieses Skript startet nur der Benutzer.
# Aufruf: Doppelklick auf werkzeuge\xr_netz_einrichten.cmd, oder
#   powershell -ExecutionPolicy Bypass -File werkzeuge\xr_netz_einrichten.ps1 [-Trocken] [-Ja] [-OhneFirewall]
#   -Trocken       zeigt nur, was geschehen wuerde (ohne Administratorrechte, aendert nichts)
#   -Ja            ohne Rueckfrage
#   -OhneFirewall  nur das Netzprofil einstufen
# Datei bewusst nur ASCII (PowerShell 5.1). Laedt nichts herunter.
param(
  [switch]$Trocken,
  [switch]$Ja,
  [switch]$OhneFirewall,
  [switch]$Erhoeht   # intern: zweiter Lauf mit Administratorrechten
)
$ErrorActionPreference = 'Stop'
$firewall = Join-Path $PSScriptRoot 'xr_firewall.ps1'

function Meldung([string]$t, [string]$farbe = '') { if ($farbe) { Write-Host $t -ForegroundColor $farbe } else { Write-Host $t } }
function Ende([int]$code) { if (-not $Trocken) { Write-Host ''; Read-Host 'Enter zum Schliessen' | Out-Null }; exit $code }

# Oeffentliche Verbindungen ueber echte Netzwerkkarten
function Kandidaten {
  $karten = @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' })
  $profile = @(Get-NetConnectionProfile -ErrorAction SilentlyContinue)
  foreach ($p in $profile) {
    $karte = $karten | Where-Object { $_.InterfaceIndex -eq $p.InterfaceIndex } | Select-Object -First 1
    [pscustomobject]@{
      Name = $p.Name; Schnittstelle = $p.InterfaceAlias; Index = $p.InterfaceIndex; Kategorie = [string]$p.NetworkCategory
      Echt = [bool]$karte
      Aendern = ([bool]$karte -and [string]$p.NetworkCategory -eq 'Public')
    }
  }
}

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$liste = @(Kandidaten)
$aendern = @($liste | Where-Object { $_.Aendern })

Meldung 'Netz fuer die Brille einrichten (KICAD und XR-Labor)' 'Cyan'
Meldung ''
Meldung 'Netzverbindungen:'
foreach ($k in $liste) {
  $was = if ($k.Aendern) { '-> wird als Privat eingestuft' } elseif (-not $k.Echt) { '(VPN bzw. virtuell: bleibt)' } else { '(bleibt)' }
  Meldung ("  {0} ({1}): {2} {3}" -f $k.Name, $k.Schnittstelle, $k.Kategorie, $was)
}
if ($liste.Count -eq 0) { Meldung '  keine Verbindung gefunden' }
Meldung ''
if ($OhneFirewall) { Meldung 'Firewall: unveraendert (-OhneFirewall).' }
elseif (-not (Test-Path $firewall)) { Meldung "FEHLER: $firewall fehlt." 'Red'; Ende 1 }
else { Meldung 'Firewall: Regeln der Gruppe "KICAD XR" anlegen bzw. erneuern (KICAD TCP 5140/UDP 5141, XR-Labor TCP 5340/UDP 5341; nur Profil Privat, nur eigenes Subnetz).' }
Meldung ''
Meldung 'Hinweis: Im Profil "Privat" gilt das Netz als vertrauenswuerdig (wie zu Hause bzw. im Buero); oeffentliche Netze unterwegs bleiben oeffentlich.' 'DarkGray'

if ($Trocken) { Meldung ''; Meldung 'Trockenlauf: nichts geaendert.' 'Yellow'; exit 0 }

if (-not $Erhoeht) {
  if (-not $Ja) {
    Meldung ''
    $antwort = Read-Host 'So einrichten? Windows fragt danach nach Administratorrechten. (J/N)'
    if ($antwort -notmatch '^(j|ja|y|yes)$') { Meldung 'Abgebrochen, nichts geaendert.' 'Yellow'; Ende 0 }
  }
  if (-not $admin) {
    $arg = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Erhoeht', '-Ja')
    if ($OhneFirewall) { $arg += '-OhneFirewall' }
    try { Start-Process -FilePath 'powershell.exe' -ArgumentList $arg -Verb RunAs | Out-Null }
    catch { Meldung 'Die Administratorrechte wurden nicht erteilt. Nichts geaendert.' 'Yellow'; Ende 1 }
    Meldung 'Das Einrichten laeuft im Fenster mit Administratorrechten weiter.'
    exit 0
  }
}
if (-not $admin) { Meldung 'FEHLER: keine Administratorrechte.' 'Red'; Ende 1 }

$fehler = 0
foreach ($k in $aendern) {
  try {
    Set-NetConnectionProfile -InterfaceIndex $k.Index -NetworkCategory Private
    Meldung ("Netz '{0}' ({1}) ist jetzt Privat." -f $k.Name, $k.Schnittstelle) 'Green'
  } catch {
    $fehler++
    Meldung ("Netz '{0}' ({1}) liess sich nicht einstufen: {2}" -f $k.Name, $k.Schnittstelle, $_.Exception.Message) 'Red'
  }
}
if ($aendern.Count -eq 0) { Meldung 'Netzprofil: nichts zu tun (keine oeffentliche Verbindung ueber eine echte Netzwerkkarte).' }

if (-not $OhneFirewall) {
  Meldung ''
  $global:LASTEXITCODE = 0
  try { & $firewall } catch { Meldung "Firewall: $($_.Exception.Message)" 'Red'; $fehler++ }
  if ($LASTEXITCODE -ne 0) { $fehler++ }
}

Meldung ''
if ($fehler -eq 0) { Meldung 'Fertig. In KICAD bzw. im XR-Labor den Teil "Brille" neu laden: die beiden Hinweise sollten verschwunden sein.' 'Green' }
else { Meldung "Fertig mit $fehler Fehler(n), siehe oben." 'Red' }
Ende $(if ($fehler -eq 0) { 0 } else { 1 })

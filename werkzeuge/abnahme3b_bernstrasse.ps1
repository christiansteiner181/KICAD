# Abnahmeszenario Modul 3b «Kollision und Aussparungen» mit Bernstrasse 42 über REST (entwurf/modul-3b.md, «Fertig, wenn …»).
# Gegen einen laufenden KICAD-Server mit einem LEEREN Projekt (lokal, nie auf OneDrive oder W:):
#   1. H+K-IFC (Revit) übernehmen: HLKS-Elemente werden Elemente, die verknüpfte Architektur bleibt Hintergrund (Netzdatei).
#   2. Projektvorgaben nach Christian (19.10.2026): Spiel 50 mm je Seite, Zusammenfassen unter 100 mm, toleranz_hart 5 mm;
#      keine Abstandsprüfung, Kernbohrungen nur von Hand. Typenliste «Typname -> Material» nach Christians Antworten
#      (Fragefenster 19.10.2026, B3d/B3e): Regeln unten in $TypRegeln; die Typnamen kommen aus der Abfrage typmaterial.
#   3. Prüflauf 1, drei Kollisionen von Hand bestätigen, Prüflauf 2 ohne Änderung (Status bleiben, nur der Zähler ändert).
#   4. Aussparungen vorschlagen, nummerieren, die des Erdgeschosses freigeben.
#   5. Ausgaben: Aussparungs-IFC (IFC4 und IFC2X3), BCF 3.0, Kollisionsliste und Aussparungsliste als CSV, Aussparungsplan als PDF, Zahlen als JSON.
# Die echte Datei liegt nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten (nie ins Repo kopieren).
#
# Aufruf:
#   powershell -ExecutionPolicy Bypass -File werkzeuge\abnahme3b_bernstrasse.ps1 -Port 5151 [-Ausgabe <Ordner>] [-ProjektOrdner <leerer Ordner>]
#   -ProjektOrdner: legt dort zuerst ein leeres Projekt an (POST /api/projekt/anlegen) und öffnet es im Server.
# Gesperrte Ports: 5013, 5098, 5130, 5131, 5149, 5150. Exit 0 = fertig, 1 = Abbruch.
# Bezeichner nur ASCII (PowerShell 5.1); Datei UTF-8 mit BOM.

[CmdletBinding(PositionalBinding = $false)]
param(
  [Parameter(Mandatory = $true)][int]$Port,
  [string]$Ausgabe = "$env:USERPROFILE\HLKS_CAD_build\abnahme3b\ausgabe",
  [string]$ProjektOrdner = '',
  [string]$Ifc = "$env:USERPROFILE\HLKS_CAD_testdaten\bernstrasse42\hlks_hk_2026-02-10.ifc",
  [string]$GeschossFreigeben = 'EG'
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }
if (@(5013, 5098, 5130, 5131, 5149, 5150) -contains $Port) { Write-Host "Port $Port ist gesperrt - Abbruch." -ForegroundColor Red; exit 1 }
if (-not (Test-Path $Ifc)) { Write-Host "Echte Datei fehlt: $Ifc" -ForegroundColor Red; exit 1 }
$Basis = "http://localhost:$Port"
New-Item -ItemType Directory -Force $Ausgabe | Out-Null

# ------------------------------------------------------------------ REST
function Post([string]$Pfad, $Koerper) {
  $json = if ($null -eq $Koerper) { '{}' } else { ConvertTo-Json -InputObject $Koerper -Depth 30 -Compress }
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
  try {
    $r = Invoke-WebRequest -Uri ($Basis + $Pfad) -Method Post -Body $bytes -ContentType 'application/json; charset=utf-8' -UseBasicParsing -TimeoutSec 1800
    $text = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
  } catch [System.Net.WebException] {
    if ($null -eq $_.Exception.Response) { throw "Server $Basis antwortet nicht: $($_.Exception.Message)" }
    $s = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream(), [System.Text.Encoding]::UTF8)
    $text = $s.ReadToEnd(); $s.Close()
    $o = $null; try { $o = $text | ConvertFrom-Json } catch { }
    if ($null -ne $o -and $o.fehler) { throw "$($o.fehler) | Vorschlag: $($o.vorschlag)" }
    throw "HTTP-Fehler $Pfad : $text"
  }
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  return $text | ConvertFrom-Json
}
function Abfrage([string]$Name, $Par) { return Post "/api/abfrage/$Name" $Par }

$Nummer = 0
$Uhr = [System.Diagnostics.Stopwatch]::new()
$Zahlen = [ordered]@{ zeit = (Get-Date -Format 'yyyy-MM-dd HH:mm'); hinweis = 'Vorgaben Christian 19.10.2026: Spiel 50, Zusammenfassen 100, toleranz_hart 5 mm' }

function Befehl([string]$Titel, [string]$Name, $Par) {
  $script:Nummer++
  $script:Uhr.Restart()
  try { $e = Post "/api/befehl/$Name" $Par }
  catch { Write-Host "[$script:Nummer] $Titel ($Name): FEHLER $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
  $s = [Math]::Round($script:Uhr.Elapsed.TotalSeconds, 1)
  if (-not $e.angenommen) {
    Write-Host "[$script:Nummer] $Titel ($Name): ABGELEHNT" -ForegroundColor Red
    Write-Host "    Fehler:    $($e.fehler)" -ForegroundColor Red
    Write-Host "    Vorschlag: $($e.vorschlag)" -ForegroundColor Yellow
    exit 1
  }
  Write-Host "[$script:Nummer] $Titel ($Name, $s s): $($e.meldung)" -ForegroundColor Cyan
  foreach ($h in @($e.hinweise | Select-Object -First 12)) { if ($h) { Write-Host "    $h" } }
  $e | Add-Member -NotePropertyName sekunden -NotePropertyValue $s -Force
  return $e
}

function Csv([string]$Datei, $Zeilen) {
  $Zeilen | ConvertTo-Csv -NoTypeInformation -Delimiter ';' | Out-File -FilePath $Datei -Encoding utf8
}

# ------------------------------------------------------------------ 0. Server und Projekt
try { $status = Invoke-RestMethod "$Basis/api/status" -TimeoutSec 20 } catch { Write-Host "Server $Basis antwortet nicht." -ForegroundColor Red; exit 1 }
if ($ProjektOrdner) {
  New-Item -ItemType Directory -Force $ProjektOrdner | Out-Null
  if (Test-Path (Join-Path $ProjektOrdner 'projekt.json')) { Write-Host "In $ProjektOrdner liegt schon ein Projekt - Abbruch (leeren Ordner angeben)." -ForegroundColor Red; exit 1 }
  $null = Post '/api/projekt/anlegen' @{ ordner = $ProjektOrdner; name = 'Abnahme 3b Bernstrasse 42' }
  Write-Host "Projekt angelegt: $ProjektOrdner" -ForegroundColor Cyan
}
$vorhanden = Abfrage 'kollisionen' @{}
if (@($vorhanden.zeilen).Count -gt 0) { Write-Host "Das offene Projekt hat schon Kollisionen - Abbruch (leeres Projekt verwenden)." -ForegroundColor Red; exit 1 }

# ------------------------------------------------------------------ 1. IFC übernehmen
$e = Befehl 'H+K-IFC Bernstrasse 42 uebernehmen (nur HLKS)' 'ifc_uebernehmen' @{ datei = $Ifc; bereiche = @('hlks') }
$ul = @($e.aenderungen | Where-Object { $_.klasse -eq 'Unterlage' -and $_.art -eq 'angelegt' } | Select-Object -First 1)
if ($ul.Count -eq 0) { Write-Host 'Keine Unterlage angelegt - Abbruch.' -ForegroundColor Red; exit 1 }
$UL = $ul[0].kennzeichen
$Zahlen.uebertrag = [ordered]@{ sekunden = $e.sekunden; unterlage = $UL; aenderungen = @($e.aenderungen).Count }

# ------------------------------------------------------------------ 2. Vorgaben (Christian Steiner, 19.10.2026)
$q = 'Vorgabe Christian Steiner, H+K Planungs AG, 19.10.2026'
$e = Befehl 'Projektvorgaben setzen (Christian 19.10.2026)' 'vorgaben_setzen' @{ werte = @{
  spiel_aussparung = @{ wert = 50; quelle = $q }
  abstand_zusammenfassen = @{ wert = 100; quelle = $q }
  toleranz_hart = @{ wert = 5; quelle = $q }
} }

# Typenliste (Christian 19.10.2026): Muster auf den Anfang des Typnamens (ohne «Familie:»), Kategorie, Zusatz zur Quelle
$qt = 'Christian Steiner, H+K Planungs AG, 19.10.2026 (Fragefenster Abnahme 3b)'
$TypRegeln = @(
  @('^2\.3_IW 1\.5/8/1\.5', 'leichtbau', ''),
  @('^_IW GIS', 'installationsvorwand', 'Geberit GIS'),
  @('^5\.1_Geschossdecke UB', 'bodenaufbau', 'UB = Bodenaufbau'),
  @('^5\.1\.1_Geschossdecke UB', 'bodenaufbau', 'UB = Bodenaufbau'),
  @('^5\.2_UB', 'bodenaufbau', 'UB = Bodenaufbau'),
  @('^5\.0_Geschossdecke Tragkonstruktion', 'beton', ''),
  @('^2\.2_IW Vorsatzschale', 'leichtbau', 'Vorsatzschale = Leichtbau'),
  @('^2\.3_IW Vorsatzschale', 'leichtbau', 'Vorsatzschale = Leichtbau'),
  @('^1\.2_AW WDVS', 'beton', 'Rohbau (Beton/Mauerwerk nicht unterschieden)'),
  @('^4\.0_DA Flachdach Tragkonstruktion', 'beton', ''),
  @('^Unterzug 24/28', 'beton', ''),
  @('^6\.0_Boden Loggia warm', 'bodenaufbau', '')
)
$tm = Abfrage 'typmaterial' @{}
$schluessel = @(@($tm.unbekannt) + @($tm.ausTypname) | ForEach-Object { $_.schluessel } | Where-Object { $_ } | Sort-Object -Unique)
$typen = @()
foreach ($r in $TypRegeln) {
  $treffer = @($schluessel | Where-Object { $_ -match $r[0] })
  if ($treffer.Count -eq 0) { Write-Host "    Typenliste: kein Typ fuer $($r[0])" -ForegroundColor Yellow; continue }
  foreach ($t in $treffer) {
    $quelle = if ($r[2]) { "$qt - $($r[2])" } else { $qt }
    $typen += @{ typname = $t; kategorie = $r[1]; quelle = $quelle }
  }
}
if ($typen.Count -gt 0) { $e = Befehl "Typenliste setzen ($($typen.Count) Typen)" 'typmaterial_setzen' @{ typen = $typen } }
$Zahlen.typenliste = @($typen | ForEach-Object { "$($_.typname) -> $($_.kategorie)" })

# ------------------------------------------------------------------ 3. Zwei Prüfläufe
$e1 = Befehl 'Prueflauf 1: HLKS gegen Architektur und HLKS' 'kollision_pruefen' @{ unterlagen = @($UL) }
# Plankopf: Projektinformation (der Prueflauf legt sie mit dem Laufzaehler an); misslingt es, laeuft das Szenario weiter
try {
  $pi = Post '/api/befehl/element_aendern' @{ element = 'PI-1'; eigenschaften = @{ phase = 'Ausführungsprojekt (Abnahme KICAD)'; zeile1 = 'MFH Bernstrasse 42'; zeile2 = 'Worb'; projektnummer = '1-0768' } }
  if (-not $pi.angenommen) { Write-Host "    Projektinformation: $($pi.fehler)" -ForegroundColor Yellow }
} catch { Write-Host "    Projektinformation nicht gesetzt: $($_.Exception.Message)" -ForegroundColor Yellow }
$liste1 = Abfrage 'kollisionen' @{}
$drei = @($liste1.zeilen | Select-Object -First 3 | ForEach-Object { $_.kollision })
if ($drei.Count -gt 0) {
  $null = Befehl 'Drei Kollisionen von Hand bestaetigen' 'kollision_status' @{ kollisionen = $drei; status = 'bestaetigt'; kommentar = 'Abnahme 3b: von Hand bestaetigt, muss im zweiten Lauf bleiben' }
}
$e2 = Befehl 'Prueflauf 2 ohne Aenderung' 'kollision_pruefen' @{ unterlagen = @($UL) }
$liste2 = Abfrage 'kollisionen' @{}
$bleiben = @($liste2.zeilen | Where-Object { $drei -contains $_.kollision -and $_.status -eq 'bestaetigt' }).Count
$Zahlen.lauf1 = [ordered]@{ sekunden = $e1.sekunden; aenderungen = @($e1.aenderungen).Count; zusammenfassung = $liste1.zusammenfassung }
$Zahlen.lauf2 = [ordered]@{ sekunden = $e2.sekunden; aenderungen = @($e2.aenderungen).Count; zusammenfassung = $liste2.zusammenfassung; kollisionslauf = $liste2.kollisionslauf; bestaetigtGeblieben = "$bleiben von $($drei.Count)" }
Write-Host "    Lauf 2: $(@($e2.aenderungen).Count) Aenderung(en); von Hand bestaetigt geblieben: $bleiben von $($drei.Count)"
Csv (Join-Path $Ausgabe 'kollisionsliste.csv') @($liste2.zeilen)

# ------------------------------------------------------------------ 4. Aussparungen
$e = Befehl 'Aussparungen vorschlagen' 'aussparungen_vorschlagen' @{}
$null = Befehl 'Aussparungen nummerieren' 'aussparungen_nummerieren' @{}
$al = Abfrage 'aussparungsliste' @{}
$eg = @($al.zeilen | Where-Object { $_.geschoss -eq $GeschossFreigeben } | ForEach-Object { if ($_.nummer) { $_.nummer } else { $_.aussparung } })
if ($eg.Count -gt 0) { $null = Befehl "Aussparungen $GeschossFreigeben freigeben ($($eg.Count))" 'aussparung_status' @{ aussparungen = $eg; status = 'freigegeben' } }
$al = Abfrage 'aussparungsliste' @{}
Csv (Join-Path $Ausgabe 'aussparungsliste.csv') @($al.zeilen)
$Zahlen.aussparungen = [ordered]@{ sekunden = $e.sekunden; anzahl = @($al.zeilen).Count; freigegeben = $eg.Count; zusammenfassung = $al.zusammenfassung }

# ------------------------------------------------------------------ 5. Ausgaben
foreach ($f in @('IFC4', 'IFC2X3')) {
  $d = Join-Path $Ausgabe "aussparungen_$($f.ToLower()).ifc"
  $Uhr.Restart(); $b = Abfrage 'aussparungen_ifc' @{ datei = $d; fassung = $f; status = @('vorgeschlagen', 'freigegeben') }
  Write-Host "[IFC] $f -> $d ($([Math]::Round($Uhr.Elapsed.TotalSeconds,1)) s): $($b.anzahl) Aussparung(en), Rundreise $($b.rundreise.ergebnis)" -ForegroundColor Cyan
  foreach ($w in @($b.warnungen | Select-Object -First 5)) { if ($w) { Write-Host "    Warnung: $w" -ForegroundColor Yellow } }
  $Zahlen["ifc_$f"] = $b
}
$bcf = Join-Path $Ausgabe 'kollisionen.bcf'
$Uhr.Restart(); $b = Abfrage 'bcf_export' @{ datei = $bcf }
Write-Host "[BCF] -> $bcf ($([Math]::Round($Uhr.Elapsed.TotalSeconds,1)) s): $(@($b.kollisionen).Count) Topic(s)" -ForegroundColor Cyan
$Zahlen.bcf = [ordered]@{ datei = $bcf; topics = @($b.kollisionen).Count }

# Aussparungsplan des freigegebenen Geschosses als PDF (Grundriss mit darstellung «aussparungen», Planblatt A1)
$kzAnsicht = "AP-$GeschossFreigeben"
$null = Befehl "Aussparungsplan $GeschossFreigeben anlegen" 'element_anlegen' @{ klasse = 'Ansicht'; kennzeichen = $kzAnsicht; name = "Aussparungsplan $GeschossFreigeben"; geschoss = $GeschossFreigeben; eigenschaften = @{ art = 'grundriss'; massstab = 50; schnitthoehe = 1000; darstellung = 'aussparungen' } }   # 1:50 wie Vorlage H+K
$e = Befehl 'Planblatt anlegen' 'planblatt_anlegen' @{ nummer = 'AP-101'; titel = "Aussparungsplan $GeschossFreigeben (Abnahme 3b)"; format = 'A1' }
$pb = @($e.aenderungen | Where-Object { $_.klasse -eq 'Planblatt' })[0].kennzeichen
$null = Befehl 'Ansicht platzieren' 'ansicht_platzieren' @{ planblatt = $pb; ansicht = $kzAnsicht; lage = @(420, 297) }
$pdf = Join-Path $Ausgabe "aussparungsplan_$GeschossFreigeben.pdf"
$Uhr.Restart(); $b = Abfrage 'plan_pdf' @{ planblatt = $pb; datei = $pdf }
Write-Host "[PDF] -> $pdf ($([Math]::Round($Uhr.Elapsed.TotalSeconds,1)) s): $($b.seiten) Seite(n)" -ForegroundColor Cyan
$Zahlen.plan = [ordered]@{ datei = $pdf; planblatt = $pb; ansicht = $kzAnsicht }
$Zahlen | ConvertTo-Json -Depth 12 | Out-File -FilePath (Join-Path $Ausgabe 'abnahme3b_zahlen.json') -Encoding utf8
Write-Host "Fertig. Ausgaben in $Ausgabe" -ForegroundColor Green
exit 0

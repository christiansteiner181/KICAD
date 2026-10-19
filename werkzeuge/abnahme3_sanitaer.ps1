# Abnahmeszenario Modul 3 «Sanitärstrang im Beispielprojekt» über REST (entwurf/modul-3.md, «Fertig, wenn …» 1 und 3).
# Zeichnet den Strang Dusche/WC (SW-F2, KW-S2, Apparate WC/WT/DU *.03 in EG und 1.OG) mit Leitungsserien neu und ergänzt
# Trinkwasser warm (System WW, Strang WW-S2, Wassererwärmer WE-1 im UG als Quelle) – dieselben Schritte wie der C#-Test
# tests/Hlks.Kern.Tests/Abnahme3SanitaerTests.cs, hier gegen einen laufenden KICAD-Server mit offenem Beispielprojekt.
#
# Aufruf (Beispiel mit den fiktiven Testserien):
#   powershell -ExecutionPolicy Bypass -File werkzeuge\abnahme3_sanitaer.ps1 -Port 5165 `
#     -SerieTw fam-test-rohrserie -SerieSw fam-test-abwasserserie `
#     -GroesseTwSteig "DN 20" -GroesseTwAnschluss "DN 15" -GroesseSwFall "DN 100" -GroesseSwWc "DN 100" -GroesseSwKlein "DN 50"
# Mit echten Serien die Typnamen genau wie in der Serie angeben (z. B. -GroesseTwSteig "22 × 1.2"; die Abfrage leitungsserie zeigt sie).
#
# Parameter:
#   -Port                Port des KICAD-Servers (Pflicht). Verboten: 5013, 5098, 5130, 5131 (Abbruch).
#   -SerieTw, -SerieSw   Leitungsserien Trinkwasser bzw. Schmutzwasser: Familien-Kennzeichen, Name oder Definitions-id.
#   -GroesseTwSteig      Typname Steigstränge KW-S2/WW-S2 und Zuleitung WE-1.
#   -GroesseTwAnschluss  Typname Etagen- und Anschlussleitungen (Reduktion am Etagenabgang, wenn kleiner als Steig).
#   -GroesseSwFall       Typname Fallstrang SW-F2.
#   -GroesseSwWc         Typname WC-Anschlüsse (weicht er vom Fallstrang ab, setzt leitung_groesse am WC 1.OG eine Reduktion).
#   -GroesseSwKlein      Typname Dusche und Waschtisch.
#   -Gefaelle            Gefälle Schmutzwasser in % (Vorgabe 2).
#   -Trockenlauf         nur Vorschau: prüft Server, Serien, Grössen und Elemente, zeigt den Altbestand mit Trockenlauf des
#                        Löschens und den Plan; ändert nichts. Die Zeichenschritte bauen auf den vorherigen auf und lassen sich
#                        erst nach deren Ausführung rechnen – volle Vorschau: Skript gegen ein neues Beispielprojekt auf
#                        einem eigenen Server laufen lassen.
#   -FamilieDatei        (optional) Familiendateien, die vorher mit familie_laden geladen werden (z. B. die Testserien);
#                        mit powershell -File mehrere durch Komma getrennt in einem Argument.
#   -OhneWeZuleitung     (optional) keine Kaltwasser-Zuleitung zum Wassererwärmer (WE-1.kw bleibt offen, P01-Hinweis).
#
# Ausgabe: je Schritt Meldung und Hinweise des Kerns; bei Ablehnung Fehler und Vorschlag des Kerns, dann Abbruch (Exit 1).
# Am Ende Prüfbefunde P13–P18 der neuen Elemente (dazu P01 der neuen Elemente und Apparate *.03) und die Materialliste je
# Strang. Exit 0 = fertig ohne Befund P13–P16, 2 = fertig mit Befunden P13–P16, 1 = Abbruch.
# Lagen (mm) wie im C#-Test, FIKTIV und frei gewählt nahe dem Altbestand: Fallstrang (10200, 9800), KW-S2 (10300, 9700),
# WW-S2 (10400, 9600), WE-1 (10350, 8800, -2700). Bezeichner bewusst nur ASCII (PowerShell 5.1); Datei UTF-8 mit BOM.

[CmdletBinding(PositionalBinding = $false)]
param(
  [Parameter(Mandatory = $true)][int]$Port,
  [Parameter(Mandatory = $true)][string]$SerieTw,
  [Parameter(Mandatory = $true)][string]$SerieSw,
  [string]$GroesseTwSteig = 'DN 20',
  [string]$GroesseTwAnschluss = 'DN 15',
  [string]$GroesseSwFall = 'DN 100',
  [string]$GroesseSwWc = 'DN 100',
  [string]$GroesseSwKlein = 'DN 50',
  [double]$Gefaelle = 2,
  [switch]$Trockenlauf,
  [string[]]$FamilieDatei = @(),
  [switch]$OhneWeZuleitung
)

$ErrorActionPreference = 'Stop'
$FamilieDatei = @($FamilieDatei | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

if (@(5013, 5098, 5130, 5131) -contains $Port) {
  Write-Host "Port $Port ist für dieses Skript gesperrt (5013, 5098, 5130, 5131) - Abbruch." -ForegroundColor Red
  exit 1
}
$Basis = "http://localhost:$Port"
$Kultur = [System.Globalization.CultureInfo]::InvariantCulture

# ------------------------------------------------------------------ REST

function Lies-Antwort($Antwort) {
  $s = $Antwort.GetResponseStream()
  $r = New-Object System.IO.StreamReader($s, [System.Text.Encoding]::UTF8)
  $t = $r.ReadToEnd(); $r.Close()
  return $t
}

# POST mit JSON; liefert das geparste Objekt. Bei HTTP-Fehler: wirft mit fehler/vorschlag des Kerns.
function Post([string]$Pfad, $Koerper) {
  $json = if ($null -eq $Koerper) { '{}' } else { ConvertTo-Json -InputObject $Koerper -Depth 30 -Compress }
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
  try {
    $r = Invoke-WebRequest -Uri ($Basis + $Pfad) -Method Post -Body $bytes -ContentType 'application/json; charset=utf-8' -UseBasicParsing -TimeoutSec 600
    $text = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
  } catch [System.Net.WebException] {
    if ($null -eq $_.Exception.Response) { throw "Server $Basis antwortet nicht: $($_.Exception.Message)" }
    $text = Lies-Antwort $_.Exception.Response
    $o = $null; try { $o = $text | ConvertFrom-Json } catch { }
    if ($null -ne $o -and $o.fehler) { throw "$($o.fehler) | Vorschlag: $($o.vorschlag)" }
    throw "HTTP-Fehler $Pfad : $text"
  }
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  return $text | ConvertFrom-Json
}

function Abfrage([string]$Name, $Par) { return Post "/api/abfrage/$Name" $Par }

# Abfrage, die fehlen darf: $null statt Ausnahme
function Abfrage-Leise([string]$Name, $Par) { try { return Abfrage $Name $Par } catch { return $null } }

function Gibt-Es([string]$Kz) { return $null -ne (Abfrage-Leise 'element_lesen' @{ element = $Kz }) }

$Neu = New-Object 'System.Collections.Generic.List[string]'          # alle im Lauf angelegten Kennzeichen
$NeuRohre = New-Object 'System.Collections.Generic.List[string]'     # angelegte Leitungen
$RohreJeSchritt = @{}
$GeoCache = @{}
$Nummer = 0

# Befehl ausführen (bzw. Trockenlauf), Meldung und Hinweise ausgeben, bei Ablehnung mit Vorschlag abbrechen.
function Befehl([string]$Titel, [string]$Name, $Par, [switch]$Vorschau) {
  $script:Nummer++
  $q = if ($Vorschau) { '?trockenlauf=true' } else { '' }
  try { $e = Post "/api/befehl/$Name$q" $Par }
  catch { Write-Host "[$script:Nummer] $Titel ($Name): FEHLER $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
  if (-not $e.angenommen) {
    Write-Host "[$script:Nummer] $Titel ($Name): ABGELEHNT" -ForegroundColor Red
    Write-Host "    Fehler:    $($e.fehler)" -ForegroundColor Red
    Write-Host "    Vorschlag: $($e.vorschlag)" -ForegroundColor Yellow
    Write-Host "    Parameter: $(ConvertTo-Json -InputObject $Par -Depth 30 -Compress)"
    exit 1
  }
  $marke = if ($e.trockenlauf) { ' [Trockenlauf]' } else { '' }
  Write-Host "[$script:Nummer] $Titel ($Name)${marke}: $($e.meldung)" -ForegroundColor Cyan
  foreach ($h in @($e.hinweise)) { if ($h) { Write-Host "    $h" } }
  if (-not $e.trockenlauf) {
    $rohre = New-Object 'System.Collections.Generic.List[string]'
    foreach ($a in @($e.aenderungen)) {
      if ($null -eq $a) { continue }
      if ($a.klasse -eq 'Rohr' -or $a.klasse -eq 'Kanal') { $script:GeoCache.Remove($a.kennzeichen) }
      if ($a.art -eq 'angelegt' -and $a.klasse -ne 'Familie') {
        $script:Neu.Add($a.kennzeichen)
        if ($a.klasse -eq 'Rohr' -or $a.klasse -eq 'Kanal') { $script:NeuRohre.Add($a.kennzeichen); $rohre.Add($a.kennzeichen) }
      }
    }
    $script:RohreJeSchritt[$Titel] = $rohre
  }
  return $e
}

# ------------------------------------------------------------------ Geometrie-Hilfen

function Punkte { $l = New-Object System.Collections.ArrayList; foreach ($p in $args) { [void]$l.Add([double[]]$p) }; , $l }
function P3([double]$x, [double]$y, [double]$z) { , ([double[]]@($x, $y, $z)) }
function An([string]$Element, [string]$Anschluss) { return @{ element = $Element; anschluss = $Anschluss } }

function Geo([string]$Kz) {
  if (-not $script:GeoCache.ContainsKey($Kz)) {
    $o = Abfrage-Leise 'element_lesen' @{ element = $Kz }
    $script:GeoCache[$Kz] = if ($null -ne $o -and $null -ne $o.geometrie) { @($o.geometrie.punkte) } else { $null }
  }
  return $script:GeoCache[$Kz]
}

# Gerade unter den Kandidaten, deren Achse den Punkt enthält (z = $null: im Grundriss) – genau eine, sonst Abbruch.
function Gerade([double]$x, [double]$y, $z, $Kandidaten) {
  if ($null -eq $Kandidaten) { $Kandidaten = $script:NeuRohre }
  $treffer = @()
  foreach ($kz in @($Kandidaten)) {
    $pt = Geo $kz
    if ($null -eq $pt -or $pt.Count -ne 2) { continue }
    $ax = [double]$pt[0][0]; $ay = [double]$pt[0][1]; $az = [double]$pt[0][2]
    $bx = [double]$pt[1][0]; $by = [double]$pt[1][1]; $bz = [double]$pt[1][2]
    if ($null -eq $z) { $az = 0; $bz = 0; $qz = 0 } else { $qz = [double]$z }
    $dx = $bx - $ax; $dy = $by - $ay; $dz = $bz - $az
    $l = [math]::Sqrt($dx * $dx + $dy * $dy + $dz * $dz)
    if ($l -lt 1e-6) { continue }
    $ux = $dx / $l; $uy = $dy / $l; $uz = $dz / $l
    $t = ($x - $ax) * $ux + ($y - $ay) * $uy + ($qz - $az) * $uz
    if ($t -le 1 -or $t -ge $l - 1) { continue }
    $px = $ax + $ux * $t - $x; $py = $ay + $uy * $t - $y; $pz = $az + $uz * $t - $qz
    if ([math]::Sqrt($px * $px + $py * $py + $pz * $pz) -le 2) { $treffer += $kz }
  }
  if ($treffer.Count -ne 1) {
    Write-Host "Gerade bei [$x, $y, $z] nicht eindeutig gefunden ($($treffer.Count) Treffer: $($treffer -join ', ')) - Abbruch." -ForegroundColor Red
    Write-Host "    Vorschlag: Verlauf der vorherigen Schritte prüfen (leitungszug, element_lesen); die Serie hat womöglich andere Einbaulängen." -ForegroundColor Yellow
    exit 1
  }
  return $treffer[0]
}

function Z([double]$x) { return $x.ToString('0.###', $Kultur) }

# ------------------------------------------------------------------ Server, Projekt, Familien

try { $status = Invoke-RestMethod -Uri "$Basis/api/status" -UseBasicParsing -TimeoutSec 15 }
catch { Write-Host "Server $Basis antwortet nicht: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
if (-not $status.offen) { Write-Host "Auf $Basis ist kein Projekt offen - zuerst ein Projekt öffnen bzw. anlegen (POST /api/projekt/anlegen { ordner, name, beispiel: true })." -ForegroundColor Red; exit 1 }
$modus = if ($Trockenlauf) { 'Trockenlauf (nur Vorschau)' } else { 'Ausführen' }
Write-Host "KICAD $Basis - Projekt «$($status.projekt)» ($($status.ort)) - $modus" -ForegroundColor Green
Write-Host "Serien: Trinkwasser «$SerieTw» (Steig $GroesseTwSteig, Anschluss $GroesseTwAnschluss), Schmutzwasser «$SerieSw» (Fall $GroesseSwFall, WC $GroesseSwWc, klein $GroesseSwKlein), Gefälle $(Z $Gefaelle) %"

foreach ($f in $FamilieDatei) { [void](Befehl "Familie $([System.IO.Path]::GetFileName($f))" 'familie_laden' @{ datei = $f } -Vorschau:$Trockenlauf) }

# Vorprüfung: Serien mit den Grössen, Elemente des Beispielprojekts
$fehlt = @()
$serienPruefen = -not ($Trockenlauf -and $FamilieDatei.Count -gt 0)
if (-not $serienPruefen) { Write-Host 'Trockenlauf: Familien nur als Vorschau geladen - Prüfung der Serien und Grössen übersprungen.' -ForegroundColor Yellow }
foreach ($s in @(@($SerieTw, @($GroesseTwSteig, $GroesseTwAnschluss)), @($SerieSw, @($GroesseSwFall, $GroesseSwWc, $GroesseSwKlein)))) {
  if (-not $serienPruefen) { break }
  $serie = $null
  try { $serie = Abfrage 'leitungsserie' @{ familie = $s[0] } }
  catch { $fehlt += "Leitungsserie «$($s[0])»: $($_.Exception.Message)"; continue }
  $typen = @($serie.groessen | ForEach-Object { $_.typ })
  foreach ($g in $s[1]) { if ($typen -notcontains $g) { $fehlt += "Serie «$($s[0])» hat keinen Typ «$g» (Typen: $($typen -join ', '))" } }
  Write-Host "Serie «$($s[0])»: $($typen.Count) Grössen ($($typen -join ', ')); Bogenwinkel $(@($serie.winkel) -join '/'), Abzweigwinkel $(@($serie.abzweigwinkel) -join '/')"
}
foreach ($kz in @('A-SAN', 'SW', 'KW', 'SW-F2', 'KW-S2', 'TS-S1-F2', 'BO-KW-V1', 'WC-E.03', 'WT-E.03', 'DU-E.03', 'WC-1.03', 'WT-1.03', 'DU-1.03')) {
  if (-not (Gibt-Es $kz)) { $fehlt += "Element $kz fehlt (Beispielprojekt MFH Musterweg erwartet)" }
}
if ($fehlt.Count -gt 0) {
  Write-Host 'Vorprüfung nicht bestanden:' -ForegroundColor Red
  foreach ($x in $fehlt) { Write-Host "    $x" -ForegroundColor Red }
  Write-Host '    Vorschlag: Serien mit familie_laden laden (oder -FamilieDatei), Typnamen wie in der Abfrage leitungsserie angeben.' -ForegroundColor Yellow
  exit 1
}

# ------------------------------------------------------------------ 0. Altbestand entfernen

# Leitungen und Formteile der Stränge SW-F2, KW-S2 und (bei einem zweiten Lauf) WW-S2; Geräte und Apparate bleiben.
$straenge = @(@('SW', 'SW-F2'), @('KW', 'KW-S2'))
if (Gibt-Es 'WW-S2') { $straenge += , @('WW', 'WW-S2') }
$alt = @()
foreach ($st in $straenge) {
  foreach ($klasse in @('Leitung', 'Formteil')) {
    $seite = 1
    do {
      $r = Abfrage 'elemente_suchen' @{ klasse = $klasse; system = $st[0]; proSeite = 500; seite = $seite }
      foreach ($e in @($r.elemente)) { if ($null -ne $e -and $e.teilVon -eq $st[1]) { $alt += $e.kennzeichen } }
      $seite++
    } while ($seite -le [int]$r.seiten)
  }
}
$alt = @($alt | Sort-Object -Unique)
Write-Host "Altbestand der Stränge $(@($straenge | ForEach-Object { $_[1] }) -join ', '): $($alt.Count) Leitungen und Formteile - $($alt -join ', ')" -ForegroundColor Green
foreach ($kz in $alt) { [void](Befehl "Altbestand $kz entfernen" 'element_loeschen' @{ element = $kz } -Vorschau:$Trockenlauf) }

# ------------------------------------------------------------------ 1. Trinkwasser warm: System, Strang, Wassererwärmer

$wwDa = Gibt-Es 'WW'
if (-not $wwDa) {
  [void](Befehl 'WW System' 'element_anlegen' @{ klasse = 'System'; kennzeichen = 'WW'; name = 'Trinkwasser warm'; teilVon = 'A-SAN'; eigenschaften = @{ systemtyp = 'Trinkwasser warm' } } -Vorschau:$Trockenlauf)
} else { Write-Host 'System WW gibt es schon - bleibt.' }

if ($Trockenlauf) {
  Write-Host ''
  Write-Host 'Trockenlauf: Die weiteren Schritte bauen auf den vorherigen auf (gelöschter Altbestand, System WW, neue Leitungen) und' -ForegroundColor Yellow
  Write-Host 'lassen sich erst nach deren Ausführung rechnen. Geplant (ohne Ausführung):' -ForegroundColor Yellow
  $plan = @(
    'Strang WW-S2 (steigstrang), Wassererwärmer WE-1 bei (10350, 8800, -2700) teilVon WW-S2',
    "S1 Fallstrang SW-F2 ${GroesseSwFall}: von WC-1.03.sw (10200, 9500, 3300) -> (10200, 9800) Bogen -> senkrecht bis TS-S1-F2.c (z -524), $(Z $Gefaelle) %",
    "je Geschoss: Dusche $GroesseSwKlein per Abzweig am Fallstrang bei z+60 über (10800, 9800), (11300, 9300); Waschtisch per Abzweig bei (11000, 9600); EG: WC $GroesseSwWc per Abzweig bei z 400",
    "KW-S2 Steigstrang $GroesseTwSteig ab BO-KW-V1.b (10300, 9700, -500) bis 3300; Etagen $GroesseTwAnschluss (1.OG ab Steigstrangende, EG per Abzweig bei 600); Zuleitung WE-1 per Abzweig bei -400",
    'KW-S2 je Geschoss Abzweige Dusche (x 11250) und WC (x 10500)',
    "WW-S2 Steigstrang $GroesseTwSteig ab WE-1.ww (10400, 8800, -2000) über (10400, 9600, -600) bis 3400; Etagen $GroesseTwAnschluss (1.OG ab Ende, EG per Abzweig bei 700); je Geschoss Abzweig Dusche (x 11100)")
  foreach ($x in $plan) { Write-Host "    - $x" }
  Write-Host 'Volle Vorschau: das Skript gegen ein neues Beispielprojekt auf einem eigenen Server laufen lassen. Nichts geändert.' -ForegroundColor Yellow
  exit 0
}

if (-not (Gibt-Es 'WW-S2')) {
  [void](Befehl 'WW-S2 Strang' 'element_anlegen' @{ klasse = 'Strang'; kennzeichen = 'WW-S2'; name = 'Steigstrang Dusche/WC'; teilVon = 'WW'; eigenschaften = @{ art = 'steigstrang' } })
} else { Write-Host 'Strang WW-S2 gibt es schon - bleibt.' }
if (-not (Gibt-Es 'WE-1')) {
  [void](Befehl 'WE-1 Wassererwaermer' 'element_anlegen' @{ klasse = 'Wassererwaermer'; kennzeichen = 'WE-1'; name = 'Wassererwärmer Dusche/WC (fiktiv)'; teilVon = 'WW-S2'; geometrie = @{ art = 'punkt'; punkte = (Punkte @(10350, 8800, -2700)) } })
} else { Write-Host 'WE-1 gibt es schon - wird angeschlossen.' }

# ------------------------------------------------------------------ 2. Schmutzwasser SW-F2

$XF = 10200; $YF = 9800; $XK = 10300; $YK = 9700; $XW = 10400; $YW = 9600
$geschosse = @(@('1', 2900), @('E', 0))

[void](Befehl 'S1 Fallstrang SW-F2' 'leitung_zeichnen' @{
    serie = $SerieSw; groesse = $GroesseSwFall; gefaelle = $Gefaelle; strang = 'SW-F2'; praefix = 'SW-F2'
    von = (An 'WC-1.03' 'sw'); nach = (An 'TS-S1-F2' 'c')
    punkte = (Punkte @($XF, 9500, 3300) @($XF, $YF, 3300) @($XF, $YF, -518)) })
if ($GroesseSwWc -ne $GroesseSwFall) {
  $wc1 = Gerade $XF 9600 $null $RohreJeSchritt['S1 Fallstrang SW-F2']
  [void](Befehl 'S1 WC 1.OG Grösse' 'leitung_groesse' @{ leitung = $wc1; groesse = $GroesseSwWc })
}
foreach ($g in $geschosse) {
  $k = $g[0]; $z = [double]$g[1]
  $du = "S $k.03 DU"
  [void](Befehl $du 'leitung_abzweigen' @{
      leitung = (Gerade $XF $YF ($z + 60)); punkt = (P3 $XF $YF ($z + 60))
      groesse = $GroesseSwKlein; gefaelle = $Gefaelle; strang = 'SW-F2'; praefix = "SW-$k.03-DU"
      punkte = (Punkte @(10800, $YF, ($z + 60)) @(11300, 9300, ($z + 60)) @(11300, 6800, ($z + 60))); ende = (An "DU-$k.03" 'sw') })
  [void](Befehl "S $k.03 WT" 'leitung_abzweigen' @{
      leitung = (Gerade 11000 9600 $null $RohreJeSchritt[$du]); punkt = @(11000, 9600)
      groesse = $GroesseSwKlein; gefaelle = $Gefaelle; strang = 'SW-F2'; praefix = "SW-$k.03-WT"
      punkte = (Punkte @(11500, 9600)); ende = (An "WT-$k.03" 'sw') })
  if ($k -eq 'E') {
    [void](Befehl 'S E.03 WC' 'leitung_abzweigen' @{
        leitung = (Gerade $XF $YF ($z + 400)); punkt = (P3 $XF $YF ($z + 400))
        groesse = $GroesseSwWc; gefaelle = $Gefaelle; strang = 'SW-F2'; praefix = 'SW-E.03-WC'
        punkte = (Punkte @($XF, 9500)); ende = (An 'WC-E.03' 'sw') })
  }
}

# ------------------------------------------------------------------ 3. Trinkwasser kalt KW-S2

[void](Befehl 'K Steigstrang KW-S2' 'leitung_zeichnen' @{
    serie = $SerieTw; groesse = $GroesseTwSteig; strang = 'KW-S2'; praefix = 'KW-S2'
    von = (An 'BO-KW-V1' 'b'); punkte = (Punkte @($XK, $YK, -500) @($XK, $YK, 3300)) })
[void](Befehl 'K 1.03 Etage' 'leitung_zeichnen' @{
    serie = $SerieTw; groesse = $GroesseTwAnschluss; strang = 'KW-S2'; praefix = 'KW-1.03'
    von = (An (Gerade $XK $YK 3250) 'ende')
    punkte = (Punkte @($XK, $YK, 3300) @($XK, $YK, 3500) @(11400, $YK, 3500) @(11400, $YK, 3300)); nach = (An 'WT-1.03' 'kw') })
[void](Befehl 'K E.03 Etage' 'leitung_abzweigen' @{
    leitung = (Gerade $XK $YK 600); punkt = (P3 $XK $YK 600)
    groesse = $GroesseTwAnschluss; strang = 'KW-S2'; praefix = 'KW-E.03'
    punkte = (Punkte @(11400, $YK, 600) @(11400, $YK, 400)); ende = (An 'WT-E.03' 'kw') })
if (-not $OhneWeZuleitung) {
  [void](Befehl 'K U.03 WE-1' 'leitung_abzweigen' @{
      leitung = (Gerade $XK $YK (-400)); punkt = (P3 $XK $YK (-400))
      groesse = $GroesseTwSteig; strang = 'KW-S2'; praefix = 'KW-U.03-WE'
      punkte = (Punkte @($XK, 8800, -400) @($XK, 8800, -2000)); ende = (An 'WE-1' 'kw') })
}
foreach ($g in $geschosse) {
  $k = $g[0]; $z = [double]$g[1]; $ze = $z + 600
  [void](Befehl "K $k.03 DU" 'leitung_abzweigen' @{
      leitung = (Gerade 11250 $YK $ze); punkt = (P3 11250 $YK $ze)
      groesse = $GroesseTwAnschluss; strang = 'KW-S2'; praefix = "KW-$k.03-DU"
      punkte = (Punkte @(11250, 6800, $ze) @(11250, 6800, ($z + 1000))); ende = (An "DU-$k.03" 'kw') })
  [void](Befehl "K $k.03 WC" 'leitung_abzweigen' @{
      leitung = (Gerade 10500 $YK $ze); punkt = (P3 10500 $YK $ze)
      groesse = $GroesseTwAnschluss; strang = 'KW-S2'; praefix = "KW-$k.03-WC"
      punkte = (Punkte @(10500, 9500, $ze) @(10500, 9500, ($z + 200))); ende = (An "WC-$k.03" 'kw') })
}

# ------------------------------------------------------------------ 4. Trinkwasser warm WW-S2

[void](Befehl 'W Steigstrang WW-S2' 'leitung_zeichnen' @{
    serie = $SerieTw; groesse = $GroesseTwSteig; strang = 'WW-S2'; praefix = 'WW-S2'
    von = (An 'WE-1' 'ww'); punkte = (Punkte @($XW, 8800, -2000) @($XW, 8800, -600) @($XW, $YW, -600) @($XW, $YW, 3400)) })
[void](Befehl 'W 1.03 Etage' 'leitung_zeichnen' @{
    serie = $SerieTw; groesse = $GroesseTwAnschluss; strang = 'WW-S2'; praefix = 'WW-1.03'
    von = (An (Gerade $XW $YW 3350) 'ende')
    punkte = (Punkte @($XW, $YW, 3400) @($XW, $YW, 3600) @(11500, $YW, 3600) @(11500, $YW, 3300)); nach = (An 'WT-1.03' 'ww') })
[void](Befehl 'W E.03 Etage' 'leitung_abzweigen' @{
    leitung = (Gerade $XW $YW 700); punkt = (P3 $XW $YW 700)
    groesse = $GroesseTwAnschluss; strang = 'WW-S2'; praefix = 'WW-E.03'
    punkte = (Punkte @(11500, $YW, 700) @(11500, $YW, 400)); ende = (An 'WT-E.03' 'ww') })
foreach ($g in $geschosse) {
  $k = $g[0]; $z = [double]$g[1]; $ze = $z + 700
  [void](Befehl "W $k.03 DU" 'leitung_abzweigen' @{
      leitung = (Gerade 11100 $YW $ze); punkt = (P3 11100 $YW $ze)
      groesse = $GroesseTwAnschluss; strang = 'WW-S2'; praefix = "WW-$k.03-DU"
      punkte = (Punkte @(11100, 6800, $ze) @(11100, 6800, ($z + 1000))); ende = (An "DU-$k.03" 'ww') })
}

# ------------------------------------------------------------------ 5. Prüfbefunde und Materialliste

Write-Host ''
Write-Host "Fertig: $script:Nummer Schritte, $($Neu.Count) Elemente angelegt, $($alt.Count) Altbestand entfernt." -ForegroundColor Green
$neuSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($kz in $Neu) { [void]$neuSet.Add($kz) }
[void]$neuSet.Add('WE-1')
$apparate = @('WC-E.03', 'WT-E.03', 'DU-E.03', 'WC-1.03', 'WT-1.03', 'DU-1.03')
$befunde = @()
foreach ($schwere in @('fehler', 'warnung', 'hinweis')) {
  $r = Abfrage 'pruefen' @{ schwere = $schwere }
  if ($r.gekuerzt) { Write-Host "Achtung: Prüfung «$schwere» gekürzt (mehr als 200 Befunde) - Liste unvollständig." -ForegroundColor Yellow }
  $befunde += @($r.befunde | Where-Object { $null -ne $_ })
}
$p13 = @($befunde | Where-Object { $neuSet.Contains($_.element) -and $_.code -match '^P1[3-8]$' } | Sort-Object code, element)
$p01 = @($befunde | Where-Object { $_.code -eq 'P01' -and ($neuSet.Contains($_.element) -or $apparate -contains $_.element) } | Sort-Object element)
Write-Host "Prüfbefunde P13-P18 der neuen Elemente: $($p13.Count)" -ForegroundColor Green
foreach ($b in $p13) { Write-Host "    $($b.code) $($b.schwere) $($b.element): $($b.text)" -ForegroundColor Yellow; if ($b.vorschlag) { Write-Host "        Vorschlag: $($b.vorschlag)" } }
Write-Host "Offene Anschlüsse (P01) der neuen Elemente und Apparate *.03: $($p01.Count)" -ForegroundColor Green
foreach ($b in $p01) { Write-Host "    $($b.code) $($b.element): $($b.text)" -ForegroundColor Yellow }

foreach ($st in @('SW-F2', 'KW-S2', 'WW-S2')) {
  $m = Abfrage 'materialliste' @{ strang = $st }
  Write-Host "Materialliste $st (Leitungen total $(Z $m.leitungslaengeTotal_m) m, Formteile $($m.formteileTotal) Stück):" -ForegroundColor Green
  foreach ($l in @($m.leitungen)) { if ($l) { Write-Host ("    Leitung  {0} | {1} | {2} Stück | {3} m" -f $l.serie, $l.groesse, $l.stueck, (Z $l.laenge_m)) } }
  foreach ($f in @($m.formteile)) {
    if ($f) { $w = if ($null -ne $f.winkel) { " | $(Z $f.winkel)°" } else { '' }; Write-Host ("    Formteil {0} | {1} | {2}{3} | {4} Stück" -f $f.familie, $f.typ, $f.klasse, $w, $f.stueck) }
  }
  if ($m.hinweis) { Write-Host "    Hinweis: $($m.hinweis)" -ForegroundColor Yellow }
}

if (@($p13 | Where-Object { $_.code -match '^P1[3-6]$' }).Count -gt 0) { exit 2 }
exit 0

# Grundausstattung Rohr- und Kanalserien: Datenpflege nach den Entscheiden von Christian vom 21.10.2026 (Auftrag Hauptlauf).
# Alles über die Kern-API des laufenden Servers (familie_aendern, Abfragen), nichts an den Projektdateien vorbei. Muster:
# werkzeuge\grundausstattung_k2_frei.ps1 (Trockenlauf zuerst, ändern erst mit -Ausfuehren, Prüfungen danach, Probe mit Rücknahme).
#
# Aufruf (Server der Grundausstattung, werkzeuge\grundausstattung_server.ps1, Port 5153; erst mit Hauptlauf abstimmen):
#   powershell -NoProfile -ExecutionPolicy Bypass -File werkzeuge\grundausstattung_pflege_2110.ps1                       Trockenlauf (ändert nichts)
#   powershell ... -File werkzeuge\grundausstattung_pflege_2110.ps1 -Ausfuehren                                          Pflege + Prüfungen
#   powershell ... -File werkzeuge\grundausstattung_pflege_2110.ps1 -Ausfuehren -Probe                                   dazu Probe zeichnen
#   -Probe allein prüft einen schon gepflegten Stand. Die Probe wird danach zurückgenommen (paket_zuruecknehmen, Prüfsumme des
#   Modells wie vorher), ausser mit -ProbeBehalten. -Port wählt den Server (Vorgabe 5153). Ein zweiter Lauf ändert nichts mehr.
#
# Was es tut (je Punkt der Entscheide; Familien über ihre id, Kennzeichen FAM-n im Arbeitsprojekt; ein familie_aendern je Familie):
#   1 Abwasser-Serien R6 Geberit Silent-db20 (FAM-16, Formteile FAM-13/14/15) und R7 Geberit PE (FAM-20, Formteile FAM-17/18/19):
#     leitung.einsatz = abwasser, leitung.groesse = { d: «da» } (Aussendurchmesser), die DN der Herstellerunterlage bleibt als
#     Typparameter «dn» mit Quelle (ziel dn, Gruppe sonstiges: Herstellerangabe, nie abgeleitet, keine Pflicht – d32 hat keine);
#     die Formteile verbinden mit ihren Anschlüssen über d statt dn (entwurf/abwasser-serie.md, Abschnitt «Daten»).
#   2 Kürzel (leitung.kuerzel, das die Beschriftung der Rohre nutzt): R1 «C-St», R3 und R4 «ST», R5 «PEX», R7 «PE», R6 «PE-S».
#   3 R1 Grösse 66,7 streichen: Typ «66,7 x 1,5» (Serie) und die Formteil-Typen nur dieser Grösse (Bogen, T-Stück, Reduktion).
#   4 Schwache Quellen mit sichtbarem Vermerk für die Freigabe: R6, R7, R2 T-Stück, K1 Bogen. Die Familie kennt kein Feld für einen
#     Freigabe- oder Quellenvermerk (FamilienFreigabe.cs: freigabe {akteur, zeit, version} gibt nur ein Mensch; Konflikt- und
#     Wertentscheide tragen eine begruendung, aber nur für einzelne Werte) – darum steht «Quellenvermerk: … – vor Freigabe prüfen»
#     am Anfang der Beschreibung und ein Satz im Bericht. Freigegeben wird nichts (die Freigabe macht nur Christian).
#   5 K3 Flexrohr AR: v_max 30 m/s und p_max 3000 Pa aus der Schmidlin-Broschüre Zubehör 2025.09 S. 28 – nur wenn die Broschüre als
#     Unterlage im Projekt liegt und der Befund die Werte trägt (Abfrage befunde); sonst wird nichts eingetragen und es wird gemeldet.
#   6 Nicht angefasst: die Bogenmasse-Konflikte von R1 (FAM-1). Das Skript listet sie nur auf (Familie, Typ, Wert alt/neu, Konflikt-Id).
#   Nicht bearbeitbar: FAM-21/22/23 (Gewinderohr-Formteile, Leserfehler beim Umschlüsseln der Quellen) – das Skript lässt sie aus,
#   prüft im Trockenlauf nur, ob sie sich inzwischen speichern lassen, und meldet es.
#   Entscheide und Grenzen, die im Skript stecken:
#   - Der Server muss mindestens den Stand 8ed6250 (Abwasser-Serie leitung.einsatz, Quellen über Leserversionen) gebaut haben; sonst Abbruch (Exit 2).
#   - Punkt 5: Die Broschüre 2025 gilt (Christian). Der Konflikt v_max 25 (Preisbuch Rohre S. 21) / 30 m/s (Broschüre) wird nicht über
#     konflikt_entscheiden (nur Mensch) beigelegt, sondern indem die Preisbuch-Quellen nicht mehr am Parameter stehen; die Entscheidung steht im Bericht.
#   - R7 d32 bleibt nicht zeichenbar, solange die Unterlage keinen Innendurchmesser und keine Wandstärke nennt (leitung.innen/wand brauchen
#     einen Wert je Typ); die fehlende DN ist es nicht mehr. Nichts erfunden – das Skript meldet es als [offen].
#   - Jede geänderte Familie ist danach für zwei Stunden für CS@grundausstattung reserviert (Sperre des Kerns, wie bei jedem familie_aendern).
# Exit 0 = alles grün, 1 = eine Prüfung bzw. ein Befehl nicht grün, 2 = abgebrochen (Server, Voraussetzung).
# Datei mit UTF-8-BOM speichern (PowerShell 5.1 liest sonst «» und Umlaute falsch – Befund 20.10.2026).
param([int]$Port = 5153, [switch]$Ausfuehren, [switch]$Probe, [switch]$ProbeBehalten)
$ErrorActionPreference = 'Stop'
$u = "http://localhost:$Port"

# ------------------------------------------------------------------ Festwerte
# id, erwarteter Name, Kategorie (Voraussetzung: der Server hat genau diese Familien)
$Erwartet = [ordered]@{
  'fam-becd0a0087ef' = @('Geberit Mapress C-Stahl Bogen', 'Bogen')            # FAM-1  R1
  'fam-d02786ea1a57' = @('Geberit Mapress C-Stahl T-Stück', 'TStueck')        # FAM-2  R1
  'fam-24c441d3e077' = @('Geberit Mapress C-Stahl Reduktion', 'Reduktion')    # FAM-3  R1
  'fam-324b7269e42a' = @('Geberit Mapress C-Stahl', 'Rohr')                   # FAM-4  R1
  'fam-c4cf417b3b59' = @('Geberit Mapress Edelstahl T-Stück', 'TStueck')      # FAM-6  R2
  'fam-dedbe432b980' = @('JRG Sanipex MT', 'Rohr')                            # FAM-12 R5
  'fam-ff98af86928f' = @('Geberit Silent-db20 Bogen', 'Bogen')                # FAM-13 R6
  'fam-f965ca8e404f' = @('Geberit Silent-db20 T-Stück', 'TStueck')            # FAM-14 R6
  'fam-f528d9e613cd' = @('Geberit Silent-db20 Reduktion', 'Reduktion')        # FAM-15 R6
  'fam-23c4a1cd1473' = @('Geberit Silent-db20', 'Rohr')                       # FAM-16 R6
  'fam-31b8fa8f52f1' = @('Geberit PE Bogen', 'Bogen')                         # FAM-17 R7
  'fam-7c4ceac4df82' = @('Geberit PE T-Stück', 'TStueck')                     # FAM-18 R7
  'fam-caa4bdbb25f6' = @('Geberit PE Reduktion', 'Reduktion')                 # FAM-19 R7
  'fam-9f55564bd8e8' = @('Geberit PE', 'Rohr')                                # FAM-20 R7
  'fam-592a8fa0bf38' = @('Stahlrohr Gewinderohr', 'Rohr')                    # FAM-24 R3
  'fam-bb11b9344d35' = @('Stahlrohr geschweisst', 'Rohr')                     # FAM-28 R4
  'fam-560cf25a9198' = @('Wickelfalzrohr rund Bogen', 'Bogen')                # FAM-29 K1
  'fam-d9255cac1391' = @('Flexrohr Schmidlin AR', 'Kanal')                    # FAM-33 K3
}
$NichtBearbeitbar = [ordered]@{ 'fam-d5efb5efe495' = 'FAM-21 Stahlrohr Gewinderohr Bogen'; 'fam-870a13b427b2' = 'FAM-22 Stahlrohr Gewinderohr T-Stück'; 'fam-eabe58a86034' = 'FAM-23 Stahlrohr Gewinderohr Reduktion' }

# 1 Abwasser-Serien: Serie (Kürzel), Formteile mit dem Parameter des Aussendurchmessers je Anschluss
$Abwasser = @(
  @{ Kurz = 'R6 Geberit Silent-db20'; Serie = 'fam-23c4a1cd1473'; Kuerzel = 'PE-S'
     Formteile = [ordered]@{ 'fam-ff98af86928f' = @{ a = 'd'; b = 'd' }; 'fam-f965ca8e404f' = @{ a = 'd'; b = 'd'; c = 'dc' }; 'fam-f528d9e613cd' = @{ a = 'D1'; b = 'D2' } } }
  @{ Kurz = 'R7 Geberit PE'; Serie = 'fam-9f55564bd8e8'; Kuerzel = 'PE'
     Formteile = [ordered]@{ 'fam-31b8fa8f52f1' = @{ a = 'd'; b = 'd' }; 'fam-7c4ceac4df82' = @{ a = 'd'; b = 'd'; c = 'dc' }; 'fam-caa4bdbb25f6' = @{ a = 'd'; b = 'd1' } } }
)
# 2 Kürzel der übrigen Serien (R6 und R7 oben)
$KuerzelSonst = [ordered]@{ 'fam-324b7269e42a' = 'C-St'; 'fam-592a8fa0bf38' = 'ST'; 'fam-bb11b9344d35' = 'ST'; 'fam-dedbe432b980' = 'PEX' }
# 3 R1 Grösse 66,7: je Familie die Typnamen
$R1_667 = [ordered]@{
  'fam-becd0a0087ef' = @('d66.7')
  'fam-d02786ea1a57' = @('d66.7')
  'fam-24c441d3e077' = @('d76.1-66.7', 'd66.7-54')
  'fam-324b7269e42a' = @('66,7 x 1,5')
}
# 4 Quellenvermerke (stehen am Anfang der Beschreibung; Text laut Bericht der Familie)
$DibtSilent = 'DIBt-Zulassung Z-42.1-265 (Stand 26.02.2019, Geltungsdauer bis 01.10.2022 abgelaufen, Händlerablage Heinze)'
$KatalogAu = 'Geberit-HDPE-Katalog «Valid from April 2020» (Australien-Ausgabe, Händlerablage Reece)'
$Vermerke = [ordered]@{
  'fam-ff98af86928f' = "Schenkellängen 15°/30°/45° aus einem Geberit-Produktdatenblatt (Stand 24/10/2022, Händlerablage heizung-billiger.de), 88,5° nur aus der $DibtSilent; nicht vom Hersteller abgerufen"
  'fam-f965ca8e404f' = "Einbaulängen 45° und 88,5° aus der $DibtSilent; nicht vom Hersteller abgerufen"
  'fam-f528d9e613cd' = "Einbaulängen aus der $DibtSilent, Anlage 3; nicht vom Hersteller abgerufen"
  'fam-23c4a1cd1473' = "Wandstärke s und Innendurchmesser di aus der $DibtSilent; Aussendurchmesser und DN von geberit.ch; nicht vom Hersteller abgerufen"
  'fam-31b8fa8f52f1' = "Schenkellängen aus dem $KatalogAu; nicht vom Hersteller abgerufen, Schweizer Massblätter nicht gefunden"
  'fam-7c4ceac4df82' = "Einbaulängen aus dem $KatalogAu; nicht vom Hersteller abgerufen, Schweizer Massblätter nicht gefunden"
  'fam-caa4bdbb25f6' = "Einbaulängen aus dem $KatalogAu; nicht vom Hersteller abgerufen, Schweizer Massblätter nicht gefunden"
  'fam-9f55564bd8e8' = "DN, Innendurchmesser und Wandstärke aus der Rohrtabelle des $($KatalogAu -replace 'Katalog ', 'Katalogs '); nicht vom Hersteller abgerufen, Schweizer Massblätter nicht gefunden"
  'fam-c4cf417b3b59' = "Masse aus dem Geberit-Katalog Polen, Kap. 4.1.8, S. 94 (Händlerablage download.sankom.net, Stand nicht angegeben); nicht vom Hersteller abgerufen, kein aktuelles Produktdatenblatt gefunden"
  'fam-560cf25a9198' = "Einbaulängen von einem anderen Hersteller (Lindab, Datenblatt Bogen BU, Stand 2021-09-08), nicht von Schmidlin – Schmidlin bemasst die Bögen nicht"
}
# 5 K3 Flexrohr AR: Broschüre Zubehör 2025.09 (Prüfsumme-Anfang), Befunde der Werte
$K3Id = 'fam-d9255cac1391'
$K3Pruefsumme = '3787356b78a0'
$K3Wert = "$K3Pruefsumme#9712"       # S. 28 «Luftgeschwindigkeit 30 m/s, max. Betriebsdruck 3000 PA»
$K3Titel = "$K3Pruefsumme#9708"      # S. 28 «Aluflexrohr AR»
$K3Preisbuch = 'f614a134b685#14059'  # Preisbuch Rohre S. 21 «25»
# 6 R1 Bogenmasse-Konflikte: nur auflisten (Unterlage-Anfänge: neu = Massänderungsliste April 2026, alt = Datenblatt 2022)
$R1Bogen = 'fam-becd0a0087ef'
$UlNeu = 'b23c14c1c864'; $UlAlt = 'a890c8659952'

# Marken im Bericht: ein Nachtrag je Thema, nur einmal
$MAbw = 'Datenpflege Abwasser-Serie'; $MFt = 'Datenpflege Abwasser-Formteil'; $MKz = 'Datenpflege Kürzel'; $M667 = 'Datenpflege Grösse 66,7'
$MQv = 'Datenpflege Quellenvermerk'; $MK3 = 'Datenpflege Betriebsgrenzen'
$Entscheid = 'Entscheid Christian 21.10.2026'

# ------------------------------------------------------------------ Hilfen
$script:fehlerZahl = 0; $script:pruefZahl = 0; $script:offenZahl = 0
function Json($o) { ConvertTo-Json -InputObject $o -Depth 60 -Compress }
function Kopie($o) { ConvertFrom-Json -InputObject (Json $o) }
function Setze($o, [string]$feld, $wert) { $o | Add-Member -NotePropertyName $feld -NotePropertyValue $wert -Force }
function Ohne($o, [string]$feld) { if ($o.PSObject.Properties[$feld]) { $o.PSObject.Properties.Remove($feld) } }
function Hat($o, [string]$feld) { $null -ne $o -and $null -ne $o.PSObject.Properties[$feld] }
function Aus([string]$t) { Write-Host $t }
function Pruef([bool]$ok, [string]$t) { $script:pruefZahl++; if ($ok) { Write-Host "  [ok]     $t" } else { Write-Host "  [FEHLER] $t"; $script:fehlerZahl++ } }
function Offen([string]$t) { $script:offenZahl++; Write-Host "  [offen]  $t" }
function Z($x) { if ($null -eq $x) { 'null' } else { ([double]$x).ToString('0.###', [Globalization.CultureInfo]::InvariantCulture) } }
function Etwa($a, $b, [double]$tol = 0.05) { $null -ne $a -and [Math]::Abs([double]$a - [double]$b) -le $tol }
function Kurz([string]$t, [int]$n = 400) { if ($t.Length -gt $n) { $t.Substring(0, $n) + ' …' } else { $t } }

function Post([string]$pfad, $par, [int]$zeit = 600) {
  $body = [Text.Encoding]::UTF8.GetBytes((Json $par))
  $text = $null
  try {
    $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$u$pfad" -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec $zeit
    $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
  } catch [System.Net.WebException] {
    $resp = $_.Exception.Response
    if ($null -eq $resp) { throw }
    $text = (New-Object IO.StreamReader($resp.GetResponseStream(), [Text.Encoding]::UTF8)).ReadToEnd()
  }
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  ConvertFrom-Json -InputObject $text
}
function Abfrage([string]$name, $par, [int]$zeit = 600) {
  $r = Post "/api/abfrage/$name" $par $zeit
  if ($null -ne $r -and $r.PSObject.Properties['fehler'] -and $null -ne $r.fehler -and -not $r.PSObject.Properties['angenommen']) {
    throw "Abfrage $name abgelehnt: $($r.fehler) – $($r.vorschlag)"
  }
  $r
}
function Befehl([string]$name, $par, [bool]$trocken) {
  $t = if ($trocken) { 'true' } else { 'false' }
  Post "/api/befehl/$name`?trockenlauf=$t" $par 900
}
function ErgebnisZeigen($e, [int]$maxHinweise = 3) {
  if ($null -eq $e) { Aus '    (keine Antwort)'; return }
  Aus ('    ' + (Kurz ([string]$e.meldung) 600))
  if ($e.fehler) { Aus ('    Fehler: ' + (Kurz ([string]$e.fehler) 600)) }
  if ($e.vorschlag) { Aus ('    Vorschlag: ' + (Kurz ([string]$e.vorschlag) 300)) }
  $h = @($e.hinweise)
  foreach ($x in ($h | Select-Object -First $maxHinweise)) { Aus ('    Hinweis: ' + (Kurz ([string]$x) 200)) }
  if ($h.Count -gt $maxHinweise) { Aus "    … $($h.Count - $maxHinweise) weitere Hinweise" }
}
function Param($def, [string]$name) { @($def.parameter | Where-Object { $_.name -eq $name }) | Select-Object -First 1 }
function Typ($def, [string]$name) { @($def.typen | Where-Object { $_.name -eq $name }) | Select-Object -First 1 }
function Wert($t, [string]$p) { if ($null -ne $t -and (Hat $t.werte $p)) { $t.werte.$p } else { $null } }
function Lesen([string]$id) { Abfrage 'familie' @{ familie = $id } }
function Zeile($f) { "{0} «{1}» ({2}, v{3}, {4}, {5} Typen)" -f $f.kennzeichen, $f.name, $f.id, $f.version, $f.status, @($f.definition.typen).Count }

# ------------------------------------------------------------------ Server und Familien lesen
Aus "Datenpflege Grundausstattung 21.10.2026 ($(if ($Ausfuehren) { 'AUSFÜHREN' } else { 'Trockenlauf' })$(if ($Probe) { ', Probe' }), Server $u)"
try { $st = Invoke-RestMethod "$u/api/status" -TimeoutSec 15 } catch { Aus "Server $u antwortet nicht: $($_.Exception.Message)"; exit 2 }
if (-not $st.offen) { Aus "Auf $u ist kein Projekt offen."; exit 2 }
Aus "Projekt: $($st.projekt) ($($st.ort)), Stand $($st.stand.pakete) Pakete, Prüfsumme $($st.stand.pruefsumme)"
$fae = Abfrage 'faehigkeiten' @{}
if ((Json $fae) -notmatch 'leitung.einsatz') { Aus 'Der Server kennt leitung.einsatz (Abwasser-Serie) nicht – Server aus main ab 8ed6250 bauen und neu starten.'; exit 2 }

$fam = @{}
$vor = @()
foreach ($id in $Erwartet.Keys) {
  try { $fam[$id] = Lesen $id } catch { $vor += "Familie $id ($($Erwartet[$id][0])) nicht lesbar: $($_.Exception.Message)"; continue }
  $f = $fam[$id]
  if ($f.name -ne $Erwartet[$id][0] -or $f.kategorie -ne $Erwartet[$id][1]) { $vor += "$id heisst «$($f.name)» ($($f.kategorie)), erwartet «$($Erwartet[$id][0])» ($($Erwartet[$id][1]))" }
  if (-not $f.gueltig) { $vor += "$($f.kennzeichen) «$($f.name)» ist ungültig: $(Kurz (Json $f.definitionsfehler) 300)" }
}
if ($vor.Count -gt 0) { Aus 'Voraussetzungen nicht erfüllt:'; foreach ($v in $vor) { Aus "  - $v" }; exit 2 }
foreach ($id in $Erwartet.Keys) { Aus ('  ' + (Zeile $fam[$id])) }
$KzVon = @{}; foreach ($id in $Erwartet.Keys) { $KzVon[$id] = $fam[$id].kennzeichen }

# ------------------------------------------------------------------ Plan aufbauen (nur, was noch nicht im Soll-Zustand ist)
$plan = [ordered]@{}
function Plan([string]$id) {
  if (-not $plan.Contains($id)) {
    $d = $fam[$id].definition
    $plan[$id] = [pscustomobject]@{ Ops = (New-Object System.Collections.ArrayList); Leitung = $null; Beschr = [string]$d.beschreibung; Bericht = [string]$d.bericht; Themen = (New-Object System.Collections.ArrayList) }
  }
  $plan[$id]
}
function LeitungPlan([string]$id) { $p = Plan $id; if ($null -eq $p.Leitung) { $p.Leitung = Kopie $fam[$id].definition.leitung }; $p }
function Thema($p, [string]$t) { if (-not $p.Themen.Contains($t)) { [void]$p.Themen.Add($t) } }
function Nachtrag($p, [string]$marke, [string]$text) { if (-not $p.Bericht.Contains($marke)) { $p.Bericht = ($p.Bericht.TrimEnd() + ' ' + $text).Trim() } }
$vor = @()

# ---- 3 R1 Grösse 66,7 streichen (Formteile zuerst, Serie zuletzt)
foreach ($id in $R1_667.Keys) {
  $d = $fam[$id].definition
  $vorhanden = @($R1_667[$id] | Where-Object { Typ $d $_ })
  # jeder Typ mit der Grösse 66,7 muss in der Liste stehen (sonst Voraussetzung nicht erfüllt)
  foreach ($t in @($d.typen)) {
    $hat667 = $false
    foreach ($pn in 'd', 'da', 'd1') { if ((Wert $t $pn) -eq 66.7) { $hat667 = $true } }
    if ($hat667 -and $R1_667[$id] -notcontains $t.name) { $vor += "$($KzVon[$id]): Typ «$($t.name)» hat die Grösse 66,7, steht aber nicht in der Streichliste" }
  }
  if ($vorhanden.Count -eq 0) { continue }
  $p = Plan $id
  foreach ($n in $vorhanden) { [void]$p.Ops.Add([ordered]@{ op = 'typ_loeschen'; name = $n }) }
  Thema $p "66,7 streichen ($($vorhanden -join ', '))"
  $kf = @($d.konflikte | Where-Object { $vorhanden -contains $_.typ })
  $kfText = if ($kf.Count -gt 0) { " Mit dem Typ entfällt der Konflikt $(@($kf | ForEach-Object { $_.id }) -join ', ') (DN der Grösse 66,7)." } else { '' }
  Nachtrag $p $M667 "NACHTRAG 21.10.2026 ($M667, $Entscheid): Typ(en) «$($vorhanden -join '», «')» entfernt – die Grösse 66,7 × 1,5 (DN 65) steht nur in den deutschen Geberit-Unterlagen, im Schweizer Katalog fehlt sie (dort 76,1 für DN 65).$kfText Zwei Grössen mit DN 65 gibt es damit nicht mehr."
}

# ---- 1 Abwasser-Serien R6 und R7
foreach ($a in $Abwasser) {
  $sid = $a.Serie; $s = $fam[$sid]; $sd = $s.definition; $l = $sd.leitung
  $aussenName = [string]$l.aussen
  $pAussen = Param $sd $aussenName
  if (-not $pAussen -or $pAussen.art -ne 'typ' -or $pAussen.datentyp -ne 'laenge') { $vor += "$($a.Kurz): leitung.aussen «$aussenName» ist kein Typparameter vom Datentyp laenge"; continue }
  $werte = @{}
  foreach ($t in @($sd.typen)) { $w = Wert $t $aussenName; if ($null -eq $w) { $vor += "$($a.Kurz): Typ «$($t.name)» hat keinen Aussendurchmesser ($aussenName)" } else { $werte[[double]$w] = $true } }
  foreach ($fk in 'bogen', 'tstueck', 'reduktion') {
    if (-not $l.formteile.$fk -or -not $a.Formteile.Contains([string]$l.formteile.$fk.familie)) { $vor += "$($a.Kurz): leitung.formteile.$fk nennt eine andere Familie als die Pflege erwartet" }
  }
  $dn = Param $sd 'dn'
  if (-not $dn -or $dn.datentyp -ne 'dn') { $vor += "$($a.Kurz): Typparameter «dn» (DN der Herstellerunterlage) fehlt" }

  # Formteile: Anschlüsse über d statt dn
  foreach ($fid in $a.Formteile.Keys) {
    $fd = $fam[$fid].definition; $zuord = $a.Formteile[$fid]
    $ops = @()
    foreach ($an in @($fd.anschluesse)) {
      $pn = $zuord[[string]$an.name]
      if (-not $pn) { $vor += "$($KzVon[$fid]): Anschluss «$($an.name)» ist in der Pflege nicht zugeordnet"; continue }
      $pp = Param $fd $pn
      if (-not $pp -or $pp.art -ne 'typ' -or $pp.datentyp -ne 'laenge' -or [string]$pp.beschreibung -notmatch 'Aussendurchmesser') { $vor += "$($KzVon[$fid]): Parameter «$pn» für Anschluss $($an.name) ist kein Aussendurchmesser (Typparameter, laenge)"; continue }
      if ((Hat $an 'd') -and [string]$an.d -eq $pn -and -not (Hat $an 'dn')) { continue }   # schon umgestellt
      foreach ($t in @($fd.typen)) {
        $w = Wert $t $pn
        if ($null -eq $w -or -not $werte.ContainsKey([double]$w)) { $vor += "$($KzVon[$fid]): Typ «$($t.name)» $pn = $(Z $w) – keine Grösse der Serie $($a.Kurz)" }
      }
      $neu = Kopie $an; Ohne $neu 'dn'; Setze $neu 'd' $pn
      $ops += , $neu
    }
    if ($ops.Count -gt 0) {
      $p = Plan $fid
      foreach ($o in $ops) { [void]$p.Ops.Add([ordered]@{ op = 'anschluss_setzen'; anschluss = $o }) }
      Thema $p "Abwasser: Anschlüsse über d ($(@($ops | ForEach-Object { $_.name }) -join ', '))"
      $dnPar = if (Param $fd 'dn') { ' Der Typparameter «dn» (DN der Herstellerunterlage) bleibt als Angabe mit Quelle.' } else { '' }
      $p.Beschr = ($p.Beschr.TrimEnd() + " Anschlüsse über den Aussendurchmesser (Abwasser-Serie $($a.Kurz): $(@($ops | ForEach-Object { $_.name + ' = ' + $_.d }) -join ', ')), nicht über die DN.").Trim()
      Nachtrag $p $MFt "NACHTRAG 21.10.2026 ($MFt, $Entscheid): Anschlüsse $(@($ops | ForEach-Object { $_.name }) -join ', ') verbinden über den Aussendurchmesser (d = $(@($ops | ForEach-Object { $_.d } | Select-Object -Unique) -join ', ')) statt über die DN, weil die Serie $($a.Kurz) ein Abwassersystem nach Aussendurchmesser ist (entwurf/abwasser-serie.md).$dnPar"
    }
  }

  # Serie: einsatz abwasser, Grösse d, Kürzel; DN der Herstellerunterlage als Herstellerangabe
  $gesetzt = ($l.einsatz -eq 'abwasser') -and (Hat $l.groesse 'd') -and $l.groesse.d -eq $aussenName -and -not (Hat $l.groesse 'dn')
  if (-not $gesetzt) {
    $p = LeitungPlan $sid
    Setze $p.Leitung 'einsatz' 'abwasser'
    Setze $p.Leitung 'groesse' ([ordered]@{ d = $aussenName })
    Thema $p "Abwasser-Serie: einsatz abwasser, Grösse d = «$aussenName»"
    if ($dn -and -not ((Hat $dn 'ziel') -and $dn.ziel -eq 'dn' -and $dn.gruppe -eq 'sonstiges')) {
      $np = Kopie $dn; Setze $np 'ziel' 'dn'; Setze $np 'gruppe' 'sonstiges'
      Setze $np 'beschreibung' (([string]$dn.beschreibung).TrimEnd('.') + ' – Herstellerangabe zum Typ, nie aus dem Aussendurchmesser abgeleitet; keine Pflicht (fehlt sie, bleibt der Typ zeichenbar)')
      [void]$p.Ops.Add([ordered]@{ op = 'parameter_setzen'; parameter = $np })
    }
    $p.Beschr = ($p.Beschr.TrimEnd() + " Abwasser-Serie (einsatz abwasser): Grösse und Beschriftung nach Aussendurchmesser d ($aussenName), Kürzel $($a.Kuerzel); die DN der Herstellerunterlage steht als Typparameter «dn» (Herstellerangabe, nie aus d abgeleitet).").Trim()
    Nachtrag $p $MAbw "NACHTRAG 21.10.2026 ($MAbw, ${Entscheid}: «Geberit PE ist immer Abwasser», ebenso Silent-db20; entwurf/abwasser-serie.md): leitung.einsatz = abwasser, leitung.groesse = { d: «$aussenName» } (Aussendurchmesser), Beschriftung «$($a.Kuerzel) <d>» (z. B. «$($a.Kuerzel) 110»). Die DN der Herstellerunterlage bleibt als Typparameter «dn» mit Quelle (ziel dn, Gruppe sonstiges: eine Angabe des Herstellers, nie abgeleitet; ohne DN wie bei d32 bleibt der Typ zeichenbar). Die Formteile verbinden über d.$(if ($a.Kuerzel -eq 'PE-S') { ' Das Kürzel «PE-S» (nicht «PE-S2») ist damit bestätigt.' })"
  }
  if ($l.kuerzel -ne $a.Kuerzel) {
    $p = LeitungPlan $sid
    Setze $p.Leitung 'kuerzel' $a.Kuerzel
    Thema $p "Kürzel $($a.Kuerzel)"
    Nachtrag $p $MKz "NACHTRAG 21.10.2026 ($MKz, $Entscheid): leitung.kuerzel = «$($a.Kuerzel)»."
  }
}

# ---- 2 Kürzel der übrigen Serien
foreach ($id in $KuerzelSonst.Keys) {
  $l = $fam[$id].definition.leitung
  if ($l.kuerzel -eq $KuerzelSonst[$id]) { continue }
  $p = LeitungPlan $id
  Setze $p.Leitung 'kuerzel' $KuerzelSonst[$id]
  Thema $p "Kürzel $($KuerzelSonst[$id])"
  Nachtrag $p $MKz "NACHTRAG 21.10.2026 ($MKz, $Entscheid): leitung.kuerzel = «$($KuerzelSonst[$id])» – die Beschriftung der Rohre lautet «$($KuerzelSonst[$id]) NW|DN <Nennweite>»; das früher offene Kürzel ist damit entschieden."
}

# ---- 4 Quellenvermerke
foreach ($id in $Vermerke.Keys) {
  $d = $fam[$id].definition
  if (([string]$d.beschreibung).StartsWith('Quellenvermerk:')) { continue }
  $p = Plan $id
  $p.Beschr = ("Quellenvermerk: $($Vermerke[$id]) – vor Freigabe prüfen. " + $p.Beschr).Trim()
  Thema $p 'Quellenvermerk'
  Nachtrag $p $MQv "NACHTRAG 21.10.2026 ($MQv, $Entscheid): Quellenvermerk für die Freigabe – $($Vermerke[$id]); die Beschreibung beginnt mit dem Vermerk «… – vor Freigabe prüfen». Freigegeben ist nichts; die Freigabe macht nur Christian."
}

# ---- 5 K3 Flexrohr AR: v_max 30 m/s, p_max 3000 Pa aus der Broschüre 2025
$k3Hinweis = $null
$k3Def = $fam[$K3Id].definition
$vm = Param $k3Def 'v_max'; $pm = Param $k3Def 'p_max'
$k3Soll = $vm -and $pm -and $vm.wert -eq 30 -and @($vm.quellen).Count -eq 1 -and @($vm.quellen)[0] -eq $K3Wert -and $pm.wert -eq 3000 -and @($pm.quellen) -contains $K3Wert
if (-not $k3Soll) {
  $ul = @($k3Def.unterlagen | Where-Object { ([string]$_.pruefsumme).StartsWith($K3Pruefsumme) }) | Select-Object -First 1
  if (-not $ul) { $k3Hinweis = "Die Schmidlin-Broschüre Zubehör 2025.09 ($K3Pruefsumme…) liegt nicht als Unterlage bei $($KzVon[$K3Id])" }
  elseif (-not $vm -or -not $pm) { $k3Hinweis = "$($KzVon[$K3Id]) hat keinen Parameter v_max bzw. p_max" }
  else {
    $br = Abfrage 'befunde' @{ unterlage = $ul.datei; id = "$K3Wert,$K3Titel"; proSeite = 20 }
    $bw = @($br.befunde | Where-Object { $_.id -eq $K3Wert }) | Select-Object -First 1
    $bt = @($br.befunde | Where-Object { $_.id -eq $K3Titel }) | Select-Object -First 1
    if (-not $bw -or -not $bt) { $k3Hinweis = "Die Befunde $K3Wert / $K3Titel gibt es in der Broschüre beim Leser des Servers nicht" }
    elseif ($bt.wert -ne 'Aluflexrohr AR' -or $bt.seite -ne $bw.seite) { $k3Hinweis = "Befund $K3Titel ist «$($bt.wert)» (S. $($bt.seite)), nicht der AR-Block der Seite von $K3Wert (S. $($bw.seite))" }
    elseif ([string]$bw.wert -notmatch '\b30\s*m/s' -or [string]$bw.wert -notmatch '\b3000\s*PA\b') { $k3Hinweis = "Befund $K3Wert (S. $($bw.seite)) lautet «$($bw.wert)» – er nennt 30 m/s und 3000 Pa nicht" }
    else {
      $p = Plan $K3Id
      $nv = Kopie $vm; Setze $nv 'wert' 30; Setze $nv 'quellen' ([object[]]@($K3Wert))
      Setze $nv 'beschreibung' 'Luftgeschwindigkeit max. (Schmidlin Broschüre Zubehör 2025.09 S. 28; das Preisbuch Rohre S. 21 nennt 25 m/s, gilt nicht: Entscheid Christian 21.10.2026 «Broschüre 2025 gilt»)'
      $np = Kopie $pm; Setze $np 'wert' 3000
      $q = New-Object System.Collections.ArrayList; [void]$q.Add($K3Wert); Setze $np 'quellen' ([object[]]$q.ToArray())
      Setze $np 'beschreibung' 'Betriebsdruck max. (Schmidlin Broschüre Zubehör 2025.09 S. 28; das Preisbuch Rohre S. 21 nennt 10 000 Pa, gilt nicht: Entscheid Christian 21.10.2026 «Broschüre 2025 gilt»)'
      [void]$p.Ops.Add([ordered]@{ op = 'parameter_setzen'; parameter = $nv })
      [void]$p.Ops.Add([ordered]@{ op = 'parameter_setzen'; parameter = $np })
      Thema $p 'v_max 30 m/s, p_max 3000 Pa (Broschüre 2025)'
      Nachtrag $p $MK3 "NACHTRAG 21.10.2026 ($MK3, ${Entscheid}: «Broschüre 2025 gilt»): v_max 30 m/s und p_max 3000 Pa aus der Schmidlin-Broschüre Zubehör 2025.09, S. 28 (${K3Wert}: «$($bw.wert)»). Die Preisbuch-Quellen stehen nicht mehr an den Parametern: v_max 25 m/s ($K3Preisbuch, Preisbuch Rohre S. 21) und p_max 10 000 Pa (Preisbuch #14044/#14045) – der Konflikt zu v_max entfällt damit, die Entscheidung ist hier dokumentiert."
    }
  }
}

# ---- Ops abschliessen: Leitung, Kopf
foreach ($id in @($plan.Keys)) {
  $p = $plan[$id]; $d = $fam[$id].definition
  if ($null -ne $p.Leitung -and (Json $p.Leitung) -ne (Json $d.leitung)) { [void]$p.Ops.Add([ordered]@{ op = 'leitung_setzen'; leitung = $p.Leitung }) }
  $kopf = [ordered]@{ op = 'kopf_setzen' }
  if ($p.Beschr -ne [string]$d.beschreibung) { $kopf.beschreibung = $p.Beschr }
  if ($p.Bericht -ne [string]$d.bericht) { $kopf.bericht = $p.Bericht }
  if ($kopf.Count -gt 1) { [void]$p.Ops.Add($kopf) }
  if ($p.Ops.Count -eq 0) { $plan.Remove($id) }
}
if ($vor.Count -gt 0) { Aus 'Voraussetzungen nicht erfüllt:'; foreach ($v in $vor) { Aus "  - $v" }; Aus 'Abbruch: nichts geändert.'; exit 2 }

# ------------------------------------------------------------------ Vorher-Stand (für die Vergleiche nach der Pflege)
$basisFehler = @{}
foreach ($id in $Erwartet.Keys) {
  $r = Abfrage 'familie_pruefen' @{ familie = $id }
  $basisFehler[$id] = [int]$r.anzahl.fehler
}
$basisPruefen = Abfrage 'pruefen' @{}
$basisSerie = @{}
foreach ($a in $Abwasser) { $basisSerie[$a.Serie] = Abfrage 'leitungsserie' @{ familie = $a.Serie } 900 }
$basisKonflikte = @{}
foreach ($id in $Erwartet.Keys) { $basisKonflikte[$id] = @($fam[$id].definition.konflikte | ForEach-Object { $_.id }) }

# ------------------------------------------------------------------ Trockenlauf, dann echt
function Speichern([bool]$trocken) {
  $alleOk = $true
  foreach ($id in $plan.Keys) {
    $p = $plan[$id]
    Aus ("  familie_aendern {0} «{1}»: {2} Op(s){3}" -f $KzVon[$id], $fam[$id].name, $p.Ops.Count, $(if ($trocken) { ' – Trockenlauf' } else { '' }))
    foreach ($t in $p.Themen) { Aus "      · $t" }
    $e = Befehl 'familie_aendern' @{ familie = $id; ops = [object[]]$p.Ops.ToArray() } $trocken
    ErgebnisZeigen $e
    if (-not $e.angenommen) { $alleOk = $false }
  }
  $alleOk
}

Aus ''
if ($plan.Count -eq 0) { Aus 'Pflege: alle Familien sind schon im Soll-Zustand – nichts zu ändern.' }
else {
  Aus "Trockenlauf der Pflege ($($plan.Count) Familien):"
  $trockenOk = Speichern $true
  Pruef $trockenOk "Trockenlauf: $($plan.Count) familie_aendern angenommen"
  if ($Ausfuehren) {
    if (-not $trockenOk) { Aus 'Abbruch: der Trockenlauf ist nicht grün – nichts geändert.'; exit 1 }
    Aus ''
    Aus 'Pflege (echt):'
    $echtOk = Speichern $false
    Pruef $echtOk "familie_aendern: $($plan.Count) angenommen"
    if (-not $echtOk) { Aus 'Abbruch nach Fehler – Stand prüfen (Abfrage aenderungen).'; exit 1 }
  }
}
if ($k3Hinweis) { Offen "K3 Flexrohr AR: nichts eingetragen – $k3Hinweis" }

if ($plan.Count -gt 0 -and -not $Ausfuehren) {
  Aus ''
  Aus 'Prüfungen und Probe erst nach der Pflege (mit -Ausfuehren).'
  Aus "Ergebnis: $(if ($script:fehlerZahl -eq 0) { 'Trockenlauf grün' } else { "$($script:fehlerZahl) Fehler" }) ($($script:pruefZahl) Prüfung(en))"
  if ($script:fehlerZahl -eq 0) { exit 0 } else { exit 1 }
}

# ------------------------------------------------------------------ Prüfungen
$neuF = @{}
foreach ($id in $Erwartet.Keys) { $neuF[$id] = Lesen $id }
function Verf($g) {
  [pscustomobject]@{ B = [bool]$g.formteile.bogen.verfuegbar; T = [bool]$g.formteile.tstueck.verfuegbar
    TW = @($g.formteile.tstueckJeWinkel | Where-Object { $_.verfuegbar }).Count; R = @($g.formteile.reduktion | Where-Object { $_.verfuegbar }).Count }
}

Aus ''
Aus 'Prüfung 1 – Familien (gültig, Entwurf und nicht freigegeben, familie_pruefen ohne neuen Fehler):'
foreach ($id in $Erwartet.Keys) {
  $f = $neuF[$id]
  $fp = Abfrage 'familie_pruefen' @{ familie = $id }
  $nf = [int]$fp.anzahl.fehler; $nw = [int]$fp.anzahl.warnungen
  Pruef ($f.gueltig -and @($f.definitionsfehler).Count -eq 0 -and $f.status -eq 'entwurf' -and -not $f.freigabe -and $nf -le $basisFehler[$id]) ("{0} «{1}» v{2}: gültig, Entwurf, Fehler in familie_pruefen {3} (vorher {4}), Warnungen {5}" -f $f.kennzeichen, $f.name, $f.version, $nf, $basisFehler[$id], $nw)
}

Aus ''
Aus 'Prüfung 2 – Abwasser-Serien R6 und R7 (einsatz, Grösse d, DN nur als Herstellerangabe, Formteile über d, Beschriftung):'
$beispiel = @{}
foreach ($a in $Abwasser) {
  $sid = $a.Serie; $s = $neuF[$sid]; $sd = $s.definition; $l = $sd.leitung; $aussenName = [string]$l.aussen
  $dnp = Param $sd 'dn'
  Pruef ($l.einsatz -eq 'abwasser' -and (Hat $l.groesse 'd') -and $l.groesse.d -eq $aussenName -and -not (Hat $l.groesse 'dn') -and $l.kuerzel -eq $a.Kuerzel) "$($a.Kurz): leitung.einsatz abwasser, groesse { d: «$($l.groesse.d)» }, kuerzel «$($l.kuerzel)»"
  Pruef ($dnp -and $dnp.ziel -eq 'dn' -and $dnp.art -eq 'typ' -and $dnp.datentyp -eq 'dn' -and -not (Hat $dnp 'formel') -and @($dnp.quellen).Count -gt 0) "$($a.Kurz): DN der Herstellerunterlage = Typparameter «dn» mit ziel dn und Quelle ($(@($dnp.quellen) -join ', ')), ohne Formel"
  foreach ($fid in $a.Formteile.Keys) {
    $ff = $neuF[$fid]; $zuord = $a.Formteile[$fid]; $ok = $true; $txt = @()
    foreach ($an in @($ff.definition.anschluesse)) { if (-not ((Hat $an 'd') -and [string]$an.d -eq $zuord[[string]$an.name] -and -not (Hat $an 'dn'))) { $ok = $false }; $txt += "$($an.name) = $($an.d)" }
    Pruef $ok "$($ff.kennzeichen) «$($ff.name)»: Anschlüsse über d ($($txt -join ', ')), keine dn"
  }
  $ls = Abfrage 'leitungsserie' @{ familie = $sid } 900
  Pruef ($ls.einsatz -eq 'abwasser' -and $ls.nachAussendurchmesser -eq $true -and $ls.kuerzel -eq $a.Kuerzel -and $ls.groessenparameter.d -eq $aussenName) "$($a.Kurz): leitungsserie einsatz $($ls.einsatz), nachAussendurchmesser $($ls.nachAussendurchmesser), kuerzel $($ls.kuerzel), Grösse über $(Json $ls.groessenparameter)"
  $gr = @($ls.groessen); $gut = @($gr | Where-Object { $_.gueltig }); $schlecht = @($gr | Where-Object { -not $_.gueltig })
  $ohneDi = @($schlecht | Where-Object { ([string]$_.fehler) -match 'innen|wand' })
  Pruef ($schlecht.Count -eq $ohneDi.Count) "$($a.Kurz): $($gut.Count) von $($gr.Count) Grössen gültig$(if ($schlecht.Count) { '; ungültig: ' + (@($schlecht | ForEach-Object { $_.typ + ' (' + (Kurz ([string]$_.fehler) 90) + ')' }) -join '; ') })"
  foreach ($sg in $schlecht) { Offen "$($a.Kurz) $($sg.typ) nicht zeichenbar: Innendurchmesser und Wandstärke fehlen in der Unterlage (Pflichtwerte ohne Quelle, nichts erfunden) – leitung.innen/wand verlangen einen Wert je Typ; d32 wird zeichenbar mit diesen Werten aus einer Unterlage oder mit einem Kern-Ausbau (innen/wand je Typ optional); die fehlende DN ist es nicht." }
  $falschB = @($gut | Where-Object { $_.beschriftung -ne "$($a.Kuerzel) $(Z $_.aussen)" -or -not (Etwa $_.d $_.aussen) })
  Pruef ($falschB.Count -eq 0 -and $gut.Count -gt 0) "$($a.Kurz): Beschriftung aller gültigen Grössen «$($a.Kuerzel) <d>» (Grösse d = Aussendurchmesser)$(if ($falschB.Count) { ', abweichend: ' + (@($falschB | ForEach-Object { $_.typ }) -join ', ') })"
  $falschDn = @($gut | Where-Object { $t = Typ $sd $_.typ; $w = Wert $t 'dn'; -not ((($null -eq $w) -and ($null -eq $_.dnHersteller)) -or (($null -ne $w) -and ($null -ne $_.dnHersteller) -and (Etwa $_.dnHersteller $w))) })
  Pruef ($falschDn.Count -eq 0) "$($a.Kurz): dnHersteller = DN der Unterlage je Typ (nie abgeleitet): $(@($gut | Where-Object { $null -ne $_.dnHersteller } | Select-Object -First 4 | ForEach-Object { 'd' + (Z $_.aussen) + ' → ' + (Z $_.dnHersteller) }) -join ', ') …$(if ($falschDn.Count) { ' ABWEICHEND: ' + (@($falschDn | ForEach-Object { $_.typ }) -join ', ') })"
  $schlechter = @()
  foreach ($g in $gut) {
    $v0g = @($basisSerie[$sid].groessen | Where-Object { $_.typ -eq $g.typ -and $_.gueltig }) | Select-Object -First 1
    if ($v0g) { $v0 = Verf $v0g; $v1 = Verf $g; if ((-not $v1.B -and $v0.B) -or (-not $v1.T -and $v0.T) -or $v1.TW -lt $v0.TW -or $v1.R -lt $v0.R) { $schlechter += "$($g.typ) (vorher B$([int]$v0.B) T$([int]$v0.T)/$($v0.TW) R$($v0.R), jetzt B$([int]$v1.B) T$([int]$v1.T)/$($v1.TW) R$($v1.R))" } }
  }
  $nB = @($gut | Where-Object { $_.formteile.bogen.verfuegbar }).Count; $nT = @($gut | Where-Object { $_.formteile.tstueck.verfuegbar }).Count; $nR = (@($gut | ForEach-Object { @($_.formteile.reduktion | Where-Object { $_.verfuegbar }).Count }) | Measure-Object -Sum).Sum
  Pruef ($schlechter.Count -eq 0) "$($a.Kurz): Formteilwahl über d nicht schlechter als vorher über die DN (Bogen $nB, T-Stück $nT, Reduktionen $nR$(if ($schlechter.Count) { '; schlechter: ' + ($schlechter -join '; ') }))"
  $d110 = @($gut | Where-Object { Etwa $_.aussen 110 }) | Select-Object -First 1
  if ($d110) { $beispiel[$sid] = $d110.beschriftung }
}
Pruef ($beispiel[$Abwasser[1].Serie] -eq 'PE 110') "Beschriftung R7 Geberit PE d110: «$($beispiel[$Abwasser[1].Serie])» (erwartet «PE 110»)"
Pruef ($beispiel[$Abwasser[0].Serie] -eq 'PE-S 110') "Beschriftung R6 Silent-db20 d110: «$($beispiel[$Abwasser[0].Serie])» (erwartet «PE-S 110», nicht «PE-S2»)"

Aus ''
Aus 'Prüfung 3 – Kürzel der Serien:'
$kzSoll = [ordered]@{}
foreach ($id in $KuerzelSonst.Keys) { $kzSoll[$id] = $KuerzelSonst[$id] }
foreach ($a in $Abwasser) { $kzSoll[$a.Serie] = $a.Kuerzel }
foreach ($id in $kzSoll.Keys) {
  $ls = Abfrage 'leitungsserie' @{ familie = $id } 900
  $g1 = @($ls.groessen | Where-Object { $_.gueltig }) | Select-Object -First 1
  Pruef ($ls.kuerzel -eq $kzSoll[$id] -and $g1 -and ([string]$g1.beschriftung).StartsWith($kzSoll[$id] + ' ')) "$($KzVon[$id]) «$($neuF[$id].name)»: kuerzel «$($ls.kuerzel)», erste Grösse $($g1.typ) → Beschriftung «$($g1.beschriftung)»"
}

Aus ''
Aus 'Prüfung 4 – R1 Grösse 66,7 gestrichen:'
foreach ($id in $R1_667.Keys) {
  $d = $neuF[$id].definition
  $rest = @($d.typen | Where-Object { $R1_667[$id] -contains $_.name })
  $mit667 = @($d.typen | Where-Object { $t = $_; @('d', 'da', 'd1') | Where-Object { (Wert $t $_) -eq 66.7 } })
  $vorher = @($fam[$id].definition.typen).Count - @($R1_667[$id] | Where-Object { Typ $fam[$id].definition $_ }).Count
  Pruef ($rest.Count -eq 0 -and $mit667.Count -eq 0 -and @($d.typen).Count -eq $vorher) "$($KzVon[$id]) «$($neuF[$id].name)»: $(@($d.typen).Count) Typen, kein Typ «$($R1_667[$id] -join '», «')» und keiner mit der Grösse 66,7"
}
$kfFam4 = @($neuF['fam-324b7269e42a'].definition.konflikte | Where-Object { $null -ne $_ })
Pruef ($kfFam4.Count -eq 0) "FAM-4: Konflikt zur Grösse 66,7 (k-ad77ab0d84) entfallen, offene Konflikte der Serie: $($kfFam4.Count)"
$ls = Abfrage 'leitungsserie' @{ familie = 'fam-324b7269e42a' } 900
$gr = @($ls.groessen); $gut = @($gr | Where-Object { $_.gueltig })
$ohneFt = @($gut | Where-Object { -not $_.formteile.bogen.verfuegbar -or -not $_.formteile.tstueck.verfuegbar })
Pruef ($gut.Count -eq $gr.Count -and $ohneFt.Count -eq 0) "R1: $($gut.Count) von $($gr.Count) Grössen gültig, Grössen ohne Bogen oder T-Stück: $($ohneFt.Count)$(if ($ohneFt.Count) { ' (' + (@($ohneFt | ForEach-Object { $_.typ }) -join ', ') + ')' }); nur noch eine Grösse mit DN 65"
$dn65 = @($gut | Where-Object { Etwa $_.dn 65 })
Pruef ($dn65.Count -eq 1) "R1: genau eine Grösse mit DN 65: $(@($dn65 | ForEach-Object { $_.typ }) -join ', ')"

Aus ''
Aus 'Prüfung 5 – Quellenvermerke (Beschreibung beginnt mit «Quellenvermerk: … – vor Freigabe prüfen», Bericht-Satz, nichts freigegeben):'
foreach ($id in $Vermerke.Keys) {
  $d = $neuF[$id].definition
  Pruef (([string]$d.beschreibung).StartsWith('Quellenvermerk: ') -and ([string]$d.beschreibung).Contains(' – vor Freigabe prüfen.') -and ([string]$d.bericht).Contains($MQv) -and $neuF[$id].status -eq 'entwurf') "$($KzVon[$id]) «$($neuF[$id].name)»: $(Kurz ([string]$d.beschreibung) 120)"
}

Aus ''
Aus 'Prüfung 6 – K3 Flexrohr AR (v_max 30 m/s, p_max 3000 Pa aus der Broschüre 2025):'
if ($k3Hinweis) { Offen "K3 Flexrohr AR: nichts eingetragen – $k3Hinweis" }
else {
  $d = $neuF[$K3Id].definition; $vm2 = Param $d 'v_max'; $pm2 = Param $d 'p_max'
  Pruef ($vm2.wert -eq 30 -and @($vm2.quellen).Count -eq 1 -and @($vm2.quellen)[0] -eq $K3Wert -and $pm2.wert -eq 3000 -and @($pm2.quellen).Count -eq 1 -and @($pm2.quellen)[0] -eq $K3Wert) "v_max $($vm2.wert) $($vm2.einheit) und p_max $($pm2.wert) $($pm2.einheit), Quelle $K3Wert (Broschüre Zubehör 2025.09 S. 28)"
  Pruef (@($d.konflikte | Where-Object { $null -ne $_ }).Count -eq 0 -and $d.abdeckung.v_max.status -eq 'geprüft' -and $d.abdeckung.p_max.status -eq 'geprüft') "FAM-33: keine offenen Konflikte, Abdeckung v_max «$($d.abdeckung.v_max.status)», p_max «$($d.abdeckung.p_max.status)»"
}

Aus ''
Aus 'Prüfung 7 – Prüfbefunde des Modells (pruefen):'
$pb = Abfrage 'pruefen' @{}
Pruef ($pb.anzahl -eq $basisPruefen.anzahl -and (Json $pb.jeCode) -eq (Json $basisPruefen.jeCode)) "Befunde: $($pb.anzahl) $(Json $pb.jeCode) (vorher $($basisPruefen.anzahl) $(Json $basisPruefen.jeCode))"

Aus ''
Aus 'Nicht angefasst – R1 Bogenmasse-Konflikte (FAM-1), offen für Christian (alt = Datenblatt 2022 laut Zeilenparser des Kerns, neu = Massänderungsliste April 2026; Vorschlag des Kerns = neu):'
$bk = @($neuF[$R1Bogen].definition.konflikte)
Pruef ((Json @($bk | ForEach-Object { $_.id })) -eq (Json @($basisKonflikte[$R1Bogen]))) "FAM-1: die $($bk.Count) Konflikte sind unverändert gegenüber vorher (gleiche Ids)"
foreach ($k in $bk) {
  $wa = @($k.werte | Where-Object { @($_.quellen) | Where-Object { ([string]$_).StartsWith($UlAlt) } }) | Select-Object -First 1
  $wn = @($k.werte | Where-Object { @($_.quellen) | Where-Object { ([string]$_).StartsWith($UlNeu) } }) | Select-Object -First 1
  Aus ("    {0} | Typ {1} | {2} | alt {3} | neu {4} | Vorschlag {5} | {6}" -f $KzVon[$R1Bogen], $k.typ, $k.parameter, $(if ($wa) { Z $wa.wert } else { '?' }), $(if ($wn) { Z $wn.wert } else { '?' }), (Z $k.vorschlag), $k.id)
}
$r2bogen = Lesen 'fam-c86c45b49645'
Aus "    (R2 Bogen $($r2bogen.kennzeichen) «$($r2bogen.name)» hat $(@($r2bogen.definition.konflikte | Where-Object { $null -ne $_ }).Count) weitere offene Konflikte – nicht Teil dieser Pflege, nicht angefasst.)"
Aus 'Nicht bearbeitbar (Leserfehler beim Umschlüsseln der Quellen, ein paralleler Auftrag behebt das):'
foreach ($id in $NichtBearbeitbar.Keys) {
  $f0 = Lesen $id
  $t = Befehl 'familie_aendern' @{ familie = $id; ops = @([ordered]@{ op = 'kopf_setzen'; beschreibung = ([string]$f0.definition.beschreibung + ' ') }) } $true
  if ($t.angenommen) { Aus "    $($NichtBearbeitbar[$id]): lässt sich jetzt speichern (Trockenlauf angenommen) – von dieser Pflege nicht angefasst" }
  else { Aus "    $($NichtBearbeitbar[$id]): nicht speicherbar – $(Kurz ([string]$t.fehler) 150)" }
}

# ------------------------------------------------------------------ Probe (zeichnen, prüfen, zurücknehmen)
if ($Probe) {
  Aus ''
  Aus 'Probe – Geberit PE d110 mit Bogen 45° und Abzweig d56 (45°), Silent-db20 d110 mit Bogen 45°, Rohr d32, dieselbe Serie an einer Heizungsleitung (Systeme PEP-SW, PEP-HZ, Kennzeichen PEP-…):'
  $vorProbe = (Invoke-RestMethod "$u/api/status" -TimeoutSec 15).stand
  $pakete = New-Object System.Collections.ArrayList
  function Echt([string]$name, $par) {
    $e = Befehl $name $par $false
    if ($e.angenommen -and $e.paketId) { [void]$pakete.Add($e.paketId) }
    $e
  }
  function Angelegt($e, [string]$klasse) { @($e.aenderungen | Where-Object { $_.art -eq 'angelegt' -and $_.klasse -eq $klasse } | ForEach-Object { $_.kennzeichen }) }
  function Element([string]$kz) { Abfrage 'element_lesen' @{ element = $kz } }
  function Punkte([string]$kz) { @((Element $kz).geometrie.punkte) }
  function PText($p) { '[' + ((@($p) | ForEach-Object { Z $_ }) -join ', ') + ']' }
  function Gleich($p, $soll) { $a = @($p); if ($a.Count -lt 3) { return $false }; for ($i = 0; $i -lt 3; $i++) { if (-not (Etwa $a[$i] $soll[$i] 0.1)) { return $false } }; $true }
  function Ex([string]$kz) { (Element $kz).eigenschaften.parameter.wert }
  function ElTyp([string]$kz) { (Element $kz).eigenschaften.familientyp.wert }
  $z = 500; $c45 = [Math]::Cos([Math]::PI / 4)
  $idR7 = $Abwasser[1].Serie; $idR6 = $Abwasser[0].Serie
  $sdR7 = $neuF[$idR7].definition; $sdR6 = $neuF[$idR6].definition
  try {
    $e = Echt 'element_anlegen' @{ klasse = 'System'; kennzeichen = 'PEP-SW'; eigenschaften = @{ systemtyp = 'Schmutzwasser' } }
    Pruef $e.angenommen "System PEP-SW (Schmutzwasser): $($e.meldung) $($e.fehler)"
    $e = Echt 'element_anlegen' @{ klasse = 'System'; kennzeichen = 'PEP-HZ'; eigenschaften = @{ systemtyp = 'Heizung Vorlauf' } }
    Pruef $e.angenommen "System PEP-HZ (Heizung Vorlauf): $($e.meldung) $($e.fehler)"

    # --- Geberit PE d110 mit Bogen 45° (Einbaulänge 10 · X45 aus dem Katalog, cm)
    $bogenR7 = Typ $neuF['fam-31b8fa8f52f1'].definition 'd110'
    $laBogen = 10 * (Wert $bogenR7 'X45')
    $zeichnen = [ordered]@{ serie = $idR7; groesse = [ordered]@{ d = 110 }; punkte = @(@(0, 0, $z), @(3000, 0, $z), @(5000, 2000, $z)); system = 'PEP-SW'; praefix = 'PEP' }
    $v = Befehl 'leitung_zeichnen' $zeichnen $true
    Pruef ($v.angenommen -and @($v.hinweise | Where-Object { $_ -like "Bogen * Typ «d110», 45° *Einbaulängen a $(Z $laBogen) mm, b $(Z $laBogen) mm*" }).Count -eq 1) "Trockenlauf leitung_zeichnen { d: 110 }: Bogen Typ «d110», 45°, Einbaulängen a $(Z $laBogen) mm, b $(Z $laBogen) mm$(if (-not $v.angenommen) { ' – ' + $v.fehler })"
    $e = Echt 'leitung_zeichnen' $zeichnen
    Pruef $e.angenommen "leitung_zeichnen Geberit PE d110: $($e.meldung) $($e.fehler)"
    $bo = (Angelegt $e 'Bogen') | Select-Object -First 1
    $k1 = Punkte 'PEP-1'; $k2 = Punkte 'PEP-2'
    Pruef ((Gleich $k1[-1] @((3000 - $laBogen), 0, $z)) -and (Gleich $k2[0] @((3000 + $laBogen * $c45), ($laBogen * $c45), $z))) "Bogen $bo 45°: PEP-1 endet bei $(PText $k1[-1]), PEP-2 beginnt bei $(PText $k2[0]) (Einbaulänge $(Z $laBogen) mm = 10 · X45 = 10 · $(Z (Wert $bogenR7 'X45')) cm)"
    Pruef ((ElTyp $bo) -eq 'd110' -and (Ex $bo).winkel -eq 45) "Bogen $bo Typ «$(ElTyp $bo)», Exemplarwerte $(Json (Ex $bo))"
    $ep = Element 'PEP-1'; $dnTyp = Wert (Typ $sdR7 'd110') 'dn'
    Pruef ($ep.eigenschaften.familientyp.wert -eq 'd110' -and (Etwa $ep.eigenschaften.durchmesser.wert 110) -and ([string]$ep.eigenschaften.durchmesser.herkunft).StartsWith('familie') -and (Etwa $ep.eigenschaften.dn.wert $dnTyp) -and ([string]$ep.eigenschaften.dn.herkunft).Contains('Typ d110') -and $null -eq @($ep.anschluesse)[0].dn -and @($ep.anschluesse)[0].d -eq 110) "PEP-1: Typ «d110», durchmesser 110 (Herkunft Familie), dn $(Z $ep.eigenschaften.dn.wert) = DN der Unterlage ($(Kurz ([string]$ep.eigenschaften.dn.herkunft) 90)), Anschlüsse mit d 110 ohne dn; abgeleitete Rohrgrösse «$($ep.abgeleitet.rohrgroesse)»"

    # --- Abzweig d56 unter 45° (T-Stück Typ «d110/56»; Einbaulängen a = 10 · X3, b = 10 · X1, c = 10 · X2 aus dem Katalog, cm)
    $tt = Typ $neuF['fam-7c4ceac4df82'].definition 'd110/56'
    $la = 10 * (Wert $tt 'X3_45'); $lb = 10 * (Wert $tt 'X1_45'); $lc = 10 * (Wert $tt 'X2_45')
    $abz = [ordered]@{ leitung = 'PEP-1'; punkt = @(1500, 0, $z); richtung = @(-1, -1, 0); laenge = 1000; groesse = [ordered]@{ d = 56 }; praefix = 'PEP-A' }
    $e = Echt 'leitung_abzweigen' $abz
    if (-not $e.angenommen) { Offen "leitung_abzweigen unter 45° nicht möglich: $(Kurz ([string]$e.fehler) 250) – Probe ohne Abzweig" }
    else {
      Pruef $true "leitung_abzweigen PEP-1 bei x = 1500, Abzweig d56 unter 45°: $($e.meldung)"
      foreach ($h in @($e.hinweise | Where-Object { $_ -like '*T-St*' })) { Aus "    Hinweis: $(Kurz ([string]$h) 220)" }
      $ts = (Angelegt $e 'TStueck') | Select-Object -First 1
      $kab = @(Angelegt $e 'Rohr' | Where-Object { $_ -notlike 'PEP-1-*' }) | Select-Object -First 1
      Pruef ($ts -and (ElTyp $ts) -eq 'd110/56' -and (Ex $ts).winkel -eq 45) "T-Stück $ts Typ «$(ElTyp $ts)», Exemplarwerte $(Json (Ex $ts))"
      $k1 = Punkte 'PEP-1'; $k12 = Punkte 'PEP-1-2'; $ka = Punkte $kab
      Pruef ((Gleich $k1[-1] @((1500 - $la), 0, $z)) -and (Gleich $k12[0] @((1500 + $lb), 0, $z))) "Durchgang: PEP-1 endet bei $(PText $k1[-1]), PEP-1-2 beginnt bei $(PText $k12[0]) (Einbaulängen a $(Z $la) mm = 10 · X3_45, b $(Z $lb) mm = 10 · X1_45)"
      Pruef ((Gleich $ka[-1] @((1500 - $lc * $c45), (-$lc * $c45), $z))) "Abzweig $kab endet am T-Stück bei $(PText $ka[-1]) (Einbaulänge c $(Z $lc) mm = 10 · X2_45 unter 45°)"
      $eab = Element $kab
      Pruef ($eab.eigenschaften.familientyp.wert -eq 'd56' -and (Etwa $eab.eigenschaften.durchmesser.wert 56)) "Abzweig ${kab}: Typ «$($eab.eigenschaften.familientyp.wert)», durchmesser $(Z $eab.eigenschaften.durchmesser.wert), dn $(Z $eab.eigenschaften.dn.wert) (Herstellerangabe)"
    }

    # --- Rohr d32 (ohne DN der Unterlage)
    $d32 = [ordered]@{ serie = $idR7; groesse = [ordered]@{ d = 32 }; punkte = @(@(0, 5000, $z), @(2000, 5000, $z)); system = 'PEP-SW'; praefix = 'PEP-D' }
    $v = Befehl 'leitung_zeichnen' $d32 $true
    if ($v.angenommen) {
      $e = Echt 'leitung_zeichnen' $d32
      $ed = Element 'PEP-D-1'
      Pruef ($e.angenommen -and $ed.eigenschaften.familientyp.wert -eq 'd32' -and (Etwa $ed.eigenschaften.durchmesser.wert 32) -and $null -eq $ed.eigenschaften.dn) "Rohr d32 gezeichnet: Typ «d32», durchmesser 32, keine DN erfunden"
    } else { Offen "Rohr d32 nicht zeichenbar: $(Kurz ([string]$v.fehler) 120) – Grund: der Typ d32 hat in der Unterlage weder Innendurchmesser noch Wandstärke (leitung.innen/wand brauchen einen Wert je Typ); die fehlende DN ist es nicht" }

    # --- Geberit Silent-db20 d110 mit Bogen 45° (Einbaulängen a = X45, b = X1_45 in mm)
    $bogenR6 = Typ $neuF['fam-ff98af86928f'].definition 'd110'
    $laS = Wert $bogenR6 'X45'; $lbS = Wert $bogenR6 'X1_45'
    $silent = [ordered]@{ serie = $idR6; groesse = [ordered]@{ d = 110 }; punkte = @(@(0, 8000, $z), @(2000, 8000, $z), @(3500, 9500, $z)); system = 'PEP-SW'; praefix = 'PEP-S' }
    $e = Echt 'leitung_zeichnen' $silent
    Pruef $e.angenommen "leitung_zeichnen Geberit Silent-db20 d110: $($e.meldung) $($e.fehler)"
    $bs = (Angelegt $e 'Bogen') | Select-Object -First 1
    $s1 = Punkte 'PEP-S-1'; $s2 = Punkte 'PEP-S-2'
    Pruef ((Gleich $s1[-1] @((2000 - $laS), 8000, $z)) -and (Gleich $s2[0] @((2000 + $lbS * $c45), (8000 + $lbS * $c45), $z))) "Silent-Bogen $bs 45°: PEP-S-1 endet bei $(PText $s1[-1]), PEP-S-2 beginnt bei $(PText $s2[0]) (Einbaulängen a $(Z $laS) mm = X45, b $(Z $lbS) mm = X1_45)"
    $es = Element 'PEP-S-1'
    Pruef ($es.eigenschaften.familientyp.wert -eq '110 x 6' -and $es.abgeleitet.rohrgroesse -eq '110') "PEP-S-1: Typ «$($es.eigenschaften.familientyp.wert)», Rohrgrösse «$($es.abgeleitet.rohrgroesse)» ($($es.abgeleitet.rohrgroesseQuelle)); Beschriftung der Serie «$($beispiel[$idR6])»"

    # --- dieselbe Serie an einer Heizungsleitung: P30
    $hz = [ordered]@{ serie = $idR7; groesse = [ordered]@{ d = 110 }; punkte = @(@(0, 12000, $z), @(2000, 12000, $z)); system = 'PEP-HZ'; praefix = 'PEP-H' }
    $e = Echt 'leitung_zeichnen' $hz
    Pruef $e.angenommen "leitung_zeichnen Geberit PE d110 im System PEP-HZ (Heizung Vorlauf): $($e.meldung) $($e.fehler)"
    $pb = Abfrage 'pruefen' @{}
    $p30 = @($pb.befunde | Where-Object { $_.code -eq 'P30' })
    Pruef ($p30.Count -eq 1 -and $p30[0].element -eq 'PEP-H-1' -and $p30[0].schwere -eq 'fehler') "P30 an der Heizungsleitung: $(@($p30 | ForEach-Object { $_.schwere + ' ' + $_.element + ': ' + (Kurz ([string]$_.text) 150) }) -join ' | ')"

    # --- sonst nur Befunde, die zur Probe gehören: P01 (offene Enden), P11 (kein Geschoss im Arbeitsprojekt), P16 (kein Gefälle gezeichnet)
    $fremd = @($pb.befunde | Where-Object { $_.element -notlike 'PEP*' -and $_.element -notlike 'BO-*' -and $_.element -notlike 'TS-*' })
    $unerwartet = @($pb.befunde | Where-Object { $_.code -notin @('P01', 'P11', 'P16', 'P30') })
    Pruef ($fremd.Count -eq 0 -and $unerwartet.Count -eq 0) "pruefen nach der Probe: $($pb.anzahl) Befunde nur an Elementen der Probe und nur P01, P11, P16 (Probe ohne Geschoss, Enden, Gefälle) und der erwartete P30; andere Elemente $($fremd.Count), andere Codes $($unerwartet.Count)$(if ($unerwartet.Count) { ', z. B. ' + $unerwartet[0].code + ' ' + $unerwartet[0].element })"
    $leitungen = @($pb.befunde | Where-Object { $_.code -in @('P13', 'P14', 'P15', 'P18', 'P29') })
    Pruef ($leitungen.Count -eq 0) "Leitungsprüfungen P13/P14/P15/P18/P29 ohne Befund ($($leitungen.Count))"
  } catch {
    Pruef $false "Probe abgebrochen: $($_.Exception.Message)"
  } finally {
    if ($ProbeBehalten) { Aus "  Probe bleibt im Projekt ($($pakete.Count) Pakete)." }
    else {
      $zurueck = 0
      for ($i = $pakete.Count - 1; $i -ge 0; $i--) {
        $r = Befehl 'paket_zuruecknehmen' @{ paket = $pakete[$i] } $false
        if ($r.angenommen) { $zurueck++ } else { Aus "    paket_zuruecknehmen $($pakete[$i]): $($r.fehler)" }
      }
      $nach = (Invoke-RestMethod "$u/api/status" -TimeoutSec 15).stand
      Pruef ($zurueck -eq $pakete.Count -and $nach.pruefsumme -eq $vorProbe.pruefsumme -and $nach.elemente -eq $vorProbe.elemente) "Probe zurückgenommen: $zurueck von $($pakete.Count) Paketen, Prüfsumme $($nach.pruefsumme) (vorher $($vorProbe.pruefsumme)), Elemente $($nach.elemente)"
    }
  }
}

Aus ''
Aus "Prüfungen: $($script:pruefZahl), bestanden: $($script:pruefZahl - $script:fehlerZahl), offen gemeldet: $($script:offenZahl)"
if ($script:fehlerZahl -eq 0) { Aus 'Ergebnis: alles grün.'; exit 0 }
Aus "Ergebnis: $($script:fehlerZahl) Prüfung(en) nicht grün."
exit 1

# Grundausstattung K2 «Rechteckkanal» (Schmidlin): Umstellung auf freie Grösse b × h – entwurf/freie-groesse-kanal.md, Abschnitt
# «Daten»; Entscheid Christian 20.10.2026: «Rechteckkanäle mit freier Grösse b × h (Normraster nur als Vorschlag); Formteile rechnen
# ihre Masse aus der Formel». Alles über die Kern-API des laufenden Servers, nichts an den Projektdateien vorbei.
#
# Aufruf (Server der Grundausstattung, werkzeuge\grundausstattung_server.ps1, Port 5153 – gebaut aus main ab 166e8a9 «freie Grösse»):
#   powershell -NoProfile -ExecutionPolicy Bypass -File werkzeuge\grundausstattung_k2_frei.ps1               Trockenlauf (ändert nichts)
#   powershell ... -File werkzeuge\grundausstattung_k2_frei.ps1 -Ausfuehren                                  Umstellung + Prüfungen
#   powershell ... -File werkzeuge\grundausstattung_k2_frei.ps1 -Ausfuehren -Probe                           dazu Probe zeichnen
#   -Probe allein prüft einen schon umgestellten Stand. Die Probe wird danach zurückgenommen (paket_zuruecknehmen, Prüfsumme des
#   Modells wie vorher), ausser mit -ProbeBehalten. -Port wählt den Server (Vorgabe 5153). Ein zweiter Lauf ändert nichts mehr.
#
# Was es tut (Familien über ihre id, Kennzeichen im Arbeitsprojekt FAM-41 … FAM-44), je Familie ein familie_entwurf mit der
# vollständigen Definition (Version + 1, Status entwurf; derselbe Speicherweg wie familie_aendern – familie_aendern kann die
# Quellen der Massbezüge «masse» nicht ändern, siehe Umschlüsselung):
#   «Rechteckkanal Bogen» (fam-361fa68eb5fa): breite/hoehe → Exemplarparameter ohne Formel (Vorgabe 400/200), 400 Typen «a x b» →
#       ein Vorlage-Typ «209 BN / 210.1 BS» (ri 115, lv 35 bleiben Typparameter, Formel L = (ri + breite/2)·tan(w/2) + lv unverändert).
#   «Rechteckkanal T-Stück» (fam-ef2f6666b5ff): breite/hoehe (Durchgang) und neu breite_c/hoehe_c (Abzweig c) als Exemplarparameter;
#       L und Lc werden Exemplarparameter mit den Formeln der Familienbeschreibung (Durchgang L = l1 + a3/2 = ri + lv + breite_c/2,
#       Abzweig Lc = l2 + a1/2 = ri + lv + breite/2); Anschluss c und Körper auf den Abzweig; 400 Typen → Vorlage-Typ «216 TS».
#   «Rechteckkanal Reduktion» (fam-7ef0df5e5db1): bleibt ein Typ «214 KO» mit Exemplar-Grössen; Beschreibung und Nachtrag.
#   «Rechteckkanal» (Serie, fam-5577b119d3db): leitung.frei { vorlage «400 x 200», grenzen b/h 145 … 2500, schritt 1, quellen } und
#       leitung.formteile.<art>.vorlage; die 400 Typen bleiben als Vorschlagsraster.
#   Je Familie: Beschreibung nachgeführt, Bericht mit Nachtrag «freie Grösse, Umbau Hauptlauf/hlks-kern 21.10.2026».
#   Umschlüsselung: Die Familien stammen vom PDF-Leser 1; der Kern ab c20e863 liest mit Leser 2, der die Befunde des Preisbuchs
#       anders nummeriert (ab S. 19 + 21). Ohne Umschlüsselung lehnt die Quellenprüfung jede Änderung ab. Die Tabelle unten ordnet
#       jede alte Id der neuen mit gleicher Seite, Art, Text und Box zu (aus den Befund-Ablagen pdf-1/pdf-2 erzeugt); das Skript prüft
#       jede neue Id vor der Änderung über die Abfrage befunde und bricht ab, wenn eine nicht stimmt oder eine unbekannte Id vorkommt.
# Quellen der neuen Werte (Schmidlin Preisbuch Kanäle, schmidlin_preisbuch_kanaele_07_2026.pdf, Prüfsumme aae0fb6a93a9…, Leser 2):
#   S. 19 «minimale Seitenhöhe: 145 mm» = aae0fb6a93a9#13044; Normkanal-Liste «145 | 200 | … | 2500» = aae0fb6a93a9#13021.
#   ANNAHMEN (im Bericht genannt): Obergrenze 2500 = grösste Normkanal-Grösse, Schritt 1 mm, Abzweighöhe des T-Stücks frei.
# Prüfungen am Ende: Familien gültig, Abfrage leitungsserie (frei, Probe, Raster 400 x 200, Formteile je Rastergrösse), pruefen;
#   mit -Probe: Kanal 430 × 270 mit Bogen 90° (Einbaulänge 365 mm), Reduktion auf 400 × 200, Abzweig 250 × 200, Raster 400 × 200,
#   100 × 100 abgelehnt bzw. P29-Fehler.
# Exit 0 = alles grün, 1 = eine Prüfung bzw. ein Befehl nicht grün, 2 = abgebrochen (Server, Voraussetzung, Umschlüsselung).
# Datei mit UTF-8-BOM speichern (PowerShell 5.1 liest sonst «» und Umlaute falsch – Befund 20.10.2026).
param([int]$Port = 5153, [switch]$Ausfuehren, [switch]$Probe, [switch]$ProbeBehalten)
$ErrorActionPreference = 'Stop'
$u = "http://localhost:$Port"

# ------------------------------------------------------------------ Festwerte
$IdSerie = 'fam-5577b119d3db'; $IdBogen = 'fam-361fa68eb5fa'; $IdT = 'fam-ef2f6666b5ff'; $IdRed = 'fam-7ef0df5e5db1'
$VorlageSerie = '400 x 200'
$VorlageBogen = '209 BN / 210.1 BS'
$VorlageT = '216 TS'
$VorlageRed = '214 KO'
$QMin = 'aae0fb6a93a9#13044'      # S. 19 «Preisberechnung: l = 1.85 x Grundpreis | minimale Seitenhöhe: 145 mm»
$QListe = 'aae0fb6a93a9#13021'    # S. 19 Normkanal-Liste «145 | 200 | 300 | … | 2250 | 2500»
$QAbwicklung = 'aae0fb6a93a9#13046'   # S. 19 «minimale Abwicklung: 600 mm» (nur im Bericht genannt)
$Marke = 'NACHTRAG 21.10.2026 (freie Grösse, Umbau Hauptlauf/hlks-kern 21.10.2026'

# Umschlüsselung Leser 1 → Leser 2: alte Id = @(neue Id, Seite, Art, Wert, Box) – Wert und Box des Befunds (bei beiden Lesern gleich)
$Umschl = @{
  '0a9e8013edd5#3' = @('0a9e8013edd5#3', 1, 'bild', '{"pixelBreite":1170,"pixelHoehe":496}', '40.07,401.99,295.63,510.43')
  'aae0fb6a93a9#12400' = @('aae0fb6a93a9#12421', 19, 'wort', '145', '69.14,496.96,80.09,502.54')
  'aae0fb6a93a9#12401' = @('aae0fb6a93a9#12422', 19, 'wort', '200', '93.65,496.96,104.59,502.62')
  'aae0fb6a93a9#12402' = @('aae0fb6a93a9#12423', 19, 'wort', '300', '118.15,496.96,129.09,502.62')
  'aae0fb6a93a9#12403' = @('aae0fb6a93a9#12424', 19, 'wort', '400', '142.65,496.96,153.6,502.62')
  'aae0fb6a93a9#12404' = @('aae0fb6a93a9#12425', 19, 'wort', '500', '167.16,496.96,178.1,502.62')
  'aae0fb6a93a9#12405' = @('aae0fb6a93a9#12426', 19, 'wort', '600', '191.66,496.96,202.61,502.62')
  'aae0fb6a93a9#12406' = @('aae0fb6a93a9#12427', 19, 'wort', '700', '216.17,496.96,227.11,502.62')
  'aae0fb6a93a9#12407' = @('aae0fb6a93a9#12428', 19, 'wort', '800', '240.67,496.96,251.61,502.62')
  'aae0fb6a93a9#12408' = @('aae0fb6a93a9#12429', 19, 'wort', '900', '265.17,496.96,276.12,502.62')
  'aae0fb6a93a9#12409' = @('aae0fb6a93a9#12430', 19, 'wort', '1000', '287.83,496.96,302.47,502.62')
  'aae0fb6a93a9#12410' = @('aae0fb6a93a9#12431', 19, 'wort', '1100', '312.33,496.96,326.97,502.62')
  'aae0fb6a93a9#12411' = @('aae0fb6a93a9#12432', 19, 'wort', '1200', '336.84,496.96,351.48,502.62')
  'aae0fb6a93a9#12412' = @('aae0fb6a93a9#12433', 19, 'wort', '1300', '361.34,496.96,375.98,502.62')
  'aae0fb6a93a9#12413' = @('aae0fb6a93a9#12434', 19, 'wort', '1400', '385.85,496.96,400.49,502.62')
  'aae0fb6a93a9#12414' = @('aae0fb6a93a9#12435', 19, 'wort', '1500', '410.35,496.96,424.99,502.62')
  'aae0fb6a93a9#12415' = @('aae0fb6a93a9#12436', 19, 'wort', '1600', '434.85,496.96,449.49,502.62')
  'aae0fb6a93a9#12416' = @('aae0fb6a93a9#12437', 19, 'wort', '1800', '459.36,496.96,474,502.62')
  'aae0fb6a93a9#12417' = @('aae0fb6a93a9#12438', 19, 'wort', '2000', '483.86,496.96,498.5,502.62')
  'aae0fb6a93a9#12418' = @('aae0fb6a93a9#12439', 19, 'wort', '2250', '508.37,496.96,523.01,502.62')
  'aae0fb6a93a9#12419' = @('aae0fb6a93a9#12440', 19, 'wort', '2500', '532.87,496.96,547.51,502.62')
  'aae0fb6a93a9#12421' = @('aae0fb6a93a9#12442', 19, 'wort', '145', '49.3,480.66,60.25,486.25')
  'aae0fb6a93a9#12442' = @('aae0fb6a93a9#12463', 19, 'wort', '200', '49.31,467.91,60.25,473.57')
  'aae0fb6a93a9#12464' = @('aae0fb6a93a9#12485', 19, 'wort', '300', '49.32,455.16,60.26,460.82')
  'aae0fb6a93a9#12486' = @('aae0fb6a93a9#12507', 19, 'wort', '400', '49.33,442.41,60.27,448.06')
  'aae0fb6a93a9#12508' = @('aae0fb6a93a9#12529', 19, 'wort', '500', '49.33,429.66,60.28,435.31')
  'aae0fb6a93a9#12529' = @('aae0fb6a93a9#12550', 19, 'wort', '600', '49.34,416.9,60.29,422.56')
  'aae0fb6a93a9#12551' = @('aae0fb6a93a9#12572', 19, 'wort', '700', '49.35,404.15,60.29,409.81')
  'aae0fb6a93a9#12572' = @('aae0fb6a93a9#12593', 19, 'wort', '800', '49.36,391.4,60.3,397.06')
  'aae0fb6a93a9#12594' = @('aae0fb6a93a9#12615', 19, 'wort', '900', '49.37,378.65,60.31,384.3')
  'aae0fb6a93a9#12615' = @('aae0fb6a93a9#12636', 19, 'wort', '1000', '45.68,365.9,60.32,371.55')
  'aae0fb6a93a9#12637' = @('aae0fb6a93a9#12658', 19, 'wort', '1100', '45.69,353.14,60.33,358.8')
  'aae0fb6a93a9#12658' = @('aae0fb6a93a9#12679', 19, 'wort', '1200', '45.69,340.39,60.33,346.05')
  'aae0fb6a93a9#12680' = @('aae0fb6a93a9#12701', 19, 'wort', '1300', '45.7,327.64,60.34,333.3')
  'aae0fb6a93a9#12701' = @('aae0fb6a93a9#12722', 19, 'wort', '1400', '45.71,314.89,60.35,320.54')
  'aae0fb6a93a9#12723' = @('aae0fb6a93a9#12744', 19, 'wort', '1500', '45.72,302.14,60.36,307.79')
  'aae0fb6a93a9#12744' = @('aae0fb6a93a9#12765', 19, 'wort', '1600', '45.73,289.38,60.37,295.04')
  'aae0fb6a93a9#12766' = @('aae0fb6a93a9#12787', 19, 'wort', '1800', '45.73,276.63,60.37,282.29')
  'aae0fb6a93a9#12787' = @('aae0fb6a93a9#12808', 19, 'wort', '2000', '45.74,263.88,60.38,269.54')
  'aae0fb6a93a9#12809' = @('aae0fb6a93a9#12830', 19, 'wort', '2250', '45.75,251.13,60.39,256.78')
  'aae0fb6a93a9#12830' = @('aae0fb6a93a9#12851', 19, 'wort', '2500', '45.32,238.3,60.25,243.96')
  'aae0fb6a93a9#12999' = @('aae0fb6a93a9#13020', 19, 'zeile', 'a', '56.4,503.28,60.85,507.44')
  'aae0fb6a93a9#13001' = @('aae0fb6a93a9#13022', 19, 'zeile', 'b', '46.12,493.01,51.01,498.78')
  'aae0fb6a93a9#13027' = @('aae0fb6a93a9#13048', 19, 'zeile', 'Material: nur Stahlblech sendzimirverzinkt', '42.26,173.61,206.37,179.49')
  'aae0fb6a93a9#18433' = @('aae0fb6a93a9#18454', 22, 'zeile', 'Normlänge: 1000 mm', '42.52,202.82,125.28,208.6')
  'aae0fb6a93a9#25961' = @('aae0fb6a93a9#25982', 26, 'zeile', 'Bogen verfügen über einen inneren Radius von 115 mm und sind 35 mm verlängert', '42.52,202.82,360.25,208.7')
  'aae0fb6a93a9#27774' = @('aae0fb6a93a9#27795', 27, 'zeile', 'Bogen schräg 10° – 60°', '121.89,753.65,207.04,759.43')
  'aae0fb6a93a9#31234' = @('aae0fb6a93a9#31255', 29, 'zeile', 'T-Stücke verfügen über einen inneren Radius von 115 mm und sind 35 mm verlängert', '42.52,202.82,368.22,208.7')
}
# neue Quellen dieses Umbaus (Leser 2), vor der Änderung ebenso geprüft
$NeuQuellen = @{
  $QMin = @(19, 'zeile', 'Preisberechnung: l = 1.85 x Grundpreis | minimale Seitenhöhe: 145 mm')
  $QListe = @(19, 'zeile', '145 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1500 | 1600 | 1800 | 2000 | 2250 | 2500')
  $QAbwicklung = @(19, 'zeile', 'minimale Abwicklung: 600 mm')
}
$Umschluessel = "Befund-Ids aller Quellen auf PDF-Leser 2 umgeschlüsselt (gleiche Stelle, gleicher Text, gleiche Box; Preisbuch Kanäle ab S. 19 Nummer + 21, z. B. #12400 → #12421 «145», #25961 → #25982); Ids im Text oben noch nach Leser 1."

# ------------------------------------------------------------------ Hilfen
$script:fehlerZahl = 0
function Json($o) { ConvertTo-Json -InputObject $o -Depth 60 -Compress }
function Kopie($o) { ConvertFrom-Json -InputObject (Json $o) }
function Setze($o, [string]$feld, $wert) { $o | Add-Member -NotePropertyName $feld -NotePropertyValue $wert -Force }
function Ohne($o, [string]$feld) { if ($o.PSObject.Properties[$feld]) { $o.PSObject.Properties.Remove($feld) } }
function Aus([string]$t) { Write-Host $t }
function Pruef([bool]$ok, [string]$t) { if ($ok) { Write-Host "  [ok]     $t" } else { Write-Host "  [FEHLER] $t"; $script:fehlerZahl++ } }
function Z($x) { if ($null -eq $x) { 'null' } else { ([double]$x).ToString('0.###', [Globalization.CultureInfo]::InvariantCulture) } }
function Etwa($a, $b) { $null -ne $a -and [Math]::Abs([double]$a - [double]$b) -le 0.01 }

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
function ErgebnisZeigen($e, [int]$maxHinweise = 6) {
  if ($null -eq $e) { Aus '    (keine Antwort)'; return }
  $m = [string]$e.meldung; if ($m.Length -gt 1500) { $m = $m.Substring(0, 1500) + ' …' }
  Aus "    $m"
  if ($e.vorschlag) { $v = [string]$e.vorschlag; if ($v.Length -gt 600) { $v = $v.Substring(0, 600) + ' …' }; Aus "    Vorschlag: $v" }
  $h = @($e.hinweise)
  foreach ($x in ($h | Select-Object -First $maxHinweise)) { Aus "    Hinweis: $x" }
  if ($h.Count -gt $maxHinweise) { Aus "    … $($h.Count - $maxHinweise) weitere Hinweise" }
}
function Param($def, [string]$name) { @($def.parameter | Where-Object { $_.name -eq $name }) | Select-Object -First 1 }
function Anschluss($def, [string]$name) { @($def.anschluesse | Where-Object { $_.name -eq $name }) | Select-Object -First 1 }
function Koerper($def, [string]$id) { @($def.koerper | Where-Object { $_.id -eq $id }) | Select-Object -First 1 }
# Eintrag gleichen Schlüssels in einer Liste der Definition ersetzen bzw. nach $nach einfügen (wie familie_aendern *_setzen)
function ListeSetzen($def, [string]$feld, [string]$schluessel, $eintrag, [string]$nach = '') {
  $alt = @($def.$feld); $neu = New-Object System.Collections.ArrayList; $gesetzt = $false
  foreach ($x in $alt) {
    if ($x.$schluessel -eq $eintrag.$schluessel) { [void]$neu.Add($eintrag); $gesetzt = $true } else { [void]$neu.Add($x) }
    if (-not $gesetzt -and $nach -and $x.$schluessel -eq $nach) { [void]$neu.Add($eintrag); $gesetzt = $true }
  }
  if (-not $gesetzt) { [void]$neu.Add($eintrag) }
  Setze $def $feld ([object[]]$neu.ToArray())
}

# Satz ersetzen; fehlt der alte Satz und steht der neue noch nicht drin, wird der neue angehängt
function Ersetzen([string]$text, [string]$alt, [string]$neu) {
  if ($text.Contains($neu)) { return $text }
  if ($text.Contains($alt)) { return $text.Replace($alt, $neu) }
  return ($text.TrimEnd() + ' ' + $neu)
}
function Nachtrag([string]$bericht, [string]$text) {
  if ($bericht -and $bericht.Contains($Marke)) { return $bericht }
  if ([string]::IsNullOrWhiteSpace($bericht)) { return $text }
  return ($bericht.TrimEnd() + ' ' + $text)
}
function ExemplarParameter($alt, [string]$name, [double]$wert, [string]$beschreibung, [string[]]$quellen) {
  $p = Kopie $alt
  Setze $p 'name' $name; Setze $p 'art' 'exemplar'; Setze $p 'wert' $wert; Setze $p 'beschreibung' $beschreibung; Setze $p 'quellen' ([object[]]$quellen)
  Ohne $p 'formel'; Ohne $p 'ziel'
  $p
}

# Quellen umschlüsseln (alle Listen «quellen» der Definition, auch typen[].quellen.<parameter>)
$script:unbekannt = @{}
function Ids($liste) {
  $r = New-Object System.Collections.ArrayList
  foreach ($id in @($liste)) {
    if ($Umschl.ContainsKey([string]$id)) { [void]$r.Add($Umschl[[string]$id][0]) } else { $script:unbekannt[[string]$id] = $true; [void]$r.Add($id) }
  }
  , ([object[]]$r.ToArray())
}
function Umschluesseln($o) {
  if ($null -eq $o) { return }
  if ($o -is [System.Array]) { foreach ($x in $o) { Umschluesseln $x }; return }
  if ($o -isnot [System.Management.Automation.PSCustomObject]) { return }
  foreach ($pr in @($o.PSObject.Properties)) {
    if ($pr.Name -eq 'quellen') {
      $v = $pr.Value
      if ($v -is [System.Management.Automation.PSCustomObject]) { foreach ($q in @($v.PSObject.Properties)) { Setze $v $q.Name (Ids $q.Value) } }
      elseif ($null -ne $v) { Setze $o 'quellen' (Ids $v) }
    } else { Umschluesseln $pr.Value }
  }
}

# ------------------------------------------------------------------ Server und Familien lesen
Aus "K2 Rechteckkanal – freie Grösse b × h ($(if ($Ausfuehren) { 'AUSFÜHREN' } else { 'Trockenlauf' })$(if ($Probe) { ', Probe' }), Server $u)"
try { $st = Invoke-RestMethod "$u/api/status" -TimeoutSec 15 } catch { Aus "Server $u antwortet nicht: $($_.Exception.Message)"; exit 2 }
if (-not $st.offen) { Aus "Auf $u ist kein Projekt offen."; exit 2 }
Aus "Projekt: $($st.projekt) ($($st.ort)), Stand $($st.stand.pakete) Pakete, Prüfsumme $($st.stand.pruefsumme)"
$fae = Abfrage 'faehigkeiten' @{}
if ((Json $fae) -notmatch 'leitung\.frei') { Aus 'Der Server kennt die freie Grösse (leitung.frei) nicht – Server aus main ab 166e8a9 bauen und neu starten.'; exit 2 }

function Lesen([string]$id) { Abfrage 'familie' @{ familie = $id } }
$serie = Lesen $IdSerie; $bogen = Lesen $IdBogen; $tst = Lesen $IdT; $red = Lesen $IdRed
$alle = @($serie, $bogen, $tst, $red)
function Umgestellt($f) { [string]$f.definition.bericht -and ([string]$f.definition.bericht).Contains($Marke) }
foreach ($f in $alle) { Aus ("  {0} «{1}» ({2}, v{3}, {4}, {5} Typen, gültig {6}{7})" -f $f.kennzeichen, $f.name, $f.id, $f.version, $f.status, @($f.definition.typen).Count, $f.gueltig, $(if (Umgestellt $f) { ', schon umgestellt' } else { '' })) }
$offen = @($alle | Where-Object { -not (Umgestellt $_) })

# Voraussetzungen (nur für noch nicht umgestellte Familien)
$vor = @()
if ($serie.name -ne 'Rechteckkanal' -or $serie.kategorie -ne 'Kanal') { $vor += "Serie $IdSerie heisst nicht «Rechteckkanal» (Kanal)" }
if ($bogen.kategorie -ne 'Bogen') { $vor += "$IdBogen ist kein Bogen" }
if ($tst.kategorie -ne 'TStueck') { $vor += "$IdT ist kein T-Stück" }
if ($red.kategorie -ne 'Reduktion') { $vor += "$IdRed ist keine Reduktion" }
foreach ($f in $alle) { if (-not $f.gueltig) { $vor += "$($f.kennzeichen) «$($f.name)» ist ungültig: $(Json $f.definitionsfehler)" } }
if (-not (Umgestellt $serie)) {
  $sl = $serie.definition.leitung
  if ($sl.groesse.b -ne 'breite' -or $sl.groesse.h -ne 'hoehe') { $vor += 'Serie: leitung.groesse ist nicht { b: breite, h: hoehe }' }
  if (-not (@($serie.definition.typen | Where-Object { $_.name -eq $VorlageSerie }))) { $vor += "Serie: Typ «$VorlageSerie» fehlt" }
  foreach ($pn in 'breite', 'hoehe') { $p = Param $serie.definition $pn; if (-not $p -or $p.art -ne 'typ' -or $p.formel) { $vor += "Serie: $pn ist kein Typparameter ohne Formel" } }
  if ($sl.formteile.bogen.familie -ne $IdBogen -or $sl.formteile.tstueck.familie -ne $IdT -or $sl.formteile.reduktion.familie -ne $IdRed) { $vor += 'Serie: leitung.formteile nennt andere Formteil-Familien' }
}
if (-not (Umgestellt $bogen)) {
  foreach ($pn in 'breite', 'hoehe', 'ri', 'lv', 'winkel', 'L') { if (-not (Param $bogen.definition $pn)) { $vor += "Bogen: Parameter $pn fehlt" } }
  if (@($bogen.verwendungen).Count -gt 0) { $vor += "$($bogen.kennzeichen) ist platziert ($(@($bogen.verwendungen).Count)×) – die Rastertypen würden fehlen; zuerst klären" }
}
if (-not (Umgestellt $tst)) {
  foreach ($pn in 'breite', 'hoehe', 'ri', 'lv', 'L', 'Lc') { if (-not (Param $tst.definition $pn)) { $vor += "T-Stück: Parameter $pn fehlt" } }
  foreach ($an in 'a', 'b', 'c') { if (-not (Anschluss $tst.definition $an)) { $vor += "T-Stück: Anschluss $an fehlt" } }
  foreach ($k in 'huelle', 'abzweig') { if (-not (Koerper $tst.definition $k)) { $vor += "T-Stück: Körper $k fehlt" } }
  if (@($tst.verwendungen).Count -gt 0) { $vor += "$($tst.kennzeichen) ist platziert ($(@($tst.verwendungen).Count)×) – die Rastertypen würden fehlen; zuerst klären" }
}
if (-not (Umgestellt $red)) {
  foreach ($pn in 'breite1', 'hoehe1', 'breite2', 'hoehe2') { $p = Param $red.definition $pn; if (-not $p -or $p.art -ne 'exemplar' -or $p.formel) { $vor += "Reduktion: $pn ist kein Exemplarparameter ohne Formel" } }
  if (-not (@($red.definition.typen | Where-Object { $_.name -eq $VorlageRed }))) { $vor += "Reduktion: Typ «$VorlageRed» fehlt" }
}
if ($vor.Count -gt 0) { Aus 'Voraussetzungen nicht erfüllt:'; foreach ($v in $vor) { Aus "  - $v" }; exit 2 }

# ------------------------------------------------------------------ Umschlüsselung prüfen (über die Abfrage befunde, Leser des Servers)
function BefundePruefen([hashtable]$soll) {
  # $soll: neue Id → @(Seite, Art, Wert[, Box]); Ergebnis: Liste der Abweichungen
  $abw = @()
  $unterlagen = @{}
  foreach ($f in $alle) { foreach ($x in @($f.definition.unterlagen)) { if ($x.pruefsumme) { $unterlagen[([string]$x.pruefsumme).Substring(0, 12)] = $x.datei } } }
  foreach ($gruppe in ($soll.Keys | Group-Object { $_.Substring(0, 12) })) {
    $datei = $unterlagen[$gruppe.Name]
    if (-not $datei) { $abw += "Unterlage $($gruppe.Name) ist in keiner K2-Familie genannt"; continue }
    $r = Abfrage 'befunde' @{ unterlage = $datei; id = (@($gruppe.Group) -join ','); proSeite = 500 }
    $ist = @{}; foreach ($b in @($r.befunde)) { $ist[$b.id] = $b }
    foreach ($id in $gruppe.Group) {
      $s = $soll[$id]; $b = $ist[$id]
      if (-not $b) { $abw += "$id fehlt beim Leser des Servers"; continue }
      $w = if ($b.wert -is [string]) { $b.wert } else { Json $b.wert }
      $ok = $b.seite -eq $s[0] -and $b.art -eq $s[1] -and $w -eq $s[2]
      if ($ok -and $s.Count -gt 3) { $box = ([string]$s[3]).Split(','); for ($i = 0; $i -lt 4; $i++) { if (-not (Etwa $b.box[$i] ([double]::Parse($box[$i], [Globalization.CultureInfo]::InvariantCulture)))) { $ok = $false } } }
      if (-not $ok) { $abw += "$id S. $($b.seite) [$($b.art)] «$w» – erwartet S. $($s[0]) [$($s[1])] «$($s[2])»" }
    }
  }
  , $abw
}
if ($offen.Count -gt 0) {
  $soll = @{}
  foreach ($k in $Umschl.Keys) { $e = $Umschl[$k]; $soll[$e[0]] = @($e[1], $e[2], $e[3], $e[4]) }
  foreach ($k in $NeuQuellen.Keys) { $soll[$k] = $NeuQuellen[$k] }
  $abw = BefundePruefen $soll
  Pruef ($abw.Count -eq 0) "Umschlüsselung Leser 1 → Leser 2: $($Umschl.Count) Ids und $($NeuQuellen.Count) neue Quellen beim Leser des Servers geprüft (Seite, Art, Text, Box)$(if ($abw.Count) { ' – Abweichungen: ' + ($abw -join '; ') })"
  if ($abw.Count -gt 0) { Aus 'Abbruch: die Umschlüsselung stimmt für diesen Server nicht – nichts geändert.'; exit 2 }
}

# ------------------------------------------------------------------ neue Definitionen aufbauen
function Basis($f) { $d = Kopie $f.definition; Ohne $d 'abdeckung'; Umschluesseln $d; $d }
$neu = @{}

# Bogen
if (-not (Umgestellt $bogen)) {
  $d = Basis $bogen
  ListeSetzen $d 'parameter' 'name' (ExemplarParameter (Param $d 'breite') 'breite' 400 'a: Kanalseite in der Bogenebene (a1 = a2 beim Normbogen); Exemplarwert – freie Grösse der Serie, die Formteilwahl setzt ihn (Vorgabe 400, Normkanal-Liste S. 19)' @($QListe))
  ListeSetzen $d 'parameter' 'name' (ExemplarParameter (Param $d 'hoehe') 'hoehe' 200 'b: Kanalseite rechtwinklig zur Bogenebene; Exemplarwert – freie Grösse der Serie, die Formteilwahl setzt ihn (Vorgabe 200, Normkanal-Liste S. 19)' @($QListe))
  Setze $d 'typen' ([object[]]@([ordered]@{ name = $VorlageBogen; werte = @{} }))
  Setze $d 'beschreibung' (Ersetzen $d.beschreibung 'Ein Typ je Normkanal-Grösse a × b (Breite × Höhe).' "Ein Vorlage-Typ «$VorlageBogen»; Breite a und Höhe b sind Exemplarparameter (freie Grösse b × h der Serie, Vorgabe 400 × 200) – die Formteilwahl der Serie setzt sie, die Einbaulänge rechnet die Formel.")
  Setze $d 'bericht' (Nachtrag $d.bericht "$Marke; entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026): breite und hoehe sind Exemplarparameter ohne Formel (Vorgabe 400 × 200, Quelle Normkanal-Liste S. 19 $QListe; die Bezeichnung «a»/«b» bleibt im Massbezug), die Formteilwahl der Serie «Rechteckkanal» setzt sie auf die Kanalgrösse. Die 400 Typen je Normkanal-Grösse sind durch den Vorlage-Typ «$VorlageBogen» ersetzt (ri 115 und lv 35 bleiben Typparameter mit ihren Quellen, Formel L unverändert); die Normkanal-Liste mit ihren Quellen steht in der Serie. Handrechnung 430 × 270, 90°: L = (115 + 215) · tan 45° + 35 = 365 mm. $Umschluessel")
  $neu[$IdBogen] = $d
}

# T-Stück
if (-not (Umgestellt $tst)) {
  $d = Basis $tst
  $pB = ExemplarParameter (Param $d 'breite') 'breite' 400 'a (a1 = a2): Kanalseite des Durchgangs in der Ebene des Abzweigs; Exemplarwert – die Formteilwahl der Serie setzt ihn (Vorgabe 400, Normkanal-Liste S. 19)' @($QListe)
  $pH = ExemplarParameter (Param $d 'hoehe') 'hoehe' 200 'b: Kanalseite des Durchgangs rechtwinklig zur Abzweigebene; Exemplarwert – die Formteilwahl der Serie setzt ihn (Vorgabe 200, Normkanal-Liste S. 19)' @($QListe)
  $pBc = ExemplarParameter (Param $d 'breite') 'breite_c' 400 'a3: Breite des Abzweigs c in der Ebene des Abzweigs; Exemplarwert – die Formteilwahl der Serie setzt die Abzweiggrösse (Vorgabe 400, Normkanal-Liste S. 19)' @($QListe)
  $pHc = ExemplarParameter (Param $d 'hoehe') 'hoehe_c' 200 'Höhe des Abzweigs c; Exemplarwert – die Formteilwahl der Serie setzt die Abzweiggrösse (Vorgabe 200, Normkanal-Liste S. 19)' @($QListe)
  $pL = Kopie (Param $d 'L'); Setze $pL 'art' 'exemplar'; Setze $pL 'formel' 'ri + lv + breite_c / 2'
  Setze $pL 'beschreibung' 'Einbaulänge Durchgang: Achse Abzweig bis Rahmen V1 bzw. V2 = l1 + a3/2 (a3 = Abzweigbreite breite_c)'
  $pLc = Kopie (Param $d 'Lc'); Setze $pLc 'art' 'exemplar'; Setze $pLc 'formel' 'ri + lv + breite / 2'
  Setze $pLc 'beschreibung' 'Einbaulänge Abzweig: Achse Durchgang bis Rahmen V3 = l2 + a1/2 (a1 = Durchgangsbreite breite)'
  ListeSetzen $d 'parameter' 'name' $pB
  ListeSetzen $d 'parameter' 'name' $pH
  ListeSetzen $d 'parameter' 'name' $pBc 'hoehe'
  ListeSetzen $d 'parameter' 'name' $pHc 'breite_c'
  ListeSetzen $d 'parameter' 'name' $pL
  ListeSetzen $d 'parameter' 'name' $pLc
  $aC = Kopie (Anschluss $d 'c'); Setze $aC 'b' 'breite_c'; Setze $aC 'h' 'hoehe_c'
  ListeSetzen $d 'anschluesse' 'name' $aC
  $kH = Kopie (Koerper $d 'huelle'); Setze $kH 'von' ([object[]]@('-L', '-breite / 2', '-max(hoehe, hoehe_c) / 2')); Setze $kH 'bis' ([object[]]@('L', 'Lc', 'max(hoehe, hoehe_c) / 2'))
  $kA = Kopie (Koerper $d 'abzweig'); Setze $kA 'von' ([object[]]@('-breite_c / 2', 0, '-hoehe_c / 2')); Setze $kA 'bis' ([object[]]@('breite_c / 2', 'Lc', 'hoehe_c / 2'))
  ListeSetzen $d 'koerper' 'id' $kH
  ListeSetzen $d 'koerper' 'id' $kA
  Setze $d 'typen' ([object[]]@([ordered]@{ name = $VorlageT; werte = @{} }))
  $be = Ersetzen $d.beschreibung 'Ein Typ je Normkanal-Grösse a × b, Abzweig gleich gross (a1 = a2 = a3, b durchgehend).' "Ein Vorlage-Typ «$VorlageT»; Durchgang a × b (breite, hoehe) und Abzweig a3 × b3 (breite_c, hoehe_c) sind Exemplarparameter (freie Grösse der Serie, Vorgabe 400 × 200) – die Formteilwahl der Serie setzt sie."
  $be = Ersetzen $be 'Reduzierte Abzweige (a3 < a1) und versetzte Ausgänge (y) sind möglich, aber nicht als Typen angelegt.' 'Reduzierte Abzweige (a3 < a1) über die Exemplarwerte; versetzte Ausgänge (y) nicht modelliert.'
  Setze $d 'beschreibung' $be
  Setze $d 'bericht' (Nachtrag $d.bericht "$Marke; entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026): Durchgang breite/hoehe und Abzweig breite_c/hoehe_c sind Exemplarparameter ohne Formel (Vorgabe 400 × 200, Quelle Normkanal-Liste S. 19 $QListe), die Formteilwahl der Serie setzt sie (a = b = Durchgang, c = Abzweig). Einbaulängen nach der Beschreibung oben, jetzt Exemplarparameter: L = ri + lv + breite_c / 2 (l1 + a3/2), Lc = ri + lv + breite / 2 (l2 + a1/2); bei gleich grossem Abzweig wie bisher 150 + a/2. Anschluss c und Körper auf den Abzweig umgestellt, die 400 Typen durch den Vorlage-Typ «$VorlageT» ersetzt. ANNAHME: die Abzweighöhe hoehe_c ist frei (bisher «b durchgehend»; ob Schmidlin T-Stücke mit anderer Abzweighöhe fertigt, ist nicht belegt). Handrechnung Durchgang 430 × 270, Abzweig 250 × 200: L = 150 + 125 = 275 mm, Lc = 150 + 215 = 365 mm. $Umschluessel")
  $neu[$IdT] = $d
}

# Reduktion
if (-not (Umgestellt $red)) {
  $d = Basis $red
  Setze $d 'beschreibung' (Ersetzen $d.beschreibung 'Je Grössenpaar ein Typ ist NICHT angelegt (Entscheid offen).' 'Typen je Grössenpaar entfallen: die Formteilwahl der Serie setzt die Anschlussgrössen als Exemplarwerte (freie Grösse, Entscheid Christian 20.10.2026).')
  Setze $d 'bericht' (Nachtrag $d.bericht "$Marke; entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026): unverändert ein Typ «$VorlageRed» (Vorlage der Serie, leitung.formteile.reduktion.vorlage); die Formteilwahl der Serie setzt breite1/hoehe1 (gross) und breite2/hoehe2 (klein) als Exemplarwerte. Typen je Grössenpaar entfallen. Einbaulänge l = 1000 mm (Normlänge) für jede Grössenkombination. $Umschluessel")
  $neu[$IdRed] = $d
}

# Serie
if (-not (Umgestellt $serie)) {
  $d = Basis $serie
  $l = $d.leitung
  Setze $l.formteile.bogen 'vorlage' $VorlageBogen
  Setze $l.formteile.tstueck 'vorlage' $VorlageT
  Setze $l.formteile.reduktion 'vorlage' $VorlageRed
  Setze $l 'frei' ([ordered]@{
      vorlage = $VorlageSerie
      grenzen = [ordered]@{ b = [ordered]@{ min = 145; max = 2500 }; h = [ordered]@{ min = 145; max = 2500 } }
      schritt = 1
      quellen = [object[]]@($QMin, $QListe)
    })
  Setze $d 'beschreibung' (Ersetzen $d.beschreibung 'Kanäle werden auch nach Mass gefertigt (Kanal 220 KK, gleiche Grössentabelle S. 18).' "Kanäle werden auch nach Mass gefertigt (Kanal 220 KK, gleiche Grössentabelle S. 18): freie Grösse b × h 145 … 2500 mm, Schritt 1 mm, aus der Vorlage «$VorlageSerie»; die Normkanal-Grössen sind das Vorschlagsraster.")
  Setze $d 'bericht' (Nachtrag $d.bericht "$Marke; entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026: «Rechteckkanäle mit freier Grösse b × h (Normraster nur als Vorschlag); Formteile rechnen ihre Masse aus der Formel»): leitung.frei mit Vorlage «$VorlageSerie», Grenzen b und h je 145 … 2500 mm, Schritt 1 mm; die 400 Typen bleiben als Vorschlagsraster. Untergrenze 145 mm = «minimale Seitenhöhe: 145 mm» (Preisbuch Kanäle S. 19, $QMin). ANNAHMEN zur Bestätigung: Obergrenze 2500 mm = grösste Grösse der Normkanal-Liste S. 19 ($QListe), Schritt 1 mm. Nicht abgebildet: «minimale Abwicklung: 600 mm» (S. 19, $QAbwicklung) – frei kennt nur Grenzen je Seite. Formteile auf Vorlage-Typen mit Exemplar-Grössen umgestellt (leitung.formteile.<art>.vorlage): Bogen «$VorlageBogen», T-Stück «$VorlageT» (Abzweig mit eigener Grösse), Reduktion «$VorlageRed». Damit erledigt: OFFEN «nach Mass», «Reduktion je Grössenpaar», «reduzierte T-Stücke». $Umschluessel")
  $neu[$IdSerie] = $d
}

if ($script:unbekannt.Count -gt 0) {
  Aus "Abbruch: Quellen ohne Eintrag in der Umschlüsselung: $(@($script:unbekannt.Keys) -join ', ') – nichts geändert."
  exit 2
}

# ------------------------------------------------------------------ Trockenlauf, dann echt (Formteile zuerst, Serie zuletzt)
$reihe = @(@($bogen, $tst, $red, $serie) | Where-Object { $neu.ContainsKey($_.id) })
function Speichern([bool]$trocken) {
  $alleOk = $true
  foreach ($f in $reihe) {
    $d = $neu[$f.id]
    Aus ("  familie_entwurf {0} «{1}»: {2} Parameter, {3} Typ(en){4}" -f $f.kennzeichen, $f.name, @($d.parameter).Count, @($d.typen).Count, $(if ($trocken) { ' – Trockenlauf' } else { '' }))
    $e = Befehl 'familie_entwurf' @{ familie = $f.id; definition = $d } $trocken
    ErgebnisZeigen $e
    if (-not $e.angenommen) { $alleOk = $false }
  }
  $alleOk
}

Aus ''
if ($reihe.Count -eq 0) { Aus 'Umstellung: alle vier Familien sind schon umgestellt – nichts zu ändern.' }
else {
  Aus 'Trockenlauf der Umstellung:'
  $trockenOk = Speichern $true
  Pruef $trockenOk "Trockenlauf: $($reihe.Count) familie_entwurf angenommen"
  if ($Ausfuehren) {
    if (-not $trockenOk) { Aus 'Abbruch: der Trockenlauf ist nicht grün – nichts geändert.'; exit 1 }
    Aus ''
    Aus 'Umstellung (echt):'
    $echtOk = Speichern $false
    Pruef $echtOk "familie_entwurf: $($reihe.Count) angenommen"
    if (-not $echtOk) { Aus 'Abbruch nach Fehler – Stand prüfen (Abfrage aenderungen).'; exit 1 }
  }
}

# ------------------------------------------------------------------ Prüfungen
$serie = Lesen $IdSerie; $bogen = Lesen $IdBogen; $tst = Lesen $IdT; $red = Lesen $IdRed
$fertig = @(@($serie, $bogen, $tst, $red) | Where-Object { Umgestellt $_ }).Count -eq 4
if (-not $fertig) {
  Aus ''
  Aus 'Prüfungen und Probe erst auf dem umgestellten Stand (mit -Ausfuehren).'
  Aus "Ergebnis: $(if ($script:fehlerZahl -eq 0) { 'Trockenlauf grün' } else { "$($script:fehlerZahl) Fehler" })"
  if ($script:fehlerZahl -eq 0) { exit 0 } else { exit 1 }
}

Aus ''
Aus 'Prüfung 1 – Familien:'
foreach ($f in @($serie, $bogen, $tst, $red)) {
  $tn = (@($f.definition.typen) | Select-Object -First 3 | ForEach-Object { $_.name }) -join ', '
  Pruef ($f.gueltig -and @($f.definitionsfehler).Count -eq 0) ("{0} «{1}» v{2}: gültig, {3} Definitionsfehler, {4} Typen ({5}{6}), Nachtrag im Bericht" -f $f.kennzeichen, $f.name, $f.version, @($f.definitionsfehler).Count, @($f.definition.typen).Count, $tn, $(if (@($f.definition.typen).Count -gt 3) { ' …' } else { '' }))
}
Pruef (@($serie.definition.typen).Count -eq 400) "Serie: die 400 Rastertypen bleiben als Vorschlag ($(@($serie.definition.typen).Count))"
$tb = @($bogen.typen) | Select-Object -First 1
Pruef ($tb.gueltig -and (Etwa $tb.werte.L 350)) "Bogen Vorlage «$($tb.name)» mit Vorgabe 400 × 200, 90°: L = $(Z $tb.werte.L) mm (Handrechnung (115 + 200)·tan 45° + 35 = 350)"
$tt = @($tst.typen) | Select-Object -First 1
Pruef ($tt.gueltig -and (Etwa $tt.werte.L 350) -and (Etwa $tt.werte.Lc 350)) "T-Stück Vorlage «$($tt.name)» mit Vorgabe 400 × 200: L = $(Z $tt.werte.L), Lc = $(Z $tt.werte.Lc) mm (150 + 400/2 = 350)"
$tr = @($red.typen) | Select-Object -First 1
Pruef ($tr.gueltig -and (Etwa $tr.werte.L 1000)) "Reduktion Vorlage «$($tr.name)»: L = $(Z $tr.werte.L) mm"
$fr0 = $serie.definition.leitung.frei
Pruef ((Json $fr0.quellen) -eq (Json @($QMin, $QListe))) "Serie leitung.frei.quellen: $(Json $fr0.quellen)"

Aus ''
Aus 'Prüfung 2 – Abfrage leitungsserie (kann eine Minute dauern):'
$sw = [Diagnostics.Stopwatch]::StartNew()
$ls = Abfrage 'leitungsserie' @{ familie = $IdSerie } 1800
Aus "  ($([Math]::Round($sw.Elapsed.TotalSeconds)) s)"
$fr = $ls.frei
Pruef ($null -ne $fr -and $fr.vorlage -eq $VorlageSerie -and $fr.grenzen.b.min -eq 145 -and $fr.grenzen.b.max -eq 2500 -and $fr.grenzen.h.min -eq 145 -and $fr.grenzen.h.max -eq 2500 -and $fr.schritt -eq 1) "frei: Vorlage «$($fr.vorlage)», $($fr.text)"
$ft = $ls.formteilFamilien
Pruef ($ft.bogen.vorlage -eq $VorlageBogen -and $ft.tstueck.vorlage -eq $VorlageT -and $ft.reduktion.vorlage -eq $VorlageRed -and $ft.bogen.imProjekt -and $ft.tstueck.imProjekt -and $ft.reduktion.imProjekt) "Formteil-Familien: Bogen $($ft.bogen.imProjekt) «$($ft.bogen.vorlage)», T-Stück $($ft.tstueck.imProjekt) «$($ft.tstueck.vorlage)», Reduktion $($ft.reduktion.imProjekt) «$($ft.reduktion.vorlage)»"
$pr = $ls.probe
function FtText($x) { if ($x.verfuegbar) { "$($x.familie) «$($x.typ)»$(if ($x.frei) { ' frei ' + (Json $x.parameter) })" } else { "nicht verfügbar: $($x.grund)" } }
Pruef ($pr.gueltig -and $pr.frei -and $pr.typ -eq $VorlageSerie) "Probe $($pr.text): gültig, frei, Typ «$($pr.typ)», aussen $(Z $pr.aussen) mm"
Pruef ($pr.formteile.bogen.verfuegbar) "Probe Bogen: $(FtText $pr.formteile.bogen)"
Pruef ($pr.formteile.tstueck.verfuegbar) "Probe T-Stück: $(FtText $pr.formteile.tstueck)"
$prr = @($pr.formteile.reduktion) | Select-Object -First 1
Pruef ($null -ne $prr -and $prr.verfuegbar) "Probe Reduktion auf $($prr.auf): $(FtText $prr)"
$gr = @($ls.groessen)
$gueltig = @($gr | Where-Object { $_.gueltig })
Pruef ($gueltig.Count -eq 400) "Rastergrössen gültig: $($gueltig.Count) von $($gr.Count)"
$r42 = $gr | Where-Object { $_.typ -eq $VorlageSerie } | Select-Object -First 1
Pruef ($r42.gueltig -and $r42.b -eq 400 -and $r42.h -eq 200 -and $r42.formteile.bogen.verfuegbar -and $r42.formteile.tstueck.verfuegbar) "Rastergrösse «$VorlageSerie» weiterhin gültig: Bogen $(FtText $r42.formteile.bogen); T-Stück $(FtText $r42.formteile.tstueck)"
$ohneBogen = @($gueltig | Where-Object { -not $_.formteile.bogen.verfuegbar })
$ohneT = @($gueltig | Where-Object { -not $_.formteile.tstueck.verfuegbar })
Pruef ($ohneBogen.Count -eq 0) "jede Rastergrösse findet ihren Bogen (ohne: $($ohneBogen.Count)$(if ($ohneBogen.Count) { ', z. B. ' + $ohneBogen[0].typ + ': ' + $ohneBogen[0].formteile.bogen.grund }))"
Pruef ($ohneT.Count -eq 0) "jede Rastergrösse findet ihr T-Stück (ohne: $($ohneT.Count)$(if ($ohneT.Count) { ', z. B. ' + $ohneT[0].typ + ': ' + $ohneT[0].formteile.tstueck.grund }))"
$redAlle = 0; $redOk = 0; $redNein = $null
foreach ($g in $gueltig) { foreach ($x in @($g.formteile.reduktion)) { $redAlle++; if ($x.verfuegbar) { $redOk++ } elseif (-not $redNein) { $redNein = "$($g.typ) → $($x.auf): $($x.grund)" } } }
Pruef ($redOk -eq $redAlle -and $redAlle -gt 0) "Reduktionen im Raster verfügbar: $redOk von $redAlle$(if ($redNein) { " (z. B. $redNein)" })"

Aus ''
Aus 'Prüfung 3 – Prüfbefunde des Modells (pruefen):'
$pb = Abfrage 'pruefen' @{}
Pruef ($pb.anzahl -eq 0) "Befunde: $($pb.anzahl) $(Json $pb.jeCode)"

# ------------------------------------------------------------------ Probe (zeichnen, prüfen, zurücknehmen)
if ($Probe) {
  Aus ''
  Aus 'Probe – Kanal 430 × 270 mit Bogen 90°, Reduktion auf 400 × 200, Abzweig 250 × 200 (System K2P-ZUL, Kennzeichen K2P-…):'
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
  function Gleich($p, $soll) { $a = @($p); if ($a.Count -lt 2) { return $false }; for ($i = 0; $i -lt [Math]::Min($a.Count, $soll.Count); $i++) { if (-not (Etwa $a[$i] $soll[$i])) { return $false } }; $true }
  function Ex([string]$kz) { (Element $kz).eigenschaften.parameter.wert }
  function Typ([string]$kz) { (Element $kz).eigenschaften.familientyp.wert }
  $z = 2500
  try {
    $e = Echt 'element_anlegen' @{ klasse = 'System'; kennzeichen = 'K2P-ZUL'; eigenschaften = @{ systemtyp = 'Zuluft' } }
    Pruef $e.angenommen "System K2P-ZUL: $($e.meldung) $($e.fehler)"

    $zeichnen = [ordered]@{ serie = $IdSerie; groesse = [ordered]@{ b = 430; h = 270 }; punkte = @(@(0, 0, $z), @(3000, 0, $z), @(3000, 3000, $z)); system = 'K2P-ZUL'; praefix = 'K2P' }
    $v = Befehl 'leitung_zeichnen' $zeichnen $true
    foreach ($h in @($v.hinweise)) { Aus "    Vorschau: $h" }
    Pruef ($v.angenommen -and @($v.hinweise | Where-Object { $_ -like 'Bogen *Einbaulängen a 365 mm, b 365 mm*' }).Count -eq 1) 'Trockenlauf leitung_zeichnen 430 × 270: Bogen mit Einbaulängen a 365 mm, b 365 mm'
    $e = Echt 'leitung_zeichnen' $zeichnen
    Pruef $e.angenommen "leitung_zeichnen 430 × 270: $($e.meldung) $($e.fehler)"
    $bo = (Angelegt $e 'Bogen') | Select-Object -First 1
    $k1 = Punkte 'K2P-1'; $k2 = Punkte 'K2P-2'
    Pruef ((Gleich $k1[-1] @(2635, 0, $z)) -and (Gleich $k2[0] @(3000, 365, $z))) "Bogen $bo 90°: K2P-1 endet bei $(PText $k1[-1]), K2P-2 beginnt bei $(PText $k2[0]) → Einbaulänge 3000 − 2635 = 365 mm (Handrechnung (115 + 215)·tan 45° + 35 = 365)"
    $xb = Ex $bo
    Pruef ((Typ $bo) -eq $VorlageBogen -and $xb.breite -eq 430 -and $xb.hoehe -eq 270 -and $xb.winkel -eq 90) "Bogen $bo Typ «$(Typ $bo)», Exemplarwerte $(Json $xb)"
    $ek1 = Element 'K2P-1'
    Pruef ($ek1.eigenschaften.familientyp.wert -eq $VorlageSerie -and $ek1.eigenschaften.breite.wert -eq 430 -and $ek1.eigenschaften.hoehe.wert -eq 270) "K2P-1: familientyp «$($ek1.eigenschaften.familientyp.wert)», breite $($ek1.eigenschaften.breite.wert), hoehe $($ek1.eigenschaften.hoehe.wert) ($($ek1.eigenschaften.breite.herkunft))"

    $e = Echt 'leitung_groesse' @{ leitungen = @('K2P-2'); groesse = [ordered]@{ b = 400; h = 200 } }
    Pruef $e.angenommen "leitung_groesse K2P-2 → 400 × 200 (Reduktion): $($e.meldung) $($e.fehler)"
    $rd = (Angelegt $e 'Reduktion') | Select-Object -First 1
    $k2 = Punkte 'K2P-2'; $xr = Ex $rd
    Pruef ($rd -and (Typ $rd) -eq $VorlageRed -and $xr.breite1 -eq 430 -and $xr.hoehe1 -eq 270 -and $xr.breite2 -eq 400 -and $xr.hoehe2 -eq 200) "Reduktion $rd Typ «$(Typ $rd)», Exemplarwerte $(Json $xr)"
    Pruef (Gleich $k2[0] @(3000, 1365, $z)) "K2P-2 beginnt nach der Reduktion bei $(PText $k2[0]) (365 + l 1000 = 1365)"
    $ek2 = Element 'K2P-2'
    Pruef ($ek2.eigenschaften.familientyp.wert -eq $VorlageSerie -and $ek2.eigenschaften.breite.wert -eq 400 -and $ek2.eigenschaften.hoehe.wert -eq 200 -and $ek2.eigenschaften.breite.herkunft -notlike '*frei*') "K2P-2 Rastergrösse: familientyp «$($ek2.eigenschaften.familientyp.wert)», 400 × 200 ($($ek2.eigenschaften.breite.herkunft))"

    $e = Echt 'leitung_abzweigen' ([ordered]@{ leitung = 'K2P-1'; punkt = @(1500, 0, $z); richtung = @(0, -1, 0); laenge = 1500; groesse = [ordered]@{ b = 250; h = 200 }; praefix = 'K2P-A' })
    Pruef $e.angenommen "leitung_abzweigen K2P-1 bei x = 1500, Abzweig 250 × 200: $($e.meldung) $($e.fehler)"
    foreach ($h in @($e.hinweise | Where-Object { $_ -like '*T-St*' })) { Aus "    Hinweis: $h" }
    $ts = (Angelegt $e 'TStueck') | Select-Object -First 1
    $kab = @(Angelegt $e 'Kanal' | Where-Object { $_ -ne 'K2P-1-2' }) | Select-Object -First 1
    $xt = Ex $ts
    Pruef ($ts -and (Typ $ts) -eq $VorlageT -and $xt.breite -eq 430 -and $xt.hoehe -eq 270 -and $xt.breite_c -eq 250 -and $xt.hoehe_c -eq 200) "T-Stück $ts Typ «$(Typ $ts)», Exemplarwerte $(Json $xt)"
    $k1 = Punkte 'K2P-1'; $k12 = Punkte 'K2P-1-2'; $ka = Punkte $kab
    Pruef ((Gleich $k1[-1] @(1225, 0, $z)) -and (Gleich $k12[0] @(1775, 0, $z))) "Durchgang: K2P-1 endet bei $(PText $k1[-1]), K2P-1-2 beginnt bei $(PText $k12[0]) → L = 275 mm (150 + 250/2)"
    Pruef ((Gleich $ka[0] @(1500, -365, $z)) -and (Gleich $ka[-1] @(1500, -1500, $z))) "Abzweig $kab von $(PText $ka[0]) bis $(PText $ka[-1]) → Lc = 365 mm (150 + 430/2)"
    $eab = Element $kab
    Pruef ($eab.eigenschaften.breite.wert -eq 250 -and $eab.eigenschaften.hoehe.wert -eq 200) "Abzweig ${kab}: $($eab.eigenschaften.breite.wert) × $($eab.eigenschaften.hoehe.wert), familientyp «$($eab.eigenschaften.familientyp.wert)»"

    # Leitungsprüfungen wie die Kern-Tests (RoutingHilfe.Sauber: P13, P14, P15, P18) und P29 ohne Befund; P01 (offene Enden der
    # Probe) und P11 (das Arbeitsprojekt hat kein Gebäude/Geschoss) gehören zur Probe, nicht zu K2 – nur angezeigt
    $pb = Abfrage 'pruefen' @{}
    $leit = @(@($pb.befunde) | Where-Object { $_.code -in @('P13', 'P14', 'P15', 'P18', 'P29') })
    Pruef ($leit.Count -eq 0 -and -not $pb.gekuerzt) "pruefen nach der Probe: Leitungsprüfungen P13/P14/P15/P18 und P29 ohne Befund ($($leit.Count)); übrige Befunde der Probe: $(Json $pb.jeCode)"
    foreach ($b in $leit) { Aus "    $($b.code) $($b.schwere) $($b.element): $($b.text)" }
    $fremd = @(@($pb.befunde) | Where-Object { $_.element -notlike 'K2P-*' -and $_.element -notlike 'BO-*' -and $_.element -notlike 'RD-*' -and $_.element -notlike 'TS-*' -and $_.element -notlike 'KA-*' })
    Pruef ($fremd.Count -eq 0) "Befunde nur an Elementen der Probe (andere: $($fremd.Count)$(if ($fremd.Count) { ', z. B. ' + $fremd[0].code + ' ' + $fremd[0].element }))"

    $v = Befehl 'leitung_zeichnen' ([ordered]@{ serie = $IdSerie; groesse = [ordered]@{ b = 400; h = 200 }; punkte = @(@(0, 5000, $z), @(3000, 5000, $z), @(3000, 8000, $z)); system = 'K2P-ZUL'; praefix = 'K2P-R' }) $true
    foreach ($h in @($v.hinweise)) { Aus "    Vorschau: $h" }
    Pruef ($v.angenommen -and @($v.hinweise | Where-Object { $_ -like 'Gerade *frei*' }).Count -eq 0) 'Trockenlauf leitung_zeichnen Rastergrösse 400 × 200: angenommen, Geraden ohne «frei»'

    $v = Befehl 'leitung_zeichnen' ([ordered]@{ serie = $IdSerie; groesse = [ordered]@{ b = 100; h = 100 }; punkte = @(@(0, 10000, $z), @(3000, 10000, $z)); system = 'K2P-ZUL'; praefix = 'K2P-K' }) $true
    Pruef (-not $v.angenommen -and $v.fehler -like '*ausserhalb der Grenzen*') "leitung_zeichnen 100 × 100 abgelehnt: $($v.fehler) – $($v.vorschlag)"
    $e = Echt 'element_anlegen' ([ordered]@{ klasse = 'Kanal'; kennzeichen = 'K2P-KLEIN'; system = 'K2P-ZUL'
        eigenschaften = [ordered]@{ familientyp = $VorlageSerie; breite = 100; hoehe = 100 }; bezuege = @{ familie = $serie.kennzeichen }
        geometrie = [ordered]@{ art = 'polylinie'; punkte = @(@(0, 12000, $z), @(2000, 12000, $z)) } })
    Pruef $e.angenommen "element_anlegen Kanal K2P-KLEIN 100 × 100 (familientyp «$VorlageSerie»): $($e.meldung) $($e.fehler)"
    $p29 = @((Abfrage 'pruefen' @{}).befunde | Where-Object { $_.code -eq 'P29' -and $_.element -eq 'K2P-KLEIN' })
    Pruef ($p29.Count -eq 1 -and $p29[0].schwere -eq 'fehler') "P29 an K2P-KLEIN: $($p29[0].schwere) – $($p29[0].text) – Vorschlag: $($p29[0].vorschlag)"
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
if ($script:fehlerZahl -eq 0) { Aus 'Ergebnis: alles grün.'; exit 0 }
Aus "Ergebnis: $($script:fehlerZahl) Prüfung(en) nicht grün."
exit 1

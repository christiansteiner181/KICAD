# Sanitärapparate als Geberit-Duofix-Familien (Phase 1 Rang 5, 30.10.2026; Christian: «sanitärobjekte müssen mit Geberit Duofix oder GIS
# nachmodeliert werden»): startet die KI-Aufträge «Familie aus Unterlagen» nacheinander auf dem Server der Grundausstattung (Port 5153,
# werkzeuge/grundausstattung_server.ps1) und schreibt je Auftrag eine Zeile ins Protokoll. Alle Familien bleiben Entwurf.
# Aufruf: powershell -File werkzeuge\duofix_auftraege.ps1 [-Nur WC,WT]
# Datei mit UTF-8-BOM speichern (PowerShell 5.1). Die Hinweise nennen keine Masse: nur aus Herstellerunterlagen, nie aus dem Gedächtnis.
param([string[]]$Nur = @(), [int]$Port = 5153)
$ErrorActionPreference = 'Stop'
$Nur = @($Nur | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$u = "http://localhost:$Port"
$ga = "$env:USERPROFILE\HLKS_CAD_build\grundausstattung"
$log = "$ga\duofix_auftraege.log"
function Log($t) { $z = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t"; Add-Content -Path $log -Value $z -Encoding UTF8; $z }

$gemeinsam = @'
Sanitärapparat als Geberit-Duofix-Vorwandelement für die H+K-Bürobibliothek, Schweiz (Geberit Schweiz bevorzugen: catalog.geberit.ch, geberit.ch;
Produktdatenblätter «Erstellt mit Geberit Produktdaten» mit Massbild). Eine Familie je Apparat; Typen = die Ausführungen, die Geberit Schweiz für diesen
Apparat als Duofix-Element führt (z. B. Bauhöhen), Typname mit Artikelnummer. Eigenschaften: hersteller Geberit, typ = Produktbezeichnung, montage =
«Vorwand Geberit Duofix», sanitaerArt = passender key aus kataloge/sanitaer_belastungswerte.json (Abfrage sanitaer_katalog). Anschlüsse nach der
KICAD-Klasse (sw, kw, ww, soweit die Klasse sie hat und das Element sie bietet) mit Lage und Grösse aus dem Massbild des Datenblatts (Bezug: Element-
Unterkante = Oberkante fertiger Boden, Vorderkante Element), Masse in der Einheit der Unterlage prüfen (Geberit-Tabellen oft cm – im Bericht nennen).
Körper: Hüllquader des Elements aus Breite, Höhe, Tiefe des Massbilds; Keramik bzw. Wanne nur, wenn die Unterlage sie bemasst. Werte nur aus
Herstellerunterlagen; Unterlagen mit unterlage_aus_url ablegen. Händlerseiten nur, wenn sie ein Herstellerdokument unverändert wiedergeben
(Dokumentnummer, Vermerk «nicht vom Hersteller abgerufen»). Gültigen Stand nennen, Widersprüche als Konflikt. Fehlt ein Mass, bleibt es offen und
steht im Bericht – nie schätzen. Achtung: in Geberit-Artikeldatenblättern stehen die Spaltenköpfe (mit Einheit, meist cm) gedreht und werden vom PDF-Leser nicht gelesen; Masse dann aus Produktdatenblättern (gedb_pro) oder catalog.geberit.ch mit Einheit belegen. Keine Preise. Familienname «Geberit Duofix <Apparat>».
'@ -replace "`r?`n", ' '

$auftraege = @(
  @{ nr = 'WC'; kategorie = 'WC'; unterlagen = @('UL-90', 'UL-91'); hinweis = 'Apparat Wand-WC: Geberit Duofix Element für Wand-WC mit Unterputz-Spülkasten (Standard im Wohnbau, z. B. Bauhöhe 112 cm). Anschlüsse sw (Abgangsbogen) und kw (Spülkasten). ' }
  @{ nr = 'WT'; kategorie = 'Waschtisch'; unterlagen = @('UL-90', 'UL-92'); hinweis = 'Apparat Waschtisch bzw. Handwaschbecken: Geberit Duofix Element für Waschtisch (Stand- bzw. Wandarmatur, die Ausführungen als Typen). Anschlüsse sw, kw, ww. ' }
  @{ nr = 'BW'; kategorie = 'Badewanne'; unterlagen = @('UL-90', 'UL-93'); hinweis = 'Apparat Badewanne: Geberit Duofix Element für Badewanne bzw. für Badewannenarmatur (Aufputz/Unterputz). Ablauf sw nur, wenn das Element ihn führt (sonst offen, im Bericht). Anschlüsse kw, ww. ' }
  @{ nr = 'DU'; kategorie = 'Dusche'; unterlagen = @('UL-90', 'UL-93'); hinweis = 'Apparat Dusche mit Duschwanne: Geberit Duofix Element für Duscharmatur (Aufputz/Unterputz). Ablauf sw nur, wenn das Element ihn führt (Wandablauf), sonst offen und im Bericht. Anschlüsse kw, ww. ' }
  @{ nr = 'SP'; kategorie = 'Spueltisch'; unterlagen = @('UL-90'); hinweis = 'Apparat Spültisch (Küche) – NICHT Duofix (Christian 30.10.2026: «küche wird mit jrg wand dosen gelöst»): Spültisch mit den Wasseranschlüssen über JRG-Wanddosen von GF (JRG Sanipex MT, Schweiz, gfps.com/de-ch; Wanddose für Armaturenanschluss, Masse aus dem GF-Datenblatt bzw. Sortiment). Familienname «Spültisch JRG Wanddosen»; montage «Wanddosen JRG Sanipex MT»; Anschlüsse kw, ww an den Wanddosen (Grösse und Lage aus der GF-Unterlage), sw nur, wenn eine Unterlage Lage und Grösse belegt, sonst offen und im Bericht. Der allgemeine Teil unten gilt sinngemäss für GF statt Geberit. '}
)

foreach ($a in $auftraege) {
  if ($Nur.Count -gt 0 -and $Nur -notcontains $a.nr) { continue }
  $hinweis = $a.hinweis + $gemeinsam
  if ($hinweis.Length -gt 4000) { Log "$($a.nr) Hinweis zu lang: $($hinweis.Length)"; continue }
  $koerper = @{ art = 'familie_aus_unterlagen'; unterlagen = $a.unterlagen; kategorie = $a.kategorie; hinweis = $hinweis; internet = $true; zeitlimitMinuten = 90 } | ConvertTo-Json -Depth 4
  $st = $null
  for ($v = 0; $v -lt 180 -and -not $st; $v++) {
    try { $st = Invoke-RestMethod "$u/api/auftraege" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($koerper)) }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 409) { Start-Sleep 60 } else { Log "$($a.nr) Start FEHLER $($_.ErrorDetails.Message)"; break } }
  }
  if (-not $st) { continue }
  Log "$($a.nr) gestartet $($st.id)"
  do { Start-Sleep 30; try { $s = Invoke-RestMethod "$u/api/auftraege/$($st.id)" } catch { $s = $null } } while (-not $s -or $s.status -in @('wartet', 'laeuft'))
  $fam = @($s.ergebnis.familien | ForEach-Object { "$($_.kennzeichen) $($_.name) [$($_.kategorie)] Typen $(@($_.typen).Count) offeneKonflikte $($_.offeneKonflikte)" }) -join '; '
  Log "$($a.nr) $($s.status) $($st.id): $fam"
}
Log 'fertig'

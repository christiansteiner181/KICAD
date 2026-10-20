# Grundausstattung Rohr- und Kanalserien (entwurf/grundausstattung-leitungsserien.md): startet die KI-Aufträge
# «Familie aus Unterlagen» nacheinander (ein KI-Lauf je Projekt) auf dem Server der Grundausstattung (Port 5153,
# werkzeuge/grundausstattung_server.ps1) und schreibt je Auftrag eine Zeile ins Protokoll.
# Aufruf: powershell -File werkzeuge\grundausstattung_auftraege.ps1 [-Nur R2,R5]
# Datei mit UTF-8-BOM speichern (PowerShell 5.1 liest sonst «» und Umlaute falsch – Befund 20.10.2026).
# Die Hinweise nennen keine Masse: Abmessungen nur aus den Unterlagen, nie aus dem Gedächtnis; keine Preise.
param([string[]]$Nur = @(), [int]$Port = 5153)
$ErrorActionPreference = 'Stop'
$Nur = @($Nur | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })   # -File übergibt «R2,R5» als einen Text
$u = "http://localhost:$Port"
$ga = "$env:USERPROFILE\HLKS_CAD_build\grundausstattung"
$log = "$ga\auftraege.log"
New-Item -ItemType Directory -Force $ga | Out-Null
function Log($t) { $z = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t"; Add-Content -Path $log -Value $z -Encoding UTF8; $z }

$gemeinsam = @'
Teil der Grundausstattung der H+K-Bürobibliothek (Liste aller Serien: grundausstattung_serien.csv). Baue es nach dem Abschnitt «Leitungsserien und Formteile» der Anleitung: zuerst die Formteil-Familien Bogen (alle Winkel, die das System führt; ein Typ je Grösse mit den Einbaulängen je Winkel und einer Formel nach winkel), T-Stück (gleich; reduzierte Abzweige nur, wenn die Unterlagen sie bemassen) und Reduktion, danach die Leitungsserie mit Verweis auf diese Formteile und regeln.winkel nach Unterlage. Vollständige Grössenreihe, wie der Hersteller sie heute führt (Typname wie der Hersteller; DN nur, wenn die Unterlage ihn der Grösse zuordnet). Werte nur aus Herstellerunterlagen; die abgelegten Unterlagen sind ein Einstieg – fehlende Masse (Einbaulängen, Aussendurchmesser, Wandstärken) beim Hersteller suchen und mit unterlage_aus_url ablegen. Händlerseiten nur, wenn sie ein Herstellerdokument unverändert wiedergeben (Dokumentnummer nennen, Vermerk «nicht vom Hersteller abgerufen»). Gültigen Stand nennen, Widersprüche als Konflikt. Fehlt ein Mass, bleibt der Formteiltyp offen und steht im Bericht – nie schätzen. Keine Preise übernehmen. Familiennamen: «<Serie>» für die Leitungsserie, «<Serie> Bogen», «<Serie> T-Stück», «<Serie> Reduktion» für die Formteile. In der Beschreibung der Serie Einsatz und Medien nennen. Alle Familien bleiben Entwurf.
'@ -replace "`r?`n", ' '

$serien = '685dab030afb_grundausstattung_serien.csv'
$auftraege = @(
  @{ nr = 'R1'; kategorie = 'Rohr'; unterlagen = @('b23c14c1c864_geberit_mapress_c-stahl_uebersicht_das-1540060.pdf', '78cacfccd36e_geberit_de_mapress_c-stahl.html', $serien)
     hinweis = 'Rohrsystem «Geberit Mapress C-Stahl», Schweiz, für Heizung und Kälte in geschlossenen Kreisläufen (Medien HZ-VL, HZ-RL, KAE-VL, KAE-RL, je auch mit Glykol). Die beiden Geberit-Unterlagen stammen von Geberit Deutschland (Übersicht das-1540060, Produktseite https://www.geberit.de/sanitaer-rohrleitungssysteme/versorgungssysteme/geberit-mapress/geberit-mapress-c-stahl/, abgerufen 19.10.2026) – Geberit Schweiz (catalog.geberit.ch, geberit.ch) bevorzugen und Abweichungen nennen. Führt Geberit Ausführungen mit gleichen Massen (z. B. aussen verzinkt, kunststoffummantelt), eine Serie und die Ausführungen im Bericht; bei verschiedenen Massen je Ausführung eine eigene Serie. ' }
  @{ nr = 'R2'; kategorie = 'Rohr'; unterlagen = @('a1fcd40547d4_geberit_ch_mapress_edelstahl_uebersicht_das-1540062.pdf', $serien)
     hinweis = 'Rohrsystem «Geberit Mapress Edelstahl», Trinkwasser (kalt, warm, Zirkulation), Schweiz. Geberit Schweiz bevorzugen (catalog.geberit.ch). Werkstoff-Kürzel nach der Anleitung (nichtrostender Stahl = CNS), wenn die Unterlage den Werkstoff nennt. ' }
  @{ nr = 'R5'; kategorie = 'Rohr'; unterlagen = @('a196b81a90fc_gf_jrg_sanipex_mt_sortiment_de.pdf', $serien)
     hinweis = 'Rohrsystem «JRG Sanipex MT» (Georg Fischer), Mehrschichtverbundrohr, Trinkwasser, Schweiz. Unterlage: GF-Sortiment JRG Sanipex MT (deutsch). GF Schweiz (gfps.com/de-ch) bevorzugen. Die Formteile, die das System für die gerade Verlegung führt. ' }
  @{ nr = 'R6'; kategorie = 'Rohr'; unterlagen = @('2b4476d76b30_geberit_ch_silent-db20.html', $serien)
     hinweis = 'Rohrsystem «Geberit Silent-db20», schallgedämmtes Abwassersystem, Schmutzwasser und Regenwasser im Gebäude, Schweiz. Unterlage: Produktseite https://www.geberit.ch/sanitaer-rohrleitungssysteme/gebaeudeentwaesserungssysteme/geberit-silent-db20/ (abgerufen 19.10.2026); Masse aus catalog.geberit.ch (Schweiz). Grössen nach Aussendurchmesser. Bögen mit allen Winkeln, die das System führt. T-Stücke als Abzweige mit Abzweigwinkel (Parameter winkel, regeln.abzweigwinkel), die bemassten Abzweigwinkel und reduzierte Abzweige, soweit bemasst. Werkstoff-Kürzel nach der Anleitung (Polyethylen-Schallschutz-Abwasserrohr = PE-S), wenn die Unterlage es belegt. ' }
  @{ nr = 'R7'; kategorie = 'Rohr'; unterlagen = @($serien)
     hinweis = 'Rohrsystem «Geberit PE» (PE-HD-Abwassersystem, geschweisst bzw. mit Elektroschweissmuffen), Abwasser im Gebäude und Grundleitungen, Schweiz. Keine Herstellerunterlage abgelegt: Geberit Schweiz (catalog.geberit.ch, geberit.ch) recherchieren und die Unterlagen mit unterlage_aus_url ablegen. Grössen nach Aussendurchmesser. Bögen mit allen Winkeln, die das System führt; T-Stücke als Abzweige mit Abzweigwinkel (Parameter winkel, regeln.abzweigwinkel) und reduzierte Abzweige, soweit bemasst. ' }
  @{ nr = 'R3'; kategorie = 'Rohr'; unterlagen = @($serien)
     hinweis = 'Rohrsystem «Stahlrohr Gewinderohr» (Gewinderohr schwarz bzw. verzinkt mit Temperguss-Gewindefittings), Heizung und Kälte, Schweiz. Quelle laut Christian Steiner: der Katalog von Debrunner Acifer (d-a.ch, Händler) – recherchieren; die Rohrabmessungen nur aus dem Katalog bzw. dem Datenblatt des Rohrherstellers, das Debrunner nennt, mit Norm und Hersteller im Bericht. Fittings (Bogen bzw. Winkel, T-Stück, Reduktion) aus dem Katalog des Fitting-Herstellers, den Debrunner führt, soweit bemasst. Debrunner ist Händler: Werte aus dem Debrunner-Katalog als solche vermerken. Grösse nach DN bzw. Zoll, wie der Katalog sie zuordnet. ' }
  @{ nr = 'R4'; kategorie = 'Rohr'; unterlagen = @($serien)
     hinweis = 'Rohrsystem «Stahlrohr geschweisst» (Stahlrohr für Schweissverbindung mit Vorschweiss-Formteilen: Bogen, T-Stück, Reduktion), Heizung und Kälte, Schweiz. Quelle laut Christian Steiner: der Katalog von Debrunner Acifer (d-a.ch, Händler) – recherchieren; Rohrabmessungen (Aussendurchmesser, Wandstärke) und Masse der Vorschweiss-Formteile nur aus dem Katalog bzw. dem Datenblatt des Herstellers, das Debrunner nennt, mit Norm und Hersteller im Bericht; Händlerwerte als solche vermerken. Grösse nach DN, wie der Katalog sie zuordnet. Gibt es schon eine Serie «Stahlrohr Gewinderohr» (familien), nichts doppelt anlegen. ' }
  @{ nr = 'K1'; kategorie = 'Kanal'; unterlagen = @('f614a134b685_schmidlin_preisbuch_rohre_07_2026.pdf', $serien)
     hinweis = 'Lüftungsleitung «Wickelfalzrohr rund» (Schmidlin-Rohr, spiralgefalzt), Kanal rund mit Grösse d, Lüftung (AUL, ZUL, ABL, FOL), Schweiz. Unterlage: Schmidlin-Preisbuch Rohre (Stand 07.2026; Seiten etwa 19 Schmidlin-Rohr, 20 Schmidlin-Rohr glatt, 25 Halbschalenbogen, 26 Segmentbogen, 28–29 Reduktionen, 30–33 Abzweigstücke) – PREISE NIE ÜBERNEHMEN, nur Masse und Bezeichnungen. Fehlen im Preisbuch Einbaulängen der Formteile, auf schmidlinag.ch nach technischen Datenblättern suchen; andere Hersteller nur, wenn Schmidlin keine Masse liefert, dann Hersteller im Bericht und in den Quellen deutlich nennen (Christian: «Lüftung Schmidlin, andere Hersteller angeben»). Formteile: Bogen (Halbschalenbogen bzw. Segmentbogen, Winkel wie im Preisbuch), T-Stück (Abzweigstück), Reduktion. Kein Werkstoff-Kürzel bei Kanälen. ' }
  @{ nr = 'K3'; kategorie = 'Kanal'; unterlagen = @('f614a134b685_schmidlin_preisbuch_rohre_07_2026.pdf', $serien)
     hinweis = 'Lüftungsleitung «Flexrohr» (flexibles Lüftungsrohr), Kanal rund mit Grösse d, Lüftung, Schweiz. Unterlage: Schmidlin-Preisbuch Rohre (Stand 07.2026; Seiten etwa 21–24 Flexible Rohre und Zubehör) – PREISE NIE ÜBERNEHMEN. Ein Flexrohr wird gebogen: regeln.winkel «frei» und biegeradius nur, wenn die Unterlage ihn nennt; eigene Bogen-Familie nur, wenn das System Formteile führt. Für T-Stück und Reduktion die Formteile der Serie «Wickelfalzrohr rund» verwenden, falls vorhanden (familien), sonst offen lassen. Mehrere Flexrohr-Arten mit verschiedenen Massen: je Art eine Serie. Andere Hersteller nur mit deutlicher Nennung. ' }
  @{ nr = 'K2'; kategorie = 'Kanal'; unterlagen = @('aae0fb6a93a9_schmidlin_preisbuch_kanaele_07_2026.pdf', $serien)
     hinweis = 'Lüftungsleitung «Rechteckkanal» (Blechkanal), Kanal rechteckig mit Grösse b × h, Lüftung, Schweiz. Unterlage: Schmidlin-Preisbuch Kanäle (Stand 07.2026; Kanal, Normkanal, Bogen, T-Stück, Übergang/Etage/Konus) – PREISE NIE ÜBERNEHMEN. Rechteckkanäle werden meist nach Mass gefertigt: Typen nur für Grössen, die die Unterlage als Liste führt (z. B. Normkanal-Grössen); führt sie keine Liste, lege die Serie mit den bemassten Grössen an und schreibe in den Bericht unter «Offene Felder»: «Rechteckkanal mit freier Grösse b × h – Christian entscheidet». Formteile (Bogen mit Radius, T-Stück, Reduktion bzw. Übergang/Etage) nur mit Massen aus der Unterlage oder aus Schmidlin-Datenblättern (schmidlinag.ch); sonst offen. Andere Hersteller nur mit deutlicher Nennung. ' }
)

foreach ($a in $auftraege) {
  if ($Nur.Count -gt 0 -and $Nur -notcontains $a.nr) { continue }
  $hinweis = $a.hinweis + $gemeinsam
  if ($hinweis.Length -gt 4000) { Log "$($a.nr) Hinweis zu lang: $($hinweis.Length)"; continue }
  $koerper = @{ art = 'familie_aus_unterlagen'; unterlagen = $a.unterlagen; kategorie = $a.kategorie; hinweis = $hinweis; internet = $true; zeitlimitMinuten = 90 } | ConvertTo-Json -Depth 4
  $st = $null
  for ($v = 0; $v -lt 180 -and -not $st; $v++) {   # 409 = anderer KI-Lauf im Projekt: bis 3 h warten
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

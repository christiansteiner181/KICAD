# Herstellerbibliothek (entwurf/herstellerbibliothek.md, Entscheide Christian 02.11.2026): je Hersteller eine erste Baureihe als
# KI-Auftrag «Familie aus Unterlagen» mit Internet, nacheinander auf dem Server werkzeuge\herstellerbibliothek_server.ps1 (Port 5154).
# Aufruf: powershell -File werkzeuge\herstellerbibliothek_auftraege.ps1 [-Nur P1,V1]
# Datei mit UTF-8-BOM speichern (PowerShell 5.1). Die Hinweise nennen keine Werte: alles aus den Herstellerunterlagen.
param([string[]]$Nur = @(), [int]$Port = 5154)
$ErrorActionPreference = 'Stop'
$Nur = @($Nur | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$u = "http://localhost:$Port"
$hb = "$env:USERPROFILE\HLKS_CAD_build\herstellerbibliothek_ki"
$log = "$hb\auftraege.log"
New-Item -ItemType Directory -Force $hb | Out-Null
function Log($t) { $z = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t"; Add-Content -Path $log -Value $z -Encoding UTF8; $z }

$gemeinsam = @'
Teil der Herstellerbibliothek der H+K-Bürobibliothek (Auftrag Christian Steiner 02.11.2026). Arbeite nach den Abschnitten «Regeltechnik und Antriebe» und «Internet-Recherche» der Anleitung. Typen vollständig, wie der Hersteller die Reihe heute in der Schweiz führt (Typname wie der Hersteller, Artikelnummer als Typparameter artikelnummer, wenn die Unterlage sie je Typ nennt). Quellen bevorzugt: Datenblatt bzw. Planungsunterlage des Herstellers (PDF), Geometrie aus einem frei abrufbaren IFC- oder STEP-Modell des Herstellers, sonst aus dem Massbild; Revit-Dateien (RFA) werden nicht gelesen. Regeltechnik vollständig, soweit die Unterlage sie nennt: Speisung, Leistungsaufnahme, Strom, Schutzart, Stellsignale und Rückmeldung, Laufzeit bzw. Stellkraft/Drehmoment, Busprotokolle mit nötigem Modul, Datenpunkte (Meldungen, Befehle, Messwerte) – jeder Wert mit Befund. Keine Kennlinien (später, Modul 5). Keine Preise. Was nur mit Anmeldung erhältlich ist, im Bericht nennen. Alle Familien bleiben Entwurf.
'@ -replace "`r?`n", ' '

$auftraege = @(
  @{ nr = 'P1'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Grundfos MAGNA3» (Heizung, Kälte, Trinkwarmwasser nur, wenn der Hersteller es nennt), alle Baugrössen. Einzel- und Doppelpumpen als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen «Grundfos MAGNA3» bzw. «Grundfos MAGNA3 D». Eigenschaft bauart der Klasse Pumpe: nasslaeufer. Anschlüsse saug und druck mit DN bzw. Gewinde wie in der Unterlage. ' }
  @{ nr = 'P2'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Wilo-Stratos MAXO» (Heizung, Kälte), alle Baugrössen. Einzel- und Doppelpumpen (Stratos MAXO-D) als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen wie der Hersteller. Eigenschaft bauart: nasslaeufer. Anschlüsse saug und druck mit DN bzw. Gewinde wie in der Unterlage. ' }
  @{ nr = 'P3'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Biral ModulA» (Biral AG, Schweiz; biral.ch), alle Baugrössen der aktuellen Reihe. Einzel- und Doppelpumpen als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen wie der Hersteller. Eigenschaft bauart: nasslaeufer. Biral bietet IFC und STEP über ein CAD-Portal an – nur nutzen, wenn ohne Anmeldung abrufbar. ' }
  @{ nr = 'V1'; kategorie = 'Ventil'
     hinweis = 'Siemens (Smart Infrastructure, Acvatix): zuerst die Antriebsfamilie «Siemens SQX» (Kategorie Antrieb, alle SQX-Typen, die Siemens für die Ventile VXG.. und VVG.. nennt, bauart hub), dann die Ventilfamilie «Siemens VXG44» (Gewindeventil, alle Nennweiten und Kvs-Werte) mit antriebe (Kombinationstabelle VXG44 × SQX aus der Siemens-Unterlage, fuer = Ventiltypen) und anbau (Kupplungspunkt aus dem Massbild). Bauart (regelventil bzw. mischventil) und Anzahl Wege aus dem Datenblatt; Medium «*». ' }
  @{ nr = 'V2'; kategorie = 'Ventil'
     hinweis = 'Belimo: zuerst die Antriebsfamilie der Drehantriebe, die Belimo für die Regelkugelhähne R2.. mit Innengewinde (DN 15 bis DN 50) nennt (Kategorie Antrieb, bauart dreh, je Antriebsreihe eine Familie, Typen = Varianten nach Ansteuerung wie Auf/Zu, 3-Punkt, stetig, MP-Bus/Modbus/BACnet, soweit Belimo sie für diese Ventile nennt), dann die Ventilfamilie «Belimo R2..» (2-Weg-Regelkugelhahn mit Innengewinde, alle Typen R2…, bauart regelkugelhahn, wege 2) mit antriebe (Kombination aus der Belimo-Unterlage) und anbau. R3.. (3-Weg) folgt in einem späteren Auftrag. ' }
  @{ nr = 'K1'; kategorie = 'Ventil'
     hinweis = 'EBRO Armaturen: Absperrklappe «EBRO Z 011» (Zwischenflanschklappe), Nennweiten DN 50 bis DN 250, bauart absperrklappe, Medium «*», noch ohne Antrieb (Antriebe folgen mit den EBRO-Portaldaten von Christian). anbau nur, wenn die Unterlage den Antriebsflansch bemasst. Führt EBRO mehrere Ausführungen (z. B. Z 011-A) mit verschiedenen Massen, je Ausführung eine Familie; Ausführungen mit gleichen Massen als Typparameter. Händlerablagen nur nach der Regel der Anleitung (unverändertes Herstellerdokument). ' }
  # IMI (Christian 02.11.2026: «FBH verteiler, FBH Verteilerkasten», «fabrikat IMI», «Expansionsgefässe ebenfalls IMI»)
  @{ nr = 'I1'; kategorie = 'Verteiler'
     hinweis = 'IMI Hydronic Engineering (Heimeier): Fussbodenheizungs-Verteiler «Dynacon Eclipse», alle Abgangszahlen, die IMI führt (Typ je Abgangszahl), Anschlüsse vl_ein und rl_aus nach Klasse Verteiler sowie die Abgänge, soweit bemasst. Zuerst die Antriebsfamilie der thermischen bzw. elektrischen Stellantriebe, die IMI für diesen Verteiler nennt (Kategorie Antrieb, Regeltechnik), und die Familie des passenden Heimeier-Verteilerkastens (Kategorie Verteilerkasten, Unterputz und Aufputz als getrennte Familien, wenn die Masse verschieden sind; Typen = Kastengrössen), dann der Verteiler mit antriebe (ohne anbau) und kaesten (Zuordnung Abgangszahl → Kastengrösse aus der IMI-Unterlage). ' }
  @{ nr = 'I2'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): statische Ausdehnungsgefässe «Statico», alle Grössen, die IMI für Heizung und Kälte führt (Typen nach Nennvolumen; verschiedene Bauformen mit verschiedenen Massen als getrennte Familien). Eigenschaft bauart: statisch. Keine Elektrik. ' }
  @{ nr = 'I3'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): Kompressor-Druckhaltung «Compresso», alle Ausführungen und Gefässgrössen, die IMI führt (Steuereinheit und Gefässe als je eigene Familie, wenn sie getrennt aufgestellt werden; die Kombination im Bericht). Eigenschaft bauart: kompressor. Regeltechnik der Steuereinheit vollständig (Speisung, Leistung, Störmeldungen, Bus wie BACnet/Modbus mit nötigem Modul, Datenpunkte). ' }
  @{ nr = 'I4'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): Pumpen-Druckhaltung «Transfero», alle Ausführungen und Gefässgrössen, die IMI führt (Steuereinheit und Gefässe als je eigene Familie, wenn sie getrennt aufgestellt werden; die Kombination im Bericht). Eigenschaft bauart: pumpe. Regeltechnik der Steuereinheit vollständig (Speisung, Leistung, Störmeldungen, Bus wie BACnet/Modbus mit nötigem Modul, Datenpunkte). ' }
)

foreach ($a in $auftraege) {
  if ($Nur.Count -gt 0 -and $Nur -notcontains $a.nr) { continue }
  $hinweis = $a.hinweis + $gemeinsam
  if ($hinweis.Length -gt 4000) { Log "$($a.nr) Hinweis zu lang: $($hinweis.Length)"; continue }
  $koerper = @{ art = 'familie_aus_unterlagen'; unterlagen = @('herstellerbibliothek_reihen.csv'); kategorie = $a.kategorie; hinweis = $hinweis; internet = $true; zeitlimitMinuten = 90 } | ConvertTo-Json -Depth 4
  $st = $null
  for ($v = 0; $v -lt 180 -and -not $st; $v++) {   # 409 = anderer KI-Lauf im Projekt: bis 3 h warten
    try { $st = Invoke-RestMethod "$u/api/auftraege" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($koerper)) }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 409) { Start-Sleep 60 } else { Log "$($a.nr) Start FEHLER $($_.ErrorDetails.Message)"; break } }
  }
  if (-not $st) { continue }
  Log "$($a.nr) gestartet $($st.id)"
  $weg = 0   # Befund 02.11.2026: ein .NET-Update beendete den Server – nicht endlos weiterfragen
  do { Start-Sleep 30; try { $s = Invoke-RestMethod "$u/api/auftraege/$($st.id)"; $weg = 0 } catch { $s = $null; $weg++ } } while ($weg -lt 20 -and (-not $s -or $s.status -in @('wartet', 'laeuft')))
  if ($weg -ge 20) { Log "$($a.nr) Server seit 10 min nicht erreichbar - Abbruch"; break }
  $fam = @($s.ergebnis.familien | ForEach-Object { "$($_.kennzeichen) $($_.name) [$($_.kategorie)] Typen $(@($_.typen).Count) offeneKonflikte $($_.offeneKonflikte)" }) -join '; '
  Log "$($a.nr) $($s.status) $($st.id): $fam"
}
Log 'fertig'

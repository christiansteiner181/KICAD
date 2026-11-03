# Herstellerbibliothek (entwurf/herstellerbibliothek.md, Entscheide Christian 02.11.2026): je Hersteller eine erste Baureihe als
# KI-Auftrag «Familie aus Unterlagen» mit Internet, nacheinander auf dem Server werkzeuge\herstellerbibliothek_server.ps1 (Port 5154).
# Aufruf: powershell -File werkzeuge\herstellerbibliothek_auftraege.ps1 [-Nur P1,V1]
# Datei mit UTF-8-BOM speichern (PowerShell 5.1). Die Hinweise nennen keine Werte: alles aus den Herstellerunterlagen.
param([string[]]$Nur = @(), [int]$Port = 5154, [string]$Ablage = 'herstellerbibliothek_ki', [int]$Runden = 4,
      [string[]]$Vorhanden = @())   # Vorhanden: «NR=Familie1|Familie2» – schon angelegte Familien eines früheren Laufs (Fortsetzung)
$ErrorActionPreference = 'Stop'
$Nur = @($Nur | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$u = "http://localhost:$Port"
$hb = "$env:USERPROFILE\HLKS_CAD_build\$Ablage"
$log = "$hb\auftraege.log"
New-Item -ItemType Directory -Force $hb | Out-Null
function Log($t) { $z = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $t"; Add-Content -Path $log -Value $z -Encoding UTF8; $z }

$gemeinsam = @'
Teil der Herstellerbibliothek der H+K-Bürobibliothek (Auftrag Christian Steiner 02.11.2026). Arbeite nach den Abschnitten «Regeltechnik und Antriebe» und «Internet-Recherche» der Anleitung. Typen vollständig, wie der Hersteller die Reihe heute in der Schweiz führt (Typname wie der Hersteller, Artikelnummer als Typparameter artikelnummer, wenn die Unterlage sie je Typ nennt). Quellen bevorzugt: Datenblatt bzw. Planungsunterlage des Herstellers (PDF), Geometrie aus einem frei abrufbaren IFC- oder STEP-Modell des Herstellers, sonst aus dem Massbild; Revit-Dateien (RFA) werden nicht gelesen. Detailgetreue Darstellung in grob, mittel und fein (Christian 02.11.2026; fein mit Antrieb, Griff, Gehäuse und Stutzen, Abschnitt Grundsätze 9 der Anleitung); was fehlt, im Bericht als «Darstellung unvollständig». Es muss aussehen wie im Datenblatt und einem IFC-Modell des Herstellers entsprechen: Vergleich je Ansicht und Detailgrad nach Ablauf 8 der Anleitung, Abweichungen als Tabelle im Bericht. Regeltechnik vollständig, soweit die Unterlage sie nennt: Speisung, Leistungsaufnahme, Strom, Schutzart, Stellsignale und Rückmeldung, Laufzeit bzw. Stellkraft/Drehmoment, Busprotokolle mit nötigem Modul, Datenpunkte (Meldungen, Befehle, Messwerte) – jeder Wert mit Befund. Keine Kennlinien (später, Modul 5). Kein Schemasymbol erfinden (Christian 02.11.2026: Schemas aus den Daten werden erst an realen Anlagen im Detail korrigiert). Keine Preise. Was nur mit Anmeldung erhältlich ist, im Bericht nennen. Alle Familien bleiben Entwurf.
'@ -replace "`r?`n", ' '

$auftraege = @(
  @{ nr = 'P1'; hersteller = 'Grundfos'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Grundfos MAGNA3» (Heizung, Kälte, Trinkwarmwasser nur, wenn der Hersteller es nennt), alle Baugrössen. Einzel- und Doppelpumpen als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen «Grundfos MAGNA3» bzw. «Grundfos MAGNA3 D». Eigenschaft bauart der Klasse Pumpe: nasslaeufer. Anschlüsse saug und druck mit DN bzw. Gewinde wie in der Unterlage. ' }
  @{ nr = 'P2'; hersteller = 'Wilo'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Wilo-Stratos MAXO» (Heizung, Kälte), alle Baugrössen. Einzel- und Doppelpumpen (Stratos MAXO-D) als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen wie der Hersteller. Eigenschaft bauart: nasslaeufer. Anschlüsse saug und druck mit DN bzw. Gewinde wie in der Unterlage. ' }
  @{ nr = 'P3'; hersteller = 'Biral'; kategorie = 'Pumpe'
     hinweis = 'Nassläufer-Umwälzpumpe «Biral ModulA» (Biral AG, Schweiz; biral.ch), alle Baugrössen der aktuellen Reihe. Einzel- und Doppelpumpen als getrennte Familien, wenn sich Massbild oder Anschlüsse unterscheiden; Familiennamen wie der Hersteller. Eigenschaft bauart: nasslaeufer. Biral bietet IFC und STEP über ein CAD-Portal an – nur nutzen, wenn ohne Anmeldung abrufbar. ' }
  @{ nr = 'V1'; kategorie = 'Ventil'
     hinweis = 'Siemens (Smart Infrastructure, Acvatix): zuerst die Antriebsfamilie «Siemens SQX» (Kategorie Antrieb, alle SQX-Typen, die Siemens für die Ventile VXG.. und VVG.. nennt, bauart hub), dann die Ventilfamilie «Siemens VXG44» (Gewindeventil, alle Nennweiten und Kvs-Werte) mit antriebe (Kombinationstabelle VXG44 × SQX aus der Siemens-Unterlage, fuer = Ventiltypen) und anbau (Kupplungspunkt aus dem Massbild). Bauart (regelventil bzw. mischventil) und Anzahl Wege aus dem Datenblatt; Medium «*». ' }
  @{ nr = 'V2'; kategorie = 'Ventil'
     hinweis = 'Belimo: zuerst die Antriebsfamilie der Drehantriebe, die Belimo für die Regelkugelhähne R2.. mit Innengewinde (DN 15 bis DN 50) nennt (Kategorie Antrieb, bauart dreh, je Antriebsreihe eine Familie, Typen = Varianten nach Ansteuerung wie Auf/Zu, 3-Punkt, stetig, MP-Bus/Modbus/BACnet, soweit Belimo sie für diese Ventile nennt), dann die Ventilfamilie «Belimo R2..» (2-Weg-Regelkugelhahn mit Innengewinde, alle Typen R2…, bauart regelkugelhahn, wege 2) mit antriebe (Kombination aus der Belimo-Unterlage) und anbau. R3.. (3-Weg) folgt in einem späteren Auftrag. ' }
  @{ nr = 'K1'; kategorie = 'Ventil'
     hinweis = 'EBRO Armaturen: Absperrklappe «EBRO Z 011» (Zwischenflanschklappe), Nennweiten DN 50 bis DN 250, bauart absperrklappe, Medium «*», noch ohne Antrieb (Antriebe folgen mit den EBRO-Portaldaten von Christian). anbau nur, wenn die Unterlage den Antriebsflansch bemasst. Führt EBRO mehrere Ausführungen (z. B. Z 011-A) mit verschiedenen Massen, je Ausführung eine Familie; Ausführungen mit gleichen Massen als Typparameter. Händlerablagen nur nach der Regel der Anleitung (unverändertes Herstellerdokument). ' }
  # Nacharbeit Darstellung (Christian 02.11.2026: detailgetreu in allen Detailgraden, wie im Datenblatt, IFC muss passen): P1 lief vorher
  @{ nr = 'P1b'; kategorie = 'Pumpe'
     hinweis = 'NACHARBEIT der bestehenden Familien «Grundfos MAGNA3» und «Grundfos MAGNA3 D» (familien; nicht neu anlegen, mit familie_aendern bzw. familie_entwurf mit familie ersetzen): Werte, Typen, Anschlüsse und Regeltechnik bleiben, wie sie sind. Ergänze die Körper, bis die Familie in grob, mittel und fein aussieht wie die Massbilder im Grundfos-Datenheft (Pumpengehäuse mit Flanschen bzw. Verschraubung, Motor, Elektronikgehäuse/Bedienfeld, Klemmenkasten) – heute zeigen mittel und fein dieselben vier bzw. sechs Quader/Zylinder. Ein frei abrufbares 3D-Modell von Grundfos (IFC, STEP) bevorzugen und für fein übernehmen; dann den geometrischen Vergleich gegen dieses Modell ausweisen. ' }
  # Voller Umfang (Auftrag Christian 02.11.2026 «alle …»; P1–K1 waren die ersten Reihen) – laufen mit Fortsetzungsrunden, -Runden hoch
  @{ nr = 'PG'; hersteller = 'Grundfos'; kategorie = 'Pumpe'
     hinweis = 'ALLE Pumpenmodelle von Grundfos, die Grundfos in der Schweiz für Gebäudetechnik führt: Nassläufer-Umwälzpumpen, Trockenläufer Inline und Block, Druckerhöhung, Abwasser und Hebeanlagen – je Baureihe eine Familie (Einzel- und Doppelpumpe getrennt, wenn Massbild oder Anschlüsse verschieden), Eigenschaft bauart passend. Je Runde einige Baureihen vollständig, die gängigen zuerst. ' }
  @{ nr = 'PW'; hersteller = 'Wilo'; kategorie = 'Pumpe'
     hinweis = 'ALLE Pumpenmodelle von Wilo, die Wilo in der Schweiz für Gebäudetechnik führt: Nassläufer-Umwälzpumpen, Trockenläufer Inline und Block, Druckerhöhung, Abwasser und Hebeanlagen – je Baureihe eine Familie (Einzel- und Doppelpumpe getrennt, wenn Massbild oder Anschlüsse verschieden, z. B. Stratos MAXO-D), Eigenschaft bauart passend. Je Runde einige Baureihen vollständig, die gängigen zuerst. ' }
  @{ nr = 'PB'; hersteller = 'Biral'; kategorie = 'Pumpe'
     hinweis = 'ALLE Pumpenmodelle von Biral (Schweiz), die Biral für Gebäudetechnik führt: Nassläufer-Umwälzpumpen, Trockenläufer Inline und Block, Druckerhöhung, Abwasser und Hebeanlagen – je Baureihe eine Familie (Einzel- und Doppelpumpe getrennt, wenn Massbild oder Anschlüsse verschieden), Eigenschaft bauart passend. Je Runde einige Baureihen vollständig. ' }
  @{ nr = 'VS'; hersteller = 'Siemens'; kategorie = 'Ventil'
     hinweis = 'Siemens (Smart Infrastructure, Acvatix): ALLE Ventile der Reihen VXG.. (Dreiweg) und VVG.. (Durchgang), die mit SQX-Antrieben kombiniert werden, je Ventilreihe eine Familie mit allen Nennweiten und Kvs-Werten, antriebe (Kombination mit der bestehenden Familie «Siemens SQX» bzw. weiteren SQX-Familien) und anbau. Fehlende SQX-Varianten (z. B. mit Rückmeldung, Notstellfunktion, Bus) in die Antriebsfamilie ergänzen. ' }
  @{ nr = 'VB'; hersteller = 'Belimo'; kategorie = 'Ventil'
     hinweis = 'Belimo: ALLE Regelkugelhähne R20xx (2-Weg) und R30xx (3-Weg) mit allen zugehörigen Antrieben, die Belimo dafür nennt – je Ventilreihe bzw. Antriebsreihe eine Familie, Antriebe mit allen Varianten (Auf/Zu, 3-Punkt, stetig, MP-Bus, Modbus, BACnet, Notstellfunktion, Federrücklauf) und vollständiger Regeltechnik, Kombinationstabelle antriebe und anbau je Ventil. ' }
  @{ nr = 'KE'; hersteller = 'EBRO'; kategorie = 'Ventil'
     hinweis = 'EBRO Armaturen: Absperrklappen Z 011 (Zwischenflansch) und Z 014 (Lug bzw. Vollflansch), DN 50 bis DN 250, alle Ausführungen mit verschiedenen Massen als je eigene Familie, bauart absperrklappe, verbindung flansch; Antriebe (Handhebel, Getriebe, elektrische Schwenkantriebe) als Antriebsfamilien, soweit EBRO sie ohne Anmeldung bemasst, mit antriebe und anbau. ' }
  # IMI (Christian 02.11.2026: «FBH verteiler, FBH Verteilerkasten», «fabrikat IMI», «Expansionsgefässe ebenfalls IMI»)
  @{ nr = 'I1'; hersteller = 'IMI'; kategorie = 'Verteiler'
     hinweis = 'IMI Hydronic Engineering (Heimeier): Fussbodenheizungs-Verteiler «Dynacon Eclipse», alle Abgangszahlen, die IMI führt (Typ je Abgangszahl), Anschlüsse vl_ein und rl_aus nach Klasse Verteiler sowie die Abgänge, soweit bemasst. Zuerst die Antriebsfamilie der thermischen bzw. elektrischen Stellantriebe, die IMI für diesen Verteiler nennt (Kategorie Antrieb, Regeltechnik), und die Familie des passenden Heimeier-Verteilerkastens (Kategorie Verteilerkasten, Unterputz und Aufputz als getrennte Familien, wenn die Masse verschieden sind; Typen = Kastengrössen), dann der Verteiler mit antriebe (ohne anbau) und kaesten (Zuordnung Abgangszahl → Kastengrösse aus der IMI-Unterlage). ' }
  @{ nr = 'I2'; hersteller = 'IMI'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): statische Ausdehnungsgefässe «Statico», alle Grössen, die IMI für Heizung und Kälte führt (Typen nach Nennvolumen; verschiedene Bauformen mit verschiedenen Massen als getrennte Familien). Eigenschaft bauart: statisch. Keine Elektrik. ' }
  @{ nr = 'I3'; hersteller = 'IMI'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): Kompressor-Druckhaltung «Compresso», alle Ausführungen und Gefässgrössen, die IMI führt (Steuereinheit und Gefässe als je eigene Familie, wenn sie getrennt aufgestellt werden; die Kombination im Bericht). Eigenschaft bauart: kompressor. Regeltechnik der Steuereinheit vollständig (Speisung, Leistung, Störmeldungen, Bus wie BACnet/Modbus mit nötigem Modul, Datenpunkte). ' }
  @{ nr = 'I4'; hersteller = 'IMI'; kategorie = 'Ausdehnungsgefaess'
     hinweis = 'IMI Hydronic Engineering (Pneumatex): Pumpen-Druckhaltung «Transfero», alle Ausführungen und Gefässgrössen, die IMI führt (Steuereinheit und Gefässe als je eigene Familie, wenn sie getrennt aufgestellt werden; die Kombination im Bericht). Eigenschaft bauart: pumpe. Regeltechnik der Steuereinheit vollständig (Speisung, Leistung, Störmeldungen, Bus wie BACnet/Modbus mit nötigem Modul, Datenpunkte). ' }
  # Kugelhähne (Christian 02.11.2026: «Kugelhanen mit knebelgriff und mit schmetterlingsgriff» – «fabrikat ega. bsp Buderus.»)
  @{ nr = 'A1'; kategorie = 'Ventil'
     hinweis = 'Absperr-Kugelhähne für Heizung (Gewindeanschluss, Nennweiten wie der Hersteller sie führt) in zwei Ausführungen: mit Knebelgriff (Hebelgriff) und mit Schmetterlingsgriff (Flügelgriff). Fabrikat beliebig, Vorschlag Christian: Buderus; nimm Buderus, wenn ein Buderus-Datenblatt mit Massen je Nennweite ohne Anmeldung erhältlich ist, sonst einen anderen gängigen Hersteller mit vollständig bemasstem Datenblatt und nenne ihn deutlich im Bericht und im Familiennamen. Eigenschaft art der Klasse Ventil: kugelhahn; Medium «*». Unterscheiden sich die Masse der beiden Griffe, zwei Familien («… Knebelgriff», «… Schmetterlingsgriff»), sonst eine Familie mit Typparameter griff. Keine Elektrik, kein Abschnitt regeltechnik. ' }
  # Gewinderohr mit GF+-Formteilen (Christian 03.11.2026: «wir brachen auch eine gewinderohrfamilie 3/8" bis 4", alle Formteile von GF+»)
  @{ nr = 'G1'; hersteller = 'GF'; kategorie = 'Bogen'
     hinweis = 'Georg Fischer (GF+) Temperguss-Gewindefittings für die bestehende Leitungsserie «Stahlrohr Gewinderohr» (familien; Grössen 3/8" bis 4", die Serie nicht neu anlegen): ALLE Formteile, die GF für diese Grössen führt, je Formstückart eine Familie mit Typ je Grösse bzw. Grössenpaar, Typname und Figurnummer wie GF: Bogen und Winkel (Kategorie Bogen, alle Winkel), T-Stücke auch reduziert (TStueck), Reduktionen, Reduziermuffen und -nippel (Reduktion), Muffen und Doppelnippel (Muffe), Verschraubungen (Verschraubung), Kreuze (Kreuz), Kappen und Stopfen (Endkappe). Gewinde je Anschluss wie in der GF-Unterlage (z. B. Rp, R, G) und Anschluss-dn nur, wenn GF ihn nennt. Danach in der Serie leitung.formteile bogen, tstueck und reduktion auf die GF-Familien setzen (familie_aendern; die bisherigen Formteile bleiben als Familien bestehen) und im Bericht nennen, was ersetzt wurde. Masse nur aus GF-Unterlagen, keine Preise. ' }
  @{ nr = 'G1b'; hersteller = 'GF'; kategorie = 'TStueck'
     hinweis = 'FORTSETZUNG von G1 (GF+ Temperguss-Gewindefittings zur Serie «Stahlrohr Gewinderohr», 3/8" bis 4"): «GF Temperguss Bögen und Winkel» besteht schon (familien). Lege jetzt die übrigen GF-Formstückarten an, je Art eine Familie: T-Stücke auch reduziert (TStueck), Reduktionen, Reduziermuffen und -nippel (Reduktion), Muffen und Doppelnippel (Muffe), Verschraubungen (Verschraubung), Kreuze (Kreuz), Kappen und Stopfen (Endkappe), mit Gewinde und verbindung je Anschluss nach der GF-Unterlage. Korrigiere ausserdem die Typregel leitung.formteile.bogen der Serie: sie wählt heute nur nach dn und findet für 45° keinen Typ – entweder die Regel um den Winkel ergänzen (Exemplar winkel bzw. Typen je Winkel) oder die Regel entfernen, damit der Kern den Typ nach Grösse und Winkel wählt; erlaubte Winkel (regeln.winkel) wie die GF-Bögen. Setze danach tstueck und reduktion der Serie auf die GF-Familien. ' }
  # Befund 03.11.2026: die KI begrenzt einen Auftrag auf seine Kategorie («für die Kategorie Bogen vollständig») – Formteile je Kategorie getrennt
  foreach ($kx in @(@('T', 'TStueck', 'T-Stücke auch reduziert'), @('R', 'Reduktion', 'Reduktionen, Reduziermuffen und -nippel'), @('M', 'Muffe', 'Muffen und Doppelnippel'),
                    @('V', 'Verschraubung', 'Verschraubungen'), @('K', 'Kreuz', 'Kreuze'), @('E', 'Endkappe', 'Kappen und Stopfen'))) {
    @{ nr = "G1$($kx[0])"; hersteller = 'GF'; kategorie = $kx[1]
       hinweis = "Georg Fischer (GF+) Temperguss-Gewindefittings 3/8`" bis 4`" zur bestehenden Serie «Stahlrohr Gewinderohr»: ALLE $($kx[2]) (Kategorie $($kx[1])), die GF führt, je Formstückart (Figur) eine Familie, Typ je Grösse bzw. Grössenpaar mit Figurnummer, Gewinde und verbindung je Anschluss nach der GF-Unterlage. Masse nur aus GF-Unterlagen, keine Preise. " }
    @{ nr = "G2$($kx[0])"; hersteller = 'Nussbaum'; kategorie = $kx[1]
       hinweis = "R. Nussbaum AG, Rotguss-Gewindefittings Optifitt-Serra (Trinkwasser): ALLE $($kx[2]) (Kategorie $($kx[1])) dieses Sortiments, je Formstückart (Artikelnummer) eine Familie, Typ je Grösse bzw. Grössenpaar, Gewinde und verbindung je Anschluss nach der Unterlage. Die Winkel bestehen schon. Masse nur aus der Nussbaum-Unterlage, keine Preise. " }
  }
  @{ nr = 'N1V'; hersteller = 'Nussbaum'; kategorie = 'Ventil'
     hinweis = 'R. Nussbaum AG (nussbaum.ch): Ventile und Hahnen für die Hausinstallation – Absperrventile, Rückflussverhinderer, Druckreduzierventile, Sicherheitsgruppen, Kugelhahnen, Auslauf- und Entleerhahnen (Kategorie Ventil, Eigenschaft art passend: absperrventil, rueckflussverhinderer, druckreduzierventil, kugelhahn, auslaufhahn, entleerhahn), je Reihe eine Familie, Typen nach Nennweite bzw. Anschluss, Gewinde und verbindung je Anschluss nach der Unterlage. Die Filter bestehen schon. ' }
  # Rotguss-Fittinge (Christian 03.11.2026: «wir brauchen auch eine Familie für Rotguss fittinge z.bsp. JRG oder Nussbaum.»)
  @{ nr = 'G2'; hersteller = 'Nussbaum'; kategorie = 'Bogen'
     hinweis = 'Rotguss-Gewindefittings für Trinkwasser (Sanitär), Fabrikat JRG (Georg Fischer JRG) oder Nussbaum (R. Nussbaum AG), beide Schweiz: nimm den Hersteller, dessen Sortiment mit Massen je Grösse ohne Anmeldung erhältlich ist, und nenne ihn im Familiennamen. ALLE Formstückarten des Rotguss-Gewindesortiments, je Art eine Familie mit Typ je Grösse bzw. Grössenpaar (Typname und Artikel- bzw. Figurnummer wie der Hersteller): Bogen und Winkel (Kategorie Bogen), T-Stücke auch reduziert (TStueck), Reduktionen, Reduziermuffen und -nippel (Reduktion), Muffen und Doppelnippel (Muffe), Verschraubungen (Verschraubung), Kreuze (Kreuz), Kappen und Stopfen (Endkappe). Gewinde je Anschluss wie in der Unterlage (z. B. Rp, R, G) und verbindung je Anschluss (gewinde-innen bzw. gewinde-aussen) nach der Unterlage. Die Familien sind Formteile ohne eigene Leitungsserie; im Bericht nennen, zu welchen bestehenden Rohrserien sie passen. Masse nur aus Herstellerunterlagen, keine Preise. ' }
  # Nussbaum (Christian 03.11.2026: «Und eine Familie Nussbaum Wasserfilter., Ventile und Hahnen.»)
  @{ nr = 'N1'; hersteller = 'Nussbaum'; kategorie = 'Filter'
     hinweis = 'R. Nussbaum AG (Schweiz, nussbaum.ch): Sanitärarmaturen für die Hausinstallation – Wasserfilter (Kategorie Filter, je Reihe eine Familie, z. B. Rückspül- und Feinfilter), Ventile (Kategorie Ventil: Absperrventile, Rückflussverhinderer, Druckreduzierventile, Sicherheitsgruppen, je Reihe eine Familie, Eigenschaft art passend) und Hahnen (Kategorie Ventil: Kugelhahnen, Auslauf- und Entleerhahnen; art kugelhahn, auslaufhahn bzw. entleerhahn). Die gängigen Reihen des aktuellen Nussbaum-Sortiments, Typen nach Nennweite bzw. Anschluss; Gewinde und verbindung je Anschluss nach der Unterlage. Elektrische Ausführungen (z. B. Filter mit automatischer Rückspülung) mit Regeltechnik. ' }
  # Übergangsteile je Serie (Christian 03.11.2026: «es gibt in allen familien übergangsteile») – Verbindungstabelle
  foreach ($x in @(
      @('U1', 'Geberit Mapress C-Stahl', 'Geberit'), @('U2', 'Geberit Mapress Edelstahl', 'Geberit'), @('U3', 'Geberit PE', 'Geberit'),
      @('U4', 'Geberit Silent-db20', 'Geberit'), @('U5', 'JRG Sanipex MT', 'Georg Fischer JRG'), @('U6', 'Stahlrohr Gewinderohr', 'Georg Fischer (GF+) bzw. den Fitting-Hersteller der Serie'),
      @('U7', 'Stahlrohr geschweisst', 'den Hersteller der Vorschweiss-Formteile der Serie'), @('U8', 'Uponor Uni Pipe PLUS FBH', 'Uponor'),
      @('U9', 'Wickelfalzrohr rund', 'Schmidlin'), @('U10', 'Rechteckkanal', 'Schmidlin'))) {
    @{ nr = $x[0]; kategorie = 'Uebergang'
       hinweis = "ÜBERGANGSTEILE für die bestehende Leitungsserie «$($x[1])» (familien; Serie und ihre Formteile nicht neu anlegen). Lege alle Übergangsstücke an, die $($x[2]) für diese Serie führt (z. B. auf Innen- bzw. Aussengewinde, Verschraubung, Flansch, andere Rohrsysteme bzw. bei Kanälen rund/eckig und auf Geräte-Stutzen), je Übergangsart eine Familie der Kategorie Uebergang nach Abschnitt «Verbindungstechnik» der Anleitung: Anschluss a = Seite der Serie im Ursprung mit der Grösse der Serie, b = andere Seite mit verbindung und gewinde bzw. dn bzw. d wie in der Unterlage, Typ je Grössenpaar (Typname bzw. Artikelnummer wie der Hersteller). Danach in der Serie mit familie_aendern leitung.verbindung (Technik der Serie nach der Unterlage) und leitung.uebergaenge (ids der neuen Übergangs-Familien) setzen. Masse nur aus Herstellerunterlagen, keine Preise. " }
  }
  # Lüftung (Christian 02.11.2026: «für lüftung gehören alle Rohre Kanäle inkl. Formstücke + Kompunenten zur Basisfamilie. + alles KWL von Zender Comfosystems»)
  # Umfang und Fabrikat nach Vorschlag, bestätigt von Christian 02.11.2026 («Ja, so lassen»): Zehnder zuerst, Komponenten Schmidlin, sonst Trox.
  @{ nr = 'Z1'; hersteller = 'Zehnder'; kategorie = 'Lueftungsgeraet'
     hinweis = 'Zehnder (ComfoSystems): alle Wohnungslüftungsgeräte mit Wärmerückgewinnung der aktuellen ComfoAir-Reihen, die Zehnder in der Schweiz führt (je Reihe eine Familie, Typen nach Baugrösse und Ausführung). Anschlüsse aul, zul, abl, fol nach Klasse Lueftungsgeraet mit Durchmesser wie in der Unterlage. Regeltechnik vollständig (Speisung, Leistung, Bedienung und Bus wie Modbus/KNX/BACnet mit nötigem Modul, Datenpunkte für Stufe, Störung, Filter, Temperaturen). ' }
  @{ nr = 'Z2'; hersteller = 'Zehnder'; kategorie = 'Luftverteiler'
     hinweis = 'Zehnder (ComfoSystems): Luftverteilsystem – Verteilerkästen der Reihe ComfoWell als Familien der Kategorie Luftverteiler (Typen nach Anzahl Abgänge, Abgänge als Anschlüsse), dazu das Rohrsystem ComfoTube als Leitungsserie (Kategorie Kanal, rund bzw. wie in der Unterlage) mit seinen Formteilen (Bogen, T-Stück, Reduktion, soweit bemasst) nach dem Abschnitt «Leitungsserien und Formteile». ' }
  @{ nr = 'Z3'; hersteller = 'Zehnder'; kategorie = 'Zuluftdurchlass'
     hinweis = 'Zehnder (ComfoSystems): Zu- und Abluftventile ComfoValve und die Luftdurchlässe des Systems, die Zehnder führt (Zuluft als Kategorie Zuluftdurchlass, Abluft als eigene Familie der Kategorie Abluftdurchlass), Typen nach Grösse. ' }
  @{ nr = 'Z4'; hersteller = 'Zehnder'; kategorie = 'Erdwaermetauscher'
     hinweis = 'Zehnder (ComfoSystems): Erdwärmetauscher bzw. Erdregister für Wohnungslüftung, die Zehnder heute führt (z. B. ComfoFond), je Bauart eine Familie; elektrische Teile (Pumpe, Regelung) mit Regeltechnik. ' }
  @{ nr = 'L1'; kategorie = 'Hosenstueck'
     hinweis = 'Schmidlin (wie die freigegebenen Serien «Wickelfalzrohr rund» und «Rechteckkanal» der Bürobibliothek): fehlende Formstücke Hosenstück (Kategorie Hosenstueck) und Endkappe/Enddeckel (Kategorie Endkappe) für Wickelfalzrohr rund und Rechteckkanal, Masse nur aus dem Schmidlin-Katalog bzw. Schmidlin-Datenblättern, PREISE NIE ÜBERNEHMEN; fehlt ein Mass, Typ offen lassen. ' }
  @{ nr = 'L2'; kategorie = 'Brandschutzklappe'
     hinweis = 'Brandschutzklappen rund und rechteckig: Schmidlin, falls Schmidlin sie bemasst führt, sonst Trox (Trox bietet VDI-3805-Daten – nur als Zweitbeleg). Auslöser und Endschalter bzw. Federrücklaufantrieb als Antriebsfamilie (Kategorie Antrieb) mit Regeltechnik, Kombination in antriebe, anbau aus dem Massbild. ' }
  @{ nr = 'L3'; kategorie = 'Luftklappe'
     hinweis = 'Absperr- und Regelklappen (Luftklappen) rund und rechteckig: Schmidlin, sonst Trox; Antriebe als Antriebsfamilie (z. B. die Klappenantriebe, die der Hersteller nennt) mit Regeltechnik, Kombination in antriebe, anbau aus dem Massbild. Eigenschaft art der Klasse Luftklappe. ' }
  @{ nr = 'L4'; kategorie = 'Schalldaempfer'
     hinweis = 'Rohr- und Kulissenschalldämpfer: Schmidlin, sonst Trox; Typen nach Grösse und Länge wie in der Unterlage, keine Akustikkennwerte erfinden (Einfügedämpfung nur, wenn tabelliert). ' }
  @{ nr = 'L5'; kategorie = 'Volumenstromregler'
     hinweis = 'Volumenstromregler rund und rechteckig (variabel und konstant): Schmidlin, sonst Trox; Regler-/Antriebseinheit als Antriebsfamilie mit Regeltechnik (Stellsignal, Rückmeldung, Bus), Kombination in antriebe. ' }
  @{ nr = 'L6'; kategorie = 'Zuluftdurchlass'
     hinweis = 'Luftdurchlässe für Büro und Wohnen (Tellerventile, Gitter, Drallauslässe): Schmidlin, sonst Trox; Zuluft und Abluft als getrennte Familien der Kategorien Zuluftdurchlass bzw. Abluftdurchlass, Typen nach Grösse. ' }
)

# Fortsetzungsrunden (Befund 03.11.2026: ein KI-Lauf endet nach 10–20 min und deckt «alle …» nur teilweise ab): derselbe Auftrag läuft
# erneut mit den schon angelegten Familien, bis die KI «VOLLSTÄNDIG» meldet, eine Runde nichts Neues anlegt oder -Runden erreicht ist.
$angelegt = @{}
$vorgabe = @{}
foreach ($v in ($Vorhanden | ForEach-Object { $_ -split ';' } | Where-Object { $_ -match '=' })) { $k0, $w0 = $v -split '=', 2; $vorgabe[$k0.Trim()] = @($w0 -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
foreach ($a0 in $auftraege) {
 if ($Nur.Count -gt 0 -and $Nur -notcontains $a0.nr) { continue }
 $angelegt[$a0.nr] = @($vorgabe[$a0.nr] | Where-Object { $_ })
 for ($runde = 1; $runde -le $Runden; $runde++) {
  $a = $a0
  # Befund 03.11.2026: vorhandene Familien des Herstellers vor jeder Runde aus dem Projekt lesen (Namen beginnen mit dem Hersteller), dazu -Vorhanden
  if ($a.hersteller) {
    try { $pf = @((Invoke-RestMethod "$u/api/abfrage/familien" -Method Post -ContentType 'application/json' -Body '{}').familien | ForEach-Object { $_.name } | Where-Object { $_ -like "$($a.hersteller)*" }) } catch { $pf = @() }
    $angelegt[$a.nr] = @(@($angelegt[$a.nr]) + $pf | Where-Object { $_ } | Select-Object -Unique)
  }
  $vor = @($angelegt[$a.nr])
  $liste = $vor -join '; '
  if ($liste.Length -gt 1600) { $liste = $liste.Substring(0, 1600) + ' … (weitere: Abfrage familien)' }
  $fortsetzung = if ($vor.Count -gt 0) { "FORTSETZUNG (Runde $runde): Zu diesem Auftrag bestehen schon diese Familien (familien): $liste. Nicht neu anlegen; lege nur an, was vom Auftrag noch fehlt (weitere Reihen, Formstückarten, Grössen, Antriebe). Fehlt nichts mehr, lege nichts an und beginne den Bericht mit dem Wort VOLLSTÄNDIG in Grossbuchstaben. " } else { '' }
  $hinweis = $fortsetzung + $a.hinweis + $gemeinsam
  if ($hinweis.Length -gt 4000) { $hinweis = $hinweis.Substring(0, 4000) }
  $koerper = @{ art = 'familie_aus_unterlagen'; unterlagen = @('herstellerbibliothek_reihen.csv'); kategorie = $a.kategorie; hinweis = $hinweis; internet = $true; zeitlimitMinuten = 90 } | ConvertTo-Json -Depth 4
  $st = $null
  for ($v = 0; $v -lt 900 -and -not $st; $v++) {   # 409 = anderer KI-Lauf im Projekt: bis 15 h warten (zweite Warteschlange auf demselben Server)
    try { $st = Invoke-RestMethod "$u/api/auftraege" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($koerper)) }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 409) { Start-Sleep 60 } else { Log "$($a.nr) Start FEHLER $($_.ErrorDetails.Message)"; break } }
  }
  if (-not $st) { break }
  Log "$($a.nr) Runde $runde gestartet $($st.id)"
  $weg = 0   # Befund 02.11.2026: ein .NET-Update beendete den Server – nicht endlos weiterfragen
  do { Start-Sleep 30; try { $s = Invoke-RestMethod "$u/api/auftraege/$($st.id)"; $weg = 0 } catch { $s = $null; $weg++ } } while ($weg -lt 20 -and (-not $s -or $s.status -in @('wartet', 'laeuft')))
  if ($weg -ge 20) { Log "$($a.nr) Server seit 10 min nicht erreichbar - Abbruch"; exit 1 }
  $fam = @($s.ergebnis.familien | ForEach-Object { "$($_.kennzeichen) $($_.name) [$($_.kategorie)] Typen $(@($_.typen).Count) offeneKonflikte $($_.offeneKonflikte)" }) -join '; '
  Log "$($a.nr) Runde $runde $($s.status) $($st.id): $fam"
  $neu = @($s.ergebnis.familien | ForEach-Object { $_.name } | Where-Object { $_ -and $vor -notcontains $_ })
  $angelegt[$a.nr] = @($vor + $neu)
  # -cmatch: nur das grossgeschriebene Signalwort (Befund 03.11.2026: «vollständig» im Fliesstext beendete die Runden)
  if ($s.status -ne 'fertig' -or $neu.Count -eq 0 -or "$($s.ergebnis.bericht)" -cmatch 'VOLLST(Ä|AE)NDIG') { Log "$($a.nr) abgeschlossen nach Runde $runde"; break }
 }
}
Log 'fertig'

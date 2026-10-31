# Modul 6 – Lüftung: Konzept Teil A «Lüftungskette bis zum Raumbedarf»

Stand 31.10.2026 · Hauptlauf · **Konzept, nicht gebaut.**

Christian, 31.10.2026:
- Er legte das Papier «KICAD – Einordnung der HLKS-Arbeitsschritte» vor. Es teilt die Arbeitsschritte in drei Kategorien:
  - Leap-fähig: KICAD erzeugt die Lösung regelbasiert.
  - gewichtet: KICAD erzeugt Varianten und empfiehlt eine.
  - verhandelt: KICAD formuliert Forderungen und Konflikte.
- Das Papier empfiehlt, mit der Lüftungskette zu beginnen.
- Zur Reihenfolge: «ich finde die reihenfolge nicht so wichtig. wir können lüftung auch vorziehen.»
- «Ja, Konzept schreiben».

## Ziel

KICAD beansprucht seinen Rahmen, statt ihn hinzunehmen. Aus Räumen und Luftmengen erzeugt es Lüftungsvarianten, bewertet sie
und dimensioniert sie vor. Daraus leitet es den **Raumbedarf als Forderung an die Architektur** ab: Zentrale, Steigzonen und
Deckenhohlraum, mit den Folgen je Variante. Die Lösung des Raumkonflikts macht KICAD nicht; das ist Verhandlung (Mensch).

Die Kette des Papiers:

| # | Schritt | Kategorie | In diesem Modul |
| --- | --- | --- | --- |
| 1 | Raumanforderungen einlesen | Eingabe | **vorhanden**, HKAP-Projektierung über Modul 1d (A-1) |
| 2 | Luftmengen und Lasten | Leap-fähig | **vorhanden**, HKAP rechnet mit SIA-2024-Standardwerten; Rechenkern nach SIA 382/1 erst in Teil B |
| 3 | Varianten erzeugen und gewichten | gewichtet | A-2, A-6 |
| 4 | Prinzipschema je Variante | Leap-fähig | A-3 |
| 5 | Vordimensionierung mit vereinfachter Trasse | Leap-fähig | A-4 |
| 6 | Raumbedarf ableiten | Leap-fähig | A-5 |
| 7 | Raumbedarf als Forderung ausgeben | verhandelt (Ausgabe) | A-7 |

## Ausgangslage und Abgrenzung

- **Normen:** SIA 382/1 und die Lüftungsnormen liegen nicht im Normenverzeichnis (`W:\05_Normen und Merkblätter`). Teil A
  verwendet deshalb keine Normwerte. Wo ein Planungswert nötig ist, gilt einer der zwei Fälle:
  - Der Wert kommt von Christian. Er wird als **Bürowert** mit Herkunft «Christian, Datum» gespeichert.
  - Es ist eine physikalische Beziehung ohne Normwert, z. B. Querschnitt = Volumenstrom ÷ Geschwindigkeit.

  Druckverlust, Schall, spezifische Ventilatorleistung und Energie nach Norm bleiben Teil B.
- **Bestand KICAD** (Abgleich 31.10.2026):
  - Klassen: Kanal (KA), Lueftungsgeraet (LG), Zuluftdurchlass (ZD), Abluftdurchlass (AD), Volumenstromregler (VVS).
  - Medien: Aussenluft, Zuluft, Abluft, Fortluft.
  - Kanalserien K1 Wickelfalzrohr, K2 Rechteckkanal, K3 Flexrohr aus Schmidlin-Katalogen (Familienbibliothek; K3 mit offenem
    Entscheid).
  - **Fehlt:** Brandschutzklappe, Schalldämpfer, Zone bzw. Brandabschnitt, Raumreservation, Lüftungsgeräte-Familien mit
    Volumenstrombereich und Abmessungen, Prinzipschema-Anordnung (Modul 5 A-S 1 wartet auf Christians Beispielplan).
- **HKAP-Projektierung** (Tools-Modul «Projektieren»):
  - Je Raumstempel: Nutzung, Personen, Zu- und Abluft, Heiz- und Kühlleistung, Temperaturen, CO₂.
  - Standardwerte nach SIA 2024:2021, Gleichzeitigkeit je Etage (kürzt die Summe), Strangschema mit kumulierten Luftmengen.
  - Modul 1d überträgt die Raumwerte (`raeume`: zuluft, abluft, strangIds …). Die Gleichzeitigkeit überträgt es nicht.
- **Abgrenzung zu Modul 12** (Generator und Bewertung, `ablauf.md`):
  - Modul 12 G1 sucht echte Kanalwege im Geschoss. Teil A hier führt nichts im Raum, sondern schätzt Längen (A-4).
  - Die **Bewertung** (A-6) wird gewerkneutral gebaut. Modul 12 E verwendet sie für die Varianten der Leitungsführung.
  - Später ersetzt Modul 12 die geschätzte Trasse durch eine gesuchte; die Kette bleibt dieselbe.
- **Keine Modellkopie je Variante:** Teil A rechnet auf den Daten (Räume, Luftmengen, Bürowerte) als reine Funktion. Das Modell
  ändert sich erst bei der Übernahme einer Variante (A-8). Damit hängt Teil A nicht an den Trockenlauf-Kosten (H1).
- **Vorgaben aus dem Vorhaben** (`vorhaben-erzeugendes-cad.md`): Trockenlauf, Verlauf als Eingabe und abfragbare Kennzahlen
  nicht verschlechtern. Jeder Befehl trägt `begruendung` und `prozessschritt` (Rang 4).

## Fertig, wenn … (Teil A)

- Für ein Projekt mit Räumen und Luftmengen erzeugt KICAD **mindestens drei Varianten** aus dem Variantenkatalog, ohne das
  Modell zu ändern.
- Jede Variante zeigt:
  - Geräte mit Volumenstrom
  - Zuordnung der Räume zu den Geräten
  - Steigzonen
  - ein Prinzipschema
  - die vordimensionierten Hauptkanäle
  - den Raumbedarf
  - die Kennzahlen
  - Befunde, z. B. wenn der Deckenhohlraum nicht reicht
- Die **Bewertung** empfiehlt eine Variante und weist jede verwendete Gewichtung mit Herkunft aus.
- Die **Raumforderung** an die Architektur entsteht als Liste bzw. Planblatt und als Raumreservationen im Modell. Freigeben und
  versenden tut der Mensch.
- Eine Variante lässt sich mit einem Befehl übernehmen und in einem Schritt rückgängig machen.
- Die KI kann die ganze Kette über MCP lesen und auslösen.
- **Referenzfälle:**
  - «MFH Musterweg» mit fiktiven Werten.
  - **BKW HS 1/2:** gegen Christians Lüftungsplanung (Projektierung PJ-2026-001 in HKAP, Projekt P-005). Nur lokal, nie im
    Repository. Christian beurteilt die Varianten fachlich.
    - Architektenmodell (Christian 31.10.2026): `W:\02_Projektdateien Objekte\Bern_BKWH_BKW HS, Viktoriastrasse\c_Pläne und
      Modelle\b_Verknüpfungen\Koordination\BKW-HS_1_ARCH_ARC.ifc`.
    - Befund 31.10.2026: Archicad, ReferenceView 1.2, 90 MB, 10 Geschosse, **kein IfcSpace**. Die Räume kommen deshalb aus der
      Raumerkennung ohne IfcSpace (Phase 2) bzw. aus den HKAP-Räumen.
    - Daneben liegt das Lüftungsmodell `BKW-HS_1_LU-GEBT_NOV.ifc`. Es dient als Vergleich für Geräte, Steigzonen und Trassen.
    - Arbeitskopie lokal unter `%USERPROFILE%\HLKS_CAD_testdaten\bkw_hs1`.

## A-1 Eingaben

1. **Räume** mit Geschoss, Fläche, lichter Höhe und Lage (Schwerpunkt).
   - **Luftmengen:** aus HKAP über Modul 1d (Herkunft HKAP), sonst Eingabe.
   - **Leere Werte:** Ein Raum ohne Luftmenge erscheint als Befund und zählt nicht. Teil A erfindet keine Werte.
2. **Gleichzeitigkeit je Geschoss bzw. Gerät:** Modul 1d um das HKAP-Feld erweitern (lesend); sonst Eingabe, Vorgabe 1.
3. **Zonen** (neue Klasse `Zone`, Kürzel ZO; Gruppierung von Räumen wie der Strang, ohne eigene Geometrie):
   - `art`: brandabschnitt, nutzungseinheit (Wohnung, Mieter), lueftungszone.
   - **Brandabschnitte erkennt KICAD grob** (Christian 31.10.2026: «die musst du im groben erkennen. basis VKF richtlinie.»).
     - Die Regeln kommen aus der VKF-Brandschutzrichtlinie «Brandschutzabstände, Tragwerke, Brandabschnitte»
       (`W:\05_Normen und Merkblätter\VKF_Brandschutzrichtlinien\VKF_Tragwerke_Brandabschnitte.pdf`). Jede Regel trägt
       Ausgabe, Ziffer und Seite; Christian prüft die Übertragung gegen.
     - Die Zuordnung leitet KICAD aus Geschossen, Nutzungen und vertikalen Räumen ab (Treppenhaus, Schacht). Jede Zone
       trägt die Herkunft «erkannt (VKF Ziff. …)» und einen Hinweis «zu bestätigen».
     - Von Hand korrigierbar. Liegt ein Brandschutzplan vor, gilt er vor der Erkennung.
4. **Kandidaten für Zentrale und Steigzonen:**
   - Zentrale: Räume mit Nutzung Technik, oder von Hand gesetzt.
   - Steigzonen: Schächte aus dem Modell, oder von Hand gesetzt.
   - Fehlt beides: Annahme je Zone am Schwerpunkt, als Befund «Lage angenommen».
5. **Planungswerte** (neuer Abschnitt `lueftung` in `kataloge/buero.json`, je Wert mit `quelle`; je Projekt in der
   Projektinformation überschreibbar). Christian, 31.10.2026:

   | Wert | Festlegung | Quelle |
   | --- | --- | --- |
   | Luftgeschwindigkeit | «Energiegesetz». Gefunden: Apparate 2 m/s bezogen auf die Nettofläche; Kanäle im massgebenden Strang nach Volumenstrom bis 1'000 / 2'000 / 4'000 / 10'000 / über 10'000 m³/h: 3 / 4 / 5 / 6 / 7 m/s; Ausnahmen nach Abs. 4 | MuKEn 2014 (Nachführung 2018) Art. 1.19 Abs. 3, wörtlich gelesen auf energiehub-gebaeude.ch. Das KEnG und die KEnV Bern (Stand 01.01.2026) nennen selbst keine Luftgeschwindigkeit. **Offen:** Gilt das im Kanton Bern, und gilt schon die MuKEn 2025 (nicht gelesen)? |
   | Wartungsfläche | Herstellerangabe, nur für Apparate und Geräte; Leitungen brauchen keine Wartungsfläche | Christian |
   | Hohlraumzuschlag | ca. 10 cm | Christian |
   | Schachtzuschlag | 20 cm (je Seite oder gesamt: beim Bau klären) | Christian |
   | Längenzuschlag | keiner (rechtwinkliger Weg ohne Zuschlag) | Christian |
   | Grenzen Rechteckkanal | offen | – |

   Fehlt ein Wert, rechnet die Kette bis zum betroffenen Schritt und meldet, welcher Wert fehlt.

## A-2 Varianten erzeugen (Schritt 3)

1. **Variantenkatalog** `kataloge/lueftung_varianten.json`. Jeder Typ beschreibt eine Regel, wie die Räume den Geräten
   zugeordnet werden. Festgelegt von Christian am 31.10.2026:
   - **zentral:** ein Gerät für das Gebäude
   - **zentral je Zone:** ein Gerät je Brandabschnitt bzw. Nutzungsart
   - **semizentral je Geschoss**
   - **dezentral je Nutzungseinheit:** Wohnungs- bzw. Mietergerät

   Einzelraumgeräte gehören nicht in den Katalog.
2. **Erzeugen** (`src/Hlks.Kern/Lueftung/Varianten.cs`): deterministisch. Gleiche Eingaben ergeben gleiche Varianten in gleicher
   Reihenfolge. Je Variante:
   - Geräte mit Lage (Kandidat aus A-1 4) und Volumenstrom: Σ Raum-Luftmengen × Gleichzeitigkeit.
   - Zuordnung Raum → Gerät, Steigzonen je Gerät.
   - Querungen von Brandabschnitten, gezählt; daraus folgen die Brandschutzklappen.
3. **Variantenstudie** (neue Klasse `Variantenstudie`, Kürzel VS): hält Eingabestand, Bürowerte, Gewichtung, Varianten und
   Kennzahlen als Daten im Modell. Netzelemente legt sie nicht an.
   - Sie lässt sich nachvollziehen und ist mit `begruendung`/`prozessschritt` protokolliert.
   - Ändert sich eine Eingabe, meldet ein Befund «Studie veraltet». Neu rechnen geht auf Knopfdruck. Das ist die Forderung des
     Papiers: «Durchrechnen der ganzen Kette auf Knopfdruck».

## A-3 Prinzipschema je Variante (Schritt 4)

- **Anordnung nach dem Fliessweg:** gewerkneutral, wie in Modul 5 A-S 1 vorgesehen. Gebaut wird sie jetzt hier zuerst;
  Heizung folgt als eigene Schicht.
  - Vorlage (Christian 31.10.2026): sein HKAP-Prinzipschema BKW HS2 (PS-2026-006), nur lesend aus der Testkopie.
  - Fliessweg Aussenluft → Gerät (Zuluftseite) → Steigzonen → Zonen, zurück Abluft → Gerät → Fortluft.
  - Brandschutzklappen an den Zonengrenzen.
- **Symbole:** aus Christians HKAP-Bibliothek `data/schema_symbole.json`, wie für Modul 5 entschieden. Lüftungssymbole sind dort
  teilweise vorhanden (Bestand vor dem Bau prüfen).
- **Vor der Übernahme** zeigt die Studie das Schema als Vorschau: Das Schema wird aus den Daten der Variante erzeugt, nicht aus
  Modellelementen. Nach der Übernahme entsteht es aus dem Modell wie jedes Schema (Modul 5: «Prinzipschema nur aus dem Modell»).
- Ein Regelbeschrieb je Schemamodul (Papier, Schritt «Funktions- und Regelbeschrieb») ist **nicht** Teil A.

## A-4 Vordimensionierung (Schritt 5)

- **Volumenstrom** je Gerät, Steigzone und Hauptkanal als Summe der zugeordneten Räume. Ohne Gleichzeitigkeit gilt Vorgabe 1.
- **Länge:** Manhattan-Distanz Gerät → Steigzone → Raumschwerpunkt im Geschoss, plus Längenzuschlag (Bürowert). Das Ergebnis
  ist als «geschätzt» gekennzeichnet.
- **Querschnitt** = Volumenstrom ÷ Auslegungsgeschwindigkeit (Bürowert je Netzteil). Gerundet wird auf die nächste Grösse der
  Kanalserie (K1 rund, K2 rechteckig mit den Grenzen aus A-1 5).
- **Gerät:** Gewählt wird aus Lüftungsgeräte-Familien nach dem Volumenstrombereich (Herstellerunterlage, Quelle wie bei den
  Familien).
  - Grundausstattung: **Zehnder** (Christian 31.10.2026) für die Wohnungsgeräte.
  - Für zentrale Geräte gibt es noch keinen Hersteller. Ohne Familie ist die Gerätegrösse eine Annahme mit Befund.
- **Geschwindigkeit in Apparaten:** Die Nettofläche des Geräts folgt aus der Apparategeschwindigkeit (A-1 5). Daraus ergibt
  sich nur ein Mindestquerschnitt als Plausibilität, nicht die Gerätewahl.
- **Teil B:** Druckverlust, Schall und spezifische Ventilatorleistung kommen erst mit Norm und Herstellerdaten.

## A-5 Raumbedarf (Schritt 6)

| Bedarf | Ableitung | Ergebnis |
| --- | --- | --- |
| Zentrale | Geräteabmessung (Familie) + Wartungsfläche (Hersteller, sonst Bürowert) | Fläche und lichte Höhe je Zentrale |
| Steigzone | Σ Kanalquerschnitte je Geschoss (Zu-, Abluft, Aussen-/Fortluft bis zur Zentrale) + Schachtzuschlag | Schachtquerschnitt je Geschoss |
| Deckenhohlraum | grösste Kanalhöhe je Zone + Hohlraumzuschlag | nötige Hohlraumhöhe je Zone |

- **Vergleich mit dem Bestand:** Lichte Höhen aus 1b/1c und vorhandene Schachtquerschnitte. Reicht der Raum nicht, entsteht ein
  Befund mit Fehlbetrag. Das ist die Grundlage der Forderung.
- Auch Folgen für Sanitär (Unterkante) und Heizung (Puffer) gehören hierher, sobald die Abhängigkeitstabelle des Papiers als
  Regelwerk steht. Teil A nicht.

## A-6 Bewertung und Empfehlung (Schritt 3)

1. **Kriterien Teil A** (Christian 31.10.2026):
   - **Raumbedarf:** Fläche Zentrale + Schacht, Hohlraumhöhe
   - **Geräte und Wartung:** Anzahl Geräte, Geräte in Nutzungseinheiten (Wartungszugang)
   - **Brandschutzklappen:** Anzahl Querungen von Brandabschnitten

   Die Kanallänge wird ausgewiesen, zählt aber nicht. Befunde mit Fehlbetrag stehen getrennt (Punkt 4).
2. **Kosten und Energie:** vorerst weggelassen (Christian 31.10.2026). Sie kommen mit Teil B bzw. den Musterprojekten.
3. **Gewichtung** (Vorschlag 31.10.2026, Christian hat nicht widersprochen):
   - Vorgabe sind Christians Erfahrungswerte (`kataloge/buero.json`, Abschnitt `bewertung`). Jedes Projekt kann sie
     überschreiben (Projektinformation).
   - Jedes Ergebnis zeigt die verwendeten Gewichte mit Herkunft. Ziele und Gewichtungen bleiben beim Menschen (Konzept
     KI-Planungsbüro, 15.10.2026).
   - Ein eigenes Bedienfeld dafür gibt es in Teil A nicht.
4. **Verfahren** (`src/Hlks.Kern/Bewertung/`, gewerkneutral, auch für Modul 12 E):
   - Jedes Kriterium wird über die Varianten normiert und gewichtet summiert.
   - **Ausschlussbefunde** (Raum reicht nicht und ist nicht verhandelbar) werden nicht verrechnet. Sie stehen getrennt.
   - Die Empfehlung nennt die zwei Kriterien, die am meisten entschieden haben.

## A-7 Raumforderung ausgeben (Schritt 7)

- **Raumreservation** (neue Klasse, Kürzel RR): Quader für Zentrale, Schacht und Hohlraumband je Zone. Die Prüfkörper nimmt die
  Kollision (3b) mit. IFC-Abbildung als Vorschlag gegen das Schema prüfen, nie aus dem Gedächtnis.
- **Liste bzw. Planblatt** «Raumbedarf Lüftung» je Variante (Planblatt wie Modul 7 A):
  - Bedarf, Bestand und Fehlbetrag
  - Begründung (Netzlängen, Geräte)
  - Kennzahlen
  - verwendete Bürowerte und Gewichte
- Austausch mit der Architektur (BCF, Modul 7b) ist später. Die Forderung verlässt KICAD nur über den Menschen.

## A-8 Übernahme

- `lueftung_variante_uebernehmen` legt im Modell an:
  - die Geräte (Familie oder Platzhalter)
  - die Systeme Aussenluft, Zuluft, Abluft, Fortluft je Gerät
  - die Zonen und die Zuordnung der Räume
  - die Raumreservationen

  Alles in einem Paket, rückgängig in einem Schritt.
- **Keine Kanäle** (Christian 31.10.2026: «Ohne Kanäle»). Die Führung macht der Mensch oder später Modul 12.

## Befehle und Abfragen (eine Tür, MCP)

- **Abfragen:**
  - `lueftung_varianten {geschosse?, variantentypen?, gewichtung?}`: rechnet ohne Speichern.
  - `raumbedarf_lesen {studie, variante}`.
  - `lueftung_lesen {studie}`: Textansicht für die KI.
- **Befehle:** `lueftung_studie_anlegen`, `lueftung_studie_neu_rechnen`, `lueftung_variante_uebernehmen`, `zone_anlegen`,
  `zone_aendern`, `raumreservation_anlegen`.
- **Befunde:** nächste freie Codes.
  - Raum ohne Luftmenge
  - Bürowert fehlt (welcher, für welchen Schritt)
  - Lage angenommen
  - Raum reicht nicht (mit Fehlbetrag)
  - Studie veraltet

## Tests (nur so viele wie nötig)

- Volumenstromsummen mit Gleichzeitigkeit.
- Querschnittswahl auf die Seriengrösse.
- Raumbedarf je Art und Befund «Raum reicht nicht».
- Bewertung: Normierung, Ausschlussbefund, deterministische Reihenfolge.
- Übernahme mit Rückgängig.
- Echtfall BKW HS 1/2 als lokaler Echt-Test, der sich ohne Daten überspringt.
- Rechenkern und Bewertung gehen vor der Abnahme an hlks-pruefer (Modul 6 gehört zu den geprüften Rechenkernen).

## Voraussetzungen und Reihenfolge

1. Christian hat am 31.10.2026 die meisten Fragen beantwortet (siehe oben, je mit Datum). Vor dem Bau fehlen noch die
   **Startgewichte** und die **Bestätigung der Luftgeschwindigkeiten** (offene Fragen 1 und 2).
2. **Bau durch den Hauptlauf**, in der Reihenfolge A-1 → A-2 → A-4 → A-5 → A-6 → A-7 → A-8 → A-3.
   - Die Brandabschnitts-Erkennung (A-1 3) braucht zuerst die VKF-Regeln mit Fundstelle. Die Richtlinie liegt auf W: als PDF.
     Am Rechner fehlt dafür noch ein PDF-Leser (poppler).
   - Nebenaufgaben gehen an Agenten: Symbole übertragen (hlks-modul), Testauswertung (hlks-test), Prüfung (hlks-pruefer).
3. **Phase 1 läuft** (Rang 5 Sanitärstrang offen). Modul 6 Teil A startet auf Christians Zuruf nach Rang 5 oder parallel, wenn er
   es so will.
4. **Grenze für echte Projekte:** Teil A braucht Räume mit Lage. Das BKW-Architektenmodell hat kein IfcSpace, und die HKAP-Räume
   liegen nur auf PDF-Plänen. Für BKW braucht es deshalb zuerst die Raumerkennung ohne IfcSpace (Phase 2). Am Beispielprojekt
   «MFH Musterweg» geht es vorher.

## Offene Fragen an Christian

1. **Startgewichte** für Raumbedarf, Geräte und Wartung sowie Brandschutzklappen.
2. **Luftgeschwindigkeit:**
   - Gelten im Kanton Bern die Werte aus MuKEn 2014 Art. 1.19 Abs. 3, oder schon die MuKEn 2025?
   - Ist das Berner Hauptformular EN-BE die richtige Stelle zum Nachweis?
3. **Schachtzuschlag 20 cm:** je Seite oder gesamt?
4. **Grenzen der Rechteckkanäle:** grösste Höhe, Seitenverhältnis.
5. **Zentrale Lüftungsgeräte:** Welcher Hersteller kommt für die Grundausstattung dazu? Zehnder deckt die Wohnungsgeräte ab.
6. **Gleichzeitigkeit:** aus HKAP über Modul 1d übernehmen (lesende Erweiterung)? Vorschlag: ja.

## Bezüge

- `entwurf/ablauf.md` (Phasen, Modul 12), `entwurf/vorhaben-erzeugendes-cad.md` (Generator, Bewertung, Datengrundlage)
- `entwurf/modul-5.md` (Prinzipschema-Anordnung A-S 1, Symbole), `entwurf/modul-1d.md` (HKAP-Räume)
- `entwurf/modul-3b-kollision.md` (Prüfkörper), `entwurf/modul-7.md` (Planblatt)
- Papier «KICAD – Einordnung der HLKS-Arbeitsschritte» (Christian, 31.10.2026; nicht im Repository)

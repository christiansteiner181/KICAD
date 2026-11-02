# Herstellerbibliothek Pumpen, Ventile, Antriebe, Klappen – mit Regeltechnik

Stand 02.11.2026 · Sitzung Herstellerbibliothek (Zweig `claude/herstellerbibliothek-c22152`) · **Konzept, Entscheide Christian 02.11.2026 eingetragen**

## Auftrag und Entscheide

Christian, 02.11.2026: «wir brauchen in der basis alle Pumpenmodelle Biral, Grundfos, Wilo. Sowie alle Ventile Siemens VXG und VVG mit
SQX antrieben, Belimo alle R20xx, R30xx sowie alle zugehörigen antriebe. Alle Klappen von EBRO Z014 Z012 von NW 50 bis NW 250» und «alle
Elektrischen Komponenten brauchen die Regeltechnischen Angaben innerhalb Familie.»

Entscheide Christian 02.11.2026 (Fragefenster):

- **Pumpen:** Nassläufer-Umwälzpumpen, Trockenläufer Inline/Block, Druckerhöhung, Abwasser/Hebeanlagen – Biral, Grundfos, Wilo.
- **Datenquelle:** VDI 3805 der Hersteller; KICAD bekommt einen Leser. Satzarten und Feldbedeutungen nur aus Norm bzw.
  Formatbeschreibung mit Fundstelle.
- **Regeltechnik in der Familie** für alle elektrischen Komponenten: elektrische Daten, Stellsignal und Rückmeldung, Kommunikation,
  Datenpunkte als Vorlage für die Klasse Datenpunkt (Modul 8 GA-Liste, Modul 9 GA-Anbindung).

## Ausgangslage (Bestand 02.11.2026)

- Familienformat `schema/hlks-familie.schema.json` (Format 1): Parameter (typ/exemplar, Formeln, `ziel` = Eigenschaft der Klasse),
  Typen mit Werten und **Quellen je Wert** (Befund-Ids `<Prüfsumme 12>#<n>` aus `unterlagen_lesen`), Körper, Anschlüsse
  (`art` rohr | kanal | elektro), Sinnbild, Schemasymbol, Unterlagen, Konflikte, Bericht. Hersteller und Produkt nur als freier Kopf.
- Unterlagen-Leser (`src/Hlks.Kern/Unterlagen/`, Schnittstelle `IUnterlagenLeser`, per Reflexion gefunden): PDF, DWG/DXF, STEP,
  Netz, IFC, Tabelle (XLSX/CSV/Typkatalog), HTML, Bild. **Kein VDI-3805-Leser** (Modul 2 hatte ihn auf «später, Format vorher prüfen»).
- Bürobibliothek `W:\03_CAD Bibliotheken\d_KICAD Familien` mit Versionen und `_unterlagen\<Prüfsumme>` (heute nur Leitungsserien).
- Klassen (`kataloge/klassen.json`): `Armatur` (abstrakt, ein/aus) → `Ventil` (art, kvs, pn), `Pumpe` (saug/druck); `Datenpunkt`
  (signal AI/AO/DI/DO/Zaehler/Wert, art, messgroesse, einheit, adresse, protokoll), `Regelkreis`, `Automationsstation`.
  **Keine Klasse Antrieb, keine Regeltechnik** am Bauteil. Seit c809707 an jedem Bauteil `hersteller`, `artikelnummer`, `dokumente`, `wartung`.

## Quellenlage VDI 3805 (Recherche 02.11.2026)

Die Blätter sind kostenpflichtig (DIN Media); frei sind nur Titel und Inhaltsverzeichnisse.

| Blatt | Inhalt | Ausgabe | Fundstelle |
| --- | --- | --- | --- |
| 1 | Grundlagen: Produktdatenmodell, Satzaufbau, Geometrie | 2022-07 | dinmedia.de/de/technische-regel/vdi-3805-blatt-1/350219035 |
| 2 | Heizungsarmaturen (Ventile) | 2021-12 | dinmedia.de/en/technical-rule/vdi-3805-blatt-2/343766835 |
| 4 | Pumpen (nur Kreiselpumpen) | 2021-02 | dinmedia.de/de/technische-regel/vdi-3805-blatt-4/327639028 |
| 24 | Stellantriebe für TGA-Komponenten | 2019-04 | dinmedia.de/en/technical-rule/vdi-3805-blatt-24/299119808 |
| 35 | Klappen, Jalousien, Volumenstromregler | 2008-01 | vdi.de/richtlinien/details/vdi-3805-blatt-35-… |

Laut Inhaltsverzeichnissen der Folgeblätter gliedert sich ein Datensatz in Satzarten für Produktbeschreibung (100 bis 450/500),
Funktionen (600), technische Daten (700 ff.), TGA-Nummer (800) und Geometrie (970 ff.) – **nur Gliederung, keine Feldbedeutungen**;
VDI 3805 wird in ISO 16757 überführt (Hinweis TROX, «Daten nach VDI 3805»).

Preise laut DIN Media (02.11.2026): Blatt 1 ab 356.50 EUR, Blatt 4 ab 165 EUR, Blatt 24 rund 110 EUR, Blatt 2 und 35 nicht erhoben.
Feldaufbau steht nur in den Blättern; frei sind Inhaltsverzeichnisse (m.vdi.de/…/inhaltsverzeichnisse/3287346.pdf Blatt 2,
…/3189375.pdf Blatt 4) und eine Analyse der RWTH (Forum Bauinformatik 2025, publications.rwth-aachen.de/record/1021202, laut Auszug
zeilenweise Textdateien `.VDI`, Semikolon als Trenner – **ungeprüft**). Kein Open-Source-Leser gefunden.

## Datenquellen je Hersteller (Recherche 02.11.2026, nichts heruntergeladen)

| Hersteller | VDI 3805 | Andere maschinenlesbare Daten | Fundstelle |
| --- | --- | --- | --- |
| Biral | **nicht angeboten** (BIM-/CAD-Download-Center nennt es nicht) | Revit, STEP, DWG, IFC über CADENAS-Portal biral.partcommunity.com | biral.ch/de/planungstools/bimcad-download-center |
| Grundfos | **nicht angeboten** (Product Center, Reiter BIM) | Revit-Familien, CAD als ZIP | grundfos.com … how-to-find-bim-assets-at-grundfos-product-center |
| Wilo | **nicht angeboten** (nur alter Hinweis Wilo-Select Classic 3.0, 2002) | Revit-Plugin mit ETIM-Daten (Webservice), STEP/DWG über CADENAS | wilo.com … Building-Information-Modeling-(BIM) |
| Siemens (VVG/VXG/SQX) | **unklar**: Datenqualität «orientiert an ISO 16757 / VDI 3805», «VDI-Ventilkennlinien als Katalogdownload» | CADENAS-Portal siemens-bt.partcommunity.com; Industry Mall mit ETIM 7/8 | siemens.com/ch/de/produkte/building-technologies/bim.html |
| Belimo | **ja** (Suchauszug): «VDI data for planners», Ausgaben 2016 und 2021, je ZIP rund 367 MB; Seite verweigerte den Abruf (403) | Revit-Plugin, DXF/RFA, CADENAS | belimo.com/…/sizing-and-selection-tools/vdi-3805.html |
| EBRO | **ja, nach Registrierung** im BIM-Portal (Suchauszug: CAD, BMEcat, Datanorm, VDI 3805) | 3D-Modelle je Produkt | ebro-armaturen.com/anwendungen/gebaudetechnik/bim-portal/ |

Folge: **Für die Pumpen trägt VDI 3805 nicht** – keiner der drei Hersteller bietet es an. Für Belimo und EBRO ist es der Weg, für
Siemens nur, wenn die Katalogdaten die Ventile samt SQX enthalten. Vergleichsfälle mit freiem Download (KSB Blatt 2/4/17, Frese) zeigen
den Aufbau der Pakete (Dateiname mit Blatt, Land, Sprache, Ausgabe, Datenstand).

## Grundsätze

1. **Werte nur mit Beleg.** Jeder Wert der Familie – Abmessung, Kennwert, Regeltechnik – nennt seine Befunde. Ein VDI-3805-Befund
   zeigt auf Satz und Feld der Datei (Satznummer, Feldnummer, Text wie in der Datei).
2. **Leser nur strukturell** (Entscheid 2 unten): Datei in Sätze und Felder zerlegen; die **Bedeutung** eines Werts belegt das
   Datenblatt. Ein VDI-Feld mit gleichem Wert ist Zweitbeleg; die Datei liefert die Typenliste der Reihe zum Abgleich.
3. **Familie je Baureihe** über den bestehenden Weg «Familie aus Unterlagen» (Status `entwurf`, Bericht mit gelesen/fehlend).
   Freigabe wie bisher nur durch einen Menschen.
4. **Keine Herstellerdaten ins Repository.** Originale auf W: (`e_Herstellerdaten`), Testkopien lokal (`~/HLKS_CAD_testdaten`);
   Tests im Repo bauen fiktive Dateien («Musterpumpe AG»).

## Familienformat: neuer Abschnitt `regeltechnik`

Neues, optionales Feld im Familienkopf. Werte, die je Typ verschieden sind, stehen wie bisher als Typparameter in `typen[].werte`
(mit `typen[].quellen`); der Abschnitt verweist mit `param` darauf. Feste Werte stehen direkt mit `quellen`.

```json
// Beispiel – alle Werte sind Platzhalter, keine Herstellerangaben
"regeltechnik": {
  "elektrisch": {
    "speisung":   [{ "spannung": 230, "art": "AC", "frequenz": 50, "phasen": 1, "quellen": ["…#12"] }],
    "leistung":   { "param": "p1_max", "einheit": "W" },
    "scheinleistung": { "param": "s_dim", "einheit": "VA" },
    "strom":      { "param": "i_max", "einheit": "A" },
    "schutzart":  { "wert": "IP44", "quellen": ["…#13"] },
    "schutzklasse": { "wert": "II", "quellen": ["…#14"] }
  },
  "stellen": {
    "signale":   [{ "art": "0-10V", "eingang": "Y", "quellen": ["…"] }, { "art": "3-punkt" }],
    "rueckmeldung": [{ "art": "2-10V", "ausgang": "U" }],
    "laufzeit":  { "param": "laufzeit", "einheit": "s" },
    "stellkraft": { "param": "stellkraft", "einheit": "N" },
    "drehmoment": { "param": "drehmoment", "einheit": "Nm" },
    "hub":       { "param": "hub", "einheit": "mm" },
    "notstellung": { "wert": "keine" }
  },
  "kommunikation": [{ "protokoll": "modbus-rtu", "zusatz": "<Busmodul laut Unterlage>", "quellen": ["…"] },
                    { "protokoll": "bacnet-mstp" }, { "protokoll": "mp-bus" }, { "protokoll": "knx" }],
  "datenpunkte": [
    { "name": "Betrieb",   "signal": "DI", "art": "meldung",  "messgroesse": "Betriebsmeldung", "weg": "kontakt", "quellen": ["…"] },
    { "name": "Stoerung",  "signal": "DI", "art": "meldung",  "messgroesse": "Sammelstörung",   "weg": "kontakt" },
    { "name": "Freigabe",  "signal": "DO", "art": "schaltung", "weg": "kontakt" },
    { "name": "Sollwert",  "signal": "AO", "art": "sollwert", "einheit": "%", "weg": "0-10V" },
    { "name": "Foerderstrom", "signal": "Wert", "art": "messung", "einheit": "m3/h", "weg": "modbus-rtu", "objekt": "<Register laut Unterlage>", "optional": true }
  ]
}
```

- **Wertebereiche** (Listen im Kern, erweiterbar): Signalarten `0-10V`, `2-10V`, `0-20mA`, `4-20mA`, `3-punkt`, `auf-zu`, `pwm`;
  Protokolle `modbus-rtu`, `modbus-tcp`, `bacnet-mstp`, `bacnet-ip`, `mp-bus`, `knx`, `lon`, `profinet`; Datenpunkt `signal` und `art`
  genau wie Klasse Datenpunkt (eine Liste, damit die Vorlage 1:1 ein Datenpunkt-Element ergibt); `weg` = Kontakt, Signalart oder Protokoll.
- **Prüfung im Kern** (wie Parameter): unbekannte Felder, Signal/Art gegen Klasse Datenpunkt, `param` muss ein Parameter sein,
  Einheit passend, jeder Wert mit `quellen` oder `param` mit Quellen je Typ; ohne Quelle Hinweis «unbelegt» (Entwurf möglich, Freigabe nicht).
- **Am Element:** Die Klasse bekommt keine Dutzend neuer Eigenschaften. Die Regeltechnik liest man über die Familie + Typ des Elements
  (Abfrage `regeltechnik` {element}); `leistung` und `spannung` werden zusätzlich Ziel-Eigenschaften (Listen, Elektro-Koordination).
- **Datenpunkte:** Modul 8 erzeugt aus der Vorlage Datenpunkt-Elemente mit `bezuege.an` = Bauteil (Befehl später, Modul 8);
  `optional` = nur auf Wunsch (Buspunkte).

## Antriebe und Ventile

Entscheid 4: **Antrieb als eigene Familie** (neue Klasse `Antrieb`, IFC `IfcActuator`, ohne Rohranschluss, mit Regeltechnik).
Die Ventilfamilie nennt die passenden Antriebe (`antriebe`: Familie + Typen, aus der Kombinationstabelle des Herstellers, mit Quelle)
und einen **Anbaupunkt** (Lage, Richtung). Das platzierte Ventil trägt die Eigenschaft `antrieb` (Familie/Typ); 3D und Listen zeigen
den Antrieb am Anbaupunkt, die Regeltechnik des Ventils ist die des Antriebs. So bleiben Siemens SQX und Belimo LR/NR/SR je **einmal**
erfasst statt in jeder Ventil×Antrieb-Kombination.

Klappen EBRO (Z 011, Z 014): Absperrklappen im Wasser = Klasse `Ventil` mit neuer Bauart `absperrklappe`; Antriebe (elektrisch/pneumatisch) wie oben.
Bauarten neu: `regelventil` (2-Weg), `regelkugelhahn`, `absperrklappe`; 3-Weg bleibt `mischventil`, dazu Eigenschaft `wege` (2/3).

## Pumpen

- Klasse `Pumpe` mit neuer Eigenschaft `bauart` (nasslaeufer, trockenlaeufer-inline, trockenlaeufer-block, druckerhoehung,
  abwasser-hebeanlage) und Regeltechnik in der Familie (Speisung, P1, Strom, Schutzart, Eingänge, Melderelais, Bus).
- **Kennlinien** (Q-H, P1, NPSH) später mit Modul 5 (Entscheid 7).
- Druckerhöhungs- und Hebeanlagen sind Geräte mit mehreren Pumpen; Familie je Anlage, Typen nach Pumpenzahl/-grösse.

## Ablage

| Was | Wo |
| --- | --- |
| Herstellerdaten wie geladen (Original, mit Datum) | `W:\03_CAD Bibliotheken\e_Herstellerdaten\<Hersteller>\<JJJJ-MM-TT>\` |
| Unterlage der Familie (Prüfsumme) | wie bisher `d_KICAD Familien\_unterlagen\<Prüfsumme>.<ext>` (automatisch) |
| Testdatei | fiktiv, im Test erzeugt; echte Dateien nur `~/HLKS_CAD_testdaten/vdi3805` (Echt-Test überspringt sich) |

## Bauschritte

| Teil | Inhalt |
| --- | --- |
| H1 | Schema + Kern: Abschnitt `regeltechnik` lesen und prüfen, Klasse `Antrieb`, Ventil-Bauarten, Pumpe `bauart`, Ventil `antriebe`/Anbaupunkt, Abfrage `regeltechnik` |
| H2 | VDI-3805-Leser Struktur (Befunde `satz`/`feld`), Art `vdi3805` in Unterlagen, Feld als Zweitbeleg in der Wertprüfung |
| H3 | Abgleich Familie ↔ Typenliste einer VDI-Datei (fehlende Typen als Hinweis) |
| H4 | Je Hersteller eine Baureihe über «Familie aus Unterlagen» (Datenblatt + IFC/STEP), Stichproben gegen Datenblatt, Christian zur Freigabe |
| H5 | Rest der Hersteller-Umfänge, Antrieb am Ventil in 3D/Listen, Bedienung (`OBERFLAECHE.md`, `inventar.json`) |

## Entscheide Christian 02.11.2026 (zweites und drittes Fragefenster)

1. **Pumpen aus Datenblatt + IFC/STEP** – Biral, Grundfos und Wilo bieten kein VDI 3805 an. Bestehender Weg «Familie aus Unterlagen»
   (Modul 2): Kennwerte und Regeltechnik aus den PDF-Datenblättern, Geometrie aus IFC/STEP, Beleg je Wert.
2. **Normblätter nicht kaufen.** Der VDI-3805-Leser zerlegt nur Sätze und Felder; er deutet keine Felder.
3. **VDI 3805 als Zweitbeleg und Typenliste:** Quelle eines Werts bleibt das Datenblatt; stimmt ein VDI-Feld mit dem Wert überein,
   wird es als zweiter Beleg genannt. Die Datei liefert die vollständige Typen-/Artikelliste einer Reihe zum Abgleich.
4. **Antriebe als eigene Familie** (Klasse `Antrieb`), Ventilfamilie mit Kombinationstabelle und Anbaupunkt.
5. **Downloads:** Das Belimo-VDI-Paket lädt die Sitzung (Dateiname, Quelle, Grösse vorher nennen); bei EBRO registriert sich Christian.
6. **EBRO: Z 011 und Z 014**, DN 50–250.
7. **Kennlinien später** mit Modul 5; jetzt Kennwerte und Regeltechnik.
9. **IMI-Zusatz (Christian 02.11.2026, über den Hauptlauf):** «wir brauchen in der Basis, FBH verteiler, FBH Verteilerkasten»,
   «fabrikat IMI», «Expansionsgefässe ebenfalls IMI». Fragefenster: FBH-Verteiler **Dynacon Eclipse** mit den IMI-Stellantrieben
   (Antriebsfamilie, `antriebe` ohne `anbau`); **Verteilerkasten als eigenes Element** (neue Klasse Verteilerkasten, Gehäuse ohne
   Netzfunktion, `bezuege.verteiler`; die Verteilerfamilie nennt die Kästen je Abgangszahl in `kaesten`); Expansion **Statico,
   Compresso, Transfero**; neue Klasse **Ausdehnungsgefaess** (bauart statisch/kompressor/pumpe, nennvolumen, ein Anschluss).
   IMI-VDI-3805-Daten nicht gefunden → Datenblatt mit Beleg je Wert. Aufträge I1–I4.
10. **Kugelhähne (Christian 02.11.2026, über den Hauptlauf):** «Kugelhanen mit knebelgriff und mit schmetterlingsgriff» –
    «fabrikat ega. bsp Buderus.» → Ventil-Bauart `kugelhahn` neu; Auftrag A1: Buderus, sonst ein anderer Hersteller mit
    vollständig bemasstem Datenblatt (deutlich genannt); zwei Familien, wenn sich die Masse der Griffe unterscheiden, sonst
    Typparameter griff.
11. **Lüftung (Christian 02.11.2026, über den Hauptlauf):** «für lüftung gehören alle Rohre Kanäle inkl. Formstücke +
    Kompunenten zur Basisfamilie. + alles KWL von Zender Comfosystems». Bestand: Wickelfalzrohr rund, Rechteckkanal (je Bogen,
    T-Stück, Reduktion), 8 Schmidlin-Flexrohre freigegeben. Neue Klassen: Hosenstueck, Endkappe (Formteile), Brandschutzklappe,
    Luftklappe, Schalldaempfer (Armaturen Lüftung, Antriebe über die Antriebsfamilie), Luftverteiler, Erdwaermetauscher.
    Reihenfolge, Fabrikat und Zehnder-Umfang nach Vorschlag, **bestätigt von Christian 02.11.2026 («Ja, so lassen»)**: Zehnder zuerst
    (Z1 ComfoAir-Geräte, Z2 ComfoWell/ComfoTube, Z3 ComfoValve/Durchlässe, Z4 Erdwärmetauscher), dann L1–L6 (Formstücke,
    Brandschutzklappen, Luftklappen, Schalldämpfer, Volumenstromregler, Durchlässe) mit Schmidlin, sonst Trox.
12. **Schemas (Christian 02.11.2026):** «du zeichnest schemas aus den daten. dies müssen wir im detail korrigieren. erst wenn
    anlagen gezeichnet sind die reale daten enthalten.» → Die Aufträge erfinden keine Schemasymbole; die Schemadarstellung der
    Bibliotheksfamilien wird an realen Anlagen korrigiert.
8. **Ablage Herstellerdaten:** `W:\03_CAD Bibliotheken\e_Herstellerdaten\<Hersteller>\<JJJJ-MM-TT>\` (Originale vor dem Einlesen).

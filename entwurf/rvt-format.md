# RVT/RFA direkt lesen – Befund zum Dateiformat

Stand 03.11.2026 · Nebensitzung (REA, Ghidra) · umgesetzt in `src/Hlks.Kern/Unterlagen/Revit*.cs`

## Anlass

Christian, 03.11.2026: «prüfe ob du herausfindest wie revit rvt dateien liest. verwende rea», danach «Ziel: revit
dateien einlesen und Exportieren», «hole die nötigen Analysewerkzeuge», «Ja, dekompilieren», «Ja, weiter». Die Frage
dahinter: Kann KICAD Revit-Modelle und -Familien direkt lesen (und schreiben), ohne den Umweg über IFC
(`src/Hlks.Kern/Import`)?

## Kurzantwort

- **Die Speicherschicht ist vollständig entschlüsselt.** Container (Compound File), Revits Fehlerkorrektur-Blöcke und
  die gzip-Kompression lassen sich ohne Revit aufheben. In einer Stichprobe von 48 Dateien der Bibliothek (RFA Revit
  2019–2025, RVT bis 118 MB) sind **alle komprimierten Ströme vollständig und CRC32-geprüft** entpackt.
- **Sofort nutzbar:** Revit-Version, Vorschaubild, Familientypen mit Parametern, Werten und Einheiten (PartAtom),
  externe Verweise. Damit liesse sich die Familienbibliothek auf W: in KICAD durchsuchen und beim Import vorbefüllen.
- **Auch die Objektebene ist gelesen (Stand 03.11.2026, Christian: «Objektleser weiter»):** Klassenschema und alle
  Element-Objekte der Partitionen werden nach dem Schema zerlegt – in der Stichprobe von 48 Dateien (Revit 2012–2025)
  **4 152 176 Sätze vollständig, 0 Lesefehler**; offen sind nur 1543 Einträge mit Extensible Storage (0,04 %). Was die
  einzelnen Felder fachlich bedeuten (z. B. welche Felder eine Leitung beschreiben) und wie Revit Geometrie (`GElement`)
  und Familienparametrik ablegt, ist damit lesbar, aber noch nicht ausgelegt.
- **Schreiben** von RVT/RFA ist nicht realistisch (Prüfdaten, Schema, Regeneration). Für den Export nach Revit ist ein
  Revit-Add-in der Weg, das KICAD-Daten über die Revit-API anlegt.

## Vorgehen und Grenzen

- Untersucht: Revit 2026 (`C:\Program Files\Autodesk\Revit 2026`, Revit.exe ProductVersion `20250227_1515(x64)`,
  448 DLLs). Nichts an Revit oder der Installation verändert, Revit nie gestartet.
- Werkzeuge: REA (Imports/Exporte, Zeichenketten), ab 03.11.2026 mit **Ghidra 12.1.4** (Dekompilierung von
  `Utility.dll`) und eigene Prüfskripte in Node (nur Bordmittel). Ghidra und JDK 21 liegen in
  `C:\Users\chris\Analysewerkzeuge`.
- Dateien der Bibliothek auf W: **nur gelesen**. Keine Inhalte, Pfade oder GUIDs daraus in diesem Bericht.
- Lizenz- und Kopierschutzmechanismen nicht untersucht; untersucht ist nur die Speicher- und Lese-Logik.
- Prüfskripte ausserhalb des Repos in den Sitzungsnotizen (`side-session-notes/…/rvt-werkzeug/`): `cfb_lesen.mjs`
  (Container, Blockkorrektur, gzip mit CRC-Prüfung), `ole_stream.ps1` (Gegenprobe über Windows-ole32).

## Wie Revit eine Datei liest

| Schicht | Befund | Beleg |
|---|---|---|
| Container | `PersistenceDB.dll` importiert aus `ole32.dll` `StgOpenStorage(Ex)`, `StgIsStorageFile`, `StgCreateDocfile` – RVT/RFA sind **OLE-Compound-Files**. | REA `ev_5c863cc5…` (Imports PersistenceDB) |
| Fehlerkorrektur | Klasse `CRCFile`, Quelldateien `CRCIO.cpp`/`CRCParameters.cpp`: der Strom ist in **Blöcke** geteilt, jeder Block endet mit Prüfdaten (Bitmatrix aus Zeilen × Spalten, Prüfzeilen am Ende). Beim Lesen prüft Revit und **repariert einzelne Bitfehler** («Error-correcting codes rescued %d bit(s)!»), dann verwirft es den Prüfteil. | Ghidra: Blocklesen `0x1800d7280`, Korrektur `0x1800d53b0` (`ev_fd176b6e…`), Grössen `0x1800d83c0` |
| Kompression | Klasse `CompressFile` → eigene gzip-Schicht **`GZIO`** (`Gzio-local.cpp`): prüft den gzip-Kopf selbst, dann rohes Deflate mit dem eingebauten **zlib 1.3.1 – unverändert** (Blocktyp-Behandlung wie im Original). Das Zeitstempel-Feld ist umgewidmet («post» = Prüfsumme folgt). | `ev_9fc3df8d…` (Read), `ev_2b3f0d79…` (open), `ev_c615973d…` (GZIO::read) |
| Stream-Namen | `StorageUtil::getStreamName` u. a.; Namen als UTF-16 in `Utility.dll`. | `ev_8e5d6b98…` (16 389 Exporte) |
| Metadaten | `BasicFileInfo::extract(Pfad)` liest die Dateiinfo, ohne das Modell zu laden. | `ev_8e5d6b98…` |
| Objekte | Archivklassen `AArchive`, `ArchiveClassMaps`, `FilePreview` …; Element-Tabellen/Worksets in `RevitDB.dll`. | Exporte/Imports |

## Aufbau der Datei

**Container:** Compound File Version 4, Sektoren 4096 B, Mini-Sektoren 64 B. Eigener Leser byte-gleich mit
Windows-ole32 (SHA-256 geprüft).

**Fehlerkorrektur-Blöcke (gemessen, CRC-bestätigt):** Jeder komprimierte Strom ist ab Byte 0 in Blöcke zu
**65 249 Bytes** geteilt; die **letzten 353 Bytes jedes vollen Blocks sind Prüfdaten**, die Nutzlast also 64 896 Bytes.
Der letzte, kürzere Block trägt ebenfalls Prüfdaten am Ende (40–600 B, Länge je nach Restgrösse). Wer den Prüfteil
nicht entfernt, bekommt verfälschte Daten – oft ohne Fehlermeldung von zlib, erkennbar nur an falscher Länge und CRC32
im gzip-Nachspann. Ströme unter einem Block (Metadaten, kleine `Global/*`) sind davon nicht betroffen. Annahme: Die
Blockparameter hängen laut Code von der Stromgrösse ab (Tabelle mit 7 Stufen, `0x1800d82c0`); in allen geprüften
Dateien (Ströme von 82 KB bis 10 MB) war es dieselbe Stufe – sehr grosse Ströme könnten abweichen.

| Stream | Inhalt |
|---|---|
| `BasicFileInfo` | roh. Vorne binär mit längenpräfixierten UTF-16LE-Zeichenketten (Format, Build, Speicherpfad, GUID); dahinter ein **Textblock in UTF-16BE** (`Format:`, `Build:`, `Worksharing:`, `Locale when saved:` …). → Revit-Version ohne Revit bestimmbar (bei einigen älteren Dateien anderer Aufbau, noch nicht erkannt). |
| `PartAtom` (nur RFA) | **UTF-8-XML im Atom-Format** (`urn:schemas-autodesk-com:partatom`): Familienname, Revit-Version, Kategorie, Familienparameter nach Gruppen, je Typ ein `A:part` mit Werten, `typeOfParameter` und Einheit. Parameternamen in der Speichersprache. |
| `RevitPreview4.0` | serialisiertes `FilePreview` → `ARasterImage`, darin ein **PNG**. |
| `TransmissionData` | 4 B Länge + UTF-16LE-XML: externe Verweise mit Pfad und Ladezustand. |
| `ProjectInformation` | ZIP-Archiv (in der RVT 2017 gesehen). |
| `Contents` | Archivkopf `62 19 22 05`, ggf. gzip ab Byte 24. |
| `Formats/Latest` | gzip ab Byte 0, entpackt z. B. 485 KB: **Klassenschema** mit Klassen-, Feldnamen und Feldtypen (rund 25 000 Bezeichner). |
| `Global/Latest`, `ElemTable`, `History`, `ContentDocuments`, `DocumentIncrementTable`, `PartitionTable` | je 8 B Vorspann (erstes Wort 0, 1 oder 5), dann ein gzip-Glied. `Global/Latest` entpackt 1,5 MB, `ElemTable` 340 KB. |
| `Partitions/<n>` | die Elemente. 44 B Kopf, dann eine Folge eigenständiger gzip-Glieder (je höchstens 128 KiB entpackt), dazwischen je 32 B Kopf (Bedeutung offen). RFA 2025: 11,3 MB, RVT 2017: 57,8 MB entpackt. RVT: eine Partition je Workset. |

Korrektur zum ersten Stand dieses Berichts: Die vermutete «Wörterbuch-Fortsetzung» zwischen gzip-Gliedern gibt es
nicht – die scheinbaren Treffer waren Folgen der fehlenden Blockkorrektur. Alle Glieder sind eigenständig.

**Stichprobe** (48 zufällige Dateien der Bibliothek ohne Sicherungskopien `.000n`, Revit 2019–2025): 40 RFA und 8 RVT
(10–118 MB) – **alle gzip-Ströme entpackt, jedes Glied mit korrekter CRC32 und Länge**.

## Was ohne Revit lesbar ist

| Teil | Stand | Aufwand |
|---|---|---|
| Version, Build, Sprache, Worksharing (BasicFileInfo) | gelöst (ältere Varianten nachziehen) | klein |
| Vorschaubild (PNG) | gelöst | klein |
| Familie: Kategorie, Typen, Parameter mit Wert, Datentyp, Einheit (PartAtom) | gelöst | klein |
| Externe Verweise (TransmissionData) | gelöst | klein |
| Alle komprimierten Ströme als Rohdaten | **gelöst** (Blockkorrektur + gzip) | klein |
| Klassenschema (`Formats/Latest`) auslegen | offen | mittel |
| Elementliste mit Klassen und Feldwerten (`Partitions` + Schema) | offen | gross |
| Geometrie, Connectors, Systeme | offen, Bedeutung der Felder unbekannt | sehr gross |
| Familien-Parametrik neu berechnen | nicht möglich ohne Revit-Kern | – |

## Aufwand und Risiko für KICAD

**Stufe 1 – Metadaten- und Familienkatalog:** **umgesetzt 03.11.2026** (Christian: «Stufe 1 bauen»), Commit 48836cc:
- `src/Hlks.Kern/Unterlagen/RevitDatei.cs`: Compound-File-Leser, Blockkorrektur (`Entblocken`), gzip mit CRC32-Prüfung
  (`Entpacken`, `Entpackt(stream)`), `Info()` mit Version (auch alte BasicFileInfo ab Revit 2012), PartAtom, Vorschau-PNG,
  externe Verweise.
- `src/Hlks.Kern/Unterlagen/RevitBefundLeser.cs`: Unterlagen-Leser «revit 1» für die neue Art `rfa`. Befunde: `pset`
  «Revit-Familie» (Name, Kategorie, Version, Build, Sprache), `pset` je Gruppe der Familienparameter, `typ` je Familientyp
  (Werte, Einheiten, Datentypen – wie ein Revit-Typkatalog), `bild` (Vorschau). Damit fliesst eine RFA wie jede andere
  Herstellerunterlage in «Familie aus Unterlagen» ein (Werte als Quellen übernehmbar); `unterlage_bild` zeigt die Vorschau.
- Angemeldet in `UnterlagenArten` (Endung .rfa), `unterlage_einfuegen` (nur zweck «familie»), Oberfläche
  (`unterlagen-text.js`: Endung und Artname), Bibliotheks-Ablage; Texte «RFA wird nicht gelesen» durch «RVT» ersetzt.
- Tests: `RevitDateiTests` (Blockkorrektur/CRC mit synthetischem Strom, PartAtom mit fiktiver Familie, Echt-Test mit
  zwei Bibliotheksfamilien unter `%USERPROFILE%\HLKS_CAD_testdaten\familien\revit`) 3/3, `unterlagen.test.mjs` 18/18.
- Nicht gebaut: ein Ordner-Katalog (alle RFA eines Ordners auf W: mit Kategorie und Typen durchsuchen) – offen als
  nächster Schritt. Nicht im Browser geprüft.

**Stufe 2 – Stand 03.11.2026 (Christian: «Stufe 2 beginnen»):** Das **Klassenschema ist vollständig entschlüsselt**
(`src/Hlks.Kern/Unterlagen/RevitSchema.cs`, `RevitDatei.Schema()`), nach Revits `ArchiveClassMaps::loadAllClasses`,
`loadNewClass`, `readADDTable`, `readADDEntry` und `FieldType::getDescription` (Ghidra):
- Klasse: `u16` Version (0), `u16` Namenlänge, Name, Elternklasse als Verweis, `u32` Schema-Nummer, `u32` Feldzahl,
  Felder, `u32` Anzahl Upgrade-GUIDs, je 16 B. Ende: Version 0 und Namenlänge 0.
- Verweis: `u16` Tag; 0 = keine, Bit 15 = neue Klasse folgt inline, sonst Tag einer geladenen Klasse (auch Selbstbezug).
  Tags zählen in Ladereihenfolge ab 12.
- Feld: `u32` Namenlänge (1–63), Name, `u32` Feldtyp (Byte 0 Grundtyp 1 bool, 2 char, 3 short, 4 int, 5 long, 6 float,
  7 double, 8 TCHAR, 9 GUID, 10 Klassenreferenz, 11 int64, 13 typedef, 14 Klasse; Bits 8–11 Zeigerart 1/2 Besitzer,
  3 Zeiger, 4 Ref-Zeiger; Bits 12–15 Sammlung 1 Array, 5 Collection, 6 String), bei Array `u32` Anzahl, bei typedef ein
  Untereintrag, bei eingebetteter Klasse ihr Verweis.
- Gelesen bis aufs Endzeichen: Revit 2017 3879, 2018 4024, 2025 4600 Klassen (12 296 Felder). Beispiele: `Element`
  (`m_id`, `m_famId`, Parameterwert-Tabellen …), `FamilySymbol` (u. a. `m_arrConnectorData`), `Connector`, `XYZ` = Array
  aus 3 double.
- **Die Tags stehen in den Partitionen vor den Objekten** (`Partitions/0` beginnt mit `ElementHeader` 0x5c7,
  `ElementParents` 0x5d3, `FillPatternElem` 0x7d8) – das Schema ist damit der Schlüssel zur Objektebene.
- Test `RevitDateiTests` 4/4. **Noch offen:** das Format der Objektdaten selbst (Reihenfolge und Kodierung der Feldwerte,
  Zeiger, Sammlungen) – nächster Schritt: Objekt-Lesecode in `Utility.dll` dekompilieren (`AArchive::LoadModel`,
  Feld-Serialisierer) und `Global/ElemTable` (Elementliste, Einträge zu 40 B mit Element-Id) auslegen.

**Objektdaten – gebaut 03.11.2026** (`src/Hlks.Kern/Unterlagen/RevitObjekte.cs`, Commit a447ea5 und Nachtrag):
- Partition (entpackt) = Abschnitte im Wechsel: **Köpfe** `[Id][u32 Länge][Daten][u32 Länge]` (je Element ein
  `ElementHeader`) und **Körper** `[Id][u32 Adler32][u32 Länge][Daten][u32 Länge]` (je Element das Objekt selbst und
  seine Geometrie `GElement` bzw. ein Platzhalter `SerializedDummy`). Id u64, in Revit 2017 und älter u32.
- Objekt: `u16` Klassen-Tag, Felder der Elternklassen, dann die eigenen. Grundtypen roh; Array feste Anzahl; Collection
  und String `u32` Anzahl + Elemente; eingebettete Klasse ohne Tag; typedef = Untertyp samt eigener Sammlung.
- Zeiger: `u32`, 0 = leer. Besitzer- und Ref-Zeiger: danach `u16` Klassen-Tag, der Inhalt folgt **erst nach dem
  Hauptobjekt** (Warteschlange, in Revits `LoadModel` sichtbar); der Zeigerwert ist −1 (neuere) bzw. die Objektnummer
  (ältere Dateien). Verweis-Zeiger (Art 3): nur die Nummer.
- Prüfungen: Satzlänge muss exakt aufgehen; Adler32 je Körper stimmt; der Kopf (`ElementHeader.m_classDef`) nennt in
  allen Fällen die Klasse des Körpers. Schema ohne Upgrade-GUIDs in Revit 2012 wird erkannt.
- Stichprobe 48 Dateien (40 RFA, 8 RVT bis 118 MB, Revit 2012–2025): 4 152 176 Sätze vollständig, 1543 ES, 0 Fehler.
- **Offen:** Extensible Storage (`ESEntity.m_blob` mit Schema-GUID; Aufbau in `ESSchema`-Elementen der Datei – lesbar,
  aber eigener Baustein); die fachliche Auslegung der Felder (Leitungen, Formteile, Anschlüsse, Parameterwerte) und
  der Geometrie (`GElement`, `GeomTable`).

**Objektdaten – erster Befund vor dem Bau (zur Nachvollziehbarkeit):**
- Ein Modell (z. B. ein Element in einer Partition) ist gerahmt: `u32` Adler32-Prüfsumme, `u32` Bytezahl, Daten, `u32`
  Bytezahl nochmals (Versions­abhängig; Funktion `0x1801b5ce0`).
- Objekt: Klassenverweis wie im Schema (`u16` Tag), dann die Felder **erst der Elternklasse, dann der eigenen** in
  Schemareihenfolge (`0x1801b1b20`). Aufeinanderfolgende einfache Felder liest Revit als einen rohen Block.
- Zeigerfelder: je `u32` – 0 = leer, −1 = neues Objekt folgt inline (mit Klassenverweis), sonst Verweis auf ein schon
  gelesenes Objekt (`0x1801c5b30`).
- Sammlungen (Collection, String, Map, Set …) liest jeweils ihr eigener **Container-Manager** (virtuelle Funktion je
  Sammlungsart) – Anzahl und Kodierung sind je Art verschieden, ebenso eigene Serialisierer für einzelne Grundtypen.
  Das ist der aufwendige Teil: Es gibt viele Sammlungsarten, jede muss einzeln dekompiliert und geprüft werden; dazu
  mehrere Formatversionen (Schwellen 6, 11, 13, 14, 15, 18, 20, 25 im Code).
- Einschätzung: ein Objektleser, der beliebige Elemente mit ihren Feldwerten liefert, ist eine Arbeit von **mehreren
  Tagen bis Wochen** mit offenem Ende (je Sammlungsart Dekompilieren und Gegenprüfen). Geometrie bleibt auch danach
  Revits eigene Darstellung (GeomTable/GNode-Klassen) und bräuchte nochmals eigene Arbeit.

**Stufe 2 – generischer Objektleser (Einschätzung vor Beginn):** Schema aus `Formats/Latest` auslegen und damit `Global/ElemTable` und die
Partitionen in Objekte mit Klassen- und Feldnamen zerlegen. Mehrere Wochen, Ergebnis unsicher; das Schema ändert mit
jeder Revit-Version, ist aber in jeder Datei mitgespeichert (das senkt das Risiko). Weitere Dekompilierung der
Archivklassen in `Utility.dll` und `RevitDB.dll` wäre dafür nötig.

**Stufe 3 – Geometrie und Systeme:** nicht empfohlen. Selbst mit allen Feldern fehlen Geometrie-Darstellung und
Regeneration.

## Alternativen

| Weg | Vorteil | Nachteil |
|---|---|---|
| **Revit-Add-in** (Revit-API), exportiert Elemente, Connectors, Systeme, Geometrie in ein KICAD-JSON | vollständig und verlässlich; Erfahrung mit eigenen Add-ins vorhanden (HK_LeitungsImport, HK-AI-BIM) | braucht lizenziertes Revit; Revit 2026 ist auf diesem Rechner laut Wissensspeicher nicht lizenziert |
| **Design Automation for Revit** (Autodesk-Cloud) | dasselbe ohne lokales Revit, stapelweise | Daten in der Autodesk-Cloud, Kosten nicht geprüft |
| **IFC-Export** (heutiger Weg) | offen, KICAD kann es schon | Verluste je nach Export, keine Familienparametrik |
| **ODA BimRv SDK** | liest RVT/RFA inkl. Geometrie ohne Revit | kostenpflichtige Mitgliedschaft, Umfang/Preis nicht geprüft |

## Export nach Revit

RVT/RFA selbst schreiben ist nicht realistisch: Revit erwartet Prüfdaten je Block (Fehlerkorrektur), Schema,
Element-Tabellen und regeneriert die Familien; eine fehlerhafte Datei lehnt es ab oder beschädigt das Modell. Gangbar
ist ein **Revit-Add-in, das KICAD-Daten (JSON) liest und die Elemente über die Revit-API anlegt** (Rohre, Kanäle,
Formteile, Familieninstanzen, Parameter) – nach dem Muster von HK_LeitungsImport (`.hkleit.json`). Revit speichert dann
selbst eine gültige Datei. Für fremde Empfänger bleibt der IFC-Export von KICAD.

## Lizenzlage – offene Fragen an Christian (keine Rechtsberatung)

1. Die Autodesk-Lizenzbedingungen untersagen nach allgemeinem Verständnis Reverse Engineering, soweit das anwendbare
   Recht es nicht ausdrücklich erlaubt. Der Wortlaut der für H+K geltenden Bedingungen ist nicht geprüft.
2. Art. 21 URG erlaubt nach seinem Grundgedanken Berechtigten, Schnittstelleninformationen durch Entschlüsseln eines
   Programms zu beschaffen, um unabhängige, interoperable Programme zu schaffen – mit Einschränkungen zur Verwendung.
   Ob das hier greift, wäre juristisch zu klären.
3. Auf Christians Auftrag wurde `Utility.dll` (Speicher- und Leseschicht) am 03.11.2026 dekompiliert, nur zum Zweck
   der Lesbarkeit eigener Dateien. Vor einem Einsatz in KICAD bzw. vor Stufe 2 sollte die Frage geklärt sein.

## Offene Punkte

- Entscheid Christian: Stufe 1 in KICAD umsetzen? Stufe 2 angehen? Add-in-Export für Geometrie?
- Blockparameter für sehr grosse Ströme (andere Stufe der Tabelle) an einer grossen RVT bestätigen.
- Bedeutung der Vorspänne (8 B, 32 B, 44 B); ältere BasicFileInfo-Varianten.
- Aufbau des Klassenschemas in `Formats/Latest` – Voraussetzung für Stufe 2.

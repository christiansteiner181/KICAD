# Projektbrowser: umschaltbare Gliederung – Spezifikation

Stand 19.10.2026 · Hauptlauf

## Anlass und Entscheide

Christian, 19.10.2026: «Den Projektbrowser Gliederung umschaltbar machen (Gebäude, Sanitär, Heizung, Lüftung,
Koordination, Energie und Umwelt). Jeder Nutzer soll einstellen können, was er primär sieht.»
Vorbild Revit: Browser-Organisation (Ansichten nach Disziplin usw.). Heute: ein fester Baum (Ansichten, Ansichtsvorlagen,
Pläne nach Gewerk, Gebäude, Hülle, Systeme, Unterlagen, Familien; `js/oberflaeche/projektbrowser.js`).

| Frage | Entscheid Christian (Fragefenster 19.10.2026) |
| --- | --- |
| Inhalt einer Fachgliederung | **Fachinhalt zuerst, Rest eingeklappt** unter «Weitere» – nichts verschwindet ganz |
| Speicherung | **je Benutzer, überall** (alle Projekte, alle Rechner des Benutzers, wie «zuletzt geöffnet») |
| Automatik | **nur von Hand** (kein Wechsel mit der aktiven Ansicht) |

## Gliederungen

Auswahlfeld oben im Projektbrowser (Symbol + Name, wie Revit «Browser-Organisation»); die gewählte ist die **primäre** des
Benutzers und gilt beim nächsten Öffnen jedes Projekts. Zahlen hinter den Ästen; Suche durchsucht die ganze Gliederung
samt «Weitere» und klappt Treffer auf. Öffnen/Auswählen/Kontextmenü wie heute.

| Gliederung | Äste in dieser Reihenfolge | Unter «Weitere» |
| --- | --- | --- |
| **Gebäude** | wie heute: Ansichten, Ansichtsvorlagen, Pläne, Gebäude (Gebäude → Geschoss → Räume, nicht platzierte Räume), Hülle, Systeme, Unterlagen, Familien | – |
| **Sanitär**, **Heizung**, **Lüftung** | Ansichten des Gewerks (Grundrisse, Schnitte, 3D, Schemas, Listen mit diesem Gewerk bzw. seinen Systemen); Pläne des Gewerks (`Planblatt.gewerk` bzw. Vorschlag, P1); Anlagen → Systeme → Stränge des Gewerks; Familien des Gewerks (Kategorie → Familie → Typ, mit Bibliotheksfamilien nach Auswahl, C3); Räume nach Geschoss | Ansichten/Pläne/Systeme/Familien der anderen Gewerke, Hülle, Unterlagen, Vorlagen, Katalog |
| **Koordination** | Kollisionen (nach Status, darunter Paar bzw. Geschoss), Aussparungen (nach Geschoss, darunter Status), Typenliste, Unterlagen (IFC, BCF), Koordinations- und Aussparungs-, Einlage- und Grundleitungspläne bzw. -ansichten | Fachäste, Hülle, Familien, Vorlagen |
| **Energie und Umwelt** | Hülle (Materialien, Aufbauten, Wärmebrücken), Gebäude → Geschoss → Räume bzw. Zonen (mit Angrenzung), Ansichten mit Darstellung «Hülle», Listen Raumgrenzen und Hüllbauteile; später Ergebnisse der Module 10a/10b (Simulation, Energie) | Fachäste, Unterlagen, Familien, Vorlagen |

- **Zuordnung zum Gewerk:** Klasse → `gewerk` aus dem Katalog; System über Systemtyp bzw. Medium; Ansicht über `gewerke`
  bzw. die Systeme der Ansicht; Planblatt über `gewerk` (bzw. Vorschlag); Familie über ihre Kategorie (Klasse). Elemente
  ohne Gewerk erscheinen unter «Weitere».
- Die Gliederungen sind in **einer** Tabelle beschrieben (Ast-Definitionen), damit später weitere (Kälte, Elektro/MSRL)
  ohne neuen Code dazukommen.
- Der Aufklapp-Zustand wird je Gliederung gemerkt (wie heute `browserOffen`).

## Farben der Gewerke (Christian 19.10.2026)

«Die Gewerke sollen im Explorer mit der Koordinationsfarbe hinterlegt sein (pastel).» → `kataloge/gewerke.json` (Hauptlauf):
je Gewerk Koordinationsfarbe (aus den H+K-Koordinationsplänen: Heizung rot, Lüftung blau, Sanitär grün, Architektur grau;
Kälte Cyan, Elektro Gelb-Orange, MSRL Violett, Energie Orange vom Hauptlauf vorgeschlagen, von Christian bestätigt: «passt
so») und Pastellton. Gewerk-Äste bzw. Gruppenköpfe (Pläne, Systeme, Familien, Fachäste,
«Weitere», Auswahlfeld) im Pastellton hinterlegt, schmaler Streifen links in der Koordinationsfarbe; dunkles Design als
Tönung (`dunkelDeckkraft`). Dieselben Koordinationsfarben gelten später für den Koordinationsplan (Modul 7).

## Speicherung je Benutzer

- **Benutzereinstellungen** wie die Liste «zuletzt» aus S1: `%OneDrive%\HLKS-CAD\einstellungen\<Rechner>.json`, je Rechner
  eine Datei, Vereinigung nach Zeit je Schlüssel (der neueste Wert gilt); ohne OneDrive `%APPDATA%\HLKS-CAD\einstellungen.json`.
  Umleitung für Tests: `HLKS_BENUTZER` (Ordner).
- Endpunkte `GET /api/benutzer/einstellungen`, `POST /api/benutzer/einstellungen {schluessel, wert}`; Schlüssel
  `projektbrowser.gliederung` (gebaeude | sanitaer | heizung | lueftung | koordination | energie). Allgemein genug für
  weitere Benutzereinstellungen (z. B. Design hell/dunkel, Menüband minimiert – nicht in diesem Auftrag umstellen).
- Gemeinsamen Code mit `ZuletztListe` (S1) nutzen (Ablage je Rechner, Vereinigung), nicht kopieren.

## Zuteilung

| Teil | Inhalt | Wer |
| --- | --- | --- |
| A | Spezifikation | Hauptlauf (erledigt 19.10.2026) |
| G1 | Gliederungen (Tabelle der Äste), Auswahlfeld, «Weitere», Suche, Benutzereinstellungen (Server + Oberfläche), Tests, Bilder | hlks-modul |

**Fertig, wenn …** Christian im Projektbrowser «Sanitär» wählt, nur Sanitär-Ansichten, -Pläne, -Systeme und -Familien
oben sieht und den Rest unter «Weitere» findet; die Wahl im nächsten Projekt und auf dem anderen Rechner wieder gilt; ein
anderer Benutzer seine eigene Gliederung behält.

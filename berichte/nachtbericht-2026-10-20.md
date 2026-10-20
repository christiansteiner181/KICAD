# KICAD – Nachtbericht 19./20.10.2026

Auftrag Christian, 19.10.2026, 22:50: «Beginne danach mit den Folgemodulen. Arbeite bis 08:00 durch.»
Alle Zuteilungen mit Ergebnis stehen in `zuteilung.md` (Zeilen 19./20.10.2026).

## Auf dem Desktop

**Geprüfter Stand da7724b (20.10.2026, 07:08)** – mit Schutz gegen Fremdaufrufe; läuft auf dem Desktop.

Das Symbol «KICAD» startet den letzten geprüften Stand (Zweig `geprueft`); ein Klick holt einen neueren und baut dabei die
Bedienungsanleitung neu. Jeder geprüfte Stand: Kern-, MCP-, Web- und Node-Tests grün (`werkzeuge/stand_pruefen.ps1`) und alle
Browser-Reihen ausser HKAP grün (Port 5098 tabu).

## Gebaut und geprüft

| Teil | Ergebnis | Bericht |
| --- | --- | --- |
| W1/W2 Fenster wie Revit | Dialoge verschieben/einklappen, Paletten andocken/Reiter/schwebend, Ansichten frei anordnen, eigenes Fenster | `zuteilung.md` |
| Hilfe «?» und F1 | öffnet die Anleitung, die KICAD selbst ausliefert (`/hilfe/`); F1 über einem Knopf springt zur Funktion | `OBERFLAECHE.md` «Hilfe (F1)» |
| Bedienungsanleitung | 273 Funktionen in 14 Kapiteln, 259 mit Bild; ✓/✗ und Notizen speichert KICAD in `OneDrive\HLKS-CAD\anleitung` | `berichte/anleitung/` |
| N3 fehlende Bedienorte | Einlage-/Grundleitungsplan, Projektinformationen bearbeitbar, Kollisions-/Aussparungsliste, CSV, KS nach Register | `nachbearbeitung.md` |
| S Symbole im Revit-Stil | über 150 eigene Symbole (je 32 und 16 px), zur Probe zuschaltbar (Ansicht › Fenster › Paletten) | Kontaktbogen `%USERPROFILE%\HLKS_CAD_kontaktbogen\kontaktbogen.html` |
| **Modul 3c KI-Palette** | Gespräch mit Claude über das Abo, Änderungen mit «Zurücknehmen», Normensuche mit Zitat, Verlauf je Projekt; Abnahmelauf mit echtem Claude bestanden (Schnittstelle und Oberfläche) | `berichte/modul-3c-testbericht.md` |
| **Modul 4 Teil A** | Belastungswerte LU/DU nach deinem Excel, Summen im Netz, Liste «Sanitär-Zusammenstellung (W3)», Befunde P21–P26; unabhängige Prüfung «abnahmefähig mit Auflagen», Auflagen umgesetzt | `berichte/modul-4a-testbericht.md`, `modul-4a-pruefbericht.md` |
| **Modul 7 Teil A** | Planblatt wie H+K: Plankopf mit Bolligen und Thun, Revisionen und Planänderungen, Plannummern-Vorschlag, P27, Revisionsstempel, Schema-Kopf, Listen auf Planblättern | `berichte/modul-7a-testbericht.md` |
| **Modul 7 Teil B1** | Koordinationsplan in Gewerkefarben, Aussparungsboxen «L / WD 44 x24 / BR +2.18» in Fachplänen (Option), Schnittmarken mit Blattverweis «Schnitt Lüftung A / L03»; vorerst über die Eigenschaften der Ansicht bedient | `OBERFLAECHE.md`, `zuteilung.md` |

## Sicherheit der KI-Palette (unabhängige Prüfung 06:29)

Urteil **mit Auflagen** (`berichte/modul-3c-sicherheitspruefung.md`). Behoben bis 06:48: **Schutz gegen Fremdaufrufe** auf alle
Schnittstellen (vorher konnte eine fremde Webseite im Browser Anfragen an KICAD auf diesem Rechner schicken – betraf alle
Schnittstellen seit Beginn, mit Claude erst gefährlich), Gesprächsprotokoll ohne Normtexte, Claude darf keine Revisionen und
Plansatz-Einstellungen mehr setzen. **Internet-Schalter der Palette vorläufig gesperrt**, bis die übrigen Auflagen (B3/B4/B6:
Projektdateien über Pfade lesbar, UNC-Pfade, Nachweis mit echtem Claude) erledigt sind.

## Deine Entscheide (Reihenfolge nach Dringlichkeit)

1. **Fach K:** in deinen Plänen Koordination (K01–K06), in KICAD bisher Kälte. Was gilt?
2. **3c-Annahmen:** Palette rechts andockbar; Kontext (Ansicht, Geschoss, Auswahl) automatisch, abwählbar; Verlauf je Projekt;
   Modell wählbar, Vorgabe Sonnet; Internet aus – und mit Internet **ohne Projektzugriff und ohne Projektkontext**; Verbotsliste
   strenger als die Spezifikation (keine Statuswechsel, kein BCF, keine Plan-/IFC-Ausgaben durch Claude); deine Auswahl wird für
   Claude freigegeben.
3. **Modul-4-Annahmen:** Kurvenwahl «grösster LU > 3 → Kurve 5» (aus HKAP, nicht im Excel); Kaltwasser vor dem Wassererwärmer
   trägt auch ΣLU WW; Vorgabe-Apparateart je Klasse (WC 7.5 l, Waschmaschine bis 6 kg …); Beispielprojekt K = 0.5 (fiktiv).
4. **Symbole:** Kontaktbogen ansehen – ersetzen, nachbessern oder bei den alten bleiben.
5. **Plankopf:** Titelband «Planinhalt» als Vorgabe aus (wie deine Pläne); Stempel liegt über der Mitte des Kopfs.
6. **Plandarstellung:** Blattverweis an Schnittmarken mit Plankürzel («L03») statt voller Plannummer; Box-Zeile «Gewerk» als
   Spartenkürzel (S, H, L).

## Was ich brauche

- **Normen SN 592000, SN EN 12056-1…5 und SVGW W3** (Ablageort oder Beschaffung): ohne sie keine Dimensionierung in Modul 4
  (Teil B); auch Modul 5 und 6 brauchen Heizlast- und Lüftungsnormen.
- Abnahmen: 3 (endgültig nach Vorführung Strangschema), 3b, 3c, 4A, 7A – je mit Prüfschritten im Testbericht; die Häkchen in der
  Anleitung lese ich.

## Bemerkt

- Echter Plan «S Strangschema Abwasser» der Bernstrasse trägt die Plannummer «1-0759-4» (sonst 1-0768) – Tippfehler?
- Das Excel «LU_DU Zusammenstellung_.xlsx» heisst im Blatt «MFHA» (nicht «Tabelle1»); Datei-Eigenschaften: erstellt
  «Hauptbenutzer», zuletzt geändert «daniele gulizia».
- Normenverzeichnis: 315 deutsche Normen gefunden, Volltext beim ersten Lauf erst für 62 (füllt sich im Hintergrund).

# 2D-CAD in KICAD («Zeichnung») – Konzept

Stand 10.11.2026, Entwurf. Auftrag Christian 10.11.2026: «ich brauche ein 2d CAD wie Autodesk CAD LT es soll DWG öffnen
und exportieren können» · «es soll eine MCP Schnittstelle haben». Gewählt: **KICAD erweitern**. Zweck (alle vier):
Architektenpläne ansehen und messen · fremde DWG bearbeiten und als DWG zurückgeben · eigene 2D-Pläne zeichnen ·
AutoCAD LT ablösen.

## 1 Befund

- **Lesen gibt es, aber nur als Unterlage.** `Grundlagen/CadLeser.cs` liest DWG/DXF mit ACadSharp 3.8.0 und zerlegt den
  Modellbereich in sechs Primitive (Linie, Polylinie, Bogen, Kreis, Text). Blöcke, Bemassungen und Schraffuren werden
  dabei aufgelöst, Layouts, Stile, Linientypen und Farben fallen weg. Zum Hinterlegen richtig, zum Bearbeiten und
  Zurückgeben unbrauchbar.
- **Schreiben gibt es noch nicht** (kein `DwgWriter` in `src/`; DXF-Schreiben nur in Testhilfen).
- **Probe 10.11.2026** (ACadSharp 3.8.0, lesen → als DWG schreiben → wieder lesen, drei echte Pläne aus
  `~/HLKS_CAD_testdaten`): `architekt_eg.dwg` (AC1024, 6664 Objekte, 272 Blöcke, 2 Layouts), `sanitaer_eg.dwg` (AC1032,
  4444 Objekte, 3 Layouts), `440_A10 2. Untergeschoss.dwg` (AC1024, 3961 Objekte). Ergebnis: Anzahl je Objektart, Layer,
  Blöcke und Layouts **vorher und nachher gleich**, keine Schreibmeldung. Beim Lesen als unbekannt gemeldet: `SUN`,
  Stile für Detail-/Schnittansichten (nicht zeichnungsrelevant).
- **Nicht geprüft:** ob AutoCAD die zurückgeschriebene Datei fehlerfrei öffnet, und ob alle Eigenschaften (Bemassungsstil,
  Schraffurmuster, Plotangaben) erhalten sind. Gleiche Anzahl beweist das nicht. → Stufe 0.

## 2 Grundsatz

1. **Die DWG bleibt die Zeichnung.** Eine Zeichnung wird nicht in KICAD-Elemente übersetzt. Im Speicher liegt das
   vollständige DWG-Dokument (Layer, Linientypen, Text- und Bemassungsstile, Blöcke, Layouts, Ansichtsfenster); Befehle
   ändern einzelne Objekte über ihre Kennung (Handle). Was KICAD nicht versteht, wird unverändert mitgeschrieben – nie
   stillschweigend entfernt. Beim Öffnen und beim Speichern steht in einem Bericht, was nicht dargestellt bzw. nicht
   erhalten werden kann.
2. **Eine Tür** wie im Kern: jede Funktion ist ein Befehl (`Zeichnung.Ausfuehren`) oder eine Abfrage. Oberfläche,
   Befehlszeile, MCP und Skripte benutzen dieselbe Tür. Keine Logik in der Oberfläche.
3. **Eigenes Dokument neben dem Modell.** Eine Zeichnung ist eine Datei (`.dwg`), unabhängig von einem KICAD-Projekt
   öffenbar. Im Projekt erscheint sie im Projektbrowser unter «Zeichnungen» und kann als Unterlage dienen.
4. **Speichern wie in AutoCAD:** Datei wird ersetzt, die vorherige Fassung bleibt als `.bak`; eine Sperrdatei zeigt, wer
   sie offen hat. Das weicht bewusst vom Paket-Grundsatz des Modells ab (Leitprinzip 6), weil das Gegenüber eine Datei
   erwartet. Rückgängig/Wiederholen je Sitzung über das Befehlsjournal.

## 3 Aufbau

```
src/Hlks.Kern/Zeichnung/
  ZeichnungDokument.cs     geöffnete Zeichnung (ACadSharp-Dokument), Öffnen, Speichern (DWG/DXF), Bericht
  ZeichnungBefehle*.cs     zeichnen, ändern, Layer, Blöcke, Bemassung, Layout – je Befehl Umkehrung fürs Rückgängig
  ZeichnungAbfragen.cs     Objekte suchen (Fenster, Layer, Art, Text), Eigenschaften, messen
  ZeichnungGrafik.cs       Darstellung als Zeichenanweisungen (für Bildschirm, PDF, PNG) inkl. Linientyp, Stärke, Farbe
  ZeichnungGeometrie.cs    Schneiden, Stutzen, Dehnen, Versetzen, Abrunden, Fangpunkte
src/Hlks.Mcp/WerkzeugeZeichnung.cs   MCP-Gruppe «zeichnung»
src/Hlks.Web/…                       Zeichenfläche, Befehlszeile, Layerpalette, Eigenschaften
```

`CadLeser` (Unterlagen) bleibt, wie er ist.

## 4 Stufen

Jede Stufe ist für sich benutzbar und wird einzeln abgenommen.

**Stufe 0 – Tragfähigkeit (kurz, vor allem anderen).** Zehn typische Büro-DWG (Architekt, eigene AutoCAD-Pläne, mit
XRef, mit Plankopf-Layout) lesen, unverändert schreiben, in AutoCAD LT öffnen und mit «Prüfung» kontrollieren.
Fertig, wenn AutoCAD alle ohne Wiederherstellung öffnet und Christian am Bildschirm keinen Unterschied sieht. Scheitert
das: Rückfall auf den ODA-Weg oder Abbruch – vor weiterem Aufwand.

**Stufe 1 – Ansehen, messen, drucken.** DWG/DXF öffnen · Modellbereich und Layouts · Layer ein/aus/sperren,
Layerzustände · Zoom, Pan, Zoom Grenzen · Linientypen, Linienstärken, Farben, Schraffuren, Texte, Bemassungen,
Blöcke mit Attributen, XRef, Bilder, Abdeckungen · Objektfang · Abstand, Fläche, Winkel, Koordinate · Eigenschaften
eines Objekts · PDF massstäblich (Fenster, Grenzen, Layout).
Fertig, wenn die drei Probepläne am Bildschirm und im PDF dem AutoCAD-Ausdruck entsprechen.

**Stufe 2 – Zeichnen, ändern, speichern.** Linie, Polylinie, Rechteck, Kreis, Bogen, Ellipse, Text, Absatztext,
Schraffur · Verschieben, Kopieren, Drehen, Spiegeln, Skalieren, Versetzen, Stutzen, Dehnen, Abrunden, Fasen, Strecken,
Bruch, Verbinden, Ursprung, Löschen, Reihe · Griffe · Eigenschaften übertragen · Layer anlegen/ändern ·
**Befehlszeile mit AutoCAD-Kürzeln** (L, PL, C, M, CO, RO, MI, TR, EX, O, F …), Koordinateneingabe (`x,y`, `@dx,dy`,
`@l<w`), Ortho, Polar, Objektfang-Spur · Speichern als DWG (2004, 2010, 2013, 2018) und DXF · `.bak`, Sperrdatei.
Fertig, wenn Christian einen fremden Plan ändert, speichert und der Empfänger ihn in AutoCAD ohne Meldung öffnet.

**Stufe 3 – Blöcke, Bemassung, Layouts.** Block einfügen, erstellen, im Block bearbeiten, Attribute · Bemassung
linear, ausgerichtet, Kette, Basis, Radius, Durchmesser, Winkel mit Bemassungsstilen · Führungslinie mit Text ·
Layouts mit Ansichtsfenstern und Massstab · Plotstil (CTB) · XRef zuordnen, lösen, binden · Bild und PDF hinterlegen ·
Zeichnungsreihenfolge · Vorlagen (`.dwt`).
Fertig, wenn ein Planblatt mit Plankopf als Layout gezeichnet, geplottet und als DWG abgegeben ist.

**Stufe 4 – Ablösung AutoCAD LT.** Abgleich mit der Liste der im Büro wirklich benutzten LT-Funktionen (liefert
Christian bzw. das Team) · H+K-Layerstandard und Vorlage · HLKS-Symbole als Blockbibliothek · Brücke zum Modell:
KICAD-Ansicht als Zeichnung ausgeben (deckt den DWG-Teil von Modul 7), Zeichnung als Unterlage ins Projekt.
Fertig, wenn ein Mitarbeiter eine Woche ohne AutoCAD LT arbeitet und die Mängelliste leer ist.

## 5 MCP-Schnittstelle

Gruppe «zeichnung» im bestehenden `Hlks.Mcp` (stdio), dieselben Befehle wie die Oberfläche. Ab Stufe 1 vorhanden,
wächst mit jeder Stufe.

| Werkzeug | Zweck | Stufe |
|---|---|---|
| `zeichnung_oeffnen`, `zeichnung_schliessen`, `zeichnungen` | Datei öffnen (Bericht: Fassung, Einheit, Layer, Layouts, nicht Dargestelltes) | 1 |
| `zeichnung_layer`, `zeichnung_layer_setzen` | Layer lesen, schalten, anlegen, ändern | 1 / 2 |
| `zeichnung_objekte` | Objekte suchen nach Fenster, Layer, Art, Text, Block; Antwort mit Kennung und Eigenschaften, seitenweise | 1 |
| `zeichnung_messen` | Abstand, Fläche, Länge, Winkel | 1 |
| `zeichnung_bild` | Ausschnitt als PNG – damit die KI sieht, was sie tut | 1 |
| `zeichnung_pdf` | Fenster oder Layout als Vektor-PDF | 1 |
| `zeichnung_zeichnen` | Objekte anlegen (Liste, ein Rückgängig-Schritt) | 2 |
| `zeichnung_aendern` | verschieben, kopieren, drehen, spiegeln, skalieren, versetzen, stutzen, dehnen, abrunden, löschen, Eigenschaften | 2 |
| `zeichnung_befehl` | eine Befehlszeile wie von Hand (`L 0,0 @500<90`) | 2 |
| `zeichnung_rueckgaengig`, `zeichnung_wiederholen` | Befehlsjournal | 2 |
| `zeichnung_speichern` | DWG/DXF, Fassung wählbar; Bericht, was nicht erhalten blieb | 2 |
| `zeichnung_block_*`, `zeichnung_bemassen`, `zeichnung_layout_*`, `zeichnung_xref_*` | Blöcke, Bemassung, Layouts, XRef | 3 |

Regeln: Kennung = Handle der DWG (bleibt über Speichern erhalten) · Längen in Zeichnungseinheiten, Einheit steht in jeder
Antwort · Fehler als `HlksFehler` mit Vorschlag · die KI arbeitet in derselben Sitzung wie der Benutzer, ihre Änderung
erscheint sofort am Bildschirm und ist ein Rückgängig-Schritt · Dateien nur aus freigegebenen Ordnern (Pfadprüfung wie
bei den Unterlagen), nie ausserhalb.

## 6 Grenzen und Risiken

- **DWG-Treue** ist das Hauptrisiko (Stufe 0). ACadSharp ist eine freie Bibliothek (MIT), kein Autodesk-Produkt; DWG 2007
  kann sie nach meinem Kenntnisstand nicht schreiben (zu prüfen), alle anderen Fassungen ab R14.
- **Nicht vorgesehen:** 3D-Volumenkörper, dynamische Blöcke verändern (anzeigen und erhalten ja), LISP/Makros,
  Objekte von AutoCAD-Aufsätzen (werden erhalten, nicht bearbeitet), Tabellenobjekte und Multi-Führungslinien erst nach
  Bedarf.
- **Schriften:** AutoCAD-Schriften (SHX) dürfen nicht mitgeliefert werden; Ersatzschrift mit gleicher Laufweite, die
  Zuordnung bleibt in der Datei unverändert.
- **Geschwindigkeit:** die Probepläne (4–7 Tausend Objekte) sind unkritisch; bei sehr grossen Plänen (über 100 000
  Objekte) braucht die Zeichenfläche eine beschleunigte Darstellung – erst messen, dann bauen.
- **Tests** nur dort, wo sie Verhalten sichern: Lesen-Schreiben-Lesen je Objektart, Geometrie der Änderungsbefehle,
  Pfadprüfung der MCP-Werkzeuge. Keine Browserreihen aus Routine.

## 7 Stand (10.11.2026)

- **Stufe 0:** sechs Prüfdateien in `%USERPROFILE%\HLKS_CAD_testdaten\dwg_gegenprobe` (je Plan `…_original.dwg` und die von KICAD
  zurückgeschriebene `…_KICAD.dwg`). Gegenprobe in AutoCAD LT durch Christian steht aus; zehn Büro-DWG fehlen noch.
- **Stufe 1, Kern und MCP gebaut:** `src/Hlks.Kern/Zeichnung/` (Datei + Sitzung mit Ordnerfreigabe, Grafik, Geometrie, Abfragen,
  Ausgabe), `src/Hlks.Mcp/ZeichnungWerkzeuge.cs`. Werkzeuge: `zeichnungen`, `zeichnung_oeffnen`, `zeichnung_schliessen`,
  `zeichnung_layer`, `zeichnung_layer_setzen`, `zeichnung_objekte`, `zeichnung_messen`, `zeichnung_bild`, `zeichnung_pdf`.
  Start nur als 2D-CAD: `Hlks.Mcp --zeichnungen <Ordner> …` (ohne `--projekt`); mit Projekt zusätzlich `--zeichnungen`.
  Am Architektenplan EG geprüft: Modellbereich und Layout 840 × 594 mm mit gedrehtem Ansichtsfenster, Plankopf und Legende.
- **Stufe 1, Zeichenfläche gebaut (10.11.2026):** `/zeichnung.html` (Startbildschirm «DWG öffnen …»): Öffnen-Dialog, Zoom/Pan, Layouts,
  Layer schalten, Auswahl mit Eigenschaften, Messen mit Objektfang (Endpunkt, Mittelpunkt, Zentrum), PDF-Dialog. Bedienung: `src/Hlks.Web/OBERFLAECHE.md`.
- **Stufe 1, offen:** Schnittpunkt- und Lotfang · Auswahl durch ein Ansichtsfenster · Abdeckungen (Wipeout) und
  Rasterbilder · externe Referenzen laden · Linienstärken und Linienarten genau statt gerundet · Polylinienbreiten · Plotstil (CTB)
  · AutoCAD-Schriften · Vergleich mit dem AutoCAD-Ausdruck (Abnahme).

- **Stufe 2 Teil 1 gebaut (10.11.2026):** Zeichnen (Linie, Polylinie, Rechteck, Kreis, Bogen, Text), Ändern (Schieben, Kopieren, Drehen, Skalieren,
  Löschen, Eigenschaften), Layer anlegen, Rückgängig/Wiederholen, Neu, Speichern als DWG 2000–2018/DXF mit .bak und Gegenprobe; Befehlszeile mit
  Kürzeln und getippten Punkten, Ortho, Auswahlfenster, Symbole im Stil von AutoCAD. Kern `ZeichnungBearbeiten.cs`, MCP sieben weitere Werkzeuge.
- **Christian 10.11.2026: «zudem müssen alle zeichnungsfunktionen vorhanden sein»** – offen: Spiegeln, Versetzen, Stutzen, Dehnen, Abrunden, Fasen,
  Strecken, Bruch, Verbinden, Reihe, Ursprung, Eigenschaften übertragen, Griffe · Ellipse, Polygon, Spline, Punkt, Schraffur, Absatztext ·
  Bemassung, Führung · Blöcke einfügen/erstellen, Attribute · Schnittpunkt-/Lot-/Tangentenfang, Polarfang.

## 8 Offen bei Christian

1. Zehn Büro-DWG für Stufe 0 (Ordner nennen) und Öffnen der zurückgeschriebenen Dateien in AutoCAD LT.
2. Reihenfolge gegenüber dem laufenden KICAD-Ablauf: einschieben oder nach dem aktuellen Modul?
3. Welche DWG-Fassung als Vorgabe beim Speichern (Vorschlag: wie geöffnet, neu = 2018)?
4. Liste der im Büro benutzten LT-Funktionen für Stufe 4.

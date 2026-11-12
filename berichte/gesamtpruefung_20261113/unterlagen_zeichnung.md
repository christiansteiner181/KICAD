# Gesamtprüfung 13.11.2026 – Bereich unterlagen_zeichnung

Zweig `worktree-agent-af9d68c682f3af72f`, Build-Ordner `C:\Users\chris\HLKS_CAD_build_gp_unterlagen`.

## 1. Kurzfazit

Die Geometrie der Unterlagen (Lage, Kalibrieren, DWG/DXF-Leser, Bildecken) rechnet im Normalfall richtig; die Handbeispiele unten stimmen.
Drei Fehler mit klarer Ursache sind korrigiert: das Kalibrieren verschob Zeichnungen in Landeskoordinaten beim Speichern um bis 25 mm,
das automatische Ausrichten einer DWG fand die Drehung nur auf etwa ein Grad genau, und verdrehte Ansichtsfenster im 2D-CAD lagen am
falschen Ort. Offen bleiben vor allem die Strichlängen in Ansichtsfenstern (PSLTSCALE) und das Spiegeln von Text an einer waagrechten
Achse. **Nicht in der Tiefe geprüft** (nur überflogen oder gar nicht gelesen): `ZeichnungPolyPfad`, `ZeichnungWeitere`, `ZeichnungAbfragen`,
`ZeichnungPlotstil`, `js/zeichnung/zeichnung.js`, `ZeichnungEndpunkte.cs`, die Leser der Herstellerunterlagen (PDF-Tabellen, Netzanalyse,
STEP, HTML, Tabellen, VDI), `RevitDatei/RevitObjekte/RevitSchema`, `BildDatei`. `UnterlagenDownload` nur auf die Schutzmassnahmen durchgesehen.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Commit |
|---|---|---|---|---|
| 1 | `Grundlagen/UnterlagenLage.cs`, `UnterlageKalibrieren.cs` | Faktor und Drehung werden mit 9 bzw. 6 Stellen gespeichert, die Verschiebung war aber mit den ungerundeten Werten gerechnet. Weit vom Nullpunkt wirkt der Rundungsrest der Drehung als Versatz. | Neue `UnterlagenLage.GerundetUm`: erst runden, dann die Verschiebung an der Mitte der beiden Passpunkte bestimmen. `ZweiPunkte` selbst bleibt exakt. | `24ae8fe` |
| 2 | `Grundlagen/UnterlageAusrichten.cs` (`Hauptrichtung`) | Der «Feinwert» war das Mittel der Fachmitten (0.5°, 1.5° …) des 1°-Histogramms, nicht der wirklichen Winkel. Liegen alle Linien in einem Fach, kam immer die Fachmitte heraus. | Je Fach zusätzlich Σ Länge × Winkel; Feinwert = längengewichtetes Mittel der wirklichen Winkel über das beste Fach und seine Nachbarn (über 0°/90° hinweg). Drehkandidaten gerundet wie gespeichert. | `5800950` |
| 3 | `Zeichnung/ZeichnungGrafik.cs` (Ansichtsfenster) | Ansichtsmitte (`ViewCenter`, im gedrehten System des Fensters) wurde zusammen mit dem Ziel vor der Drehung abgezogen. Ohne Verdrehung gleich, mit Verdrehung falsch. | Blatt = Mitte + k · (Drehung · (Modell − Ziel) − Ansichtsmitte). | `7c5f3d0` |

Handbeispiele zu den Korrekturen:

1. LV95 in Metern, Passpunkt (2 600 000, 1 200 000) m = 2 860 km vom Ursprung, Drehung 17.1234564° → gespeichert 17.123456°.
   Rest 0.0000004° = 6.98·10⁻⁹ rad × 2.86·10⁹ mm = **20 mm** Versatz beider Passpunkte (grösster Fall 0.0000005° → 25 mm). Nach der Korrektur < 0.01 mm.
2. Modell achsparallel, Zeichnung um 12.4° gedreht: alt Fach 12 → 12.5°, Modell Fach 0 → 0.5°, Drehung −12.0° statt −12.4°;
   0.4° auf 6.1 m = **43 mm** an der Ecke. Neu −12.4°.
3. Verdrehung 90°, Ansichtsmitte (100, 50), Massstab 1, Fenstermitte (200, 200), Ziel (0, 0): Modellpunkt (50, −100) gedreht = (100, 50) =
   Ansichtsmitte → Blatt **(200, 200)**. Alt: Drehung von (50−100, −100−50) = (150, −50) → Blatt (350, 150), ausserhalb des Fensters (100 × 100).

Test: ein Lauf nach allen drei Korrekturen,
`dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~GrundlagenBefehleTests|FullyQualifiedName~UnterlageAusrichtenTests|FullyQualifiedName~ZeichnungTests|FullyQualifiedName~GrundlagenCadTests"`
→ **88 bestanden, 0 Fehler**. Darin drei neue Tests (`Kalibrieren_Landeskoordinaten_PasspunkteLiegenNachDemSpeichernGenau`,
`Ausrichten_KrummerWinkel_TrifftDieWaendeAufZehnMillimeter`, `Darstellung_VerdrehtesAnsichtsfenster_LegtDenModellbereichRichtig`).
Eine Gegenprobe «ohne Korrektur rot» ist nicht gelaufen; dass die neuen Tests den alten Fehler fangen, ist nur gerechnet (Werte oben).
Node-Tests nicht gelaufen (kein JS geändert).

## 3. Nachgerechnet und in Ordnung

- **Lage** (`UnterlagenLage.Auf/Zurueck`, `kalibrierung.js`): Modell = Drehung(Faktor · u) + d; Umkehrung stimmt, Kern und Oberfläche rechnen gleich
  (Spiegelung der Pixelzeile vor Skalierung und Drehung). Beispiel `ZweiPunkte` (1,1),(4,5) → (10,10),(10,20): Länge 5 → 10, Faktor 2,
  Drehung 90° − 53.13° = 36.87°, (1,1) → (10,10), (4,5) → (10,20).
- **Bildecken**: Bild 2000 × 1000 Bildpunkte, 150 dpi, 1:100 → 25.4/150 × 100 = 16.933 mm je Bildpunkt; Lage (16.933, 0°, 0, 16 933):
  Ecke unten links (Pixel 0/1000) → (0, 0), oben rechts (2000/0) → (33 867, 16 933). PDF: mm je Bildpunkt aus der Seitenbreite, nicht aus dem gerundeten dpi.
- **Einheiten** (`CadLeser.MmJe`): mm 1, cm 10, m 1000, Zoll 25.4, Fuss 304.8, US-Vermessungsfuss 1 200 000/3937; ohne INSUNITS Annahme mm mit Hinweis.
- **Blöcke**: Abbildung = OCS · Verschiebung(Einfügepunkt) · Drehung · Reihenversatz · Skalierung · Verschiebung(−Basispunkt); negative Massstäbe
  (Spiegelung) kehren den Umlaufsinn der Bögen um (`Det < 0`), ungleiche Massstäbe machen aus Bögen Polylinien. Normale −z: x gespiegelt
  (Achsenregel von AutoCAD: Ax = (−1, 0, 0)).
- **Ausbuchtung**: r = c(b² + 1)/(4|b|), Pfeil = |b|c/2, Mitte links der Sehne bei b > 0 und |b| < 1; Fläche mit Kreisabschnitt ± je nach Umlaufsinn.
- **Bogen durch drei Punkte**, **Abrunden** (Tangentenlänge r/tan(θ/2), Mitte auf der Winkelhalbierenden im Abstand r/sin(θ/2)), **Versetzen**,
  **Stutzen/Dehnen**, **Verbinden** (Ausbuchtungen beim Umkehren), **Spiegeln einer Blockreferenz** (Drehung 2φ − α, y-Massstab negativ): stimmen.
- **Textlage**: Grundlinie aus Ankerpunkt (Mitte: halbe Höhe, Oben: ganze Höhe unter dem Anker, in Schriftrichtung gedreht); Absatztext 5/3 Zeilenabstand.
- **RevitLeitungen**: Gerade = Ursprung + Richtung × Parameter, Bogen = Mitte + r(cos t · x + sin t · y), Fuss → mm mit 304.8.
- **Download**: nur http/https, keine Anmeldedaten, Adressen des eigenen Netzes auch nach Namensauflösung und Weiterleitung gesperrt, Grössen- und ZIP-Grenzen.

## 4. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 4.1 | `Zeichnung/ZeichnungGrafik.cs` (`Striche`, Faktor `k.T.Skala`) | Strichlängen der Linientypen werden im Ansichtsfenster immer mit dem Fenstermassstab multipliziert. Das entspricht PSLTSCALE = 0. Bei PSLTSCALE = 1 (Vorgabe von AutoCAD) gelten die Strichlängen im Papiermass. | Im Layout-PDF sind gestrichelte Linien um den Fenstermassstab zu fein (bei 1:100 hundertfach) und wirken durchgezogen. | PSLTSCALE aus dem Kopf der Datei lesen; bei 1 den Fenstermassstab aus dem Faktor nehmen. An einer echten DWG mit Layout prüfen. | klein | mittel |
| 4.2 | `Zeichnung/ZeichnungGeoBefehle.cs:188–189` | Spiegeln von Text: Drehung = 2φ − α + 180°. Handbeispiel waagrechte Achse (φ = 0), Text waagrecht (α = 0): neue Drehung **180°**, der Text steht auf dem Kopf. Senkrechte Achse: 0° (richtig). | Der Kommentar «Text bleibt lesbar» gilt nur für steile Achsen. | 2φ − α rechnen und nur dann 180° dazu, wenn der Text sonst auf dem Kopf stünde – oder das Verhalten von AutoCAD an einem Beispiel festlegen (Frage 1). | klein | mittel |
| 4.3 | `Zeichnung/ZeichnungGeoBefehle.cs:190` | Beim Spiegeln einer Blockreferenz bleiben ihre Attribute (eigene Textobjekte) am alten Ort. | Beschriftung eines gespiegelten Symbols steht daneben. | Attribute wie Text mitspiegeln. | klein | tief |
| 4.4 | `Zeichnung/ZeichnungGrafik.cs:52` (`vp.Id != 1`) | Das Blattfenster wird an der Nummer 1 erkannt. Im Test wurde das erste, per Bibliothek angelegte Fenster nach dem DXF-Rundlauf als Ansicht gezeichnet (zwei Linien statt einer). | Ob das bei Dateien aus AutoCAD vorkommt, ist nicht geprüft; wenn ja, liegt der Modellbereich einmal zu viel über dem Blatt. | An einer echten DWG mit Layout prüfen; sonst zusätzlich das erste Fenster des Layouts überspringen. | klein | tief |
| 4.5 | `Plan/PlanSvg.cs:81–88` | Bogen mit von = bis (voller Kreis als Bogen, so liefert ihn `CadLeser.Bogen` bei 360°) ergibt im SVG nichts; PDF und Bildschirm zeichnen den Kreis. | Seltene Vollkreis-Bögen fehlen im SVG. | Wie `bogenPfad` in `plangrafik.js`: bei 0 → 360° zwei Halbbögen. | klein | tief |
| 4.6 | `Grundlagen/CadLeser.cs:212–221`, `ZeichnungGrafik.cs` (`Text`) | Text in gespiegelten Blöcken: Richtung wird gespiegelt, die Ausrichtung (links/rechts) nicht. | Text läuft vom Anker in die falsche Richtung bzw. steht auf dem Kopf. | Bei `Det < 0` Richtung um 180° drehen und links/rechts tauschen. | klein | tief |
| 4.7 | `Grundlagen/UnterlageAusrichten.cs` (`Verfeinern`) | Verfeinert wird nur die Verschiebung, die Drehung bleibt beim Wert der Hauptrichtung. Abstände 100/250/300/80 mm und 30 % sind weiterhin Annahmen des Hauptlaufs. | Bei Gebäuden mit zwei Hauptrichtungen oder wenigen Wandlinien kann die Drehung um Zehntelgrad danebenliegen. | Drehung in die Ausgleichung aufnehmen (Punkt-zu-Kante mit drei Unbekannten). | mittel | tief |
| 4.8 | `Grundlagen/CadLeser.cs:279`, `ZeichnungGrafik.cs` (Ellipse) | Ellipsen mit geneigter Normale werden ohne Zähler «geneigt» projiziert gezeichnet (alle anderen Arten melden es). | Uneinheitlich, kein falsches Mass im Grundriss. | Wie bei Bogen und Kreis zählen. | klein | tief |
| 4.9 | `Grundlagen/UnterlagenLage.cs:24` (`Zurueck`) | Faktor 0 in einer von Hand geänderten `transformation` teilt durch null (NaN im Umriss); die Oberfläche prüft `faktor > 0`, der Kern nicht. | Nur nach `element_aendern` mit unsinnigem Wert. | Im Kalibrieren Faktor ≤ 0 als Fehler melden. | klein | tief |
| 4.10 | `Unterlagen/UnterlagenDownload.cs:61` | Tote Bedingung `a != "ifc" \|\| true` in der Fehlermeldung. | keine | Filter entfernen. | klein | tief |

Nebenbefund ohne Bezug zum Code: jeder `git commit` in dieser Arbeitskopie meldet `failed to delete '.git/worktrees/wt_b1'` und `wt_b2`
(Permission denied). Die Commits sind trotzdem geschrieben; es sind liegengebliebene Einträge anderer Arbeitskopien.

## 5. Offene Fragen an Christian

1. Spiegeln von Text im 2D-CAD (4.2): Soll Text nach dem Spiegeln immer lesbar bleiben (nie auf dem Kopf), auch an einer waagrechten Achse?
2. Gibt es eine DWG aus AutoCAD mit Layout, gestrichelten Linien und einem verdrehten Ansichtsfenster (z. B. Bernstrasse), an der 4.1, 4.4 und
   die Korrektur 3 gegen den Plot aus AutoCAD geprüft werden können? Bisher ist das nur an selbst erzeugten Dateien gerechnet.
3. Automatisches Ausrichten: die Schwellen (30 % Treffer, 80 mm) sind Annahmen des Hauptlaufs – so lassen oder nach Erfahrung mit echten Plänen festlegen?

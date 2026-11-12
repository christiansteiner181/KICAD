# Gesamtprüfung 13.11.2026 – Korrekturen aus der Gegenprüfung (Runde 6)

Zweig `worktree-agent-a9b9fac9615e289e2`, Build-Ordner `C:\Users\chris\HLKS_CAD_build_gp_gegen`. Nichts gepusht, keine Marken.

## 1. Kurzfazit

Alle elf Befunde der Gegenprüfung (B1–B10 und der Dämmdialog) sind korrigiert, je mit eigenem Commit (B2 und B3 gemeinsam, weil
beide dieselbe Stelle in `basis.js` betreffen). Getestet wurde nur gefiltert: Kern 160 + 9 Tests grün, Web 3 grün, Node
`werkzeuge.test.mjs` 54 grün (1 übersprungen, wie vorher). Kein Test war vorher rot. Nicht im Browser geprüft sind die drei
Oberflächenteile (B2, B3, Dämmdialog) und der Betrachter (B5) – dort gibt es nur Syntaxprüfung bzw. Tests der reinen Logik.
Zwei Punkte brauchen einen Blick von Christian bzw. dem Hauptlauf: die Regel für rechtwinklige Wandanschlüsse (B1, bewusst
unverändert) und die Strenge der neuen Lagen-Schreibweise (B8).

## 2. Korrigiert

### B1 – Eckverlängerung mit Abstand zur Achse (`src/Hlks.Kern/Architektur/Wandgeometrie.cs`) – Commit `cc2104d`
- **Fehler:** `Eckverlaengerung` nahm an, das Wandende liege auf der Achse der anderen Wand; `Verlaengerung` greift aber bis
  Dicke/2 + 1 mm. Endet eine schräge Wand auf der Fläche, blieb eine Kerbe im Umriss.
- **Korrektur:** `Verlaengerung` rechnet den vorzeichenbehafteten Weg quer zur anderen Wand, der dem Ende bis zu deren Achse fehlt
  (Richtung +1 am Ende, −1 am Anfang), und gibt ihn mit: e = (dO/2 + abstand − dW/2·|cos|) / |sin|, nie negativ.
- **Nachgerechnet:** Wand o Achse y = 0, 200 mm; Wand w 200 mm, Richtung (0.707, −0.707), Achsende (2000, 100). abstand = 100 →
  e = (100 + 100 − 70.71) / 0.7071 = 182.84 mm; Ecken des Wandendes (2058.6, −100) und (2200, 41.4); Umrissfläche
  5000·200 + 3000·200 = 1.6 m². Vorher e = 41.4 mm: Endkante von (2100, 141.4) nach (1958.6, 0), also oberhalb y = 100 offen.
- **Bewusst unverändert:** Rechtwinklig (cos = 0) bleibt es bei der halben Dicke der anderen Wand (Spezifikation Modul 1a). Das
  Wandende liegt dort in jedem Fall ganz in der anderen Wand, der Umriss ist sauber; mit dem Abstand wüchse `VerlEnde` bei einem
  Ende auf der Fläche auf die ganze Dicke und `FreiBis` (freie Länge für Öffnungen) würde 100 mm kleiner.
- **Test:** `ArchitekturBefehleTests.SchraegerAnschluss_EndeAufDerFlaeche_WandumrissGeschlossen` (neu); bestehender Test der
  Gehrung Achse auf Achse (41.42 mm) bleibt grün. Filter `FullyQualifiedName~ArchitekturBefehleTests`.

### B4 – KI-Pfadprüfung bei Ausgaben (`Baustelle/BaustelleExport.cs`, `IfcExport/IfcExport.cs`) – Commit `6ebd7af`
- **Fehler:** `datei` ging ohne `KiPfade.Pruefen` an `Path.GetFullPath` – die KI konnte an jeden Ort schreiben.
- **Korrektur:** `datei = KiPfade.Pruefen(p, datei, "baustelle_export")` bzw. `"ifc_export"` vor `GetFullPath`; Menschen unverändert.
- **Test:** `KiSicherheitTests.Ausgaben_Ki_BcfExport_AussparungenIfc_PlanPdf_NurUnterDenWurzeln` um beide Abfragen ergänzt
  (Ablehnung mit «Pfad für die KI nicht erlaubt», Ordner «aussen» bleibt leer).

### B6 – Paketkopie vor dem Original (`src/Hlks.Kern/Projekt.cs`) – Commit `a6780e7`
- **Fehler:** Sortiert die Kopie vor dem Original (z. B. «(1) …»), wurde sie als das Paket angewandt und das Original im selben
  Lauf noch einmal.
- **Korrektur:** am Schleifenanfang `if (bekannt.Contains(id)) continue;`. Geprüft: `bekannt` enthält nach der Kopie die Paket-Id,
  die Liste `neu` ist vor der Schleife gefiltert – der Fall trat nur innerhalb eines Laufs auf.
- **Test:** `SpeicherTests.KopieEinesPaketsWirdNichtZweimalAngewandt` ergänzt (Kopie «(1) <id>.json», ein Eintrag im Verlauf,
  gleiche Prüfsumme wie beim Urheber).

### B7 – Vorwand spiegeln ohne «rueckseite» (`src/Hlks.Kern/Architektur/Bearbeiten.cs`) – Commit `acb9356`
- **Fehler:** Fehlte die Eigenschaft, blieb sie beim Spiegeln leer – `VorwandGestell` nimmt dann «links», die Rohbauwand wechselte
  still die Seite.
- **Korrektur:** `(e.Text("rueckseite") ?? "links")` auswerten; ohne Angabe wird «rechts» gesetzt, «keine» bleibt.
- **Test:** `VorwandTests.Spiegeln_RueckseiteBleibtAnDerRohbauwand` ergänzt (Rückseite nach dem Spiegeln an x = 0 gespiegelt, gleiche
  Höhe; «keine» bleibt «keine»).

### B8 – Lagen lesen (`src/Hlks.Kern/Leitungen/Lagen.cs`) – Commit `2d8764a`
- **Fehler:** Jede Ziffernfolge zählte: «Lage 1: 120, Lage 2: 240» ergab [1, 120, 2, 240].
- **Korrektur:** Trennen an Komma, Strichpunkt, Zeilenwechsel; jeder Wert muss als Ganzes passen: wahlweise Bezeichnung davor
  (endet mit «:» oder «=» bzw. mit einem Wort vor Leerzeichen/Vorzeichen, z. B. «1. Lage UKD −12cm»), Vorzeichen, Zahl, Einheit
  mm/cm/m. Unlesbares steht neu in der Abfrage `lagen` je Geschoss im Feld `hinweis` (mit Kennzeichen und dem Wert), statt still zu
  zählen. «12cm» = 120 mm und das Minus als Abstand sind unverändert.
- **Folge:** Werte nur mit Leerzeichen getrennt («120 240 360») gelten jetzt als unlesbar und werden gemeldet; vorher wurden sie
  gelesen. Die Oberfläche (Lagemanager) zeigt das Feld `hinweis` noch nicht an.
- **Test:** `LagenTests.Lagen_EinheitCmUndMinus_WieGeschrieben` ergänzt (drei Schreibweisen → [120, 240]; «Lage 2 240» und
  «zirka 3x» im Hinweis).

### B9 – Pendeln in der Nachführung (`src/Hlks.Kern/Api/Nachfuehrungen.cs`, `Modell/Modell.cs`) – Commit `9eedecc`
- **Fehler:** Regeln, die einander im Kreis auslösen, liefen bis zur Rundengrenze (bis 2000).
- **Korrektur:** Je Runde der Regeln ein Fingerabdruck (Art, Id, Inhalt vorher und nachher ohne Version und Paket). Wiederholt
  sich einer, Abbruch mit Hinweis «… abgebrochen: die Folgeänderungen pendeln (Runde n setzt dasselbe wie Runde m: …)». Runde 0
  (der Befehl selbst, kann ein Import sein) zählt nicht. Die Grenze und das Verhalten an der Grenze sind unverändert.
- **Test:** `RoutingNachfuehrungTests.Pendeln_WirdErkanntUndGemeldet_StattBisZurGrenzeZuRechnen` (neu, 4 statt 500 Aufrufe).
  Der bestehende Test der Rundengrenze brauchte eine Regel mit je Runde neuem Inhalt (Zähler im Namen) – die alte Regel setzte
  immer dasselbe und gilt jetzt zu Recht als Pendeln.

### B10 – «gekürzt» und Meldung für Besucher (`Api/Abfragen.cs`, `Hlks.Web/BaustelleZugaenge.cs`, `BaustelleZugang.cs`) – Commit `50133c1`
- **netz_folgen:** fragt Grenze + 1 Schritte ab; der Hinweis kommt nur, wenn es wirklich mehr als 2000 sind, die Ausgabe bleibt bei
  2000. Kein neuer Test (bräuchte ein Netz mit über 2000 Elementen); `AbfragenTests.NetzFolgen*` grün.
- **Zugänge:** `AusText(text, besucher)` und `FuerBesucher(p)`. Die Seiten unter `/baustelle/` (Link, Übersicht, Seite, Rückmeldung)
  melden nur «Die Zugänge der Baustelle sind zurzeit nicht lesbar.» ohne Dateinamen und JSON-Fehlerstelle; `/api/baustelle/zugaenge`
  (KICAD, localhost) behält die ausführliche Meldung.
- **Test:** `GesamtpruefungOberflaecheTests.Zugaenge_UnlesbareDatei_…` ergänzt.

### B2/B3 – Klick-Reihe und eingereihte Längeneingabe (`wwwroot/js/werkzeuge/basis.js`, `reihe.js`) – Commit `d987ca6`
- **B2:** Neue reine Funktion `klickEinreihen(def, wartetAufBenutzer)` in `reihe.js`: eingereiht wird nur bei `def.mehrpunkt`;
  sonst entfällt der Klick während der laufenden Abfrage wie früher. Betroffen sind damit alle Werkzeuge ohne `mehrpunkt: true`
  (auch Achse und Raumtrennung – deren erster Klick fragt den Kern nicht, es geht dort nichts verloren).
- **B3:** `tippBestaetigen` merkt sich Maus und Shift; läuft beim Enter noch ein Schritt, wird der Fang beim Abarbeiten mit
  `fangFuer(maus, shift)` neu gerechnet, sonst gilt der Fang vom Tastendruck.
- **Test:** `node --test js/werkzeuge/werkzeuge.test.mjs` – neuer Test zu `klickEinreihen`. B3 hat keinen Test (hängt am Browser).
  `OBERFLAECHE.md` nachgeführt.

### B5 – Link-Filter im Betrachter (`src/Hlks.Kern/Baustelle/betrachter.js`) – Commit `4383a13`
- **Korrektur:** Positivliste: Link nur, wenn `new URL(roh, location.href).protocol` http:, https: oder file: ist; sonst Titel als
  Text. Kein Test (läuft nur im Browser); `node --check` und die Kern-Tests `~Baustelle` grün.
- **Folge:** Ein Laufwerkspfad «C:\…» in `datei` ergibt jetzt keinen Link mehr (der Browser liest «c:» als Schema); vorher war
  es ein Link, der nicht funktionierte.

### Dämmdialog (`wwwroot/js/oberflaeche/aktionen-leitung.js`, `werkzeuge/optionen.js`) – Commit `a30e675`
- **Korrektur:** Erste Wahl «– keine Angabe –»; sie ist vorgewählt, wenn die Leitung keine Art hat, und speichert `daemmung: null`.
  Katalogarten und «andere …» bleiben. Gilt für Hinzufügen und Bearbeiten.
- **Test:** `daemmartVorwahl` / `daemmartAusWahl` in `werkzeuge.test.mjs` (liegt aus technischen Gründen im Commit `d987ca6`,
  zusammen mit dem Eintrag in `OBERFLAECHE.md`).

### Testläufe
| Lauf | Filter | Ergebnis |
|---|---|---|
| Kern | ArchitekturBefehleTests, KiSicherheitTests, SpeicherTests, VorwandTests, LagenTests, RoutingNachfuehrungTests | 160 grün |
| Kern | AbfragenTests.NetzFolgen, ~Baustelle | 9 grün |
| Web | GesamtpruefungOberflaecheTests | 3 grün |
| Node | js/werkzeuge/werkzeuge.test.mjs | 54 grün, 1 übersprungen |

## 3. Gefunden, nicht korrigiert

- `src/Hlks.Kern/Architektur/Wandgeometrie.cs` (`FreiVon`/`FreiBis`): Die freie Länge für Öffnungen zieht `VerlAnfang`/`VerlEnde`
  ab und stimmt nur, wenn das Wandende auf der Achse der anderen Wand liegt. Endet die Wand auf der Fläche, liegt nichts von ihr
  in der anderen Wand, es werden trotzdem dO/2 (rechtwinklig) bzw. neu der grössere Wert (schräg) abgezogen. Auswirkung: Öffnung
  nahe am Anschluss wird zu früh abgelehnt. Vorschlag: den Anteil in der anderen Wand getrennt von der Verlängerung führen.
  Aufwand klein–mittel, Priorität tief.
- `src/Hlks.Kern/Architektur/Wandgeometrie.cs` (`Eckverlaengerung`): Ist die andere Wand dünner als dW·|cos| (dicke Wand trifft
  spitz auf dünne), gibt es keine Verlängerung, die zugleich schliesst und nicht vorsteht; die Formel wählt «nicht vorstehen».
  Auswirkung: kleine Kerbe im Umriss. Aufwand mittel, Priorität tief.
- Lagemanager (Oberfläche): zeigt das neue Feld `hinweis` der Abfrage `lagen` nicht an. Aufwand klein, Priorität mittel.
- B9: Der Fingerabdruck vergleicht die Operationen einer Runde, nicht den ganzen Modellstand. Eine Regel, die in zwei Runden
  dieselben Elemente unverändert neu setzt, während sonst nichts geschieht, gilt als Pendeln – das ist gewollt, aber strenger als
  «gleicher Modellstand». Aufwand für die exakte Variante mittel (Prüfsumme je Runde), Priorität tief.

## 4. Offene Fragen an Christian

1. B1: Soll auch der rechtwinklige Anschluss mit Ende auf der Fläche bis zur abgewandten Fläche verlängert werden (heute halbe
   Dicke nach Spezifikation Modul 1a)?
2. B8: Sollen Lagen, die nur mit Leerzeichen getrennt sind («120 240 360»), weiter gelesen werden? Heute werden sie gemeldet.
3. Unverändert offen aus der Gegenprüfung: Minus-Toleranz bei Lagen und ob die Nachführung an der Rundengrenze ablehnt.

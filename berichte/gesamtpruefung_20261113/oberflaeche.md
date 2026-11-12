# Gesamtprüfung KICAD 13.11.2026 – Bereich Oberfläche und Web-Server

Zweig `worktree-agent-aac01dda5bc5c4eba`, Ausgangsstand `3aed5cb`.

## 1. Kurzfazit

Das Aktionsregister ist in Ordnung: 262 Aktionen, kein Zwei-Buchstaben-Kürzel doppelt, kein Knopf im Menüband ohne Symbol oder Gruppe, keine
doppelte Registrierung (geprüft, indem das echte Register in Node geladen wurde). Die Umrechnung Bildschirm ↔ Modell, der Fang in gedrehten
Ansichten und der Polarfang sind nachgerechnet und stimmen. Korrigiert sind sechs kleine Punkte: Reihenfolge der Kürzelliste, drei
Kontextbefehle, die an jedem Bauteil gesperrt standen statt ausgeblendet, und im Web-Server drei Fälle, in denen Daten verloren gingen oder
ein Fehler ohne Meldung blieb. Offen bleiben vor allem Punkte am Baustellen-Zugang im Büronetz (er ist ohne Zugangsdatei für alle offen) und
ein Klick, der während einer laufenden Abfrage verloren geht. Nicht vertieft gelesen: `eigenschaften.js`, `paletten.js`,
`optionsleiste.js`/`optionen.js`, `css/oberflaeche.css` und die Dialoge der Fachmodule (Koordination, Hülle, HKAP, KI, Quest); keine
Browserreihe, kein Server gestartet.

## 2. Korrigiert

| Nr. | Datei | Fehler | Korrektur | Test | Commit |
|---|---|---|---|---|---|
| 1 | `js/oberflaeche/kuerzel-liste.js` | Die Liste «Tastaturkürzel» (KS) stand noch in der Registerfolge vor dem 09.11.2026 (Architektur, Hülle, Heizung …), nicht in der von Christian festgelegten (Datei, Einfügen, Heizung …). | `REGISTER_NAMEN` in der Reihenfolge von `menueband.js` | `node --test js/oberflaeche/nachbearbeitung-n3.test.mjs`: neue Zusicherung «Reihenfolge wie Menüband» grün; 22 von 23 grün, der eine rote war schon vorher rot (siehe 3.1) | `dc1d291` |
| 2 | `js/oberflaeche/aktionen-verbinden.js`, `aktionen-leitung.js` | «Zu T-Stück (+)», «Abzweig entfernen (−)» und «Fliessrichtung ab hier» standen im Kontextregister an jedem Bauteil, meist gesperrt. Entscheid 11.11.2026 «Ja, ausblenden» galt nur für die Leitungsbefehle. | `fuer`: nur am Bogen, nur am T-Stück, nur an Pumpe/Lüftungsgerät | `node --test js/oberflaeche/kontextmenue-logik.test.mjs`: 2 von 2 grün; Register in Node geladen: alle drei haben `fuer`. Kein neuer Test (reine Zuordnung) | `c3ca675` |
| 3 | `BaustelleZugang.cs` | Zwei Rückmeldungen derselben Person in derselben Sekunde bekamen denselben Dateinamen; die zweite überschrieb die erste. Ein sehr langer Name sprengte den Pfad. | `RueckmeldungDatei`: Laufnummer bei vorhandener Datei, Name höchstens 40 Zeichen | `dotnet test tests/Hlks.Web.Tests --filter "FullyQualifiedName~GesamtpruefungOberflaecheTests\|FullyQualifiedName~BaustelleZugangTests"`: 5 von 5 grün | `b503e44` |
| 4 | `BaustelleZugaenge.cs`, `js/oberflaeche/aktionen-baustelle.js` | Unlesbare `baustelle\zugaenge.json` gab eine leere Antwort 500; der Dialog «Zugänge» zeigte dabei «Noch keine Zugänge». Fehlgeschlagenes Entfernen blieb stumm. | `AusText` wirft eine Meldung mit Ausweg (Zugang bleibt gesperrt); der Dialog zeigt den Fehler | wie Nr. 3 | `b503e44` |
| 5 | `Fehlerschutz.cs` (neu), `Program.cs` | Unerwartete Ausnahmen unter `/api` kamen als leere Antwort 500; die Oberfläche zeigte nur «Fehler 500». | Middleware: `{ fehler, vorschlag }` mit 500 und Eintrag im Protokoll; abgebrochene Anfragen und begonnene Antworten unberührt | wie Nr. 3 | `b503e44` |
| 6 | `OBERFLAECHE.md` | – | Nr. 1 und 2 nachgeführt | – | mit dem Bericht |

## 3. Gefunden, nicht korrigiert

### 3.1 Test, der schon vorher rot war
- `js/oberflaeche/nachbearbeitung-n3.test.mjs:397` «Verdrahtung: Aktionen im Register Ansicht › Erstellen …»: Der Test vergleicht den Quelltext
  von `const REGISTER` in `menueband.js` mit der Reihenfolge vor dem 09.11.2026 und ohne «BIM to Field». Der Code ist richtig, der Test veraltet.
  Vorschlag: die Zeile streichen oder auf die Menge der Register umstellen. Aufwand klein, Priorität mittel.

### 3.2 Baustellen-Zugang im Büronetz (`BaustelleZugang.cs`)
- **Ohne Zugangsdatei offen für alle** (`:86` ff., `BaustelleZugaenge.cs:31`): Solange kein Zugang angelegt ist, sieht jeder im Büronetz die
  Webseiten des offenen Projekts und kann Rückmeldungen senden. Steht so im Kopf der Datei («Testserver»), ist aber mit dem Register «BIM to
  Field» jetzt ein Arbeitsweg. Vorschlag: sobald der Zugang eingeschaltet ist, immer Anmeldung verlangen. Aufwand klein, Priorität hoch – Entscheid Christian.
- **Rückmeldungen unbegrenzt** (`:206`): je Anfrage bis 60 MB, beliebig viele, ohne Anmeldung (ohne Zugangsdatei). Füllt den Projektordner.
  Vorschlag: Obergrenze je Tag bzw. Gesamtgrösse des Eingangs. Aufwand klein, Priorität mittel.
- **Schlüssel im Link über HTTP, Cookie ein Jahr, kein Zähler für Fehlversuche** (`:101`): im Büronetz vertretbar, ausserhalb nicht.
  Der Schlüssel hat 12 Zeichen aus 31 (rund 59 Bit), Raten ist unrealistisch. Vor einem Einsatz über das Internet: HTTPS und Ablaufdatum.
  Aufwand mittel, Priorität mittel.
- **`/baustelle/` antwortet auch auf dem localhost-Port ohne Host-Prüfung** (`Herkunftsschutz.cs:38`): `/baustelle` ist nicht geschützt. Eine
  fremde Webseite könnte per DNS-Rebinding die Baustellen-Webseiten lesen, solange es keine Zugangsdatei gibt. Vorschlag: auf dem
  localhost-Port die Host-Prüfung auch für `/baustelle`. Aufwand klein, Priorität mittel.
- **Dialog «Zugänge»** (`aktionen-baustelle.js:49`): Der Hinweis «Die Links zeigen auf den Baustellen-Testserver (Port 5135)» sagt nicht, dass
  dieser Server gestartet sein muss (`werkzeuge\baustelle_testserver.ps1`); sonst führen die Links ins Leere. Der Link nimmt die erste
  IPv4-Adresse des Rechners – mit VPN oder zwei Netzkarten kann das die falsche sein. Aufwand klein, Priorität mittel.

### 3.3 Werkzeuge und Esc (`js/werkzeuge/basis.js`)
- **Klick während einer laufenden Abfrage geht verloren** (`:258` `run`, `:318` `drueck`): Solange ein Klick noch beim Kern rechnet, verwirft
  `run` jeden weiteren Klick ohne Zeichen; die Vorschau läuft weiter. Bei schnellem Zeichnen fehlt dann ein Punkt, und Vorschau und
  Ergebnis weichen ab. Vorschlag: den letzten Klick vormerken und danach ausführen (wie `escWartet`) oder Wartezeiger zeigen. Aufwand klein, Priorität hoch.
- **Esc bei hängender Abfrage** (`:272`): Esc wird vorgemerkt und erst nach der Antwort ausgeführt. `api.js` hat keine Zeitgrenze; antwortet
  der Server nicht, beendet Esc das Werkzeug nie. Vorschlag: zweites Esc beendet sofort, oder Zeitgrenze für Befehle. Aufwand klein, Priorität mittel.
- **Zweiter Klick innerhalb 400 ms und 6 px wird immer verschluckt** (`:312`), auch bei Werkzeugen ohne Doppelklick (`mehrpunkt` fehlt).
  Selten spürbar. Vorschlag: nur bei `mehrpunkt` verschlucken. Aufwand klein, Priorität tief.
- **Ungültige Zifferneingabe** (`:372`, z. B. «12.5.3»): wird ohne Hinweis verworfen. Aufwand klein, Priorität tief.

### 3.4 Geometrie – nachgerechnet (Zusatzauftrag Christian 12.11.2026)
Alle Werte von Hand gerechnet und mit dem Code verglichen (`ausrichtung.js`, `fangen.js`):
- **Bildschirm ↔ Modell, Drehung 90°:** Modell (1000, 0) → gedreht (0, 1000) → SVG (0, −1000), also am Bildschirm nach oben; zurück
  (0, −1000) → (1000, 0). Stimmt mit `transform="scale(1 -1) rotate(d)"` überein. Hin und zurück bei 37°: (1234.5, −678.9) → (1234.5, −678.9).
- **Orthogonal (Shift) in einer um 30° gedrehten Ansicht:** letzter Punkt (0, 0), Maus (1000, 500). In Ansichtsachsen (616, 933), senkrecht
  gewinnt, zurück ins Modell (466.5, 808.0). Richtung im Modell 60° = 90° − 30°. Stimmt.
- **Polarfang 45°:** Maus (1000, 30), Radius 50 → Strahl 0°, Querabstand 30 ≤ 50 → (1000, 0). Bei 359° → 0° (kein Sprung auf 360).
  In der um 30° gedrehten Ansicht zeigt der Winkel 90° der Ansicht = 60° im Modell. Stimmt.
- **Längeneingabe:** (0, 0) Richtung (3, 4), 2500 mm → (1500, 2000). Stimmt.
- **Gefangener Endpunkt in gedrehter Ansicht:** kommt nach Hin- und Rückdrehung exakt zurück (Rundung auf 1e-6 mm).

Auffälligkeiten dabei (keine Rechenfehler, aber Logik zum Entscheiden):
- **Shift + Endpunkt** (`fangen.js` `fangenOrtho`): Ein Endpunkt bis zum Fangradius neben der orthogonalen Linie wird auf die Linie
  projiziert und als «Endpunkt» gezeigt. Beispiel: Endpunkt (1005, 15), Linie y = 0 → gefangen (1005, 0). Der Punkt liegt dann 15 mm neben dem
  echten Endpunkt, das Symbol sagt aber Endpunkt. Revit und AutoCAD lassen den Objektfang vor Ortho gelten. Vorschlag: eigenes Symbol
  («Verlängerung») oder Endpunkt hat Vorrang. Aufwand klein, Priorität mittel – Entscheid Christian.
- **Raster bei Polar und Shift:** Die Länge wird aufs Raster gerundet; nah am letzten Punkt wird sie 0 (Beispiel: Maus 30 mm entfernt,
  Raster 100 → Punkt = letzter Punkt). Kürzer als das halbe Raster lässt sich mit Raster nicht zeichnen; die Vorschau zeigt dann nichts.
  Vorschlag: Mindestlänge ein Rasterschritt oder in diesem Fall ohne Raster. Aufwand klein, Priorität tief.
- **Leistung** (`fangen.js`): Lot, und mit Shift auch Endpunkt und Mitte, laufen bei jeder Mausbewegung über alle Kanten der Ansicht (nicht
  nur über die nahen). Bei grossen Plänen mit IFC-Unterlage spürbar. Vorschlag: Vorauswahl über einen Streifen um die Linie. Aufwand klein, Priorität mittel.
- **`zuBildschirm`/`zuSvg`** (`grundriss.js:232`, `:569`) rechnen Breite und Höhe getrennt; das stimmt nur, solange der Ausschnitt dasselbe
  Seitenverhältnis hat wie das Fenster. `einpassen` und `groesseGeaendert` halten das ein; kein Fehler gefunden.

### 3.5 Menüband und Aktionen
- **Register «Ändern» nach Werkzeugende** (`menueband.js:131`): Während ein Werkzeug läuft, springt das Band nicht (so festgehalten). Endet das
  Werkzeug und eine Leitung ist noch gewählt, bleibt das Band im alten Register. Siehe Frage 1. Aufwand klein.
- **Netzbefehle an Bauteilen ohne Netz** (`aktionen-bearbeiten.js:138`, `aktionen-verbinden.js:57`): «Stromab folgen», «Stromauf folgen»,
  «Verbinden», «Trennen …» stehen im Kontextregister auch an Wand, Raum, Text. Das Rechtsklick-Menü blendet sie dort schon aus
  (`kontextmenue-logik.js` `NUR_NETZ`). Vorschlag: `fuer` über die Rolle des Elements. Aufwand klein, Priorität mittel.
- **Drei Knöpfe mit demselben Symbol** im Register «BIM to Field» (`aktionen-baustelle.js`: Webseite Monteur, Webseite Projektleiter,
  Webseite … alle `bildschirm`), «Zugänge» mit dem Symbol der Eigenschaften. Aufwand klein, Priorität tief.
- **Gruppen «Planblätter» und «Schema» im Register Ansicht** fehlen in `GRUPPENFOLGE` (`menueband.js:49`) und stehen darum immer am Ende. Priorität tief.
- **`familie.platzieren-aus-browser`, `familie.bearbeiten`**: `aktiv` ohne `hinweis`; gesperrt kommt nur «… ist im Moment nicht möglich». Priorität tief.
- **Kürzel `Strg+D`** steht in der Aktion `start.zeigen`, wird aber von `startbildschirm.js` selbst behandelt; `kuerzel.js` kennt die
  Kombination nicht. Kein Fehler, aber zwei Stellen. Priorität tief.
- **Kontextmenü und laufendes Werkzeug** (`dialoge.js:181`): Esc schliesst das Menü mit `stopPropagation` auf demselben Knoten wie der
  Tastaturhörer der Werkzeuge; das hält ihn nicht auf. Praktisch kaum erreichbar, weil der Rechtsklick dem Werkzeug gehört. Priorität tief.

### 3.6 Web-Server
- **Neues Projekt lädt alle freigegebenen Familien in der Anfrage** (`ProjektDienst.cs:203`): Je Familie ein Befehl samt Herstellerunterlagen
  von der Bibliothek, ohne Fortschritt; über VPN kann das lange dauern, die Oberfläche zeigt nur den wartenden Dialog. Vorschlag:
  Fortschritt über den Ereignisstrom oder Unterlagen nachladen. Aufwand mittel, Priorität mittel.
- **Meldung ohne Bibliothek** (`Hlks.Kern/Familien/FamilienGrundbestand.cs:15`, nicht mein Bereich): lautet «0 von 0 freigegebenen Familien
  geladen. Nicht geladen: Familienbibliothek nicht eingestellt». Bei vielen Fehlern wird die Meldung beliebig lang (alle mit «;» verkettet,
  Einblendung 6 s). Vorschlag: eigener Satz für «keine Bibliothek», Liste auf fünf kürzen und in einem Dialog zeigen. Aufwand klein, Priorität mittel.
- **Mustersysteme und Hauptanlagen beim Anlegen** (`ProjektDienst.cs:205–209`): Fehler werden ohne Meldung geschluckt. Schlägt es fehl, fehlt
  dem neuen Projekt die Basis, und niemand erfährt es. Vorschlag: in denselben Hinweis aufnehmen. Aufwand klein, Priorität mittel.
- **Doppelte Hilfen** (`Program.cs:85–107` `Koerper`/`Sicher` und `EndpunktHilfe.cs`): gleiche Aufgabe, leicht verschieden (`Sicher` kennt
  `TimeoutException` und `HkXrFehler` nicht). Seit dem Fehlerschutz ohne Folgen für den Benutzer ausser dem Statuscode. Aufwand klein, Priorität tief.
- **`zustand.js` `derKlasse`**: läuft bei jedem Aufruf über alle Elemente; wird aus `aktiv()`-Prüfungen und Dialogen gerufen. Bei grossen
  Modellen ein Index je Klasse. Aufwand klein, Priorität tief (nicht gemessen).

## 4. Offene Fragen an Christian

1. Leitung bleibt nach dem Zeichnen gewählt: Soll das Menüband dann auf «Ändern | Rohr» springen, oder im Gewerkeregister bleiben, damit man
   gleich weiterzeichnet?
2. Baustellen-Zugang: Darf er ohne angelegte Zugänge offen bleiben (Testbetrieb), oder soll er grundsätzlich nur mit persönlichem Link gehen?
3. Shift und Endpunkt: Soll ein Endpunkt neben der orthogonalen Linie Vorrang haben (wie Revit), oder die Linie (wie heute, dann mit eigenem Symbol)?
4. Netzbefehle (Stromab/Stromauf folgen, Verbinden, Trennen) im Kontextregister an Wand, Raum, Text ebenfalls ausblenden?

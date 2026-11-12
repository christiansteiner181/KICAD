# Gesamtprüfung KICAD, Runde 3 – Bereich `schema_api`

Stand: Zweig `worktree-agent-a4e3abfc5c9198ad6`, 12.11.2026. Geprüft: `src/Hlks.Kern/Api/` (Nachfuehrungen.cs, Befehle.cs, Abfragen.cs, Mehrfach.cs,
SystemVererben.cs), `src/Hlks.Kern/Projekt.cs` (Ausführen, Trockenlauf, Pakete, Rückgängig, Wiederholen, gezieltes Zurücknehmen), `src/Hlks.Kern/Modell/Modell.cs`
(Anwenden, Kopie), `src/Hlks.Kern/Leitungen/LeitungenNachfuehren.cs`, `src/Hlks.Kern/Schema/` (SchemaGraph, SchemaAnordnung, SchemaGrafik, SchemaSymbole,
SchemaSymbolZeichnung, SchemaBefehle, LueftungSchema) und `src/Hlks.Web/wwwroot/js/ansichten/schema-logik.js`, `schema.js` (Koordinaten, Ziehen, Zelle).

## 1. Kurzfazit

Der Befund aus Runde 2 ist behoben: Die Nachführung läuft jetzt, bis nichts mehr ändert, und meldet es, falls die Grenze je greift. Daneben fanden sich drei
echte Fehler in der Kern-API (Wiederholen überschrieb eine spätere fremde Änderung ohne Meldung; ein Geometriepunkt mit einer Koordinate lief als Ausnahme
statt als Ablehnung durch; `element_aendern` nahm ein leeres Kennzeichen an) und einer in der Schema-Oberfläche (im Bündel einer Steigzone hängte «Element
einfügen» alles an den ersten Strang). Die Geometrie des Strangschemas hält der Nachrechnung stand (Blickrichtung, Bandhöhen, Drehung der Symbole, Bögen,
Textlage); offen sind Darstellungsfragen, die Christian entscheiden muss – vor allem die Fliessrichtung richtungsabhängiger Symbole und das stille Stauchen
bei vielen Knoten in einer Zelle. Kein Test war vor den Änderungen rot.

## 2. Korrigiert

### 2.1 Nachführung endet nicht mehr still nach 5 Runden (Auftrag Punkt 1)
- **Datei:** `src/Hlks.Kern/Api/Nachfuehrungen.cs`
- **Fehler:** `MaxRunden = 5`. Je Runde folgt ein Glied einer Kette (Leitung → Formteil → Formteil → …). Handrechnung: Runde 1 zieht RE-1 nach, Runde 5
  RE-5 – das sechste Formteil und die Leitung dahinter blieben stehen, ohne Hinweis; das Netz war gerissen (P13 meldete es erst in der Prüfung).
- **Korrektur:** Schleife bis keine Regel mehr etwas ändert. Schutz gegen Endlosschleifen: Grenze `Rundengrenze(n) = Elementzahl + 2`, mindestens 8, höchstens
  2000 (eine Kette aus n Gliedern braucht n + 1 Runden und kann nicht länger sein als das Modell). Greift die Grenze, steht in den Hinweisen des Befehls
  «Nachführung nach N Runden abgebrochen: … (zuletzt geändert: …) … Prüfbefunde ansehen (P13 …) oder den Befehl rückgängig machen».
- **Test:** `RoutingNachfuehrungTests.KetteAusSechsFormteilen_FolgtGanz_BisZurLeitungDahinter` (sechs Reduktionen DN 25/20 direkt aneinander, RO-1 um 100 mm
  quer verschoben: alle sechs und RO-2 folgen, kein P13, Rückgängig in einem Schritt) und `Rundengrenze_EndlosRegel_HoertAufUndMeldetEs` (eine Regel, die sich
  selbst auslöst: 12 Aufrufe, dann Hinweis). Filter `FullyQualifiedName~RoutingNachfuehrungTests`: 13 von 13 grün.
- **Commit:** `79df155` Nachführung: Rundengrenze an der Netzgrösse statt fest 5, Hinweis wenn sie greift

### 2.2 Wiederholen überschreibt keine spätere Änderung mehr
- **Datei:** `src/Hlks.Kern/Projekt.cs` (`Wiederholen`, neu `NichtMehrWieVorher`)
- **Fehler:** Rückgängig prüft den Inhalt («Rückgängig überschreibt nie eine spätere Änderung»), Wiederholen nicht. Ablauf: A ändert WC-1, macht es rückgängig;
  B ändert WC-1 (Abgleich); A drückt Wiederholen → A's alter Stand lag ohne Meldung über B's Änderung. Bei einem Löschen, das schon ein anderer gemacht hatte,
  entstand eine Operation ohne `ElementVorher` (die Beschreibung des Pakets wäre mit einer Null-Ausnahme gescheitert).
- **Korrektur:** Vor jeder Operation wird der Stand mit ihrem «vorher» verglichen (Inhalt, nicht Version – wie `Umkehren`); weicht er ab: abgelehnt mit
  «WC-1 wurde inzwischen geändert oder gelöscht.» und Vorschlag. Dazu trägt «Nichts zu wiederholen.» jetzt auch ein `Fehler`-Feld (wie Rückgängig).
- **Test:** `PaketZuruecknehmenTests.Wiederholen_NachFremderAenderung_Abgelehnt_FremdesBleibt` (zwei Projekte auf derselben Ablage). Filter
  `FullyQualifiedName~PaketZuruecknehmenTests|FullyQualifiedName~TrockenlaufUndoTests|FullyQualifiedName~BefehleTests|FullyQualifiedName~VerbindenTests`: 264 von 264 grün.
- **Commit:** `fc10af4` Wiederholen überschreibt keine spätere Änderung mehr

### 2.3 Befehlsparameter: drei Eingaben, die falsch oder gar nicht abgelehnt wurden (ein Commit, alle in Befehle.cs)
- **Dateien:** `src/Hlks.Kern/Api/Befehle.cs`, `src/Hlks.Kern/Leitungen/RoutingBefehle.cs`
- **Fehler und Korrektur:**
  1. `Param.Punkt` trennte den Text «KENNZEICHEN.anschluss» immer am letzten Punkt. Ein Kennzeichen mit Punkt ohne Anschlussname («WC-E.02», im Beispielprojekt
     üblich) wurde zu Element «WC-E», Anschluss «02» → «Element «WC-E» gibt es nicht». Die Oberfläche umging das schon (Kommentar in `schema-logik.js`), die KI
     über MCP nicht. Jetzt gilt der ganze Text als Element, wenn es eines mit diesem Kennzeichen gibt (verbinden, trennen, leitung_ziehen, element_aendern
     verbinden, leitung_zeichnen von/nach).
  2. Ein Geometriepunkt mit nur einer Koordinate (`"punkte": [[5]]`) lief als `IndexOutOfRangeException` durch – nicht in der Fangliste von
     `Projekt.AusfuehrenIntern`, also kein «Abgelehnt», sondern ein Serverfehler. Jetzt Ablehnung mit Vorschlag («Jeder Punkt als [x, y] oder [x, y, z] in mm»).
  3. `element_aendern` mit `kennzeichen: ""` bzw. nur Leerzeichen setzte ein leeres Kennzeichen; das Element war danach nur noch über die Id ansprechbar. Jetzt
     abgelehnt mit Vorschlag; die Meldung «schon vergeben» nennt neu die Klasse und einen freien Vorschlag (wie element_anlegen).
- **Test:** `BefehleTests.Verbinden_KennzeichenMitPunkt_AlsText`, `BefehleTests.PunktMitEinerKoordinate_UndLeeresKennzeichen_Abgelehnt`; Lauf wie 2.2 (264 grün),
  dazu `FullyQualifiedName~RoutingNachfuehrungTests|FullyQualifiedName~RoutingZeichnenTests|FullyQualifiedName~RoutingAbzweigTests`: 38 von 38 grün.
- **Commit:** `612c9d8` Befehlsparameter: Kennzeichen mit Punkt, Punkt mit einer Koordinate, leeres Kennzeichen

### 2.4 Schema: Zelle im Bündel nimmt den Strang mit der nächsten Linie
- **Dateien:** `src/Hlks.Web/wwwroot/js/ansichten/schema-logik.js` (`zelleBei`), `src/Hlks.Web/OBERFLAECHE.md` (Zeile zu «Element einfügen» nachgeführt)
- **Fehler:** Die Abfrage `schema` gibt jeder Spalte x und Breite ihrer **Gruppe**. Im Bündel einer Steigzone (z. B. KW, WW, Zirkulation mit Linien bei 40, 43,
  46 mm) traf `find` immer die erste Spalte: «Element einfügen» setzte `teilVon` stets auf den ersten Strang, die Statusleiste zeigte ihn auch so an.
- **Korrektur:** Unter den Spalten, die den Punkt enthalten, gilt die mit der nächsten Stranglinie (Gleichstand: die erste). Handbeispiel: Klick bei x = 43.4 →
  Abstände 3.4 / 0.4 / 2.6 → WW-1; x = 41.5 → 1.5 / 1.5 → KW-1 (erste); x = 80 (bei den Endgeräten) → ZI-1 (letzte Linie des Bündels).
- **Test:** `node --test js/ansichten/schema.test.mjs` (neuer Test «Zelle im Bündel (Steigzone) …»): 35 bestanden, 3 übersprungen (brauchen einen Testserver), 0 Fehler.
- **Commit:** `d8ecd3f` Schema: Zelle im Bündel nimmt den Strang mit der nächsten Linie

## 3. Nachgerechnet und in Ordnung (Geometrie)

- **Blickrichtung** (`SchemaAnordnung.cs` Quer/Tiefe, Z. ~803): von Süden gesehen links = West → quer = x, nah = Süd → Tiefe = y; von Norden quer = −x, Tiefe = −y;
  von Osten (Blick nach Westen, linke Hand im Süden) quer = y, Tiefe = −x; von Westen quer = −y, Tiefe = x. Alle vier stimmen.
- **Bandhöhen** (`HoehenSetzen`): 2 Geschosse, Band 1 mit 3 Einlage-Spuren → Höhe max(2.4, 2·1.2 + 2·1.5) = 5.4, Zuwachs 3.0; Zeile 1 auf y0 = 53; Bandmitte
  53 + 1.2 − 2.7 = 51.5, Oberkante 54.2 (= y0 + 1.2, unverändert zur Zeile darüber), Unterkante 48.8 (= 50 − 1.2, unverändert zur Zeile darunter). Stimmt mit dem
  Dateikopf überein; das oberste Band wächst nach oben, Unterkante bleibt.
- **Drehung der Symbole** (`SchemaSymbole.Gedreht`, `SchemaSymbolZeichnung.Dreh`): 90° gegen den Uhrzeigersinn = (x, y) → (−y, x); Anschlussrichtungen N→W, W→S, S→E,
  E→N; Bogenwinkel + 90. Stimmt. HKAP-Bögen (y nach unten, Uhrzeigersinn) → KICAD: von = −(start + sweep), bis = von + sweep; der neuere Zeichner behandelt auch
  negative Sweeps (min/|sweep|), der ältere `SinnbildPrimitive` nur positive – in der eingebauten Bibliothek kommt nur `A(0, −3, 3, 0, 180)` vor, also in Ordnung.
- **Anschlussversatz am Endgerät** (`Versatz`): n = 3 Stränge → Schritt min(1.5, 4.5/2) = 1.5, Versätze −1.5 / 0 / +1.5 um die Gerätemitte. Stimmt.
- **Grössentexte** (`GroessenTexte.Setzen`): waagrecht Einfügepunkt = Grundlinie unten am Rahmen, senkrecht (90° gedreht, Schrift wächst nach links) rechts am
  Rahmen; Ausgangsseite über bzw. links der Linie. Stimmt.
- **Orthogonale Linienführung** (`Orthogonal`): alle Fälle des Dateikopfs durchgegangen (Abgang senkrecht zuerst, Etage waagrecht zuerst, Strang–Strang
  senkrecht am Anfangsstrang, Spur–freier Knoten senkrecht ab der Spur). Stimmt mit der Beschreibung überein.
- **Trockenlauf:** kehrt in `Uebernehmen` vor Reservierung, Paket und Ereignis zurück; Befehle mit Dateizugriff (Familienbibliothek, Mustersystem, Plansatz, BCF)
  fragen `k.Trockenlauf` ab. Ein Paket je Befehl, Nachführungen im selben Paket; Rückgängig kehrt alle Operationen in umgekehrter Reihenfolge um.
- **Schemagraph:** `Frei` = kein Knoten an beiden Enden; Stellen mit `xs.Min()` (Einlage- und Grundleitungsspuren) laufen nur über nicht freie Linien, also nie leer.

## 4. Gefunden, nicht korrigiert

| Nr. | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `Schema/SchemaSymbole.cs:76–113`, `Schema/SchemaSymbolZeichnung.cs:33` | Symbole in der Leitung drehen nur nach «Leitung senkrecht/waagrecht», nie nach der Fliessrichtung. Ein «senkrecht» gezeichnetes Symbol (Durchfluss oben → unten) steht an einem Steigstrang (Fluss aufwärts) verkehrt, an einer waagrechten Leitung immer links → rechts. | Richtungsabhängige Sinnbilder (Rückschlagventil, Pumpe, Schmutzfänger) können gegen die Fliessrichtung zeigen. | Fliessrichtung der Linie am Knoten auswerten (SchemaLinie.Von/Nach und Lage) und um 180° drehen bzw. spiegeln; je Symbol im Katalog ein Merkmal «richtungsabhängig». Erst Frage 1 klären. | mittel | hoch |
| 2 | `Schema/SchemaAnordnung.cs:1833–1846` (SteigSetzen), `:1880–1893` (SpurenSetzen) | Viele Knoten in einer Zelle werden «gleichmässig enger» gesetzt, ohne Hinweis. Handrechnung: Platz auf der Stranglinie 50 − 14 − 20 = 16 mm; bei 9 Steigknoten Abstand 2 mm, bei 17 noch 1 mm – das Ventilsymbol ist 4 × 3 mm. Spuren: Platz 12 mm, ab 5 waagrechten Strängen unter 4 mm. | Symbole und Kennzeichen überdecken sich; nur für Wohnungsblöcke gibt es heute einen Hinweis («eng gesetzt»). | In `HinweiseSammeln` einen Hinweis je Zelle, sobald die Stufe unter die Symbolhöhe fällt («EG × Strang KW-1: 12 Knoten auf 16 mm – von Hand verteilen oder Zeilenhöhe»); allenfalls Zeilenhöhe je Geschoss wachsen lassen (Entscheid). | klein (Hinweis) / mittel (Zeile wächst) | mittel |
| 3 | `Schema/SchemaAnordnung.cs:983` (Baeume, `Endpunkt`) | Ein Gerät an Strängen verschiedener Gruppen (z. B. Waschtisch an KW und WW ohne gemeinsame Steigzone) steht bei der ersten Gruppe; der Baum der anderen Gruppe führt seine Linie quer durch die Gruppen dazwischen. | Linien kreuzen fremde Stranglinien und Endgeräte, ohne dass das Schema sagt warum. | Hinweis «WT-1 hängt an Strängen in 2 Gruppen – Steigzone setzen (Strang.steigzone), dann stehen sie als Bündel». | klein | mittel |
| 4 | `Schema/SchemaAnordnung.cs:1811` (Breite, Kreis) | Die Gruppenbreite deckt den Strangkreis nur nach rechts. Handrechnung: ein Strang, Kreismitte 10 mm ab Gruppenrand, Radius = 0.66 · Zeichen + 1.2 → ab 14 Zeichen (10.4 mm) ragt der Kreis links über die Gruppe, bei 22 Zeichen 5.7 mm. | Bei langen Steigzonen-Namen überdeckt der Kreis den Kopf der Nachbargruppe bzw. die Geschossspalte. | Linken Überstand zur Breite der Gruppe davor schlagen oder die Stranglinie in der Gruppe nach rechts rücken. | klein | tief |
| 5 | `Schema/SchemaAnordnung.cs:628–634` (Zeilen) | Je Geschoss **aller** Gebäude eine Zeile, nach Kote. Zwei Gebäude mit je einem EG ergeben zwei Zeilen übereinander. | Bei mehreren Gebäuden wird das Schema doppelt so hoch und die Stränge laufen scheinbar durch fremde Geschosse. | Zeilen je Kote zusammenfassen oder das Schema je Gebäude filtern (Ansicht.gebaeude). Frage 3. | mittel | tief (heute ein Gebäude) |
| 6 | `Projekt.cs:185`, `Modell/Modell.cs:64` und `:184`, `Api/Befehle.cs:72` | Leistung: jeder Befehl (auch Trockenlauf, Rückgängig) kopiert das ganze Modell tief (`Modell.Kopie`) und rechnet alle Prüfungen über den neuen Stand; `Modell.VerbindungenVon` durchsucht alle Verbindungen (O(V)) und wird in Schleifen gerufen (Löschen, Versatz, Strangzuordnung); `NaechstesKennzeichen` durchsucht je Aufruf alle Elemente (O(n²) beim Anlegen vieler Teile in einem Befehl). | Bei grossen Modellen (Bernstrasse-Grösse) wächst die Antwortzeit jedes Klicks linear mit dem Modell, bei Massenbefehlen quadratisch. | Index «Verbindungen je Element» im Modell (wie `anAnschluss`); Zähler je Kürzel im Kontext; Kopie des Modells auf Schreibzugriff (copy-on-write der Elemente) – das ist Architektur, erst messen (Stoppuhr um Kopie und Prüfungen im Protokoll). | mittel bis gross | mittel |
| 7 | `Projekt.cs:192` | Die Fangliste von `AusfuehrenIntern` kennt nur FormatException, InvalidOperationException, JsonException, KeyNotFoundException. ArgumentException, IndexOutOfRange- und NullReferenceException aus einem Befehl laufen als Ausnahme zum Aufrufer (Web: Serverfehler statt «Abgelehnt»). | Ein Programmfehler in einem von rund 200 Befehlen zeigt sich als nackter Fehler ohne Vorschlag; das Modell bleibt zwar unberührt (Arbeitskopie). | Allgemeiner Fang am Schluss: «Interner Fehler in {Befehl}: {Meldung}» mit Vorschlag «Parameter prüfen; der Fehler steht im Protokoll», Ausnahme ins Protokoll. | klein | mittel |
| 8 | `Api/Befehle.cs:154` (`Param.Bool`) | Ein Wert, der kein Bool ist (z. B. `"alle": "true"` als Text, wie KI-Aufrufe es gern schicken), gilt still als Vorgabe (false). | `schema_anordnen { alle: "true" }` räumt nur verwaiste Lagen auf statt alle; `trockenlauf` ist dagegen streng. | Wie `TrockenlaufAusParametern` ablehnen («muss true oder false sein») oder "true"/"false" als Text annehmen. Betrifft alle Befehle – darum nicht ohne Entscheid geändert. | klein | mittel |
| 9 | `Api/SystemVererben.cs:62` → `Befehle.cs` (`Elementpflege.Zuordnungen`) | Das System wird nur gegen den Strang geprüft, wenn `teilVon` im selben Aufruf steht. `system_vererben` (und `element_aendern system`) kann einer Leitung in Strang S1 (System A) das System B geben. | Element und sein Strang gehören zu verschiedenen Systemen; das Strangschema ordnet nach dem Strang, Mengen nach dem System. | In `Zuordnungen` auch bei `system` allein gegen den Strang prüfen bzw. `system_vererben` den Strang mitnehmen lassen (Frage 4). | klein | mittel |
| 10 | `Api/Befehle.cs:234` (`Elementpflege.Eigenschaften`) | Jede übergebene Eigenschaft bekommt eine neue Herkunft, auch wenn der Wert gleich bleibt. `element_aendern` meldet darum nie «Keine Änderung», und `elemente_aendern` (Mehrfachauswahl) überschreibt die Herkunft aller gewählten Elemente (z. B. «katalog» bzw. «import» wird «mensch»). | Die Abfrage `warum` zeigt danach den letzten Klick statt der eigentlichen Quelle; unnötige Pakete. | Gleichen Inhalt und gleiche Einheit überspringen, ausser es wird eine `quelle` mitgegeben. | klein | tief |
| 11 | `Leitungen/LeitungenNachfuehren.cs:40` | Je geändertes Element wird das «vorher» mit `k.Operationen.FirstOrDefault` gesucht (O(Operationen)). Mit der neuen Rundengrenze bei langen Ketten bzw. grossen Verschiebungen O(n²). | Nur Rechenzeit bei sehr grossen Auswahlen. | Im Kontext ein Wörterbuch Id → erster Stand führen. | klein | tief |
| 12 | `Schema/SchemaAnordnung.cs:808–840` (StrangLage) | Die Steigachse wird im 50-mm-Raster gesucht (`Math.Round(mx / 50)`). Liegt die Achse nahe einer Rastergrenze (z. B. x = 24.9 und 25.1 nach einem Versatz von 0.2 mm), teilt sich ihr Gewicht auf zwei Felder. | Ein kurzes Fallstück an anderer Stelle kann dann als «Achse» gewinnen – die Gruppe steht im Schema an der falschen Stelle. | Felder mit ihren Nachbarn zusammenzählen (3 × 3) oder Abschnitte nach Abstand ≤ 50 mm zusammenfassen. | klein | tief |
| 13 | `Schema/SchemaBefehle.cs` (`SchemaLage`) | `schema_lage` auf einem Lüftungs-Prinzipschema (lueftungVariante) wird angenommen, wirkt aber nicht – das Lüftungsschema liest keine Hand-Lagen. | Verwirrend, kein Hinweis. | Ablehnen mit «Das Prinzipschema Lüftung wird aus der Variante gezeichnet; Hand-Lagen gibt es dort nicht». | klein | tief |
| 14 | `Schema/LueftungSchema.cs:195` u. a. | Zahlen im Lüftungsschema werden mit der Kultur des Servers formatiert (`{a.Gleichzeitigkeit:0.##}`). | Je nach Windows-Einstellung «0.8» oder «0,8»; sonst im Kern überall `InvariantCulture`. | `string.Create(CultureInfo.InvariantCulture, …)`. | klein | tief |
| 15 | `Schema/SchemaGraph.cs:210` | `g != "allgemein"` vergleicht mit Gross-/Kleinschreibung, zwei Zeilen weiter (`Ausgeblendet`) ohne. | Nur bei abweichender Schreibweise im Katalog. | `Equals(…, OrdinalIgnoreCase)`. | klein | tief |
| 16 | `Systeme/Mustersysteme.cs` (`MustersystemSpeichern`) | Der Befehl schreibt eine Datei ausserhalb des Pakets (im Trockenlauf nicht). | Rückgängig nimmt das Speichern nicht zurück – wie bei der Familienbibliothek, dort aber dokumentiert. | In der Befehlsbeschreibung sagen. | klein | tief |

## 5. Offene Fragen an Christian

1. **Fliessrichtung im Schemasymbol:** Sollen richtungsabhängige Symbole (Rückschlagventil, Pumpe, Schmutzfänger) im Strangschema mit der Fliessrichtung der
   Leitung drehen? Heute stehen sie an senkrechten Strängen immer gleich, egal ob Steig- oder Fallstrang. Welche Symbole der HKAP-Bibliothek sind
   richtungsabhängig?
2. **Viele Knoten in einer Zelle:** Reicht ein Hinweis, oder soll die Zeile eines Geschosses wachsen, wenn die Knoten nicht mehr in 16 mm passen?
3. **Mehrere Gebäude:** Ein Schema über alle Gebäude (Zeilen je Kote zusammengefasst) oder je Gebäude ein Schema?
4. **System und Strang:** Darf ein Leitungsbauteil ein anderes System haben als sein Strang? Wenn nein: soll `system_vererben` den Strang mitziehen oder ablehnen?
5. **Bool-Parameter als Text** («true» statt true, Nr. 8): ablehnen oder annehmen?
6. **Rundengrenze der Nachführung:** Wenn die Grenze greift, bleibt der Befehl heute angenommen (mit Hinweis). Wäre Ablehnen des ganzen Befehls richtiger
   (wie beim Versatz, der nicht passt)? Im Auftrag stand «Hinweis»; so ist es gebaut.

## 6. Testläufe (alle gefiltert)

- Kern `FullyQualifiedName~RoutingNachfuehrungTests`: 13 / 13
- Kern `FullyQualifiedName~BefehleTests|~PaketZuruecknehmenTests|~TrockenlaufUndoTests|~VerbindenTests`: 264 / 264
- Kern `FullyQualifiedName~RoutingNachfuehrungTests|~RoutingZeichnenTests|~RoutingAbzweigTests`: 38 / 38
- Node `js/ansichten/schema.test.mjs`: 35 bestanden, 3 übersprungen (Testserver), 0 Fehler
- Nicht gelaufen: Browserreihen, Schema-Tests des Kerns (Schema*, LueftungSchema* – dort wurde kein Kern-Code geändert).

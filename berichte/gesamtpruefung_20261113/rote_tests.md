# Gesamtprüfung 13.11.2026 – rote Tests der vollen Testreihe (Endstand `032efb7`)

## 1. Kurzfazit

Alle 14 roten Tests (Mcp 4, Web 7, Node 3) sind geklärt und grün. Zwei gingen auf eine Korrektur der Nacht zurück (Fehlerschutz machte aus
nicht lesbaren Anfragen 500 statt 400) – Produktcode korrigiert. Einer zeigte einen echten Fehler von vor der Nacht («Rückgängig» im frisch
angelegten Projekt entfernte die Grundausstattung) – Produktcode korrigiert. Einer verlangte eine Sicherheitseinordnung (19 Werkzeuge des
2D-CAD für die KI-Palette) – vorsichtig als «verboten» eingeordnet, **zur Bestätigung durch Christian**. Die übrigen zehn waren veraltete
Tests gegenüber Entscheiden vom 06., 07., 09., 10. und 12.11.2026 und sind nachgeführt.

«Vorher schon rot» ist aus der Git-Historie hergeleitet (Datum des Commits, der das Verhalten geändert hat, gegenüber dem Ausgangsstand
`3aed5cb`), nicht durch einen Testlauf auf dem alten Stand.

## 2. Je Test

### Mcp (Lauf: 4 Klassen gefiltert, 10 Tests, 0 rot) – Commit `1ade976`, nur Tests

| Test | Ursache | vorher rot | Korrektur |
|---|---|---|---|
| ProtokollTests.Server_ListetAlleWerkzeuge… | Erwartete Liste = Abfragen + Befehle + Sitzungswerkzeuge; die 19 Werkzeuge `zeichnung_*` (2D-CAD, `99c4378` ff., 10.11.) fehlten | ja (10.11.) | Test: vergleicht mit `Werkzeugauswahl.Alle()`; deren Zusammensetzung prüft `SicherheitTests.Werkzeugauswahl_Lesen_UndAlle` bereits |
| FehlerkennungTests.Alle_Werkzeuge_Ablehnung… | Dieselbe Ursache: Reflexion ohne `ZeichnungWerkzeuge` | ja (10.11.) | Test: Klasse ergänzt – die Zeichnungswerkzeuge werden jetzt mit erzwungener Ablehnung aufgerufen und tragen alle `isError` |
| WerkzeugAbzweigwinkelTests.LeitungAbzweigen_MitWinkel… | 60° wird seit 06.11. auf 45° angepasst (Christian: «passe die abzweigewinkel automatisch an», bis 30°); der Test erwartete Ablehnung | ja (06.11.) | Test: 60° → angenommen mit Hinweis «gesetzt 45°»; Ablehnung mit 10° (35° daneben) |
| WerkzeugMengenTests.Materialliste_LeitungenJeSerie… | «ohne Serie» heisst seit 07.11. «ohne Produktlinie» (`47e67f3`) | ja (07.11.) | Test: Konstante `HlksMasse.OhneSerie` statt Text. Die Nachtkorrektur am Systemfilter (`33ae98c`) ist nicht die Ursache |

### Web (Lauf: ApiTests, KiSicherheitWebTests, StartApiTests gefiltert, 86 Tests, 0 rot)

| Test | Ursache | vorher rot | Korrektur | Commit |
|---|---|---|---|---|
| ApiTests.UngueltigerTrockenlaufWertGibt400Nicht500 | `Fehlerschutz` (Nacht, `b503e44`) fing auch die `BadHttpRequestException` von ASP.NET ab → 500 «Unerwarteter Fehler» | **nein, durch die Nacht** | **Produkt** `Fehlerschutz.cs`: nicht lesbare Anfrage bleibt 400, neu mit `{ fehler, vorschlag }` | `9ac03e6` |
| StartApiTests.Zuletzt_FehlerWerden400MitVorschlag | Dieselbe Ursache («kein json» → 500) | **nein, durch die Nacht** | wie oben | `9ac03e6` |
| KiSicherheitWebTests.B5_JedesWerkzeugIstEingeordnet… | 19 Werkzeuge `zeichnung_*` ohne Einordnung in `KiEinordnung`/`Verbote` | ja (10.11.) | **Produkt** `GespraechDienst.Verbote`: alle 19 verboten (siehe offene Frage 1). Wirkung unverändert: nicht Eingeordnetes bekam die Palette schon bisher nicht | `a06cc0a` |
| ApiTests.Befehl_RueckgaengigOhneAenderung… | Echter Fehler: Die Pakete der Grundausstattung (Hauptanlagen/Mustersysteme 09.11., Familien 12.11.) lagen nach «Projekt anlegen» auf dem Rückgängig-Stapel; Strg+Z im frischen Projekt entfernte sie | ja (09.11., `199d65d`) | **Produkt** `ProjektDienst.Anlegen`: nach dem Einrichten neu öffnen (wie beim Beispiel), Stapel leer. Test unverändert | `953e9fa` |
| ApiTests.Anlegen_LeeresProjektMitNameUndStandardname | Erwartete 0 Elemente; seit 09.11. hat jedes neue Projekt die Hauptanlage je Gewerk (4 Anlagen) | ja (09.11.) | Test: nur Anlagen im Modell; `familienHinweis` nennt die fehlende Familienbibliothek des Testlaufs («ohne Familien»). Der Grundbestand ist nicht abgeschaltet – der Testlauf hat keine Bibliothek, das meldet die Antwort | `953e9fa` |
| ApiTests.Abfragen_ParameterTabelleDecktGenauDieListeAb | Abfrage `mustersysteme` (09.11., `4f3ab31`) fehlte in der Parametertabelle des Tests | ja (09.11.) | Test: Eintrag ergänzt | `953e9fa` |
| ApiTests.Abfrage_JedeAusDerListeLiefertJson… | Dieselbe Ursache (KeyNotFound `mustersysteme`) | ja (09.11.) | wie oben; die Abfrage liefert ohne Systembibliothek 200 | `953e9fa` |

### Node (Lauf: 3 Dateien, 91 Tests, 90 grün, 1 übersprungen – braucht Server) – Commit `f60f1da`, nur Tests

| Test | Ursache | vorher rot | Korrektur |
|---|---|---|---|
| huelle.test.mjs «Verdrahtung: Register Hülle nach Architektur …» | Regex auf die Registerzeile im Quelltext (`…Hülle, Heizung`); Reihenfolge seit 09.11. neu («Tab Ordnung») | ja (09.11.) | Test: nur noch «Hülle direkt nach Architektur» (gilt weiter) |
| sanitaer-belastung.test.mjs «Verdrahtung: Register je Gewerk …» | Ganze Registerzeile nachbuchstabiert (alte Reihenfolge, ohne «BIM to Field») | ja (09.11.) | Test: je Gewerk ein Register vorhanden, kein «Gebäudetechnik»; Zeile gestrichen (reine Quelltextprüfung) |
| xr-register.test.mjs «Kürzeldialog: … zwischen Ansicht und Verwalten» | Die Nacht (`dc1d291`) hat die Kürzelliste richtig auf die Reihenfolge des Menübands gestellt; der Test hielt die alte fest | **nein, durch die Nacht** (Produkt richtig, Test veraltet) | Test: prüft die sortierte Liste `kuerzelZeilen` (Verwalten, XR, Ändern) |

Dazu `OBERFLAECHE.md`: ein Satz zur nicht rückgängig machbaren Grundausstattung (in `f60f1da`).

## 3. Gefunden, nicht korrigiert

- `src/Hlks.Web/ProjektDienst.cs` (Anlegen): Das Neuöffnen nach dem Einrichten liest das frische Projekt einmal aus der Ablage. Bei grosser
  Bibliothek kostet das etwas Zeit zusätzlich zum Laden der Familien; nicht gemessen. Aufwand klein, Priorität tief.
- Git: Jeder Commit meldet `failed to delete '.git/worktrees/wt_b1'` bzw. `wt_b2` (Permission denied) – verwaiste Arbeitskopien-Einträge, die Git
  nicht aufräumen kann (vermutlich von OneDrive gehalten). Harmlos, aber der Hauptlauf sollte sie bei Gelegenheit mit `git worktree prune` entfernen.

## 4. Offene Fragen an Christian

1. **2D-CAD in der KI-Palette:** Die 19 Werkzeuge `zeichnung_*` sind jetzt für die Palette verboten (vorher: nicht eingeordnet, also ebenfalls
   nicht verfügbar). Soll die Palette die lesenden bekommen (`zeichnungen`, `zeichnung_oeffnen`, `_layer`, `_objekte`, `_messen`, `_bild`,
   `_bloecke`, `_plotstile`)? Dazu müsste die Palette dem Server Zeichnungsordner mitgeben (`--zeichnungen`) – das ist ein eigener Entscheid.
2. **Rückgängig nach «Projekt anlegen»:** Die Grundausstattung ist jetzt nicht mehr rückgängig machbar. So gewollt?

# Gesamtprüfung KICAD – Bereich Datenhaltung (Runde 5, 13.11.2026)

Geprüft: `src/Hlks.Kern/Speicher/OrdnerSpeicher.cs`, `IProjektSpeicher.cs`, `src/Hlks.Kern/Projekt.cs` (Öffnen, Übernehmen, Synchronisieren,
Standabbild, Freigeben), `src/Hlks.Kern/Modell/Modell.cs` (Anwenden, Laden, Abbild), `src/Hlks.Web/AbgleichDienst.cs`, `ProjektDienst.cs`.
Rückgängig, Wiederholen und gezieltes Zurücknehmen sind in `schema_api.md` geprüft und hier nicht wiederholt.

## 1. Kurzfazit

Die Ablage ist sorgfältig gebaut: Kopf und Sperren werden atomar ersetzt, Pakete nie überschrieben, Temp-Dateien übergangen, halbe Dateien
mit Wiederholung gelesen und sonst als «unlesbar» geführt, ohne die übrigen Pakete aufzuhalten; der Arbeitsplatz liest die Ablage ausserhalb
der Projektsperre und meldet «getrennt / wieder verbunden» nur beim Wechsel. Drei echte Lücken sind korrigiert: ein Paket mit gültigem JSON,
aber unbrauchbarem Inhalt blockierte Öffnen und Abgleich ganz; Konfliktkopien von Paketen wurden ein zweites Mal angewandt; fiel die Ablage
während des Öffnens weg, öffnete das Projekt still mit einem Teilstand. Offen bleibt vor allem ein Logikfehler der Konfliktregel
(Versionszähler sind je Gerät verschieden, nach zwei Konflikten am selben Element können zwei Geräte dauerhaft Verschiedenes zeigen – mit Test
nachgewiesen) sowie das unbegrenzte Wachstum der Ordner `aenderungen` und `sperren`, die in jedem Takt bzw. jedem Status ganz gelesen werden.

## 2. Korrigiert

### 2.1 Unbrauchbares Paket blockierte Öffnen und Abgleich (Commit `3f73b67`)
- **Dateien:** `src/Hlks.Kern/Modell/Grundtypen.cs` (neu `Paket.Mangel()`), `src/Hlks.Kern/Projekt.cs` (`Synchronisieren`), `src/Hlks.Web/ProjektDienst.cs` (Vorlesen der Etappe).
- **Fehler:** Abgefangen war nur ungültiges JSON. Eine Datei mit gültigem JSON, aber unbrauchbarem Inhalt (Operation einer neueren
  Programmversion, `"operationen": null`, `element.setzen` ohne Element) löste in `Modell.Anwenden` mitten im Paket eine
  `InvalidOperationException` bzw. Null-Ausnahme aus. Folgen: die Operationen davor waren schon angewandt (halbes Paket), `Projekt.Oeffnen` scheiterte
  mit einer unbehandelten Ausnahme, und am Arbeitsplatz warf jede Abgleich-Etappe von neuem – alle Pakete dahinter kamen nie mehr an, gemeldet
  wurde nur eine Warnung im Protokoll des Servers.
- **Korrektur:** `Paket.Mangel()` prüft vor dem Anwenden (Id, Operationen, bekannte Art, Element bzw. Verbindung vorhanden). Ein Paket mit Mangel gilt wie
  ein unlesbares: nichts wird angewandt, die übrigen laufen weiter, der Arbeitsplatz führt es unter «unlesbar» mit Wartezeit und Meldung.
- **Test:** `SpeicherTests.PaketMitUnbekannterOperationWirdGanzUebergangen` (erste Operation anwendbar, zweite unbekannt, dazu eine Datei «{}»:
  nichts halb angewandt, späteres Paket kommt, Prüfsummen beider Geräte gleich).

### 2.2 Kopie eines Pakets wurde ein zweites Mal angewandt (Commit `3f73b67`)
- **Datei:** `src/Hlks.Kern/Projekt.cs` (`Synchronisieren`).
- **Fehler:** Bekannt war ein Paket über den Dateinamen, angewandt wurde es unter der Id im Inhalt. Eine Kopie mit anderem Namen (Konfliktkopie
  der Synchronisation «…-PC2.json», Kopie von Hand) wurde deshalb noch einmal angewandt: Scheinkonflikt «ältere Änderung verworfen» an die
  Oberfläche, doppelter Eintrag im Verlauf (das gezielte Zurücknehmen dieses Pakets wurde danach mit «später geändert» abgelehnt).
- **Korrektur:** Heisst die Datei anders als das Paket darin und ist das Paket schon bekannt, wird sie einmal gemeldet und übergangen. Ist nur die
  Kopie da, gilt sie als das Paket; das Original wird später nicht mehr doppelt angewandt.
- **Test:** `SpeicherTests.KopieEinesPaketsWirdNichtZweimalAngewandt`.

### 2.3 Ablage fällt während des Öffnens weg – Projekt öffnete mit Teilstand (Commit `3f73b67`)
- **Datei:** `src/Hlks.Kern/Projekt.cs` (`Synchronisieren`, `Oeffnen`).
- **Fehler:** Brach das Netz nach der Paketliste ab, wurde jedes weitere Paket einzeln (je bis 750 ms Wiederholungen) als «nicht lesbar» geführt und
  das Projekt öffnete ohne Meldung mit dem bis dahin gelesenen Stand; `Oeffnen` erkannte nur den Ausfall der Paketliste selbst. Bei 2000 offenen
  Paketen wären das rund 25 Minuten Wartezeit gewesen.
- **Korrektur:** Nach einem Lesefehler wird einmal nachgesehen, ob die Ablage noch antwortet. Wenn nicht: Abbruch mit «Projektablage nicht erreichbar»,
  `Oeffnen` meldet das mit «Netzwerk/VPN prüfen».
- **Test:** `SpeicherTests.AblageFaelltBeimLesenDerPaketeWeg_OeffnenMeldetNichtErreichbar` (Ablage-Attrappe, die nach der ersten Liste wegfällt; nur ein Paket wird versucht).

### 2.4 Neuanlegen einer Datei: keine Wiederholung, gelungenes Umbenennen nicht erkannt (Commit `be64d35`)
- **Datei:** `src/Hlks.Kern/Speicher/OrdnerSpeicher.cs` (`NeuAtomar`, gebraucht für Pakete und Standabbilder).
- **Fehler:** Anders als `ErsetzenAtomar` wiederholte `NeuAtomar` das Umbenennen nicht. Hielt ein Virenscanner oder die Synchronisation die eben
  geschriebene Temp-Datei kurz fest, wurde der Befehl mit «Projektablage nicht erreichbar» abgelehnt. Meldete das Umbenennen einen Fehler, obwohl es
  gewirkt hatte (SMB-Aussetzer), galt die eigene Datei als fremd («gibt es schon») und der Kern schrieb dasselbe Paket unter der nächsten Nummer noch einmal.
- **Korrektur:** Umbenennen bis fünfmal wiederholen (wie beim Ersetzen); ist das Ziel da und die Temp-Datei weg, war es das eigene Umbenennen.
- **Test:** kein neuer (ohne Fehlereinspeisung ins Dateisystem nicht nachstellbar); bestehende Reihe `SpeicherTests` grün, darunter
  `PaketWirdNieUeberschrieben`, `ProjektWeichtBeiBesetzterPaketIdAufNeueNummerAus`, `ZweiBearbeiterSchreibenGleichzeitig`.

### 2.5 Standabbild wurde in jedem Takt neu vorbereitet, solange eines geschrieben wurde (Commit `e80348d`)
- **Datei:** `src/Hlks.Web/ProjektDienst.cs` (`AbgleichenEtappe`).
- **Fehler:** `StandImHintergrund` schreibt nur ein Abbild zur Zeit und verwirft weitere. Solange das Schreiben dauerte (grosses Modell über VPN), nahm
  jeder Takt (2 s) die Projektsperre, kopierte das ganze Modell und warf die Kopie weg – genau dann, wenn die Ablage ohnehin langsam ist.
- **Korrektur:** Solange ein Abbild geschrieben wird, gilt keines als fällig. Kein geändertes Verhalten nach aussen.
- **Test:** bestehende `AbgleichTests` (u. a. `RegelmaessigesStandabbild_NachVielenPaketen`, `Schliessen_SchreibtStandabbildUndGibtReservierungenFrei`) grün.

### Testläufe
- Kern `FullyQualifiedName~SpeicherTests`: 36 von 36 grün (33 bestehende, 3 neue).
- Web `FullyQualifiedName~AbgleichTests`: 26 von 26 grün.
- Kein Test war vor den Änderungen rot.

## 3. Gefunden, nicht korrigiert

| Nr | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `Modell/Modell.cs:111–137` (`Anwenden`), `Projekt.cs` (`Synchronisieren`) | **Konfliktregel läuft nach zwei Konflikten am selben Element auseinander.** Ob ein Konflikt vorliegt, entscheidet der Versionszähler; der ist aber je Gerät verschieden hoch (wer einen Konflikt «neuere übernommen» sieht, zählt eins weiter als das Gerät, das die ältere verwirft). Nachgerechnet mit einem vorübergehenden Test (zwei Projekte auf derselben Ablage): Runde 1: A benennt um («1»), B ohne Abgleich («2») → nach Abgleich A Version 3, B Version 2, beide «2». Runde 2: A benennt um («A», Basis 3), kurz danach B («B», Basis 2). B liest das Paket von A: Basis 3 = eigene Version 3 → **kein Konflikt erkannt, ältere Änderung «A» überschreibt die neuere «B»**. A liest das Paket von B: Konflikt, neuere gewinnt → «B». Ergebnis: A «B», B «A», ein frisch öffnendes drittes Gerät «B»; Prüfsummen A = C ≠ B, kein Fehler gemeldet. | Zwei Arbeitsplätze zeigen dauerhaft Verschiedenes, bis jemand das Element wieder ändert; wer vom falschen Stand aus weiterarbeitet, überschreibt die Arbeit des anderen. Selten, weil Reservierungen gleichzeitiges Ändern normalerweise verhindern (nötig sind Freigeben bzw. Ablauf und ein Abgleichrückstand – auf dem NAS Sekunden, in einem über OneDrive synchronisierten Ordner Minuten). | Version nicht je Gerät zählen, sondern aus dem gewinnenden Paket ableiten (`Version = Basis + 1` der Operation, die gilt); dann haben gleiche Inhalte auf allen Geräten gleiche Versionen und der Fall wird als Konflikt erkannt. Die einfache Regel «ältere Paket-Id verliert immer» wäre falsch, sobald die Uhren zweier Rechner abweichen. Eingriff in den Kern (Rückgängig, Standabbilder bestehender Projekte) – Architektur, mit dem obigen Ablauf als Test. Zusätzlich: Prüfsumme der Arbeitsplätze gegeneinander prüfen (z. B. im Standabbild ablegen und beim Abgleich vergleichen), damit ein Auseinanderlaufen auffällt. | mittel | hoch |
| 2 | `Speicher/OrdnerSpeicher.cs:139` (`PaketListe`), `ProjektDienst.cs` (`AbgleichenEtappe`, Schritt 1) | Der Ordner `aenderungen` wächst unbegrenzt (jede Änderung eine Datei, nie archiviert) und wird in jedem Takt (2 s) von jedem Arbeitsplatz ganz aufgelistet und sortiert. | Über die Lebensdauer eines Projekts kommen Zehntausende Dateien zusammen; das Auflisten über SMB/VPN dauert dann länger als der Takt (Wachhund «antwortet nicht», Dateiwächter-Puffer). Öffnen bleibt dank Standabbild schnell, der laufende Abgleich nicht. | Pakete, die in einem Standabbild enthalten und älter als z. B. 30 Tage sind, in Unterordner je Monat verschieben (`aenderungen/2026-11/`); der Takt listet nur den laufenden und den letzten Monat, das gezielte Zurücknehmen liest bei Bedarf ältere. Vorher messen: Zeit von `PaketListe` im Status ausgeben (`letzteDauerMs` gibt es schon). | mittel | mittel |
| 3 | `Speicher/OrdnerSpeicher.cs:223` (`Reservierungen`), `:212` (`Freigeben`), `ProjektDienst.cs:716` | Abgelaufene Sperrdateien fremder Geräte werden nie gelöscht (nur der Eigentümer räumt beim Schliessen auf; abgestürzte Sitzungen, KI-Läufe mit anderem Gerätenamen nie). Jeder Status liest alle Sperrdateien einzeln (Zeitgrenze 1 s), `Freigeben()` beim Schliessen ebenfalls (Zeitgrenze 4 s). Eine halb geschriebene Sperrdatei kostet bei jedem Lesen 750 ms Wiederholungen. | Nach einigen Monaten zeigt der Status über VPN nur noch den zuletzt bekannten Stand der Reservierungen, und das Schliessen läuft in die Zeitgrenze, ohne die eigenen freizugeben. | Abgelaufene Sperren, die länger als z. B. 24 h abgelaufen sind, beim Lesen löschen (ein Tag Abstand macht den Wettlauf mit einer Verlängerung ungefährlich); unlesbare Sperren ohne Wiederholung als verwaist behandeln, wenn sie älter als eine Minute sind. | klein | mittel |
| 4 | `Projekt.cs:244` (`Uebernehmen`), `OrdnerSpeicher.cs:162` (`Reservieren`) | Jeder Befehl reserviert alle berührten Elemente einzeln: je Element eine Datei lesen und eine atomar schreiben (mit `Flush` auf die Platte), unter der Projektsperre. | Massenbefehle (Mehrfachauswahl, Nachführen nach IFC, grosse Verschiebungen) mit 1000 Elementen brauchen über VPN leicht eine Minute; die Oberfläche meldet währenddessen «beschäftigt». Danach bleiben alle 1000 zwei Stunden für andere gesperrt. | Messen (Stoppuhr um `Reservieren` im Ergebnis). Wenn es stört: eigene gültige Reservierungen im Arbeitsspeicher merken und nur verlängern, wenn sie in weniger als z. B. 30 Minuten ablaufen; ohne `Flush` schreiben (eine verlorene Sperre ist harmloser als eine langsame). | mittel | mittel |
| 5 | `OrdnerSpeicher.cs:204–206` (`Reservieren`, Übernahme) | Eine abgelaufene oder unlesbare fremde Sperre wird mit «Ersetzen» übernommen. Übernehmen zwei Geräte dieselbe im selben Augenblick, glauben beide, sie zu haben. | Beide ändern dasselbe Element; es greift die Konfliktregel (Punkt 1). Sehr selten. | Abgelaufene Sperre zuerst löschen, dann mit `CreateNew` neu anlegen (genau ein Gewinner, wie beim Neuanlegen). | klein | tief |
| 6 | `ProjektDienst.cs:234` (`Schliessen` bei besetztem Projekt), `:271` (`Umhaengen`) | (a) Ist das Projekt besetzt, wird es ohne Sperre abgehängt und freigegeben, während ein Befehl noch läuft; reserviert dieser Befehl danach, bleibt die Sperre zwei Stunden stehen. (b) Wird derselbe Ordner erneut geöffnet, gibt das alte Projekt seine Reservierungen erst nach dem Umhängen frei – ein Befehl im neuen Projekt in diesem Augenblick verliert seine Sperre (gleicher Akteur, gleiches Gerät). | Verwaiste bzw. verlorene Reservierung in einem Zeitfenster von Millisekunden. | (a) hinnehmen oder nach Ende des hängenden Befehls noch einmal freigeben; (b) Freigeben des alten Projekts vor dem Anbinden des neuen, wenn es derselbe Ordner ist. | klein | tief |
| 7 | `Projekt.cs:117` (`Oeffnen`), `OrdnerSpeicher.cs:71` (`KopfLesen`) | Ist `projekt.json` beschädigt (kein gültiges JSON), lautet die Meldung «Projektablage … nicht erreichbar: … ist unlesbar» mit dem Vorschlag «Netzwerk/VPN prüfen». | Irreführend: das Netz ist in Ordnung, die Datei ist kaputt. | Eigene Meldung «projekt.json ist beschädigt» mit Vorschlag (aus Sicherung wiederherstellen); dazu müsste die Ablage «unlesbar» von «nicht erreichbar» unterscheidbar machen (heute private Ausnahme). | klein | tief |
| 8 | `Modell/Grundtypen.cs` (`ProjektKopf.SchemaVersion`), `Projekt.cs:91` | Die Schemaversion wird geschrieben, beim Öffnen aber nie geprüft. | Öffnet ein Rechner mit älterem Programmstand ein Projekt, das ein neuerer geschrieben hat, arbeitet er ohne Warnung weiter; unbekannte Felder gehen beim nächsten Ändern des Elements verloren (das Element wird als Ganzes ersetzt). Seit 2.1 werden wenigstens unbekannte Operationen gemeldet statt halb angewandt. | Beim Öffnen prüfen: höhere Version → nur lesend öffnen oder ablehnen mit «KICAD aktualisieren». Heute gibt es nur Version 1; die Prüfung muss aber vor der ersten Erhöhung auf allen Rechnern sein. | klein | mittel |
| 9 | `Projekt.cs` (`Uebernehmen`) | Vor dem Schreiben eines Pakets wird nicht abgeglichen; ein Befehl rechnet auf dem Stand des letzten Takts (bis 2 s alt, bei Ablage über OneDrive länger). Hat inzwischen jemand dasselbe Element geändert und freigegeben, ersetzt das neue Paket dessen Änderung als Ganzes. | Die Änderung des anderen geht verloren; gemeldet wird ein Konflikt-Ereignis erst beim anderen. Auf dem NAS wegen der Reservierungen sehr selten. | Nach dem Reservieren die Pakete lesen, die die reservierten Elemente betreffen könnten (neue seit dem letzten Abgleich), und bei Treffer den Befehl ablehnen («wurde inzwischen von … geändert – erneut versuchen»). | mittel | tief |
| 10 | `Projekt.cs:445` (`PaketZuruecknehmen`, Paket aus dem Standabbild) | Für ein Paket, das schon im Standabbild steckt, werden alle neueren Pakete der Ablage einzeln gelesen. | Bei einem alten Paket in einem grossen Projekt Hunderte bis Tausende Dateizugriffe unter der Projektsperre. | Zuerst nur die Paketliste filtern und abbrechen, sobald ein Hindernis gefunden ist; oder die berührten Ids je Paket im Standabbild mitführen. | klein | tief |

Geprüft und in Ordnung: atomares Ersetzen von Kopf, Sperren und Austauschdatei (Temp-Datei im selben Ordner, Umbenennen mit Wiederholung); Temp-Dateien
mit Punkt werden nie gelesen und nach Fehlern entfernt; Standabbild fällt bei beschädigter neuester Datei auf die nächstältere zurück und trägt die
Liste der enthaltenen Pakete und die Löschvermerke (später eintreffende ältere Pakete holen Gelöschtes nicht zurück); Name des Standabbilds sortiert
auch bei verstellter Uhr nach den vorhandenen; Reservieren ist «alles oder nichts» mit fester Reihenfolge; der Abgleich hält die Projektsperre nur zum
Anwenden, nie während eines Ablage-Zugriffs; Projektwechsel verwirft laufende Arbeiten über die Generation; Schliessen und Beenden haben Zeitgrenzen.

## 4. Offene Fragen an Christian

1. **Wo liegt `e_KICAD` im Betrieb – nur auf dem NAS (W:) oder auch in einem über OneDrive synchronisierten Ordner?** Die Ablage ist für das NAS
   gebaut (Sperren und Pakete sind sofort für alle sichtbar). In einem OneDrive-Ordner kommen Sperren und Pakete mit Verzögerung an; dann sind
   gleichzeitige Änderungen am selben Element normal und Punkt 1 (Auseinanderlaufen) wird vom seltenen zum erwartbaren Fall. Wenn OneDrive vorgesehen
   ist, gehört Punkt 1 vor den nächsten Einsatz mit zwei Personen.
2. **Reservierungen nach einem Absturz:** Stürzt ein Rechner ab oder bricht das VPN beim Schliessen ab, bleiben dessen Elemente bis zwei Stunden nach
   der letzten Änderung für alle anderen gesperrt; freigeben kann nur dieselbe Person am selben Gerät («meine Elemente freigeben»). Soll es einen
   Knopf «Reservierung von … übernehmen» mit Rückfrage geben, oder eine kürzere Dauer (z. B. 30 Minuten, mit Verlängerung solange das Projekt offen ist)?
3. **Aufräumen der Pakete (Punkt 2):** Dürfen alte Pakete in Monatsordner verschoben werden, oder soll der Ordner `aenderungen` als lückenloses
   Protokoll flach bleiben?
4. **Ältere Programmstände (Punkt 8):** Sollen Mac und Windows-PC ein Projekt ablehnen bzw. nur lesend öffnen, wenn es von einem neueren KICAD
   geschrieben wurde?

Nebenbefund ausserhalb des Bereichs: Bei jedem `git commit` in dieser Arbeitskopie meldet Git «failed to delete …/.git/worktrees/wt_b1» und
«wt_b2: Permission denied» (verwaiste Verwaltungsordner fremder Arbeitskopien, vermutlich von OneDrive gehalten). Die Commits gelingen trotzdem.

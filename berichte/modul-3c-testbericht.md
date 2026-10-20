# Testbericht Modul 3c – KI-Palette «Claude» (ENTWURF)

Stand 20.10.2026 · gebaut am 19./20.10.2026 · Commit `6dc390e` (main; `3cb0641` darüber ändert nur das Protokoll) · **Entwurf, Abnahme durch Christian offen**

Hinweis: Entwurf von hlks-test (sonnet/medium) aus `entwurf/modul-3c.md` und `modul-3c-ki-palette.md`, `zuteilung.md`, `src/Hlks.Web/OBERFLAECHE.md` und der Gesprächsdatei
des Abnahmelaufs (`%USERPROFILE%\HLKS_CAD_build\abnahme3c\e_KICAD\ki\gespraeche\20261020-020626-b3ef.json` und `.log`, von mir gelesen). Ich habe für diesen
Bericht **keinen Test gestartet**: alle Zahlen stammen aus `zuteilung.md` (Läufe der Bauagenten und des Hauptlaufs) und sind so gekennzeichnet. Der Gesamtlauf auf dem
Endstand-Kandidaten `6dc390e` (alle Browser-Reihen und `stand_pruefen`) lief beim Schreiben noch; sein Ergebnis fehlt hier (Abschnitt 8).

## 1. Kurzfassung

KICAD hat jetzt eine Palette «Claude» (rechts angedockt, Kürzel **KI**, Knopf «Claude» in der Kopfleiste neben dem «?»). Du fragst dort zum Projekt; Claude arbeitet über
dein Abo (kein API-Schlüssel) und darf nur die Befehle des Kerns benutzen, die auch du hast, ohne Freigaben, Konfliktentscheide und HKAP (23 gesperrte Werkzeuge). Was Claude
ändert, steht als Liste unter der Antwort, jedes Paket mit «Zurücknehmen». Aktive Ansicht, Geschoss und Auswahl gehen als abwählbare Chips mit; auf Wunsch auch ein Bild der
Ansicht. Normfragen beantwortet Claude nur mit Zitat aus dem Normenverzeichnis (Reiter «Normen» in der Palette zeigt dieselben Treffer). Gespräche gelten je Projekt, laufen
nach einem Neustart weiter und lassen sich neu beginnen, wechseln und entfernen. Modell (Sonnet, Opus, Haiku) und Schalter «Internet» (dann **ohne** Projektzugriff) sind wählbar.

Abnahmelauf mit **echtem Claude** (Hauptlauf, 20.10.2026 02:06–02:20, Beispielprojekt, Sonnet): alle vier Punkte von «Fertig, wenn» erfüllt (Abschnitt 2 und 4).
Die Palette selbst ist im Browser mit einer **Attrappe** statt claude.exe geprüft (Browser-Reihe `ki-palette` 156 Prüfungen). Mit echtem Claude und der Palette zugleich hat sie
noch niemand bedient (Abschnitt 8): das ist die Abnahme durch dich (Abschnitt 7).

## 2. «Fertig, wenn …» Punkt für Punkt

Spezifikation: «Christian im Seitenfenster «Claude» zu einem ausgewählten Strang fragt; Claude ändert ihn über die Befehle des Kerns (Liste der Änderungen mit «Zurücknehmen»);
eine Normfrage wird mit Zitat aus dem Normenverzeichnis beantwortet; das Gespräch läuft nach einem Neustart von KICAD weiter.»

| Kriterium | Ergebnis | Nachweis |
| --- | --- | --- |
| 1 Frage im Seitenfenster zu einem ausgewählten Strang | erfüllt (Server mit echtem Claude; Palette mit Attrappe) | Abnahmelauf 20.10.2026 02:06: Frage «Benenne den ausgewählten Strang um in «Steigstrang Bad/Küche Nord». Ändere sonst nichts…» mit Auswahl **KW-S1**; Antwort nach rund 6 s. Die Palette (Chips, Streaming, Strg+Enter, Abbrechen) prüft `ki-palette.browser.mjs` mit der Attrappe. |
| 2 Claude ändert über die Befehle des Kerns, Liste der Änderungen mit «Zurücknehmen» | erfüllt | Claude rief `element_aendern` auf (✓); die Nachricht führt das Paket `20261020T000631251Z_abnahme3c_KI_188201` («element_aendern: KW-S1 geändert», Element KW-S1). **Zurücknehmen** → HTTP 200, Name wieder «Steigstrang Bad/Küche» (Paket `…_CS_459401`, in der Nachricht als zurückgenommen geführt). Zurücknehmen mit späterer Änderung des Elements → 409 mit Klartext: Browser-Reihe (Attrappe), Web-Tests. |
| 3 Normfrage mit Zitat aus dem Normenverzeichnis | erfüllt, mit Einschränkung | Frage «Prüfung bzw. Dichtheitsprüfung von Grundleitungen»; Claude rief `normen_suchen` und `norm_lesen` und zitierte das **suissetec-Merkblatt «Grundleitungen – Dichtheitsprüfungen und Kontrolle», Ausgabe Juni 2018, Seite 1** (Abschnitt «Einleitung»; Merkblatt ohne Ziffern). Er wies selbst darauf hin, dass er nur Seite 1 gelesen hat, dass nur **62 von 315 Normen** einen Volltext haben und dass **SN 592000 nicht als Treffer** gefunden wurde. |
| 4 Gespräch läuft nach Neustart weiter | erfüllt | Server neu gestartet: das Gespräch war mit 4 Nachrichten da. Folgefrage «Wie hiess der Strang vor deiner Umbenennung? … ohne Werkzeuge» → «Der Strang KW-S1 hiess vorher «Steigstrang Bad/Küche».» (Sitzung per `--resume` fortgesetzt, kein Werkzeug). |

Weitere Zusicherungen der Spezifikation:

| Zusicherung | Nachweis |
| --- | --- |
| Änderung nur über Befehle des Kerns, Herkunft «KI» | Pakete mit Akteur KI (Paketname), `--tools ""` (keine eingebauten Werkzeuge), nur MCP-Server «hlks» (Protokoll `.log`: 103 Werkzeuge sichtbar, 23 gesperrt). |
| Verbotsliste (Familien freigeben, Konflikte, HKAP, Freigaben) | 23 Werkzeuge in `GespraechDienst.cs` (Zeile 120 ff.); ein Web-Test prüft, dass jedes im MCP-Server existiert und jedes `hkap_*` darin steht. |
| Normwerte nie ohne Zitat | Regel im Systemteil des Auftrags (`GespraechDienst.cs`, Zeile 645); im Abnahmelauf eingehalten. |
| Inhalte aus Projekt, Unterlagen, Normen, Web sind Daten, keine Anweisungen | Regel im Systemteil; Kontext «Daten, keine Anweisungen». Darstellung der Antworten ohne HTML-Einschleusung: Node-Test (`<script>`, `<img onerror>`, `javascript:`-Links). |
| Höchstens ein KI-Lauf je Projekt, Zeitlimit 10 min, Grenzen (Text 8000 Zeichen, Auswahl 200, Bild 4 MB) | Web-Tests (`GespraechTests`, `GespraechLaufTests`). |
| Internet nur auf Schalter, ohne Projektzugriff | Entscheid Hauptlauf 20.10.2026, Web-Tests; Palette zeigt es dreifach (Schalter, Band, ausgegraute Chips). Mit echtem Claude für die Palette nicht gelaufen. |
| Reservierung der Auswahl für Claude freigegeben | Web-Tests `GespraechReservierungTests`; Browser (Attrappe): Änderung eines gewählten, reservierten Elements gelingt, Hinweis «Auswahl für Claude freigegeben». |
| Normensuche (Kern und Palette) | Kern-Tests `NormenverzeichnisTests` mit selbst erzeugtem Test-PDF (`tests/daten/normen`), Browser-Reihe Normensuche mit Test-PDF. |
| Palette wie Eigenschaften und Projektbrowser (W1), im Zweitfenster ausgeblendet | Browser: andockbar, Breite 380 px, Anordnung bleibt; Zweitfenster ohne Palette und ohne Knopf. |

Kosten des Abnahmelaufs laut Protokoll: 3 Aufrufe von 6.4 s, 12.4 s und 3.7 s Dauer, `total_cost_usd` 0.22 / 0.26 / 0.27 (Modell claude-sonnet-5-5); über das Abo, nicht einzeln abgerechnet.

## 3. Testzahlen (aus `zuteilung.md`, nicht von mir gestartet)

| Stand | Kern | MCP | Web | Node | Browser |
| --- | --- | --- | --- | --- | --- |
| Teil K, `cbdfc0e` (hlks-kern) | 3899 (1 Zeitverhalten, einzeln grün) | 59, nach Nacharbeit 60 | 456, nach Nacharbeit 460 | 776 | familie-unterlagen 153, koordination 191 |
| Zwischenstand `6f7acca` (`stand_pruefen` grün) | 3899 | 60 | 470 | 816 | 16 von 19 Reihen grün; rot: familien-leitung 77/78 (Test veraltet, N3-Listen), symbole 37/39 (Revit-Fassung der N3-Symbole fehlte), nachbearbeitung-n3 nicht gelaufen (Portwächter) – alle drei danach behoben |
| Teil O (hlks-modul, `1da72bd`, mit Nacharbeit `1e106e9`) | – | – | 470, nach Nacharbeit 474 | 857, nach Nacharbeit 858 | **ki-palette 144, nach Nacharbeit 156** (neu, mit Attrappe); menueband 191 (beide Symbolstile), dialoge-paletten 147, anordnung 87, start 111, hilfe 89, symbole 39 |
| `e791723` (mit Teil K, ohne Teil O; `stand_pruefen` grün) | 3965 | 63 | 471 | 817 | 20 Reihen / 21 Läufe grün ohne Wiederholung → geprüfter Stand `e791723` (Marke `geprueft-20261020-0146`) |
| Nach Zusammenführen mit Modul 4 A-O (Hauptlauf) | – | – | – | 875 von 887 (12 übersprungen) | – |
| **Endstand-Kandidat `6dc390e`** | Lauf offen | | | | läuft (hlks-test, Ports 5190–5199) |

Gesamtsumme für `6dc390e`: **steht aus** (Abschnitt 8). Vergleich Modul 3b (Stand 19.10.2026): Kern 3431, MCP 42, Web 148, Node 456 – die Zahlen sind seither auch durch S, N3 und Modul 4 gewachsen, ein Anteil nur für 3c ist aus `zuteilung.md` nicht herauszurechnen.

## 4. Abnahmelauf mit echtem Claude (Hauptlauf, 20.10.2026)

Beispielprojekt `%USERPROFILE%\HLKS_CAD_build\abnahme3c`, Testserver 5152, Gespräch «Abnahme 3c» (`20261020-020626-b3ef`), Akteur CS, Modell Sonnet, Internet aus, Claude Code 2.1.299.

| Schritt | Frage / Aktion | Ergebnis |
| --- | --- | --- |
| 1 | «Benenne den ausgewählten Strang um …» (Kontext: Auswahl KW-S1, kein Bild) | `element_aendern` ✓; Antwort: «Der Strang KW-S1 heisst jetzt «Steigstrang Bad/Küche Nord». Sonst habe ich nichts geändert, und es sind keine neuen Prüfbefunde dazugekommen.» Änderung mit Paket gelistet. |
| 2 | Zurücknehmen des Pakets | HTTP 200, Name wieder «Steigstrang Bad/Küche»; Paket als zurückgenommen geführt (Zeit 02:06:45). |
| 3 | Normfrage Dichtheitsprüfung Grundleitungen («nur mit Zitat») | `normen_suchen` ✓, `norm_lesen` ✓; Zitat suissetec-Merkblatt, Juni 2018, S. 1, drei Auszüge in Anführungszeichen, Hinweis auf S. 5 (Protokollblatt) und S. 6 (Quellen); «Prüfverfahren und Prüfwerte habe ich nicht gelesen»; Hinweis 62/315 Normen mit Volltext; «SN 592000 … nicht als Treffer gefunden». |
| 4 | Server neu gestartet; Folgefrage ohne Werkzeuge | Gespräch mit 4 Nachrichten da; Antwort «hiess vorher «Steigstrang Bad/Küche»». |

Beobachtungen (meine, aus Gesprächsdatei und Protokoll):

- Der Kontext der ersten Frage enthielt nur die **Auswahl** (kein Ansicht- und kein Geschoss-Eintrag). Die Palette schickt Ansicht und Geschoss standardmässig mit; der Lauf ging deshalb wahrscheinlich über die Schnittstelle des Servers und nicht über die Palette im Browser (Schlussfolgerung, nicht beobachtet).
- Die Antwort auf die Normfrage nennt keine Ziffer, weil das Merkblatt keine hat; sie nennt die Fundstelle sonst vollständig (Dokument, Ausgabe, Seite, Abschnitt).
- Claude hat in Schritt 3 nichts über SN 592000 behauptet, sondern die Lücke genannt.

## 5. Befunde und ihre Behebung (aus `zuteilung.md`)

| # | Befund | Fund | Behebung |
| --- | --- | --- | --- |
| 1 | Internet-Schalter gab alle Projektwerkzeuge zusätzlich frei | Hauptlauf bei Teil K | `40bbe07`: mit Internet nur Web, Normen, Familien/Unterlagen; `3800adb`: mit Internet kein Projektkontext, eigene Sitzung ohne Verlauf |
| 2 | Verbotsliste strenger als die Spezifikation (23 Werkzeuge: zusätzlich Status, BCF, Plan-/IFC-Ausgaben, Bibliothek) | Teil K | als Annahme in Abschnitt 6 |
| 3 | Vom Menschen reservierte Auswahl blockierte Änderungen von Claude («Fertig, wenn» verletzt) | Hauptlauf bei Teil O | `1e106e9`: `Projekt.FreigebenFuer`, Hinweis «Auswahl für Claude freigegeben» |
| 4 | Kein sichtbarer Einstieg (nur Kürzel und Häkchen in einer Liste) | Hauptlauf bei Teil O | `1e106e9`: Knopf «Claude» in der Kopfleiste neben dem «?» |
| 5 | Doppelte N3-Revit-Symbole | Hauptlauf bei Teil O | `1e106e9` |
| 6 | Neuladen mitten in einer Antwort konnte Text doppeln oder auslassen | Teil O | Text-Ereignisse tragen `ab` (Zeichen vor dem Teil), Test `GespraechLaufTests.Langsam_TextKommtInTeilen` |
| 7 | 1 Kern-Test (Zeitverhalten) unter Last rot, einzeln grün | Teil K | kein Befund am Produktcode, lastabhängig |
| 8 | Reihen familien-leitung, symbole, nachbearbeitung-n3 im Zwischenlauf rot | Zwischenstand `6f7acca` | fremd zu 3c (Test veraltet, Revit-Fassung, Portwächter); vom Hauptlauf bzw. in Auftrag S behoben |

Aus meiner Sicht **keine offenen Befunde am Produktcode**; ich habe keine Tests ergänzt oder geändert.

## 6. Annahmen zur Bestätigung

Bitte mit Ja/Nein beantworten. Jede ist als Einstellung oder kleiner Eingriff änderbar.

1. **A1 Position:** Palette rechts angedockt (Vorgabe), frei andockbar (links, rechts, unten, Reiter, schwebend), beim ersten Mal 380 px breit. Passt?
2. **A2 Kontext automatisch:** aktive Ansicht, Geschoss und Auswahl gehen mit jeder Frage mit, als Chips sichtbar und je Frage abwählbar. Einverstanden?
3. **A3 Verlauf je Projekt:** ein Gespräch läuft weiter; «Neu», früheres Gespräch wählen, «✕» entfernen (Datei bleibt unter `ki\geloescht`). Gespräche liegen im Projektordner (`ki\gespraeche`, mit Protokoll `.log`). Einverstanden?
4. **A4 Modell und Internet:** Sonnet als Vorgabe, Opus und Haiku wählbar; Internet aus als Vorgabe, Schalter gilt bis zum Umschalten (je Projekt gemerkt). Einverstanden?
5. **Internet = ohne Projektzugriff** (Entscheid Hauptlauf 20.10.2026): mit Internet sieht Claude weder Ansicht noch Geschoss noch Auswahl noch Bild, kann das Modell weder lesen noch ändern und hat keine Erinnerung an frühere Fragen (eigene Sitzung); die Palette zeigt es dreifach. Der Preis: «Suche im Internet nach einem Produkt und trage es ins Projekt ein» geht nicht in einer Frage. Einverstanden?
6. **Verbotsliste strenger als die Spezifikation:** 23 gesperrte Werkzeuge (Familien freigeben und speichern, Konflikte, Werte bestätigen, Bibliothek, alle HKAP, Aussparungs- und Kollisionsstatus, BCF, Aussparungs-IFC, Plan- und Serien-PDF, `unterlage_aus_url` nur mit Internet). Zu streng, zu locker?
7. **Auswahl wird für Claude freigegeben:** beim Start einer Frage ohne Internet gibt KICAD deine Reservierungen auf diesem Gerät für die mitgeschickte Auswahl frei; beim nächsten eigenen Ändern reserviert es neu. Reservierungen anderer Arbeitsplätze bleiben. Einverstanden?
8. **Bild der Ansicht ist als Vorgabe aus** (Chip wählbar); das Bild geht nur mit der Frage an Claude und wird nicht gespeichert. Lieber an?
9. **Knopf «Claude» in der Kopfleiste und Kürzel KI** (kein Menüband-Knopf, das Register Ansicht ist voll). Passt der Ort?
10. **Normenverzeichnis:** Vorgabe `W:\05_Normen und Merkblätter`, Zwischenspeicher lokal unter `%LOCALAPPDATA%\HLKS_CAD\normen` (nie OneDrive, lizenzierte Texte), nur deutsche Fassungen. Einverstanden?
11. **Zeitlimit 10 Minuten je Frage.** Reicht das?

## 7. Abnahme-Prüfschritte für Christian

Voraussetzung: KICAD läuft auf dem geprüften Stand (Zweig «geprueft»; `6dc390e` erst nach grünem Endlauf), Beispielprojekt offen, Claude auf dem Rechner angemeldet.

1. **KI** tippen oder den Knopf «Claude» oben rechts klicken: Palette erscheint rechts; nochmals klicken: weg.
2. Im Grundriss den Strang **KW-S1** wählen. Die Chips «Ansicht», «Geschoss», «Auswahl: KW-S1» stehen an.
3. Fragen: «Benenne den ausgewählten Strang um in «Steigstrang Bad/Küche Nord». Ändere sonst nichts.» – **Strg+Enter**. Antwort und Änderungsliste ansehen.
4. Auf das Element in der Liste klicken (wählt es), den Namen in den Eigenschaften lesen, dann **«Zurücknehmen»**: der alte Name ist zurück.
5. Reiter «Normen»: «Dichtheitsprüfung Grundleitungen» suchen; danach im Gespräch dieselbe Frage «nur mit Zitat» stellen. Fundstelle mit dem Merkblatt vergleichen.
6. «Internet» einschalten: Hinweisband und ausgegraute Chips; eine allgemeine Frage stellen; Marke «Internet · ohne Projektzugriff».
7. Ein Element in den Eigenschaften selbst ändern, es gewählt lassen, Claude es ändern lassen: Hinweis «Auswahl für Claude freigegeben».
8. KICAD beenden, neu starten, Projekt öffnen, Palette einblenden: das Gespräch ist da; eine Folgefrage («Wie hiess der Strang vorher?») stellen.
9. «Neu», eine Frage, im Gespräch-Menü wechseln, ein Gespräch mit «✕» entfernen.
10. Chip «Bild der Ansicht» einschalten und «Was siehst du im Plan?» fragen.
11. Claude bitten, eine Familie freizugeben: erwartet wird, dass er es nicht kann.

## 8. Offene Punkte

| # | Punkt | Wer / wann |
| --- | --- | --- |
| 1 | Endlauf `6dc390e` (Kern, MCP, Web, Node, alle Browser-Reihen) – Zahlen in Abschnitt 3 nachtragen | hlks-test / Hauptlauf |
| 2 | Palette mit **echtem** Claude bedient (Browser): nur Abnahme durch Christian (Abschnitt 7); der Abnahmelauf ging wahrscheinlich über die Server-Schnittstelle | Christian |
| 3 | Mit echtem Claude nicht geprüft: Internet-Lauf aus der Palette, Bild der Ansicht, Modelle Opus und Haiku, Abbrechen, Zeitlimit | Christian / Hauptlauf |
| 4 | Normenverzeichnis: Volltext erst für 62 von 315 Normen; SN 592000, SN EN 12056 und SVGW W3 liegen nicht vor (Modul 4 Teil B) | Christian (beschaffen) |
| 5 | Folgen auf einem zweiten Rechner (Sitzung weg, `--resume` schlägt fehl, neue Sitzung mit Verlauf) nur durch Tests mit der Attrappe belegt | Christian, zweiter Rechner |
| 6 | Rechner ohne Abo: Meldung «Claude nicht verfügbar» nur mit der Attrappe geprüft | Christian |
| 7 | Bedienungsanleitung: Kapitel «KI-Palette Claude» (14 Funktionen) ist im Verzeichnis, Bilder folgen mit Szenen im Bildskript | Hauptlauf / hlks-modul |

## 9. Zuteilung und Modellwahl (aus `zuteilung.md`)

| Teil | Inhalt | Agent | Ergebnis |
| --- | --- | --- | --- |
| – | Spezifikation und Annahmen A1–A4 | Hauptlauf | ok |
| K | Gesprächsdienst, Zurücknehmen, Normensuche, MCP `normen_suchen`/`norm_lesen`/`kontext_bild` | hlks-kern | Nacharbeit (Internet-Schalter, Befund 1) |
| O | Palette, Gespräche, Chips, Normen, Zurücknehmen | hlks-modul | Nacharbeit (Reservierung, Knopf, Symbole, Befunde 3–5) |
| – | Abnahmelauf mit echtem Claude | Hauptlauf | ok |
| – | Testbericht (dieser Entwurf) | hlks-test | Entwurf |

Je eine Nacharbeit bei hlks-kern und hlks-modul; die Schwelle «zwei Nacharbeiten je Aufgabenart» ist nicht erreicht, ich schlage **keine Änderung** der Zuteilungstabelle vor. Beide Nacharbeiten betrafen Lücken der Spezifikation (Internet, Reservierung) mehr als Agentenfehler. Entscheid beim Hauptlauf.

## Bekannte Grenzen dieser Prüfung

- Ich habe keinen Test gestartet; alle Testzahlen stammen aus `zuteilung.md`. Der Endlauf auf `6dc390e` fehlt.
- Die Palette ist nur mit der Attrappe im Browser geprüft; der Abnahmelauf mit echtem Claude ging (wahrscheinlich) über die Server-Schnittstelle.
- Ein einzelner Lauf mit einem Modell (Sonnet) und einem Projekt (Beispielprojekt); das Verhalten von Claude ist nicht deterministisch.
- Normwerte habe ich weder geprüft noch eingetragen; das Zitat der Normfrage stammt aus der Antwort von Claude und wurde von mir nicht am Merkblatt nachgeprüft.
- Kein unabhängiger Prüfer (hlks-pruefer) für 3c vorgesehen; die Prüfer-Regel gilt für die Rechenkerne und M3/M5.

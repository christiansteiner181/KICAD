# Testbericht Modul G «Gesten am Arbeitsplatz», Stufe 1 (Abnahme nicht abgeschlossen, Tuning im Labor)

Stand 26.10.2026 · Geprüft: Zweig `claude/modul-gesten` @ `8079195` (Bau hlks-kern), mit `main` @ `7e6dd1f` zusammengeführt in der Arbeitskopie von hlks-test (Merge-Commit auf
Zweig `worktree-agent-a68ae3340b821a2ba`; Konflikte nur in `zuteilung.md`, `src/Hlks.Web/OBERFLAECHE.md`, `entwurf/modul-gesten.md`, beide Seiten behalten, Entwurf = Fassung des Zweigs) ·
**Entwurf, Abnahme durch Christian offen.** Der Zweig `claude/modul-gesten` ist während des Laufs auf `e6dcce6` («Doku Erkennung und Navigation …») weitergerückt; dieser Commit ist
nicht mitgeprüft (laut Meldung nur Doku, nicht nachgesehen).

Nach Vorgabe vom 26.10.2026 («nur spezifische Unit-Tests») liefen nur die im Auftrag genannten gefilterten Läufe, je einmal. Kein Produktcode geändert.

## 1. Testzahlen

| Lauf | Filter | Ergebnis |
| --- | --- | --- |
| Node | `gesten-logik`, `gesten-datenschutz`, `gesten-ausloesen`, `navigieren`, `ansichten`, `schema`, `maus` (je `*.test.mjs`) und dazu `oberflaeche/xr-register.test.mjs` | **261 Tests, 256 bestanden, 0 Fehler, 5 übersprungen** |
| Web | `GestenAuslieferungTests`, `GestenEinstellungTests`, `BenutzerEinstellungen`, `ApiTests.Oberflaeche_AlleReferenziertenJsUndCssDateienWerdenMitPassendemInhaltstypGeliefert` | **147 bestanden, 0 Fehler, 0 übersprungen** (5 s) |
| Kern | kein Lauf | `git diff --stat ba43f8b..HEAD -- src/Hlks.Kern` ist leer: Kern unverändert |
| Browser | `gesten.browser.mjs` (einmal) | **36 ok, 0 Fehler** |
| Bildrate | `werkzeuge/gesten_bildrate.mjs` (neu, 2 Läufe mit 60-Hz-Grenze bzw. ohne; dazu 1 Kontrolllauf, siehe 2) | Zustandsprüfungen 8 ok, 0 Fehler (Kamera/Worker) |

Anmerkungen:
- Die 5 übersprungenen Node-Tests sind «Echte Daten …» bzw. «Echter Kern …» (kein Testserver erreichbar, Tests überspringen sich selbst); sie gehören nicht zu den Gesten.
- Die Kürzel-/Menüband-Tests, die `ansicht.gesten` prüfen: ein Treffer unter `*.test.mjs` auf `ansicht.gesten` ist `gesten-ausloesen.test.mjs` (im Lauf enthalten). Zusätzlich habe ich
  `xr-register.test.mjs` genommen (prüft die Menüband-Tabelle in `OBERFLAECHE.md` nach dem Zusammenführen mit Register XR; grün). Ein eigener Kürzel-Test «GE» fand sich nicht.
- «Neu» gegenüber dem Stand vor Modul G: die Gesten-Tests (Node) und `GestenAuslieferungTests`, `GestenEinstellungTests` (Web) kommen mit dem Zweig; ich habe keine Zahl «vorher» gemessen,
  da keine Gesamtläufe erlaubt waren.
- Gebaut wurde in einen eigenen Ausgabeordner (`HLKS_BUILD=C:\Users\chris\HLKS_CAD_build_gesten_test`), damit der gemeinsame Bau unter `%USERPROFILE%\HLKS_CAD_build` unangetastet blieb.

## 2. Bildrate der 3D-Ansicht (Skript `werkzeuge/gesten_bildrate.mjs`)

Messart: Edge **headless** (`--headless=new`), 1280 × 800, Software-Rendering (SwiftShader), Beispielprojekt «MFH Musterweg», 3D-Ansicht AN-3D; je Bild ein Drehschritt über
`navigieren({ drehen })`, requestAnimationFrame über 20 s je Zustand. Gesten werden wie am Schalter der Statusleiste über `steuerung.schalten(true/false)` ein- und ausgeschaltet;
Testkamera `--use-fake-device-for-media-stream`. Fühler vor der Seite umhüllen `getUserMedia` und `Worker` («KICAD-Gesten»).

**Lauf 1, mit 60-Hz-Grenze:** a 59,9 · b 60,0 · c 60,0 Bilder/s. Die Messung klemmt an den 60 Hz (Szene ist leicht), sie sagt über die Last nichts aus (Differenz 0,2 %). Als Beleg nur
für «bei 60 Hz keine verlorenen Bilder» brauchbar (grösste Bildzeit mit Gesten 17 ms).

**Lauf 2, ohne Grenze** (`HLKS_BR_UNGEKLEMMT=1`: `--disable-frame-rate-limit --disable-gpu-vsync`), mit einem nicht gewerteten Anlauf von 20 s:

| Zustand | Bilder/s | Bildzeit Mittel | 95-%-Bildzeit (= 5-%-Quantil der Rate) |
| --- | --- | --- | --- |
| (Anlauf, nicht gewertet) | 502 | 2,0 ms | 3,3 ms (303/s) |
| (a) Gesten aus | 367,4 | 2,7 ms | 3,7 ms (270/s) |
| (b) Gesten an, Testkamera | 331,6 | 3,0 ms | 4,0 ms (250/s) |
| (c) wieder aus | 356,0 | 2,8 ms | 3,8 ms (263/s) |

Differenz (b) gegen (a): **−9,7 %**; gegen (c): −6,8 %; gegen das Mittel aus a und c (361,7/s): **−8,3 %**. Messrauschen (c gegen a): −3,1 %. Im Worker lief die Erkennung mit 22 Bildern/s,
20 bis 22 ms je Bild (CPU), nach 10 s ohne Hand im Ruhebetrieb mit 5 Bilder/s.

Einordnung: Das Kriterium «höchstens 10 % unter der Bildrate ohne Gesten» ist in dieser Anordnung **knapp erfüllt** (−8,3 % gegen das Mittel, −9,7 % gegen (a) allein, Streuung ±3 %). Die
Anordnung ist ungünstig: Software-Rendering und der Erkennungs-Worker (CPU, kein GPU-Delegate) teilen sich dieselben Kerne, und das Rendern läuft ohne Bildbegrenzung. In der absoluten Bildzeit
sind das +0,3 ms je Bild; die Bildrate bei 60 Hz bleibt unverändert (Lauf 1). Auf dem Desktop mit echter Grafikkarte und 60 Hz Anzeige ist deshalb keine spürbare Abnahme zu erwarten, die
Messung dort steht aber aus (siehe 4).

Befund zur Messung selbst (Kontrolllauf `HLKS_BR_KONTROLLE=1`, ohne Gesten): die **erste** 20-s-Messung nach dem Start liegt in dieser Umgebung um etwa 25 bis 30 % höher als alle
folgenden (546 gegen 404 und 406/s), ohne dass Gesten im Spiel sind. Ursache nicht geklärt (vermutet: Warmlauf/Taktung des Rechners oder Wiederverwendung gepufferter Zeichnung in der
ersten Phase; kein Befund zum Produktcode). Darum wertet das Skript einen vorgeschalteten Anlauf nicht und vergleicht (b) mit dem Mittel aus (a) und (c). Ein erster Lauf ohne Anlauf zeigte
sonst scheinbar −31 % (b gegen a), obwohl (c) mit ausgeschalteten Gesten gleich tief lag wie (b): das war reiner Reihenfolgeeffekt, keine Last der Gesten.

### Gesten aus: Kamera aus, keine Last

Geprüft über die Fühler (beide Läufe):
- Vor dem Einschalten und während (a): `getUserMedia` nie aufgerufen, kein Track «live», kein Worker «KICAD-Gesten» (gebaut minus beendet = 0). **ok**
- (b): `getUserMedia` mindestens einmal, Track live, genau 1 Worker. **ok**
- Nach dem Ausschalten (c): kein Track «live», Worker beendet, während (c) keine neue Kamera. **ok**
- Beim Schliessen versucht MediaPipe eine Protokollanfrage an `https://odml.pa.googleapis.com/v1/log`; sie wird von der Netzsperre abgewiesen (Browser-Test, «gesperrte Anfragen»). Kein Fehler,
  aber ein Hinweis, dass die Bibliothek nach aussen melden will. Nur Fakt, keine Wirkung.

## 3. Erkennung der Gesten (aus `gesten.browser.mjs`)

Mit der Testkamera (Testbild ohne Hand) lässt sich nur der Betrieb prüfen, nicht die Trefferquote:
- Start bis «bereit» 479 ms (Laden des Modells 242 ms), Erkennung 22,0 Bilder/s, 19,9 ms je Bild im Worker (CPU, kein Rückfall).
- Modell und WASM nur aus `/lib/mediapipe/`; der Worker fragt keinen anderen Server.
- Testbild ohne Hand: keine Schritt-Geste (kein Fehlauslösen im Leerlauf).
- Fenster versteckt: Kamera frei; wieder sichtbar: Kamera an, 22,1 Bilder/s. Ruhebetrieb nach 10 s ohne Hand: 4,9 Bilder/s (Soll 5). Stoppen: Zustand «aus». Keine Ausnahmen, keine Fehler in der Konsole.
- Navigation: Rollen um die Blickachse, Drehen, Zoom, Verschieben in 3D; Haus-Knopf und «alles zeigen» richten auf; Grundriss und Schema verschieben und zoomen (Drehen/Rollen dort ohne Wirkung).

**Eine Erkennungsrate der Gesten (Treffer je Geste) gibt es aus dieser Messung nicht**, weil die Testkamera keine Hand zeigt. Sie liegt nur als Logiktest vor (`gesten-logik.test.mjs`,
`gesten-ausloesen.test.mjs`: synthetische Handpunkte, Pistolen-/Faust-/Wisch-Erkennung, Totbereich, Glättung) und muss am Desktop mit echter Hand gemessen werden.

## 4. Nur am Desktop mit echter Kamera prüfbar (offen)

1. Echte Hände: Erkennungsrate je Geste (Pistole, Faust, Wisch) bei verschiedenen Lichtverhältnissen, Hintergründen, Handhaltungen.
2. Fehlauslösungen in 10 Minuten normaler Arbeit (tippen, telefonieren, zur Tasse greifen): Vorgabe höchstens eine; Messung mit Christian am Projekt Test_praxis.
3. Händigkeit: Vorgabe links, einstellbar; rechts und die Umschaltung mit echter Hand prüfen.
4. Bildwinkel 60° der eingebauten Kamera: der Bildwinkel steckt als Annahme in der Umrechnung in Weltkoordinaten; die Testkamera taugt dafür nicht.
5. Bildrate mit echter Grafikkarte und 60-Hz-Anzeige (nicht headless, nicht Software-Rendering); Skript mit `HLKS_BR_SICHTBAR=1` und gegebenenfalls `HLKS_BR_UNGEKLEMMT=1` am Desktop laufen lassen
   (nicht auf dem laufenden Desktop-Port, Server auf Port 5172). Sinnvoll auch mit dem GPU-Delegate statt CPU.
6. Kameraerlaubnis des Browsers, LED der Kamera aus nach dem Ausschalten, Verhalten bei fremd belegter Kamera.

## 5. Bekannte Grenzen und offene Punkte

- Die Bildrate wurde headless mit Software-Rendering gemessen (Werte oben); der Kriterienwert −8,3 % liegt nahe an der Grenze von 10 %. Eine Wiederholung am Desktop ist nötig, bevor der Punkt
  als erfüllt gilt.
- Reihenfolgeeffekt der Messung (erste Messung nach Start 25 bis 30 % höher) nicht erklärt; durch Anlauf umgangen.
- Das Skript nimmt `HLKS_BUILD` wie die Browsertests; ohne diese Variable läuft es auf dem gemeinsamen Bau unter `%USERPROFILE%\HLKS_CAD_build` (kann den Stand eines anderen Laufs sein).
- Nicht mitgeprüft: Commit `e6dcce6` des Zweigs; Browserreihen anderer Module; Kern- und MCP-Tests (nicht verlangt, Kern unverändert).
- Keine Befunde im Produktcode: kein Test wurde fehlgeschlagen, kein Test mit `Skip` angelegt.

## 6. Abnahme am Desktop mit Christian (26.10.2026, Prüfserver 5176, Test_praxis auf W:, ASUS FHD webcam)

Ergebnis: **nicht abgeschlossen.** Christian, 26.10.2026: «wir beenden hier. wir machen das tuning in der labor app.» Die «Fertig, wenn»-Liste ist am Desktop
nicht erfüllt; Fehlauslösungen in 10 min und Bildrate mit Grafikkarte wurden nicht gemessen.

| Befund Christian | Ursache / Änderung | Commit |
|---|---|---|
| «gesten haben keine reaktion», Skelett nur bei der rechten Hand | Händigkeit wurde getauscht; MediaPipe 1.1.1 nennt im ungespiegelten Bild schon die Hand des Benutzers | e34f2c1 |
| «zu kleiner Raum vor Kamera» | 640 × 480 war ein 4:3-Ausschnitt; jetzt 1920 × 1080 (volle Sensorgrösse), Erkennungsbild 960 breit; Messzeile im Dialog | 7a35d67, af1ee51, c7c8926 |
| Aufnahme nicht gespeichert (Download im App-Fenster kam nicht an) | Speicherdialog des Browsers (`showSaveFilePicker`) | 7a35d67 |
| «geste bricht oft ab» (auch nach 1–2 s) | Schwellen der Erkennung zurück auf 0,5; Lücken bis 350 ms überbrückt; Ursache nicht belegt → Abbruchprotokoll | af1ee51, ec98087, c3aca8b |
| «alle operationen abdämpfen» | Drehen 3°/cm, Verschieben 2 %/cm, Rollen 1 : 2, Zoom-Exponent 1,25, Glättung 160/250 ms | ec98087 |
| «zoom, ausschalten» | G5 aus (`ZOOM_GESTE = false`), Liste, Doku, Anleitung | 448e2a3 |
| «speichere weshalb die gesten … abbrechen» | Abbruchprotokoll `%LOCALAPPDATA%\HLKS_CAD\gesten\abbrueche.jsonl` (Grund, Dauer, Masse; nur Zahlen) | c3aca8b |

Gezielte Läufe nach den Änderungen: Node `gesten-logik` 51/51, `gesten-ausloesen` 51/51, `gesten-datenschutz` 7/7, `gesten-protokoll` 2/2,
`navigieren`/`ansichten`/`menueband` 110 (2 übersprungen), `xr-register` 15/15; Web `GestenProtokollTests` 7/7. Gesamtlauf: durch `stand_pruefen.ps1` des Hauptlaufs.

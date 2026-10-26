# KICAD (HLKS_CAD) – Regeln für Sitzungen in diesem Repository

Eigenes BIM-Werkzeug der H+K Planungs AG für Heizung, Lüftung, Sanitär und MSRL (Überblick: `LIESMICH.md`). Am Windows-PC von
Christian Steiner gilt zusätzlich die übergeordnete `CLAUDE.md` im Ordner darüber; diese Datei ergänzt sie und gilt vor allem für
**Cloud-Sitzungen** (seit 21.10.2026), die nur dieses Repository sehen.

## Verbindlich, überall
- **Normwerte nie aus dem Gedächtnis**, nur mit Quelle (Norm, Ausgabe, Ziffer, Seite). Normtexte (SIA u. a.) nie ins Repository.
  Fehlt eine Quelle: Annahme kennzeichnen und im Bericht als offene Frage an Christian nennen.
- **Keine echten Projektdaten** (Pläne, Modelle, Bilder, Unterlagen von Kunden) ins Repository. Testdaten sind fiktiv, Beispielprojekt
  «MFH Musterweg». Die Echt-Tests lesen `~/HLKS_CAD_testdaten` und überspringen sich, wenn die Dateien fehlen.
- **Nicht anfassen:** `schema/bcf-3.0`, `schema/ids-1.0`. **PDFsharp** bleibt auf 6.2.4 (ab 6.5 AGPL, siehe `Hlks.Kern.csproj`).
- **Keine Geheimnisse** ins Repository; Schlüssel nur als Umgebungsvariable.
- **Sprache:** Deutsch (Schweiz, «ss» statt «ß») in Namen, Kommentaren, Oberfläche und Berichten, wie im bestehenden Code.
- Neue oder geänderte Funktionen: Tests dazu, aber nur so viele wie effektiv nötig (Christian 26.10.2026) – sie sichern
  echtes Verhalten ab, nicht Quelltext oder Texte; Bedienung in `src/Hlks.Web/OBERFLAECHE.md` und `berichte/anleitung/inventar.json`
  nachführen, wie in den bestehenden Einträgen.

## Cloud-Sitzungen (Linux)
- **Zweig:** Arbeiten auf einem eigenen Zweig `cloud/<thema>`. Nie auf `main` oder `geprueft` pushen, nie Marken setzen. Der Hauptlauf
  am Windows-PC holt den Zweig, prüft ihn mit `werkzeuge/stand_pruefen.ps1` und führt ihn zusammen.
- **Commits:** `git config user.name "Christian Steiner"` und `git config user.email "christian.steiner@hkplanung.ch"`, Meldung
  deutsch, Trailer `Co-Authored-By` wie von der Sitzung vorgegeben.
- **Tests:** immer gefiltert auf das Geänderte, z. B. `bash werkzeuge/cloud/tests.sh kern -- --filter "FullyQualifiedName~Stuetze"`.
  `bash werkzeuge/cloud/tests.sh` (alles) nur, wenn der Auftrag es ausdrücklich verlangt (Christian 26.10.2026: «nur
  spezifische Unit-Tests»). Vor dem letzten Commit müssen die betroffenen Teile grün sein.
- **Nicht verfügbar:** PowerShell-Werkzeuge (`werkzeuge/*.ps1`), Browserreihen (`*.browser.mjs`, Edge), Abnahmeserver, HKAP,
  Laufwerk W:, echte Testdaten. Was nur dort prüfbar ist, im Schlussbericht ausdrücklich als «nicht geprüft» nennen.
- **Schriften:** Arial und Calibri sind durch Liberation Sans und Carlito mit gleichen Laufweiten ersetzt (`werkzeuge/cloud/einrichten.sh`).
- Windows-Verhalten nie verschlechtern: Pfade mit `Path.Combine`, Gross-/Kleinschreibung von Dateinamen genau wie im Repository.
- **Schlussbericht** der Sitzung: Zweig, Commits, Testzahlen (Kern / Mcp / Web / Node), was nicht geprüft ist, offene Fragen.

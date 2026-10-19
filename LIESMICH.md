# HLKS-CAD

Eigenes BIM-Werkzeug der H+K Planungs AG für Heizung, Lüftung, Sanitär und MSRL – «sozusagen ein Revit für
Gebäudetechnik». Factsheet und Ablaufplan: Claude-Doc «HLKS-CAD – Factsheet und Ablaufplan»
(https://claude.ai/code/artifact/1c4a4d98-1fcb-47f1-aa25-157fb8cdc06b). Stand: Modul 0a (Datenmodell, Kern-API).

## Leitprinzipien (verbindlich für jeden Baustein)

1. **Die KI versteht den Systemzusammenhang.** Das Modell denkt vom System her (Systemgraph mit Anschlüssen und
   Verbindungen); Geometrie hängt am Element, nicht umgekehrt.
2. **Eine Tür für alles.** Jede Funktion ist ein Befehl oder eine Abfrage des Kerns (`Projekt.Ausfuehren`,
   `Abfragen`). Oberfläche, KI (MCP) und Skripte benutzen dieselbe API. Keine Logik in der Oberfläche.
3. **Ein Element, viele Ansichten.** Grundriss, 3D, Schema, Listen zeigen dasselbe Element; Eigenschaften gibt es
   nur einmal.
4. **Jeder Wert hat eine Herkunft** (`Herkunft`: Art, Akteur, Zeit, Quelle). Normwerte nur aus den Normtexten
   mit Ausgabe, Ziffer und Seite – nie aus dem Gedächtnis. In Modul 0a gibt es keine Normwerte.
5. **Auslegung nach Norm, Simulation nach Physik** (ab Modul 4 bzw. 10).
6. **Pakete statt Überschreiben.** Jede Änderung ist ein neues Paket in `aenderungen/`; gemeinsame Dateien werden
   nie überschrieben. Rückgängig ist ein weiteres Paket.

## Aufbau

```
HLKS_CAD/
  Directory.Build.props     Build ausserhalb OneDrive (%USERPROFILE%\HLKS_CAD_build, mit HLKS_BUILD umlenkbar)
  HLKS_CAD.slnx
  kataloge/                 klassen.json, medien.json, systemtypen.json – feste Kataloge, selbsterklärend
  schema/                   hlks-modell.schema.json – Datenmodell mit deutschen Beschreibungen
  src/Hlks.Kern/            Kern-Bibliothek
    Modell/                 Grundtypen (Element, Wert, Herkunft, Anschluss, Geometrie, Verbindung, Operation, Paket),
                            Modell (Speicher im RAM, Anwenden von Operationen), Netz (Fliessrichtung, Verbraucher)
    Kataloge/               Katalog laden, Basisklassen auflösen
    Api/                    Befehle (ändern), Abfragen (lesen), Ableitungen (Raumliste, Strangliste, Mengen)
    Pruefung/               Prüfungen P01–P18 mit Vorschlag (P13–P18 Modul 3: Leitungen/, Schema/)
    Speicher/               IProjektSpeicher (+ OrdnerSpeicher für die NAS-Ablage)
    Datenquellen/           IDatenquelle (+ Simulator; später BACnet/OPC UA, nur lesend)
    Beispiel/               Beispielprojekt «MFH Musterweg» (fiktiv), entsteht vollständig über Befehle
    Projekt.cs              die eine Tür: Ausfuehren, Rueckgaengig, Wiederholen, Synchronisieren, Ereignis Geaendert
  src/Hlks.Mcp/             MCP-Server (stdio) und Kommandozeile über dem Kern
  tests/                    xUnit-Tests
  ki-test/                  KI-Verständnistest (Fragen, Erwartung aus dem Kern, Auswertung)
  zuteilung.md              Protokoll der Agenten-Zuteilung (siehe CLAUDE.md)
```

## Konventionen

- Längen in **mm**, Koordinaten x Ost, y Nord, z über ±0.00 (absolut). Ausgaben in m, m², m³ sind gekennzeichnet.
- Elemente werden über **Kennzeichen** angesprochen (Id geht auch). Kennzeichen sind eindeutig.
- Klassen, Eigenschaften, Medien, Systemtypen nur aus den Katalogen. Unbekannte Eigenschaft = Fehler mit Liste
  der erlaubten.
- Fliessrichtung steht an den Anschlüssen: `rein`, `raus`, `beide` (Formteile). Leitungen fliessen von `anfang`
  nach `ende`. `Netz.Folgen` geht stromab oder stromauf.
- Rolle im Netz (Katalog `rolle`): verbraucher (Endgerät) · quelle · senke · zentrale · leitung.
- Zuordnung: Leitungsbauteil → `teilVon` Strang (System wird übernommen) · Strang → `teilVon` System ·
  System → `teilVon` Anlage · Geschoss → `teilVon` Gebäude · Raum mit `geschoss`. Geschoss und Raum werden aus
  der Lage abgeleitet, wenn sie fehlen.
- Fehler sind `HlksFehler(meldung, vorschlag)`: verständlich für Mensch und KI, mit Weg zur Lösung.
- Texte, Kommentare und Bezeichner deutsch, Schweizer Schreibweise (ss statt ß).

## Ablage (NAS)

`W:\02_Projektdateien Objekte\<Projekt>\c_Pläne und Modelle\e_KICAD\`

```
projekt.json              Kopf (Id, Name, Schemaversion)
stand\stand-<zeit>.json   Standabbilder (Elemente, Verbindungen, enthaltene Pakete)
aenderungen\<paket>.json  Änderungspakete, je Befehl eines, nie überschrieben
sperren\<element>.json    Reservierungen (wer bearbeitet gerade was), atomar angelegt
unterlagen\               IFC, DWG, PDF, JPG (ab Modul 1b)
```

## Bauen und testen

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build HLKS_CAD.slnx
& "C:\Program Files\dotnet\dotnet.exe" test HLKS_CAD.slnx
```

Mehrere Bauläufe gleichzeitig (z. B. parallele Agenten): vorher `$env:HLKS_BUILD = "$env:USERPROFILE\HLKS_CAD_build\<name>"`
setzen, sonst sperren sich die obj-Ordner gegenseitig. Kataloge findet der Kern über `HLKS_KATALOG` oder durch
Suchen nach `kataloge/` aufwärts vom Programmordner bzw. Arbeitsverzeichnis.

## Regeln für Bau-Agenten

- Nur in den Dateien arbeiten, die der Auftrag nennt, und nur unter `HLKS_CAD/`. Nie auf W:.
- Keine Normwerte, keine Rechenregeln erfinden; fehlt etwas, melden.
- Zu jedem Teil Tests schreiben; vor «fertig» alle Tests laufen lassen.
- Scheitert der Build an Dateien ausserhalb des eigenen Auftrags (paralleler Agent), 1–2 Minuten warten und erneut
  versuchen – fremde Dateien nie ändern.

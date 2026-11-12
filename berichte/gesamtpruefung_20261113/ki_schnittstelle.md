# Gesamtprüfung KICAD, Runde 4 – Bereich `ki_schnittstelle`

Stand: Zweig `worktree-agent-ac8fbb5e996eef2ec` (Basis `ad973df`). Geprüft: `src/Hlks.Mcp/` (alle Werkzeugdateien, Hüllen, Filter, Werkzeugauswahl),
`src/Hlks.Kern/Sicherheit/KiPfade.cs` samt allen Aufrufstellen, `src/Hlks.Kern/Api/Tuer.cs`, `Projekt.Ausfuehren(…, akteurArt)`, die Sperren für die
Akteur-Art «ki» im Kern, `src/Hlks.Web/KiEinordnung.cs`, `GespraechDienst.cs` (Verbotsliste, `Fragen`, Kontext, Bild), `GespraechEndpunkte.cs`,
`AuftragDienst.cs` (Start, Eingabeprüfung), `AuftragEndpunkte.cs`, `Herkunftsschutz.cs`, `KiUmgebung.cs`, die Datei-Endpunkte
(`/api/baustelle/datei`, `/api/baustelle/eingang/{datei}`, `/api/ifc/export/datei`, `/api/unterlagen/{id}/bild|netz`, `/hilfe/`), der Upload
`/api/unterlagen` und `unterlage_aus_url`. Nicht noch einmal geprüft: Server-Fehlerbehandlung und Baustellen-Zugang (Bericht `oberflaeche.md`),
`familie_laden` (Bericht `import_familien_baustelle.md`), `Param.Bool` (Bericht `schema_api.md`, Punkt 8).

## 1. Kurzfazit

Die KI-Schnittstelle ist sorgfältig gebaut: Namen, Beschreibungen, Pflicht/optional und Typen der 206 Werkzeuge sind über die Abdeckungstests an den Kern
gebunden; ein eigener Abgleich (Skript über alle Werkzeugmethoden) hat zusätzlich bestätigt, dass jedes Werkzeug jeden seiner Parameter unter dem
richtigen Namen an den Kern weiterreicht. Die Pfadprüfung `KiPfade` hält den nachgerechneten Sonderfällen stand, die Datei-Endpunkte lassen keinen
Ausbruch zu, der Internetabruf sperrt das eigene Netz auch nach Weiterleitungen. Gefunden und korrigiert: (1) `rueckmeldung_lesen` – für die KI
erlaubt – nahm jeden Dateipfad ohne `KiPfade` (auch `\\fremder-server\…`); (2) acht Fachabfragen lasen im MCP-Server das Modell ohne vorherigen Abgleich
und konnten der KI einen veralteten Stand zeigen. Offen und zu entscheiden: Die Sperren «nur Mensch» hängen teils nur an der Verbotsliste der Palette,
nicht am Kern, und die Pfadgrenze gilt nicht für eine KI, die sich über HTTP je Aufruf meldet.

## 2. Korrigiert

| # | Datei | Fehler | Korrektur | Test (Filter, Ergebnis) | Commit |
|---|---|---|---|---|---|
| 1 | `src/Hlks.Kern/Baustelle/BaustelleRueckmeldung.cs` (`Laden`), `src/Hlks.Mcp/WerkzeugeBaustelle.cs`, Kommentar in `KiPfade.cs` | `rueckmeldung_lesen` steht in `KiEinordnung.Lesend` (für die KI erlaubt) und hat einen Parameter `datei`. `Laden` rief `File.Exists`/`File.ReadAllText` direkt auf – ohne `KiPfade.Pruefen`. Eine KI konnte damit (a) mit `\\fremder-server\x.json` eine SMB-Verbindung nach aussen auslösen (Befund B4 der Sicherheitsprüfung 3c), (b) über die drei verschiedenen Meldungen («gibt es nicht» / «kein gültiges JSON: …» / «keine Rückmeldung») prüfen, ob beliebige Dateien auf dem Rechner existieren und JSON sind. Das Werkzeug kam am 02.11.2026 dazu, nach der Liste in `KiPfade.cs` vom 20.10.2026. | `Laden(Projekt, JsonObject)` prüft den Pfad vor dem ersten Dateizugriff mit `KiPfade.Pruefen(p, datei, "rueckmeldung_lesen")`. Menschen unverändert. Liste im Kopf von `KiPfade.cs` um `rvt_als_ifc` (prüft schon über `IfcImport.Dateipfad`) und `rueckmeldung_lesen` ergänzt. | `dotnet test tests/Hlks.Kern.Tests --filter "FullyQualifiedName~KiSicherheitTests"` → 46/46 grün (1 neu: `Rueckmeldung_Ki_LesenNurUnterDenWurzeln_UebernehmenNieAuchJeAufruf`) | `ab8acfa` |
| 2 | `src/Hlks.Kern/Baustelle/BaustelleRueckmeldung.cs` (`RueckmeldungUebernehmen`) | Die Sperre «Rückmeldungen der Baustelle übernimmt der Mensch» fragte `k.Projekt.AkteurArt`. Eine KI, die über HTTP schreibt und sich mit `X-Hlks-Akteur-Art: ki` meldet (Rang 4, 30.10.2026: «Befehle nur für Menschen lehnen ab»), hat das Projekt des Servers (Art «mensch») – die Sperre griff nicht, das Paket trug trotzdem die Art «ki». | Sperre fragt `k.Herkunft.Art` (Art des Aufrufs), wie `FamilienFreigabe.NurMensch`. | wie 1 (derselbe neue Test: abgelehnt mit `akteurArt: "ki"` am Mensch-Projekt, angenommen ohne) | `ab8acfa` |
| 3 | `src/Hlks.Mcp/Werkzeuge.cs`, `WerkzeugeArchitektur.cs`, `WerkzeugeBaustelle.cs`, `WerkzeugeHeizung.cs`, `WerkzeugeLueftung.cs` | `datenpunktliste`, `lagen`, `vorwand_stueckliste`, `raumbezeichnungen_dwg`, `heizlast`, `heizkoerper_auslegung`, `rohr_druckverlust`, `kanal_druckverlust` liefen mit `synchronisieren: false`, lesen aber das Modell. Der MCP-Server ist ein zweiter Prozess auf dem Projektordner; ändert der Mensch in der Oberfläche einen Raum und fragt die KI danach als Erstes z. B. `heizlast`, rechnete sie mit dem alten Stand. Widerspricht dem Kopf von `Werkzeuge.cs` («Vor jedem Aufruf wird synchronisiert») und dem Test `Synchronisieren_FremdeAenderungSichtbarVorJedemWerkzeugaufruf`, der nur `element_lesen` prüfte. | Bei diesen acht Werkzeugen den Abgleich wieder eingeschaltet. Ohne Abgleich bleiben nur Abfragen ohne Modellbezug: `normen_suchen`, `norm_lesen`, `rechenweg`, `grundlagen`, `farben`, `mustersysteme`, `webablage_status`, und `synchronisieren` selbst. (Die Zeile für `raumbezeichnungen_dwg` in `WerkzeugeBaustelle.cs` ging mit Commit `ab8acfa` mit, weil die Datei dort schon geändert war.) | `dotnet test tests/Hlks.Mcp.Tests --filter "FullyQualifiedName~WerkzeugTests.Synchronisieren\|FullyQualifiedName~WerkzeugTests.Abdeckung"` → 5/5 grün (Test um `lagen` nach fremder Änderung von `ukRohdecke` erweitert) | `08ba67f` (+ `ab8acfa`) |

## 3. Gefunden, nicht korrigiert

| # | Datei:Zeile | Problem | Auswirkung | Vorschlag | Aufwand | Priorität |
|---|---|---|---|---|---|---|
| 1 | `src/Hlks.Web/GespraechDienst.cs:142` (Verbotsliste), `src/Hlks.Mcp/Program.cs:119`–`121`, `ki-test/ki_test.ps1:44` | Was die KI nicht darf, steht zum grossen Teil nur in der Verbotsliste der Palette bzw. in `--werkzeuge`. Der Kern lehnt die Art «ki» selbst nur ab bei: Familienfreigabe, Konflikt entscheiden, Wert bestätigen, `familie_speichern`, neuere Familienfassung, `rueckmeldung_uebernehmen`, `baustelle_export` (nur Stand «freigegeben»), Aussparungsnummern. Nicht im Kern gesperrt: `hkap_senden`, `hkap_befunde_senden`, `hkap_uebernehmen` und die übrigen `hkap_*`, `aussparung_status`, `kollision_status`, `bcf_import`, `montagestatus_setzen`, `revision_hinzufuegen`, `plansatz_revision_hinzufuegen`, `plansatz_setzen`, `familie_in_bibliothek`, `familie_aktualisieren`, `mustersystem_speichern`, `familienauswahl_setzen`, `ifc_export`, `plan_pdf`, `plansatz_pdf`, `bcf_export`, `aussparungen_ifc`. | Startet jemand den MCP-Server mit `--art ki` ohne `--werkzeuge` (so tut es `ki-test/ki_test.ps1`; ebenso jede eigene Einbindung in Claude Code oder Claude Desktop), hat die KI alle 206 Werkzeuge – auch «an HKAP senden» und Freigaben. Die Palette und die Aufträge sind nicht betroffen (sie geben `--werkzeuge` mit). | Entweder (a) `--art ki` ohne `--werkzeuge` registriert nur die erlaubten Werkzeuge (dazu `KiEinordnung.Erlaubt` und die Verbotsliste von `Hlks.Web` in den Kern verschieben), oder (b) eine Sperre im Kern an einer Stelle: `Projekt.Ausfuehren` lehnt für Herkunft «ki» alle Befehle der Verbotsliste ab, `Tuer.Abfrage` die schreibenden Abfragen. (b) schliesst auch Punkt 2. | mittel | hoch |
| 2 | `src/Hlks.Kern/Sicherheit/KiPfade.cs:46` (`IstKi`), `src/Hlks.Kern/Projekt.cs:133` (`ArtJetzt`), `src/Hlks.Web/Program.cs:170` | `KiPfade.IstKi` fragt `p.AkteurArt` (Art des Projekts), nicht die Art des laufenden Aufrufs. Für Befehle über `POST /api/befehl/{name}` mit `X-Hlks-Akteur-Art: ki` gilt die Pfadgrenze darum nicht (`unterlage_einfuegen`, `ifc_uebernehmen`, `familie_laden`, `bcf_import`, `plansatz_setzen` nehmen jeden Pfad); Abfragen (`/api/abfrage/…`) kennen den Kopf gar nicht. Gleich gebaut: `BaustelleExport.cs:45` und `FamilienQuellenVersion.cs:577` (`p.AkteurArt`). | Der Kopf ist eine Selbstauskunft – wer ihn weglässt, gilt ohnehin als Mensch; ein Schutz gegen eine böswillige KI ist der HTTP-Weg also nie. Aber der Kommentar «Befehle nur für Menschen lehnen ab» stimmt nur für die Sperren, die `k.Herkunft.Art` fragen. | `Projekt.ArtJetzt` öffentlich machen (`AkteurArtJetzt`) und in `KiPfade.IstKi` verwenden. Nicht ohne Entscheid gemacht: eine Claude-Code-Sitzung, die heute über HTTP mit dem Kopf arbeitet und z. B. ein IFC aus `Downloads` übernimmt, würde danach abgelehnt. | klein | mittel |
| 3 | `src/Hlks.Kern/Grundlagen/UnterlageEinfuegen.cs:69`, `src/Hlks.Kern/Import/Unterlagen.cs:36` | `unterlage_einfuegen` kopiert die Datei auch im Trockenlauf in den Ordner `unterlagen` (`Unterlagen.Ablegen` vor jeder Prüfung von `k.Trockenlauf`). | Die Anleitung des MCP-Servers sagt zum Trockenlauf «ohne etwas zu speichern»; nach einem abgelehnten oder nur geprüften Einfügen liegt die Kopie trotzdem im Projekt (nie überschrieben, nach Prüfsumme benannt – kein Datenverlust, aber Dateien ohne Unterlage-Element). | Im Trockenlauf nur Prüfsumme und Name berechnen, nicht kopieren; oder den Satz in `Werkzeuge.Anleitung` und in der Beschreibung ergänzen («die Datei wird auch im Trockenlauf abgelegt»). | klein | tief |
| 4 | `src/Hlks.Mcp/Werkzeuge.cs:101` (`Ganz`), `src/Hlks.Kern/Api/Tuer.cs:17` (`I`) | Ganzzahlige Parameter (`seite`, `proSeite`, `anzahl`): der MCP-Server rundet (`Math.Round`, 1.5 → 2), die Tür des Kerns schneidet ab (`(int)d`, 1.5 → 1). | Dieselbe Abfrage mit `seite: 1.6` liefert über MCP Seite 2, über HTTP Seite 1. Praktisch selten. | Eine Regel für beide (ablehnen, wenn keine ganze Zahl – wie `unterlage_einfuegen.seite` es schon tut). | klein | tief |
| 5 | `src/Hlks.Web/AuftragDienst.cs:223`–`225` | `kategorie` und `familie` eines KI-Auftrags haben keine Längengrenze und kein Muster (nur `hinweis` ≤ 4000 Zeichen); `kategorie` geht wörtlich in den Auftragstext. In der Palette sind die Kontextfelder dagegen auf 100 Zeichen und ein Muster begrenzt (B9). | Der Wert kommt vom Menschen am Rechner (Herkunftsschutz), kein Angriffsweg von aussen; nur uneinheitlich. | `kategorie` gegen die familienfähigen Klassen des Katalogs prüfen, `familie` löst der Kern ohnehin auf. | klein | tief |
| 6 | `src/Hlks.Web/GespraechDienst.cs:134`, `src/Hlks.Web/AuftragDienst.cs:13`–`19` | Kommentare nennen alte Stände («Stand 19.10.2026, 120 Werkzeuge»); heute sind es 206 plus `kontext_bild` und die `zeichnung_*`. | Nur Dokumentation; die Tests (`WerkzeugEinordnungTests`) halten die Listen selbst aktuell. | Zahl streichen, auf den Test verweisen. | klein | tief |

Geprüft und in Ordnung (kein Befund):

- **Parameter**: Skript über alle `[McpServerTool]`-Methoden – kein Parameter, der nicht an den Kern geht, kein abweichender Schlüsselname. Im Kern
  wurden die Verdachtsfälle «deklariert, nie gelesen» (`bcf_export.kollisionen/koordinaten/marker`, `plansatz_*`, `unterlagen_lesen.*`) einzeln
  nachgeschlagen: alle werden in Nachbardateien gelesen. Unbekannte Parameter lehnt `Projekt.AusfuehrenIntern` ab.
- **`KiPfade.Grund`** von Hand nachgerechnet: `C:\Projekt\..\..\x` → nach `GetFullPath` ausserhalb, abgelehnt; `C:\Projekt2\x` bei Wurzel `C:\Projekt` →
  `Unter` vergleicht mit `C:\Projekt\`, abgelehnt; `W:\…` wird mit `netz.laufwerke` zu `\\HK-NAS02\WorkShare_NAS\…` und gegen die Wurzeln in derselben Form
  verglichen; `\\HK-NAS02@SSL\…` und `\\hk-nas02.evil.example\…` haben einen anderen Servernamen, abgelehnt; `C:\Projekt\CON` wird je nach Windows zu
  `\\.\CON` (Server «.», abgelehnt) oder bleibt eine gewöhnliche Datei unter der Wurzel; leere oder ungültige Wurzeln werden übersprungen. Die Annahmen
  im Kopf (Junctions, 8.3-Namen) bleiben.
- **Datei-Endpunkte**: `/api/baustelle/datei` (nur `.html` direkt in `ausgabe\baustelle`, Vergleich des Ordners nach `GetFullPath`),
  `/api/baustelle/eingang/{datei}` (`Path.GetFileName`, nur `.json`), `/api/ifc/export/datei`, `/api/unterlagen/{id}/netz` (`Path.GetFileName`),
  `/hilfe/` (`AnleitungDienst.Aufloesen`). Upload `/api/unterlagen` auf 500 MB begrenzt, Name bereinigt, Temp-Ordner je Anfrage; Bild der Frage 4 MB,
  Frage 8000 Zeichen, Auswahl 200.
- **`unterlage_aus_url`**: Adressprüfung bei jedem Verbindungsaufbau (`ConnectCallback`), Weiterleitungen selbst verfolgt, 50 MB, 60 s.
- **Fehler mit Vorschlag**: `Werkzeuge.Lauf` fängt alles, `Fehlerkennung` setzt `isError` für alle Werkzeuge; leere `--werkzeuge`-Liste entfernt alle
  Werkzeuge (schliesst, statt zu öffnen).

## 4. Offene Fragen an Christian

1. **Sperren im Kern oder in der Liste?** Sollen «an HKAP senden», Status-Freigaben, Revisionen und Ausgaben an Dritte für die Akteur-Art «ki» im Kern
   selbst gesperrt sein (Punkt 3.1) – also auch dann, wenn jemand den MCP-Server ohne Werkzeugliste in ein eigenes Claude einbindet? Heute schützt dort
   nur die Liste der Palette.
2. **KI über HTTP**: Soll für Aufrufe mit `X-Hlks-Akteur-Art: ki` dieselbe Pfadgrenze gelten wie für die KI der Palette (Punkt 3.2)? Das würde
   Sitzungen einschränken, die heute über HTTP Dateien von ausserhalb des Projektordners übernehmen.
3. **Trockenlauf von `unterlage_einfuegen`**: Darf er die Datei schon ablegen (heute), oder soll er wirklich nichts schreiben (Punkt 3.3)?

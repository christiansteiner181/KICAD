# Testbericht Modul 1d – HKAP-Universalschnittstelle

Stand 18.10.2026 (Abschluss zusammen mit 1c) · gebaut am 18.10.2026 (geplant 14.–18.12.2026) · **von Christian abgenommen am 18.10.2026** · HKAP-Änderungen
**nicht ausgeliefert** (Freigabe nötig)

## Ergebnis

Alle Abnahmekriterien der Spezifikation (`entwurf/modul-1d.md`) sind erfüllt – geprüft gegen die **HKAP-Testkopie**
(Port 5098, abgeschottete Datenkopie), nie gegen HKAP im Betrieb:

| Kriterium | Nachweis |
| --- | --- |
| KICAD-Projekt mit HKAP-Projekt verknüpft | `hkap_verknuepfen` mit P-005 / PJ-2026-001, Gegenprüfung Name und Kürzel; Oberfläche Register «Verwalten» › HKAP › Verbinden (Projektliste aus `/api/austausch/projekte`) |
| Projektdaten im Plankopf | Projekt → Projektinformation (Bezeichnung, Adresse, Kunde, Kürzel, Phase), Herkunft «hkap» |
| Räume einer HKAP-Projektierung übernommen | 6 Geschosse und 121 Räume übernommen; Räume ohne Gegenstück erscheinen als **nicht platzierte Räume** im Projektbrowser (platzieren oder einem gezeichneten Raum zuordnen); Anforderungen (Personen, Luftmengen, Leistungen, Temperaturen, CO₂) als Raum-Eigenschaften, HKAP führt |
| In KICAD gezeichnete Fläche steht in HKAP | `hkap_senden`: Fläche und Höhe gehen als `kicad { flaecheM2, hoeheMm }` an den HKAP-Raum; in HKAP «· KICAD 6.00 m²» neben der Planfläche und die Zeile «aus KICAD: …» |
| Prüfbefund als Pendenz in HKAP | `hkap_befunde_senden`: Befund → Pendenz mit Quelle «KICAD», Raum und Projektkennung; erneutes Senden ohne Doppel; Marke «aus KICAD · Raum» in der Pendenzenliste |
| Beidseitig geänderter Wert → Konfliktliste | Drei-Wege-Abgleich mit Basisstand; Konflikte mit beiden Fassungen, Wahl je Zeile (Oberfläche Abgleich-Dialog); veraltete Version beim Senden → «Konflikt», nichts überschrieben |
| Trockenlauf, rückgängig, MCP | Jeder Schritt mit Vorschau; Übernehmen = ein Paket (rückgängig samt Basisstand); 7 MCP-Werkzeuge |

## Was gebaut ist

| Seite | Inhalt | Ort |
| --- | --- | --- |
| HKAP (Hauptlauf) | Austauschstelle `/api/austausch`: Katalog (Datenarten, Felder, Richtung, führende Seite), Projektliste, Lesen je Datenart, Schreiben datensatzweise mit erwarteter Version (Konflikt statt Überschreiben, Trockenlauf als Vorgabe); Datenarten projekt (nur lesen), geschosse, raeume, pendenzen | `AufgabenProtokolle.Web/Services/AustauschService.cs`, `Program.cs` |
| HKAP | **Versionsprüfung für Projektierungen** (`X-Version`/`If-Match`): der offene Editor überschreibt KICAD-Werte nicht mehr still, sondern fragt «Neu laden» / «Meine Fassung speichern»; «Neu laden» behält die Etage | `Program.cs`, `wwwroot/index.html` |
| HKAP | Raum `kicad { id, flaecheM2, hoeheMm, felder, stand }`, Pendenz `quelle`, `quelleId`, `raum`; Marken «aus KICAD»; von KICAD gesetzte Felder fallen aus `siaFelder` | `Models/Projektierung.cs`, `Models/Project.cs`, `index.html`, `CHANGELOG.md` («Unveröffentlicht») |
| KICAD-Kern | HKAP-Klient (5 s Zeitlimit, klare Meldung bei «nicht erreichbar»), Basisstand `austausch/hkap.json` im Projektordner, Drei-Wege-Abgleich, Abbildung der Felder, Befunde → Pendenzen | `src/Hlks.Kern/Austausch/` |
| KICAD-Befehle | `hkap_verbindung`, `hkap_verknuepfen`, `hkap_abgleich`, `hkap_uebernehmen`, `hkap_raum_zuordnen`, `hkap_senden`, `hkap_befunde_senden` (+ MCP) | `Austausch/HkapBefehle.cs`, `Hlks.Mcp/WerkzeugeHkap.cs` |
| KICAD-Oberfläche | Register «Verwalten» › HKAP: Verbinden/Verknüpfen, Abgleich-Vorschau mit Konflikten, Senden, Prüfbefunde senden; nicht platzierte Räume im Projektbrowser | `wwwroot/js/oberflaeche/aktionen-hkap.js`, `hkap-text.js` |

## Tests

| Bereich | Tests |
| --- | --- |
| HKAP-Vertrag gegen die Testkopie (Skript, Hauptlauf): Lesen, Trockenlauf, Schreiben, HKAP-geführte Felder, veraltete Version, neuer Raum, Löschen abgelehnt, Pendenz ohne Doppel, Projekt nur lesen, 409 für den Editor, `kicad` bleibt nach Editor-Speichern | 25 von 25 (vor und nach dem Nachtrag) |
| HKAP-Oberfläche im Browser (Testkopie): Marke im Raum, Konfliktdialog → «Neu laden» übernimmt den KICAD-Wert, Marke in den Pendenzen | geprüft |
| KICAD-Kern (Attrappe + Integration gegen die Testkopie) | 42 von 42 |
| KICAD-Oberfläche (Node) | 34 |
| KICAD-Oberfläche im Browser gegen die Testkopie (Abschluss: frisches Beispielprojekt) | 85 Prüfungen |

HKAP selbst hat keine automatischen Tests; der Vertrag ist über das Prüfskript und die KICAD-Integration abgesichert.

## Befunde während des Baus

| # | Fund | gefunden von | Behebung |
| --- | --- | --- | --- |
| 1 | HKAP-Projektierungen hatten keinen Konfliktschutz; der Editor speichert alle 2.5 s das ganze Objekt | Hauptlauf (Bestandsaufnahme) | Versionsprüfung wie bei Pendenzen |
| 2 | Raumfläche in HKAP nicht gespeichert, SIA-Nachführung überschreibt externe Werte, kein CORS | Hauptlauf | Feld `kicad`, Feld aus `siaFelder` nehmen, Austausch Server zu Server |
| 3 | Nicht platzierter Raum hätte fremde `kicad`-Werte in HKAP geleert | hlks-kern (B) | senden erst, wenn gezeichnet |
| 4 | Zwei KICAD-Projekte am selben HKAP-Projekt hätten einander Pendenzen als erledigt vorgeschlagen | hlks-kern (B) | Projektkennung in `quelleId` |
| 5 | Basisstand im Webserver nur im Arbeitsspeicher; Herkunftsart «hkap» fehlte; Anforderungs-Eigenschaften fehlten; Projektliste fehlte im Vertrag | hlks-kern (B) | Hauptlauf |
| 6 | Darstellung lieferte keine Kennungen (HKAP-Raum-Id nur aus Herkunftstext) | hlks-modul (C) | `kennungen` in der Darstellung |
| 7 | **Datenbefund HKAP (Livedaten):** P-005 hat PLZ und Ort vertauscht («Bern» / «3013») | hlks-kern (B) | in HKAP korrigieren |
| 8 | Browser-Test hing an einem Test-Strang, der nur im Arbeitsprojekt des Bauauftrags stand (im frischen Beispielprojekt kein Prüfbefund zum Senden) | Hauptlauf (Abschluss) | Test legt den Strang selbst an |

## Annahmen zum Prüfen (Prüfliste)

1. Führende Seite je Datenart und Feld wie in der Spezifikation (Lage/Fläche/Höhe/Etage KICAD; Anforderungen HKAP;
   Nummer/Bezeichnung/Nutzung wer zuerst; Pendenzen: Inhalt KICAD, Status HKAP).
2. «Kunde» in HKAP = Bauherr im Plankopf.
3. HKAP-Projektierungen mit Versionsprüfung (Verhaltensänderung im Betrieb).
4. Prüfbefunde ab «fehler» werden Pendenzen (Priorität fehler → hoch, warnung → mittel, hinweis → tief; Kategorie
   «KICAD-Prüfung»); behobene Befunde bekommen einen Vorschlag, Status und Löschen bleiben bei HKAP.
5. Neue Geschosse aus HKAP: Geschosshöhe 3000 mm als **Platzhalter** mit Hinweis (HKAP kennt keine Höhe).
6. Geschosse beim ersten Abgleich über eindeutigen Namen/Präfix zugeordnet; Räume nur über «Zuordnen» oder eine bekannte KICAD-Id.
7. Löschen geht nie über die Austauschstelle (nur Vorschlag an den Menschen).

## Bekannte Grenzen

- Stränge/Schema, Rechenergebnisse, Mengen und Pläne folgen in den Modulen 3–7 (gleicher Vertrag, neue Datenarten).
- Einen echten HKAP-Konflikt beim Senden erzeugt der Browser-Test durch eine eingespielte Antwort; die Kernmeldung ist im
  Kern-Test geprüft.
- **Auslieferung der HKAP-Änderungen** erst nach deiner Freigabe; bis dahin kann KICAD nur gegen die Testkopie arbeiten.

## Selbst ausprobieren

HKAP-Testkopie: **http://localhost:5098** (Daten frisch aus deinem Bestand kopiert, abgeschottet). KICAD-Testserver:
http://localhost:5131. In KICAD: Verwalten › HKAP › Verbinden (Adresse http://localhost:5098) → P-005 / PJ-2026-001
verknüpfen → Abgleich → «Aus HKAP übernehmen» → einen nicht platzierten Raum zuordnen → «Nach HKAP senden» → in der
Testkopie unter P-005 › Tools › Projektieren die Marke «aus KICAD» ansehen.

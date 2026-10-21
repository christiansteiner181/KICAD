# Modul 5 – Heizung: Spezifikation Teil A (Datenmodell, Prinzipschema, Oberfläche)

Stand 21.10.2026, 03:15 · Hauptlauf · Auftrag Christian 21.10.2026 ~00:40 (Fragefenster): «danach Modul 5 Heizung
vorbereiten ohne Normwerte (Datenmodell, Prinzipschema, Oberfläche; Rechenkern später)».

## Ausgangslage und Abgrenzung

- **Keine Normwerte in Teil A.** Für Heizlast, Heizkörper-Umrechnung, Rohrdimensionierung, Fussbodenheizung und
  Druckhaltung fehlen die Normen und Stoffwert-Quellen im Normenverzeichnis. Betroffen sind SIA 384/1, SIA 384/2 bzw.
  SN EN 12831-1, SIA 2028, SIA 2024, SN EN 442, SN EN 1264 und SN EN 12828. Diese Teile bleiben **Teil B**.
  - Die Normnamen dienen hier nur als Merkposten. Welche Normen gelten, entscheidet Christian mit dem Ablageort.
  - Teil A rechnet nichts, wofür eine Norm oder ein Stoffwert nötig ist. Er bildet ab, summiert vorhandene Angaben und
    ordnet an.
- **Bestand** (Bestandsaufnahme 21.10.2026):
  - Heizungsklassen im Katalog: Waermeerzeuger, Speicher, Verteiler, Heizkoerper, Fussbodenheizkreis, Pumpe, Ventil
    (`art` mischventil), Waermezaehler.
  - Medien HZ-VL/HZ-RL und KAE-VL/KAE-RL, je mit Glykol-Variante. Vorlauf und Rücklauf sind getrennte Systeme ohne
    Verknüpfung.
  - Anlagen A-HZG und A-KAE.
  - **Nicht vorhanden:**
    - Heizgruppe, Mischer mit 3 Anschlüssen, Ausdehnungsgefäss, Wärmetauscher, Fühler
    - Temperaturfelder
    - ein Register oder eine Gruppe «Heizung» in der Oberfläche
    - Heizungssymbole im Schema (heute nur Klassen-Ersatzbilder)
    - ein Prinzipschema: `schemaart: prinzipschema` wird heute wie ein Strangschema angeordnet, mit Hinweis
  - Das Beispielprojekt hat nur einen Kältekreis.
- **Vorlagen aus Christians Werkzeugen:**
  - **HKAP-Prinzipschema** (`AufgabenProtokolle.Web`, nur lesen):
    - Symbole nach der H+K-Legende (Riehen-Plan) in `Services/SchemaSymbole.cs`, 65 eingebaute Symbole.
    - Konvention: **Armaturen senkrecht, Durchfluss oben → unten, Stellantrieb links**.
    - Hinterlegung Apparat #CCFFE0, Gefäss #E4D6EA, Kälte-Apparat #CCE0FF.
    - Bausteine `SchemaStandardGruppen.cs`: Heizgruppe gemischt, Heizgruppe ungemischt, Sicherheitsgruppe.
    - Leitungspaare P_HZ und P_KAE.
    - Testdaten nur aus `%TEMP%\hkap_testkopie\data\`, nie live.
  - **Heizschema Bernstrasse 42 aus IFC** (16.10.2026): Wärmepumpe → Umschaltventil → Speicher Heizung und Speicher mit
    FWS → Heizgruppe FBH gemischt (3-Weg-Ventil, Pumpe, 2 Thermometer) → Verteilung 1. UG → Strang A und B, 45 Kreise.
    Das ist der Referenzfall für das Prinzipschema.
- Rechenkerne von Modul 5 (Teil B) gehen vor der Abnahme an hlks-pruefer (CLAUDE.md). Teil A wird am Ende ebenfalls geprüft
  (Datenmodell und Anordnung).

## Fertig, wenn … (Teil A)

Christian kann eine Heizungsanlage modellieren, im Grundriss oder direkt im Schema:
- Erzeuger, Speicher, Verteiler
- Heizgruppen mit Pumpe, Mischer, Armaturen und Fühlern
- Heizkörper bzw. FBH-Kreise

Das **Prinzipschema** ordnet die Anlage nach dem Fliessweg an, mit den H+K-Symbolen. Jede Heizgruppe zeigt ihre
Auslegungstemperaturen (Eingabe) und ihre Leistungssumme (Summe der Angaben). Fehlende oder falsche Vor-/Rücklauf-Verbindungen
erscheinen als Befund. Das Beispielprojekt enthält eine Heizungsanlage. Die Bernstrasse ergibt aus dem IFC ein
Prinzipschema, das dem Heizschema vom 16.10.2026 entspricht. Die KI kann die Anlage über MCP lesen und ändern.

## Teil A-D – Datenmodell (hlks-kern)

1. **Heizgruppe** (neue Klasse `Heizgruppe`, Kürzel HG; kein Netzelement, sondern eine Gruppierung wie der Strang):
   - **Gehört zu:** einer Anlage (A-HZG oder A-KAE).
   - **Verweise:** das Vorlauf- und das Rücklaufsystem (Systempaar, Punkt 2); optional der Verteiler-Abgang (Element und
     Anschlusspaar, z. B. `vl_2`/`rl_2`).
   - **Glieder:** die Elemente der Gruppe in Fliessreihenfolge. Sie werden abgeleitet, Punkt 3.
   - **Eigenschaften:**
     - `art`: ungemischt, gemischt, einspritzung, drossel, umlenk, weitere über den Katalog; Anzeige deutsch.
     - `verbraucherart`: heizkoerper, fbh, lufterhitzer, gemischt, kaelte.
     - `vlTemp` und `rlTemp` in °C als **Eingabe des Planers** (Herkunft Mensch bzw. HKAP), ohne Vorgabewert aus einer Norm.
     - `spreizung`, abgeleitet.
     - `leistung`, abgeleitet, Punkt 4.
     - `strangschema`: Verweis auf eine Schemaansicht, wie die HKAP-Heizgruppe mit `verweisSchemaId`.
   - **IFC (Vorschlag):** IfcDistributionCircuit, PredefinedType HEATING bzw. CHILLEDWATER, ObjectType «Heizgruppe»,
     aggregiert unter dem System der Anlage. Gegen das IFC4- und IFC2x3-Schema prüfen, nie aus dem Gedächtnis; der Export
     von Modul 7 B2 wird nachgeführt.
2. **Systempaar Vor-/Rücklauf:**
   - **Verknüpfung:** Eigenschaft `gegenstueck` am System (VL ↔ RL, beidseitig). Gesetzt wird sie beim Anlegen über
     `system_paar_anlegen` (legt beide Systeme mit passenden Medien an), beim Rohrpaar zeichnen (Punkt A-O 2) und beim
     IFC-Import, wenn die Namen eindeutig zusammenpassen («H VL NT» ↔ «H RL NT»).
   - **Medienpaare** aus `medien.json`, neues Feld `gegenstueck`: HZ-VL ↔ HZ-RL, die Glykol-Varianten, KAE-VL ↔ KAE-RL.
   - **Klassen mit vl/rl-Anschlusspaar** (Heizkörper, Verteiler, Erzeuger …): Ihre Anschlüsse gehören in die beiden Systeme
     eines Paars.
3. **Glieder der Heizgruppe ableiten** (`src/Hlks.Kern/Heizung/Heizgruppen.cs`), deterministisch, aus dem Netz von Modul 3
   (Stromab/Stromauf folgen):
   - **Vorlaufweg:** vom Verteiler-Abgang bzw. Gruppenanfang stromab bis zu den Verbrauchern.
   - **Rücklaufweg:** von den Verbrauchern stromab bis zum Verteiler.
   - **Glieder** sind die Armaturen, Pumpen, Mischer, Fühler und Zähler auf diesen Wegen, in Fliessreihenfolge und je mit
     Weg (VL oder RL).
   - **Verbraucher** sind die Elemente mit Rolle «verbraucher», stromab des Vorlaufwegs.
   - **Mischer:** Der Bypass verbindet RL und VL. Das ist eine Masche und für die Ableitung erlaubt; sie bricht nicht ab.
   - **Ohne Heizgruppe** (Altbestand, IFC-Import): Die Ableitung schlägt Heizgruppen je Verteiler-Abgang vor
     (`heizgruppen_vorschlagen`, mit Trockenlauf).
4. **Leistungssummen** (normunabhängig, Muster Modul 4 Teil A):
   - **Summen:** Je Leitung bzw. Teilstrecke, je Heizgruppe, Verteiler und Anlage wird die `leistung` der Verbraucher
     summiert, auf dem Vorlauf stromab, auf dem Rücklauf stromauf.
   - **Beschriftung:** Die Summe heisst «Σ Normleistung der Verbraucher (Angabe)». Sie wird ausdrücklich nicht auf
     Auslegungstemperaturen umgerechnet; das ist Teil B.
   - **Masche:** Jeder Verbraucher zählt höchstens einmal, mit Befund wie in Modul 4.
   - **Je Raum:** Σ Leistung der Verbraucher neben `Raum.heizleistung` (HKAP oder Eingabe) als Information, ohne Befund.
     Normleistung und Raumheizlast sind nicht vergleichbar, solange Teil B fehlt.
5. **Klassen ergänzen** (`kataloge/klassen.json`, `ifc.json`; BKP wie die bestehenden Heizungsklassen 242/243, neue
   Kälte-Zuordnung als Annahme zur Bestätigung):
   - **Mischventil** (MV): Armatur, Anschlüsse `a` (Durchgang ein), `b` (Bypass ein) und `ab` (aus); `art` 3weg, 4weg
     (zusätzlich `b_aus`) oder umschalt (`ab` ein, `a`/`b` aus); `kvs` und `pn` aus der Unterlage; IfcValve MIXING bzw.
     DIVERTING.
     - Das heutige `Ventil.art = mischventil` bleibt lesbar; ein Befund schlägt die Umstellung vor.
   - **Ausdehnungsgefaess** (AG): Apparat, 1 Anschluss; `inhalt` l und `vordruck` bar aus der Unterlage; IfcTank EXPANSION.
   - **Waermetauscher** (WT): Apparat, Anschlüsse `p_vl`, `p_rl`, `s_vl` und `s_rl` (primär/sekundär, eigene Medien je
     Seite); `art` platte oder rohr; IfcHeatExchanger.
   - **Fuehler** (TF): Armatur im Rohr (ein/aus); `art` temperatur, druck, thermometer, manometer, stroemung;
     IfcSensor bzw. IfcFlowInstrument.
   - **Waermeerzeuger:** `art` zusätzlich `kaeltemaschine` ist **nicht** vorgesehen. Für die Kälte gibt es die neue Klasse
     **Kaelteerzeuger** (KEZ), Anschlüsse KAE-VL/KAE-RL plus optional Rückkühlseite; IfcChiller. Weitere Kälteklassen
     (Rückkühler, Kühldecke) später.
   - **Waermepumpe mit Quellenseite:** Die Familie darf zusätzliche Anschlüsse `q_vl`/`q_rl` (Sole, Grundwasser) tragen.
     Die Klasse bleibt Waermeerzeuger `art waermepumpe`.
   - **Ventil.art erweitern:** strangregulierventil, sicherheitsventil (Eckventil, Abblase-Anschluss), ueberstroemventil,
     thermostatventil, zonenventil, entlueftung, entleerung, schmutzfaenger. Die Symbole liefert Teil A-S.
   - **Verteiler:** Die Abgangszahl folgt der Familie (heute fest 2). Die Klasse beschreibt die Anschlussnamen `vl_n`/`rl_n`.
6. **Befunde** (Prüfsystem; nächste freie Codes ab P31, Texte mit Lösungsvorschlag):
   - Verbraucher mit vl/rl, dessen Anschlüsse nicht beide verbunden sind oder nicht in einem Systempaar liegen.
   - Heizgruppe ohne Pumpe, ohne Systempaar oder ohne Verbraucher.
   - Mischventil falsch eingebunden: `ab` nicht stromab, `b` nicht vom Rücklauf.
   - `vlTemp` ≤ `rlTemp`, oder eine Temperatur fehlt (Hinweis).
   - Heizungselement in keiner Heizgruppe (Info).
   - `Ventil.art = mischventil` (Umstellung auf Mischventil vorschlagen).
7. **Befehle und Abfragen** (eine Tür, KI-Einordnung je Werkzeug, MCP):
   - Befehle: `heizgruppe_anlegen`, `heizgruppe_aendern`, `heizgruppen_vorschlagen`, `system_paar_anlegen`.
   - Abfragen: `heizung_lesen {anlage}` (Textansicht: Erzeuger → Speicher → Verteiler → Gruppen mit Gliedern, Temperaturen,
     Leistungssummen) und `heizung_leistung {anlage|heizgruppe|raum}`.
   - Bestehende Abfragen (`system_lesen`, `strang_text`) zeigen die Heizgruppe.
8. **Beispielprojekt MFH Musterweg:** eine Heizungsanlage A-HZG, alle Werte als **FIKTIV** gekennzeichnet:
   - Wärmepumpe Luft/Wasser, Speicher, Ausdehnungsgefäss
   - Verteiler mit Heizgruppe FBH gemischt und Heizgruppe Heizkörper ungemischt
   - je Geschoss ein FBH-Verteiler mit Kreisen, im Grundriss verlegt

   Der bestehende Kältekreis bleibt.
9. **HKAP:** `tempVL`/`tempRL` der HKAP-Heizgruppen werden beim Abgleich (Modul 1d, nur lesend) auf die verknüpfte
   Heizgruppe abgebildet, mit Herkunft HKAP. Nichts zurückschreiben; Tests nur gegen die Testkopie 5098.
10. **Tests:**
    - Netztests: Ableitung der Glieder, Mischer-Masche, Leistungssummen, Systempaar, jeder Befund.
    - Referenzfall **Bernstrasse IFC**: Import aus der lokalen Testkopie
      `%USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42`. `heizgruppen_vorschlagen` ergibt die Heizgruppe FBH gemischt mit
      3-Weg-Ventil, Pumpe und 2 Thermometern sowie 45 Kreise in Strang A (6/7/7/4) und B (5/5/5/6).
    - Beispielprojekt.
    - KI-Verständnistest: `heizung_lesen` beschreibt die Anlage richtig.

## Teil A-S – Prinzipschema

1. **Anordnung nach dem Fliessweg** (hlks-kern; `src/Hlks.Kern/Schema/Prinzipschema.cs`, gleiche Schnittstelle wie die
   Strangschema-Anordnung, sodass `schema_anordnen`, `schema_lage` und P17 weiter gelten). Kein Geschossraster.
   - **Spalten von links nach rechts:** Erzeugung, Speicherung bzw. hydraulische Weiche, Verteilung.
   - **Verteilung:** ein waagrechter Verteilbalken, Vorlauf oben, Rücklauf unten.
   - **Heizgruppen:** je Abgang eine senkrechte Spalte nach oben. Vorlauf und Rücklauf laufen parallel, die Glieder senkrecht
     in Fliessreihenfolge, nach der H+K-Konvention (Durchfluss oben → unten in der Armatur, Stellantrieb links).
   - **Verbraucher oben:** als Sammelsymbol mit Anzahl und Σ Leistung (Vorgabe) oder einzeln (Ansichtseigenschaft
     `verbraucherEinzeln`); Verweis auf das Strangschema der Gruppe, wenn gesetzt.
   - **Kälte (A-KAE)** wird gleich angeordnet; ein Wärmetauscher trennt Primär- und Sekundärseite in zwei Spalten.
   - **Abstände** sind Darstellungsannahmen im Raster wie `SchemaRaster`, keine Normwerte. Weicht die Lage von Hand ab, gilt
     `schema_lage`; «Neu anordnen» setzt zurück.
   - **Nicht zuordenbare Elemente:** Bereich «Übrige» mit Befund. Die Anordnung muss für die Bernstrasse ohne Übrige
     auskommen, ausser bei den bekannten Modellmängeln (AG ohne Anschluss, offener Bypass).
   - **Modul 6:** Die Lüftung übernimmt diese Anordnung (Gerät und Kanalnetz mit Klappen). Die Fliessweg-Logik deshalb
     gewerkneutral bauen und die Heizungsregeln als eigene Schicht.
2. **Symbole** (hlks-modul; `src/Hlks.Kern/Schema/SchemaSymbole.cs`):
   - **Übertragen:** die Heizungs- und Kältesymbole aus HKAP `Services/SchemaSymbole.cs`, gleiche Geometrie. Dazu gehören
     Wärmeerzeugung, Speicher, Ausdehnung, Regelarmaturen, Pumpen, Verteilung, Armaturen, Messen und Regeln, Verbraucher
     und Wärmeübertrager. Die Liste steht in der Bestandsaufnahme, Abschnitt 4b; Sanitär ist schon da.
   - **Zuordnung:** Klasse bzw. `art` → Symbol.
   - **Hinterlegung:** Apparat #CCFFE0, Gefäss #E4D6EA, Kälte-Apparat #CCE0FF.
   - **Legende:** wie in HKAP; sie enthält die verwendeten Leitungsarten und Armaturen.
   - Ein Familienteil `schemasymbol` hat weiterhin Vorrang.
   - **Tests:** Bildvergleich je Symbol gegen eine Referenz aus dem HKAP-Code (gleiche Linien) sowie die Zuordnung.
3. **Bausteine** (hlks-kern, Befehl `baustein_einfuegen`):
   - **Mitgeliefert:** Heizgruppe gemischt, Heizgruppe ungemischt, Sicherheitsgruppe, mit dem Aufbau aus HKAP
     `SchemaStandardGruppen.cs`. Sie fügen Elemente und Verbindungen ins Modell ein, mit Heizgruppe und Systempaar.
   - **Eigene Bausteine** als Bürobibliothek (`baustein_speichern` aus der Auswahl) erst nach Rückfrage. Zuerst nur die
     mitgelieferten.
4. **Schema anlegen:** `schema_anlegen` mit Vorlage `heizung` bzw. `kaelte` und `schemaart prinzipschema`. Der Test, der
   `heizung` heute ablehnt, wird angepasst. Planblatt wie Modul 7 A; die Plannummer kommt aus der Rückfrage.
5. **Schema zuerst:** Elemente lassen sich wie im Strangschema direkt im Prinzipschema einfügen und verbinden. Sie bestehen
   im Modell ohne Grundrisslage (P17 «nicht platziert» als Hinweis) und werden später im Grundriss platziert. Das Modell
   bleibt die einzige Wahrheit.
6. **Tests:**
   - Anordnung des Beispielprojekts und der Bernstrasse (Lagen, Reihenfolge, keine Überschneidungen).
   - Bausteine einfügen und anordnen.
   - Kälte mit Wärmetauscher.
   - Handlage bleibt nach dem Ändern erhalten.

## Teil A-O – Oberfläche (hlks-modul, nach A-D bzw. A-S)

1. **Gruppe «Heizung» im Register Gebäudetechnik:**
   - Wärmeerzeuger, Kälteerzeuger, Speicher, Verteiler, Heizkörper, FBH-Kreis
   - Armatur (Pumpe, Ventil, Mischventil, Fühler, Zähler), Ausdehnungsgefäss
   - Heizgruppe anlegen bzw. Baustein einfügen

   Platziert wird über Familien (CM), wie bei den Sanitärapparaten. Die heute gesperrten Platzhalter (PX, PA) bekommen ihre
   Funktion.
2. **Rohrpaar zeichnen:** Vorlauf und Rücklauf in einem Zug, im Abstand nebeneinander (Planmuster H01: Doppellinie).
   - Der Kern-Befehl `leitung_paar_zeichnen` versetzt die Achse, setzt die Formteile beider Leitungen und das Systempaar.
     Die Geometrie baut hlks-kern.
   - In der Oberfläche ist es ein Schalter «Paar» im Rohr-Werkzeug.
3. **Eigenschaften:**
   - Heizgruppe: Art, Temperaturen, Spreizung, Leistungssumme mit «?»-Quelle «Summe der Angaben, Normleistung»
   - System: Gegenstück
   - neue Klassen: ihre Felder
4. **Prinzipschema:**
   - Dialog «Schema anlegen» mit Vorlage Heizung/Kälte und Schemaart.
   - Gruppe «Schema»: Verbinden, Trennen, Element einfügen, Neu anordnen, Baustein einfügen.
   - Ansichtseigenschaft `verbraucherEinzeln`.
5. **Projektbrowser:** Die Gliederung `heizung` zeigt Anlagen, Heizgruppen und Systempaare.
6. **Anleitung:** Kapitel Modul 5 in `berichte/anleitung/inventar.json` mit Bildern aus dem Beispielprojekt; dazu
   `OBERFLAECHE.md` und die Browser-Reihen `heizung.browser.mjs` und `prinzipschema.browser.mjs`.

## Teil B – Rechnen (wartet auf Normen und Quellen)

Jeder Wert nur mit Zitat (Norm, Ausgabe, Ziffer, Seite). Normtabellen als versionierte JSON; Christian prüft die Übertragung
gegen. Rechenkerne gehen vor der Abnahme an hlks-pruefer.
- **Heizlast je Raum** aus der Hülle von Modul 1c. Klimadaten und Nutzungsdaten nach den Normen, die Christian bestimmt.
  Dazu gehören Auslegungs-Aussentemperatur, Standort und Lüftungsverlust; die Eingabefelder entstehen erst mit der Quelle.
- **Heizkörper:** Leistung bei Auslegungstemperaturen (Exponent aus der Herstellerunterlage).
- **FBH-Auslegung:** Verlegeabstand, Kreislänge, Leistung, Durchfluss; Datenbox H13–H16.
- **Volumenstrom je Teilstrecke:** aus Leistung und Spreizung, Stoffwerte mit Quelle. Dazu DN und Druckverlust
  (Rauheit und ζ aus Herstellerunterlagen) sowie die Pumpenförderhöhe.
- **Hydraulischer Abgleich und Ausdehnungsgefäss.**

## Zuteilung und Reihenfolge

| Schritt | Inhalt | Wer |
| --- | --- | --- |
| 1 | A-D 1–10 (Datenmodell, Heizgruppe, Systempaar, Leistungssummen, Klassen, Befunde, Befehle, Beispielprojekt, Bernstrasse-Referenz) | hlks-kern |
| 1 (parallel) | A-S 2 Symbole aus HKAP | hlks-modul |
| 2 | A-S 1, 3–6 Anordnung, Bausteine, Schema anlegen | hlks-kern |
| 2 (parallel) | A-O 2 Kern-Befehl `leitung_paar_zeichnen` | hlks-kern |
| 3 | A-O 1–6 Oberfläche, Anleitung, Browser-Reihen | hlks-modul |
| 4 | Testbericht, Prüfung Teil A | hlks-test, hlks-pruefer |

## Annahmen (Rückfrage an Christian am Morgen)

1. **Schema zuerst erlaubt** (A-S 5): Elemente entstehen im Prinzipschema ohne Grundrisslage, wie im Strangschema.
2. **Anordnung:** Erzeugung links, Verteilbalken waagrecht (VL oben, RL unten), Heizgruppen senkrecht nach oben,
   Verbraucher als Sammelsymbol, H+K-Konvention der Armaturen.
3. **Bausteine:** zuerst nur die drei HKAP-Standardgruppen; eigene Bausteine später.
4. **Kälte:** neue Klasse Kälteerzeuger in Modul 5; Rückkühler und Kühldecke später. BKP der Kälteklassen bestätigen.
5. **Plannummer** des Prinzipschemas Heizung: welche Nummer nach H+K-Schema?
6. **Teil B:** Ablageort und Auswahl der Normen bzw. Quellen für Heizlast, Heizkörper, FBH und Rohrnetz.

# Freie Grösse für Kanalserien (Nachtrag Modul 3) – Spezifikation

Stand 20.10.2026 · Hauptlauf

## Anlass und Entscheid

Grundausstattung K2 (Rechteckkanal, Schmidlin): Rechteckkanäle werden nach Mass gefertigt; heute kennt eine Leitungsserie nur ihre
Typen (Grössen), K2 deshalb nur das Normkanal-Raster (400 Typen). Christian, 20.10.2026 (Fragefenster): «Rechteckkanäle mit freier
Grösse b × h (Normraster nur als Vorschlag); Formteile rechnen ihre Masse aus der Formel.»

Heute: `Leitungsserie.TypFuer(g)` findet nur einen Typ mit genau dieser Grösse; `Leitungsserie.Formteil(…)` wählt den Typ der
Formteil-Familie, dessen ausgewertete Anschlussgrössen passen (`src/Hlks.Kern/Leitungen/Leitungsserie.cs`). Routing, Befehle
(`RoutingBefehle.cs`) und Prüfungen (`Leitungspruefungen.cs`) bauen darauf.

## Datenmodell (Familienformat, `schema/hlks-familie.schema.json`, `FamLeitung`)

- Block `leitung` neu `frei` (nur Kategorie **Kanal**, rund und rechteckig; Rohrserien nie – dort gilt die DN):
  `"frei": { "vorlage": "<Typname>", "grenzen": { "b": { "min": 145, "max": 2500 }, "h": { … } }, "schritt": 1, "quellen": […] }`
  - `vorlage`: Typ, dessen Werte und Formeln (aussen, innen, wand, weitere Parameter) für jede freie Grösse gelten; die
    Grössenparameter (`leitung.groesse`: b/h bzw. d) werden durch die gewünschte Grösse ersetzt. Die Grössenparameter der Vorlage
    müssen Typparameter ohne Formel sein (sonst Definitionsfehler).
  - `grenzen`: optional, je Grössenschlüssel min/max in mm, nur aus der Quelle (mit `quellen`); fehlt = jede Grösse > 0.
  - `schritt`: optional, Raster der Eingabe in mm (Vorgabe 1); eine Grösse ausserhalb des Rasters wird gerundet vorgeschlagen, nie still.
  - Die Typen bleiben das **Vorschlagsraster** (Normgrössen). Eine Grösse, die einem Typ entspricht, nimmt diesen Typ.
- `leitung.formteile.<art>` neu optional `vorlage` (Typname der Formteil-Familie für freie Grössen; fehlt = erster Typ).

## Kern (hlks-kern)

1. **Serie:** `TypFuer(g)` → passender Rastertyp; sonst bei `frei` und innerhalb von Grenzen und Schritt eine **virtuelle Grösse**
   (Typ = Vorlage, Grösse g, aussen/innen/wand aus der Vorlage mit ersetzten Grössenparametern, Kennzeichen `frei: true`);
   ausserhalb → null mit Grund. Meldungen und `GroessenText` nennen bei freien Serien «frei b × h (145 … 2500 mm, Schritt 1), Vorschlag:
   …» (gekürzt).
2. **Formteilwahl:** passt kein Typ der Formteil-Familie und ist die Serie frei, wird der Vorlage-Typ der Formteil-Familie mit
   **Exemplarwerten** ausgewertet: Ist ein Grössenwert eines Anschlusses (d, b, h) direkt ein Parametername, setzt die Wahl diesen
   Parameter auf die Soll-Grösse (Bogen a = b = g; T-Stück a = b = Durchgang, c = Abzweig; Reduktion a = gross, b = klein). Danach
   Prüfung wie bisher (`Passt`, Lage der Anschlüsse, Winkel). Das Formteil-Element trägt familientyp = Vorlage und die
   Exemplarwerte in `parameter`; seine Einbaulängen rechnen die Formeln der Familie. Lässt sich ein Anschluss nicht so setzen
   (Grössenwert ist eine Formel), abgelehnt mit Grund und Vorschlag.
3. **Routing und Befehle** (`leitung_zeichnen`, `leitung_ziehen`, `leitung_groesse`, `leitung_serie`, `leitung_abzweigen`,
   `uebergang_setzen`, `leitung_aufteilen`): nehmen freie Grössen bei freien Serien; Kanal-Element: familientyp = Rastertyp bzw.
   Vorlage, breite/hoehe bzw. durchmesser = tatsächliche Grösse.
4. **Prüfungen:** neue P-Nummer (nächste freie) «Grösse ausserhalb der Grenzen der Serie» (fehler) und «Grösse nicht im Schritt der
   Serie» (hinweis); P14/P15 unverändert mit tatsächlichen Grössen.
5. **Abfrage `leitungsserie`:** zusätzlich `frei` (vorlage, grenzen, schritt) und eine Probe mit einer freien Grösse (Mitte der
   Grenzen, sonst 430 × 270) samt Formteilverfügbarkeit.
6. **Listen, Mengen, IFC:** nach tatsächlicher Grösse gruppieren; IFC-Typobjekt bei freien Grössen je Familie und tatsächlicher
   Grösse (Name «<Familie> <b × h>»), nicht je Vorlage.
7. **KI-Anleitung** (`ki/familie_aus_unterlagen.md`, Abschnitt Leitungsserien): freie Grösse anlegen, wenn die Quelle «nach Mass»
   nennt (Kanäle); Grenzen nur aus der Quelle; Raster als Typen.
8. **Tests:** Testfamilien unter `tests/daten/familien/` (Kanal rechteckig frei, Bogen mit Formel L = (ri + breite/2)·tan(w/2) + lv,
   T-Stück mit eigenem Abzweig b_c/h_c, Reduktion mit breite1/hoehe1/breite2/hoehe2); `TypFuer` innerhalb/ausserhalb/Raster;
   Formteilwahl je Art mit freien Grössen; Routing Kanal 430 × 270 mit Bogen 90° (Einbaulänge (115 + 215)·tan 45° + 35 = 365 mm),
   Reduktion auf 400 × 200, Abzweig 250 × 200; Prüfungen; Speichern/Laden; IFC-Export der Grösse; Rohrserie mit `frei` → Fehler.

## Oberfläche (hlks-modul, nach dem Kern)

- Kanal zeichnen bzw. Eigenschaften einer Kanal-Leitung mit freier Serie: Zahlenfelder b und h (bzw. d) mit Vorschlagsliste aus dem
  Raster, Grenzen und Schritt geprüft (Meldung statt stiller Rundung), Anzeige «frei».
- Familieneditor: Block `frei` anzeigen und bearbeiten (Vorlage, Grenzen, Schritt); Abfrage-Ansicht der Serie zeigt die Probe.
- Anleitung (`berichte/anleitung/inventar.json`), `OBERFLAECHE.md`, Browser-Reihe.

## Daten (Hauptlauf, nach dem Bau)

K2 im Arbeitsprojekt Grundausstattung: Serie `frei` mit Vorlage aus dem Raster, Grenzen aus Schmidlin S. 19 («minimale Seitenhöhe
145 mm»; Obergrenze 2500 = grösster Rastertyp, Annahme zur Bestätigung), Formteile auf Vorlage-Typen mit Exemplar-Grössen umgestellt
(T-Stück mit eigenem Abzweig), Reduktion je Paar entfällt.

**Fertig, wenn** im Arbeitsprojekt Grundausstattung ein Rechteckkanal 430 × 270 mit Bogen 90° (Einbaulänge 365 mm), Reduktion
auf 400 × 200 und einem Abzweig 250 × 200 entsteht, die Prüfungen ohne Befund sind und die Normgrössen weiter als Vorschlag erscheinen.

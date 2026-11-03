# Module 8 und 9 – MSRL und GA-Anbindung: Gerüst

Stand 04.11.2026 · Hauptlauf · Auftrag Christian 03.11.2026: «nach sanitär berechnung heizlast, Heizung und Lüftung. dann die gerüste für MSRL GA
und schnittestelle zu webablage.» Christian 04.11.2026: «bitte ale fragen selbst bestätigen» – Entscheide unten als **Annahme Hauptlauf**.

## Ausgangslage

- **Datenmodell vorhanden** (Modul 0a): Klassen `Automationsstation` (hersteller, adresse), `Datenpunkt` (signal AI/AO/DI/DO/Zaehler/Wert, art
  messung/stellung/meldung/schaltung/zaehlung/sollwert, messgroesse, einheit, physikalisch, adresse, protokoll bacnet/opcua/modbus/mqtt/knx, trend,
  trendIntervall, trendDauer; `bezuege.an` = Bauteil, `bezuege.station`), `Regelkreis` (art regelung/steuerung/verriegelung/ueberwachung;
  `bezuege` messung/stellung/fuehrung). Abfragen `datenpunkte` (Funktionsgraph) und `zeitreihe` (Datenquelle).
- **Datenquelle** (`src/Hlks.Kern/Datenquellen/IDatenquelle.cs`): Werte bleiben auf der Anlage und werden beim Zugriff abgerufen (Entscheid im
  Factsheet: kein Server); heute `Simulator`.
- **Grundlagen:** Im Büro keine Datenpunkt- bzw. Funktionslistenvorlage, kein SIA 411 und keine VDI 3814 (Fundstellenbericht, Abschnitt 4;
  Grundlage `fehlt.heizung_normen`). Schulungsmaterial Regeltechnik in HFGT16 `5_Regeltechnik`.

## Gebaut (Gerüst, 04.11.2026)

- **Grundausstattung** `kataloge/datenpunkte.json`: Datenpunkte je Bauteilklasse (Pumpe B/S/EA, Mischventil Y, Fühler M, Wärme- und
  Wasserzähler Z, Wärmeerzeuger B/S/F/SW, Lüftungsgerät B/S/EA/TZU/FI, Volumenstromregler Y/V) – **Annahme Hauptlauf**, bis eine Bürovorlage vorliegt.
- **Befehl `datenpunkte_vorschlagen { anlage? }`**: legt die Datenpunkte an (`DP-<Bauteil>-<kurz>`, `bezuege.an`), vorhandene bleiben; Trockenlauf.
- **Abfrage `datenpunktliste { anlage? }`**: je Anlage Zahl nach Signal (AI, AO, DI, DO, Zähler, Werte) und Liste mit Bauteil, Art, Messgrösse,
  Adresse, Station – die GA-Datenpunktliste für die Ausschreibung.
- MCP und KI-Gruppen «MSRL planen» (Befehl) bzw. «Gebäudeautomation lesen» (Abfrage); Test `DatenpunktlisteTests`.

## Nächste Schritte (nicht gebaut)

1. **Funktionsliste je Regelkreis:** Regelkreise aus Heizgruppen (Vorlauftemperatur-Regelung: Fühler VL → Mischventil, Führung Aussentemperatur),
   Lüftungsgeräten (Zuluft-Temperatur), Pumpen (Freigabe); Befehl `regelkreise_vorschlagen`, Darstellung im Prinzipschema.
2. **Automationsstationen zuordnen:** je Technikzentrale bzw. Anlage eine Station; `bezuege.station` der Datenpunkte; Summen je Station.
3. **Ausgabe Datenpunktliste** als Liste (Modul 7) und für die Ausschreibung (BKP 2xx MSRL).
4. **Modul 9 GA-Anbindung:** Adapter `IDatenquelle` je Protokoll (BACnet/IP lesen, OPC UA lesen; nur lesend, Spezifikation 3c), Adressen vom
   GA-Unternehmer (Import einer Adressliste CSV), Zeitreihen beim Zugriff, Prüfung «Datenpunkt ohne Adresse».
5. **Fragen an Christian:** Bürovorlage für Datenpunkt- und Funktionslisten? Gilt SIA 411 bzw. VDI 3814 (Ablage)? Welches GA-Protokoll im Büro
   (BACnet, OPC UA, KNX)? Grundausstattung bestätigen bzw. ergänzen (z. B. Pumpe mit Drehzahl AO, Wärmeerzeuger mit Rücklauftemperatur).

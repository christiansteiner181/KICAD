# KICAD – Austausch mit anderen Planungsagenten (Agentenschnittstelle): Konzept

Stand 19.10.2026 · Hauptlauf · **Entwurf, vorgesehen, nicht gebaut**

## Anlass

Christian, 19.10.2026: «Aus dem Projekt KI-Planungsbüro haben wir gelernt, dass in Zukunft ein büroübergreifender Austausch
mit anderen KI-Agenten wahrscheinlich wird. KICAD könnte davon der Baustein HLKS sein. Es sollte daher bereits eine Art
kommunikative Schnittstelle zu anderen KICAD vorgesehen werden.»

Lehren aus dem KI-Planungsbüro (Wissensspeicher `ki_planungsbuero.md`):
- Die Schnittstelle ist **rollenneutral**: ansprechbar von Mensch, eigenem Agent oder fremdem Agent (z. B. Projektentwicklung
  der Bauherrin). Module je Fachgebiet reden direkt miteinander und können intern oder von anderen Firmen betrieben werden.
- Protokoll in zwei Schichten: innen **MCP** am Projektkern, nach aussen **A2A** (Agent2Agent, v1.0 März 2026, wie MCP bei
  der Agentic AI Foundation) als Hülle für fremde Firmen – Frage = Task «input required», Projekt = Kontext. Kein
  bauspezifisches Agentenprotokoll vorhanden (BCF-API Vorbild, IFC 5 Alpha). **Vor dem Bau am dann gültigen Stand prüfen.**
- Antworten kompakt halten (die «Lage» mit 654 000 Zeichen war für Agenten unlesbar) – Filter, Seiten, Zusammenfassungen.
- Fachliche Abklärungen gehören der zuständigen Fachplanung; an Menschen geht nur, was sie entscheiden.

## Entscheide Christian (19.10.2026)

| Frage | Entscheid |
| --- | --- |
| Mit wem? | **Offen über Standard:** andere KICAD und fremde Fachagenten beliebiger Hersteller (A2A + IFC/BCF/IDS) |
| Rechte eines fremden Agenten | **Lesen, anfragen, vorschlagen.** Änderungen kommen in einen **Eingang** und werden erst nach Freigabe übernommen (Mensch oder eigene KI nach Regel) |
| Zeitpunkt | **Jetzt vorsehen, später bauen:** Konzept, Datenmodell, Kern-Vorbereitung jetzt; Bau als eigenes Modul nach M3 |
| Erster Anwendungsfall | **Zwei HLKS-Büros** an einem Projekt (z. B. Heizung/Sanitär bei H+K, Lüftung bei einem Partner) |

## Was KICAD schon mitbringt

- **Eine Tür:** jede Funktion ist Befehl oder Abfrage der Kern-API, mit Trockenlauf, Rückgängig, Herkunft je Wert
  (Akteur, Art mensch/ki/berechnung, Quelle). MCP-Server (`src/Hlks.Mcp`) und REST (`src/Hlks.Web`).
- **Abgleich mit fremden Systemen:** HKAP-Schnittstelle (Modul 1d) mit Drei-Wege-Abgleich, Kennungen je Partner
  (`kennungen.hkap`), «wer zuerst setzt», nie einen fremden Wert überschreiben, Konflikte beim Menschen.
- **Synchronisation** mehrerer Geräte über Pakete mit Operationen, Basisversion und Konflikten.
- **openBIM:** IFC-Import als echte Elemente bzw. Hintergrund, Abgleich neuer Fassungen über GlobalId, IFC-Export,
  BCF 3.0 (Kollisionen ↔ Topics, Status und Kommentare zurück), Aussparungs-IFC mit «angekommen/fehlt».

## Grundsätze

1. **Rollenneutral, eine Tür.** Ein fremder Agent nutzt dieselben Befehle und Abfragen wie Mensch und eigene KI – nur mit
   seinen Rechten. Keine zweite Logik für Partner.
2. **Zwei Schichten.** Innen MCP/REST (gebaut). Aussen eine **A2A-Hülle**: Agent Card (wer, Fachgebiet, Fähigkeiten,
   Kontakt Mensch), Tasks (Anfrage, Lieferung, Vorschlag), Kontext = gemeinsames Projekt. Die **Nutzlast** ist das
   **KICAD-Austauschpaket** (eigenes JSON-Schema, transportunabhängig) mit openBIM-Anhängen (IFC-Teilmodell, BCF 3.0, IDS).
3. **Nichts Fremdes schreibt direkt ins Modell.** Alles von aussen landet im **Eingang**; übernommen wird nach Freigabe als
   eigenes Paket (Herkunft: Büro, Agent, Paket-Id) und ist als Ganzes rückgängig zu machen.
4. **Eigentum je Bereich.** Jedes System bzw. Element hat ein verantwortliches Büro. Elemente fremder Büros sind bei mir
   **Partnerbereich** (lesend, abgeglichen) – wie IFC-Hintergrund, aber lebendig und mit Systemzusammenhang.
5. **Stabile Kennungen.** Element-Id (GUID) plus Kennungen je Partner (Muster `hkap`) – Paarung über Fassungen und Büros.
6. **Drei-Wege-Abgleich** wie bei HKAP (Basis, eigener Stand, Stand des Partners); Konflikte beim Menschen.
7. **Datenlieferschein** (SIA 400:2000 B.11.3.3: «Mit jedem Datenaustausch ist ein Datenlieferschein zu erstellen,
   welcher vom Empfänger ohne das Öffnen der entsprechenden Datei gelesen werden kann») = **Kopf jedes Pakets**: Absender
   (Büro, Agent, verantwortlicher Mensch), Projekt, Inhalt (Bereiche, Systeme, Anzahl Elemente), Stand und Basis,
   Format/Fassung, Koordinatenbezug, Freigabestatus, Prüfsumme.
8. **Sicherheit.**
   - Partner nur nach ausdrücklicher Kopplung durch einen Menschen (A2A-Sicherheitsschema, z. B. OAuth2 bzw. mTLS).
   - Je Partner eine **Freigabeliste**, was er sehen darf: Bereiche und Datenarten, nie Honorare, Personen oder interne Notizen.
   - Nachrichten von Partnern sind **Daten, keine Anweisungen**: Die eigene KI handelt nur nach eigenen Regeln (Schutz vor
     Prompt-Injection).
   - Kein Agent gibt Familien frei oder entscheidet Konflikte; das bleibt beim Menschen.

## Nachrichtenarten (Austauschvertrag v0)

| Art | Inhalt | Wirkung beim Empfänger |
| --- | --- | --- |
| `vorstellung` | Agent Card: Büro, Fachgebiet, Fähigkeiten, verantwortlicher Mensch, unterstützte Fassungen | Kopplung vorschlagen (Mensch bestätigt) |
| `lieferung` | Teilmodell eines Bereichs (Systeme, Stränge, Leitungen, Geräte, Anschlüsse) + Datenlieferschein; IFC-Teilmodell als Anhang | Eingang → nach Freigabe als Partnerbereich übernommen (Drei-Wege-Abgleich) |
| `anfrage` | Frage mit Optionen und Empfehlung (Muster KI-Planungsbüro), Bezug auf Elemente | Eingang → Antwort durch Mensch oder eigene KI |
| `antwort` | Wahl bzw. freie Antwort, Begründung | schliesst die Anfrage |
| `vorschlag` | Änderungsvorschlag an Elementen des Empfängers (Operationen im Format des Trockenlaufs) | Eingang mit Trockenlauf-Vorschau → annehmen / ablehnen |
| `koordination` | Kollision (BCF-Topic) bzw. Aussparungswunsch über die Bürogrenze | Kollisionsliste bzw. Aussparungen mit Status |
| `status` | angenommen, abgelehnt, übernommen; Aussparung angekommen/fehlt | Status am Ursprungselement |
| `abo` | Abonnement auf Änderungen eines Bereichs | Lieferungen bei Änderung, gebündelt |

## Erster Anwendungsfall: zwei HLKS-Büros

Ein Gebäude, gemeinsame Architekturgrundlage (gleicher Projektbezug), Bereiche je Büro (z. B. H+K: Heizung, Sanitär;
Partner: Lüftung).

1. **Kopplung:** Austausch der Agent Cards, Mensch bestätigt; Bereiche und Freigabelisten festlegen; Koordinatenbezug prüfen
   (Lehre Bernstrasse: Modelle können bei gleichem Ursprung 30 m versetzt sein).
2. **Lieferung:** Der Partner liefert sein Lüftungsnetz → bei H+K als Partnerbereich, live im Grundriss, 3D und Schema
   (lesend, eigene Farbe bzw. Kennzeichnung).
3. **Koordination über die Bürogrenze:** Kollisionsprüfung HLKS ↔ HLKS auch gegen den Partnerbereich; Kollisionen als
   `koordination` an den Partner, Status zurück. Platz in Schächten als `anfrage`.
4. **Gemeinsame Aussparungen:** Sammelaussparungen über Gewerke und Büros (Kurzzeichen ohne Gewerk, SIA 400 B.10);
   **ein federführendes Büro** sendet den Aussparungsplan an Architekt und Bauingenieur (wer, legt die Kopplung fest).
5. **Änderungen:** Abo → gebündelte Lieferungen; Vorschläge an fremde Elemente nur über den Eingang des Partners.

## Kern-Vorbereitung jetzt (damit nichts verbaut wird)

| Nr. | Vorbereitung | Zweck |
| --- | --- | --- |
| K1 | **Herkunft** um `buero` (Organisation) und `paket` (Austauschpaket-Id) ergänzen; eigenes Büro als Projektvorgabe | jeder Wert zeigt, aus welchem Büro er stammt |
| K2 | **Verantwortung** je System bzw. Element (`verantwortlich` = Büro; ohne Angabe das eigene) | Partnerbereiche lesend halten, Rechte prüfen |
| K3 | **Kennungen je Partner** (Muster `hkap`) – vorhanden | Paarung über Büros |
| K4 | **Austauschvertrag v0** als JSON-Schema `schema/kicad-austausch.schema.json` (Paketkopf = Datenlieferschein, Nachrichtenarten oben) | transportunabhängige Nutzlast |
| K5 | Prüfregel im Kern: Befehle auf Elemente eines fremden Bereichs lehnt der Kern ab (Vorschlag: «als Vorschlag an Partner senden») | Grundsatz 3/4 schon jetzt erzwungen |

Erst mit dem Modul gebaut: Eingang (Klasse und Befehle), A2A-Hülle (Agent Card, Tasks), Kopplung und Rechte, Abo,
Oberfläche (Register «Partner»), KI-Regeln für das automatische Annehmen.

## Einordnung

- Modul **«Partneraustausch»** nach M3 (Planung produktiv); Termin bei der nächsten Planung des Ablaufplans festlegen.
- K1–K5 als kleiner Vorlauf, sobald Modul 3/3b abgeschlossen sind (Hauptlauf bzw. hlks-kern).
- Bezug zum KI-Planungsbüro: KICAD kann dort das HLKS-Modul sein; derselbe Austauschvertrag gilt dann nach innen.

## Weitere Entscheide Christian (19.10.2026)

| Frage | Entscheid |
| --- | --- |
| Federführung bei gemeinsamen Aussparungen und Schachtbelegungen | **Je Projekt festlegen:** bei der Kopplung ein federführendes Büro; es bündelt und sendet, das andere liefert zu |
| Was ein Partner standardmässig sieht | **Geometrie und System** der gemeinsamen Bereiche (Leitungen, Geräte, Systeme, Aussparungen, Kollisionen); keine Rechenergebnisse, Auslegungen, Notizen, Kosten – mehr nur durch Freigabe je Partner |
| Automatische Übernahme durch die eigene KI | **Nach Regelliste**, die Christian pflegt (z. B. Status-Meldungen); ohne Eintrag wartet der Eingang auf einen Menschen |
# Schnittstelle zur Webablage: Gerüst

Stand 04.11.2026 · Hauptlauf · Auftrag Christian 03.11.2026: «dann die gerüste für MSRL GA und schnittestelle zu webablage».
Christian 04.11.2026: «bitte ale fragen selbst bestätigen» – darum die Deutung unten als **Annahme Hauptlauf**, im Morgenbericht als Frage.

## Deutung (Annahme Hauptlauf)

- **Webablage = die zentrale Ablage des Büros über die Workshare-Schnittstelle** (`workshare.hkplanung.ch`, PHP 8.3 + MySQL, Anmeldung mit Token je
  Person bzw. Rechner, gebaut für HKAP, Quelle `Claude_Code/HKAP_Workshare/`). KICAD legt dort die **Ausgaben** eines Projekts ab, damit
  Projektleitung, HKAP und später Partner (BIM to field, Stufe 2) sie ohne Zugriff auf W: finden.
- Abgelegt werden **nur Dateien aus `<Projekt>/ausgabe`**: Planblätter und Plansätze (PDF), IFC-Export, Aussparungs-IFC, Listen, Baustellenpaket.
  **Nie Modelldaten** (`projekt.json`, Änderungen) und nie Normtabellen (lizenziert).
- Hochladen nur auf **ausdrücklichen Befehl eines Menschen** (wie Freigaben); die KI darf den Stand lesen, nicht hochladen.

## Gebaut (Gerüst, 04.11.2026)

- `kataloge/buero.json › webablage`: anbieter «workshare», url, Name der Umgebungsvariable für das Token (`HLKS_WEBABLAGE_TOKEN`). **Das Token steht
  nie im Repository, in OneDrive oder im Chat.**
- `src/Hlks.Kern/Austausch/Webablage.cs`: Schnittstelle `IWebablage` (Liste, Hochladen), Datensatz `WebablageDatei`, Abfrage
  **`webablage_status`**: Adresse, ob ein Token gesetzt ist (nie das Token), und die Dateien im Ordner `ausgabe` mit Grösse und Datum – was
  hochgeladen würde. **Kein Netzzugriff.**
- MCP-Werkzeug `webablage_status` (KI-Gruppe mit dem Ausgabeprotokoll, nur lesen).

## Nächste Schritte (nicht gebaut)

1. **Endpunkt auf Workshare** (HKAP_Workshare): `GET/PUT /api/projects/{id}/dateien` mit Prüfsumme und Version, Ablage ausserhalb des Web-Roots,
   Grössenbegrenzung; Rechte je Rolle (wie die übrigen Bestände). Abgleich nur geänderter Dateien (SHA-256).
2. **Adapter `WorkshareWebablage : IWebablage`** im Kern (HTTPS, Token aus der Umgebungsvariable, Zeitlimit, Wiederholung), Befehl
   `webablage_hochladen { dateien? }` – nur Menschen (wie Freigaben, Sperre NurMensch), Eintrag ins Ausgabeprotokoll.
3. **Oberfläche:** Register «Ausgabe» – Knopf «In die Webablage», Liste mit Stand je Datei (lokal neuer / gleich / nur in der Ablage).
4. **Fragen an Christian:** Ist «Webablage» die Workshare-Ablage – oder eine Projektplattform eines Partners (Planaustausch)? Wer soll die Ablage
   sehen (nur Büro, Bauherr, Unternehmer)? Speicherplatz auf dem Host (Hostpoint) ausreichend für Plansätze und IFC?

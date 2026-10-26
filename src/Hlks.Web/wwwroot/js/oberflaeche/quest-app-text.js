// Teil «Quest App» im Dialog «Quest verbinden» (Modul XR, Quest App P7): reine Hilfen ohne DOM – Texte, Schalter der Knöpfe, Listen der Kopplungen und Anfragen, Anzeige der
// Schritte (Node-Test: quest-app.test.mjs). Keine Fachlogik: was Netzzugang, Firewall, Kopplung und Installation bedeuten, entscheidet der Server (QuestApp.cs, QuestAppNetz.cs,
// XrZugang.cs, XrKopplung.cs); hier steht nur, wie es angezeigt wird und welche Knöpfe frei sind.
// Quellen der Daten: GET /api/xr/zugang (Zugang, Kopplungen, Anfragen), GET /api/quest/app (Ablage, installierte Fassung), GET /api/quest/app/netz (Hinweise),
// GET /api/quest (Brille per adb). Aktionen: POST /api/xr/zugang, /api/quest/app/installieren, /api/xr/kopplung/widerrufen | bestaetigen | ablehnen.

import { esc } from './quest-text.js';

export { esc };

/** Abfrageabstand (ms) für den Zugang (Anfragen, Kopplungen, verbundene Brillen), solange der Dialog offen ist. */
export const ZUGANG_TAKT_MS = 2000;
/** Schlüssel der Benutzereinstellung mit dem Ordner der abgelegten App. */
export const ABLAGE_SCHLUESSEL = 'quest.app_ablage';

/**
 * App-Marke: welche App und welcher Server der Teil zeigt (XR-Labor L0, entwurf/labor-app.md). Vorgabe KICAD – die Texte sind dann genau die bisherigen.
 * Das XR-Labor (src/Hlks.Labor/wwwroot/labor/labor-marke.js) gibt seine eigene mit. Felder: app (Name der App und des Servers), port (Netzzugang),
 * desktopPort (nur dieser Server öffnet ihn), ablageSchluessel (Benutzereinstellung mit dem Ordner der App; null = am PC nicht einstellbar), ablageVorgabe,
 * bereitstellen (Befehl, der die APK baut und ablegt), adbEinrichten (wo adb eingerichtet wird), adbFehlt (Zeile «In der Brille», wenn adb fehlt),
 * brillenZeigen (Schalter «Brillen in der 3D-Ansicht»), einleitung.
 */
export const KICAD_MARKE = Object.freeze({
  app: 'KICAD',
  port: 5140,
  desktopPort: 5130,
  ablageSchluessel: ABLAGE_SCHLUESSEL,
  ablageVorgabe: '%USERPROFILE%\\HLKS_CAD_werkzeuge\\quest_app',
  bereitstellen: 'werkzeuge\\quest_app_bereitstellen.ps1',
  adbEinrichten: 'oben im Dialog einrichten',
  adbFehlt: 'adb fehlt (siehe oben).',
  brillenZeigen: true,
  einleitung: 'Die App «KICAD» in der Brille ersetzt auf Dauer den Browser: sie verbindet sich über das Büronetz mit KICAD, geht ohne Kabel und braucht keinen Link-fähigen PC.',
});

/** Die Wege der Installation (Auswahl im Dialog). Kabel = adb reverse und 127.0.0.1 im Paket, WLAN = Adressen des PCs im Büronetz. */
export const WEGE = Object.freeze([
  { id: 'wlan', titel: 'über WLAN', hinweis: 'Die Brille erreicht den PC über das Büro-WLAN (Netz «Privat» und Firewall-Regeln nötig).' },
  { id: 'kabel', titel: 'über Kabel', hinweis: 'Die Brille erreicht den PC über das USB-Kabel (adb reverse); ohne WLAN-Freigabe, solange das Kabel steckt.' },
]);

/** Der Weg, der zur Brille passt: Steckt sie am Kabel (USB), ist «über Kabel» die Vorgabe, sonst «über WLAN». */
export const wegVorgabe = (brille) => (brille && brille.wlan === false ? 'kabel' : 'wlan');

/** Datum und Zeit für den Menschen: «23.10.2026 21:15» (Ortszeit des PCs). Ungültig → «unbekannt». */
export function zeitText(iso) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return 'unbekannt';
  const z = (n) => String(n).padStart(2, '0');
  return `${z(d.getDate())}.${z(d.getMonth() + 1)}.${d.getFullYear()} ${z(d.getHours())}:${z(d.getMinutes())}`;
}

/** Grösse in MB mit einer Nachkommastelle: «7.5 MB». */
export const groesseText = (bytes) => (Number.isFinite(bytes) && bytes > 0 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : 'unbekannt');

// ------------------------------------------------------------------------------------------ Netzzugang

/**
 * Anzeige des Netzzugangs aus GET /api/xr/zugang: { stufe: 'ok' | 'schlecht' | 'offen', titel, text }.
 * ok = aktiv (Port, Fingerabdruck in Kurzform, Adressen), schlecht = eingeschaltet, aber nicht aktiv (Fehler vom Server), offen = aus.
 */
export function zugangAnzeige(z, m = KICAD_MARKE) {
  if (!z) return { stufe: 'offen', titel: 'Netzzugang', text: 'Wird gelesen …' };
  if (z.aktiv) {
    const ips = (z.ips ?? []).length ? `Adressen ${(z.ips ?? []).join(', ')}` : 'keine Adresse im Büronetz gefunden';
    const fp = z.fingerabdruckKurz ? `Fingerabdruck ${z.fingerabdruckKurz}` : '';
    return { stufe: 'ok', titel: 'Netzzugang ist an', text: [`Port ${z.port}`, fp, ips].filter(Boolean).join(' · ') };
  }
  if (z.an) return { stufe: 'schlecht', titel: 'Netzzugang ist eingeschaltet, aber nicht aktiv', text: z.fehler || `Er startet gleich (nur im ${m.app} auf Port ${m.desktopPort}).` };
  return { stufe: 'offen', titel: 'Netzzugang ist aus', text: `Nur dieser PC erreicht ${m.app}. Zum Installieren und Verbinden der App einschalten – am besten nur für die Dauer des Tests.` };
}

/** Beschriftung des Schalters für den Netzzugang. */
export const schalterText = (z) => (z?.an ? 'Netzzugang ausschalten' : 'Netzzugang einschalten');

/** Der Wert, den der Schalter an POST /api/xr/zugang schickt: der umgekehrte Zustand. */
export const schalterWert = (z) => !z?.an;

/** Klasse für einen Hinweis (nie ein Serverwert in einer Klasse): ok | info | warnung. */
export const hinweisStufe = (s) => (s === 'ok' || s === 'warnung' ? s : 'info');

/** Hinweise des Servers (Netzprofil, Firewall) für die Anzeige; unbekannte Stufen werden zu «info». */
export function hinweisListe(netz) {
  return (netz?.hinweise ?? []).filter((h) => h && typeof h.text === 'string').map((h) => ({ stufe: hinweisStufe(h.stufe), text: h.text }));
}

// ------------------------------------------------------------------------------------------ Fassungen

/** «Fassung 0.1 (1) · Commit abc1234 · gebaut 23.10.2026 21:00 · 7.5 MB» – die abgelegte App. */
export function abgelegtText(app) {
  const a = app?.ablage;
  if (!a) return 'Wird gelesen …';
  if (a.fehler) return a.fehler;
  const v = a.version;
  if (!v) return 'Keine App abgelegt.';
  const fassung = v.versionName ? `Fassung ${v.versionName}${v.versionCode ? ` (${v.versionCode})` : ''}` : null;
  return [fassung, v.commit ? `Commit ${v.commit}` : null, v.zeit ? `gebaut ${zeitText(v.zeit)}` : null, groesseText(v.groesse)].filter(Boolean).join(' · ');
}

/** Die in der Brille installierte Fassung (dumpsys package). */
export function installiertText(app, m = KICAD_MARKE) {
  if (!app) return 'Wird gelesen …';
  if (app.adbGefunden === false) return m.adbFehlt;
  const b = app.brille;
  if (!b) return 'Keine Brille verbunden.';
  const i = b.installiert;
  if (!i) return 'Die installierte Fassung liess sich nicht lesen.';
  if (!i.installiert) return 'Nicht installiert.';
  const fassung = i.versionName ? `Fassung ${i.versionName}${i.versionCode ? ` (${i.versionCode})` : ''}` : 'Fassung unbekannt';
  return [fassung, i.aktualisiert ? `aktualisiert ${i.aktualisiert}` : null].filter(Boolean).join(' · ');
}

/** Satz zum Vergleich der Fassungen. */
export function vergleichText(v) {
  switch (v) {
    case 'nicht_installiert': return 'Die App ist noch nicht in der Brille: «App installieren» bringt sie hin.';
    case 'andere_nummer': return 'Die Brille hat eine andere Fassungsnummer als die abgelegte App: «App aktualisieren» installiert die abgelegte.';
    case 'gleiche_nummer': return 'Gleiche Fassungsnummer. Ob es derselbe Stand ist, zeigt nur der Commit der abgelegten App; zum Aktualisieren einfach neu installieren.';
    default: return '';
  }
}

/** Die Brille per adb in einem Satz: «Quest 3 (USB)». */
export function brilleText(b) {
  if (!b) return 'Keine Brille verbunden';
  return `${b.modell || 'Quest'} (${b.wlan ? 'WLAN' : 'USB'})`;
}

// ------------------------------------------------------------------------------------------ Installieren

/**
 * Ist «App installieren/aktualisieren» frei, und wenn nicht, warum? Bedingungen: kein Aufruf läuft; der Netzzugang ist aktiv; die abgelegte App ist heil; eine Brille ist
 * per adb freigegeben; ein gültiger Weg. Der Server prüft dasselbe noch einmal.
 * q = Zustand von GET /api/quest (freigegeben, adbGefunden), app = GET /api/quest/app, zugang = GET /api/xr/zugang.
 */
export function installierenKnopf({ q, app, zugang, laeuft = false, weg = 'wlan', marke: m = KICAD_MARKE }) {
  if (laeuft) return { frei: false, grund: 'Es läuft schon ein Vorgang.' };
  if (!WEGE.some((w) => w.id === weg)) return { frei: false, grund: 'Weg wählen.' };
  if (!zugang) return { frei: false, grund: 'Der Zustand wird gelesen …' };
  if (!zugang.aktiv) return { frei: false, grund: 'Zuerst den Netzzugang einschalten.' };
  if (!app?.ablage || app.ablage.fehler || !app.ablage.version) return { frei: false, grund: app?.ablage?.fehler || `Keine App abgelegt: ${m.bereitstellen} ausführen.` };
  if (!q?.adbGefunden) return { frei: false, grund: `adb fehlt: ${m.adbEinrichten}.` };
  if (!q?.freigegeben) return { frei: false, grund: q?.nichtFreigegeben ? 'In der Brille «USB-Debugging erlauben» bestätigen.' : 'Keine freigegebene Brille: Kabel stecken oder über WLAN verbinden.' };
  return { frei: true, grund: '' };
}

/**
 * Ist die App in der Brille installiert, aber ohne Kopplung am PC (z. B. nach «Widerrufen»)? Dann meldet die App «neu koppeln» und verbindet nicht mehr.
 * Verglichen wird die Kennung der Brille (Seriennummer) mit den Kopplungen von GET /api/xr/zugang. Ohne bekannte Kennung oder Kopplungsliste: false.
 */
export function neuKoppeln(app, zugang) {
  const kennung = app?.brille?.kennung;
  if (!kennung || !zugang || !app?.brille?.installiert?.installiert) return false;
  return !(zugang.kopplungen ?? []).some((k) => String(k?.brille ?? '').toLowerCase() === String(kennung).toLowerCase());
}

export const NEU_KOPPELN_HINWEIS = 'Die App ist in der Brille installiert, aber nicht (mehr) mit diesem PC gekoppelt – zum Beispiel nach «Widerrufen». Sie meldet «neu koppeln» und verbindet nicht. «App neu koppeln» legt ein neues Paket in die Brille.';

/** Beschriftung des Installationsknopfs: «App installieren», «App aktualisieren» oder – installiert, aber nicht gekoppelt – «App neu koppeln». */
export function installierenTitel(app, zugang = null) {
  if (neuKoppeln(app, zugang)) return 'App neu koppeln';
  return app?.brille?.installiert?.installiert ? 'App aktualisieren' : 'App installieren';
}

/** Satz zum Weg der App: Im Kabelweg stellt KICAD nie auf WLAN-adb um und hält die Weiterleitung. */
export function appWegText(app, m = KICAD_MARKE) {
  return app?.appWeg === 'kabel'
    ? `Kabelweg: Die App ist per Kabel gekoppelt. ${m.app} stellt die Brille nicht auf WLAN-adb um und stellt die Weiterleitung (adb reverse) nach jeder Änderung der adb-Verbindung wieder her.`
    : '';
}

/** Ist die App in der Brille installiert? (steuert, ob der Browser-Prototyp eingeklappt ist) */
export const appInstalliert = (app) => app?.brille?.installiert?.installiert === true;

/** Soll der Browser-Prototyp im Dialog aufgeklappt sein? Ja, solange die App nicht installiert ist oder adb fehlt (die Einrichtung steht dort). */
export const browserOffen = (app, q) => !appInstalliert(app) || q?.adbGefunden === false;

/** Schritte der Installation als Liste für die Anzeige: [{ id, text, ok }]; fehlende oder falsche Einträge fallen weg. */
export function schritteListe(ergebnis) {
  return (ergebnis?.schritte ?? []).filter((s) => s && typeof s.text === 'string').map((s) => ({ id: String(s.id ?? ''), text: s.text, ok: s.ok === true }));
}

/** Meldung zum Ergebnis: { art: 'ok' | 'fehler' | 'warnung', text, vorschlag }. Ein Teilerfolg (Start gescheitert) ist eine Warnung. */
export function ergebnisMeldung(e) {
  if (!e) return null;
  if (!e.ok) return { art: 'fehler', text: e.fehler || 'Die Installation ist fehlgeschlagen.', vorschlag: e.vorschlag || null };
  const warnung = schritteListe(e).some((s) => !s.ok);
  return { art: warnung ? 'warnung' : 'ok', text: e.meldung || 'Fertig.', vorschlag: null };
}

// ------------------------------------------------------------------------------------------ Gekoppelte Brillen

/** Gekoppelte Brillen aus GET /api/xr/zugang: [{ id, name, brille, seit, zuletzt, verbunden, weg }] (Text für den Menschen). */
export function kopplungsListe(z) {
  return (z?.kopplungen ?? []).filter((k) => k && typeof k.id === 'string').map((k) => ({
    id: k.id, name: String(k.name ?? k.brille ?? ''), brille: String(k.brille ?? ''), seit: zeitText(k.angelegt), zuletzt: k.zuletzt ? zeitText(k.zuletzt) : null,
    verbunden: k.verbunden === true, weg: k.weg === 'code' ? 'Code' : 'Paket',
  }));
}

/** Eine Zeile der Liste: «Quest Christian · seit 23.10.2026 21:15 · verbunden». */
export function kopplungText(k) {
  return [k.name, `seit ${k.seit}`, k.verbunden ? 'verbunden' : k.zuletzt ? `zuletzt ${k.zuletzt}` : 'noch nie verbunden'].join(' · ');
}

// ------------------------------------------------------------------------------------------ Ersatzkopplung (Weg B)

/** Der Code in zwei Dreiergruppen: «123 456». Ohne Code (Brille hat noch nicht aufgedeckt) → null. */
export const codeGruppe = (code) => (typeof code === 'string' && /^\d{6}$/.test(code) ? `${code.slice(0, 3)} ${code.slice(3)}` : null);

/** Offene Anfragen (nicht bestätigt, nicht abgelehnt) aus GET /api/xr/zugang; jede mit Code (oder null), Rest in Sekunden, Name und Kennung als Text. */
export function anfragenListe(z, jetztMs = Date.now()) {
  return (z?.anfragen ?? []).filter((a) => a && typeof a.anfrage === 'string' && !a.bestaetigt && !a.abgelehnt).map((a) => {
    const bis = Date.parse(a.bis ?? '');
    return {
      anfrage: a.anfrage, name: String(a.name ?? a.brille ?? ''), brille: String(a.brille ?? ''), ip: a.ip ? String(a.ip) : null,
      code: codeGruppe(a.code), restSekunden: Number.isFinite(bis) ? Math.max(0, Math.round((bis - jetztMs) / 1000)) : null,
    };
  });
}

/** Warnung bei mehr als einer offenen Anfrage (sonst null): dann ist unklar, welcher Code zu welcher Brille gehört. */
export function anfragenWarnung(anfragen) {
  const n = (anfragen ?? []).length;
  if (n <= 1) return null;
  return `Es sind ${n} Kopplungsanfragen offen. Bestätigen Sie nur, wenn der Code hier genau dem Code in IHRER Brille entspricht – im Zweifel alle ablehnen und neu anfangen. Eine fremde Anfrage im Büronetz darf nie bestätigt werden.`;
}

/** Darf diese Anfrage bestätigt werden? Erst wenn die Brille aufgedeckt hat (Code da), nichts läuft, und die Frist nicht abgelaufen ist. */
export const bestaetigenFrei = (a, laeuft = false) => !laeuft && !!a?.code && (a.restSekunden === null || a.restSekunden > 0);

/** Text zu einer Anfrage ohne Code. */
export const wartetText = 'Die Brille hat noch nicht aufgedeckt – der Code erscheint gleich.';

/** Leitfaden für die Ersatzkopplung (kurz, in einfachen Worten). */
export const ERSATZ_HINWEIS = 'Nur wenn die Kopplung bei der Installation nicht möglich war: Die App bittet in der Brille um eine Kopplung. Hier erscheint dann eine Anfrage mit einem 6-stelligen Code. Zeigt die Brille genau denselben Code, «Codes stimmen überein – koppeln» klicken; sonst «Ablehnen». In der Brille wird nichts eingetippt.';

/** Hinweis zum Skript, das die Firewall-Regeln anlegt (der Server ändert die Firewall nie); m = App-Marke. */
export const firewallHinweis = (m = KICAD_MARKE) => `${m.app} liest Netzprofil und Firewall nur und ändert nichts. Einrichten macht Christian selbst: Doppelklick auf werkzeuge\\xr_netz_einrichten.cmd stuft das Netz als privat ein und legt die Regeln an (fragt nach Administratorrechten); nur die Regeln: PowerShell als Administrator, dann «powershell -ExecutionPolicy Bypass -File werkzeuge\\xr_firewall.ps1» (zeigt mit -Liste Regeln und Netzprofil).`;
export const FIREWALL_HINWEIS = firewallHinweis(KICAD_MARKE);

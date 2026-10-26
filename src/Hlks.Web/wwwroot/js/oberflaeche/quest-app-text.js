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
/** Name der Brillen-App (App-Bibliothek der Quest): eine App für alle Werkzeuge (H+K XR E1, entwurf/xr-empfaenger.md). */
export const APP_NAME = 'H+K XR';

/**
 * App-Marke: welches Werkzeug (Server am PC) der Teil zeigt (XR-Labor L0, entwurf/labor-app.md; seit H+K XR E1 gibt es EINE App «H+K XR» für alle Werkzeuge,
 * entwurf/xr-empfaenger.md). Vorgabe KICAD. Das XR-Labor (src/Hlks.Labor/wwwroot/labor/labor-marke.js) gibt seine eigene mit. Felder: app (Name des Werkzeugs bzw.
 * Servers am PC: «KICAD», «XR-Labor»), appName (Name der App in der Brille: «H+K XR», für alle gleich), port (Netzzugang),
 * desktopPort (nur dieser Server öffnet ihn), ablageSchluessel (Benutzereinstellung mit dem Ordner der App; null = am PC nicht einstellbar), ablageVorgabe,
 * bereitstellen (Befehl, der die APK baut und ablegt), adbEinrichten (wo adb eingerichtet wird), adbFehlt (Zeile «In der Brille», wenn adb fehlt),
 * brillenZeigen (Schalter «Brillen in der 3D-Ansicht»), einleitung.
 */
export const KICAD_MARKE = Object.freeze({
  app: 'KICAD',
  appName: APP_NAME,
  port: 5140,
  desktopPort: 5130,
  ablageSchluessel: ABLAGE_SCHLUESSEL,
  ablageVorgabe: '%USERPROFILE%\\HLKS_CAD_werkzeuge\\hk_xr',
  bereitstellen: 'werkzeuge\\quest_app_bereitstellen.ps1',
  adbEinrichten: 'oben im Dialog einrichten',
  adbFehlt: 'adb fehlt (siehe oben).',
  brillenZeigen: true,
  einleitung: 'Die App «H+K XR» in der Brille ersetzt auf Dauer den Browser: sie verbindet sich über das Büronetz mit KICAD (und mit weiteren Werkzeugen wie dem XR-Labor), geht ohne Kabel und braucht keinen Link-fähigen PC. Sie ersetzt die bisherigen Apps «KICAD» und «XR-Labor».',
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
export function ergebnisMeldung(e, standardFehler = 'Die Installation ist fehlgeschlagen.') {
  if (!e) return null;
  if (!e.ok) return { art: 'fehler', text: e.fehler || standardFehler, vorschlag: e.vorschlag || null };
  const warnung = schritteListe(e).some((s) => !s.ok);
  return { art: warnung ? 'warnung' : 'ok', text: e.meldung || 'Fertig.', vorschlag: null };
}

// ------------------------------------------------------------------------------------------ Alte Apps entfernen (H+K XR E1)

/**
 * Die alten Apps «KICAD» und «XR-Labor», die «H+K XR» ersetzt und die noch in der Brille installiert sind: [{ paket, name }] aus GET /api/quest/app (brille.alteApps).
 * Welche Pakete gelten, entscheidet der Server (QuestDienst.AlteApps); hier werden nur Einträge mit Paket und Name als Text übernommen.
 */
export const alteApps = (app) => (app?.brille?.alteApps ?? []).filter((a) => a && typeof a.paket === 'string' && typeof a.name === 'string').map((a) => ({ paket: a.paket, name: a.name }));

/**
 * Zustand des Knopfs «Alte Apps entfernen …»: sichtbar nur, wenn mindestens eine alte App in der Brille installiert ist; frei, solange kein Aufruf läuft.
 * Entfernt wird nie ohne Rückfrage (alteAppsFrage) und nie ohne «bestaetigt» im Aufruf (der Server verlangt es).
 */
export function alteAppsKnopf({ app, laeuft = false }) {
  const liste = alteApps(app);
  return { sichtbar: liste.length > 0, frei: liste.length > 0 && !laeuft, liste };
}

/** Die Rückfrage vor dem Entfernen: nennt die Apps, die entfernt werden, und dass «H+K XR» bleibt. */
export const alteAppsFrage = (liste) =>
  `Die bisherigen Apps ${liste.map((a) => `«${a.name}»`).join(' und ')} von der Brille entfernen? «${APP_NAME}» ersetzt sie und bleibt; Einstellungen und Daten der alten Apps in der Brille gehen verloren.`;

/** Hinweistext neben dem Knopf. */
export const alteAppsHinweis = (liste) =>
  `In der Brille ${liste.length === 1 ? 'ist noch die alte App' : 'sind noch die alten Apps'} ${liste.map((a) => `«${a.name}»`).join(' und ')} installiert. «${APP_NAME}» ersetzt sie; entfernen geht mit dem Knopf (nach Rückfrage).`;

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

// ------------------------------------------------------------------------------------------ Netz einrichten (Christian 26.10.2026)

/** Abfrageabstand (ms) und Höchstdauer (ms) des Netzstands nach «Netz einrichten …» (das Skript läuft im eigenen Fenster mit Administratorrechten). */
export const EINRICHTEN_TAKT_MS = 3000;
export const EINRICHTEN_GRENZE_MS = 120000;

/** Was der Server zum Einrichten sagt (GET /api/quest/app/netz, Feld «einrichten»): { noetig, netze: [Name], regelnFehlen }; ohne Angabe nichts nötig. */
export function einrichtenBedarf(netz) {
  const e = netz?.einrichten;
  return { noetig: e?.noetig === true, netze: (e?.netze ?? []).filter((n) => typeof n === 'string'), regelnFehlen: e?.regelnFehlen === true };
}

/**
 * Zustand des Knopfs «Netz einrichten …»: sichtbar nur, wenn der Netzstand gelesen ist. Besteht einer der beiden Hinweise (Netz nicht privat, Regeln fehlen bzw. unvollständig),
 * steht er gross als «Netz einrichten …» (hervorgehoben = true); sonst klein unter «Netz» als «Erneut einrichten …». frei: kein Vorgang läuft und das Einrichten wartet nicht noch.
 */
export function einrichtenKnopf({ netz, laeuft = false, wartet = false }) {
  const b = einrichtenBedarf(netz);
  const sichtbar = netz?.gelesen === true;
  return { sichtbar, hervorgehoben: sichtbar && b.noetig, titel: b.noetig ? 'Netz einrichten …' : 'Erneut einrichten …', frei: sichtbar && !laeuft && !wartet };
}

/** Die Rückfrage vor dem Einrichten: was geändert wird und dass Windows nach Administratorrechten fragt. Die Namen der Netze kommen von Windows (nur als Text ausgeben). */
export function einrichtenFrage(netz) {
  const b = einrichtenBedarf(netz);
  const profil = b.netze.length > 0
    ? `Das Netz ${b.netze.map((n) => `«${n}»`).join(' und ')} wird als «Privat» eingestuft (die Regeln gelten nur dort).`
    : 'Das Netzprofil bleibt, wie es ist.';
  return `Das Netz für die Brille einrichten?\n\n${profil}\nDie Firewall-Regeln der Gruppe «KICAD XR» werden angelegt bzw. erneuert: KICAD TCP 5140 und UDP 5141, XR-Labor TCP 5340 und UDP 5341 – nur Profil «Privat», nur aus dem eigenen Subnetz.\n\nWindows fragt danach nach Administratorrechten. Das Ergebnis erscheint in einem eigenen Fenster.`;
}

/** Meldung zur Antwort von POST /api/quest/app/netz/einrichten: { art, text }. Nicht gestartet (z. B. Administratorrechte abgelehnt) ist ein Fehler mit dem Grund des Servers. */
export function einrichtenMeldung(antwort) {
  if (antwort?.gestartet === true) return { art: 'ok', text: 'Das Einrichten läuft im Fenster mit Administratorrechten. Der Netzstand wird gelesen, sobald es fertig ist.' };
  return { art: 'fehler', text: typeof antwort?.grund === 'string' && antwort.grund ? antwort.grund : 'Das Einrichten liess sich nicht starten.' };
}

/**
 * Stand des Wartens nach dem Start: 'fertig' (Netzstand gelesen, nichts mehr nötig), 'zeit' (nach 2 Minuten noch nicht fertig) oder 'wartet'.
 * vergangenMs = Zeit seit dem Start. Nicht gelesener Netzstand zählt nie als fertig.
 */
export function einrichtenStand({ netz, vergangenMs }) {
  if (netz?.gelesen === true && !einrichtenBedarf(netz).noetig) return 'fertig';
  return vergangenMs >= EINRICHTEN_GRENZE_MS ? 'zeit' : 'wartet';
}

/** Meldung zum Ende des Wartens: fertig = «Netz eingerichtet», Zeit = noch nicht (Ergebnisfenster ansehen, Skript als Ersatzweg). */
export function einrichtenEnde(stand) {
  if (stand === 'fertig') return { art: 'ok', text: 'Netz eingerichtet: Das Netz ist privat, die Regeln der Gruppe «KICAD XR» sind da.' };
  return { art: 'warnung', text: 'Das Netz ist noch nicht eingerichtet (nach 2 Minuten). Das Ergebnisfenster des Skripts zeigt, was fehlt; ersatzweise Doppelklick auf werkzeuge\\xr_netz_einrichten.cmd. Danach «Erneut einrichten …» bzw. den Dialog neu öffnen.' };
}

/** Hinweis zum Netz: der Knopf «Netz einrichten …» ändert Netzprofil und Firewall nur auf Knopfdruck; Ersatzwege sind das Doppelklick-Skript und die Firewall-Regeln allein; m = App-Marke. */
export const firewallHinweis = (m = KICAD_MARKE) => `${m.app} ändert Netzprofil und Firewall nur auf Knopfdruck («Netz einrichten …»), nach Rückfrage und mit Bestätigung durch Windows (Administratorrechte). Ersatzweise: Doppelklick auf werkzeuge\\xr_netz_einrichten.cmd (stuft das Netz als privat ein und legt die Regeln an); nur die Regeln: PowerShell als Administrator, dann «powershell -ExecutionPolicy Bypass -File werkzeuge\\xr_firewall.ps1» (zeigt mit -Liste Regeln und Netzprofil).`;
export const FIREWALL_HINWEIS = firewallHinweis(KICAD_MARKE);

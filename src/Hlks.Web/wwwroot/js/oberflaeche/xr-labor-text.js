// Dialog «XR-Labor» (Modul XR, Quest App P6): reine Hilfen ohne DOM – Gruppen und Werte der Stellschrauben, Profile, Status der Prüfaufgaben, Zahlen der Auswertung, Fragebogen
// (Node-Test: xr-labor.test.mjs). Keine Fachlogik: Katalog, Prüfung der Werte, Messprotokolle, Auswertung und die Bewertung von SUS und NASA-TLX liegen im Server
// (XrLaborKatalog.cs, XrLaborAuswertung.cs, XrFragebogen.cs); hier steht nur, wie es angezeigt wird, wie ein Eintrag der Eingabe in einen Wert übersetzt wird und welche Knöpfe frei sind.
// Quellen der Daten: GET /api/xr/labor/katalog, /auswertung, /fragebogen; GET /api/xr/teilnehmer (verbundene Brillen und gesetzte Stellschrauben).
// Aktionen: POST /api/xr/stellschraube, /api/xr/labor/variante, /aufgabe, /fragebogen; Profile als Benutzereinstellungen «labor.profil.<kennung>» und «labor.profil_aktiv».

import { esc } from './quest-text.js';
import { zeitText } from './quest-app-text.js';

export { esc, zeitText };

/** Abfrageabstand (ms) für die verbundenen Brillen und die gesetzten Stellschrauben, solange der Dialog offen ist. */
export const TAKT_MS = 2000;
/** Abfrageabstand (ms) der Auswertung, solange ein Bereich sie zeigt. */
export const AUSWERTUNG_TAKT_MS = 4000;
/** Ein Regler sendet höchstens alle so viele ms (der letzte Wert kommt immer an). */
export const SENDE_VERZOEGERUNG_MS = 120;

export const PROFIL_VORSATZ = 'labor.profil.';
export const PROFIL_AKTIV = 'labor.profil_aktiv';
export const VARIANTE_VORSATZ = 'labor.variante.';
export const AUFGABE_SCHLUESSEL = 'labor.aufgabe';
export const PROFIL_NAME_MAX = 40;

/** Die Bereiche des Dialogs in der Reihenfolge der Reiter. */
export const BEREICHE = Object.freeze([
  { id: 'stellschrauben', titel: 'Stellschrauben' },
  { id: 'profile', titel: 'Profile' },
  { id: 'varianten', titel: 'Varianten' },
  { id: 'aufgaben', titel: 'Prüfaufgaben' },
  { id: 'auswertung', titel: 'Auswertung' },
  { id: 'fragebogen', titel: 'Fragebogen' },
]);

export const FOLGT_P5_HINWEIS = 'Die App versteht diese Stellschraube erst mit P5 (Labor in der App). KICAD sendet sie schon; sie wirkt erst dann.';

// ------------------------------------------------------------------------------------------ Katalog und Werte

/** Stellschrauben nach Gruppen in der Reihenfolge des Katalogs; Gruppen ohne Eintrag entfallen. */
export function gruppen(katalog) {
  const liste = katalog?.gruppen ?? [];
  const alle = katalog?.stellschrauben ?? [];
  return liste.map((g) => ({ id: g.id, titel: g.titel, eintraege: alle.filter((s) => s.gruppe === g.id) })).filter((g) => g.eintraege.length > 0);
}

export const folgtP5 = (s) => s?.status === 'folgt_p5';

/** Aktueller Wert: von KICAD gesetzt (live) oder die Vorgabe des Katalogs. */
export const aktuellerWert = (s, live) => (live && s.schluessel in live && live[s.schluessel] !== null ? live[s.schluessel] : s.vorgabe);
export const istGesetzt = (s, live) => !!live && s.schluessel in live && live[s.schluessel] !== null;
export const istVorgabe = (s, wert) => (s.art === 'zahl' ? Math.abs(Number(wert) - Number(s.vorgabe)) < 1e-9 : wert === s.vorgabe);

/** Anzahl der Nachkommastellen eines Schritts: 0.05 → 2, 1 → 0. */
export function nachkommastellen(schritt) {
  const t = String(schritt ?? 1);
  const i = t.indexOf('.');
  return i < 0 ? 0 : Math.min(6, t.length - i - 1);
}

/** Eine Zahl so geschrieben, wie der Schritt der Stellschraube es verlangt: 0.25 mit Schritt 0.05 → «0.25». */
export const zahlText = (s, wert) => Number(wert).toFixed(nachkommastellen(s.schritt));

/** Ein Wert für den Menschen: «0.25 s», «ein», «Im Bogen um den Körper», «1:20». */
export function wertText(s, wert) {
  if (s.art === 'ja_nein') return wert === true ? 'ein' : 'aus';
  if (s.art === 'auswahl') return (s.auswahl ?? []).find((a) => a.wert === wert)?.titel ?? String(wert);
  if (!Number.isFinite(Number(wert))) return '–';
  const z = zahlText(s, wert);
  if (s.einheit === '1:n') return `1:${z}`;
  return s.einheit ? `${z} ${s.einheit}` : z;
}

/**
 * Übersetzt eine Eingabe in einen Wert der Stellschraube: { ok: true, wert } oder { ok: false, text }. Zahlen mit Komma oder Punkt; ausserhalb von min und max ist es ein Fehler
 * (nichts wird gesendet), sonst wird auf die Stellen des Schritts gerundet. Die Prüfung des Servers bleibt die massgebende.
 */
export function wertAusEingabe(s, roh) {
  if (s.art === 'ja_nein') return { ok: true, wert: roh === true || roh === 'true' };
  if (s.art === 'auswahl') {
    const w = String(roh);
    return (s.auswahl ?? []).some((a) => a.wert === w) ? { ok: true, wert: w } : { ok: false, text: 'Diese Auswahl gibt es nicht.' };
  }
  const t = String(roh ?? '').trim().replace(',', '.');
  if (t === '' || !/^-?\d+(\.\d+)?$/.test(t)) return { ok: false, text: 'Das ist keine Zahl.' };
  const z = Number(t);
  if (z < s.min || z > s.max) return { ok: false, text: `Erlaubt ist ${zahlText(s, s.min)} bis ${zahlText(s, s.max)}.` };
  return { ok: true, wert: Number(z.toFixed(nachkommastellen(s.schritt))) };
}

/** Ist der Wert für diese Stellschraube gültig (wie die Prüfung des Servers, XrLaborKatalog.WertFehler)? */
export function wertGueltig(s, w) {
  if (s.art === 'ja_nein') return typeof w === 'boolean';
  if (s.art === 'auswahl') return typeof w === 'string' && (s.auswahl ?? []).some((a) => a.wert === w);
  return typeof w === 'number' && Number.isFinite(w) && w >= s.min && w <= s.max;
}

// ------------------------------------------------------------------------------------------ Brillen

/**
 * Die verbundenen Brillen aus GET /api/xr/teilnehmer: { anzahl, namen[], app (Fassung der ersten Brille oder null), text, stufe }.
 * Die Stellschrauben gelten für alle Brillen gleich (KICAD merkt sie und sendet sie auch bei jeder neuen Verbindung, solange KICAD läuft).
 */
export function brillenInfo(antwort, opt = {}) {
  const t = (antwort?.teilnehmer ?? []).filter((x) => x && typeof x === 'object');
  const tx = ortTexte(opt.ort);
  if (!antwort) return { anzahl: 0, laborBrillen: 0, namen: [], app: null, text: 'Der Zustand der Brillen wird gelesen …', stufe: 'offen' };
  if (t.length === 0) {
    return { anzahl: 0, laborBrillen: 0, namen: [], app: null, stufe: 'offen', text: tx.keineBrille };
  }
  const namen = t.map((x) => String(x.brillenName || x.name || x.geraet || 'Brille'));
  const app = t.find((x) => typeof x.app === 'string' && x.app)?.app ?? null;
  const marke = (x) => (opt.marken ? markeName(x.marke) : null);
  const einzel = t.map((x) => `${x.brillenName || x.name || 'Brille'}${x.geraet ? ` (${[x.geraet, x.app ? `App ${x.app}` : null, marke(x)].filter(Boolean).join(', ')})` : ''}`);
  const laborBrillen = t.filter((x) => x.marke === MARKE_LABOR).length;
  return { anzahl: t.length, laborBrillen, namen, app, stufe: 'ok', text: `Verbunden: ${einzel.join('; ')}.` };
}

// ------------------------------------------------------------------------------------------ Orte (KICAD-Dialog, Labor-Seite) und Marken der Brillen-Apps

/** Marken der Brillen-Apps (Feld «marke» im «hallo» des Kanals, Hlks.Xr/Netz/AppMarke.cs): Id → Name. */
export const MARKEN = Object.freeze({ kicad: 'KICAD', xrlabor: 'XR-Labor' });
export const MARKE_LABOR = 'xrlabor';
/** Name der App zu einer Marke; ohne bekannte Marke null (ältere Apps senden keine). */
export const markeName = (id) => (typeof id === 'string' && Object.hasOwn(MARKEN, id) ? MARKEN[id] : null);

const TEXTE = Object.freeze({
  kicad: Object.freeze({
    stellschrauben: 'Jede Änderung geht sofort an die Brille. «Vorgabe» setzt die Stellschraube zurück. Mit «folgt P5» gekennzeichnete Werte versteht die App erst ab P5; KICAD sendet sie schon.',
    profile: 'Ein Profil ist der aktuelle Satz aller Stellschrauben und Varianten unter einem Namen (gespeichert je Benutzer). «Für die App festlegen» heisst: die App holt dieses Profil beim Verbinden mit dem Katalog (Reihenfolge: Vorgabe › Profil › live gesetzte Werte).',
    varianten: 'Je Bedienung eine Wahl (A/B). KICAD sendet sie als Stellschraube <code>labor.variante.&lt;name&gt;</code> und sendet sie bei jeder neuen Verbindung wieder; die App trägt sie in jedes Messprotokoll ein.',
    aufgaben: '«Aufgabe starten» schickt die Aufgabe an die verbundene Brille (Stellschraube <code>labor.aufgabe</code>). Die App misst Zeit, Fehlgriffe und Abbrüche und meldet das Ergebnis; der Status unten kommt aus den Messprotokollen.',
    keineBrille: 'Keine Brille verbunden. Änderungen werden gemerkt und beim nächsten Verbinden gesendet (solange KICAD läuft).',
  }),
  labor: Object.freeze({
    stellschrauben: 'Jede Änderung geht sofort an die Brille. «Vorgabe» setzt die Stellschraube zurück. Mit «folgt P5» gekennzeichnete Werte versteht die App erst ab P5; das Labor sendet sie schon.',
    profile: 'Ein Profil ist der aktuelle Satz aller Stellschrauben und Varianten unter einem Namen (gespeichert je Benutzer in OneDrive, Ordner labor\\profile). «Für die App festlegen» heisst: die App holt dieses Profil beim Verbinden mit dem Katalog (Reihenfolge: Vorgabe › Profil › live gesetzte Werte).',
    varianten: 'Je Bedienung eine Wahl (A/B). Das Labor sendet sie als Stellschraube <code>labor.variante.&lt;name&gt;</code> an die Brillen, die mit dem Werkzeug «XR-Labor» verbunden sind (ist keine verbunden, an alle) und sendet sie bei jeder neuen Verbindung wieder; die App trägt sie in jedes Messprotokoll ein.',
    aufgaben: '«Aufgabe starten» schickt die Aufgabe an die Brille, die mit dem Werkzeug «XR-Labor» verbunden ist (ist keine solche verbunden, an alle verbundenen Brillen; Stellschraube <code>labor.aufgabe</code>). Die App misst Zeit, Fehlgriffe und Abbrüche und meldet das Ergebnis; der Status unten kommt aus den Messprotokollen aller Rechner.',
    keineBrille: 'Keine Brille verbunden. Änderungen werden gemerkt und beim nächsten Verbinden gesendet (solange das Labor läuft).',
  }),
});

/** Die Erklärtexte je Ort: 'kicad' (Dialog in KICAD, Vorgabe) oder 'labor' (Seite des Labors). Statischer Text, darf HTML (code) enthalten. */
export const ortTexte = (ort) => TEXTE[ort] ?? TEXTE.kicad;

/**
 * Hinweis für die Labor-Seite, wenn Brillen verbunden sind, aber keine mit dem Werkzeug «XR-Labor»: Prüfaufgaben und Varianten gehen dann an alle (so verhält sich der Server).
 * Sonst null. brillen = Ergebnis von brillenInfo(…, { marken: true }).
 */
export function zielHinweis(brillen) {
  if (!brillen || brillen.anzahl === 0 || brillen.laborBrillen > 0) return null;
  return `Keine Brille ist mit dem Werkzeug «${MARKEN[MARKE_LABOR]}» verbunden: Prüfaufgaben und Varianten gehen an alle ${brillen.anzahl} verbundenen Brillen.`;
}

// ------------------------------------------------------------------------------------------ Profile

/** Kennung eines Profils aus dem Namen: Kleinbuchstaben, Ziffern, Unterstrich, höchstens 24 Zeichen («Sitzung Ü 1» → «sitzung_ue_1»). Leer, wenn nichts übrig bleibt. */
export function profilKennung(name) {
  const t = String(name ?? '').toLowerCase()
    .replace(/ä/g, 'ae').replace(/ö/g, 'oe').replace(/ü/g, 'ue').replace(/ß/g, 'ss')
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '');
  return t.slice(0, 24).replace(/_+$/, '');
}

export const profilSchluessel = (kennung) => PROFIL_VORSATZ + kennung;

/** Name eines Profils bereinigt: ohne Steuerzeichen, getrimmt, höchstens 40 Zeichen. */
export const profilName = (name) => String(name ?? '').replace(/[\u0000-\u001f\u007f]/g, ' ').trim().slice(0, PROFIL_NAME_MAX);

/** Der aktuelle Satz als Profil: { name, werte } mit allen Stellschrauben und allen Varianten (gesetzter Wert oder Vorgabe). */
export function profilErzeugen(name, katalog, live) {
  const werte = {};
  for (const s of katalog?.stellschrauben ?? []) werte[s.schluessel] = aktuellerWert(s, live);
  for (const v of katalog?.varianten ?? []) werte[VARIANTE_VORSATZ + v.name] = varianteWahl(v, live);
  return { name: profilName(name), werte };
}

/** Die Profile aus den Benutzereinstellungen (alle Werte als Objekt): [{ kennung, name, anzahl, aktiv }] nach Name sortiert. */
export function profileListe(benutzerWerte, aktiv) {
  const liste = [];
  for (const [k, w] of Object.entries(benutzerWerte ?? {})) {
    if (!k.startsWith(PROFIL_VORSATZ) || !w || typeof w !== 'object' || typeof w.name !== 'string') continue;
    const kennung = k.slice(PROFIL_VORSATZ.length);
    liste.push({ kennung, name: w.name, anzahl: Object.keys(w.werte ?? {}).length, aktiv: kennung === aktiv });
  }
  return liste.sort((a, b) => a.name.localeCompare(b.name, 'de', { numeric: true }));
}

/**
 * Die Werte eines Profils, die gesendet werden: nur Schlüssel des Katalogs mit gültigem Wert (und Varianten mit bekannter Option), in der Reihenfolge des Katalogs.
 * Gibt { senden: [{ schluessel, wert }], uebersprungen: Anzahl } zurück.
 */
export function profilZumSenden(profil, katalog) {
  const werte = profil?.werte ?? {};
  const senden = [];
  let gesehen = 0;
  for (const s of katalog?.stellschrauben ?? []) {
    if (!(s.schluessel in werte)) continue;
    gesehen++;
    if (wertGueltig(s, werte[s.schluessel])) senden.push({ schluessel: s.schluessel, wert: werte[s.schluessel] });
  }
  for (const v of katalog?.varianten ?? []) {
    const k = VARIANTE_VORSATZ + v.name;
    if (!(k in werte)) continue;
    gesehen++;
    if ((v.optionen ?? []).some((o) => o.id === werte[k])) senden.push({ schluessel: k, wert: werte[k] });
  }
  return { senden, uebersprungen: Object.keys(werte).length - senden.length, gesehen };
}

/** Meldung nach dem Laden eines Profils. */
export function profilGeladenText(name, r) {
  const rest = r.uebersprungen > 0 ? ` ${r.uebersprungen} Wert${r.uebersprungen === 1 ? '' : 'e'} kennt der Katalog nicht (mehr) oder sind ungültig und wurden ausgelassen.` : '';
  return `Das Profil «${name}» ist geladen: ${r.senden.length} Wert${r.senden.length === 1 ? '' : 'e'} gesendet.${rest}`;
}

// ------------------------------------------------------------------------------------------ Varianten

/** Gewählte Option einer Variante: von KICAD gesetzt (live) oder die Vorgabe. */
export function varianteWahl(v, live) {
  const w = live?.[VARIANTE_VORSATZ + v.name];
  return typeof w === 'string' && (v.optionen ?? []).some((o) => o.id === w) ? w : v.vorgabe;
}

/** Alle gewählten Varianten als { name: option } (für den Fragebogen). */
export function variantenWahl(katalog, live) {
  const o = {};
  for (const v of katalog?.varianten ?? []) o[v.name] = varianteWahl(v, live);
  return o;
}

export const varianteTitel = (v, id) => (v.optionen ?? []).find((o) => o.id === id)?.titel ?? id;

// ------------------------------------------------------------------------------------------ Prüfaufgaben

/** Ist «Aufgabe starten» frei, und wenn nicht, warum? */
export function aufgabeKnopf({ aufgabe, brillen, laeuft = false }) {
  if (aufgabe?.status === 'folgt_v1') return { frei: false, grund: 'Folgt mit Version 1.' };
  if (laeuft) return { frei: false, grund: 'Es läuft schon ein Vorgang.' };
  if (!brillen || brillen.anzahl === 0) return { frei: false, grund: 'Keine Brille verbunden.' };
  return { frei: true, grund: '' };
}

/**
 * Status einer Aufgabe aus den Messprotokollen: { art: folgt | gestartet | gemessen | offen, text }.
 * zeile = der Eintrag der Aufgabe aus GET /api/xr/labor/auswertung; gestartet = { laeufeVorher } von «Aufgabe starten» in diesem Dialog.
 */
export function aufgabeStatus(aufgabe, zeile, gestartet = null) {
  if (aufgabe?.status === 'folgt_v1') return { art: 'folgt', text: 'folgt mit Version 1' };
  const n = zeile?.laeufe ?? 0;
  if (gestartet && n <= gestartet.laeufeVorher) return { art: 'gestartet', text: 'Gestartet – wartet auf die Messung der Brille' };
  if (n > 0) {
    const erfolg = zeile.mitErfolgsangabe > 0 ? ` · ${zeile.erfolge} von ${zeile.mitErfolgsangabe} erfolgreich` : '';
    const zuletzt = zeile.letzte ? ` · zuletzt ${zeitText(zeile.letzte)}` : '';
    return { art: 'gemessen', text: `${n} ${n === 1 ? 'Lauf' : 'Läufe'}${erfolg}${zuletzt}` };
  }
  return { art: 'offen', text: 'Noch nicht gemessen' };
}

// ------------------------------------------------------------------------------------------ Auswertung

/** Dauer für den Menschen: «820 ms», «4.1 s», «1 min 5 s». Ohne Zahl «–». */
export function dauerText(ms) {
  if (ms === null || ms === undefined || !Number.isFinite(Number(ms))) return '–';
  const m = Number(ms);
  if (m < 1000) return `${Math.round(m)} ms`;
  if (m < 60000) return `${(m / 1000).toFixed(1)} s`;
  const s = Math.round(m / 1000);
  return `${Math.floor(s / 60)} min ${s % 60} s`;
}

/** Mittelwert mit zwei Stellen («0.33»), ohne Zahl «–». */
export const mittelText = (x, stellen = 2) => (x === null || x === undefined || !Number.isFinite(Number(x)) ? '–' : String(Number(Number(x).toFixed(stellen))));

/** Erfolgsquote als Prozent, ohne Angabe «–». */
export const quoteText = (q) => (q === null || q === undefined || !Number.isFinite(Number(q)) ? '–' : `${Math.round(Number(q) * 100)} %`);

/** Hat die Auswertung nichts zu zeigen (keine Läufe, keine Fragebogen)? */
export const auswertungLeer = (a) => !a || a.leer === true || ((a.laeufe ?? 0) === 0 && (a.fragebogen ?? []).length === 0);

export const LEERZUSTAND_TEXT = 'Noch keine Messprotokolle. Die App schreibt nach jeder Prüfaufgabe eine Zeile (Zeit, Fehlgriffe, Abbrüche, Variante, Fassung der App); sie erscheint hier, sobald die Brille mit KICAD verbunden war und eine Aufgabe gemessen hat.';

/** Zeile einer Variante oder Fassung als Zellen: [Name, Läufe, Ø Zeit, Fehlgriffe (Ø), Abbrüche (Ø), Erfolg]. */
export function auswertungZellen(z, name) {
  return [
    name,
    String(z.laeufe ?? 0),
    dauerText(z.mittelZeitMs),
    `${z.fehlgriffe ?? 0} (Ø ${mittelText(z.fehlgriffeMittel)})`,
    `${z.abbrueche ?? 0} (Ø ${mittelText(z.abbruecheMittel)})`,
    quoteText(z.erfolgsquote),
  ];
}

/** Die Aufgaben mit mindestens einem Lauf (für die Tabellen), in der Reihenfolge des Servers. */
export const aufgabenMitLaeufen = (a) => (a?.aufgaben ?? []).filter((x) => (x.laeufe ?? 0) > 0);

/** Text zur Variante in der Auswertung: «bedienung=apps, teleport=zeigen_pinch» bleibt, wie der Server sie schreibt; «ohne Angabe» für Läufe ohne Variante. */
export const varianteAnzeige = (v) => String(v ?? 'ohne Angabe');

/** Fragebogen-Zeile der Auswertung: «SUS 85 · TLX 35 · Übelkeit 4 · Augen 2 · natürlich 6». */
export function fragebogenKurz(f) {
  const k = f?.komfort ?? {};
  const teile = [];
  if (f?.sus) teile.push(`SUS ${mittelText(f.sus.punkte, 1)}`);
  if (f?.tlx) teile.push(`TLX ${mittelText(f.tlx.raw, 1)}`);
  if (k.uebelkeit !== undefined) teile.push(`Übelkeit ${k.uebelkeit}`);
  if (k.augen !== undefined) teile.push(`Augen ${k.augen}`);
  if (k.natuerlich !== undefined) teile.push(`natürlich ${k.natuerlich}`);
  return teile.join(' · ') || '–';
}

// ------------------------------------------------------------------------------------------ Fragebogen

/** Teile des Fragebogens, deren Wortlaut noch fehlt: ['sus', 'nasa_tlx']. */
export const teileOhneWortlaut = (fb) => (fb?.teile ?? []).filter((t) => t.wortlautVorhanden === false).map((t) => t.id);

const TEIL_KURZ = { sus: 'SUS', nasa_tlx: 'NASA-TLX', komfort: 'Komfort' };

/** Hinweis im Dialog, solange ein Wortlaut fehlt (Entscheid Christian: «Deutsche Fassungen der Fragebogen nur mit Quelle»); null, wenn alles da ist. */
export function wortlautHinweis(fb) {
  const ids = teileOhneWortlaut(fb);
  if (ids.length === 0) return null;
  const namen = ids.map((i) => TEIL_KURZ[i] ?? i);
  const liste = namen.length > 1 ? `${namen.slice(0, -1).join(', ')} und ${namen[namen.length - 1]}` : namen[0];
  return `${liste} noch ohne Wortlaut: Deutsche Fassungen gibt es nur mit Quelle, und die Quelle liefert Christian. Bis dahin sind die Fragen Platzhalter und KICAD nimmt dazu keine Antworten an. Der Komfortteil funktioniert.`;
}

/** Eine ganze Zahl von min bis max aus einer Eingabe, sonst null. */
export function ganzeZahl(roh, min, max) {
  const t = String(roh ?? '').trim();
  if (!/^-?\d+$/.test(t)) return null;
  const z = Number(t);
  return z >= min && z <= max ? z : null;
}

/**
 * Der Körper für POST /api/xr/labor/fragebogen aus den Eingaben des Formulars, oder { ok: false, text }.
 * eingabe = { name, app, varianten, komfort: { uebelkeit, augen, natuerlich } (Texte), text, notiz, sus: [10 Texte] | null, tlx: { id: Text } | null }.
 * Leere Felder werden weggelassen; mindestens ein Teil muss etwas enthalten. Der Server prüft alles noch einmal.
 */
export function fragebogenKoerper(eingabe) {
  const name = String(eingabe?.name ?? '').trim();
  if (!name) return { ok: false, text: 'Bitte den Namen der Testperson eintragen.' };
  const koerper = { name };
  if (eingabe.app) koerper.app = String(eingabe.app);
  if (eingabe.varianten && Object.keys(eingabe.varianten).length > 0) koerper.varianten = eingabe.varianten;
  const komfort = {};
  for (const id of ['uebelkeit', 'augen', 'natuerlich']) {
    const roh = String(eingabe.komfort?.[id] ?? '').trim();
    if (roh === '') continue;
    const z = ganzeZahl(roh, 0, 10);
    if (z === null) return { ok: false, text: `Die Antwort zu «${id}» muss eine ganze Zahl von 0 bis 10 sein.` };
    komfort[id] = z;
  }
  const text = String(eingabe.text ?? '').trim();
  if (text) komfort.text = text;
  if (Object.keys(komfort).length > 0) koerper.komfort = komfort;
  if (Array.isArray(eingabe.sus) && eingabe.sus.some((x) => String(x ?? '').trim() !== '')) {
    const sus = eingabe.sus.map((x) => ganzeZahl(x, 1, 5));
    if (sus.length !== 10 || sus.some((x) => x === null)) return { ok: false, text: 'SUS: alle zehn Aussagen mit 1 bis 5 beantworten.' };
    koerper.sus = sus;
  }
  if (eingabe.tlx && Object.values(eingabe.tlx).some((x) => String(x ?? '').trim() !== '')) {
    const tlx = {};
    for (const [id, roh] of Object.entries(eingabe.tlx)) {
      const z = ganzeZahl(roh, 0, 100);
      if (z === null) return { ok: false, text: 'NASA-TLX: alle sechs Skalen mit 0 bis 100 beantworten.' };
      tlx[id] = z;
    }
    if (Object.keys(tlx).length !== 6) return { ok: false, text: 'NASA-TLX: alle sechs Skalen mit 0 bis 100 beantworten.' };
    koerper.tlx = tlx;
  }
  const notiz = String(eingabe.notiz ?? '').trim();
  if (notiz) koerper.notiz = notiz;
  if (!koerper.komfort && !koerper.sus && !koerper.tlx) return { ok: false, text: 'Bitte mindestens eine Frage beantworten.' };
  return { ok: true, koerper };
}

/** Meldung nach dem Speichern: «Gespeichert: SUS 85.0 · TLX 35.0 · Übelkeit 4 …». */
export const gespeichertText = (rec) => `Gespeichert (${rec?.datei ?? 'Datei'}): ${fragebogenKurz(rec)}.`;

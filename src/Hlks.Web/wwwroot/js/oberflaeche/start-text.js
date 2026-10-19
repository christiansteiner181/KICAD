// Reine Funktionen für den Startbildschirm (S1, entwurf/startbildschirm.md): Texte, Sortierung, Suche und HTML der Kacheln «Zuletzt
// geöffnet», der Liste «Alle KICAD-Projekte», der Familien und des Ordner-Browsers. Kein Browser, keine Fachlogik: gezeigt wird, was
// der Server liefert (/api/zuletzt, /api/projekte/ablage, /api/familien/zuletzt, /api/ordner); gerechnet wird nichts ausser
// Sortierung, Filter und Zeittexten. In Node getestet (start.test.mjs).

import { esc } from './einfuegen-text.js';

export { esc };

// ---------------------------------------------------------------------------------------------- kleine Sinnbilder (Linien in currentColor)

const SYM = {
  pin: '<path d="M10 3h12l-2 9 5 6H7l5-6zM16 18v10"/>',
  ordner: '<path d="M4 9h8l2 3h14v14H4z"/>',
  neu: '<path d="M8 4h11l5 5v19H8z"/><path d="M19 4v5h5M16 15v8M12 19h8"/>',
  beispiel: '<path d="M6 28V8l10-4 10 4v20z"/><path d="M11 12h3M18 12h3M11 17h3M18 17h3M14 28v-6h4v6"/>',
  buch: '<path d="M6 5h15a3 3 0 013 3v20H9a3 3 0 01-3-3z"/><path d="M6 25a3 3 0 013-3h15M11 10h8"/>',
  haus: '<path d="M4 16L16 5l12 11M8 14v13h6v-8h4v8h6V14"/>',
  suche: '<circle cx="13" cy="13" r="7"/><path d="M18 18l8 8"/>',
  neuSuchen: '<path d="M26 12a10 10 0 00-18-3M6 20a10 10 0 0018 3"/><path d="M8 4v6h6M24 28v-6h-6"/>',
  hoch: '<path d="M16 26V8M9 14l7-7 7 7"/>',
  punkte: '<circle cx="8" cy="16" r="2" fill="currentColor"/><circle cx="16" cy="16" r="2" fill="currentColor"/><circle cx="24" cy="16" r="2" fill="currentColor"/>',
  laufwerk: '<rect x="4" y="9" width="24" height="14" rx="2"/><path d="M8 19h6M23 19h1"/>',
  kicad: '<rect x="5" y="5" width="22" height="22" rx="2"/><path d="M12 22V10M12 16l7-6M13 15l7 7"/>',
};

/** Sinnbild als Inline-SVG (Grösse in px). */
export function sym(name, px = 16) {
  return `<svg class="sym" viewBox="0 0 32 32" width="${px}" height="${px}" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${SYM[name] ?? ''}</svg>`;
}

// ---------------------------------------------------------------------------------------------- Zeit und Namen

const zweistellig = (n) => String(n).padStart(2, '0');

/** «Heute 14:32», «Gestern 09:10», sonst «19.10.2026»; ungültig oder leer → «–». */
export function zeitText(iso, jetzt = new Date()) {
  if (!iso) return '–';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '–';
  const tag = (x) => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const diff = Math.round((tag(jetzt) - tag(d)) / 86400000);
  const uhr = `${zweistellig(d.getHours())}:${zweistellig(d.getMinutes())}`;
  if (diff === 0) return `Heute ${uhr}`;
  if (diff === 1) return `Gestern ${uhr}`;
  return `${zweistellig(d.getDate())}.${zweistellig(d.getMonth() + 1)}.${d.getFullYear()}`;
}

/** «vor 3 min», «vor 2 Std.», «vor 4 Tagen» für den Hinweis zur Suche. */
export function vorText(iso, jetzt = new Date()) {
  const d = new Date(iso);
  if (!iso || Number.isNaN(d.getTime())) return '';
  const s = Math.max(0, Math.round((jetzt - d) / 1000));
  if (s < 45) return 'gerade eben';
  if (s < 3600) return `vor ${Math.max(1, Math.round(s / 60))} min`;
  if (s < 86400) return `vor ${Math.round(s / 3600)} Std.`;
  const t = Math.round(s / 86400);
  return `vor ${t} ${t === 1 ? 'Tag' : 'Tagen'}`;
}

/** Anfangsbuchstaben für den Platzhalter ohne Bild: «MFH Musterweg» → «MM», «Bernstrasse» → «BE». */
export function initialen(name) {
  const w = String(name ?? '').replace(/[_\-.,;:()]/g, ' ').split(/\s+/).filter((x) => /[\p{L}\p{N}]/u.test(x));
  if (w.length === 0) return '?';
  const erster = Array.from(w[0].replace(/[^\p{L}\p{N}]/gu, ''));
  if (w.length === 1) return erster.slice(0, 2).join('').toUpperCase() || '?';
  const zweiter = Array.from(w[1].replace(/[^\p{L}\p{N}]/gu, ''));
  return ((erster[0] ?? '') + (zweiter[0] ?? '')).toUpperCase() || '?';
}

/** Farbton 0–359 aus dem Namen: derselbe Name ergibt immer dieselbe Farbe des Platzhalters. */
export function farbton(name) {
  let h = 0;
  for (const c of String(name ?? '')) h = (h * 31 + c.codePointAt(0)) % 360;
  return h;
}

/** Ordnerpfad für Vergleiche: einheitliche Trenner, ohne Schlusstrenner, klein. */
export const normOrdner = (o) => String(o ?? '').trim().replace(/\//g, '\\').replace(/\\+$/, '').toLowerCase();
export const gleicherOrdner = (a, b) => !!a && !!b && normOrdner(a) === normOrdner(b);

/** Teile eines Pfads: «W:\a\b» → ['W:', 'a', 'b']. */
export const pfadTeile = (p) => String(p ?? '').split(/[\\/]/).filter(Boolean);

/** Objektordner aus …\<Objekt>\c_Pläne und Modelle\e_KICAD (drittletzter Teil); sonst der letzte Teil. */
export function objektName(ordner) {
  const t = pfadTeile(ordner);
  if (t.length >= 3 && /^e_/i.test(t[t.length - 1])) return t[t.length - 3];
  return t[t.length - 1] ?? '';
}

/** URL des Vorschaubilds; v (Änderungszeit) verhindert ein altes Bild aus dem Zwischenspeicher des Browsers. */
export const vorschauUrl = (ordner, v) => `/api/projekt/vorschau?ordner=${encodeURIComponent(ordner)}${v ? `&v=${encodeURIComponent(v)}` : ''}`;

// ---------------------------------------------------------------------------------------------- Sortieren und Suchen

/** Angeheftete zuerst, dann nach Zeit (neueste zuerst); stabil. */
export function kachelnSortieren(liste) {
  const zeit = (k) => { const t = Date.parse(k.zeitIso ?? ''); return Number.isFinite(t) ? t : 0; };
  return [...(liste ?? [])]
    .map((k, i) => [k, i])
    .sort((a, b) => (Number(!!b[0].angeheftet) - Number(!!a[0].angeheftet)) || (zeit(b[0]) - zeit(a[0])) || (a[1] - b[1]))
    .map(([k]) => k);
}

/** Alle Wörter der Suche müssen in einem der Felder vorkommen (ohne Gross-/Kleinschreibung). Leere Suche → alles. */
export function filtern(liste, suche, felder) {
  const w = String(suche ?? '').toLowerCase().split(/\s+/).filter(Boolean);
  if (w.length === 0) return liste ?? [];
  return (liste ?? []).filter((x) => {
    const text = felder.map((f) => (typeof f === 'function' ? f(x) : x?.[f])).filter((v) => v !== null && v !== undefined).join(' ').toLowerCase();
    return w.every((t) => text.includes(t));
  });
}

export const FELDER_PROJEKT = ['name', 'objekt', 'ordner', 'nummer'];
export const FELDER_FAMILIE = ['name', 'kategorie', 'hersteller', 'produkt', 'status', 'projekt', 'projektNummer'];

// ---------------------------------------------------------------------------------------------- Kacheln «Zuletzt geöffnet»

/**
 * Eine Kachel. k: Eintrag von /api/zuletzt; i: Index in der angezeigten Liste (data-i); opt.offen: Ablageort des offenen Projekts.
 * Zustände: angeheftet (Marke), offen (Projekt ist gerade geöffnet), fehlt (Ordner nicht erreichbar: vorhanden === false).
 */
export function kachelHtml(k, i, { offen = null, jetzt = new Date() } = {}) {
  const istOffen = gleicherOrdner(k.ordner, offen);
  const fehlt = k.vorhanden === false;
  const klassen = ['sb-kachel', k.angeheftet ? 'angeheftet' : '', istOffen ? 'offen' : '', fehlt ? 'fehlt' : ''].filter(Boolean).join(' ');
  const bild = k.vorschau && !fehlt
    ? `<img src="${esc(vorschauUrl(k.ordner, k.vorschauV))}" alt="" loading="lazy" draggable="false">`
    : `<span class="sb-platzhalter" style="--h:${farbton(k.name)}">${esc(initialen(k.name))}</span>`;
  const marken = (k.angeheftet ? `<span class="sb-pin" title="Angeheftet">${sym('pin', 14)}</span>` : '')
    + (istOffen ? '<span class="sb-marke-offen">Offen</span>' : '')
    + (fehlt ? '<span class="sb-marke-fehlt" title="Der Ordner ist nicht erreichbar oder das Projekt wurde verschoben">nicht erreichbar</span>' : '');
  const objekt = k.objekt || objektName(k.ordner);
  const wann = `${zeitText(k.zeitIso, jetzt)}${k.rechner ? ` · ${k.rechner}` : ''}`;
  return `<article class="${klassen}" role="button" tabindex="0" data-i="${i}" title="${esc(k.ordner)}">
    <div class="sb-bild">${bild}${marken}</div>
    <div class="sb-kachel-text"><b class="sb-name">${esc(k.name)}</b><span class="sb-objekt">${esc(objekt)}</span><span class="sb-zeit">${esc(wann)}</span></div>
    <button type="button" class="sb-menue" data-i="${i}" title="Mehr …" aria-label="Menü">${sym('punkte', 16)}</button></article>`;
}

export function kachelnHtml(liste, opt = {}, leerText = 'Noch keine Projekte geöffnet.') {
  if (!liste || liste.length === 0) return `<p class="sb-leer">${esc(leerText)}</p>`;
  return liste.map((k, i) => kachelHtml(k, i, opt)).join('');
}

/** Menüeinträge einer Kachel (Titel, Aktion als Text, ob wirksam). Die Ausführung liegt im Startbildschirm. */
export function kachelMenue(k, { offen = null } = {}) {
  return [
    { aktion: 'oeffnen', titel: gleicherOrdner(k.ordner, offen) ? 'Zum Projekt' : 'Öffnen' },
    { aktion: 'anheften', titel: k.angeheftet ? 'Lösen' : 'Anheften' },
    { aktion: 'entfernen', titel: 'Aus der Liste entfernen' },
    { trenner: true },
    { aktion: 'explorer', titel: 'Ordner im Explorer zeigen' },
  ];
}

// ---------------------------------------------------------------------------------------------- Liste «Alle KICAD-Projekte auf W:»

/** Hinweiszeile zur Projektsuche: Anzahl, Alter, Dauer; oder die Meldung, wenn die Ablage nicht erreichbar ist. */
export function ablageStatusText(a, jetzt = new Date()) {
  if (!a) return 'Suche läuft …';
  if (!a.erreichbar) return a.hinweis || 'Die Projektablage ist nicht erreichbar.';
  const n = (a.projekte ?? []).length;
  const dauer = Number.isFinite(a.dauerMs) ? `${(a.dauerMs / 1000).toFixed(1).replace('.', ',')} s` : '';
  return `${n} ${n === 1 ? 'Projekt' : 'Projekte'} · gesucht ${vorText(a.gesucht, jetzt)}${dauer ? ` (${dauer})` : ''}${a.hinweis ? ` · ${a.hinweis}` : ''}`;
}

export function ablageTitel(a) {
  const pfad = a?.pfad ? String(a.pfad) : '';
  const laufwerk = /^([A-Za-z]:)/.exec(pfad)?.[1];
  return laufwerk ? `Alle KICAD-Projekte auf ${laufwerk}` : 'Alle KICAD-Projekte';
}

/** Tabelle der Projekte (Name, Objekt, Projektnummer, zuletzt geändert). Der Index i (data-i) bezieht sich auf die übergebene Liste. */
export function ablageHtml(projekte, { offen = null, jetzt = new Date(), leerText = 'Keine Projekte gefunden.' } = {}) {
  if (!projekte || projekte.length === 0) return `<p class="sb-leer">${esc(leerText)}</p>`;
  const zeilen = projekte.map((p, i) => {
    const istOffen = gleicherOrdner(p.ordner, offen);
    return `<tr class="sb-zeile${istOffen ? ' offen' : ''}" tabindex="0" data-i="${i}" title="${esc(p.ordner)}">
      <td class="sb-z-name">${esc(p.name)}${istOffen ? ' <span class="sb-marke-offen">Offen</span>' : ''}</td><td>${esc(p.objekt)}</td><td>${esc(p.nummer || '–')}</td><td class="sb-z-zeit">${esc(zeitText(p.geaendert, jetzt))}</td></tr>`;
  }).join('');
  return `<table class="sb-tab"><thead><tr><th>Name</th><th>Objekt</th><th>Projektnummer</th><th>Zuletzt geändert</th></tr></thead><tbody>${zeilen}</tbody></table>`;
}

// ---------------------------------------------------------------------------------------------- Familien

export const STATUS_TEXT = { entwurf: 'Entwurf', freigegeben: 'Freigegeben' };
export const statusText = (s) => STATUS_TEXT[s] ?? (s ? String(s) : 'Entwurf');

/** Zeilen «zuletzt bearbeitete Familien». Der Index i (data-i) bezieht sich auf die übergebene Liste. */
export function familienHtml(familien, { jetzt = new Date(), leerText = 'Keine Familien in der Bibliothek.' } = {}) {
  if (!familien || familien.length === 0) return `<p class="sb-leer">${esc(leerText)}</p>`;
  return familien.map((f, i) => {
    const zeile2 = [f.kategorie, f.hersteller].filter(Boolean).join(' · ');
    const projekt = f.projekt ? `${f.projekt}${f.projektNummer ? ` (${f.projektNummer})` : ''}` : 'Projekt unbekannt';
    return `<div class="sb-familie" role="button" tabindex="0" data-i="${i}" title="${esc(f.projektOrdner ?? '')}">
      <div class="sb-f-kopf"><b class="sb-name">${esc(f.name)}</b><span class="sb-f-status st-${esc(f.status ?? 'entwurf')}">${esc(statusText(f.status))}</span><span class="sb-f-version">v${esc(f.version ?? '?')}</span></div>
      ${zeile2 ? `<div class="sb-f-zeile">${esc(zeile2)}</div>` : ''}
      <div class="sb-f-zeile leise">${esc(projekt)} · ${esc(zeitText(f.zeitIso, jetzt))}</div></div>`;
  }).join('');
}

/** Tabelle der ganzen Bibliothek (nur lesend), wenn kein Projekt offen ist. */
export function bibliothekTabelleHtml(familien, jetzt = new Date()) {
  if (!familien || familien.length === 0) return '<p class="leise">Keine Familien in der Bibliothek.</p>';
  const z = familien.map((f) => `<tr><td>${esc(f.name)}</td><td>${esc(f.kategorie ?? '')}</td><td>${esc(f.hersteller ?? '')}</td><td>${esc(statusText(f.status))}</td><td>v${esc(f.version ?? '?')}</td><td>${esc(f.projekt ?? '')}</td><td>${esc(zeitText(f.zeitIso, jetzt))}</td></tr>`).join('');
  return `<table class="tab"><thead><tr><th>Name</th><th>Kategorie</th><th>Hersteller</th><th>Status</th><th>Version</th><th>Projekt</th><th>Zuletzt</th></tr></thead><tbody>${z}</tbody></table>`;
}

// ---------------------------------------------------------------------------------------------- Ordner-Browser

/** Schnellzugriff links: Laufwerke, Projektablage, zuletzt benutzte Ordner → [{ gruppe, titel, pfad, symbol }]. */
export function schnellzugriff(laufwerke, ablage, zuletzt) {
  const l = [];
  if (ablage) l.push({ gruppe: 'Projektablage', titel: pfadTeile(ablage).slice(-1)[0] ?? ablage, pfad: ablage, symbol: 'kicad' });
  for (const d of laufwerke ?? []) l.push({ gruppe: 'Laufwerke', titel: `${d.name}${d.bezeichnung ? ` ${d.bezeichnung}` : ''}${d.art === 'Netzlaufwerk' ? ' (Netz)' : ''}`.trim(), pfad: d.pfad, symbol: 'laufwerk' });
  // Ein Projektordner (…\<Objekt>\c_Pläne und Modelle\e_KICAD) heisst nach seinem Objekt, sonst gelten die letzten zwei Teile des Pfads
  for (const z of zuletzt ?? []) {
    const t = pfadTeile(z);
    l.push({ gruppe: 'Zuletzt benutzt', titel: t.length >= 3 && /^e_/i.test(t[t.length - 1]) ? t[t.length - 3] : (t.slice(-2).join('\\') || z), pfad: z, symbol: 'ordner' });
  }
  return l;
}

/** Merkt einen Ordner an der Spitze der Liste «zuletzt benutzt» (ohne Doppelte, höchstens max). */
export function ordnerMerken(liste, pfad, max = 8) {
  if (!pfad) return liste ?? [];
  return [pfad, ...(liste ?? []).filter((x) => !gleicherOrdner(x, pfad))].slice(0, max);
}

export function schnellzugriffHtml(eintraege, aktuell = '') {
  let h = '';
  let gruppe = null;
  for (const e of eintraege) {
    if (e.gruppe !== gruppe) { gruppe = e.gruppe; h += `<div class="ob-gruppe">${esc(gruppe)}</div>`; }
    h += `<button type="button" class="ob-ziel${gleicherOrdner(e.pfad, aktuell) ? ' aktiv' : ''}" data-pfad="${esc(e.pfad)}" title="${esc(e.pfad)}">${sym(e.symbol, 14)}<span>${esc(e.titel)}</span></button>`;
  }
  return h || '<p class="leise">Keine Orte.</p>';
}

/**
 * Inhalt des Ordner-Browsers. d: Antwort von /api/ordner (mit ordner[] oder laufwerke[]); gewaehlt: Pfad des markierten Unterordners.
 * Hervorgehoben: KICAD-Projekte (projekt) und Ordner, die ein Projekt enthalten (enthaeltProjekt).
 */
export function ordnerListeHtml(d, gewaehlt = '') {
  if (!d) return '<p class="leise">Wird geladen …</p>';
  if (Array.isArray(d.laufwerke) && !d.pfad) {
    return d.laufwerke.map((l) => `<button type="button" class="ob-ordner" data-pfad="${esc(l.pfad)}" tabindex="-1">${sym('laufwerk', 16)}<span class="ob-name">${esc(l.name)}${l.bezeichnung ? ` ${esc(l.bezeichnung)}` : ''}</span><span class="ob-art">${esc(l.art)}</span></button>`).join('')
      || '<p class="leise">Keine Laufwerke gefunden.</p>';
  }
  const kopf = d.istProjekt
    ? `<div class="ob-hinweis projekt">${sym('kicad', 16)} Dieser Ordner ist ein KICAD-Projekt.</div>`
    : d.projektUnter ? `<div class="ob-hinweis projekt">${sym('kicad', 16)} Dieser Ordner enthält ein KICAD-Projekt: <span class="mono">${esc(d.projektUnter)}</span></div>` : '';
  const zeilen = (d.ordner ?? []).map((o) => {
    const art = o.projekt ? '<span class="ob-art projekt">KICAD-Projekt</span>' : o.enthaeltProjekt ? '<span class="ob-art enthaelt">enthält Projekt</span>' : '';
    return `<button type="button" class="ob-ordner${o.projekt ? ' projekt' : o.enthaeltProjekt ? ' enthaelt' : ''}${gleicherOrdner(o.pfad, gewaehlt) ? ' gewaehlt' : ''}" data-pfad="${esc(o.pfad)}" data-projekt="${o.projekt ? 1 : 0}" data-enthaelt="${o.enthaeltProjekt ? 1 : 0}" tabindex="-1">${sym(o.projekt || o.enthaeltProjekt ? 'kicad' : 'ordner', 16)}<span class="ob-name">${esc(o.name)}</span>${art}</button>`;
  }).join('');
  const leer = zeilen ? '' : '<p class="leise">Keine Unterordner.</p>';
  const mehr = d.gekuerzt ? '<p class="leise">Die Liste ist gekürzt (sehr viele Ordner).</p>' : '';
  const fehler = d.hinweis ? `<p class="fehlertext">${esc(d.hinweis)}</p>` : '';
  return kopf + zeilen + leer + mehr + fehler;
}

/**
 * Welchen Ordner liefert «Öffnen»? Ein Projektordner selbst; ein Objektordner, der ein Projekt enthält, liefert dessen Ordner e_KICAD;
 * ein markierter Unterordner gilt vor dem aktuellen. Sonst der eingetippte Pfad (der Server prüft ihn).
 */
export function oeffnenZiel({ eingabe, daten, gewaehlt }) {
  const text = String(eingabe ?? '').trim();
  const k = (daten?.ordner ?? []).find((o) => gleicherOrdner(o.pfad, gewaehlt) && gleicherOrdner(text, o.pfad));
  if (k?.projekt) return k.pfad;
  if (k?.enthaeltProjekt && daten?.kicadOrdner) return `${k.pfad.replace(/[\\/]+$/, '')}\\${daten.kicadOrdner}`;
  if (daten && gleicherOrdner(text, daten.pfad)) {
    if (daten.istProjekt) return daten.pfad;
    if (daten.projektUnter) return daten.projektUnter;
  }
  return text;
}

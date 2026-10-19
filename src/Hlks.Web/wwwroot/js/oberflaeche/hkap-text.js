// Reine Funktionen für den Austausch mit HKAP (Modul 1d, Auftrag C): Texte und HTML der Dialoge «Mit HKAP verknüpfen», «Abgleich mit HKAP»
// und «Prüfbefunde senden», Parameter der Befehle hkap_verknuepfen, hkap_uebernehmen, hkap_senden, hkap_befunde_senden, hkap_raum_zuordnen
// und die Hilfen für die «nicht platzierten Räume» im Projektbrowser. Kein Browser, keine Fachlogik: gezeigt wird, was der Kern liefert
// (Abfragen hkap_verbindung und hkap_abgleich, BefehlsErgebnis); gerechnet wird nichts. In Node getestet (hkap.test.mjs).

import { esc } from './einfuegen-text.js';

export { esc };

export const STANDARD_URL = 'http://localhost:5013';

/** Datenarten des Austauschs in fester Reihenfolge (wie im Kern). */
export const DATENARTEN = [['projekt', 'Projekt'], ['geschosse', 'Geschosse'], ['raeume', 'Räume'], ['straenge', 'Stränge'], ['pendenzen', 'Pendenzen']];
export const DATENART_NAMEN = Object.fromEntries(DATENARTEN);
/** Datenarten, die «Aus HKAP übernehmen» kennt (Pendenzen übernimmt KICAD nicht), und die, die «Nach HKAP senden» kennt (Projektdaten führt HKAP). */
export const UEBERNEHMBAR = ['projekt', 'geschosse', 'raeume', 'straenge'];
export const SENDBAR = ['geschosse', 'raeume', 'straenge'];
export const SCHWEREN = [['fehler', 'Fehler'], ['warnung', 'Warnung'], ['hinweis', 'Hinweis']];

const istObjekt = (x) => x !== null && typeof x === 'object' && !Array.isArray(x);
const leer = (x) => x === null || x === undefined || (typeof x === 'string' && x.trim() === '');
const zeichenKurz = (t, max) => (t.length > max ? t.slice(0, max - 1) + '…' : t);

// ---------------------------------------------------------------------------------------------- Werte und Adresse

/** Liste der KICAD-Stränge im Block «kicad.straenge» (Modul 3): Einträge mit kennzeichen und systemtyp. */
const istStrangListe = (v) => Array.isArray(v) && v.length > 0 && v.every((x) => istObjekt(x) && 'kennzeichen' in x && 'systemtyp' in x);

/** «HZ-VL-1 (Heizung Vorlauf, 2 Endgeräte, 5.45 m) · HZ-RL-1 (Heizung Rücklauf, 2 m)»: ein KICAD-Strang je Eintrag, Länge = Summe aller Serien und Grössen. */
export function strangListeText(liste) {
  return liste.map((s) => {
    const m = (s.laengen ?? []).reduce((a, l) => a + (Number(l.laengeM) || 0), 0);
    const teile = [s.systemtyp, s.endgeraete ? `${s.endgeraete} ${s.endgeraete === 1 ? 'Endgerät' : 'Endgeräte'}` : '', m > 0 ? `${Math.round(m * 100) / 100} m` : ''].filter(Boolean);
    return `${s.kennzeichen}${teile.length > 0 ? ` (${teile.join(', ')})` : ''}`;
  }).join(' · ');
}

/** Ein Wert für die Anzeige: leer als «–», Zahlen auf höchstens 3 Nachkommastellen, Objekte als «feld: wert · feld: wert». */
export function wertText(v, max = 160) {
  if (leer(v)) return '–';
  if (typeof v === 'boolean') return v ? 'ja' : 'nein';
  if (typeof v === 'number') return Number.isFinite(v) ? String(Math.round(v * 1000) / 1000) : '–';
  if (istStrangListe(v)) return zeichenKurz(strangListeText(v), max);
  if (Array.isArray(v)) return zeichenKurz(v.map((x) => wertText(x, max)).join(', '), max);
  if (istObjekt(v)) return zeichenKurz(Object.entries(v).filter(([, x]) => !leer(x)).map(([k, x]) => `${k}: ${wertText(x, max)}`).join(' · ') || '–', max);
  return zeichenKurz(String(v), max);
}

/** Adresse ohne Leerraum und ohne Schrägstrich am Ende. */
export const urlNormal = (u) => String(u ?? '').trim().replace(/\/+$/, '');

export const urlGueltig = (u) => /^https?:\/\/[^\s/]+(:\d+)?(\/\S*)?$/i.test(urlNormal(u));

/** Die Projektinformation (die mit dem kleinsten Kennzeichen, wie im Kern) aus einer Liste von Darstellungen. */
export function projektinfo(elemente) {
  return [...(elemente ?? [])].filter((e) => e?.klasse === 'Projektinformation').sort((a, b) => (a.kennzeichen < b.kennzeichen ? -1 : a.kennzeichen > b.kennzeichen ? 1 : 0))[0] ?? null;
}

/** Die Verknüpfung aus Projektinformation.hkap, sonst null. */
export function verknuepfung(info) {
  const h = info?.werte?.hkap;
  if (!istObjekt(h) || leer(h.projekt)) return null;
  const t = (x) => (leer(x) ? null : String(x));
  return { projekt: String(h.projekt), projektierung: t(h.projektierung), kuerzel: t(h.kuerzel), name: t(h.name), url: t(h.url) };
}

/** Vorgabe der Adresse im Dialog: die der Verknüpfung, sonst http://localhost:5013. */
export const urlVorgabe = (link) => link?.url ?? STANDARD_URL;

/** «P-005 «Testprojekt» (TP) · Projektierung PJ-2026-001». */
export function verknuepfungText(link) {
  if (!link) return 'nicht verknüpft';
  return [link.projekt + (link.name ? ` «${link.name}»` : '') + (link.kuerzel ? ` (${link.kuerzel})` : ''),
    link.projektierung ? `Projektierung ${link.projektierung}` : 'ohne Projektierung'].join(' · ');
}

/** Zeilen der bestehenden Verknüpfung für eine Tabelle. */
export function verknuepfungZeilen(link) {
  if (!link) return [];
  return [['Projekt', link.projekt], ['Bezeichnung', link.name ?? '–'], ['Kürzel', link.kuerzel ?? '–'],
    ['Projektierung', link.projektierung ?? '– (nur Projektdaten und Pendenzen)'], ['Adresse', link.url ?? `${STANDARD_URL} (Vorgabe)`]];
}

// ---------------------------------------------------------------------------------------------- Verbinden (hkap_verbindung, hkap_verknuepfen)

/** Parameter der Abfrage hkap_verbindung: Adresse nur, wenn eine da ist; projekt listet zusätzlich dessen Projektierungen. */
export function verbindungParameter(url, projekt = null) {
  const p = {};
  if (!leer(url)) p.url = urlNormal(url);
  if (!leer(projekt)) p.projekt = String(projekt).trim();
  return p;
}

/**
 * Zustand der Verbindung aus der Antwort von hkap_verbindung:
 * { ok, titel, meldung, vorschlag, fassung } – ok = erreichbar und ohne Meldung des Kerns. Nicht erreichbar ist keine Störung der Oberfläche:
 * der Kern meldet es mit Vorschlag, die Oberfläche zeigt beides deutlich, aber ohne Fehlerton.
 */
export function verbindungStatus(v) {
  if (!v) return { ok: false, titel: 'Noch nicht geprüft', meldung: '', vorschlag: '', fassung: null };
  if (v.erreichbar === false)
    return { ok: false, titel: 'HKAP ist nicht erreichbar', meldung: v.meldung ?? '', vorschlag: v.vorschlag ?? '', fassung: null };
  if (v.erreichbar === true && !leer(v.meldung))
    return { ok: false, titel: 'HKAP antwortet, aber nicht wie erwartet', meldung: v.meldung, vorschlag: v.vorschlag ?? '', fassung: v.fassung ?? null };
  return { ok: v.erreichbar === true, titel: v.erreichbar === true ? 'HKAP ist erreichbar' : 'Noch nicht geprüft', meldung: '', vorschlag: '', fassung: v.fassung ?? null };
}

/** Kasten zur Verbindung: grün-leise bei Erfolg, sonst ein deutlicher Hinweiskasten (Kern-Meldung und Vorschlag). */
export function verbindungHtml(v) {
  const s = verbindungStatus(v);
  if (s.ok) {
    const n = Array.isArray(v?.datenarten) ? v.datenarten.length : null;
    return `<div class="hkap-ok"><b>✓ ${esc(s.titel)}</b> · ${esc(v.url ?? '')}${s.fassung ? ` · Austauschfassung ${esc(s.fassung)}` : ''}${n ? ` · ${n} Datenarten` : ''}</div>`;
  }
  if (!v) return '';
  return `<div class="hkap-kasten" role="status"><b>${esc(s.titel)}</b>${s.meldung ? `<div>${esc(s.meldung)}</div>` : ''}${s.vorschlag ? `<div class="leise">Vorschlag: ${esc(s.vorschlag)}</div>` : ''}</div>`;
}

/** Tabelle der HKAP-Projekte (Id, Bezeichnung, Phase); wahl = gewählte Id, link = bestehende Verknüpfung. */
export function projekteHtml(projekte, wahl = null, link = null, suche = '') {
  const alle = Array.isArray(projekte) ? projekte : [];
  const s = String(suche ?? '').trim().toLowerCase();
  const l = s === '' ? alle : alle.filter((p) => `${p.id} ${p.name ?? ''} ${p.phase ?? ''}`.toLowerCase().includes(s));
  if (alle.length === 0) return '<p class="leise">HKAP meldet keine Projekte.</p>';
  if (l.length === 0) return `<p class="leise">Kein Projekt passt zu «${esc(suche)}» (${alle.length} in HKAP).</p>`;
  return `<table class="tab klickbar hkap-liste"><thead><tr><th>Projekt</th><th>Projektname</th><th>Phase</th></tr></thead><tbody>${
    l.map((p) => `<tr data-projekt="${esc(p.id)}" class="${p.id === wahl ? 'gewaehlt' : ''}" tabindex="0"><td>${esc(p.id)}${link?.projekt === p.id ? ' <span class="empfohlen">verknüpft</span>' : ''}</td><td>${esc(p.name ?? '')}</td><td class="leise">${esc(p.phase ?? '')}</td></tr>`).join('')}</tbody></table>`
    + (l.length < alle.length ? `<p class="leise">${l.length} von ${alle.length} Projekten.</p>` : '');
}

/** Tabelle der Projektierungen des gewählten Projekts. */
export function projektierungenHtml(liste, wahl = null, link = null, projekt = null) {
  if (liste === null || liste === undefined) return '<p class="leise">Zuerst ein Projekt wählen.</p>';
  if (liste.length === 0) return '<p class="leise">Dieses Projekt hat keine Projektierung. Verknüpfen geht dann nur für Projektdaten und Pendenzen.</p>';
  return `<table class="tab klickbar hkap-liste"><thead><tr><th>Projektierung</th><th>Bezeichnung</th></tr></thead><tbody>${
    liste.map((p) => `<tr data-projektierung="${esc(p.id)}" class="${p.id === wahl ? 'gewaehlt' : ''}" tabindex="0"><td>${esc(p.id)}${link?.projekt === projekt && link?.projektierung === p.id ? ' <span class="empfohlen">verknüpft</span>' : ''}</td><td>${esc(p.name ?? '')}</td></tr>`).join('')}</tbody></table>`;
}

/**
 * Warnungen vor dem Verknüpfen (nur Gegenüberstellung der Angaben, der Kern prüft und sagt im Trockenlauf dasselbe):
 * andere Verknüpfung als bisher (der Basisstand des alten Abgleichs gilt dann nicht mehr), Name in HKAP weicht von der Verknüpfung ab.
 */
export function wechselWarnungen(link, projekt, projektierung = null) {
  const w = [];
  if (!link || leer(projekt)) return w;
  const pj = (x) => x ?? 'ohne Projektierung';
  if (link.projekt !== projekt)
    w.push(`Das Projekt ist mit ${link.projekt}${link.name ? ` «${link.name}»` : ''} verknüpft; es wird auf ${projekt} umgestellt. Der Basisstand des alten Abgleichs gilt dann nicht mehr, die Kennungen der alten Projektierung werden nicht mehr zugeordnet.`);
  else if ((link.projektierung ?? null) !== (projektierung ?? null))
    w.push(`Die Projektierung wechselt von ${pj(link.projektierung)} auf ${pj(projektierung)}. Der Basisstand des alten Abgleichs gilt dann nicht mehr.`);
  return w;
}

/**
 * Name und Kürzel, die HKAP laut Kern für das Projekt nennt: aus dem Trockenlauf von hkap_verknuepfen («Verknüpft mit HKAP P-005 «Name» (Kürzel), …»).
 * Die Projektliste von HKAP trägt dafür den falschen Namen (Projektname, nicht Bezeichnung der Projektdaten), darum die Angabe des Kerns.
 */
export function angabenAusHinweis(text) {
  const m = /Verknüpft mit HKAP\s+(\S+)\s+«([^»]*)»\s+\(([^)]*)\)/.exec(String(text ?? ''));
  return m ? { projekt: m[1], name: m[2], kuerzel: m[3] } : null;
}

/** Warnung, wenn für dasselbe Projekt Name oder Kürzel in HKAP von der bestehenden Verknüpfung abweichen (Gegenprüfung), sonst null. */
export function namensAbweichung(link, angaben) {
  if (!link || !angaben || link.projekt !== angaben.projekt) return null;
  const gleich = (a, b) => leer(a) || leer(b) || String(a).trim().toLowerCase() === String(b).trim().toLowerCase();
  if (gleich(link.name, angaben.name) && gleich(link.kuerzel, angaben.kuerzel)) return null;
  const alt = `«${link.name ?? '?'}» (${link.kuerzel ?? '?'})`;
  return `HKAP-Projekt ${link.projekt} heisst jetzt «${angaben.name}» (${angaben.kuerzel}); verknüpft war ${alt}. Beim Verknüpfen werden Name und Kürzel nachgeführt – bitte prüfen, ob es dasselbe Projekt ist (HKAP vergibt P-NNN als Maximum + 1, die Id kann wiederkehren).`;
}

/** Parameter von hkap_verknuepfen. Die Adresse geht nur mit, wenn sie von der bisher wirksamen abweicht (bisherige Verknüpfung, sonst Vorgabe). */
export function verknuepfenParameter({ projekt, projektierung = null, url = '', link = null }) {
  const p = { projekt: String(projekt ?? '').trim() };
  if (!leer(projektierung)) p.projektierung = String(projektierung).trim();
  const wirksam = urlNormal(link?.url ?? STANDARD_URL);
  if (!leer(url) && urlNormal(url) !== wirksam) p.url = urlNormal(url);
  return p;
}

/** Prüfung vor dem Verknüpfen: true oder Fehlertext. */
export function verknuepfenPruefen({ projekt, url = '' }) {
  if (leer(projekt)) return 'Bitte ein HKAP-Projekt aus der Liste wählen.';
  if (!urlGueltig(url || STANDARD_URL)) return 'Die HKAP-Adresse ist ungültig. Beispiel: ' + STANDARD_URL;
  return true;
}

// ---------------------------------------------------------------------------------------------- Abgleich (hkap_abgleich)

/** Zustand der Antwort von hkap_abgleich: ok | nichtVerknuepft | nichtErreichbar. */
export function abgleichStatus(a) {
  if (!a) return { art: 'leer', meldung: '', vorschlag: '' };
  if (a.verknuepft === false) return { art: 'nichtVerknuepft', meldung: a.meldung ?? 'Das Projekt ist nicht mit HKAP verknüpft.', vorschlag: a.vorschlag ?? '' };
  if (a.erreichbar === false) return { art: 'nichtErreichbar', meldung: a.meldung ?? 'HKAP ist nicht erreichbar.', vorschlag: a.vorschlag ?? '' };
  return { art: 'ok', meldung: '', vorschlag: '' };
}

/** Kasten für «nicht verknüpft» und «nicht erreichbar»: deutlich, mit Vorschlag des Kerns, ohne Fehlerrot. */
export function abgleichKastenHtml(a) {
  const s = abgleichStatus(a);
  if (s.art === 'ok' || s.art === 'leer') return '';
  const titel = s.art === 'nichtVerknuepft' ? 'Das Projekt ist noch nicht mit HKAP verknüpft' : 'HKAP ist nicht erreichbar';
  const vorschlag = s.art === 'nichtVerknuepft' ? 'Im Register Verwalten, Gruppe HKAP, «Verbinden» wählen und Projekt und Projektierung angeben.' : hinweisFuerMenschen(s.vorschlag);
  return `<div class="hkap-kasten" role="status"><b>${esc(titel)}</b><div>${esc(hinweisFuerMenschen(s.meldung))}</div>${vorschlag ? `<div class="leise">Vorschlag: ${esc(vorschlag)}</div>` : ''}</div>`;
}

export const datenartVon = (a, name) => (a?.datenarten ?? []).find((d) => d.name === name) ?? null;

/** Zählung einer Datenart: Datensätze nach KICAD / nach HKAP, Konflikte, Vorschläge (Löschungen und Zuordnungen), unveränderte. */
export function zaehlen(d) {
  const n = (x) => (Array.isArray(x) ? x.length : 0);
  if (!d) return { nachKicad: 0, nachHkap: 0, konflikte: 0, vorschlaege: 0, unveraendert: 0 };
  if (d.name === 'pendenzen')
    return { nachKicad: 0, nachHkap: n(d.neu) + n(d.geaendert), konflikte: 0, vorschlaege: n(d.erledigtVorschlag) + n(d.erledigtInHkap), unveraendert: d.unveraendert ?? 0 };
  return {
    nachKicad: n(d.nachKicad?.neu) + n(d.nachKicad?.geaendert),
    nachHkap: n(d.nachHkap?.neu) + n(d.nachHkap?.geaendert),
    konflikte: n(d.konflikte),
    vorschlaege: n(d.nachKicad?.geloescht) + n(d.nachHkap?.geloescht) + n(d.zuordnungsvorschlaege),
    unveraendert: d.unveraendert ?? 0,
  };
}

/** Eine Zeile Zusammenfassung über alle Datenarten: «← 3 aus HKAP · → 1 nach HKAP · 2 Konflikte · 1 Vorschlag». */
export function zusammenfassungText(a) {
  const z = a?.zusammenfassung;
  if (!z) return '';
  const teile = [`← ${z.nachKicad ?? 0} aus HKAP`, `→ ${z.nachHkap ?? 0} nach HKAP`, `${z.konflikte ?? 0} ${z.konflikte === 1 ? 'Konflikt' : 'Konflikte'}`];
  if (z.vorschlaege) teile.push(`${z.vorschlaege} ${z.vorschlaege === 1 ? 'Vorschlag' : 'Vorschläge'}`);
  return teile.join(' · ');
}

const hinweisFeld = (f) => [f.ueberschreibt ? 'überschreibt den Wert der anderen Seite' : '', f.grund ?? ''].filter(Boolean).join(' · ');

/**
 * Zeilen der Änderungstabelle einer Datenart (Projekt, Geschosse, Räume): je Richtung neu, geändert (je Feld), gelöscht (nur Vorschlag).
 * alt = Wert, der überschrieben wird (am Ziel), neu = Wert, der fliesst. richtung: nachKicad (← aus HKAP) | nachHkap (→ nach HKAP).
 */
export function zeilenVon(d) {
  const z = [];
  for (const [richtung, seite] of [['nachKicad', d?.nachKicad], ['nachHkap', d?.nachHkap]]) {
    if (!seite) continue;
    for (const n of seite.neu ?? [])
      z.push({ richtung, art: 'neu', datensatz: n.element ?? n.hkapId ?? '', hkapId: n.hkapId ?? null, feld: '', alt: null, neu: n.werte ?? null, hinweis: hinweisFuerMenschen(n.hinweis ?? '') });
    for (const g of seite.geaendert ?? [])
      for (const f of g.felder ?? []) {
        const ausHkap = richtung === 'nachKicad';
        z.push({ richtung, art: 'geaendert', datensatz: g.element ?? g.hkapId ?? '', hkapId: g.hkapId ?? null, feld: f.feld, alt: ausHkap ? f.kicad : f.hkap, neu: ausHkap ? f.hkap : f.kicad, hinweis: hinweisFeld(f) });
      }
    for (const l of seite.geloescht ?? [])
      z.push({ richtung, art: 'geloescht', datensatz: l.element ?? l.hkapId ?? '', hkapId: l.hkapId ?? null, feld: '', alt: null, neu: null, hinweis: hinweisFuerMenschen(l.hinweis ?? '') });
  }
  return z;
}

const RICHTUNG_TEXT = { nachKicad: '← aus HKAP', nachHkap: '→ nach HKAP' };
const ART_TEXT = { neu: 'neu', geaendert: 'geändert', geloescht: 'gelöscht (Vorschlag)' };

/** Datensatz mit Element und HKAP-Id: «1.02» + leise «E1-S3». */
const datensatzHtml = (z) => `${esc(z.datensatz)}${z.hkapId && z.hkapId !== z.datensatz ? ` <span class="leise">${esc(z.hkapId)}</span>` : ''}`;

export function aenderungenZeilenHtml(zeilen) {
  if (zeilen.length === 0) return '<p class="leise">Keine Änderungen in dieser Datenart.</p>';
  return `<table class="tab kompakt vorschau-tab hkap-aenderungen"><thead><tr><th>Richtung</th><th>Art</th><th>Datensatz</th><th>Feld</th><th>Bisher (am Ziel)</th><th>Neu</th><th>Bemerkung</th></tr></thead><tbody>${
    zeilen.map((z) => `<tr class="r-${z.richtung}"><td class="hkap-richtung">${RICHTUNG_TEXT[z.richtung]}</td><td class="art-${z.art}">${ART_TEXT[z.art]}</td><td>${datensatzHtml(z)}</td><td class="hkap-feld">${esc(z.feld)}</td>`
      + `<td class="hkap-wert">${z.art === 'geaendert' ? esc(wertText(z.alt)) : ''}</td><td class="hkap-wert">${z.art === 'geloescht' ? '' : esc(wertText(z.neu))}</td><td class="leise">${esc(z.hinweis)}</td></tr>`).join('')}</tbody></table>`;
}

// ---- Konflikte

export const WAHL_TEXT = { kicad: 'KICAD', hkap: 'HKAP' };

/** Tabelle der Konflikte: beide Fassungen, Basisstand, Wahl je Zeile (offen lassen | KICAD | HKAP). wahl = { schluessel: 'kicad'|'hkap' }. */
export function konflikteHtml(konflikte, wahl = {}, uebrige = null) {
  const l = Array.isArray(konflikte) ? konflikte : [];
  if (l.length === 0) return '';
  const sel = (k) => {
    const w = wahl[k.schluessel] ?? '';
    const o = (wert, text) => `<option value="${wert}"${w === wert ? ' selected' : ''}>${text}</option>`;
    return `<select class="k-wahl" data-schluessel="${esc(k.schluessel)}" aria-label="Wahl für ${esc(k.schluessel)}">${o('', uebrige ? `übrige: ${WAHL_TEXT[uebrige]}` : 'offen lassen')}${o('kicad', 'KICAD')}${o('hkap', 'HKAP')}</select>`;
  };
  return `<table class="tab kompakt hkap-konflikte"><thead><tr><th>Datensatz</th><th>Feld</th><th>KICAD</th><th>HKAP</th><th>Stand beim letzten Abgleich</th><th>Wahl</th></tr></thead><tbody>${
    l.map((k) => `<tr class="hkap-konflikt${wahl[k.schluessel] ? ' entschieden' : ''}" data-schluessel="${esc(k.schluessel)}"><td>${esc(k.element ?? k.hkapId ?? '')}${k.hkapId && k.hkapId !== k.element ? ` <span class="leise">${esc(k.hkapId)}</span>` : ''}</td><td>${esc(k.feld)}</td>`
      + `<td class="hkap-wert">${esc(wertText(k.kicad))}</td><td class="hkap-wert">${esc(wertText(k.hkap))}</td><td class="hkap-wert leise">${esc(wertText(k.basis))}</td><td>${sel(k)}</td></tr>`).join('')}</tbody></table>`;
}

/** Wahl der Konflikte für hkap_uebernehmen: { "raeume/E1-S3/bezeichnung": "kicad", "*": "hkap" } oder null, wenn nichts gewählt ist. */
export function konfliktParameter(wahl = {}, uebrige = null) {
  const o = {};
  for (const [k, w] of Object.entries(wahl)) if (w === 'kicad' || w === 'hkap') o[k] = w;
  if (uebrige === 'kicad' || uebrige === 'hkap') o['*'] = uebrige;
  return Object.keys(o).length > 0 ? o : null;
}

/** Schlüssel aller Konflikte der Antwort (über alle Datenarten). */
export function konfliktSchluessel(a) {
  const aus = (a?.konflikte ?? []).map((k) => k.schluessel);
  return aus.length > 0 ? aus : (a?.datenarten ?? []).flatMap((d) => (d.konflikte ?? []).map((k) => k.schluessel));
}

/** Wahl nach einem Neuladen bereinigen: nur Schlüssel, die es noch gibt. */
export function wahlBereinigen(wahl, a) {
  const da = new Set(konfliktSchluessel(a));
  return Object.fromEntries(Object.entries(wahl ?? {}).filter(([k]) => da.has(k)));
}

/** Wie viele Konflikte bleiben ohne Wahl offen (mit «alle übrigen» keiner)? */
export function offeneKonflikte(a, wahl = {}, uebrige = null) {
  if (uebrige === 'kicad' || uebrige === 'hkap') return 0;
  return konfliktSchluessel(a).filter((k) => wahl[k] !== 'kicad' && wahl[k] !== 'hkap').length;
}

// ---- Pendenzen

/** Tabelle der Pendenzen-Vorschau (hkap_abgleich, Vorschau von hkap_befunde_senden mit Schwere fehler). */
export function pendenzenHtml(d) {
  const z = [];
  for (const n of d?.neu ?? []) z.push(['neu', n.quelleId ?? '', n.aufgabe, n.raum, n.prioritaet ? `Priorität ${n.prioritaet}` : '']);
  for (const g of d?.geaendert ?? []) z.push(['geaendert', g.id ?? '', g.felder?.aufgabe ?? wertText(g.felder), g.felder?.raum ?? '', '']);
  for (const e of d?.erledigtVorschlag ?? []) z.push(['erledigt', e.id ?? '', e.aufgabe, '', `Befund behoben – Vorschlag: in HKAP auf «erledigt» setzen (jetzt ${wertText(e.status)})`]);
  for (const e of d?.erledigtInHkap ?? []) z.push(['wieder', e.id ?? '', e.aufgabe, '', e.hinweis ?? '']);
  const art = { neu: ['art-neu', 'neu'], geaendert: ['art-geaendert', 'geändert'], erledigt: ['art-geloescht', 'erledigt (Vorschlag)'], wieder: ['art-geaendert', 'in HKAP erledigt'] };
  const tab = z.length === 0 ? '<p class="leise">Keine neuen oder geänderten Pendenzen aus Prüfbefunden (Schwere «fehler»).</p>'
    : `<table class="tab kompakt vorschau-tab hkap-aenderungen"><thead><tr><th>Richtung</th><th>Art</th><th>Pendenz / Befund</th><th>Aufgabe</th><th>Raum</th><th>Bemerkung</th></tr></thead><tbody>${
      z.map(([a, id, aufgabe, raum, bem]) => `<tr class="r-nachHkap"><td class="hkap-richtung">→ nach HKAP</td><td class="${art[a][0]}">${art[a][1]}</td><td>${esc(id)}</td><td>${esc(wertText(aufgabe))}</td><td>${esc(wertText(raum))}</td><td class="leise">${esc(bem)}</td></tr>`).join('')}</tbody></table>`;
  const fuss = [d?.unveraendert ? `${d.unveraendert} schon aktuell` : '', d?.fremd ? `${d.fremd} KICAD-Pendenz(en) aus anderen Projekten (unberührt)` : ''].filter(Boolean).join(' · ');
  return `${tab}${fuss ? `<p class="leise">${esc(fuss)}</p>` : ''}`;
}

// ---- eine Datenart als Abschnitt

/** Wie viele nicht platzierte Räume der Abschnitt beim Namen nennt (alle stehen im Projektbrowser). */
const MAX_PLATZ = 12;

/** Kurzzeile hinter der Überschrift: «← 2 · → 1 · 1 Konflikt · 5 unverändert». */
export function abschnittKopf(d) {
  const z = zaehlen(d);
  const t = [];
  if (z.nachKicad) t.push(`← ${z.nachKicad}`);
  if (z.nachHkap) t.push(`→ ${z.nachHkap}`);
  if (z.konflikte) t.push(`${z.konflikte} ${z.konflikte === 1 ? 'Konflikt' : 'Konflikte'}`);
  if (z.vorschlaege) t.push(`${z.vorschlaege} ${z.vorschlaege === 1 ? 'Vorschlag' : 'Vorschläge'}`);
  if (z.unveraendert) t.push(`${z.unveraendert} unverändert`);
  return t.length > 0 ? t.join(' · ') : 'nichts zu tun';
}

/** Zuordnungsvorschläge (gleiche Raumnummer auf beiden Seiten): Tabelle mit Knopf «Zuordnen». */
export function zuordnungsvorschlaegeHtml(liste) {
  if (!liste?.length) return '';
  // Stränge (Modul 3) tragen «strang» (ein oder mehrere KICAD-Stränge, durch Komma getrennt), Räume «raum»
  if (liste.some((v) => v.strang !== undefined))
    return `<h4>Zuordnungsvorschläge</h4><p class="leise">Diese Stränge gibt es auf beiden Seiten mit derselben Bezeichnung. Bis sie zugeordnet sind, wird weder ein Strang in KICAD angelegt noch einer nach HKAP gesendet. Ein HKAP-Strang kann mehrere KICAD-Stränge bündeln (z. B. Vorlauf und Rücklauf).</p>
    <table class="tab kompakt"><thead><tr><th>HKAP-Strang</th><th>KICAD-Stränge</th><th>Grund</th><th></th></tr></thead><tbody>${
    liste.map((v) => `<tr><td>${esc(v.hkapId)}</td><td>${esc(v.strang)}</td><td class="leise">${esc(v.grund ?? '')}</td><td><button type="button" class="dlg-knopf h-zuordnen" data-hkap="${esc(v.hkapId)}" data-strang="${esc(v.strang)}">Zuordnen</button></td></tr>`).join('')}</tbody></table>`;
  return `<h4>Zuordnungsvorschläge</h4><p class="leise">Diese Räume gibt es auf beiden Seiten mit derselben Nummer. Bis sie zugeordnet sind, wird weder ein Raum in KICAD angelegt noch einer nach HKAP gesendet.</p>
    <table class="tab kompakt"><thead><tr><th>HKAP-Raum</th><th>KICAD-Raum</th><th>Grund</th><th></th></tr></thead><tbody>${
    liste.map((v) => `<tr><td>${esc(v.hkapId)}</td><td>${esc(v.raum)}</td><td class="leise">${esc(v.grund ?? '')}</td><td><button type="button" class="dlg-knopf h-zuordnen" data-hkap="${esc(v.hkapId)}" data-raum="${esc(v.raum)}">Zuordnen</button></td></tr>`).join('')}</tbody></table>`;
}

/**
 * Systemwahl für neue HKAP-Stränge (Modul 3): Hat KICAD kein oder mehrere zum Medium passende Systeme, legt «Aus HKAP übernehmen» den nicht platzierten
 * Strang erst nach einer Wahl an. Je HKAP-Strang eine Liste von Haken (passende Systeme zuerst, die übrigen einklappbar); gewählt = { hkapId: [system, …] }.
 */
export function systemWahlHtml(liste, wahl = {}) {
  if (!liste?.length) return '';
  const zeile = (e) => {
    const gewaehlt = new Set(wahl[e.hkapId] ?? []);
    const passend = new Set(e.kandidaten ?? []);
    const haken = (s) => `<label class="haken"><input type="checkbox" class="s-wahl" data-hkap="${esc(e.hkapId)}" value="${esc(s.kennzeichen)}"${gewaehlt.has(s.kennzeichen) ? ' checked' : ''}> ${esc(s.kennzeichen)} <span class="leise">${esc(s.systemtyp ?? '')}</span></label>`;
    const systeme = e.systeme ?? [];
    const erste = systeme.filter((s) => passend.has(s.kennzeichen)), rest = systeme.filter((s) => !passend.has(s.kennzeichen));
    const titel = [e.bezeichnung, e.medium ? `Medium ${e.medium}` : 'ohne Medium'].filter(Boolean).join(' · ');
    return `<tr><td>${esc(e.hkapId)}</td><td>${esc(titel)}</td><td>${erste.map(haken).join(' ')}${erste.length === 0 ? '<span class="leise">kein passendes System – </span>' : ''}${rest.length > 0 ? `<details class="hkap-details"><summary>weitere Systeme (${rest.length})</summary>${rest.map(haken).join(' ')}</details>` : ''}</td></tr>`;
  };
  return `<h4>Systemwahl für neue Stränge aus HKAP</h4><p class="leise">Zu diesen HKAP-Strängen hat KICAD kein oder mehrere passende Systeme. Ohne Wahl wird der Strang nicht angelegt. Mehrere Systeme: je System ein nicht platzierter Strang mit derselben HKAP-Zuordnung (z. B. Vorlauf und Rücklauf).</p>
    <table class="tab kompakt hkap-systemwahl"><thead><tr><th>HKAP-Strang</th><th>Bezeichnung</th><th>System in KICAD</th></tr></thead><tbody>${liste.map(zeile).join('')}</tbody></table>`;
}

/** Wahl der Systeme für hkap_uebernehmen: { ST1: ['HZ-VL', 'HZ-RL'] } oder null, wenn nichts gewählt ist. Nur HKAP-Stränge, die der Abgleich noch zur Wahl stellt. */
export function systemWahlParameter(wahl = {}, a = null) {
  const offen = a ? new Set(systemWahlIds(a)) : null;
  const o = {};
  for (const [id, liste] of Object.entries(wahl)) if (Array.isArray(liste) && liste.length > 0 && (!offen || offen.has(id))) o[id] = [...liste];
  return Object.keys(o).length > 0 ? o : null;
}

/** HKAP-Strang-Ids, zu denen der Abgleich eine Systemwahl verlangt. */
export function systemWahlIds(a) {
  return (a?.datenarten ?? []).flatMap((d) => (d.systemWahl ?? []).map((e) => e.hkapId));
}

/** Wahl nach einem Neuladen bereinigen: nur Stränge, die noch zur Wahl stehen. */
export function systemWahlBereinigen(wahl, a) {
  const da = new Set(systemWahlIds(a));
  return Object.fromEntries(Object.entries(wahl ?? {}).filter(([k]) => da.has(k)));
}

/** Nur lesen: Raumanforderungen bzw. Auslegungsdaten der Stränge aus HKAP (bei Räumen mit der Fläche in beiden Fassungen). */
export function nurLesenHtml(liste, datenart = 'raeume') {
  const l = (liste ?? []).filter((x) => x && istObjekt(x.werte));
  if (l.length === 0) return '';
  const zeilen = l.map((x) => {
    const w = { ...x.werte };
    const flaeche = [x.flaecheKicadM2 != null ? `KICAD ${wertText(x.flaecheKicadM2)} m²` : '', w.flaecheHkapM2 != null ? `HKAP ${wertText(w.flaecheHkapM2)} m²` : ''].filter(Boolean).join(' · ');
    delete w.flaecheHkapM2;
    return `<tr><td>${esc(x.element ?? x.hkapId ?? '')}${x.hkapId && x.hkapId !== x.element ? ` <span class="leise">${esc(x.hkapId)}</span>` : ''}</td><td class="hkap-wert">${esc(wertText(w, 220))}</td><td>${esc(flaeche)}</td><td class="leise">${x.geaendertInHkap?.length ? esc('seit dem letzten Abgleich in HKAP geändert: ' + x.geaendertInHkap.join(', ')) : ''}</td></tr>`;
  }).join('');
  return `<details class="hkap-details"><summary>Nur zum Lesen: ${datenart === 'straenge' ? 'Auslegung der Stränge aus HKAP' : 'Anforderungen aus HKAP'} (${l.length})</summary><p class="leise">KICAD kennt diese Werte (noch) nicht als Eigenschaft; sie werden angezeigt, aber nicht übernommen.</p>
    <table class="tab kompakt"><thead><tr><th>Datensatz</th><th>Werte aus HKAP</th><th>Fläche</th><th></th></tr></thead><tbody>${zeilen}</tbody></table></details>`;
}

/** Abweichungen, die in keine Richtung fliessen (z. B. «HKAP-Wert bleibt, KICAD leer»). */
export function abweichendHtml(liste) {
  const l = liste ?? [];
  if (l.length === 0) return '';
  return `<details class="hkap-details"><summary>Abweichend, aber ohne Fluss (${l.length})</summary>
    <table class="tab kompakt"><thead><tr><th>Datensatz</th><th>Feld</th><th>KICAD</th><th>HKAP</th><th>Grund</th></tr></thead><tbody>${
    l.map((f) => `<tr><td>${esc(f.element ?? f.hkapId ?? '')}</td><td>${esc(f.feld)}</td><td class="hkap-wert">${esc(wertText(f.kicad))}</td><td class="hkap-wert">${esc(wertText(f.hkap))}</td><td class="leise">${esc(hinweisFuerMenschen(f.grund ?? ''))}</td></tr>`).join('')}</tbody></table></details>`;
}

/** Ein Abschnitt je Datenart: Überschrift, Änderungstabelle, Konflikte mit Wahl, Zuordnungen, nicht platzierte Räume, Hinweise. */
export function datenartHtml(d, wahl = {}, uebrige = null, systemWahl = {}) {
  const titel = DATENART_NAMEN[d?.name] ?? d?.name ?? '';
  const kopf = `<h3 class="hkap-abschnitt" data-datenart="${esc(d?.name)}">${esc(titel)} <span class="leise">${esc(abschnittKopf(d))}</span></h3>`;
  if (!d) return '';
  if (d.name === 'pendenzen')
    return `<section class="hkap-art">${kopf}${pendenzenHtml(d)}<p class="leise">${esc(d.hinweis ?? '')} Gesendet wird über «Prüfbefunde senden» (Register Verwalten › HKAP).</p></section>`;
  const nurHinweise = !d.nachKicad && !d.nachHkap;
  if (nurHinweise) return `<section class="hkap-art">${kopf}${(d.hinweise ?? []).map((h) => `<p class="leise">${esc(h)}</p>`).join('')}</section>`;
  const konf = d.konflikte?.length
    ? `<h4>Konflikte (${d.konflikte.length}) – beide Seiten haben seit dem letzten Abgleich geändert</h4>${konflikteHtml(d.konflikte, wahl, uebrige)}` : '';
  const zuordn = d.zuordnungen?.length
    ? `<p class="leise">${d.zuordnungen.map((x) => `${esc(x.element ?? '')} ↔ HKAP ${esc(x.hkapId)}: ${esc(x.grund)}`).join('<br>')}</p>` : '';
  const platz = d.name === 'straenge' && d.nichtPlatziert?.length
    ? `<p class="hkap-platz"><b>Nicht platzierte Stränge (${d.nichtPlatziert.length}):</b> ${d.nichtPlatziert.slice(0, MAX_PLATZ).map(esc).join(', ')}${d.nichtPlatziert.length > MAX_PLATZ ? ` … und ${d.nichtPlatziert.length - MAX_PLATZ} weitere` : ''} <span class="leise">– Stränge aus HKAP ohne Leitungen (Platzhalter im passenden System): Leitungen mit diesem Strang zeichnen oder einen gezeichneten Strang dem HKAP-Strang zuordnen.</span></p>`
    : d.nichtPlatziert?.length
    ? `<p class="hkap-platz"><b>Nicht platzierte Räume (${d.nichtPlatziert.length}):</b> ${d.nichtPlatziert.slice(0, MAX_PLATZ).map(esc).join(', ')}${d.nichtPlatziert.length > MAX_PLATZ ? ` … und ${d.nichtPlatziert.length - MAX_PLATZ} weitere` : ''} <span class="leise">– im Projektbrowser unter «Nicht platzierte Räume»: Rechtsklick › Platzieren … oder einem gezeichneten Raum zuordnen.</span></p>` : '';
  const hinweise = d.hinweise?.length ? `<ul class="hinweisliste">${d.hinweise.map((h) => `<li>${esc(hinweisFuerMenschen(h))}</li>`).join('')}</ul>` : '';
  return `<section class="hkap-art">${kopf}${aenderungenZeilenHtml(zeilenVon(d))}${konf}${zuordnungsvorschlaegeHtml(d.zuordnungsvorschlaege)}${systemWahlHtml(d.systemWahl, systemWahl)}${zuordn}${platz}${abweichendHtml(d.abweichend)}${nurLesenHtml(d.nurLesen, d.name)}${hinweise}</section>`;
}

/** Alle Abschnitte des Abgleichs (Datenarten in fester Reihenfolge). */
export function abgleichHtml(a, wahl = {}, uebrige = null, systemWahl = {}) {
  const arten = DATENARTEN.map(([n]) => datenartVon(a, n)).filter(Boolean);
  return arten.map((d) => datenartHtml(d, wahl, uebrige, systemWahl)).join('');
}

/** Warnungen des Kerns zur Gegenprüfung (Name/Kürzel weichen ab, Id wiederkehrend) als Kasten. */
export function warnungenHtml(warnungen) {
  const w = (warnungen ?? []).filter(Boolean);
  if (w.length === 0) return '';
  return `<div class="hkap-kasten" role="status"><b>${w.length === 1 ? 'Bitte prüfen' : 'Bitte prüfen (' + w.length + ')'}</b>${w.map((t) => `<div>${esc(hinweisFuerMenschen(t))}</div>`).join('')}</div>`;
}

/** Basisstand in einem Satz. */
export function basisText(b) {
  if (!b) return '';
  return b.stand ? `vom ${b.stand} (${b.datensaetze ?? 0} Datensätze im Basisstand)` : 'Noch kein Abgleich: ohne Basisstand gilt eine leere Seite als unverändert.';
}

// ---------------------------------------------------------------------------------------------- Übernehmen und Senden

/** Parameter von hkap_uebernehmen: datenarten nur bei Teilauswahl (Vorgabe projekt, geschosse, raeume, straenge), konflikte nur mit Wahl, system (Systemwahl für neue Stränge) nur mit Wahl. */
export function uebernehmenParameter({ datenarten = UEBERNEHMBAR, wahl = {}, uebrige = null, systemWahl = {}, abgleich = null } = {}) {
  const arten = UEBERNEHMBAR.filter((a) => datenarten.includes(a));
  const p = {};
  if (arten.length !== UEBERNEHMBAR.length) p.datenarten = arten;
  const k = konfliktParameter(wahl, uebrige);
  if (k) p.konflikte = k;
  const s = systemWahlParameter(systemWahl, abgleich);
  if (s && arten.includes('straenge')) p.system = s;
  return p;
}

/** Parameter von hkap_senden: datenarten nur bei Teilauswahl (Vorgabe geschosse, raeume, straenge). Projektdaten führt HKAP und fallen weg. */
export function sendenParameter({ datenarten = SENDBAR } = {}) {
  const arten = SENDBAR.filter((a) => datenarten.includes(a));
  return arten.length !== SENDBAR.length ? { datenarten: arten } : {};
}

export function uebernehmenPruefen(datenarten) {
  return UEBERNEHMBAR.some((a) => datenarten.includes(a)) ? true : 'Bitte Projekt, Geschosse, Räume oder Stränge wählen.';
}

export function sendenPruefen(datenarten) {
  return SENDBAR.some((a) => datenarten.includes(a)) ? true : 'Zum Senden Geschosse, Räume oder Stränge wählen (Projektdaten führt HKAP, Pendenzen gehen mit «Prüfbefunde senden»).';
}

/** Art einer Zeile aus den Hinweisen des Kerns: konflikt (HKAP hat inzwischen geändert) | abgelehnt | vorschlag | '' */
export function hinweisArt(text) {
  const t = String(text ?? '');
  if (/^KONFLIKT\b/.test(t)) return 'konflikt';
  if (/\babgelehnt\b/i.test(t)) return 'abgelehnt';
  if (/Vorschlag/.test(t)) return 'vorschlag';
  return '';
}

/** Namen der Kern-Befehle in den Texten des Kerns (sie sind für MCP geschrieben) als Knöpfe der Oberfläche. */
const KNOPFNAMEN = {
  hkap_verknuepfen: '«Verbinden»', hkap_verbindung: '«Verbinden»', hkap_abgleich: '«Abgleich»', hkap_uebernehmen: '«Aus HKAP übernehmen»',
  hkap_senden: '«Nach HKAP senden»', hkap_befunde_senden: '«Prüfbefunde senden»', hkap_raum_zuordnen: '«Zuordnen»', hkap_strang_zuordnen: '«Zuordnen»',
};

/**
 * Ein Text des Kerns für Menschen: der Rat an die MCP-Werkzeuge am Ende («Weiter mit hkap_abgleich.») entfällt, Befehlsnamen (hkap_senden …)
 * heissen wie die Knöpfe der Oberfläche, Parameterlisten in geschweiften Klammern entfallen.
 */
export const hinweisFuerMenschen = (t) => String(t ?? '')
  .replace(/\s*Weiter mit hkap_\w+\.?\s*$/, '')
  .replace(/mit hkap_raum_zuordnen zuordnen/g, 'im Abgleich unter «Zuordnungsvorschläge» zuordnen')
  .replace(/zuerst hkap_raum_zuordnen/g, 'zuerst zuordnen (Abgleich, «Zuordnungsvorschläge»)')
  .replace(/mit hkap_strang_zuordnen zuordnen/g, 'im Abgleich unter «Zuordnungsvorschläge» zuordnen')
  .replace(/zuerst hkap_strang_zuordnen/g, 'zuerst zuordnen (Abgleich, «Zuordnungsvorschläge»)')
  .replace(/Wahl: hkap_uebernehmen mit system \{[^}]*\}(?: \([^)]*\))?/g, 'Wahl: im Abgleich unter «Systemwahl» das System ankreuzen')
  .replace(/mit hkap_strang_zuordnen einem gezeichneten Strang zuordnen/g, 'einem gezeichneten Strang zuordnen (Abgleich, «Zuordnungsvorschläge»)')
  .replace(/(hkap_\w+)(\s*\{[^}]*\})?/g, (m, name) => KNOPFNAMEN[name] ?? m)
  .trim();

/** Fällt HKAP aus (nicht erreichbar, antwortet nicht)? Das ist keine Störung der Oberfläche und bekommt keinen Fehlerton. */
export const istAusfall = (fehler) => /nicht erreichbar|antwortet nicht/i.test(String(fehler ?? ''));

/**
 * Ergebnis eines Befehls (hkap_uebernehmen, hkap_senden, hkap_befunde_senden, hkap_raum_zuordnen) für die Anzeige.
 * Die Zusammenfassung steht in hinweise[0] (bei «Keine Änderung.» als Meldung, auch bei hkap_senden für KICAD), der Rest sind Einzelzeilen.
 * → { art: 'ok'|'warnung'|'ausfall'|'fehler', titel, zusammenfassung, vorschlag, zeilen: [{ text, art }], konflikte, abgelehnt, trockenlauf }
 */
export function ergebnisAnzeige(r) {
  if (!r) return { art: 'fehler', titel: 'Keine Antwort', zusammenfassung: 'Der Server hat nicht geantwortet.', vorschlag: '', zeilen: [], konflikte: 0, abgelehnt: 0, trockenlauf: false };
  if (!r.angenommen) {
    const text = hinweisFuerMenschen(r.fehler || r.meldung || 'Der Befehl wurde abgelehnt.');
    return { art: istAusfall(text) ? 'ausfall' : 'fehler', titel: istAusfall(text) ? 'HKAP ist nicht erreichbar' : 'Abgelehnt', zusammenfassung: text, vorschlag: hinweisFuerMenschen(r.vorschlag ?? ''), zeilen: [], konflikte: 0, abgelehnt: 0, trockenlauf: !!r.trockenlauf };
  }
  const hinweise = (r.hinweise ?? []).filter(Boolean).map(hinweisFuerMenschen);
  const zusammenfassung = hinweise[0] ?? r.meldung ?? 'Erledigt.';
  const zeilen = hinweise.slice(1).map((text) => ({ text, art: hinweisArt(text) }));
  const konflikte = zeilen.filter((z) => z.art === 'konflikt').length;
  const abgelehnt = zeilen.filter((z) => z.art === 'abgelehnt').length;
  return { art: konflikte + abgelehnt > 0 ? 'warnung' : 'ok', titel: r.trockenlauf ? 'Vorschau (Trockenlauf – nichts wurde geändert)' : 'Ergebnis', zusammenfassung, vorschlag: '', zeilen, konflikte, abgelehnt, trockenlauf: !!r.trockenlauf };
}

/** HTML des Ergebnisses; aenderungen (Tabelle der KICAD-Änderungen) liefert der Aufrufer als fertigen HTML-Text. */
export function ergebnisHtml(r, aenderungenTabelleHtml = '') {
  const e = ergebnisAnzeige(r);
  if (e.art === 'ausfall')
    return `<div class="hkap-kasten" role="status"><b>${esc(e.titel)}</b><div>${esc(e.zusammenfassung)}</div>${e.vorschlag ? `<div class="leise">Vorschlag: ${esc(e.vorschlag)}</div>` : ''}</div>`;
  if (e.art === 'fehler')
    return `<div class="fehlerkasten" role="alert"><b>${esc(e.zusammenfassung)}</b>${e.vorschlag ? `<div>Vorschlag: ${esc(e.vorschlag)}</div>` : ''}</div>`;
  const kasten = e.konflikte > 0
    ? `<div class="hkap-kasten"><b>${e.konflikte} ${e.konflikte === 1 ? 'Datensatz' : 'Datensätze'} in HKAP inzwischen geändert – nichts überschrieben.</b><div>Den Abgleich neu laden: der aktuelle Stand steht dann in der Konfliktliste.</div></div>` : '';
  const liste = e.zeilen.length === 0 ? '' : `<ul class="hinweisliste hkap-hinweise">${e.zeilen.map((z) => `<li class="${z.art}">${esc(z.text)}</li>`).join('')}</ul>`;
  return `<div class="hkap-ergebnis-inhalt ${e.art}"><h4>${esc(e.titel)}</h4><p class="hkap-summe"><b>${esc(e.zusammenfassung)}</b></p>${kasten}${aenderungenTabelleHtml}${liste}</div>`;
}

// ---------------------------------------------------------------------------------------------- Prüfbefunde senden

export const schwereGueltig = (s) => SCHWEREN.some(([w]) => w === s);

/** Parameter von hkap_befunde_senden (Vorgabe «fehler» wird ausdrücklich mitgeschickt, damit die Vorschau und das Senden dasselbe tun). */
export function befundeParameter(schwere = 'fehler') {
  return { schwere: schwereGueltig(schwere) ? schwere : 'fehler' };
}

export const SCHWERE_ERKLAERUNG = {
  fehler: 'nur Befunde der Schwere «fehler» (empfohlen)',
  warnung: 'Fehler und Warnungen',
  hinweis: 'alle Befunde, auch Hinweise',
};

// ---------------------------------------------------------------------------------------------- Nicht platzierte Räume

/**
 * Ein Raum ohne Geometrie ist nicht platziert. Liefert die Darstellung die Kennungen mit (Feld «kennungen»), zählt nur ein Raum mit Kennung «hkap»;
 * sonst gilt jeder Raum ohne Geometrie (in KICAD gibt es sonst keinen).
 */
export function istNichtPlatziert(e) {
  if (e?.klasse !== 'Raum') return false;
  if ((e.geometrie?.punkte?.length ?? 0) > 0) return false;
  return e.kennungen ? Object.prototype.hasOwnProperty.call(e.kennungen, 'hkap') : true;
}

/** Nummer eines Raums: Eigenschaft «nummer», sonst das Kennzeichen. */
export const raumNummer = (r) => String(r?.werte?.nummer ?? r?.kennzeichen ?? '').trim();

/** «1.02 Bad» (Nummer und Name, sonst Nutzung). */
export function raumTitel(r) {
  const n = raumNummer(r);
  const z = r?.name || r?.werte?.nutzung || '';
  return [n, z].filter(Boolean).join(' ');
}

/** Wie istNichtPlatziert, mit Sortierung nach Geschoss und Nummer. */
export function nichtPlatzierte(elemente) {
  const k = (a, b) => String(a ?? '').localeCompare(String(b ?? ''), 'de', { numeric: true });
  return (elemente ?? []).filter(istNichtPlatziert).sort((a, b) => k(a.geschoss, b.geschoss) || k(raumNummer(a), raumNummer(b)) || k(a.kennzeichen, b.kennzeichen));
}

/** HKAP-Raum-Id aus einer Kennung «PJ-…/E1-S3» (oder null). */
export function hkapIdAusKennung(kennung, projektierung = null) {
  const m = /^([^/]+)\/(.+)$/.exec(String(kennung ?? '').trim());
  if (!m) return null;
  return projektierung && m[1].toLowerCase() !== String(projektierung).toLowerCase() ? null : m[2];
}

/**
 * HKAP-Raum-Id aus dem Herkunftstext eines übernommenen Werts («… HKAP P-005/PJ-2026-001/E1-S3, Feld raumNummer, Stand …»), wie ihn element_lesen
 * liefert. Hilfsweg, solange die Darstellung die Kennungen nicht mitliefert.
 */
export function hkapIdAusHerkunft(text, projektierung = null) {
  const m = /HKAP\s+[^/\s,]+\/([^/\s,]+)\/([^\s,]+)/.exec(String(text ?? ''));
  if (!m) return null;
  return projektierung && m[1].toLowerCase() !== String(projektierung).toLowerCase() ? null : m[2];
}

/** HKAP-Raum-Id eines nicht platzierten Raums: Kennung der Darstellung › Herkunft einer Eigenschaft aus element_lesen › null. */
export function hkapRaumId(e, lesen = null, projektierung = null) {
  const k = hkapIdAusKennung(e?.kennungen?.hkap, projektierung);
  if (k) return k;
  for (const w of Object.values(lesen?.eigenschaften ?? {})) {
    const id = hkapIdAusHerkunft(w?.herkunft, projektierung);
    if (id) return id;
  }
  return null;
}

/** Gezeichnete Räume, die ein nicht platzierter Raum bekommen kann: gleiche Nummer zuerst, dann gleiches Geschoss. Schon zugeordnete (Kennung bekannt) entfallen. */
export function zuordnenKandidaten(raeume, platzhalter) {
  const k = (a, b) => String(a ?? '').localeCompare(String(b ?? ''), 'de', { numeric: true });
  const nr = raumNummer(platzhalter).toLowerCase();
  return (raeume ?? [])
    .filter((r) => r.klasse === 'Raum' && r.id !== platzhalter?.id && !istNichtPlatziert(r) && !r.kennungen?.hkap)
    .map((r) => ({ raum: r, gleicheNummer: nr !== '' && raumNummer(r).toLowerCase() === nr, gleichesGeschoss: !!platzhalter?.geschoss && r.geschoss === platzhalter.geschoss }))
    .sort((a, b) => Number(b.gleicheNummer) - Number(a.gleicheNummer) || Number(b.gleichesGeschoss) - Number(a.gleichesGeschoss) || k(a.raum.kennzeichen, b.raum.kennzeichen));
}

/** Parameter von hkap_raum_zuordnen. */
export const zuordnenParameter = (raum, hkapRaum) => ({ raum: String(raum), hkapRaum: String(hkapRaum) });

/** Parameter von hkap_strang_zuordnen (Modul 3): ein oder mehrere KICAD-Stränge (Kennzeichen, durch Komma getrennt) und die HKAP-Strang-Id. */
export const strangZuordnenParameter = (strang, hkap) => ({ strang: String(strang), hkap: String(hkap) });

/** Vorbelegung des Raum-Werkzeugs (Nummer, Nutzung) aus einem nicht platzierten Raum. */
export function raumVorbelegung(r) {
  return { nummer: String(r?.werte?.nummer ?? '').trim(), nutzung: String(r?.werte?.nutzung ?? '').trim() };
}

/** Der erste gezeichnete Raum, der seit dem Merken der Ids dazugekommen ist (Raum mit Geometrie), sonst null. */
export function neuGezeichneterRaum(vorherIds, raeume) {
  const v = vorherIds instanceof Set ? vorherIds : new Set(vorherIds ?? []);
  return (raeume ?? []).find((r) => r.klasse === 'Raum' && !v.has(r.id) && (r.geometrie?.punkte?.length ?? 0) > 0) ?? null;
}

/** Der Grundriss, in dem ein Raum platziert werden kann: Ansicht der Art grundriss (keine Vorlage) im Geschoss des Raums. */
export function grundrissFuer(ansichten, raum) {
  return (ansichten ?? []).filter((a) => a.klasse === 'Ansicht' && a.werte?.art === 'grundriss' && a.werte?.vorlage !== true && raum?.geschoss && a.geschoss === raum.geschoss)
    .sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }))[0] ?? null;
}

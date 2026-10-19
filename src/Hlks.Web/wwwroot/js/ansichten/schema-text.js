// Schema-Ansicht (Modul 3, Teil C2): reine Texte und HTML-Bausteine – ohne DOM, in Node getestet (schema.test.mjs).
// Banner mit den Hinweisen des Kerns, Tooltip, Liste im Dialog «Element einfügen», Auswahl der Anschlüsse beim Verbinden.
// Alles, was aus Daten stammt, wird maskiert (esc).

import { esc } from './hilfen.js';

/** Banner oben in der Ansicht: Zeile mit Zahl und erstem Hinweis, aufgeklappt die ganze Liste. offen = aufgeklappt. */
export function bannerHtml(banner, offen = false) {
  if (!banner || banner.anzahl === 0) return '';
  const kopf = `<button type="button" class="sc-banner-kopf" aria-expanded="${offen ? 'true' : 'false'}" title="${offen ? 'Hinweise zuklappen' : 'Alle Hinweise des Kerns zeigen'}">`
    + `<span class="sc-banner-zahl">${banner.anzahl} Hinweis${banner.anzahl === 1 ? '' : 'e'}</span>`
    + (offen ? '' : `<span class="sc-banner-text">${esc(banner.erste)}</span>`) + '<span class="sc-banner-pfeil">' + (offen ? '▴' : '▾') + '</span></button>';
  const liste = offen ? `<ul class="sc-banner-liste">${banner.alle.map((h) => `<li>${esc(h)}</li>`).join('')}</ul>` : '';
  return kopf + liste;
}

/** Tooltip eines Knotens: «WC-E.02 · WC · Strang KW-S1 · EG · nicht platziert · Hand-Lage». */
export function knotenTip(k) {
  if (!k) return '';
  const t = [k.kennzeichen, k.klasse];
  if (k.strang) t.push(`Strang ${k.strang}`); else if (k.system) t.push(`System ${k.system}`);
  if (k.geschoss) t.push(k.geschoss);
  if (k.platziert === false) t.push('nicht platziert');
  if (k.manuell) t.push('Hand-Lage');
  return t.join(' · ');
}

/** Tooltip eines Linienteils: «SW-F1-1 · Rohr · NW 100 · SW» (el = Darstellung des Elements, linie = Eintrag der Abfrage). */
export function linieTip(linie, el) {
  const t = [];
  if (el) { t.push(el.kennzeichen, el.klasse); } else t.push('Verbindung');
  if (linie?.groessen?.length) t.push(linie.groessen.join(' / '));
  if (linie?.medium) t.push(linie.medium);
  if (el && !el.geometrie) t.push('nicht platziert');
  return t.join(' · ');
}

/** Kennzeichen-Hinweis in der Statusleiste für die Werkzeuge. */
export const TOOL_HINWEIS = Object.freeze({
  verbinden0: 'Verbinden: ersten Knoten anklicken  (Esc beendet)',
  verbinden1: (kz) => `Verbinden: zweiten Knoten anklicken – von ${kz}  (Esc beendet)`,
  trennen: 'Trennen: eine Linie (oder einen Knoten) anklicken  (Esc beendet)',
  einfuegen: (was) => `Einfügen: ${was} – in eine Zelle (Geschoss × Strang) klicken  (Esc beendet)`,
});

// ---------------------------------------------------------------------------------------------- Element einfügen

const GRUPPEN = [['endgeraete', 'Endgeräte'], ['geraete', 'Geräte'], ['armaturen', 'Armaturen'], ['formteile', 'Formteile']];
const ART_TEXT = { endgeraet: 'Endgerät', geraet: 'Gerät', armatur: 'Armatur', formteil: 'Formteil' };
export const artText = (art) => ART_TEXT[art] ?? art;

/**
 * Wahlliste des Dialogs: [{ gruppe, eintraege: [{ wert, titel, sub }] }] aus den einfügbaren Klassen
 * (schema-logik.einfuegbareKlassen) und Familien (einfuegbareFamilien). wert: «k:WC» bzw. «f:FAM-1|Typ».
 */
export function einfuegenGruppen(klassen, familien) {
  const aus = [];
  for (const [schluessel, titel] of GRUPPEN) {
    const l = klassen?.[schluessel] ?? [];
    if (l.length) aus.push({ gruppe: titel, eintraege: l.map((k) => ({ wert: `k:${k.name}`, titel: k.name, sub: k.beschreibung ?? '' })) });
  }
  const fam = [];
  for (const f of familien ?? []) for (const t of f.typen) fam.push({ wert: `f:${f.kennzeichen}|${t}`, titel: `${f.name}: ${t}`, sub: `${f.kategorie} · ${artText(f.art)}` });
  if (fam.length) aus.push({ gruppe: 'Familien', eintraege: fam });
  return aus;
}

/** «k:WC» → { art: 'klasse', klasse } · «f:FAM-1|22-600» → { art: 'familie', familie, typ } · sonst null. */
export function wahlAusWert(wert) {
  const s = String(wert ?? '');
  if (s.startsWith('k:') && s.length > 2) return { art: 'klasse', klasse: s.slice(2) };
  if (s.startsWith('f:')) {
    const i = s.indexOf('|');
    if (i > 2 && i < s.length - 1) return { art: 'familie', familie: s.slice(2, i), typ: s.slice(i + 1) };
  }
  return null;
}

/** Filtert die Gruppen nach einem Suchtext (Titel oder Beschreibung); leere Gruppen entfallen. */
export function einfuegenFiltern(gruppen, text) {
  const q = String(text ?? '').trim().toLowerCase();
  if (!q) return gruppen;
  return gruppen
    .map((g) => ({ gruppe: g.gruppe, eintraege: g.eintraege.filter((e) => `${e.titel} ${e.sub}`.toLowerCase().includes(q)) }))
    .filter((g) => g.eintraege.length > 0);
}

/** HTML der Wahlliste (Optionsfelder); vorgewaehlt = wert. */
export function einfuegenListeHtml(gruppen, vorgewaehlt = null) {
  if (!gruppen.length) return '<p class="leise">Keine passende Klasse oder Familie.</p>';
  return gruppen.map((g) => `<div class="sc-gruppe"><div class="sc-gruppentitel">${esc(g.gruppe)}</div>${g.eintraege.map((e) =>
    `<label class="sc-wahl"><input type="radio" name="sc-wahl" value="${esc(e.wert)}"${e.wert === vorgewaehlt ? ' checked' : ''}><span class="sc-wahl-titel">${esc(e.titel)}</span>${e.sub ? `<span class="leise sc-wahl-sub">${esc(e.sub)}</span>` : ''}</label>`).join('')}</div>`).join('');
}

/** Rahmen des Dialogs «Element einfügen». */
export function einfuegenDialogHtml(gruppen, vorgewaehlt = null) {
  return `<div class="sc-einfuegen">
    <p class="dlg-text">Klasse oder Familie wählen, danach in die Zelle (Geschoss × Strang) klicken, in die das Element kommen soll. Es entsteht <b>ohne Lage im Gebäude</b> («nicht platziert») und steht zunächst nur im Schema.</p>
    <input type="search" class="sc-suche" placeholder="Suchen …" aria-label="Klasse oder Familie suchen">
    <div class="sc-liste">${einfuegenListeHtml(gruppen, vorgewaehlt)}</div>
    <label class="fz"><span>Kennzeichen</span><input type="text" class="sc-kz" placeholder="leer = automatisch"></label>
    <label class="fz"><span>Name</span><input type="text" class="sc-name" placeholder="leer = ohne"></label>
  </div>`;
}

// ---------------------------------------------------------------------------------------------- Anschlüsse beim Verbinden

/**
 * Dialog bei mehreren passenden Anschlusspaaren. paare = [[anschlussA, anschlussB], …] aus der Ablehnung des Kerns;
 * ohne Paare zeigt er die freien Anschlüsse beider Elemente (frei = { a: [{ name, medium, fluss }], b: [...] }).
 */
export function anschlussDialogHtml({ a, b, paare = [], frei = null, meldungText = '' }) {
  const kopf = `<p class="dlg-text">${esc(a)} und ${esc(b)} lassen sich auf mehrere Arten verbinden. Welche Anschlüsse?</p>`
    + (meldungText ? `<p class="leise">${esc(meldungText)}</p>` : '');
  if (paare.length) {
    return `<div class="sc-anschluss">${kopf}${paare.map(([x, y], i) =>
      `<label class="sc-wahl"><input type="radio" name="sc-paar" value="${i}"${i === 0 ? ' checked' : ''}><span class="sc-wahl-titel">${esc(a)}.${esc(x)} ↔ ${esc(b)}.${esc(y)}</span></label>`).join('')}</div>`;
  }
  const opt = (l) => (l ?? []).map((x) => `<option value="${esc(x.name)}">${esc(x.name)}${x.medium ? ` (${esc(x.medium)}${x.fluss ? ', ' + esc(x.fluss) : ''})` : ''}</option>`).join('');
  return `<div class="sc-anschluss">${kopf}
    <label class="fz"><span>${esc(a)}</span><select class="sc-a">${opt(frei?.a)}</select></label>
    <label class="fz"><span>${esc(b)}</span><select class="sc-b">${opt(frei?.b)}</select></label></div>`;
}

/** Freie Anschlüsse eines Elements aus der Antwort der Abfrage element_lesen (verbundenMit leer). */
export function freieAnschluesse(lesen) {
  return (lesen?.anschluesse ?? []).filter((x) => !x.verbundenMit).map((x) => ({ name: x.name, medium: x.medium, fluss: x.fluss }));
}

// ---------------------------------------------------------------------------------------------- Schema neu anordnen

/** Text der Rückfrage «Schema neu anordnen»: n Hand-Lagen, davon v verwaiste. */
export function anordnenText(handLagen, verwaiste = 0) {
  const n = handLagen;
  const von = n === 1 ? 'Ein Knoten ist' : `${n} Knoten sind`;
  return `${von} von Hand angeordnet${verwaiste > 0 ? ` (${verwaiste} Hand-Lage${verwaiste === 1 ? '' : 'n'} ohne Element im Schema)` : ''}.\n`
    + 'Alle Hand-Lagen entfernen? Das Schema ist dann wieder ganz automatisch angeordnet. «Nur Verwaiste aufräumen» entfernt nur die Lagen gelöschter oder nicht mehr gezeigter Elemente.';
}

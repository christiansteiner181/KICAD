// Eigenschaftenpalette für Familien (Modul 2, C1): HTML-Bausteine und Eingabe-Umrechnung – rein, in Node getestet (familien.test.mjs).
//   - platzierte Elemente (Bezug «familie»): Gruppe «Familie» (Familie mit Sprung in den Editor, Typ als Auswahl, Exemplarparameter),
//     Gruppe «Anschlüsse» (aus der Abfrage anschlusslagen, Weltlage), Marke «Familie» an Kennwerten mit Herkunft familie;
//   - das Element Familie selbst (Editor aktiv oder im Projektbrowser gewählt): Kopf (Name, Hersteller, Produkt, Beschreibung), Status.
// Die Palette ändert nie selbst: Typwechsel und Exemplarwerte gehen als element_aendern an den Kern, Kopfangaben als familie_aendern (kopf_setzen).
// Die Bausteine zeile/gruppe/… liefert eigenschaften.js (Parameter H), damit das Aussehen einheitlich bleibt.

import { esc, feldText, zahlLesen } from './familien-text.js';

/** Exemplarparameter einer Familie ohne Formel (Formelparameter sind nicht einstellbar). */
export const exemplarParameter = (f) => (f?.definition?.parameter ?? []).filter((p) => p.art === 'exemplar' && (p.formel === undefined || p.formel === null));
export const exemplarFormeln = (f) => (f?.definition?.parameter ?? []).filter((p) => p.art === 'exemplar' && p.formel !== undefined && p.formel !== null);

/** Wert des Typs für einen Parameter (aufgelöst vom Kern: Typwert oder Vorgabe) oder undefined. */
export function typVorgabe(f, typName, name) {
  const t = (f?.typen ?? []).find((x) => x.name === typName);
  const v = t?.werte?.[name];
  return v === null ? undefined : v;
}

/** Marke «Familie» an einem Kennwert, dessen Herkunft «familie» ist (Herkunftstext aus element_lesen: «familie, T, Zeit, Familie Typ …»). */
export function familienMarke(herkunft) {
  return /^familie\b/i.test(String(herkunft ?? '')) ? '<span class="ep-fam-marke" title="Der Wert kommt aus der Familie; eine Änderung von Hand wird bei der nächsten Nachführung ersetzt.">Familie</span>' : '';
}

/** Neues Objekt der Exemplarwerte nach einer Eingabe → { objekt | null } oder { fehler }. text (Zahl/Text) oder checked (ja_nein). */
export function exemplarNeu(bisher, p, eingabe) {
  const n = { ...(bisher && typeof bisher === 'object' ? bisher : {}) };
  let wert;
  if (p.datentyp === 'ja_nein') wert = eingabe === true || eingabe === 'true' ? true : eingabe === false || eingabe === 'false' ? false : undefined;
  else {
    const t = String(eingabe ?? '').trim();
    if (t === '') wert = undefined;
    else if (p.datentyp === 'text' || p.datentyp === 'medium') wert = t;
    else { const z = zahlLesen(t); if (z === null) return { fehler: 'Bitte eine Zahl eingeben (Formeln gibt es nur in der Familie).' }; wert = z; }
  }
  if (wert === undefined) delete n[p.name]; else n[p.name] = wert;
  return { objekt: Object.keys(n).length ? n : null };
}

const EINHEIT = { laenge: 'mm', winkel: '°', dn: 'DN' };
const einheitVon = (p) => (p.datentyp === 'zahl' ? p.einheit : EINHEIT[p.datentyp]) ?? '';

/** Gruppen «Familie» und «Anschlüsse» für ein platziertes Element. d = { f (Antwort familie), lagen (Antwort anschlusslagen oder null), typ, exemplar, kz }. */
export function elementGruppen(d, H) {
  const f = d.f;
  if (!f) return '';
  const typen = (f.typen ?? []).map((t) => [t.name, t.name]);
  const zeilen = [];
  zeilen.push(H.zeile('Familie', `<span class="ep-text" title="${esc(f.kennzeichen)} · ${esc(f.kategorie)}">${esc(f.name)}</span><button type="button" class="ep-fam-knopf" data-aktion="fam-editor" data-fam="${esc(f.kennzeichen)}" title="Familie im Editor öffnen">Editor</button>`, { titel: `${f.kennzeichen} · ${f.kategorie} · ${f.status === 'freigegeben' ? 'freigegeben' : 'Entwurf'}, Version ${f.version}` }));
  const aktuell = d.typ ?? null;
  zeilen.push(H.zeile('Typ', `<select data-ziel="eig:familientyp" data-typ="auswahl">${typen.map(([w, t]) => `<option value="${esc(w)}"${w === aktuell ? ' selected' : ''}>${esc(t)}</option>`).join('')}${aktuell && !typen.some(([w]) => w === aktuell) ? `<option value="${esc(aktuell)}" selected>${esc(aktuell)} (gibt es nicht mehr)</option>` : ''}</select>`, { titel: 'Typ der Familie (Typwechsel führt Anschlüsse und Kennwerte nach)' }));
  const ex = d.exemplar && typeof d.exemplar === 'object' ? d.exemplar : {};
  for (const p of exemplarParameter(f)) {
    const ein = einheitVon(p);
    const gesetzt = Object.hasOwn(ex, p.name);
    const vorgabe = typVorgabe(f, aktuell, p.name);
    const wert = gesetzt ? ex[p.name] : undefined;
    const titel = [p.beschreibung, gesetzt ? 'Exemplarwert' : 'Wert des Typs (leer lassen = Vorgabe)'].filter(Boolean).join('\n');
    if (p.datentyp === 'ja_nein') {
      const an = gesetzt ? wert === true || wert === 1 : vorgabe === true || vorgabe === 1;
      zeilen.push(H.zeile(p.name, `<input type="checkbox" data-ziel="fam:param:${esc(p.name)}" data-typ="famja" ${an ? 'checked' : ''}>`, { titel }));
    } else {
      const ph = vorgabe === undefined ? '' : feldText(vorgabe);
      zeilen.push(H.zeile(p.name, `<input type="text" data-ziel="fam:param:${esc(p.name)}" data-typ="famtext" data-datentyp="${esc(p.datentyp)}" value="${esc(gesetzt ? feldText(wert) : '')}" placeholder="${esc(ph)}">${ein ? `<span class="ep-einheit">${esc(ein)}</span>` : ''}`, { titel }));
    }
  }
  for (const p of exemplarFormeln(f)) zeilen.push(H.zeile(p.name, `<span class="ep-text mono" title="Formel – nicht einstellbar">= ${esc(p.formel)}</span>`, { titel: p.beschreibung ?? 'Wert aus einer Formel' }));
  let h = H.gruppe('Familie', zeilen);

  const l = d.lagen?.anschluesse ?? [];
  if (l.length) {
    const z = l.map((a) => {
      const gross = a.dn != null ? `DN ${a.dn}` : a.b != null ? `${a.b}×${a.h ?? '?'}` : a.d != null ? `Ø${a.d}` : '';
      const lage = (a.lage ?? []).map((x) => feldText(Math.round(x * 100) / 100)).join(' · ');
      return H.zeile(a.name, `<span class="ep-text" title="Lage in Weltkoordinaten (mm): ${esc(lage)}">${esc([a.medium, a.fluss, gross].filter(Boolean).join(' · '))}</span>`, { titel: `Lage x · y · z (mm): ${lage}\nRichtung: ${(a.richtung ?? []).join(', ')}${a.verbunden ? `\nverbunden mit ${a.verbunden}` : ''}`, klasse: 'ep-anschluss' })
        + H.zeile('', `<span class="ep-text mono" data-lage="${esc(a.name)}">${esc(lage)}</span>${a.verbunden ? `<span class="ep-einheit">→ ${esc(a.verbunden)}</span>` : ''}`, { klasse: 'klein' });
    });
    h += H.gruppe('Anschlüsse', z);
  }
  return h;
}

/** Palette des Elements Familie: Kopf (kopf_setzen), Status, Zahlen. d = { f (Antwort familie) }. */
export function familienKopfHtml(d, H) {
  const f = d.f;
  if (!f) return '<div class="ep-leer">Familie wird geladen …</div>';
  const def = f.definition ?? {};
  const kopfFeld = (name, wert, extra = '') => `<input type="text" data-ziel="famkopf:${name}" data-typ="text" value="${esc(wert ?? '')}" ${extra}>`;
  let h = `<div class="ep-kopf"><div class="ep-typ"><span class="ep-sym">${H.symbol ?? ''}</span><b>Familie</b></div>`
    + `<div class="ep-hinweis">${f.status === 'freigegeben' ? 'freigegeben' : 'Entwurf'} · Version ${esc(f.version)}${(f.verwendungen ?? []).length ? ` · ${(f.verwendungen ?? []).length} platzierte Elemente` : ''}</div></div>`;
  h += H.gruppe('Identität', [
    H.zeile('Kennzeichen', H.nurLesen(f.kennzeichen)),
    H.zeile('Name', kopfFeld('name', def.name ?? f.name), { pflicht: true }),
    H.zeile('Kategorie', H.nurLesen(f.kategorie), { titel: 'KICAD-Klasse der Elemente (kann nicht geändert werden)' }),
  ]);
  h += H.gruppe('Angaben', [
    H.zeile('Hersteller', kopfFeld('hersteller', def.hersteller)),
    H.zeile('Produkt', kopfFeld('produkt', def.produkt)),
    H.zeile('Beschreibung', kopfFeld('beschreibung', def.beschreibung)),
  ]);
  h += H.gruppe('Inhalt', [
    H.zeile('Parameter', H.nurLesen(String((def.parameter ?? []).length))),
    H.zeile('Typen', H.nurLesen((f.typen ?? []).map((t) => t.name).join(', ') || '–')),
    H.zeile('Körper', H.nurLesen(String((def.koerper ?? []).length))),
    H.zeile('Anschlüsse', H.nurLesen(String((def.anschluesse ?? []).length))),
    H.zeile('Verwendungen', H.nurLesen((f.verwendungen ?? []).join(', ') || '–')),
  ]);
  if (!f.gueltig || (f.definitionsfehler ?? []).length) h += H.gruppe('Prüfung', (f.definitionsfehler ?? []).map((e) => H.zeile('', `<span class="fehlertext">${esc(e.stelle)}: ${esc(e.meldung)}</span>`)));
  return h;
}

/** kopf_setzen-Parameter aus einem Feld der Kopfpalette: leer entfernt (ausser name). */
export function kopfOp(feld, text) {
  const t = String(text ?? '').trim();
  if (feld === 'name' && t === '') return { fehler: 'Der Name darf nicht leer sein.' };
  return { op: { op: 'kopf_setzen', [feld]: t === '' ? null : t } };
}

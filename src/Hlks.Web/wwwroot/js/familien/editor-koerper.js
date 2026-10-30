// Familieneditor (Modul 2, C1): die Seiten Körper und Sinnbild – links die Liste, rechts die Eigenschaften des gewählten Eintrags
// je nach Art (Quader, Zylinder, Kegel, Kugel, Extrusion, Rotation, Sweep; Linie, Polylinie, Polygon, Kreis, Bogen, Text).
// Jede Zahl darf eine Formel sein (Trockenlauf des Kerns bei jeder Eingabe). Konturen und Pfade sind Punkttabellen (u, v, Wölbung bzw. x, y, z).
// Änderungen gehen als ops an familie_aendern: koerper_setzen / koerper_loeschen bzw. sinnbild_setzen.

import {
  esc, klon, KOERPER_ARTEN, MATERIALIEN, DETAILGRADE, SINNBILD_ARTEN, STIFTE, LINIENARTEN, koerperFelder, koerperNeu, freieKoerperId, koerperDetailgrade, detailgradKurz,
  sinnbildFelder, sinnbildNeu, pfadLesen, pfadSetzen, zahlOderFormel, zahlLesen, feldText, punktAusTexten, konturZeile, profilWechseln, profilArt, freierName,
} from './familien-text.js';
import { eingabeFeld, auswahlFeld, haken, knopf, tabelle, leerHinweis, formZeile } from './editor-bausteine.js';
import { sinnbildPrimitive } from './familien-ansicht.js';
import { primitivSvg } from '../ansichten/plangrafik.js';

const KOORD = ['x', 'y', 'z'], UV = ['u', 'v'];

// ------------------------------------------------------------------------------------------------ Felder zeichnen

function zellen(werte, n, fk, namen, o = {}) {
  return Array.from({ length: n }, (_, i) => eingabeFeld(feldText(werte?.[i]), { fk: `${fk}.${i}`, feld: o.feld, live: true, klasse: 'xyz', titel: `${namen[i]} (Zahl oder Formel)`, platzhalter: namen[i], extra: { 'data-c': i } })).join('');
}

function punktTabelle(punkte, fk, spalten, o) {
  const kopf = [...(spalten === 2 ? UV : KOORD), ...(o.woelbung ? ['Wölbung'] : []), ''];
  const zeilen = (punkte ?? []).map((p, i) => `<tr class="fe-pz" data-i="${i}">${Array.from({ length: spalten + (o.woelbung ? 1 : 0) }, (_, c) => `<td>${eingabeFeld(feldText(p?.[c]), {
    fk: `${fk}.${i}.${c}`, live: true, klasse: 'xyz', platzhalter: c < spalten ? (spalten === 2 ? UV : KOORD)[c] : 'Wölbung', titel: c < spalten ? 'Zahl oder Formel' : 'DXF-Wölbung (leer = gerade Strecke; tan(Öffnungswinkel/4))', extra: { 'data-c': c } })}</td>`).join('')}
    <td class="fe-k">${(o.min ?? 0) < (punkte ?? []).length ? knopf('✕', 'punkt-weg', { klasse: 'klein', titel: 'Punkt löschen' }) : ''}</td></tr>`);
  const mehr = o.max === undefined || (punkte ?? []).length < o.max;
  return `${tabelle(kopf, zeilen, { klasse: 'punkttabelle' })}${mehr ? knopf('＋ Punkt', 'punkt-neu', { titel: 'Punkt anfügen', klasse: 'klein' }) : ''}`;
}

/** HTML eines Feldes f (Beschreibung aus koerperFelder/sinnbildFelder) mit dem aktuellen Wert. */
function feldHtml(f, wert, fk) {
  const h = (inner, extra = '') => `<div class="fe-feld" data-pfad="${esc(f.pfad)}" data-ftyp="${f.typ}"${f.optional ? ' data-optional="1"' : ''}${extra}>${inner}</div>`;
  switch (f.typ) {
    case 'zahl': return formZeile(f.label, h(eingabeFeld(feldText(wert), { fk, live: true, platzhalter: f.optional ? 'leer = Vorgabe' : 'Zahl oder Formel', titel: 'Zahl oder Formel in mm bzw. Grad' })));
    case 'zahlnur': return formZeile(f.label, h(eingabeFeld(feldText(wert), { fk, klasse: 'schmal' })));
    case 'text': return formZeile(f.label, h(eingabeFeld(wert ?? '', { fk })));
    case 'auswahl': return formZeile(f.label, h(auswahlFeld(f.optionen, wert ?? '', { fk })));
    case 'punkt3': return formZeile(f.label, h(zellen(wert ?? [], 3, fk, KOORD)));
    case 'punkt2': return formZeile(f.label, h(zellen(wert ?? [], 2, fk, UV)));
    case 'punkte2': return formZeile(f.label, h(punktTabelle(wert ?? [], fk, 2, { woelbung: !f.ohneWoelbung, min: f.min, max: f.max })), { klasse: 'oben' });
    case 'punkte3': return formZeile(f.label, h(punktTabelle(wert ?? [], fk, 3, { min: f.min })), { klasse: 'oben' });
    // Herstellernetz: nur Anzeige (Ecken und Dreiecke kommen aus der Unterlage, nicht von Hand)
    case 'netzinfo': return formZeile(f.label, `<span class="fe-netzinfo">${Array.isArray(wert) ? wert.length : 0} Dreiecke (aus einer Herstellerunterlage)</span>`);
    case 'loecher': {
      const l = wert ?? [];
      return formZeile(f.label, h(`${l.map((ring, i) => `<div class="fe-loch" data-i="${i}"><div class="fe-loch-kopf">Loch ${i + 1} ${knopf('Loch entfernen', 'loch-weg', { klasse: 'klein' })}</div>${punktTabelle(ring, `${fk}.${i}`, 2, { woelbung: true, min: 2 })}</div>`).join('')}${knopf('＋ Loch', 'loch-neu', { klasse: 'klein', titel: 'Loch in der Kontur anfügen' })}`), { klasse: 'oben' });
    }
    case 'profil': {
      const art = profilArt(wert);
      const inner = `${auswahlFeld([['kreis', 'Kreis (Radius)'], ['rechteck', 'Rechteck (b × h)']], art, { fk: `${fk}.art`, klasse: 'fe-profilart' })}`
        + (art === 'kreis' ? eingabeFeld(feldText(wert?.kreis), { fk: `${fk}.r`, live: true, klasse: 'xyz', platzhalter: 'r', extra: { 'data-c': 0 } })
          : [0, 1].map((i) => eingabeFeld(feldText(wert?.rechteck?.[i]), { fk: `${fk}.rh${i}`, live: true, klasse: 'xyz', platzhalter: 'bh'[i], extra: { 'data-c': i } })).join(''));
      return formZeile(f.label, h(inner));
    }
    default: return '';
  }
}

/** Liest den Wert eines Feldes aus dem DOM: { wert } (undefined = entfernen) oder { fehler }. */
export function feldLesen(box) {
  const typ = box.dataset.ftyp, optional = box.dataset.optional === '1';
  const eing = [...box.querySelectorAll('input[type=text]:not(.fe-profilart)')];
  const text = (el) => el?.value ?? '';
  switch (typ) {
    case 'zahl': { const z = zahlOderFormel(text(eing[0])); return z === undefined ? (optional ? { wert: undefined } : { fehler: 'Wert fehlt' }) : { wert: z }; }
    case 'zahlnur': { const t = text(eing[0]).trim(); if (t === '') return { wert: undefined }; const z = zahlLesen(t); return z === null ? { fehler: 'Zahl erwartet' } : { wert: z }; }
    case 'text': return { wert: text(eing[0]) };
    case 'auswahl': return { wert: box.querySelector('select').value };
    case 'punkt3': { const r = punktAusTexten(eing.map(text), 3); return r.fehler ? { fehler: r.fehler } : { wert: r.punkt }; }
    case 'punkt2': { const r = punktAusTexten(eing.map(text), 2); return r.fehler ? { fehler: r.fehler } : { wert: r.punkt }; }
    case 'punkte2': return punktzeilenLesen(box, 2);
    case 'punkte3': return punktzeilenLesen(box, 3);
    case 'loecher': {
      const ringe = [];
      for (const l of box.querySelectorAll('.fe-loch')) { const r = punktzeilenLesen(l, 2); if (r.fehler) return r; ringe.push(r.wert); }
      return { wert: ringe.length ? ringe : undefined };
    }
    case 'profil': {
      const art = box.querySelector('.fe-profilart').value;
      if (art === 'kreis') { const z = zahlOderFormel(text(eing[0])); return z === undefined ? { fehler: 'Radius fehlt' } : { wert: { kreis: z } }; }
      const b = zahlOderFormel(text(eing[0])), h = zahlOderFormel(text(eing[1]));
      return b === undefined || h === undefined ? { fehler: 'b und h fehlen' } : { wert: { rechteck: [b, h] } };
    }
    default: return { fehler: 'unbekanntes Feld' };
  }
}

function punktzeilenLesen(box, spalten) {
  const aus = [];
  for (const tr of box.querySelectorAll('tr.fe-pz')) {
    const t = [...tr.querySelectorAll('input[type=text]')].map((e) => e.value);
    if (spalten === 3) { const r = punktAusTexten(t, 3); if (r.fehler) return { fehler: `Punkt ${aus.length + 1}: ${r.fehler}` }; aus.push(r.punkt); }
    else { const r = konturZeile(t); if (r.fehler) return { fehler: `Punkt ${aus.length + 1}: ${r.fehler}` }; aus.push(r.punkt); }
  }
  return { wert: aus };
}

/** Wert eines Feldes nach einem Knopf (Punkt anfügen/löschen, Loch anfügen/entfernen) – ohne DOM-Lesen, aus dem Objekt. */
function listeAendern(obj, f, el) {
  const a = el.dataset.aktion;
  const wert = klon(pfadLesen(obj, f.pfad)) ?? [];
  const spalten = f.typ === 'punkte3' ? 3 : 2;
  const loch = el.closest('.fe-loch');
  const ring = loch ? wert[Number(loch.dataset.i)] : wert;
  if (a === 'punkt-neu') {
    const letzter = ring.at(-1) ?? Array(spalten).fill(0);
    const neu = Array.from({ length: spalten }, (_, i) => { const v = letzter[i]; return typeof v === 'number' ? v + 10 : 0; });
    ring.push(neu);
  } else if (a === 'punkt-weg') {
    ring.splice(Number(el.closest('tr.fe-pz').dataset.i), 1);
  } else if (a === 'loch-neu') {
    wert.push([[0, 0], [10, 0], [10, 10], [0, 10]]);
  } else if (a === 'loch-weg') {
    wert.splice(Number(loch.dataset.i), 1);
  } else return undefined;
  return wert.length === 0 && f.typ === 'loecher' ? null : wert;
}

// ------------------------------------------------------------------------------------------------ Gemeinsames Gerüst (Liste + Formular)

function detailgradFeld(k, fk) {
  const aktiv = new Set(koerperDetailgrade(k));
  return formZeile('Detailgrad', `<div class="fe-feld" data-pfad="detailgrad" data-ftyp="detailgrad">${DETAILGRADE.map((g) => `<label class="fe-hk">${haken(aktiv.has(g), { fk: `${fk}.${g}`, extra: { 'data-g': g } })} ${g}</label>`).join('')}</div>`, { titel: 'In welchen Detailgraden der Teil erscheint' });
}

function detailgradLesen(box) {
  const an = [...box.querySelectorAll('input[type=checkbox]')].filter((c) => c.checked).map((c) => c.dataset.g);
  if (an.length === 0) return { fehler: 'Mindestens ein Detailgrad muss bleiben.' };
  return { wert: an.length === DETAILGRADE.length ? undefined : DETAILGRADE.filter((g) => an.includes(g)) };
}

// ================================================================================================ Körper

export const koerperTab = {
  id: 'koerper',
  titel: 'Körper',
  zeichne(ctx) {
    const ks = ctx.def.koerper ?? [];
    if (!ctx.auswahl.koerper || !ks.some((k) => k.id === ctx.auswahl.koerper)) ctx.auswahl.koerper = ks[0]?.id ?? null;
    const sel = ks.find((k) => k.id === ctx.auswahl.koerper);
    const zeilen = ks.map((k) => `<tr data-zeile="${esc(k.id)}" class="fe-liste-zeile${k.id === ctx.auswahl.koerper ? ' gewaehlt' : ''}">
      <td>${esc(KOERPER_ARTEN.find(([a]) => a === k.art)?.[1] ?? k.art)}</td><td>${esc(k.name ?? k.id)}</td><td class="mono" title="grob · mittel · fein">${esc(detailgradKurz(k))}</td></tr>`);
    const neuKnoepfe = KOERPER_ARTEN.map(([a, t]) => knopf(t, 'koerper-neu', { extra: { 'data-art': a }, titel: `${t} hinzufügen (Platzhaltermasse, danach anpassen)`, klasse: 'klein' })).join('');
    return `<div class="fe-werkzeuge"><span class="leise">Neu:</span>${neuKnoepfe}<span class="fe-trenner"></span>${knopf('Duplizieren', 'koerper-dup', { deaktiviert: !sel, klasse: 'klein' })}${knopf('Löschen', 'koerper-loeschen', { deaktiviert: !sel, klasse: 'klein' })}</div>`
      + `<div class="fe-zweispaltig"><div class="fe-liste">${ks.length ? tabelle(['Art', 'Name', 'g m f'], zeilen) : leerHinweis('Noch kein Körper. Oben eine Art wählen.')}</div>`
      + `<div class="fe-detail">${sel ? koerperFormular(sel) : leerHinweis('Einen Körper wählen.')}</div></div>`;
  },
  aenderung(el, ctx) {
    const k = (ctx.def.koerper ?? []).find((x) => x.id === ctx.auswahl.koerper);
    if (!k) return null;
    return eintragAenderung(el, k, koerperFelder(k.art), (neu) => [{ op: 'koerper_setzen', koerper: neu }]);
  },
  klick(el, ctx) {
    const a = el.dataset.aktion;
    const ks = ctx.def.koerper ?? [];
    const k = ks.find((x) => x.id === ctx.auswahl.koerper);
    if (a === 'koerper-neu') {
      const art = el.dataset.art;
      const id = freieKoerperId(ctx.def, art);
      ctx.auswahl.koerper = id;
      return { ops: [{ op: 'koerper_setzen', koerper: koerperNeu(art, id, id) }] };
    }
    if (a === 'koerper-dup' && k) {
      const id = freierName(`${k.id}_kopie`, ks.map((x) => x.id)).replace(/ /g, '_');
      ctx.auswahl.koerper = id;
      return { ops: [{ op: 'koerper_setzen', koerper: { ...klon(k), id, name: `${k.name ?? k.id} (Kopie)` } }] };
    }
    if (a === 'koerper-loeschen' && k) return { ops: [{ op: 'koerper_loeschen', id: k.id }] };
    if (a === 'punkt-neu' || a === 'punkt-weg' || a === 'loch-neu' || a === 'loch-weg') {
      const box = el.closest('.fe-feld');
      const f = koerperFelder(k?.art).find((x) => x.pfad === box?.dataset.pfad);
      if (!k || !f) return null;
      const neu = listeAendern(k, f, el);
      return neu === undefined ? null : { ops: [{ op: 'koerper_setzen', koerper: pfadSetzen(k, f.pfad, neu === null ? undefined : neu) }] };
    }
    return null;
  },
  zeileGewaehlt(el, ctx) {
    const id = el.closest('[data-zeile]')?.dataset.zeile;
    if (!id || id === ctx.auswahl.koerper) return false;
    ctx.auswahl.koerper = id;
    return true;
  },
};

function koerperFormular(k) {
  const fk = (p) => `k:${k.id}:${p}`;
  const kopf = formZeile('Name', `<div class="fe-feld" data-pfad="name" data-ftyp="text">${eingabeFeld(k.name ?? '', { fk: fk('name') })}</div>`)
    + formZeile('Id / Art', `<span class="mono">${esc(k.id)}</span> <span class="leise">· ${esc(KOERPER_ARTEN.find(([a]) => a === k.art)?.[1] ?? k.art)}</span>`)
    + formZeile('Material', `<div class="fe-feld" data-pfad="material" data-ftyp="auswahl">${auswahlFeld(MATERIALIEN, k.material ?? '', { fk: fk('material'), leer: '(Vorgabe)' })}</div>`, { titel: 'Darstellungsmaterial (Farbe in 3D)' })
    + detailgradFeld(k, fk('detail'))
    + formZeile('Sichtbar (Formel)', `<div class="fe-feld" data-pfad="sichtbar" data-ftyp="zahl" data-optional="1">${eingabeFeld(feldText(k.sichtbar), { fk: fk('sichtbar'), live: true, platzhalter: 'leer = immer sichtbar', titel: 'Formel (ja/nein), z. B. mit_abdeckung' })}</div>`, { titel: 'Formel, die den Körper ein- oder ausblendet' });
  return `<div class="fe-formular">${kopf}<hr class="fe-hr">${koerperFelder(k.art).map((f) => feldHtml(f, pfadLesen(k, f.pfad), fk(f.pfad))).join('')}</div>`;
}

/** Gemeinsame Auswertung einer Feldänderung (Körper und Sinnbild): liest das Feld, setzt es im Objekt und baut die ops. */
function eintragAenderung(el, obj, _felder, opsVon) {
  const box = el.closest('.fe-feld');
  if (!box) return null;
  const pfad = box.dataset.pfad, typ = box.dataset.ftyp;
  let neu;
  if (typ === 'profil' && el.classList.contains('fe-profilart')) {
    neu = pfadSetzen(obj, pfad, profilWechseln(el.value));
    return { ops: opsVon(neu) };
  }
  const r = typ === 'detailgrad' ? detailgradLesen(box) : feldLesen(box);
  if (r.fehler) return { fehler: `${pfad}: ${r.fehler}` };
  neu = pfadSetzen(obj, pfad, r.wert);
  // leere Texte und Auswahlen (Name, Material, Stift, Linienart, Füllung) entfernen das Feld
  if (r.wert === '' && (typ === 'auswahl' || typ === 'text')) neu = pfadSetzen(obj, pfad, undefined);
  return { ops: opsVon(neu) };
}

// ================================================================================================ Sinnbild

function sinnbildFormular(s, i) {
  const fk = (p) => `s:${i}:${p}`;
  const stilFeld = (p, optionen, label) => formZeile(label, `<div class="fe-feld" data-pfad="stil.${p}" data-ftyp="auswahl">${auswahlFeld(optionen.map(([v, t]) => [v, t]).filter(([v]) => v !== ''), pfadLesen(s, `stil.${p}`) ?? '', { fk: fk(`stil.${p}`), leer: optionen.find(([v]) => v === '')?.[1] ?? '' })}</div>`);
  return `<div class="fe-formular">${formZeile('Art', `<span>${esc(SINNBILD_ARTEN.find(([a]) => a === s.art)?.[1] ?? s.art)}</span>`)}`
    + detailgradFeld(s, fk('detail'))
    + formZeile('Sichtbar (Formel)', `<div class="fe-feld" data-pfad="sichtbar" data-ftyp="zahl" data-optional="1">${eingabeFeld(feldText(s.sichtbar), { fk: fk('sichtbar'), live: true, platzhalter: 'leer = immer sichtbar' })}</div>`)
    + stilFeld('stift', STIFTE, 'Stift') + stilFeld('linie', LINIENARTEN, 'Linienart')
    + formZeile('Füllung', `<div class="fe-feld" data-pfad="stil.fuellung" data-ftyp="text">${eingabeFeld(pfadLesen(s, 'stil.fuellung') ?? '', { fk: fk('stil.fuellung'), platzhalter: 'leer, schnitt, papier oder #rrggbb' })}</div>`)
    + `<hr class="fe-hr">${sinnbildFelder(s.art).map((f) => feldHtml(f, pfadLesen(s, f.pfad), fk(f.pfad))).join('')}</div>`;
}

function sinnbildVorschau(liste) {
  const prims = (liste ?? []).map(sinnbildPrimitive).filter(Boolean);
  if (prims.length === 0) return '';
  const pts = [];
  for (const p of prims) {
    for (const q of p.punkte ?? []) pts.push(q);
    if ((p.art === 'kreis' || p.art === 'bogen') && p.radius > 0) { const [x, y] = p.punkte[0]; pts.push([x - p.radius, y - p.radius], [x + p.radius, y + p.radius]); }
  }
  const minx = Math.min(...pts.map((p) => p[0])), maxx = Math.max(...pts.map((p) => p[0])), miny = Math.min(...pts.map((p) => p[1])), maxy = Math.max(...pts.map((p) => p[1]));
  const rand = Math.max(maxx - minx, maxy - miny, 10) * 0.1;
  const ctx = { massstab: 1, schraffuren: new Map(), muster: new Map() };
  const svg = prims.map((p) => primitivSvg(p, ctx)).join('');
  return `<svg class="gr-svg fe-sinnbild-svg" viewBox="${minx - rand} ${-(maxy + rand)} ${maxx - minx + 2 * rand} ${maxy - miny + 2 * rand}" preserveAspectRatio="xMidYMid meet" style="--m:1;--minpx:0.5px"><g transform="scale(1 -1)">${svg}</g></svg>`;
}

export const sinnbildTab = {
  id: 'sinnbild',
  titel: 'Sinnbild',
  zeichne(ctx) {
    const liste = ctx.def.sinnbild ?? [];
    if (ctx.auswahl.sinnbild === null || ctx.auswahl.sinnbild === undefined || ctx.auswahl.sinnbild >= liste.length) ctx.auswahl.sinnbild = liste.length ? 0 : null;
    const sel = ctx.auswahl.sinnbild === null ? null : liste[ctx.auswahl.sinnbild];
    const zeilen = liste.map((s, i) => `<tr data-zeile="${i}" class="fe-liste-zeile${i === ctx.auswahl.sinnbild ? ' gewaehlt' : ''}"><td>${i + 1}</td><td>${esc(SINNBILD_ARTEN.find(([a]) => a === s.art)?.[1] ?? s.art)}</td><td class="mono">${esc(detailgradKurz(s))}</td></tr>`);
    const neuKnoepfe = SINNBILD_ARTEN.map(([a, t]) => knopf(t, 'sinnbild-neu', { extra: { 'data-art': a }, klasse: 'klein', titel: `${t} hinzufügen` })).join('');
    return `<div class="fe-werkzeuge"><span class="leise">Neu:</span>${neuKnoepfe}<span class="fe-trenner"></span>${knopf('Löschen', 'sinnbild-loeschen', { deaktiviert: !sel, klasse: 'klein' })}${knopf('Sinnbild entfernen', 'sinnbild-alle-weg', { deaktiviert: liste.length === 0, klasse: 'klein', titel: 'Ohne Sinnbild zeichnet der Grundriss den Umriss der Körper von oben' })}</div>`
      + `<p class="leise fe-hinweis-zeile">Das Sinnbild ist die 2D-Darstellung im Grundriss (von oben, Familienkoordinaten). Ohne Sinnbild zeichnet der Grundriss den Umriss der Körper von oben.</p>`
      + `<div class="fe-zweispaltig"><div class="fe-liste">${liste.length ? tabelle(['#', 'Art', 'g m f'], zeilen) : leerHinweis('Kein Sinnbild: Umriss der Körper von oben.')}${sinnbildVorschau(liste)}</div>`
      + `<div class="fe-detail">${sel ? sinnbildFormular(sel, ctx.auswahl.sinnbild) : ''}</div></div>`;
  },
  aenderung(el, ctx) {
    const liste = ctx.def.sinnbild ?? [];
    const i = ctx.auswahl.sinnbild;
    const s = liste[i];
    if (!s) return null;
    return eintragAenderung(el, s, sinnbildFelder(s.art), (neu) => { const l = klon(liste); l[i] = neu; return [{ op: 'sinnbild_setzen', sinnbild: l }]; });
  },
  klick(el, ctx) {
    const a = el.dataset.aktion;
    const liste = klon(ctx.def.sinnbild ?? []);
    const i = ctx.auswahl.sinnbild;
    if (a === 'sinnbild-neu') { liste.push(sinnbildNeu(el.dataset.art)); ctx.auswahl.sinnbild = liste.length - 1; return { ops: [{ op: 'sinnbild_setzen', sinnbild: liste }] }; }
    if (a === 'sinnbild-loeschen' && liste[i]) { liste.splice(i, 1); return { ops: [{ op: 'sinnbild_setzen', sinnbild: liste.length ? liste : null }] }; }
    if (a === 'sinnbild-alle-weg') return { ops: [{ op: 'sinnbild_setzen', sinnbild: null }] };
    if (a === 'punkt-neu' || a === 'punkt-weg') {
      const s = liste[i];
      const box = el.closest('.fe-feld');
      const f = sinnbildFelder(s?.art).find((x) => x.pfad === box?.dataset.pfad);
      if (!s || !f) return null;
      const neu = listeAendern(s, f, el);
      if (neu === undefined) return null;
      liste[i] = pfadSetzen(s, f.pfad, neu);
      return { ops: [{ op: 'sinnbild_setzen', sinnbild: liste }] };
    }
    return null;
  },
  zeileGewaehlt(el, ctx) {
    const i = Number(el.closest('[data-zeile]')?.dataset.zeile);
    if (!Number.isInteger(i) || i === ctx.auswahl.sinnbild) return false;
    ctx.auswahl.sinnbild = i;
    return true;
  },
};

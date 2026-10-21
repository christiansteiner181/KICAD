// Plangrafik des Kerns → SVG-Text. Reine Funktionen ohne DOM (in Node testbar). Der Kern liefert die Primitive
// (Grafiktypen.cs: linie, polylinie, polygon mit Löchern, kreis, bogen, text, bild); hier werden sie nur gezeichnet.
//
// Koordinaten: Modell-mm der Ansicht (Grundriss x Ost / y Nord, Schnitt und Ansicht u / v = z). Das Markup liegt in einer
// Gruppe mit transform="scale(1 -1)", die y spiegelt – hier steht deshalb alles in Modellkoordinaten, y nach oben.
// Strichstärken und Schriftgrössen kommen in Papier-mm und werden mit dem Massstab in Modell-mm umgerechnet
// (massstabsgetreu). Farben nur über Klassen (pl = Planfarbe als Linie, pf = Planfarbe als Füllung, pp = Papier) oder
// #rrggbb-Attribute aus den Daten; die Klassen folgen dem Design (hell/dunkel) über CSS-Variablen in ansichten.css.
// Dort steht auch die Mindestbreite von einem Bildschirmpixel: stroke-width: max(--m × Stiftbreite, --minpx).

import { esc } from './hilfen.js';

/** Zeichenreihenfolge der Ebenen, unten → oben – Spiegel von Ebenen.Reihenfolge (Hlks.Kern/Grafik/Grafiktypen.cs). */
export const EBENEN = ['grundlage', 'raum', 'decke', 'huelle', 'wand', 'oeffnung', 'achse', 'leitung', 'formteil', 'armatur', 'apparat', 'sonst', 'fuellung', 'beschriftung', 'symbol'];

/** Rang einer Ebene; unbekannte Ebenen zählen wie «sonst». */
export function ebenenRang(ebene) {
  const i = EBENEN.indexOf(ebene);
  return i >= 0 ? i : EBENEN.indexOf('sonst');
}

/** Teile in Zeichenreihenfolge (stabil: innerhalb einer Ebene bleibt die Reihenfolge des Kerns). */
export function nachEbenen(teile) {
  return (teile ?? []).map((t, i) => ({ t, i, r: ebenenRang(t.ebene) })).sort((a, b) => a.r - b.r || a.i - b.i).map((x) => x.t);
}

/** Stiftbreiten in Papier-mm (Spiegel von Stifte.Mm, SIA 400:2000 B.3.1). */
export const STIFT_MM = Object.freeze({ duenn: 0.18, mittel: 0.35, dick: 0.70 });
export const stiftMm = (stift) => STIFT_MM[stift] ?? STIFT_MM.duenn;
/** Strichbreite in Modell-mm bei Massstab 1:massstab. */
export const strichbreite = (stift, massstab) => stiftMm(stift) * massstab;
const STIFT_KLASSE = { duenn: 'sd', mittel: 'sm', dick: 'sk' };

/** Strichmuster der Linienarten in Papier-mm (Strich, Lücke, …) – wie PdfZeichner.cs. */
export const LINIENMUSTER = Object.freeze({ strich: [3.0, 1.0], strichpunkt: [6.0, 1.0, 0.5, 1.0], punkt: [0.5, 1.0] });
/** stroke-dasharray in Modell-mm; null für durchgezogene Linien. */
export function dasharray(linie, massstab) {
  const m = LINIENMUSTER[linie];
  return m ? m.map((x) => zahl(x * massstab)).join(' ') : null;
}

/** Punktdurchmesser der Punktschraffur in Papier-mm (wie PdfZeichner.cs). */
const PUNKT_MM = 0.2;
const ZEILENABSTAND = 1.2;   // Vielfaches der Schrifthöhe (wie PdfZeichner.cs)

/** Zahl für Markup: höchstens zwei Nachkommastellen, nie NaN. */
export function zahl(v) {
  const n = Number(v);
  if (!Number.isFinite(n)) return '0';
  return String(Math.round(n * 100) / 100);
}

const gueltigerPunkt = (p) => Array.isArray(p) && Number.isFinite(p[0]) && Number.isFinite(p[1]);
const hex = (f) => (typeof f === 'string' && /^#[0-9a-fA-F]{3,8}$/.test(f) ? f : null);

// ---------------------------------------------------------------------------------------------- Pfade

const punktText = (p) => `${zahl(p[0])} ${zahl(p[1])}`;
const linienPfad = (punkte, schliessen = false) => 'M' + punkte.map(punktText).join('L') + (schliessen ? 'Z' : '');

/** Pfad eines Polygons mit Löchern (gerade-ungerade Füllregel). */
export function polygonPfad(p) {
  const ringe = [p.punkte, ...(p.loecher ?? [])].map((r) => (r ?? []).filter(gueltigerPunkt)).filter((r) => r.length >= 3);
  return ringe.map((r) => linienPfad(r, true)).join('');
}

/** Bogenpfad von «von» nach «bis» (Grad, Gegenuhrzeigersinn). Volle Kreise werden zu zwei Halbbögen. */
export function bogenPfad(mx, my, r, von, bis) {
  let sweep = (bis - von) % 360;
  if (sweep <= 0) sweep += 360;
  const pt = (w) => [mx + r * Math.cos(w * Math.PI / 180), my + r * Math.sin(w * Math.PI / 180)];
  const a = pt(von);
  if (sweep >= 359.999) {
    const m = pt(von + 180);
    return `M${punktText(a)}A${zahl(r)} ${zahl(r)} 0 1 1 ${punktText(m)}A${zahl(r)} ${zahl(r)} 0 1 1 ${punktText(a)}`;
  }
  const e = pt(von + sweep);
  // sweep-flag 1 = in Richtung wachsender Winkel; der y-gespiegelte Gruppenraum macht daraus Gegenuhrzeigersinn
  return `M${punktText(a)}A${zahl(r)} ${zahl(r)} 0 ${sweep > 180 ? 1 : 0} 1 ${punktText(e)}`;
}

// ---------------------------------------------------------------------------------------------- Stil

/** Linienattribute: Klasse (Stiftbreite, Planfarbe) und Attribute; null-Stift = keine Kontur. */
function kontur(stil, ctx) {
  if (!stil?.stift) return { klasse: '', attr: ' stroke="none"' };
  const f = hex(stil.farbe);
  const da = dasharray(stil.linie, ctx.massstab);
  // Deckkraft gilt der Füllung; ohne Füllung (Linien, Unterlagen aus DWG/IFC) gilt sie der Linie (wie im PDF, PdfZeichner.Stift)
  const d = stil.deckkraft;
  const deck = !stil.fuellung && Number.isFinite(d) && d < 1 ? ` stroke-opacity="${zahl(Math.max(0, d))}"` : '';
  return {
    klasse: `${STIFT_KLASSE[stil.stift] ?? 'sd'}${f ? '' : ' pl'}`,
    attr: ` stroke-width="${zahl(strichbreite(stil.stift, ctx.massstab))}"${f ? ` stroke="${f}"` : ''}${da ? ` stroke-dasharray="${da}"` : ''}${deck}`,
  };
}

/** Füllung: null = keine, «schnitt» = Planfarbe (oder Linienfarbe), «papier» = Hintergrund, #rrggbb. */
function fuellung(stil) {
  const d = stil?.deckkraft;
  const deck = Number.isFinite(d) && d < 1 ? ` fill-opacity="${zahl(Math.max(0, d))}"` : '';
  switch (stil?.fuellung) {
    case undefined: case null: case '': return { klasse: '', attr: ' fill="none"' };
    case 'schnitt': { const f = hex(stil.farbe); return f ? { klasse: '', attr: ` fill="${f}"${deck}` } : { klasse: 'pf', attr: deck }; }
    case 'papier': return { klasse: 'pp', attr: deck };
    default: { const f = hex(stil.fuellung); return f ? { klasse: '', attr: ` fill="${f}"${deck}` } : { klasse: '', attr: ' fill="none"' }; }
  }
}

const klassen = (...k) => { const t = k.filter(Boolean).join(' '); return t ? ` class="${t}"` : ''; };

// ---------------------------------------------------------------------------------------------- Schraffuren

/** Schraffuren aus /api/schraffuren in eine Map (Name klein → Definition). */
export function schraffurMap(liste) {
  return new Map((Array.isArray(liste) ? liste : []).map((s) => [String(s.name).toLowerCase(), s]));
}

/** Rundung der Musterlage auf 0.5 Modell-mm (bei 1:50 sind das 0.01 mm auf dem Papier): hält die Zahl der <pattern> klein und
 *  lässt Bauteile mit demselben Anker dasselbe Muster teilen. */
const LAGE_RASTER = 0.5;

/**
 * Lage des Musters (N8): Das Muster ist an die Achse des Bauteils gebunden. winkel = effektiver Linienwinkel (Winkel der
 * Schraffur + Stil.musterWinkel), anker = Stil.musterUrsprung (Punkt auf der Achse, Modell-mm) oder null. Ergibt { tx, ty }:
 * Verschiebung der Kachel im gedrehten Mustersystem, so dass eine Musterlinie durch den Anker läuft (bei Linien nur quer zur
 * Linie, ty; bei Punkten auch längs, tx), jeweils modulo Abstand d. Ohne Anker { 0, 0 }.
 */
export function musterLage(winkel, anker, d, art = 'linien') {
  if (!Array.isArray(anker) || !Number.isFinite(anker[0]) || !Number.isFinite(anker[1]) || !(d > 0)) return { tx: 0, ty: 0 };
  const r = winkel * Math.PI / 180, c = Math.cos(r), s = Math.sin(r);
  const lx = c * anker[0] + s * anker[1], ly = -s * anker[0] + c * anker[1];
  const mod = (v) => { const m = ((v % d) + d) % d; const q = Math.round(m / LAGE_RASTER) * LAGE_RASTER; return q >= d ? 0 : q; };
  return { tx: art === 'punkte' ? mod(lx) : 0, ty: mod(ly) };
}

/** Id und Markup einer Musterfläche (SVG-<pattern>) für einen Winkel; Abstand mm Papier × Massstab. Ohne Anker im Ursprung
 *  verankert wie im PDF; mit Anker (Stil.musterUrsprung) läuft eine Musterlinie bzw. das Punktraster durch den Anker. */
function musterMarkup(def, winkel, farbe, ctx, anker = null) {
  const d = def.abstandMm * ctx.massstab;
  const fKey = farbe ? farbe.slice(1).toLowerCase() : 'p';
  const { tx, ty } = musterLage(winkel, anker, d, def.art);
  const lage = tx || ty ? `-t${zahl(tx)}_${zahl(ty)}` : '';
  const id = `sch-${def.name}-${def.art === 'linien' || winkel ? zahl(winkel) : 'p'}-${fKey}-${ctx.massstab}${lage}`.replace(/[^A-Za-z0-9_-]/g, '_');
  if (ctx.muster.has(id)) return id;
  const kopf = `<pattern id="${id}" patternUnits="userSpaceOnUse" width="${zahl(d)}" height="${zahl(d)}"`;
  const dreh = `rotate(${zahl(winkel)})${lage ? ` translate(${zahl(tx)} ${zahl(ty)})` : ''}`;
  let inhalt;
  if (def.art === 'punkte') {
    const r = PUNKT_MM / 2 * ctx.massstab;
    const kreis = (x, y) => `<circle cx="${x}" cy="${y}" r="${zahl(r)}"/>`;
    const gedreht = winkel || lage ? ` patternTransform="${dreh}"` : '';
    inhalt = `${kopf}${gedreht}><g class="pk ${farbe ? '' : 'pf'}"${farbe ? ` fill="${farbe}"` : ''}>${kreis(0, 0)}${kreis(zahl(d), 0)}${kreis(0, zahl(d))}${kreis(zahl(d), zahl(d))}</g></pattern>`;
  } else {
    // Linienschar: Linien bei y = k·d, im Muster gedreht um den Winkel; Linien auf Kachelrand doppelt, damit die Mitte voll ist
    const w = zahl(strichbreite('duenn', ctx.massstab));
    inhalt = `${kopf} patternTransform="${dreh}"><path class="sd ${farbe ? '' : 'pl'}" fill="none" stroke-width="${w}"${farbe ? ` stroke="${farbe}"` : ''} d="M0 0H${zahl(d)}M0 ${zahl(d)}H${zahl(d)}"/></pattern>`;
  }
  ctx.muster.set(id, inhalt);
  return id;
}

/** Schraffur-Füllungen eines Polygons: Liste von Pfad-Elementen mit Musterfüllung; «voll» als Volltonfläche.
 *  Das Muster folgt der Achse des Bauteils (N8): Der Kern liefert stil.musterWinkel (Drehung auf das Blatt, Grad) und
 *  stil.musterUrsprung (Anker auf der Achse); hier wird nur angewendet. */
function schraffuren(p, d, ctx) {
  const stil = p.stil ?? {};
  const def = stil.muster ? ctx.schraffuren?.get(String(stil.muster).toLowerCase()) : null;
  if (!def) return '';
  const f = hex(stil.farbe);
  if (def.art === 'voll') return `<path${klassen(f ? '' : 'pf', 'ph')} d="${d}" fill-rule="evenodd"${f ? ` fill="${f}"` : ''} stroke="none"/>`;
  if (!(def.abstandMm > 0)) return '';
  const dreh = Number.isFinite(stil.musterWinkel) ? stil.musterWinkel : 0;
  const anker = Array.isArray(stil.musterUrsprung) ? stil.musterUrsprung : null;
  const winkel = def.art === 'linien' ? (def.winkel?.length ? def.winkel : [0]) : [0];
  // Linienscharen sind nach 180° deckungsgleich, das Punktraster nach 90°: gleiche Lage, gleiches <pattern>
  const periode = def.art === 'punkte' ? 90 : 180;
  const eff = (w) => { const v = Math.round(((w + dreh) % periode + periode) % periode * 1000) / 1000; return v >= periode ? 0 : v; };
  return winkel.map((w) => `<path class="ph" d="${d}" fill-rule="evenodd" fill="url(#${musterMarkup(def, eff(w), f, ctx, anker)})" stroke="none"/>`).join('');
}

// ---------------------------------------------------------------------------------------------- Primitive

/** SVG-Text eines Primitivs. ctx = { massstab, schraffuren: Map, muster: Map }. Ungültiges ergibt ''. */
export function primitivSvg(p, ctx) {
  if (!p) return '';
  const stil = p.stil ?? {};
  switch (p.art) {
    case 'linie': case 'polylinie': {
      const pts = (p.punkte ?? []).filter(gueltigerPunkt);
      if (pts.length < 2 || !stil.stift) return '';
      const k = kontur(stil, ctx);
      return `<path${klassen(k.klasse)} d="${linienPfad(pts)}" fill="none"${k.attr}/>`;
    }
    case 'polygon': {
      const d = polygonPfad(p);
      if (!d) return '';
      const k = kontur(stil, ctx), f = fuellung(stil);
      // Bildschirmfüllung (Raumfarben): im dunklen Design gedämpft; die Hülle (Ebene huelle) nicht – sie soll kräftig bleiben
      if (stil.nurBildschirm && stil.fuellung && !ctx.ohneDaempfung) f.klasse = [f.klasse, 'bf'].filter(Boolean).join(' ');
      const muster = schraffuren(p, d, ctx);
      const eigen = `d="${d}" fill-rule="evenodd"`;
      if (!muster) return `<path${klassen(f.klasse, k.klasse)} ${eigen}${f.attr}${k.attr}/>`;
      // Reihenfolge wie im PDF: Füllung, Schraffur, Kontur
      const flaeche = stil.fuellung ? `<path${klassen(f.klasse)} ${eigen}${f.attr} stroke="none"/>` : '';
      const rand = stil.stift ? `<path${klassen(k.klasse)} ${eigen} fill="none"${k.attr}/>` : '';
      return flaeche + muster + rand;
    }
    case 'kreis': {
      if (!gueltigerPunkt(p.punkte?.[0]) || !(p.radius > 0)) return '';
      const k = kontur(stil, ctx), f = fuellung(stil);
      return `<circle${klassen(f.klasse, k.klasse)} cx="${zahl(p.punkte[0][0])}" cy="${zahl(p.punkte[0][1])}" r="${zahl(p.radius)}"${f.attr}${k.attr}/>`;
    }
    case 'bogen': {
      if (!gueltigerPunkt(p.punkte?.[0]) || !(p.radius > 0) || !Number.isFinite(p.von) || !Number.isFinite(p.bis) || !stil.stift) return '';
      const k = kontur(stil, ctx);
      return `<path${klassen(k.klasse)} d="${bogenPfad(p.punkte[0][0], p.punkte[0][1], p.radius, p.von, p.bis)}" fill="none"${k.attr}/>`;
    }
    case 'text': return textSvg(p, ctx);
    case 'bild': return bildSvg(p);
    default: return '';
  }
}

// ---------------------------------------------------------------------------------------------- Bild (Unterlagen PDF/Bild)

/** Zahl mit bis zu sechs Nachkommastellen (für Matrixwerte; «zahl» rundet auf 0.01 und wäre für Bruchteile zu grob). */
const zahl6 = (v) => String(Math.round(Number(v) * 1e6) / 1e6);

/**
 * Affine Abbildung eines Bildes der Grösse 1 × 1 (links oben = Ursprung, y nach unten) auf die vier Ecken im Modell
 * [unten links, unten rechts, oben rechts, oben links]: Matrix [a, b, c, d, e, f] für SVG transform="matrix(…)".
 * Aus drei Ecken (oben links, oben rechts, unten links); die vierte folgt bei Ähnlichkeitsabbildungen von selbst.
 * null bei ungültigen oder entarteten Ecken. Die Matrix gilt im Modellraum (y nach oben); der y-gespiegelte Gruppenraum der
 * Ansicht stellt das Bild damit wieder aufrecht.
 */
export function bildMatrix(punkte) {
  if (!Array.isArray(punkte) || punkte.length < 4 || !punkte.slice(0, 4).every(gueltigerPunkt)) return null;
  const [ul, , or, ol] = punkte;   // unten links, oben rechts, oben links
  const m = [or[0] - ol[0], or[1] - ol[1], ul[0] - ol[0], ul[1] - ol[1], ol[0], ol[1]];
  if (Math.abs(m[0] * m[3] - m[1] * m[2]) < 1e-9) return null;
  return m;
}

/** URL der Bilddatei einer Unterlage. */
export const bildUrl = (quelle) => `/api/unterlagen/${encodeURIComponent(quelle)}/bild`;

/** Bild einer Unterlage: <image> mit Matrix auf die vier Ecken, Deckkraft aus dem Stil; nie anklickbar (pointer-events none). */
function bildSvg(p) {
  const m = bildMatrix(p.punkte);
  if (!m || !p.quelle) return '';
  const d = p.stil?.deckkraft;
  const deck = Number.isFinite(d) && d < 1 ? ` opacity="${zahl6(Math.max(0, d))}"` : '';
  return `<image class="bi" pointer-events="none" href="${esc(bildUrl(p.quelle))}" x="0" y="0" width="1" height="1" preserveAspectRatio="none" transform="matrix(${m.map(zahl6).join(' ')})"${deck}/>`;
}

const ANKER = { links: 'start', mitte: 'middle', rechts: 'end' };

/** Text: Schrifthöhe in Papier-mm × Massstab, Ausrichtung → text-anchor, Winkel gegen den Uhrzeigersinn, y-Spiegelung aufgehoben. */
function textSvg(p, ctx) {
  if (!gueltigerPunkt(p.punkte?.[0]) || !p.text) return '';
  const groesse = (Number.isFinite(p.groesse) && p.groesse > 0 ? p.groesse : 2.5) * ctx.massstab;
  const f = hex(p.stil?.farbe);
  const winkel = Number.isFinite(p.winkel) && p.winkel !== 0 ? ` rotate(${zahl(-p.winkel)})` : '';
  const zeilen = String(p.text).replace(/\r/g, '').split('\n');
  const inhalt = zeilen.length === 1 ? esc(zeilen[0])
    : zeilen.map((z, i) => `<tspan x="0" dy="${i === 0 ? 0 : zahl(groesse * ZEILENABSTAND)}">${esc(z)}</tspan>`).join('');
  return `<text${klassen('pt', f ? '' : 'pf')} transform="translate(${punktText(p.punkte[0])}) scale(1 -1)${winkel}" font-size="${zahl(groesse)}" text-anchor="${ANKER[p.ausrichtung] ?? 'start'}"${p.fett ? ' font-weight="bold"' : ''}${f ? ` fill="${f}"` : ''}>${inhalt}</text>`;
}

/** Hover-/Auswahl-/Trefferflächen eines Teils: je Kontur ein unsichtbarer, bildschirmfester Strich (CSS .th). */
function trefferSvg(teil) {
  const gesehen = new Set();
  let aus = '';
  for (const p of teil.primitive ?? []) {
    let el = '';
    if (p.art === 'linie' || p.art === 'polylinie') {
      const pts = (p.punkte ?? []).filter(gueltigerPunkt);
      if (pts.length >= 2) el = `<path class="th" d="${linienPfad(pts)}"/>`;
    } else if (p.art === 'polygon') {
      const d = polygonPfad(p);
      if (d) el = `<path class="th fl" d="${d}" fill-rule="evenodd"/>`;
    } else if (p.art === 'kreis' && gueltigerPunkt(p.punkte?.[0]) && p.radius > 0) {
      el = `<circle class="th fl" cx="${zahl(p.punkte[0][0])}" cy="${zahl(p.punkte[0][1])}" r="${zahl(p.radius)}"/>`;
    } else if (p.art === 'bogen' && gueltigerPunkt(p.punkte?.[0]) && p.radius > 0 && Number.isFinite(p.von) && Number.isFinite(p.bis)) {
      el = `<path class="th" d="${bogenPfad(p.punkte[0][0], p.punkte[0][1], p.radius, p.von, p.bis)}"/>`;
    }
    if (el && !gesehen.has(el)) { gesehen.add(el); aus += el; }
  }
  return aus;
}

/** SVG eines Teils: <g data-id> (anklickbar) bzw. <g class="e-ohne"> ohne Id (nicht anklickbar). */
export function teilSvg(teil, ctx) {
  const id = teil.id ?? null;
  const ebene = String(teil.ebene ?? 'sonst').replace(/[^A-Za-z0-9_-]/g, '');
  const c = ebene === 'huelle' ? { ...ctx, ohneDaempfung: true } : ctx;   // gleiche Muster-Map, nur die Dämpfung der Bildschirmfüllung entfällt
  const inhalt = (teil.primitive ?? []).map((p) => primitivSvg(p, c)).join('');
  if (!inhalt) return '';
  // Rasterbilder (PDF/Bild-Unterlagen) sind nie anklickbar: wie ein Teil ohne Treffer, die Id bleibt am Knoten
  if (id && (teil.primitive ?? []).every((p) => p.art === 'bild')) return `<g class="e-ohne e-${ebene}" data-id="${esc(id)}">${inhalt}</g>`;
  return `<g class="${id ? 'el' : 'e-ohne'} e-${ebene}"${id ? ` data-id="${esc(id)}"` : ''}>${inhalt}${id ? trefferSvg(teil) : ''}</g>`;
}

/** Die ganze Plangrafik: <defs> mit den benutzten Mustern, dann die Teile in Ebenenreihenfolge. */
export function planSvg(plan, schraffurenMap = new Map()) {
  const ctx = { massstab: Number(plan?.massstab) > 0 ? Number(plan.massstab) : 100, schraffuren: schraffurenMap, muster: new Map() };
  const teile = nachEbenen(plan?.teile).map((t) => teilSvg(t, ctx)).join('');
  return `<defs>${[...ctx.muster.values()].join('')}</defs>${teile}`;
}

// ---------------------------------------------------------------------------------------------- Fangen und Ausschnitt

/** Fangdaten für die Werkzeuge: { punkte: [{ p: [x, y], id }], kanten: [{ a, b, id }] } aus Fangpunkten/-kanten der Teile. */
export function fangdatenVon(plan) {
  const punkte = [], kanten = [];
  for (const t of plan?.teile ?? []) {
    const id = t.id ?? null;
    for (const p of t.fangpunkte ?? []) if (gueltigerPunkt(p)) punkte.push({ p: [p[0], p[1]], id });
    for (const k of t.fangkanten ?? []) if (Array.isArray(k) && gueltigerPunkt(k[0]) && gueltigerPunkt(k[1])) kanten.push({ a: [k[0][0], k[0][1]], b: [k[1][0], k[1][1]], id });
  }
  return { punkte, kanten };
}

/** Begrenzung {minx, miny, maxx, maxy} der Plangrafik; aus plan.begrenzung, sonst aus den Punkten; null wenn leer. */
export function begrenzungVon(plan) {
  const b = plan?.begrenzung;
  if (Array.isArray(b) && b.length === 4 && b.every(Number.isFinite)) return { minx: b[0], miny: b[1], maxx: b[2], maxy: b[3] };
  let r = null;
  const nimm = (p) => {
    if (!gueltigerPunkt(p)) return;
    r = r ? { minx: Math.min(r.minx, p[0]), miny: Math.min(r.miny, p[1]), maxx: Math.max(r.maxx, p[0]), maxy: Math.max(r.maxy, p[1]) } : { minx: p[0], miny: p[1], maxx: p[0], maxy: p[1] };
  };
  for (const t of plan?.teile ?? []) for (const p of t.primitive ?? []) {
    if (p.art === 'text') continue;
    for (const q of p.punkte ?? []) nimm(q);
    if ((p.art === 'kreis' || p.art === 'bogen') && gueltigerPunkt(p.punkte?.[0]) && p.radius > 0) {
      const [x, y] = p.punkte[0];
      nimm([x - p.radius, y - p.radius]); nimm([x + p.radius, y + p.radius]);
    }
  }
  return r;
}

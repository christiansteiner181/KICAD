// Leitungen zeichnen (Modul 3, C1) – reine Logik ohne Browser, in Node getestet (leitung.test.mjs).
// Die Oberfläche rechnet nichts Fachliches: Formteile, Einbaulängen, Winkel der Serie und Verbindungen entscheidet der Kern
// (leitung_zeichnen, leitung_abzweigen …). Hier steht nur, was zum Zeichnen gehört: Winkelfang nach den erlaubten Winkeln der
// Serie (Eingabehilfe), Höhenwechsel (senkrechter Punkt), Lage der Anschlüsse als Fangpunkte, Aufbau der Befehlsparameter
// (von/nach, Höhe, System/Strang), Zustand der Optionsleiste und Texte der Vorschau-Zeile aus den Hinweisen des Kerns.
// Annahmen (Bedienvorgaben, keine Norm): Fangbereich des Winkelfangs 3°; ohne Winkelliste der Serie fängt der Winkelfang nur
// Achsen, ±45° und ±90° zur letzten Geraden.

import { abstand, winkelGrad, rund } from './fangen.js';
import { WerkzeugFehler } from './parameter.js';

export const LEITUNGSKLASSEN = ['Rohr', 'Kanal'];
export const FORMTEILKLASSEN = ['Bogen', 'TStueck', 'Reduktion', 'Reinigungsoeffnung'];
export const WINKELFANG_GRAD = 3;            // Annahme: Fangbereich des Winkelfangs
export const ABZWEIG_WINKEL = 90;            // das T-Stück zweigt rechtwinklig ab (Kern: ±0.5°)
export const STANDARD_WINKEL_FREI = [45, 90];

export const istLeitungsklasse = (klasse) => LEITUNGSKLASSEN.includes(klasse);
export const istFormteilklasse = (klasse) => FORMTEILKLASSEN.includes(klasse);
/** Leitung mit Serie = Familienelement (bezuege.familie); ohne Serie = Altbestand (Polylinie, z. B. aus IFC). */
export const hatSerie = (el) => !!el?.bezuege?.familie;
export const istAltbestand = (el) => istLeitungsklasse(el?.klasse) && !hatSerie(el);
export const kategorieVon = (klasse) => (klasse === 'Kanal' ? 'Kanal' : 'Rohr');

const zahlOK = (x) => typeof x === 'number' && Number.isFinite(x);
const gerundet = (p) => p.map((x) => rund(x, 2));

// ------------------------------------------------------------------------------------------------ Winkel

export const normWinkel = (w) => { const n = ((w % 360) + 360) % 360; return n >= 360 - 1e-9 ? 0 : n; };
/** Kleinster Unterschied zweier Richtungen in Grad (0 … 180). */
export function winkelDiff(a, b) {
  const d = Math.abs(normWinkel(a) - normWinkel(b));
  return d > 180 ? 360 - d : d;
}

/** Erlaubte Bogenwinkel der Serie (Antwort leitungsserie): aufsteigende Liste oder null (= der Kern lässt jeden Winkel zu). */
export function erlaubteWinkel(serie) {
  if (!serie || serie.winkelRegel !== 'liste' || !Array.isArray(serie.winkel)) return null;
  const l = serie.winkel.map(Number).filter((w) => Number.isFinite(w) && w > 0 && w < 180);
  return l.length ? [...new Set(l)].sort((a, b) => a - b) : null;
}

/**
 * Richtungen (Grad, 0 = Ost, gegen den Uhrzeigersinn), auf die der Winkelfang im Grundriss einrastet.
 *   vorher    Richtung der letzten waagrechten Geraden (null: keine, z. B. am Anfang oder nach einem senkrechten Stück)
 *   winkel    erlaubte Bogenwinkel der Serie (null: frei)
 *   erster    Richtung der ersten Geraden an einem Anschluss (waagrechte Achse des Anschlusses)
 *   nur       nur diese Richtungen (z. B. Abzweig: rechtwinklig zur Leitung)
 */
export function richtungsKandidaten({ vorher = null, winkel = null, erster = null, nur = null } = {}) {
  if (nur) return [...new Set(nur.map((w) => rund(normWinkel(w), 6)))];
  const s = new Set();
  const add = (w) => s.add(rund(normWinkel(w), 6));
  if (erster !== null) { add(erster); return [...s]; }          // am Anschluss gilt nur seine Achse
  for (const a of [0, 90, 180, 270]) add(a);
  if (vorher !== null) {
    for (const w of winkel ?? STANDARD_WINKEL_FREI) { add(vorher + w); add(vorher - w); }
    s.delete(rund(normWinkel(vorher + 180), 6));                 // Kehrtwende gibt es nicht
  }
  return [...s].sort((a, b) => a - b);
}

/**
 * Winkelfang: rastet die Richtung letzter → maus auf den nächsten Kandidaten ein (innerhalb toleranz), die Länge bleibt
 * (auf das Raster gerundet, wenn raster > 0). Rückgabe { punkt, winkel, laenge, abweichung } oder null (nichts in Reichweite).
 */
export function winkelFang({ letzter, maus, kandidaten, toleranz = WINKELFANG_GRAD, raster = 0 }) {
  const l = abstand(letzter, maus);
  if (l < 1e-6 || !kandidaten?.length) return null;
  const w = winkelGrad(letzter, maus);
  let best = null;
  for (const k of kandidaten) {
    const d = winkelDiff(w, k);
    if (d <= toleranz && (!best || d < best.d)) best = { k, d };
  }
  if (!best) return null;
  const laenge = raster > 0 ? Math.max(raster, Math.round(l / raster) * raster) : l;
  const r = best.k * Math.PI / 180;
  return { punkt: [letzter[0] + Math.cos(r) * laenge, letzter[1] + Math.sin(r) * laenge], winkel: best.k, laenge, abweichung: best.d };
}

/** Richtung (Grad) der letzten waagrechten Geraden einer Punktfolge (x, y, z); null, wenn es keine gibt (senkrecht oder zu kurz). */
export function letzteRichtung(punkte) {
  for (let i = punkte.length - 1; i >= 1; i--) {
    const a = punkte[i - 1], b = punkte[i];
    if (abstand([a[0], a[1]], [b[0], b[1]]) > 1) return winkelGrad([a[0], a[1]], [b[0], b[1]]);
    if (Math.abs((b[2] ?? 0) - (a[2] ?? 0)) > 0.5) return null;   // senkrechtes Stück: keine Richtung mehr
  }
  return null;
}

// ------------------------------------------------------------------------------------------------ Höhe

/** Höhe der Achse in absoluten mm: Kote des Geschosses + Höhe über OK Geschoss. */
export const hoeheAbsolut = (kote, hoehe) => (zahlOK(hoehe) ? (zahlOK(kote) ? kote : 0) + hoehe : null);
/** Höhe über OK Geschoss aus einer absoluten Höhe. */
export const hoeheRelativ = (kote, z) => rund(z - (zahlOK(kote) ? kote : 0), 3);

/**
 * Höhenwechsel (Steigleitung): hängt einen Punkt senkrecht über bzw. unter den letzten (gleiche x, y) auf die neue Höhe.
 * Rückgabe { punkte (neue Liste), angehaengt }. Ohne Punkte oder bei gleicher Höhe (±0.5 mm) bleibt alles.
 */
export function hoehenWechsel(punkte, zNeu) {
  if (!punkte.length || !zahlOK(zNeu)) return { punkte, angehaengt: false };
  const l = punkte[punkte.length - 1];
  if (Math.abs((l[2] ?? 0) - zNeu) <= 0.5) return { punkte, angehaengt: false };
  return { punkte: [...punkte, [l[0], l[1], zNeu]], angehaengt: true };
}

// ------------------------------------------------------------------------------------------------ Anschlüsse

/** Anschlüsse mit Weltlage aus der Antwort der Abfrage anschlusslagen (ohne Lage = nur logisch verbindbar → nicht fangbar). */
export function anschluesseAus(antwort) {
  if (!antwort?.anschluesse) return [];
  return antwort.anschluesse.filter((a) => Array.isArray(a.lage) && a.lage.length >= 3 && a.lage.every(zahlOK)).map((a) => ({
    element: antwort.element, name: a.name, medium: a.medium ?? null, fluss: a.fluss ?? null,
    dn: a.dn ?? null, d: a.d ?? null, b: a.b ?? null, h: a.h ?? null,
    lage: a.lage.slice(0, 3), richtung: Array.isArray(a.richtung) ? a.richtung.slice(0, 3) : null, verbunden: a.verbunden ?? null,
    p: [a.lage[0], a.lage[1]],
  }));
}

export function anschlussGroesse(a) {
  if (a?.dn != null) return `DN ${a.dn}`;
  if (a?.b != null) return `${a.b}×${a.h ?? '?'}`;
  if (a?.d != null) return `Ø${a.d}`;
  return '–';
}

/** «HK-1.vl · HZ-VL · rein · DN 15 · frei» bzw. «… · belegt (RO-3.anfang)». */
export function anschlussText(a) {
  return [`${a.element}.${a.name}`, a.medium, a.fluss, anschlussGroesse(a), a.verbunden ? `belegt (${a.verbunden})` : 'frei'].filter(Boolean).join(' · ');
}

export const anschlussFrei = (a) => !a.verbunden;

/** Anschluss als Ende eines Befehls: { element, anschluss } (Kennzeichen, nie die Textform «KZ.name»). */
export const anschlussEnde = (a) => ({ element: a.element, anschluss: a.name });

/** Richtung des waagrechten Anteils einer Anschlussachse in Grad (0 = Ost) oder null (senkrecht oder keine Richtung). */
export function anschlussRichtungPlan(a) {
  const r = a?.richtung;
  if (!r || Math.hypot(r[0], r[1]) < 0.05) return null;
  return normWinkel(Math.atan2(r[1], r[0]) * 180 / Math.PI);
}

/** Alle Anschlüsse, deren Lage im Grundriss höchstens radius mm von p liegt (nächster zuerst). */
export function anschluesseNahe(liste, p, radius) {
  return liste.map((a) => ({ a, d: abstand(a.p, p) })).filter((x) => x.d <= radius).sort((x, y) => x.d - y.d).map((x) => x.a);
}

/** Fangpunkte für fangen.js (extraPunkte): { p, art: 'anschluss', daten }. */
export const anschlussFangpunkte = (liste) => liste.map((a) => ({ p: a.p, art: 'anschluss', daten: a }));

/**
 * Reihenfolge nach der Fliessrichtung: Der Kern verlangt den Verlauf in Fliessrichtung. Beginnt die Zeichnung an einem Anschluss
 * mit Fluss «rein» (nimmt auf) oder endet sie an einem mit «raus» (speist), wird sie umgedreht, wenn das alles in Ordnung bringt.
 * Rückgabe { punkte, von, nach, umgedreht }.
 */
export function flussRichtung({ punkte, von = null, nach = null }) {
  const startOk = (a) => !a || a.fluss !== 'rein';
  const endeOk = (a) => !a || a.fluss !== 'raus';
  const normal = startOk(von) && endeOk(nach);
  const umgekehrt = startOk(nach) && endeOk(von);
  if (!normal && umgekehrt) return { punkte: [...punkte].reverse(), von: nach, nach: von, umgedreht: true };
  return { punkte, von, nach, umgedreht: false };
}

// ------------------------------------------------------------------------------------------------ Abzweig (bestehende Gerade)

/**
 * Fusspunkt von p (x, y) auf der Geraden a–b (x, y, z): { t, punkt [x, y, z], abstand (Grundriss), laenge, senkrecht }.
 * Senkrecht (Grundriss-Länge ≤ 1 mm): kein Fusspunkt im Grundriss, punkt = [x, y, null].
 */
export function projektionAufGerade(p, a, b) {
  const dx = b[0] - a[0], dy = b[1] - a[1], dz = (b[2] ?? 0) - (a[2] ?? 0);
  const lxy2 = dx * dx + dy * dy;
  const laenge = Math.hypot(dx, dy, dz);
  if (Math.sqrt(lxy2) <= 1) return { t: null, punkt: [a[0], a[1], null], abstand: abstand(p, a), laenge, senkrecht: true };
  const t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / lxy2;
  const q = [a[0] + t * dx, a[1] + t * dy, (a[2] ?? 0) + t * dz];
  return { t, punkt: q, abstand: abstand(p, q), laenge, senkrecht: false };
}

/**
 * Die Gerade (Rohr bzw. Kanal mit Serie, zwei Punkte) unter dem Mauspunkt: { element, projektion } oder null.
 * leitungen: Darstellungen aus dem Zustand; radius in mm (Grundriss); Fusspunkt mindestens 1 mm von den Enden.
 */
export function leitungUnterMaus(maus, leitungen, radius, klasse) {
  let best = null;
  for (const el of leitungen) {
    if (el.klasse !== klasse || !hatSerie(el)) continue;
    const pts = el.geometrie?.punkte;
    if (!pts || pts.length !== 2) continue;
    const pr = projektionAufGerade(maus, pts[0], pts[1]);
    if (pr.senkrecht) {
      if (pr.abstand <= radius && (!best || pr.abstand < best.projektion.abstand)) best = { element: el, projektion: pr };
      continue;
    }
    const lxy = abstand(pts[0], pts[1]);
    if (pr.t * lxy <= 1 || (1 - pr.t) * lxy <= 1) continue;
    if (pr.abstand <= radius && (!best || pr.abstand < best.projektion.abstand)) best = { element: el, projektion: pr };
  }
  return best;
}

/** Richtung der Leitung im Grundriss in Grad (0 = Ost) oder null (senkrecht). */
export function leitungRichtungPlan(el) {
  const pts = el?.geometrie?.punkte;
  if (!pts || pts.length < 2 || abstand(pts[0], pts[pts.length - 1]) <= 1) return null;
  return winkelGrad(pts[0], pts[pts.length - 1]);
}

/** Rechtwinklige Richtungen zur Leitung im Grundriss (beide Seiten). */
export const abzweigRichtungen = (leitungsWinkel) => (leitungsWinkel === null ? [] : [normWinkel(leitungsWinkel + ABZWEIG_WINKEL), normWinkel(leitungsWinkel - ABZWEIG_WINKEL)]);

/** Einheitsvektor [dx, dy, 0] der rechtwinkligen Richtung auf der Seite, auf der die Maus liegt (nur das T-Stück). */
export function abzweigSeite(leitungsWinkel, durchPunkt, maus) {
  const r = (leitungsWinkel + 90) * Math.PI / 180;
  const n = [Math.cos(r), Math.sin(r)];
  const seite = (maus[0] - durchPunkt[0]) * n[0] + (maus[1] - durchPunkt[1]) * n[1] >= 0 ? 1 : -1;
  return [rund(n[0] * seite, 6) + 0, rund(n[1] * seite, 6) + 0, 0];   // «+ 0» macht aus −0 eine 0
}

// ------------------------------------------------------------------------------------------------ Optionsleiste

/** Serien der Kategorie (Antwort familien): gültig, mit Typen. [{ kz, name, typen }] nach Name. */
export function serienDerKategorie(familien, kategorie) {
  return (familien ?? []).filter((f) => f.kategorie === kategorie && f.gueltig !== false && (f.typen ?? []).length > 0)
    .map((f) => ({ kz: f.kennzeichen, name: f.name, typen: f.typen }))
    .sort((a, b) => String(a.name).localeCompare(String(b.name), 'de'));
}

/** Gültige Grössen einer Serie (Antwort leitungsserie): [{ typ, aussen, innen, wand, breite, hoehe }]. */
export function groessenListe(serie) {
  return (serie?.groessen ?? []).filter((g) => g.gueltig !== false).map((g) => ({
    typ: g.typ, aussen: g.aussen ?? null, innen: g.innen ?? null, wand: g.wand ?? null, dn: g.dn ?? null, b: g.b ?? null, h: g.h ?? null, d: g.d ?? null,
  }));
}

/** Breite der Vorschau (mm): Aussenmass der gewählten Grösse (Rechteckkanal: Breite); null, wenn unbekannt. */
export function vorschauBreite(serie, typ) {
  const g = groessenListe(serie).find((x) => x.typ === typ);
  return g ? (g.aussen ?? g.b ?? g.d ?? null) : null;
}

/**
 * Zustand der Optionsleiste und nötige Korrekturen der gemerkten Werte.
 *   eingabe  { serien (serienDerKategorie), serie (gewählter Wert), serieDef (leitungsserie der gewählten Serie oder null),
 *              groesse, systemKz, strangKz, systeme [{kz, name}], straenge [{kz, name, system}] }
 * Rückgabe { keineSerie, serieOptionen [[kz, text]], serie, groessenOptionen [[typ, typ]], groesse, strangOptionen, strang, system, korrektur }.
 * `korrektur` sagt, welche Werte nicht mehr stimmen (Serie weg, Grösse nicht in der Serie, Strang gehört zu einem anderen System).
 */
export function optionenZustand({ serien, serie = '', serieDef = null, groesse = '', systemKz = '', strangKz = '', straenge = [] }) {
  const korrektur = {};
  const serieOptionen = serien.map((s) => [s.kz, s.name]);
  let serieWert = serie;
  if (serieWert && !serien.some((s) => s.kz === serieWert)) { serieWert = serien[0]?.kz ?? ''; korrektur.serie = serieWert; }
  if (!serieWert && serien.length === 1) { serieWert = serien[0].kz; korrektur.serie = serieWert; }
  const typen = serieDef && serieDef.familie === serieWert ? groessenListe(serieDef).map((g) => g.typ) : (serien.find((s) => s.kz === serieWert)?.typen ?? []);
  let groesseWert = groesse;
  if (groesseWert && typen.length && !typen.includes(groesseWert)) { groesseWert = ''; korrektur.groesse = ''; }
  let strangWert = strangKz;
  const strang = straenge.find((s) => s.kz === strangWert);
  if (strangWert && !strang) { strangWert = ''; korrektur.strang = ''; }
  else if (strang && systemKz && strang.system && strang.system !== systemKz) { strangWert = ''; korrektur.strang = ''; }
  const strangOptionen = straenge.filter((s) => !systemKz || !s.system || s.system === systemKz).map((s) => [s.kz, s.system ? `${s.kz} (${s.system})` : s.kz]);
  return {
    keineSerie: serien.length === 0,
    serieOptionen, serie: serieWert,
    groessenOptionen: typen.map((t) => [t, t]), groesse: groesseWert,
    strangOptionen, strang: strangWert,
    korrektur,
  };
}

// ------------------------------------------------------------------------------------------------ Befehlsparameter

/** Fehlende Pflichtangaben vor dem ersten Klick: { feld, text, vorschlag } oder null. */
export function pflichtFehlt(opt, { kategorie = 'Rohr', serien = [] } = {}) {
  if (!serien.length) return { feld: 'serie', text: `Keine Leitungsserie der Kategorie ${kategorie} im Projekt.`, vorschlag: 'Eine Leitungsserie als Familie laden (Projektbrowser › Familien › Familie laden).' };
  if (!opt.serie) return { feld: 'serie', text: 'Die Serie fehlt.', vorschlag: 'In der Optionsleiste eine Leitungsserie wählen.' };
  if (!opt.groesse) return { feld: 'groesse', text: 'Die Grösse fehlt.', vorschlag: 'In der Optionsleiste eine Grösse der Serie wählen.' };
  if (!zahlOK(opt.hoehe)) return { feld: 'hoehe', text: 'Die Höhe fehlt.', vorschlag: 'In der Optionsleiste die Höhe der Achse über OK Geschoss in mm eintragen (Pflichtfeld).' };
  return null;
}

/** Eigenschaften der neuen Geraden aus der Optionsleiste (Dämmdicke). */
export function eigenschaftenAus(opt) {
  const e = {};
  if (zahlOK(opt.daemmdicke) && opt.daemmdicke > 0) e.daemmdicke = opt.daemmdicke;
  return Object.keys(e).length ? e : undefined;
}

/**
 * leitung_zeichnen. punkte [[x, y, z]] in der Reihenfolge des Zeichnens; von/nach sind Anschlüsse (anschluesseAus) am Anfang
 * bzw. Ende der Zeichnung. Zeichnet man gegen die Fliessrichtung, wird umgedreht (flussRichtung). System oder Strang sind
 * Pflicht, solange kein Anschluss das System liefert.
 */
export function leitungParameter({ opt, punkte, von = null, nach = null, geschoss = null }) {
  if (punkte.length < 2) throw new WerkzeugFehler('Eine Leitung braucht mindestens zwei Punkte.', 'Weitere Punkte anklicken.');
  if (!opt.serie) throw new WerkzeugFehler('Die Serie fehlt.', 'In der Optionsleiste eine Leitungsserie wählen.');
  if (!opt.groesse) throw new WerkzeugFehler('Die Grösse fehlt.', 'In der Optionsleiste eine Grösse der Serie wählen.');
  const r = flussRichtung({ punkte, von, nach });
  const p = { serie: opt.serie, groesse: opt.groesse, punkte: r.punkte.map(gerundet) };
  if (r.von) p.von = anschlussEnde(r.von);
  if (r.nach) p.nach = anschlussEnde(r.nach);
  if (opt.strang) p.strang = opt.strang;
  if (opt.system) p.system = opt.system;
  if (!p.strang && !p.system && !r.von && !r.nach)
    throw new WerkzeugFehler('System oder Strang fehlt.', 'In der Optionsleiste ein System (oder einen Strang) wählen – oder an einem Anschluss beginnen, der das System liefert.');
  if (zahlOK(opt.gefaelle) && opt.gefaelle > 0) p.gefaelle = opt.gefaelle;
  if (geschoss) p.geschoss = geschoss;
  const eig = eigenschaftenAus(opt);
  if (eig) p.eigenschaften = eig;
  return { parameter: p, umgedreht: r.umgedreht };
}

/**
 * leitung_abzweigen. leitung = Kennzeichen der Geraden, punkt = Fusspunkt [x, y, z], punkte = Verlauf des Abzweigs (ohne den
 * Fusspunkt, in Zeichenrichtung = vom T-Stück weg); ohne punkte gilt richtung (nur das T-Stück). Serie und Grösse aus der
 * Optionsleiste (System und Strang übernimmt der Kern von der Leitung).
 */
export function abzweigParameter({ opt, leitung, punkt, punkte = [], richtung = null, nach = null, geschoss = null }) {
  if (!leitung) throw new WerkzeugFehler('Keine Leitung für den Abzweig.', 'Zuerst auf eine bestehende Gerade klicken.');
  const p = { leitung, punkt: gerundet(punkt) };
  if (punkte.length) p.punkte = punkte.map(gerundet);
  else if (richtung) p.richtung = richtung;
  else throw new WerkzeugFehler('Der Abzweig hat keine Richtung.', 'Einen Punkt neben der Leitung anklicken oder Enter für nur das T-Stück.');
  if (opt?.serie) p.serie = opt.serie;
  if (opt?.groesse) p.groesse = opt.groesse;
  if (nach) p.ende = anschlussEnde(nach);
  if (opt && zahlOK(opt.gefaelle) && opt.gefaelle > 0) p.gefaelle = opt.gefaelle;
  if (geschoss) p.geschoss = geschoss;
  return p;
}

/** leitung_groesse bzw. leitung_serie aus der Palette: nur diese Leitung (leitung) oder der ganze Zug (zug: true). */
export function groessenParameter({ leitung, groesse, zug }) {
  const p = { leitung, groesse };
  if (zug) p.zug = true;
  return p;
}
export function serienParameter({ leitung, serie, zug }) {
  const p = { leitung, serie };
  if (zug) p.zug = true;
  return p;
}
/** leitung_aufteilen für eine Altbestand-Polylinie; groesse nur, wenn ausdrücklich gewählt (sonst aus der Leitung). */
export function aufteilenParameter({ leitung, serie, groesse = '' }) {
  if (!serie) throw new WerkzeugFehler('Die Serie fehlt.', 'Eine Leitungsserie wählen.');
  const p = { leitung, serie };
  if (groesse) p.groesse = groesse;
  return p;
}

// ------------------------------------------------------------------------------------------------ Vorschau-Zeile (Hinweise des Kerns)

const FORMTEIL_ART = /^(Bogen|T-Stück|Reduktion|Reinigungsöffnung) (\S+) Typ «([^»]+)»(?:, ([\d.]+)°)?/;
const ZAHL = (t) => Number(t);

/** Zahl in der Schreibweise des Kerns («0.###»). */
export const mmText = (x) => `${Math.round(x * 1000) / 1000} mm`;

/**
 * Liest die Hinweise eines (Trocken-)Laufs von leitung_zeichnen / leitung_abzweigen:
 * { geraden [{ kz, groesse, laenge }], formteile [{ art, kz, typ, winkel }], summe (mm), sonst [Text], kopf }.
 * Unbekannte Zeilen bleiben unter `sonst` (nichts geht verloren).
 */
export function vorschauAus(r) {
  const hin = (r?.hinweise ?? []).filter(Boolean);
  const geraden = [], formteile = [], sonst = [];
  let kopf = null;
  for (const h of hin) {
    let m;
    if ((m = /^Gerade (\S+) ([^:]+): ([\d.]+) mm/.exec(h))) geraden.push({ kz: m[1], groesse: m[2], laenge: ZAHL(m[3]) });
    else if ((m = FORMTEIL_ART.exec(h))) formteile.push({ art: m[1], kz: m[2], typ: m[3], winkel: m[4] !== undefined ? ZAHL(m[4]) : null });
    else if (/\d+ Gerade\(n\), zusammen [\d.]+ mm/.test(h)) kopf = h;
    else sonst.push(h);
  }
  const summe = geraden.reduce((s, g) => s + g.laenge, 0);
  return { geraden, formteile, summe: Math.round(summe * 1000) / 1000, sonst, kopf };
}

const mehrzahl = (n, eins, viele) => `${n} ${n === 1 ? eins : viele}`;

/** Eine Zeile: «2 Geraden, 3900 mm · 1 Bogen 90° · …». */
export function vorschauZeile(v) {
  const teile = [];
  if (v.geraden.length) teile.push(`${mehrzahl(v.geraden.length, 'Gerade', 'Geraden')}, ${mmText(v.summe)}`);
  const gruppen = new Map();
  for (const f of v.formteile) {
    const k = f.winkel !== null ? `${f.art} ${f.winkel}°` : f.art;
    gruppen.set(k, (gruppen.get(k) ?? 0) + 1);
  }
  if (gruppen.size) teile.push([...gruppen].map(([k, n]) => `${n} ${k}`).join(', '));
  return teile.length ? teile.join(' · ') : (v.kopf ?? v.sonst[0] ?? 'Keine Änderung.');
}

/** Alle Zeilen der Vorschau mit Länge bzw. Typ und Winkel (für das Aufklappen). */
export function vorschauListe(v) {
  return [
    ...v.geraden.map((g) => `Gerade ${g.kz} · ${g.groesse} · ${mmText(g.laenge)}`),
    ...v.formteile.map((f) => `${f.art} ${f.kz} · Typ «${f.typ}»${f.winkel !== null ? ` · ${f.winkel}°` : ''}`),
    ...v.sonst,
  ];
}

/** Ablehnung des Kerns: Text und Vorschlag (der Kern sagt, was nicht passt und wie es geht). */
export function ablehnung(r) {
  const text = r?.fehler || r?.meldung || 'Der Kern hat den Befehl abgelehnt.';
  return { text, vorschlag: r?.vorschlag ?? null };
}

/** Befunde, die ein Befehl neu auslöst (Prüfungen P13–P18 …), als kurze Texte für Meldungen. */
export function neueBefundeTexte(r, max = 3) {
  const l = (r?.neueBefunde ?? []).filter((b) => b && (b.schwere !== 'hinweis' || /^P1[3-8]$/.test(b.code ?? '')));
  const texte = l.slice(0, max).map((b) => `${b.code}${b.element ? ' ' + b.element : ''}: ${b.text}`);
  if (l.length > max) texte.push(`… und ${l.length - max} weitere`);
  return texte;
}

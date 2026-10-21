// Parameterbildung der Zeichen- und Ändern-Werkzeuge – reine Funktionen ohne Browser (in Node testbar).
// Aus Mauspunkten und Optionen der Optionsleiste entstehen die Parameter der Kern-Befehle (entwurf/modul-1a.md, Abschnitt
// Befehle). Gerechnet wird nur, was zur Eingabe gehört (Versatz der Masslinie aus dem Mausabstand, Blickseite eines Schnitts,
// Drehwinkel aus zwei Klicks); Fachregeln prüft der Kern. Fehlt eine Pflichtangabe, gibt es einen WerkzeugFehler mit Vorschlag.

import { abstand, rund, rundPunkt } from './fangen.js';

export class WerkzeugFehler extends Error {
  constructor(meldung, vorschlag = null) { super(meldung); this.name = 'WerkzeugFehler'; this.vorschlag = vorschlag; }
}

const pkt = (p) => rundPunkt([p[0], p[1]]);
const istZahl = (x) => typeof x === 'number' && Number.isFinite(x);
const positiv = (x) => istZahl(x) && x > 0;

/** Entfernt aufeinanderfolgende gleiche Punkte (Abstand < 1 mm). */
export function ohneDoppelte(punkte) {
  const r = [];
  for (const p of punkte) if (r.length === 0 || abstand(r[r.length - 1], p) >= 1) r.push(pkt(p));
  return r;
}

// ------------------------------------------------------------------------------------------------ Architektur

/**
 * wand_zeichnen. Ein letzter Punkt, der den ersten trifft (Abstand < 1 mm, mindestens drei verschiedene Punkte), schliesst
 * den Wandzug. dicke ist Pflicht; hoehe leer = Geschosshöhe (der Kern setzt die Vorgabe).
 */
export function wandParameter({ geschoss, punkte, dicke, hoehe = null, bezugslinie = 'mitte', material = '', geschlossen = false }) {
  if (!geschoss) throw new WerkzeugFehler('Die Ansicht hat kein Geschoss.', 'Einen Grundriss mit Geschoss aktivieren.');
  if (!positiv(dicke)) throw new WerkzeugFehler('Die Wanddicke fehlt.', 'In der Optionsleiste die Dicke in mm eintragen (aus dem Wandaufbau bzw. den Plänen des Architekten).');
  let pts = ohneDoppelte(punkte);
  let zu = !!geschlossen;
  if (pts.length >= 3 && abstand(pts[0], pts[pts.length - 1]) < 1) { pts = pts.slice(0, -1); zu = true; }
  if (pts.length < 2) throw new WerkzeugFehler('Eine Wand braucht zwei verschiedene Punkte.', 'Anfangs- und Endpunkt anklicken.');
  if (zu && pts.length < 3) throw new WerkzeugFehler('Ein geschlossener Wandzug braucht mindestens drei Punkte.', 'Weitere Punkte anklicken.');
  const p = { geschoss, punkte: pts, dicke };
  if (positiv(hoehe)) p.hoehe = hoehe;
  if (bezugslinie) p.bezugslinie = bezugslinie;
  if (material) p.material = material;
  if (zu) p.geschlossen = true;
  return p;
}

/**
 * oeffnung_setzen. Tür: seite und band (Vorgabe aus dem Kern, wenn leer); Fenster: Brüstung ist Pflicht.
 * punkt wird vom Kern auf die Wandachse projiziert.
 */
export function oeffnungParameter({ klasse, wand, punkt, breite, hoehe, bruestung = null, seite = '', band = '' }) {
  if (klasse !== 'Tuer' && klasse !== 'Fenster') throw new WerkzeugFehler(`«${klasse}» ist keine Öffnung.`, 'Tür oder Fenster wählen.');
  const art = klasse === 'Tuer' ? 'Tür' : 'Fenster';
  if (!wand) throw new WerkzeugFehler('Keine Wand unter dem Mauszeiger.', `Den Mauszeiger über eine Wand führen und klicken, um ${art === 'Tür' ? 'die Tür' : 'das Fenster'} zu setzen.`);
  if (!positiv(breite)) throw new WerkzeugFehler(`Die Breite der ${art === 'Tür' ? 'Tür' : 'des Fensters'} fehlt.`, 'In der Optionsleiste die lichte Breite in mm eintragen.');
  if (!positiv(hoehe)) throw new WerkzeugFehler(`Die Höhe der ${art === 'Tür' ? 'Tür' : 'des Fensters'} fehlt.`, 'In der Optionsleiste die lichte Höhe in mm eintragen.');
  const p = { klasse, wand, punkt: pkt(punkt), breite, hoehe };
  if (klasse === 'Fenster') {
    if (!istZahl(bruestung) || bruestung < 0) throw new WerkzeugFehler('Die Brüstungshöhe des Fensters fehlt.', 'In der Optionsleiste die Brüstung (Höhe über OK Boden) in mm eintragen, 0 oder grösser.');
    p.bruestung = bruestung;
  } else {
    if (seite) p.seite = seite;
    if (band) p.band = band;
  }
  return p;
}

/** raum_erkennen. */
export function raumParameter({ geschoss, punkt, nummer = '', nutzung = '', name = '' }) {
  if (!geschoss) throw new WerkzeugFehler('Die Ansicht hat kein Geschoss.', 'Einen Grundriss mit Geschoss aktivieren.');
  const p = { geschoss, punkt: pkt(punkt) };
  if (nummer) p.nummer = nummer;
  if (nutzung) p.nutzung = nutzung;
  if (name) p.name = name;
  return p;
}

/** Nächste Achsbezeichnung: A → B … Z → AA, 1 → 2. Leer oder unbekannt: null. */
export function naechsteBezeichnung(bezeichnung) {
  const t = String(bezeichnung ?? '').trim();
  if (t === '') return null;
  if (/^\d+$/.test(t)) return String(Number(t) + 1);
  if (/^[A-Za-z]+$/.test(t)) {
    const gross = t === t.toUpperCase();
    const z = t.toUpperCase().split('');
    let i = z.length - 1;
    while (i >= 0) {
      if (z[i] === 'Z') { z[i] = 'A'; i--; } else { z[i] = String.fromCharCode(z[i].charCodeAt(0) + 1); break; }
    }
    if (i < 0) z.unshift('A');
    const r = z.join('');
    return gross ? r : r.toLowerCase();
  }
  const m = t.match(/^(.*?)(\d+)$/);   // «A1» → «A2»
  if (m) return m[1] + String(Number(m[2]) + 1);
  return null;
}

/** Erste noch freie Bezeichnung der gewünschten Art ('buchstaben' | 'zahlen') nach den vorhandenen. */
export function bezeichnungVorschlag(vorhandene, art = 'buchstaben') {
  const liste = (vorhandene ?? []).map((x) => String(x ?? '').trim()).filter(Boolean);
  if (art === 'zahlen') {
    const zahlen = liste.filter((x) => /^\d+$/.test(x)).map(Number);
    return String(zahlen.length ? Math.max(...zahlen) + 1 : 1);
  }
  const buchstaben = liste.filter((x) => /^[A-Za-z]+$/.test(x)).map((x) => x.toUpperCase());
  if (buchstaben.length === 0) return 'A';
  buchstaben.sort((a, b) => a.length - b.length || (a < b ? -1 : a > b ? 1 : 0));
  return naechsteBezeichnung(buchstaben[buchstaben.length - 1]);
}

/** element_anlegen Klasse Achse. */
export function achseParameter({ a, b, bezeichnung }) {
  const bez = String(bezeichnung ?? '').trim();
  if (!bez) throw new WerkzeugFehler('Die Achsbezeichnung fehlt.', 'In der Optionsleiste eine Bezeichnung eintragen, z. B. A oder 1.');
  if (abstand(a, b) < 1) throw new WerkzeugFehler('Die Achse hat keine Länge.', 'Zwei verschiedene Punkte anklicken.');
  return { klasse: 'Achse', eigenschaften: { bezeichnung: bez }, geometrie: { art: 'linie', punkte: [pkt(a), pkt(b)] } };
}

// ------------------------------------------------------------------------------------------------ Beschriftung

const ansichtBezug = (ansicht) => {
  if (!ansicht) throw new WerkzeugFehler('Beschriftung gehört zu einer Ansicht.', 'Zuerst eine Ansicht aktivieren.');
  return { ansicht };
};

/** Messrichtung d (Einheitsvektor) einer Masskette nach der Regel des Kerns (BeschriftungMass). N9: horizontal/vertikal in den Achsen der
 *  Ansicht (drehung in Grad, Gegenuhrzeigersinn); ohne Drehung genau wie bisher. */
export function messrichtung(punkte, richtung = 'ausgerichtet', drehung = 0) {
  const p0 = punkte[0];
  let ziel = punkte[punkte.length - 1];
  if (abstand(p0, ziel) < 1e-9) ziel = punkte.reduce((m, q) => (abstand(p0, q) > abstand(p0, m) ? q : m), ziel);
  const v = [ziel[0] - p0[0], ziel[1] - p0[1]];
  const achse = (grad) => {
    const w = (grad - drehung) * Math.PI / 180, a = [Math.cos(w), Math.sin(w)];
    return v[0] * a[0] + v[1] * a[1] >= 0 ? a : [-a[0], -a[1]];
  };
  if (richtung === 'horizontal') return drehung ? achse(0) : [v[0] >= 0 ? 1 : -1, 0];
  if (richtung === 'vertikal') return drehung ? achse(90) : [0, v[1] >= 0 ? 1 : -1];
  const l = Math.hypot(v[0], v[1]);
  return l < 1e-9 ? [1, 0] : [v[0] / l, v[1] / l];
}

/** Versatz der Masslinie: Abstand des Klickpunkts von der Linie durch den ersten Messpunkt, positiv = links der Messrichtung. */
export function bemassungVersatz(punkte, richtung, lage, drehung = 0) {
  const d = messrichtung(punkte, richtung, drehung);
  const n = [-d[1], d[0]];
  return rund((lage[0] - punkte[0][0]) * n[0] + (lage[1] - punkte[0][1]) * n[1], 1);
}

/** element_anlegen Klasse Bemassung: Messpunkte, Richtung, versatz aus dem letzten Klick (drehung: N9, Drehung der Ansicht). */
export function bemassungParameter({ ansicht, punkte, richtung = 'ausgerichtet', lage, einheit = '', drehung = 0 }) {
  const bezug = ansichtBezug(ansicht);
  const pts = ohneDoppelte(punkte);
  if (pts.length < 2) throw new WerkzeugFehler('Eine Bemassung braucht mindestens zwei Messpunkte.', 'Zwei Punkte anklicken, dann die Lage der Masslinie.');
  const eig = { richtung, versatz: bemassungVersatz(pts, richtung, lage, drehung) };
  if (einheit) eig.einheit = einheit;
  return { klasse: 'Bemassung', eigenschaften: eig, geometrie: { art: 'polylinie', punkte: pts }, bezuege: bezug };
}

/** element_anlegen Klasse Textnotiz. groesse in Papier-mm (leer = Vorgabe des Kerns). */
export function textParameter({ ansicht, punkt, inhalt, groesse = null, ausrichtung = '', winkel = null }) {
  const bezug = ansichtBezug(ansicht);
  const text = String(inhalt ?? '').replace(/\r\n?/g, '\n').replace(/\s+$/, '');
  if (!text.trim()) throw new WerkzeugFehler('Der Text ist leer.', 'Einen Text eingeben.');
  const eig = { inhalt: text };
  if (positiv(groesse)) eig.groesse = groesse;
  if (ausrichtung) eig.ausrichtung = ausrichtung;
  if (istZahl(winkel) && winkel !== 0) eig.winkel = winkel;
  return { klasse: 'Textnotiz', eigenschaften: eig, geometrie: { art: 'punkt', punkte: [pkt(punkt)] }, bezuege: bezug };
}

/** element_anlegen Klasse Detaillinie. */
export function linieParameter({ ansicht, punkte, stift = '', linienart = '' }) {
  const bezug = ansichtBezug(ansicht);
  const pts = ohneDoppelte(punkte);
  if (pts.length < 2) throw new WerkzeugFehler('Eine Detaillinie braucht mindestens zwei Punkte.', 'Punkte anklicken, Enter oder Doppelklick beendet.');
  const eig = {};
  if (stift) eig.stift = stift;
  if (linienart) eig.linienart = linienart;
  return { klasse: 'Detaillinie', eigenschaften: eig, geometrie: { art: 'polylinie', punkte: pts }, bezuege: bezug };
}

/** element_anlegen Klasse Fuellbereich (Umriss wird nicht doppelt geschlossen). */
export function fuellParameter({ ansicht, punkte, muster = '', farbe = '', rand = null }) {
  const bezug = ansichtBezug(ansicht);
  let pts = ohneDoppelte(punkte);
  if (pts.length >= 2 && abstand(pts[0], pts[pts.length - 1]) < 1) pts = pts.slice(0, -1);
  if (pts.length < 3) throw new WerkzeugFehler('Ein Füllbereich braucht mindestens drei Punkte.', 'Eckpunkte anklicken, Enter oder Doppelklick beendet.');
  const eig = {};
  if (muster) eig.muster = muster;
  if (farbe) {
    if (!/^#[0-9a-fA-F]{6}$/.test(farbe)) throw new WerkzeugFehler(`Farbe «${farbe}» ist ungültig.`, 'Farbe als #rrggbb angeben, z. B. #cccccc.');
    eig.farbe = farbe;
  }
  if (rand !== null) eig.rand = !!rand;
  return { klasse: 'Fuellbereich', eigenschaften: eig, geometrie: { art: 'polygon', punkte: pts }, bezuege: bezug };
}

/**
 * element_anlegen Klasse Kote. Im Grundriss gilt z (Höhe des Punkts, Vorgabe Geschosskote), im Schnitt/Ansicht die zweite
 * Koordinate v. wert = fester Wert in mm statt der gemessenen Höhe.
 */
export function koteParameter({ ansicht, punkt, z = null, wert = null, zusatz = '' }) {
  const bezug = ansichtBezug(ansicht);
  const p = istZahl(z) ? [...pkt(punkt), z] : pkt(punkt);
  const eig = {};
  if (istZahl(wert)) eig.wert = wert;
  if (zusatz) eig.zusatz = zusatz;
  return { klasse: 'Kote', eigenschaften: eig, geometrie: { art: 'punkt', punkte: [p] }, bezuege: bezug };
}

/** element_anlegen Klasse Symbol, art nordpfeil. */
export function nordpfeilParameter({ ansicht, punkt, winkel = null, groesse = null }) {
  const bezug = ansichtBezug(ansicht);
  const eig = { art: 'nordpfeil' };
  if (istZahl(winkel) && winkel !== 0) eig.winkel = winkel;
  if (positiv(groesse)) eig.groesse = groesse;
  return { klasse: 'Symbol', eigenschaften: eig, geometrie: { art: 'punkt', punkte: [pkt(punkt)] }, bezuege: bezug };
}

// ------------------------------------------------------------------------------------------------ Schnitt

/** Seite des Punkts zur Linie a → b: 'links' (Normale (−dy, dx)), sonst 'rechts'. Auf der Linie: 'links'. */
export function seiteDerLinie(a, b, p) {
  const kreuz = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]);
  return kreuz >= 0 ? 'links' : 'rechts';
}

/**
 * element_anlegen Klasse Ansicht, art schnitt: Linie im Grundriss, Blickseite, Sichttiefe, Massstab der aktiven Ansicht.
 * Annahme: blick «links» = der Blick geht auf die linke Seite der Linie (Anfang → Ende), wie der Mauszeiger beim Setzen.
 */
export function schnittParameter({ geschoss = null, a, b, seitenpunkt, tiefe, massstab = null, name = '' }) {
  if (abstand(a, b) < 1) throw new WerkzeugFehler('Die Schnittlinie hat keine Länge.', 'Zwei verschiedene Punkte anklicken.');
  if (!positiv(tiefe)) throw new WerkzeugFehler('Die Sichttiefe fehlt.', 'In der Optionsleiste die Tiefe in mm eintragen.');
  const eig = { art: 'schnitt', blick: seiteDerLinie(a, b, seitenpunkt), tiefe };
  if (positiv(massstab)) eig.massstab = massstab;
  const p = { klasse: 'Ansicht', eigenschaften: eig, geometrie: { art: 'linie', punkte: [pkt(a), pkt(b)] } };
  if (geschoss) p.geschoss = geschoss;
  if (name) p.name = name;
  return p;
}

// ------------------------------------------------------------------------------------------------ Ändern

function liste(elemente) {
  const l = (elemente ?? []).filter(Boolean);
  if (l.length === 0) throw new WerkzeugFehler('Nichts gewählt.', 'Zuerst ein oder mehrere Elemente wählen (Strg+Klick für mehrere).');
  return l;
}

/** elemente_verschieben: Basispunkt → Zielpunkt. */
export function verschiebenParameter(elemente, von, nach) {
  return { elemente: liste(elemente), dx: rund(nach[0] - von[0], 3), dy: rund(nach[1] - von[1], 3) };
}

/** elemente_kopieren: Basispunkt → Zielpunkt, anzahl Kopien (jede um dx, dy weiter). */
export function kopierenParameter(elemente, von, nach, anzahl = 1) {
  const p = { ...verschiebenParameter(elemente, von, nach) };
  p.anzahl = Number.isInteger(anzahl) && anzahl >= 1 ? anzahl : 1;
  return p;
}

/** Drehwinkel in Grad (−180 … 180], gegen den Uhrzeigersinn, von der Richtung mitte → von nach mitte → nach. */
export function drehwinkel(mitte, von, nach) {
  const w = (Math.atan2(nach[1] - mitte[1], nach[0] - mitte[0]) - Math.atan2(von[1] - mitte[1], von[0] - mitte[0])) * 180 / Math.PI;
  let n = ((w % 360) + 360) % 360;
  if (n > 180) n -= 360;
  return rund(n, 4);
}

/** elemente_drehen. */
export function drehenParameter(elemente, mitte, winkel) {
  if (!istZahl(winkel)) throw new WerkzeugFehler('Der Drehwinkel fehlt.', 'Zwei Klicks setzen oder den Winkel in Grad eingeben.');
  return { elemente: liste(elemente), mitte: pkt(mitte), winkel: rund(winkel, 4) };
}

/** elemente_spiegeln: Achse durch zwei Punkte. */
export function spiegelnParameter(elemente, a, b) {
  if (abstand(a, b) < 1) throw new WerkzeugFehler('Die Spiegelachse hat keine Länge.', 'Zwei verschiedene Punkte anklicken.');
  return { elemente: liste(elemente), achse: [pkt(a), pkt(b)] };
}

// ------------------------------------------------------------------------------------------------ Griffe

/** Anzahl Griffe an der Geometrie (Linie/Polylinie: alle Punkte; sonst 0). */
export function griffPunkte(geometrie) {
  if (!geometrie || !['linie', 'polylinie'].includes(geometrie.art)) return [];
  return (geometrie.punkte ?? []).map((p) => [p[0], p[1]]);
}

/**
 * Neue Geometrie, wenn ein Griff gezogen wird: Art, Dicke, Höhe bleiben, der Punkt index wird ersetzt (z bleibt erhalten).
 */
export function griffGeometrie(geometrie, index, neu) {
  const pts = geometrie?.punkte ?? [];
  if (!(index >= 0 && index < pts.length)) throw new WerkzeugFehler('Dieser Griff gehört nicht zur Geometrie.', null);
  const g = { ...geometrie, punkte: pts.map((p, i) => (i === index ? [rund(neu[0]), rund(neu[1]), ...(p.length > 2 ? [p[2]] : [])] : [...p])) };
  return g;
}

/** Eigenschaft, die ein Türgriff umdreht: 'seite' (links ↔ rechts) oder 'band' (anfang ↔ ende). */
export function tuerUmdrehen(tuer, was) {
  const w = tuer?.werte ?? {};
  if (was === 'seite') return { element: tuer.kennzeichen, eigenschaften: { seite: (w.seite ?? 'links') === 'links' ? 'rechts' : 'links' } };
  if (was === 'band') return { element: tuer.kennzeichen, eigenschaften: { band: (w.band ?? 'anfang') === 'anfang' ? 'ende' : 'anfang' } };
  throw new WerkzeugFehler(`«${was}» lässt sich nicht umdrehen.`, 'seite oder band');
}

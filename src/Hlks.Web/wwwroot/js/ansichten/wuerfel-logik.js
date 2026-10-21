// Navigationswürfel der 3D-Ansicht (Nachbearbeitung N11, analog Revit ViewCube): die Rechenregeln, rein und ohne DOM und three (in Node getestet:
// wuerfel.test.mjs). Zeichnen und Bedienen stehen in wuerfel.js.
//
// Koordinaten der 3D-Ansicht (three): x = Ost (Projekt-x), y = oben, z = −Nord (Projekt-y). Kamerarichtung = Richtung vom Ziel zur Kamera.
// Der Würfel hat eigene Achsen («lokal»): +x RECHTS, +y OBEN, +z VORNE (Kamera auf der Südseite, Blick nach Norden), −z HINTEN.
// Er ist um die Gebäudeausrichtung (Winkel θ um die Hochachse, gegen den Uhrzeigersinn von oben) gegen die Welt gedreht; ohne Ausrichtung ist θ = 0
// und VORNE blickt zur Projektachse. Eine Kamerarichtung wird als Azimut (Winkel um die Hochachse, atan2(x, z)) und Polarwinkel (ab +y) geführt.

const GRAD = Math.PI / 180;

/** Schwelle (0 … 1) auf einer Würfelfläche: ab dieser Koordinate zählt der Punkt zur Kante bzw. Ecke (Revit teilt jede Fläche in 3 × 3). */
export const SCHWELLE = 0.62;

/** Die sechs Flächen: Name (deutsch), Normale, «oben» und «rechts» in der Fläche (so, dass die Beschriftung von aussen aufrecht steht). */
export const FLAECHEN = Object.freeze([
  { name: 'VORNE', normale: [0, 0, 1], hoch: [0, 1, 0], rechts: [1, 0, 0] },
  { name: 'HINTEN', normale: [0, 0, -1], hoch: [0, 1, 0], rechts: [-1, 0, 0] },
  { name: 'RECHTS', normale: [1, 0, 0], hoch: [0, 1, 0], rechts: [0, 0, -1] },
  { name: 'LINKS', normale: [-1, 0, 0], hoch: [0, 1, 0], rechts: [0, 0, 1] },
  { name: 'OBEN', normale: [0, 1, 0], hoch: [0, 0, -1], rechts: [1, 0, 0] },
  { name: 'UNTEN', normale: [0, -1, 0], hoch: [0, 0, 1], rechts: [1, 0, 0] },
]);

const NAME_ACHSE = { x: ['LINKS', 'RECHTS'], y: ['UNTEN', 'OBEN'], z: ['HINTEN', 'VORNE'] };

/** Bereich (Fläche, Kante oder Ecke) als Vektor mit Einträgen −1, 0, 1 aus einem Punkt auf der Würfeloberfläche (Koordinaten −1 … 1, lokal). */
export function bereichVonPunkt(p, schwelle = SCHWELLE) {
  const f = (v) => (v > schwelle ? 1 : v < -schwelle ? -1 : 0);
  const r = { x: f(p.x), y: f(p.y), z: f(p.z) };
  if (r.x === 0 && r.y === 0 && r.z === 0) {   // Punkt im Innern (sollte nicht vorkommen): die Achse mit dem grössten Betrag
    const a = Math.abs(p.x) >= Math.abs(p.y) && Math.abs(p.x) >= Math.abs(p.z) ? 'x' : Math.abs(p.y) >= Math.abs(p.z) ? 'y' : 'z';
    r[a] = p[a] < 0 ? -1 : 1;
  }
  return r;
}

/** 'flaeche' (1 Achse), 'kante' (2) oder 'ecke' (3). */
export function bereichsArt(r) {
  const n = Math.abs(r.x) + Math.abs(r.y) + Math.abs(r.z);
  return n === 1 ? 'flaeche' : n === 2 ? 'kante' : 'ecke';
}

/** Name des Bereichs, z. B. «OBEN», «VORNE-RECHTS», «OBEN-VORNE-RECHTS» (Reihenfolge: oben/unten, vorne/hinten, rechts/links). */
export function bereichsName(r) {
  const t = [];
  if (r.y) t.push(NAME_ACHSE.y[r.y > 0 ? 1 : 0]);
  if (r.z) t.push(NAME_ACHSE.z[r.z > 0 ? 1 : 0]);
  if (r.x) t.push(NAME_ACHSE.x[r.x > 0 ? 1 : 0]);
  return t.join('-');
}

/** Schlüssel eines Bereichs («1,0,-1»), zum Gruppieren der Teilflächen. */
export const bereichsSchluessel = (r) => `${r.x},${r.y},${r.z}`;

/** Normierte Richtung (lokal) vom Ziel zur Kamera, wenn man den Bereich anklickt. */
export function richtungLokal(r) {
  const l = Math.hypot(r.x, r.y, r.z) || 1;
  return { x: r.x / l, y: r.y / l, z: r.z / l };
}

/** Alle 26 Bereiche (6 Flächen, 12 Kanten, 8 Ecken). */
export function alleBereiche() {
  const aus = [];
  for (let x = -1; x <= 1; x++) for (let y = -1; y <= 1; y++) for (let z = -1; z <= 1; z++) if (x || y || z) aus.push({ x, y, z });
  return aus;
}

/**
 * Die 54 Teilflächen der Würfeloberfläche (9 je Fläche): Ecken [p0..p3] gegen den Uhrzeigersinn von aussen, Bereich, Fläche, Mitte der Teilfläche.
 * Der Würfel hat die Kantenlänge 2; `lueckee` lässt zwischen den Teilflächen einen schmalen Streifen frei (gezeichnet als Linie).
 */
export function teilflaechen(schwelle = SCHWELLE, luecke = 0.03) {
  const aus = [];
  const bereiche = (i) => (i < 0 ? [-1, -schwelle] : i > 0 ? [schwelle, 1] : [-schwelle, schwelle]);
  for (const f of FLAECHEN) {
    for (const iu of [-1, 0, 1]) for (const iv of [-1, 0, 1]) {
      const [u0, u1] = bereiche(iu).map((v, k) => v + (k === 0 ? luecke : -luecke));
      const [v0, v1] = bereiche(iv).map((v, k) => v + (k === 0 ? luecke : -luecke));
      const punkt = (u, v) => ({ x: f.normale[0] + f.rechts[0] * u + f.hoch[0] * v, y: f.normale[1] + f.rechts[1] * u + f.hoch[1] * v, z: f.normale[2] + f.rechts[2] * u + f.hoch[2] * v });
      const bereich = { x: f.normale[0] + f.rechts[0] * iu + f.hoch[0] * iv, y: f.normale[1] + f.rechts[1] * iu + f.hoch[1] * iv, z: f.normale[2] + f.rechts[2] * iu + f.hoch[2] * iv };
      aus.push({ flaeche: f.name, iu, iv, mitte: iu === 0 && iv === 0, bereich, ecken: [punkt(u0, v0), punkt(u1, v0), punkt(u1, v1), punkt(u0, v1)] });
    }
  }
  return aus;
}

// ---------------------------------------------------------------------------------------------------- Ausrichtung des Gebäudes

/**
 * EINE Stelle für die Gebäudeausrichtung (Nachbearbeitung N9): Winkel θ in Grad, um den VORNE gegen die Projektachsen gedreht ist
 * (gegen den Uhrzeigersinn, von oben gesehen). Liest Gebaeude.planwinkel (N9: Drehung der Darstellung in Grad, die die längste Gebäudekante
 * waagrecht stellt – die Gebäudekante liegt also um −planwinkel gegen die x-Achse), sonst 0 = Projektachsen.
 * Nach dem Zusammenführen mit N9 nur hier anpassen (Vorzeichen, Herkunft des Winkels).
 */
export function gebaeudeAusrichtungGrad(gebaeude) {
  const w = gebaeude?.werte ?? gebaeude;
  const p = Number(w?.planwinkel);
  return Number.isFinite(p) ? -p : 0;
}

/** Nordwinkel in Grad aus Projektinformation.nordwinkel (Winkel von Projekt-Nord +y zu geografisch Nord im Uhrzeigersinn); ohne Angabe 0. */
export function nordwinkelGrad(projektinfo) {
  const w = projektinfo?.werte ?? projektinfo;
  const n = Number(w?.nordwinkel);
  return Number.isFinite(n) ? n : 0;
}

/** Richtung geografisch Nord in der Welt (three: x, z), Länge 1. Projekt-Nord = −z; Nordwinkel im Uhrzeigersinn von oben. */
export function nordRichtungWelt(nordGrad) {
  const n = nordGrad * GRAD;
  return { x: Math.sin(n), z: -Math.cos(n) };
}

/** Dreht einen Vektor (x, y, z) um die Hochachse um θ (Bogenmass, gegen den Uhrzeigersinn von oben): +x → −z. */
export function drehenUmHochachse(v, theta) {
  const c = Math.cos(theta), s = Math.sin(theta);
  return { x: v.x * c + v.z * s, y: v.y, z: -v.x * s + v.z * c };
}
export const lokalZuWelt = (v, theta) => drehenUmHochachse(v, theta);
export const weltZuLokal = (v, theta) => drehenUmHochachse(v, -theta);

// ---------------------------------------------------------------------------------------------------- Kamerawinkel

/** Kamerarichtung (x, y, z) → { azimut, polar }. Steht sie fast senkrecht, gilt `azimutFallback` (Bildschirm-oben bleibt «hinten»). */
export function kugelwinkel(d, azimutFallback = 0) {
  const l = Math.hypot(d.x, d.y, d.z) || 1;
  const y = d.y / l;
  const waagrecht = Math.hypot(d.x, d.z) / l;
  return { azimut: waagrecht < 1e-6 ? azimutFallback : Math.atan2(d.x, d.z), polar: Math.acos(Math.max(-1, Math.min(1, y))) };
}

/** { azimut, polar } → Richtung (Länge 1). */
export function richtungAusWinkeln(azimut, polar) {
  const s = Math.sin(polar);
  return { x: s * Math.sin(azimut), y: Math.cos(polar), z: s * Math.cos(azimut) };
}

/** Zielwinkel (Welt) beim Klick auf einen Bereich: Richtung lokal → Welt (Gebäudeausrichtung θ in Bogenmass). */
export function zielWinkel(r, theta = 0) {
  const lokal = richtungLokal(r);
  const welt = lokalZuWelt(lokal, theta);
  const vorneAzimut = Math.atan2(lokalZuWelt({ x: 0, y: 0, z: 1 }, theta).x, lokalZuWelt({ x: 0, y: 0, z: 1 }, theta).z);
  const w = kugelwinkel(welt, vorneAzimut);
  return { ...w, richtung: richtungAusWinkeln(w.azimut, w.polar), welt };
}

/** Kleinster Winkelunterschied b − a im Bereich (−π, π]. */
export function winkelDifferenz(a, b) {
  let d = (b - a) % (2 * Math.PI);
  if (d > Math.PI) d -= 2 * Math.PI;
  if (d <= -Math.PI) d += 2 * Math.PI;
  return d;
}

/** Weicher Übergang 0 … 1 (langsam – schnell – langsam). */
export const weich = (t) => { const x = Math.max(0, Math.min(1, t)); return x * x * (3 - 2 * x); };

/** Zwischenstand der Kamerafahrt bei t (0 … 1): Azimut auf dem kurzen Weg, Polarwinkel und Abstand linear, Ziel linear. */
export function zwischenstand(von, nach, t) {
  const k = weich(t);
  return {
    azimut: von.azimut + winkelDifferenz(von.azimut, nach.azimut) * k,
    polar: von.polar + (nach.polar - von.polar) * k,
    abstand: von.abstand + (nach.abstand - von.abstand) * k,
    ziel: { x: von.ziel.x + (nach.ziel.x - von.ziel.x) * k, y: von.ziel.y + (nach.ziel.y - von.ziel.y) * k, z: von.ziel.z + (nach.ziel.z - von.ziel.z) * k },
  };
}

/** Zeigerbewegung am Würfel (Pixel) → Änderung von Azimut und Polarwinkel (Bogenmass); Ziehen nach rechts dreht den Würfel nach rechts. */
export function ziehenZuWinkel(dx, dy, radPerPixel = 0.011) {
  return { dAzimut: -dx * radPerPixel, dPolar: -dy * radPerPixel };
}

// ---------------------------------------------------------------------------------------------------- Kompassring

/**
 * Winkel (Bogenmass, im Uhrzeigersinn ab Bildschirm-oben), unter dem geografisch Nord auf dem Ring steht, wenn die Kamera den Azimut `azimut` hat
 * (Blick von oben auf den Ring). O, S, W folgen im Abstand von je 90°.
 */
export function nordAufRing(azimut, nordGrad) {
  const w = (nordGrad * GRAD + azimut) % (2 * Math.PI);
  return w < 0 ? w + 2 * Math.PI : w;
}

/** Winkel (Bogenmass im Uhrzeigersinn ab oben) der vier Himmelsrichtungen auf dem Ring. */
export function ringMarken(azimut, nordGrad) {
  const n = nordAufRing(azimut, nordGrad);
  const m = (k) => (n + k * Math.PI / 2) % (2 * Math.PI);
  return { N: m(0), O: m(1), S: m(2), W: m(3) };
}

/** Winkel eines Zeigerpunkts um die Mitte des (flach gedrückten) Rings, im Uhrzeigersinn ab oben; `verhaeltnis` = Höhe / Breite des Rings. */
export function ringWinkel(punkt, mitte, verhaeltnis = 1) {
  return Math.atan2(punkt.x - mitte.x, -(punkt.y - mitte.y) / (verhaeltnis || 1));
}

/** Änderung des Azimuts, wenn der Ring vom Winkel `von` zu `nach` (beide wie ringWinkel) gezogen wird: der Ring dreht mit der Hand, die Kamera um dasselbe Mass. */
export const ringZug = (von, nach) => winkelDifferenz(von, nach);

// ---------------------------------------------------------------------------------------------------- Orbit um einen Drehpunkt

/**
 * Orbit um die Auswahl bzw. die Modellmitte (Umschalt + Mitte): die OrbitControls drehen um ihr Ziel. Damit der Blick beim Beginn nicht springt,
 * wird das Ziel auf der Blicklinie verschoben, und zwar auf die Tiefe des Drehpunkts. pos, ziel, pivot = {x, y, z}.
 */
export function schwenkZiel(pos, ziel, pivot, mindestAbstand = 0.05) {
  const d = { x: ziel.x - pos.x, y: ziel.y - pos.y, z: ziel.z - pos.z };
  const l = Math.hypot(d.x, d.y, d.z) || 1;
  const e = { x: d.x / l, y: d.y / l, z: d.z / l };
  const t = Math.max(mindestAbstand, (pivot.x - pos.x) * e.x + (pivot.y - pos.y) * e.y + (pivot.z - pos.z) * e.z);
  return { x: pos.x + e.x * t, y: pos.y + e.y * t, z: pos.z + e.z * t };
}

// ---------------------------------------------------------------------------------------------------- Startansicht

/** Vorgabe der Startansicht: Isometrie von VORNE-RECHTS-OBEN (Revit-Home). */
export const START_VORGABE = Object.freeze({ x: 1, y: 1, z: 1 });

/** Schlüssel, unter dem die Oberfläche die Startansicht einer 3D-Ansicht merkt (je Projekt und Ansicht, je Rechner). */
export const startansichtSchluessel = (ort, kennzeichen) => `startansicht3d.${String(ort ?? '').toLowerCase()}.${String(kennzeichen ?? '')}`;

/** Gemerkte Startansicht prüfen: { azimut, polar } in Grad relativ zur Gebäudeausrichtung, sonst null. */
export function startansichtLesen(roh) {
  const a = Number(roh?.azimutGrad), p = Number(roh?.polarGrad);
  if (!Number.isFinite(a) || !Number.isFinite(p) || p < 0 || p > 180) return null;
  return { azimutGrad: a, polarGrad: p };
}

/** Aktuelle Kamerarichtung (Welt) → merkbare Startansicht (Winkel relativ zur Gebäudeausrichtung θ, damit sie einer neuen Ausrichtung folgt). */
export function startansichtAusRichtung(weltRichtung, theta = 0) {
  const w = kugelwinkel(lokalZuWelt(weltRichtung, -theta), 0);
  const runden = (x) => Math.round(x * 1000) / 1000;
  return { azimutGrad: runden(w.azimut / GRAD), polarGrad: runden(w.polar / GRAD) };
}

/** Zielwinkel (Welt) der Startansicht: die gemerkte oder die Vorgabe (Isometrie). */
export function startansichtZiel(gemerkt, theta = 0) {
  if (gemerkt) {
    const lokal = richtungAusWinkeln(gemerkt.azimutGrad * GRAD, gemerkt.polarGrad * GRAD);
    const welt = lokalZuWelt(lokal, theta);
    const vorne = lokalZuWelt({ x: 0, y: 0, z: 1 }, theta);
    const w = kugelwinkel(welt, Math.atan2(vorne.x, vorne.z));
    return { ...w, richtung: richtungAusWinkeln(w.azimut, w.polar) };
  }
  return zielWinkel(START_VORGABE, theta);
}

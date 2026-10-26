// Erzeugt die synthetischen Testfolgen der Gestenlogik (Modul G) als JSON im Format von aufnahmeBeenden() (gesten.js): je Datei eine Folge mit «erwartet».
// Aufruf:  node src/Hlks.Web/wwwroot/js/gesten/testfolgen/erzeugen.mjs      (schreibt testfolgen/*.json neu; gesten-logik.test.mjs prüft, dass sie aktuell sind)
// Echte Aufnahmen (Christian, ohne Bild, nur Punkte) kommen als weitere *.json dazu – mit eigenem «erwartet», z. B. { "schritte": [], "navigation": "keine" } für
// zehn Minuten normale Arbeit. Dateien, die mit «synth-» beginnen, gehören diesem Erzeuger.
//
// erwartet: { schritte: [{ geste, richtung }] (genau diese, in dieser Reihenfolge), navigation: 'keine' | { drehen: [[min, max], [min, max]], rollen: [min, max],
//             zoom: [min, max] } (Summe über die Folge, Bogenmass bzw. Faktor), navigationEndetBis (ms), gesten (müssen vorkommen), zweiD: { verschieben: … } (Plan) }

import { writeFileSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { handBild, stand, zufall, KAMERA } from './handmodell.mjs';
import { bildFuerAufnahme, AUFNAHME_VERSION } from '../gesten-logik.js';

const GRAD = Math.PI / 180;
const RAUSCHEN = Object.freeze({ bild: 0.0015, tiefe: 0.003, welt: 0.002 });
export const P0 = [-0.12, 0.05, 0.5];   // linke Hand: links der Bildmitte, etwas unter der Mitte, 50 cm vor der Kamera
export const plus = (p, d) => [p[0] + d[0], p[1] + d[1], p[2] + d[2]];
export const k = (t, haltung, lage, extra = {}) => ({ t, haltung, lage, ...extra });

/** Eine Folge: Bilder mit fps (Zeiten leicht unregelmässig), Rauschen aus einem festen Zufallsstrom. */
export function folge({ name, beschreibung, hand = 'links', seite = hand, fps = 30, dauer, schluessel, zittern = null, rauschen = RAUSCHEN, saat = 1, eingaben = [], erwartet }) {
  const zz = zufall(saat);
  const bilder = [];
  const schritt = 1000 / fps;
  for (let i = 0; ; i++) {
    const t = Math.round((i * schritt + (i > 0 ? (zz.gleich() - 0.5) * 4 : 0)) * 10) / 10;
    if (t > dauer) break;
    const st = stand(schluessel, t, zittern);
    const haende = [];
    if (st.sichtbar) {
      const b = handBild({ seite, haltung: st.haltung, lage: st.lage, roll: st.roll, nick: st.nick, gier: st.gier, rauschen, zz });
      if (b.imBild >= 0.6) haende.push({ punkte: b.punkte, welt: b.welt, haendigkeit: seite, score: 0.95 });   // grösstenteils ausserhalb: keine Erkennung
    }
    bilder.push(bildFuerAufnahme(t, haende));
  }
  return {
    version: AUFNAHME_VERSION, name, beschreibung, quelle: 'synthetisch: testfolgen/erzeugen.mjs mit handmodell.mjs (keine echten Daten)',
    hand, bildbreite: KAMERA.breite, bildhoehe: KAMERA.hoehe, bilder, eingaben, erwartet,
  };
}

/** Faust im Bild schliessen, stillhalten, dann `bewegung` (Schlüsselbilder ab 1000 ms), wieder öffnen. */
function faustFolge(name, beschreibung, bewegung, ende, erwartet, extra = {}) {
  return folge({
    name, beschreibung, dauer: ende + 350, erwartet, ...extra,
    schluessel: [k(0, 'offen', P0), k(400, 'offen', P0), k(550, 'faust', P0), k(1000, 'faust', P0), ...bewegung, k(ende, 'offen', bewegung.at(-1).lage, { roll: bewegung.at(-1).roll ?? 0 }), k(ende + 350, 'offen', bewegung.at(-1).lage, { roll: bewegung.at(-1).roll ?? 0 })],
  });
}

/** Lockere Hand ruhig bei `start`, ab 600 ms ein Anstoss um `d` in 150 ms, danach ruhig. */
function wischFolge(name, beschreibung, start, d, erwartet, extra = {}) {
  const { gier = 0, haltung = 'locker', ...rest } = extra;
  return folge({
    name, beschreibung, dauer: 1300, erwartet, ...rest,
    schluessel: [k(0, haltung, start, { gier }), k(600, haltung, start, { gier }), k(750, haltung, plus(start, d), { gier }), k(1300, haltung, plus(start, d), { gier })],
  });
}

const KEINE = Object.freeze({ schritte: [], navigation: 'keine' });
const wenig = [-0.1, 0.1];

export function alleFolgen() {
  const f = [];
  // ================================================================== fliessend (Faust)
  f.push(faustFolge('synth-faust-drehen-rechts', 'G4: Faust im Bild schliessen, stillhalten, 12 cm nach rechts, öffnen. 3D dreht nach rechts, Plan verschiebt nach rechts.',
    [k(1600, 'faust', plus(P0, [0.12, 0, 0])), k(1900, 'faust', plus(P0, [0.12, 0, 0]))], 2050,
    { schritte: [], navigation: { drehen: [[0.8, 1.4], wenig], rollen: [-0.05, 0.05], zoom: [0.95, 1.05] }, gesten: ['drehen'], zweiD: { verschieben: [[0.3, 0.6], [-0.05, 0.05]] } }));
  f.push(faustFolge('synth-faust-drehen-runter', 'G4: Faust 10 cm nach unten. 3D dreht die Vorderseite nach unten.',
    [k(1600, 'faust', plus(P0, [0, 0.10, 0])), k(1900, 'faust', plus(P0, [0, 0.10, 0]))], 2050,
    { schritte: [], navigation: { drehen: [wenig, [0.6, 1.2]], rollen: [-0.05, 0.05], zoom: [0.95, 1.05] }, gesten: ['drehen'] }));
  f.push(faustFolge('synth-faust-rollen', 'G7: Faust wie einen Drehknopf 40° gegen den Uhrzeiger (wie gesehen) drehen. 3D rollt gegen den Uhrzeiger.',
    [k(1800, 'faust', P0, { roll: 40 * GRAD }), k(2100, 'faust', P0, { roll: 40 * GRAD })], 2250,
    { schritte: [], navigation: { drehen: [wenig, wenig], rollen: [0.4, 0.65], zoom: [0.95, 1.05] }, gesten: ['rollen'] }));
  {
    const a = [-0.15, 0.05, 0.55], b = [-0.15, 0.05, 0.42];
    f.push(folge({
      name: 'synth-faust-zur-kamera', beschreibung: 'G5: Faust neben der Bildmitte 13 cm gerade auf die Kamera zu. Zoomt näher, dreht nicht (die Faust wandert im Bild nach aussen).',
      dauer: 2350, schluessel: [k(0, 'offen', a), k(400, 'offen', a), k(550, 'faust', a), k(1000, 'faust', a), k(1700, 'faust', b), k(2000, 'faust', b), k(2150, 'offen', b), k(2350, 'offen', b)],
      erwartet: { schritte: [], navigation: { drehen: [wenig, wenig], rollen: [-0.05, 0.05], zoom: [1.3, 1.95] }, gesten: ['zoomen'] },
    }));
  }
  f.push(folge({
    name: 'synth-faust-loesen-im-zug', beschreibung: 'Faust lösen beendet sofort: die Hand öffnet sich mitten in der Bewegung und läuft offen weiter – keine Bewegung mehr ab dem Öffnen.',
    dauer: 2300, schluessel: [k(0, 'offen', P0), k(400, 'offen', P0), k(550, 'faust', P0), k(1000, 'faust', P0), k(1300, 'faust', plus(P0, [0.06, 0, 0]), { linear: true }),
      k(1400, 'offen', plus(P0, [0.08, 0, 0]), { linear: true }), k(2000, 'offen', plus(P0, [0.14, 0, 0]), { linear: true }), k(2300, 'offen', plus(P0, [0.14, 0, 0]))],
    erwartet: { schritte: [], navigation: { drehen: [[0.15, 0.62], wenig] }, navigationEndetBis: 1400, gesten: ['drehen'] },
  }));
  f.push(faustFolge('synth-faust-drehen-15bps', 'G4 mit 15 Bildern/s (Rückfall): Faust 12 cm nach rechts.',
    [k(1600, 'faust', plus(P0, [0.12, 0, 0])), k(1900, 'faust', plus(P0, [0.12, 0, 0]))], 2050,
    { schritte: [], navigation: { drehen: [[0.8, 1.4], wenig], rollen: [-0.05, 0.05], zoom: [0.95, 1.05] }, gesten: ['drehen'] }, { fps: 15, saat: 7 }));

  // ================================================================== Schritt (Wisch, Pistole)
  const P1 = [-0.06, 0.05, 0.5];
  f.push(wischFolge('synth-wisch-links', 'G2: lockere Hand, ein Anstoss 9 cm nach links in 0.15 s.', P0, [-0.09, 0, 0], { schritte: [{ geste: 'G2', richtung: 'links' }], navigation: 'keine' }));
  f.push(wischFolge('synth-wisch-rechts', 'G2: lockere Hand, ein Anstoss 9 cm nach rechts.', P1, [0.09, 0, 0], { schritte: [{ geste: 'G2', richtung: 'rechts' }], navigation: 'keine' }, { saat: 2 }));
  f.push(wischFolge('synth-wisch-hoch', 'G3: lockere Hand, ein Anstoss 8 cm nach oben.', P0, [0, -0.08, 0], { schritte: [{ geste: 'G3', richtung: 'hoch' }], navigation: 'keine' }, { saat: 3 }));
  f.push(wischFolge('synth-wisch-runter', 'G3: lockere Hand, ein Anstoss 8 cm nach unten.', [-0.10, -0.02, 0.5], [0, 0.08, 0], { schritte: [{ geste: 'G3', richtung: 'runter' }], navigation: 'keine' }, { saat: 4 }));
  f.push(wischFolge('synth-wisch-links-handflaeche-seitlich', 'G2: Handfläche zeigt zur Seite (70° gedreht), Anstoss nach links.', P0, [-0.09, 0, 0],
    { schritte: [{ geste: 'G2', richtung: 'links' }], navigation: 'keine' }, { gier: 70 * GRAD, saat: 5 }));
  f.push(wischFolge('synth-wisch-rechts-offen-15bps', 'G2 mit 15 Bildern/s und offener Hand: Anstoss nach rechts.', P1, [0.09, 0, 0],
    { schritte: [{ geste: 'G2', richtung: 'rechts' }], navigation: 'keine' }, { fps: 15, haltung: 'offen', saat: 6 }));
  f.push(folge({
    name: 'synth-pistole-kurz', beschreibung: 'G6: aus der lockeren Hand kurz (0.5 s) die Pistolengeste zeigen, Daumen oben, dann wieder locker.',
    dauer: 1600, schluessel: [k(0, 'locker', P0), k(500, 'locker', P0), k(600, 'pistole', P0), k(1100, 'pistole', P0), k(1200, 'locker', P0), k(1600, 'locker', P0)],
    erwartet: { schritte: [{ geste: 'G6', richtung: null }], navigation: 'keine' },
  }));

  // ================================================================== Störfolgen: nichts darf auslösen
  f.push(folge({
    name: 'synth-stoer-zittern', beschreibung: 'Zittern: lockere Hand 1.5 s, dann eine Faust 2 s ruhig gehalten, mit Zittern der Hand (3 mm, 7 Hz) und doppeltem Rauschen der Erkennung.',
    dauer: 4000, zittern: { amplitude: 0.003, hz: 7 }, rauschen: { bild: 0.003, tiefe: 0.006, welt: 0.004 }, saat: 11,
    schluessel: [k(0, 'locker', P0), k(1500, 'locker', P0), k(1650, 'faust', P0), k(3500, 'faust', P0), k(3650, 'locker', P0), k(4000, 'locker', P0)],
    erwartet: KEINE,
  }));
  {
    // Hände auf der Tastatur am unteren Bildrand: Handfläche nach unten, Finger zur Kamera, tippen (Finger beugen und strecken), seitliche Sprünge zwischen den Tasten
    const T = [-0.08, 0.14, 0.45];
    const s = [];
    for (let i = 0; i <= 24; i++) {
      const t = i * 125;
      const seitlich = i % 6 === 3 ? 0.07 : i % 6 === 4 ? 0.07 : 0;   // ein schneller Sprung von 7 cm in 125 ms und zurück
      s.push(k(t, i % 2 ? 'locker' : 'offen', plus(T, [seitlich + 0.01 * Math.sin(i), 0.004 * (i % 2), 0]), { nick: 85 * GRAD }));
    }
    const eingaben = [];
    for (let t = 1600; t <= 3000; t += 150) eingaben.push(t);   // erst ab 1.6 s tippt die Hand wirklich (Tasten)
    f.push(folge({
      name: 'synth-stoer-tippen-rand', beschreibung: 'Tippen: Hand am unteren Bildrand, Handfläche nach unten, Finger tippen, schnelle seitliche Sprünge; ab 1.6 s mit Tastendrücken.',
      dauer: 3000, schluessel: s, eingaben, saat: 12, erwartet: KEINE,
    }));
  }
  f.push(folge({
    name: 'synth-stoer-tasse', beschreibung: 'Zur Tasse greifen: die Hand kommt mit der Tasse (Griff, Handfläche seitlich) von unten ins Bild, hält beim Trinken, kippt 30°, geht wieder hinaus.',
    dauer: 2800, saat: 13,
    schluessel: [k(0, 'griff', [-0.10, 0.24, 0.5], { gier: 80 * GRAD }), k(700, 'griff', [-0.06, -0.02, 0.45], { gier: 80 * GRAD }), k(1100, 'griff', [-0.06, -0.02, 0.45], { gier: 80 * GRAD }),
      k(1600, 'griff', [-0.06, -0.02, 0.45], { gier: 80 * GRAD, roll: 30 * GRAD }), k(1900, 'griff', [-0.06, -0.02, 0.45], { gier: 80 * GRAD, roll: 30 * GRAD }),
      k(2500, 'griff', [-0.10, 0.26, 0.5], { gier: 80 * GRAD }), k(2800, 'griff', [-0.10, 0.26, 0.5], { gier: 80 * GRAD })],
    erwartet: KEINE,
  }));
  f.push(folge({
    name: 'synth-stoer-aussen', beschreibung: 'Hand nach aussen aus dem Bild: die linke Hand ruht, dann geht sie schnell nach links aus dem Bild (30 cm in 0.35 s).',
    dauer: 1300, saat: 14, schluessel: [k(0, 'locker', P0), k(600, 'locker', P0), k(950, 'locker', plus(P0, [-0.30, 0, 0]), { linear: true }), k(1300, 'locker', plus(P0, [-0.30, 0, 0]))],
    erwartet: KEINE,
  }));
  f.push(folge({
    name: 'synth-stoer-falsche-hand', beschreibung: 'Falsche Hand: die rechte Hand wischt nach rechts und dreht mit der Faust; Gestenhand ist links.',
    hand: 'links', seite: 'rechts', dauer: 3600, saat: 15,
    schluessel: [k(0, 'locker', [0.06, 0.05, 0.5]), k(600, 'locker', [0.06, 0.05, 0.5]), k(750, 'locker', [0.15, 0.05, 0.5]), k(1300, 'locker', [0.15, 0.05, 0.5]),
      k(1450, 'faust', [0.15, 0.05, 0.5]), k(1900, 'faust', [0.15, 0.05, 0.5]), k(2600, 'faust', [0.05, 0.05, 0.5]), k(3000, 'faust', [0.05, 0.05, 0.5]), k(3200, 'offen', [0.05, 0.05, 0.5]), k(3600, 'offen', [0.05, 0.05, 0.5])],
    erwartet: KEINE,
  }));
  f.push(wischFolge('synth-stoer-maus-sperre', 'Sperre neben der Maus: derselbe Wisch nach rechts, aber 120 ms vor dem Anstoss hat die Maus geklickt.', P1, [0.09, 0, 0], KEINE, { eingaben: [480], saat: 2 }));
  f.push(folge({
    name: 'synth-stoer-zurueckholen', beschreibung: 'Zurückholen: Wisch nach rechts, danach holt die Hand zügig zurück nach links – nur ein Wisch (rechts).',
    dauer: 1800, saat: 16,
    schluessel: [k(0, 'locker', P1), k(600, 'locker', P1), k(750, 'locker', plus(P1, [0.09, 0, 0])), k(1000, 'locker', plus(P1, [0.09, 0, 0])), k(1200, 'locker', P1), k(1800, 'locker', P1)],
    erwartet: { schritte: [{ geste: 'G2', richtung: 'rechts' }], navigation: 'keine' },
  }));
  f.push(folge({
    name: 'synth-stoer-hand-hebt-sich', beschreibung: 'Hand ins Bild heben: die offene Hand kommt zügig von unten ins Bild (28 cm in 0.7 s) und bleibt stehen.',
    dauer: 1300, saat: 17, schluessel: [k(0, 'offen', [-0.10, 0.30, 0.5]), k(700, 'offen', [-0.10, 0.02, 0.5]), k(1300, 'offen', [-0.10, 0.02, 0.5])],
    erwartet: KEINE,
  }));
  f.push(folge({
    name: 'synth-stoer-hand-sinkt', beschreibung: 'Hand sinken lassen: die lockere Hand ruht 1 s, dann fällt sie zügig nach unten aus dem Bild (30 cm in 0.4 s).',
    dauer: 1700, saat: 18, schluessel: [k(0, 'locker', P0), k(1000, 'locker', P0), k(1400, 'locker', plus(P0, [0, 0.30, 0]), { nick: 30 * GRAD }), k(1700, 'locker', plus(P0, [0, 0.30, 0]))],
    erwartet: KEINE,
  }));
  f.push(folge({
    name: 'synth-stoer-zeigen-lange', beschreibung: 'Zeigen: Pistolenhaltung mit Daumen oben, aber 2.5 s lang (man zeigt auf etwas) – keine Pistolengeste.',
    dauer: 3500, saat: 19, schluessel: [k(0, 'locker', P0), k(500, 'locker', P0), k(600, 'pistole', P0), k(3100, 'pistole', P0), k(3200, 'locker', P0), k(3500, 'locker', P0)],
    erwartet: KEINE,
  }));
  return f;
}

const hier = path.dirname(fileURLToPath(import.meta.url));
/** Dateiname einer Folge. */
export const dateiName = (folge) => `${folge.name}.json`;
/** Inhalt einer Datei (eine Zeile je Bild, damit Unterschiede lesbar bleiben). */
export function dateiText(folge) {
  const { bilder, ...kopf } = folge;
  return `{\n${Object.entries(kopf).map(([a, b]) => `  ${JSON.stringify(a)}: ${JSON.stringify(b)},`).join('\n')}\n  "bilder": [\n${bilder.map((b) => '    ' + JSON.stringify(b)).join(',\n')}\n  ]\n}\n`;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  for (const d of readdirSync(hier)) if (d.startsWith('synth-') && d.endsWith('.json')) rmSync(path.join(hier, d));
  const folgen = alleFolgen();
  for (const f of folgen) writeFileSync(path.join(hier, dateiName(f)), dateiText(f));
  console.log(`${folgen.length} Testfolgen geschrieben: ${hier}`);
}

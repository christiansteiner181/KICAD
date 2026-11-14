// Erzeugt die mitgelieferte Familie «Geberit Montageelemente» (kataloge/familien/geberit_montageelemente.familie.json) aus kataloge/vorwand_geberit.json.
// Christian 14.11.2026: «wir brauchen eine familie» (Frage 30: Montageelemente als Familie mit eigenen Anschlüssen; «die anschlüsse sind im vorwand element»).
// Je Element des Katalogs ein Typ (Name = Titel), Werte aus dem Katalog (Quellen je Wert dort, Annahmen ebenso). Einfügepunkt = Vorderseite der Vorwand
// in der Mitte des Elements auf OK fertig Boden; die Familie zeigt mit der Vorderseite nach −y (0° = Süden). Anschlüsse 5 mm vor der Beplankung
// (Planungshandbuch S. 163), Richtung −y; das Element selbst liegt um «versatz» (Exemplar: Beplankung bzw. Beplankung + Profil) hinter der Vorderseite.
// Ohne Grösse der Anschlüsse: der Katalog belegt nur den WC-Abgang (d 90, S. 178); ein Familienanschluss kann seine Grösse nicht je Typ weglassen.
// Aufruf: node werkzeuge/montageelemente_familie.mjs

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const kat = JSON.parse(readFileSync(join(repo, 'kataloge', 'vorwand_geberit.json'), 'utf8'));
const elemente = Array.isArray(kat.elemente) ? kat.elemente : Object.entries(kat.elemente).map(([id, v]) => ({ id, ...v }));

const p = (name, art, datentyp, beschreibung, wert) => ({ name, art, datentyp, gruppe: 'abmessung', ...(wert !== undefined ? { wert } : {}), beschreibung });
const parameter = [
  p('breite', 'typ', 'laenge', 'Breite des Elements (Katalog breite)'),
  p('rahmenhoehe', 'typ', 'laenge', 'Höhe des Rahmens (Katalog rahmenhoehe bzw. hoehe)'),
  p('rahmentiefe', 'typ', 'laenge', 'Tiefe des Rahmens (Katalog rahmentiefe)'),
  p('rahmenunterkante', 'typ', 'laenge', 'Unterkante des Rahmens über OK fertig Boden (Katalog rahmenunterkante; Duofix 0 = ab OKFF)', 0),
  ...['sw', 'kw', 'ww'].flatMap((a) => [
    { name: `mit_${a}`, art: 'typ', datentyp: 'zahl', gruppe: 'anschluss', wert: 0, beschreibung: `1 = Anschluss ${a.toUpperCase()} vorhanden` },
    p(`${a}_z`, 'typ', 'laenge', `Höhe des Anschlusses ${a.toUpperCase()} über OK fertig Boden (Katalog anschluesse.${a}.z)`, 0),
    p(`${a}_s`, 'typ', 'laenge', `Seitlicher Versatz des Anschlusses ${a.toUpperCase()} ab Elementmitte (Katalog anschluesse.${a}.s)`, 0),
  ]),
  // Darstellung (14.11.2026, Christian: «die familien sind etwas unschön und nur wenig detailliert»): Werte aus elemente[].darstellung des Katalogs
  // (mit Quelle bzw. Annahme dort), sonst die Vorgaben in «vorgabe» unten (Annahme Darstellung)
  { name: 'ist_wc', art: 'typ', datentyp: 'zahl', gruppe: 'sonstiges', wert: 0, beschreibung: '1 = WC-Element (Spülkasten, Abwasserbogen)' },
  { name: 'mit_bolzen', art: 'typ', datentyp: 'zahl', gruppe: 'sonstiges', wert: 0, beschreibung: '1 = Befestigung des Apparats mit zwei Gewindestangen' },
  p('profil', 'typ', 'laenge', 'Querschnitt der Rahmenprofile (Darstellung)', 40),
  p('spk_b', 'typ', 'laenge', 'Spülkasten Breite', 0), p('spk_h', 'typ', 'laenge', 'Spülkasten Höhe', 0), p('spk_t', 'typ', 'laenge', 'Spülkasten Tiefe', 0),
  p('spk_uk', 'typ', 'laenge', 'Spülkasten Unterkante über OKFF', 0),
  p('bet_b', 'typ', 'laenge', 'Betätigungsöffnung Breite', 0), p('bet_h', 'typ', 'laenge', 'Betätigungsöffnung Höhe', 0), p('bet_mz', 'typ', 'laenge', 'Betätigungsöffnung Mitte über OKFF', 0),
  p('bolzen_a', 'typ', 'laenge', 'Abstand der Gewindestangen (Befestigung des Apparats)', 0), p('bolzen_z', 'typ', 'laenge', 'Höhe der Gewindestangen über OKFF', 0),
  p('bolzen_l', 'typ', 'laenge', 'Überstand der Gewindestangen vor die Beplankung (Darstellung)', 40),
  p('sw_r', 'typ', 'laenge', 'Radius Abwasserbogen bzw. -stutzen (Darstellung; WC d 90 nach Planungshandbuch S. 178)', 25),
  // Duofix (Christian 14.11.2026 mit Massbild: «duofix haben unten keinen durchlaufenden Träger, sondern verstellbare füsse»): zwei Füsse bis auf
  // den Rohboden statt des unteren Riegels; Fussplatte 73 × 48 mm, Fussmitte 195 mm ab Elementmitte (Montageanleitung 972.240.00.0(00) S. 2, Deutung)
  { name: 'mit_fuessen', art: 'typ', datentyp: 'zahl', gruppe: 'sonstiges', wert: 0, beschreibung: '1 = Duofix: verstellbare Füsse statt unterem Riegel' },
  p('fuss', 'exemplar', 'laenge', 'Länge der Füsse unter OK fertig Boden = Bodenaufbau (setzt der Kern beim Bestücken; 0–200 bzw. 0–250 mm verstellbar)', 100),
  p('versatz', 'exemplar', 'laenge', 'Abstand Vorderseite Vorwand → Vorderkante Element (setzt der Kern beim Bestücken: Beplankung bzw. Beplankung + Profil)', 0),
];
const medien = { sw: 'SW', kw: 'KW', ww: 'WW' };
const fluss = { sw: 'raus', kw: 'rein', ww: 'rein' };
const typen = elemente.map((e) => {
  const w = { breite: e.breite, rahmenhoehe: e.rahmenhoehe ?? e.hoehe, rahmentiefe: e.rahmentiefe ?? 40, rahmenunterkante: e.rahmenunterkante ?? 0 };
  for (const a of ['sw', 'kw', 'ww']) {
    const x = e.anschluesse?.[a];
    w[`mit_${a}`] = x ? 1 : 0;
    w[`${a}_z`] = x?.z ?? 0;
    w[`${a}_s`] = x?.s ?? 0;
  }
  const d = e.darstellung ?? {};
  const app = e.apparate ?? [], wc = app.includes('WC'), rh = w.rahmenhoehe, ru = w.rahmenunterkante, b = w.breite;
  const spkH = wc ? Math.min(500, Math.round(rh * 0.45)) : 0;
  const vorgabe = {
    ist_wc: wc ? 1 : 0, mit_bolzen: app.some((x) => ['WC', 'Waschtisch', 'Urinal'].includes(x)) ? 1 : 0, profil: 40,
    spk_b: wc ? b - 2 * 40 - 40 : 0, spk_h: spkH, spk_t: wc ? Math.max(40, (e.rahmentiefe ?? 120) - 20) : 0, spk_uk: wc ? ru + rh - 60 - spkH : 0,
    bet_b: wc ? 250 : 0, bet_h: wc ? 170 : 0, bet_mz: wc ? Math.round(ru + rh - 60 - spkH / 2) : 0,
    bolzen_a: app.some((x) => ['WC', 'Waschtisch', 'Urinal'].includes(x)) ? 180 : 0,
    bolzen_z: e.traverse ?? (wc ? 330 : e.anschluesse?.sw?.z ? e.anschluesse.sw.z + 300 : 0),
    bolzen_l: 40, sw_r: wc ? 45 : 25,
  };
  vorgabe.mit_fuessen = e.system === 'duofix' ? 1 : 0;
  for (const [k2, v] of Object.entries(vorgabe)) w[k2] = typeof d[k2] === 'number' ? d[k2] : v;
  return { name: e.titel, werte: w };
});
const familie = {
  format: 1,
  id: 'geberit-montageelemente',
  name: 'Geberit Montageelemente',
  kategorie: 'Montageelement',
  hersteller: 'Geberit',
  beschreibung: 'Duofix- und GIS-Montageelemente als Familie (Christian 14.11.2026). Typen und Werte aus kataloge/vorwand_geberit.json – Quellen je Wert '
    + '(Planungshandbuch Sanitär 2025 Dok. 995.828.00.1, Produktdatenblätter, Montageanleitung 970.704) und Annahmen stehen dort. Erzeugt mit '
    + 'werkzeuge/montageelemente_familie.mjs – nicht von Hand ändern, sondern den Katalog.',
  version: 1,
  status: 'entwurf',
  parameter,
  typen,
  koerper: [
    // Rahmen aus Profilen: zwei Ständer, oberer und unterer Riegel
    { id: 'staender_links', name: 'Ständer links', art: 'quader', material: 'geraet', von: ['-breite / 2', 'versatz', 'rahmenunterkante'], bis: ['-breite / 2 + profil', 'versatz + rahmentiefe', 'rahmenunterkante + rahmenhoehe'] },
    { id: 'staender_rechts', name: 'Ständer rechts', art: 'quader', material: 'geraet', von: ['breite / 2 - profil', 'versatz', 'rahmenunterkante'], bis: ['breite / 2', 'versatz + rahmentiefe', 'rahmenunterkante + rahmenhoehe'] },
    { id: 'riegel_oben', name: 'Riegel oben', art: 'quader', material: 'geraet', von: ['-breite / 2 + profil', 'versatz', 'rahmenunterkante + rahmenhoehe - profil'], bis: ['breite / 2 - profil', 'versatz + rahmentiefe', 'rahmenunterkante + rahmenhoehe'] },
    { id: 'riegel_unten', name: 'Riegel unten', art: 'quader', material: 'geraet', sichtbar: '1 - mit_fuessen', von: ['-breite / 2 + profil', 'versatz', 'rahmenunterkante'], bis: ['breite / 2 - profil', 'versatz + rahmentiefe', 'rahmenunterkante + profil'] },
    { id: 'fuss_links', name: 'Fuss links', art: 'quader', material: 'geraet', sichtbar: 'mit_fuessen * fuss', von: ['-breite / 2 + 5', 'versatz + rahmentiefe / 2 - 15', '-fuss'], bis: ['-breite / 2 + 35', 'versatz + rahmentiefe / 2 + 15', 'rahmenunterkante'] },
    { id: 'fuss_rechts', name: 'Fuss rechts', art: 'quader', material: 'geraet', sichtbar: 'mit_fuessen * fuss', von: ['breite / 2 - 35', 'versatz + rahmentiefe / 2 - 15', '-fuss'], bis: ['breite / 2 - 5', 'versatz + rahmentiefe / 2 + 15', 'rahmenunterkante'] },
    { id: 'fussplatte_links', name: 'Fussplatte links', art: 'quader', material: 'geraet', sichtbar: 'mit_fuessen * fuss', detailgrad: ['mittel', 'fein'], von: ['-195 - 36.5', 'versatz + rahmentiefe / 2 - 24', '-fuss'], bis: ['-195 + 36.5', 'versatz + rahmentiefe / 2 + 24', '-fuss + 5'] },
    { id: 'fussplatte_rechts', name: 'Fussplatte rechts', art: 'quader', material: 'geraet', sichtbar: 'mit_fuessen * fuss', detailgrad: ['mittel', 'fein'], von: ['195 - 36.5', 'versatz + rahmentiefe / 2 - 24', '-fuss'], bis: ['195 + 36.5', 'versatz + rahmentiefe / 2 + 24', '-fuss + 5'] },
    { id: 'traverse', name: 'Traverse (Befestigung)', art: 'quader', material: 'geraet', sichtbar: 'mit_bolzen', von: ['-breite / 2 + profil', 'versatz', 'bolzen_z - profil / 2'], bis: ['breite / 2 - profil', 'versatz + profil', 'bolzen_z + profil / 2'] },
    // WC: Unterputz-Spülkasten mit Betätigungsöffnung an der Beplankung
    { id: 'spuelkasten', name: 'Spülkasten', art: 'quader', material: 'geraet', sichtbar: 'ist_wc', von: ['-spk_b / 2', 'versatz + rahmentiefe - spk_t', 'spk_uk'], bis: ['spk_b / 2', 'versatz + rahmentiefe', 'spk_uk + spk_h'] },
    { id: 'betaetigung', name: 'Betätigungsöffnung', art: 'quader', material: 'geraet', sichtbar: 'ist_wc', detailgrad: ['mittel', 'fein'], von: ['-bet_b / 2', -8, 'bet_mz - bet_h / 2'], bis: ['bet_b / 2', 'versatz + rahmentiefe - spk_t', 'bet_mz + bet_h / 2'] },
    // Befestigung des Apparats: zwei Gewindestangen durch die Beplankung
    { id: 'bolzen_links', name: 'Gewindestange links', art: 'zylinder', material: 'geraet', sichtbar: 'mit_bolzen', detailgrad: ['mittel', 'fein'], von: ['-bolzen_a / 2', 'versatz', 'bolzen_z'], nach: ['-bolzen_a / 2', '-bolzen_l', 'bolzen_z'], r: 6 },
    { id: 'bolzen_rechts', name: 'Gewindestange rechts', art: 'zylinder', material: 'geraet', sichtbar: 'mit_bolzen', detailgrad: ['mittel', 'fein'], von: ['bolzen_a / 2', 'versatz', 'bolzen_z'], nach: ['bolzen_a / 2', '-bolzen_l', 'bolzen_z'], r: 6 },
    // Abwasser: WC-Bogen nach unten, sonst Stutzen nach hinten
    { id: 'abwasserbogen', name: 'Abwasserbogen', art: 'sweep', material: 'rohr', sichtbar: 'ist_wc', profil: { kreis: 'sw_r' },
      pfad: [['sw_s', -5, 'sw_z'], ['sw_s', 'versatz + rahmentiefe - sw_r', 'sw_z'], ['sw_s', 'versatz + rahmentiefe - sw_r', 'rahmenunterkante']], biegeradius: 'sw_r' },
    { id: 'abwasserstutzen', name: 'Abwasserstutzen', art: 'zylinder', material: 'rohr', sichtbar: 'mit_sw * (1 - ist_wc)', von: ['sw_s', -5, 'sw_z'], nach: ['sw_s', 'versatz + rahmentiefe / 2', 'sw_z'], r: 'sw_r' },
    // Wasseranschlüsse: Stutzen von der Beplankung zum Rahmen
    { id: 'stutzen_kw', name: 'Anschluss KW', art: 'zylinder', material: 'rohr', sichtbar: 'mit_kw', von: ['kw_s', -5, 'kw_z'], nach: ['kw_s', 'versatz + profil', 'kw_z'], r: 10 },
    { id: 'stutzen_ww', name: 'Anschluss WW', art: 'zylinder', material: 'rohr', sichtbar: 'mit_ww', von: ['ww_s', -5, 'ww_z'], nach: ['ww_s', 'versatz + profil', 'ww_z'], r: 10 },
  ],
  anschluesse: ['sw', 'kw', 'ww'].map((a) => ({
    name: a, medium: medien[a], fluss: fluss[a], art: 'rohr', form: 'rund',
    lage: [`${a}_s`, -5, `${a}_z`], richtung: [0, -1, 0], sichtbar: `mit_${a}`,
    beschreibung: `${a.toUpperCase()} – 5 mm vor der Beplankung (Planungshandbuch S. 163)`,
  })),
};
const ziel = join(repo, 'kataloge', 'familien', 'geberit_montageelemente.familie.json');
mkdirSync(dirname(ziel), { recursive: true });
writeFileSync(ziel, JSON.stringify(familie, null, 2) + '\n', 'utf8');
console.log(`${typen.length} Typen → ${ziel}`);

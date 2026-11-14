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
    { id: 'rahmen', name: 'Rahmen', art: 'quader', material: 'geraet', von: ['-breite / 2', 'versatz', 'rahmenunterkante'], bis: ['breite / 2', 'versatz + rahmentiefe', 'rahmenunterkante + rahmenhoehe'] },
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

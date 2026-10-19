// Bedienungsanleitung bauen (CLAUDE.md «Bedienungsanleitung nach jedem Modul», seit 19.10.2026): setzt aus dem
// Funktionsverzeichnis berichte/anleitung/inventar.json, den Lücken berichte/anleitung/luecken.json und den Bildern des
// Bildskripts (werkzeuge/anleitung_bilder.mjs → bilder.json) eine Seite zusammen, die KICAD selbst unter http://localhost:<port>/hilfe/
// ausliefert (Entscheid Christian Steiner 19.10.2026; AnleitungEndpunkte.cs). Vorlage: werkzeuge/anleitung/vorlage.html (Platzhalter __DATEN__).
//
// Aufruf: node werkzeuge/anleitung_bauen.mjs [--aus <Ordner>] [--repo <Ordner>] [--commit <sha>] [--datum <TT.MM.JJJJ>] [--bilder <Ordner mit bilder.json>] [--text "…"]
//   --aus     Zielordner (index.html + bilder/ + stand.txt). Vorgabe: HLKS_ANLEITUNG, sonst kataloge/buero.json › anleitung.ordner
//             (%…%-Variablen werden aufgelöst; heute %USERPROFILE%\HLKS_CAD_anleitung). NIE im Repo oder auf OneDrive – die Bilder
//             enthalten echte Projekte (Bernstrasse).
//   --repo    Arbeitskopie, aus der Inventar, Lücken, Vorlage und buero.json kommen (Vorgabe: der Ordner über diesem Skript). Der Desktop-Start
//             baut so aus seiner Arbeitskopie %USERPROFILE%\HLKS_CAD_desktop.
//   --bilder  Vorgabe %USERPROFILE%\HLKS_CAD_testdaten\anleitung\bilder
//   --commit  geprüfter Stand, den die Seite beschreibt; wird auch in <Zielordner>\stand.txt gemerkt (der Desktop-Start baut neu, wenn er abweicht)
// Die Seite wird erst nach dem Bau der Bilder ersetzt (temporäre Datei, dann umbenennen): ein laufendes KICAD liefert nie eine halbe Seite.
import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync, renameSync } from 'node:fs';
import { join, basename, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const skriptRepo = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const arg = name => { const i = process.argv.indexOf('--' + name); return i > 0 ? process.argv[i + 1] : undefined; };
const repo = resolve(arg('repo') || skriptRepo);
const lies = p => JSON.parse(readFileSync(p, 'utf8').replace(/^﻿/, ''));
/** %NAME% durch die Umgebungsvariable ersetzen (Gross/Klein egal wie unter Windows); unbekannte Variablen bleiben stehen. */
const umgebung = t => String(t).replace(/%([^%]+)%/g, (m, n) => {
  const k = Object.keys(process.env).find(x => x.toLowerCase() === n.toLowerCase());
  return k ? process.env[k] : m;
});

// Zielordner: --aus › HLKS_ANLEITUNG › buero.json
let aus = arg('aus') || process.env.HLKS_ANLEITUNG;
if (!aus) {
  try { aus = lies(join(repo, 'kataloge', 'buero.json'))?.anleitung?.ordner; } catch { /* unten gemeldet */ }
}
if (!aus) { console.error('Zielordner fehlt: --aus <Ordner>, HLKS_ANLEITUNG oder kataloge/buero.json › anleitung.ordner angeben.'); process.exit(2); }
aus = umgebung(aus);
if (aus.includes('%')) { console.error(`Im Zielordner ${aus} ist eine Umgebungsvariable nicht gesetzt.`); process.exit(2); }
const ausVoll = resolve(aus);
const gesperrt = [repo, skriptRepo, process.env.OneDrive, process.env.OneDriveCommercial, process.env.OneDriveConsumer].filter(Boolean).map(p => resolve(p).toLowerCase() + '\\');
if (gesperrt.some(p => (ausVoll.toLowerCase() + '\\').startsWith(p))) {
  console.error(`Zielordner ${ausVoll} liegt im Repo bzw. auf OneDrive – Bilder echter Projekte gehören nicht dorthin.`); process.exit(2);
}
const bilderOrdner = arg('bilder') || join(process.env.USERPROFILE || '', 'HLKS_CAD_testdaten', 'anleitung', 'bilder');

const inventar = lies(join(repo, 'berichte', 'anleitung', 'inventar.json'));
const lueckenDatei = join(repo, 'berichte', 'anleitung', 'luecken.json');
const luecken = existsSync(lueckenDatei) ? lies(lueckenDatei).luecken || [] : [];
const bilder = existsSync(join(bilderOrdner, 'bilder.json')) ? lies(join(bilderOrdner, 'bilder.json')) : { bilder: [] };

mkdirSync(join(ausVoll, 'bilder'), { recursive: true });
const bildNach = new Map();
let kopiert = 0;
for (const b of bilder.bilder || []) {
  const quelle = join(bilderOrdner, b.datei);
  if (!existsSync(quelle)) { console.warn('Bild fehlt:', quelle); continue; }
  const name = basename(b.datei);
  copyFileSync(quelle, join(ausVoll, 'bilder', name)); kopiert++;
  for (const id of b.ids || []) bildNach.set(id, { datei: 'bilder/' + name, b: b.breite, h: b.hoehe, projekt: b.projekt, markierung: !!b.markierung, text: b.titel || '' });
}

let funktionen = 0, mitBild = 0;
const kapitel = inventar.kapitel.map(k => ({
  id: k.id, titel: k.titel, modul: k.modul, einleitung: k.einleitung,
  funktionen: k.funktionen.map(f => {
    funktionen++;
    const bild = bildNach.get(f.id) || null; if (bild) mitBild++;
    return { id: f.id, titel: f.titel, wo: f.wo, schritte: f.schritte, erwartung: f.erwartung, pruefpunkte: f.pruefpunkte || [], hinweis: f.hinweis || '', bild };
  }),
}));

const daten = {
  stand: { commit: arg('commit') || inventar.stand || '', datum: arg('datum') || '', text: arg('text') || '' },
  kapitel, luecken,
};
const json = JSON.stringify(daten).replace(/</g, '\\u003c');
const vorlage = readFileSync(join(repo, 'werkzeuge', 'anleitung', 'vorlage.html'), 'utf8');
if (!vorlage.includes('__DATEN__')) { console.error('Platzhalter __DATEN__ fehlt in der Vorlage'); process.exit(2); }
const index = join(ausVoll, 'index.html');
const seite = vorlage.replace('__DATEN__', () => json);
writeFileSync(index + '.tmp', seite, 'utf8');
try { renameSync(index + '.tmp', index); } catch { writeFileSync(index, seite, 'utf8'); }   // Rückfall, falls Windows das Ersetzen einer gerade gelesenen Datei verweigert
if (arg('commit')) writeFileSync(join(ausVoll, 'stand.txt'), arg('commit') + '\n', 'utf8');
console.log(`Seite ${index}: ${kapitel.length} Kapitel, ${funktionen} Funktionen, ${mitBild} mit Bild, ${kopiert} Bilddateien, ${luecken.length} Lücken`);

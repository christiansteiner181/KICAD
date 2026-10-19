// Bedienungsanleitung bauen (CLAUDE.md «Bedienungsanleitung nach jedem Modul», seit 19.10.2026): setzt aus dem
// Funktionsverzeichnis berichte/anleitung/inventar.json, den Lücken berichte/anleitung/luecken.json und den Bildern des
// Bildskripts (werkzeuge/anleitung_bilder.mjs → bilder.json) eine Seite zusammen, die der Hauptlauf als privates
// Artifact veröffentlicht. Vorlage: werkzeuge/anleitung/vorlage.html (Platzhalter __DATEN__).
//
// Aufruf: node werkzeuge/anleitung_bauen.mjs --aus <Ordner> --commit <sha> --datum <TT.MM.JJJJ> [--bilder <Ordner mit bilder.json>] [--text "…"]
//   --bilder  Vorgabe %USERPROFILE%\HLKS_CAD_testdaten\anleitung\bilder
//   --aus     Zielordner (index.html + bilder/); NIE im Repo oder auf OneDrive – die Bilder enthalten echte Projekte
//             (Bernstrasse). Der Hauptlauf nimmt sein Scratchpad.
import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync } from 'node:fs';
import { join, basename, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const arg = name => { const i = process.argv.indexOf('--' + name); return i > 0 ? process.argv[i + 1] : undefined; };
const aus = arg('aus');
if (!aus) { console.error('--aus <Ordner> fehlt'); process.exit(2); }
const ausVoll = resolve(aus);
const gesperrt = [repo, process.env.OneDrive, process.env.OneDriveCommercial, process.env.OneDriveConsumer].filter(Boolean).map(p => resolve(p).toLowerCase() + '\\');
if (gesperrt.some(p => (ausVoll.toLowerCase() + '\\').startsWith(p))) {
  console.error(`Zielordner ${ausVoll} liegt im Repo bzw. auf OneDrive – Bilder echter Projekte gehören nicht dorthin.`); process.exit(2);
}
const bilderOrdner = arg('bilder') || join(process.env.USERPROFILE || '', 'HLKS_CAD_testdaten', 'anleitung', 'bilder');
const lies = p => JSON.parse(readFileSync(p, 'utf8').replace(/^﻿/, ''));

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
writeFileSync(join(ausVoll, 'index.html'), vorlage.replace('__DATEN__', () => json), 'utf8');
console.log(`Seite ${join(ausVoll, 'index.html')}: ${kapitel.length} Kapitel, ${funktionen} Funktionen, ${mitBild} mit Bild, ${kopiert} Bilddateien, ${luecken.length} Lücken`);

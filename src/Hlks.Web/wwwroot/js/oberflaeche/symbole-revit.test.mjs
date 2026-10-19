// Tests des Symbolsatzes im Revit-Stil (Teil S, entwurf/menueband-revit.md) ohne Browser: Vollständigkeit gegenüber symbole.js und werkzeuge/symbole.js, Grössen (32 und 16, je eigens gezeichnet),
// gültiges SVG, keine festen Farben (nur Klassen aus css/symbole-revit.css, Werte als CSS-Variablen für hell und dunkel), Zuordnungstabelle, Umschalten von symbol()/symbolWz().
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/symbole-revit.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as rv from './symbole-revit.js';
import * as strich from './symbole.js';
import * as wz from '../werkzeuge/symbole.js';
import * as stil from './symbolstil.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');
const css = lies('../../css/symbole-revit.css');
const strichNamen = [...strich.SYMBOLNAMEN_STRICH, ...wz.SYMBOLNAMEN];

// Symbole, die es beim Bau dieses Satzes gab (Stand 19.10.2026): jedes muss eine Revit-Fassung haben. Kommt in symbole.js später ein Symbol dazu, gilt bis zu seiner Zeichnung der Strich.
const STAND = rv.ZUORDNUNG.map((z) => z[0]);

// ------------------------------------------------------------------------------------------------ Vollständigkeit

test('Jedes Symbol aus symbole.js und werkzeuge/symbole.js (Stand der Zuordnungstabelle) hat eine Revit-Fassung in 32 und 16', () => {
  const fehlt = [];
  for (const n of strichNamen) {
    if (!STAND.includes(n)) continue;   // später dazugekommen: Strich bleibt, bis es gezeichnet ist (siehe nächster Test)
    if (!rv.hatRevitFassung(n)) { fehlt.push(n); continue; }
    if (!rv.revitInhalt(n, false) || !rv.revitInhalt(n, true)) fehlt.push(`${n} (Grösse)`);
  }
  assert.deepEqual(fehlt, []);
  assert.ok(STAND.length >= 150, `Tabelle hat ${STAND.length} Zeilen`);
});

test('Symbole, die der Strichsatz inzwischen mehr hat, sind erlaubt (Rückfall auf Strich); alle Schlüssel der Tabelle gibt es im Strichsatz', () => {
  const neu = strichNamen.filter((n) => !rv.hatRevitFassung(n));
  if (neu.length) console.log('# Hinweis: ohne Revit-Fassung (Strich bleibt):', neu.join(' '));
  const unbekannt = STAND.filter((n) => !strichNamen.includes(n));
  assert.deepEqual(unbekannt, [], 'Schlüssel der Tabelle ohne Strichsymbol');
});

test('REVIT_NAMEN und Zuordnungstabelle sind deckungsgleich; je Zeile vier Angaben; Schlüssel einmalig', () => {
  assert.deepEqual([...rv.REVIT_NAMEN].sort(), [...STAND].sort());
  assert.equal(new Set(STAND).size, STAND.length, 'Schlüssel doppelt in der Tabelle');
  for (const z of rv.ZUORDNUNG) {
    assert.equal(z.length, 4, `Zeile «${z[0]}» hat ${z.length} Spalten`);
    for (const t of z) assert.ok(t.length > 0, `leere Angabe in Zeile «${z[0]}»`);
  }
});

test('Zuordnungstabelle: «eigenes» steht für Befehle ohne Revit-Entsprechung (Kollisionen, Aussparungen, HKAP, Familie aus Unterlagen) – diese tragen keinen Revit-Namen', () => {
  const zeile = (k) => rv.ZUORDNUNG.find((z) => z[0] === k);
  for (const k of ['koAusVorschlagen', 'koAusNummer', 'koAusListe', 'hkapVerbinden', 'hkapAbgleich', 'hkapBefunde', 'famUnterlagen']) assert.equal(zeile(k)[2], 'eigenes', k);
  assert.equal(zeile('wand')[2], 'Wand');
  assert.equal(zeile('rohr')[3].includes('Zylinder'), true);
});

// ------------------------------------------------------------------------------------------------ SVG

/** Kleiner Prüfer für das SVG-Innenleben: nur svg/path/rect/circle/ellipse, ausgewogen, Attribute in Anführungszeichen, Pfade syntaktisch ok, Masse im Zeichenfeld. */
function pruefeSvg(text, grenze, name) {
  const tokens = [...text.matchAll(/<(\/?)([a-z]+)((?:\s+[a-zA-Z-]+="[^"]*")*)\s*(\/?)>/g)];
  const ohneTags = text.replace(/<(\/?)([a-z]+)((?:\s+[a-zA-Z-]+="[^"]*")*)\s*(\/?)>/g, '');
  assert.equal(ohneTags.trim(), '', `${name}: Text ausserhalb von Tags oder kaputtes Tag: «${ohneTags.slice(0, 40)}»`);
  const stapel = [];
  const ERLAUBT = new Set(['svg', 'path', 'rect', 'circle', 'ellipse']);
  for (const [, zu, tag, attrs, selbst] of tokens) {
    assert.ok(ERLAUBT.has(tag), `${name}: Element <${tag}> nicht erlaubt`);
    if (zu) { assert.equal(stapel.pop(), tag, `${name}: Tag </${tag}> ohne Gegenstück`); continue; }
    const a = Object.fromEntries([...attrs.matchAll(/([a-zA-Z-]+)="([^"]*)"/g)].map((m) => [m[1], m[2]]));
    for (const k of Object.keys(a)) assert.ok(['class', 'd', 'x', 'y', 'width', 'height', 'cx', 'cy', 'r', 'rx', 'ry', 'viewBox', 'data-sym', 'aria-hidden'].includes(k), `${name}: Attribut «${k}» nicht erlaubt (Farben gehören in die Klassen)`);
    if (tag === 'path') {
      assert.ok(a.d, `${name}: Pfad ohne d`);
      assert.match(a.d, /^[MmLlHhVvCcSsQqTtAaZz0-9eE.,\s-]+$/, `${name}: Pfad enthält ungültige Zeichen: ${a.d}`);
      assert.match(a.d, /^\s*[Mm]/, `${name}: Pfad beginnt nicht mit M`);
      for (const z of a.d.match(/-?\d*\.?\d+/g) ?? []) assert.ok(Math.abs(Number(z)) <= grenze + 0.01, `${name}: Zahl ${z} ausserhalb 0…${grenze}`);
      // Absolute Anfangskoordinate im Feld
      const m = a.d.match(/^\s*M\s*(-?\d*\.?\d+)[\s,]*(-?\d*\.?\d+)/);
      if (m) assert.ok(Number(m[1]) >= -0.01 && Number(m[1]) <= grenze + 0.01 && Number(m[2]) >= -0.01 && Number(m[2]) <= grenze + 0.01, `${name}: Pfad beginnt ausserhalb des Felds (${m[1]}, ${m[2]})`);
    } else if (tag === 'rect') {
      const [x, y, w, h] = ['x', 'y', 'width', 'height'].map((k) => Number(a[k]));
      assert.ok([x, y, w, h].every(Number.isFinite), `${name}: Rechteck unvollständig`);
      assert.ok(x >= -0.01 && y >= -0.01 && x + w <= grenze + 0.01 && y + h <= grenze + 0.01, `${name}: Rechteck ausserhalb des Felds`);
    } else if (tag === 'circle' || tag === 'ellipse') {
      const cx = Number(a.cx), cy = Number(a.cy), rx = Number(tag === 'circle' ? a.r : a.rx), ry = Number(tag === 'circle' ? a.r : a.ry);
      assert.ok([cx, cy, rx, ry].every(Number.isFinite), `${name}: Kreis/Ellipse unvollständig`);
      assert.ok(cx - rx >= -0.01 && cy - ry >= -0.01 && cx + rx <= grenze + 0.01 && cy + ry <= grenze + 0.01, `${name}: Kreis/Ellipse ausserhalb des Felds`);
    }
    if (!selbst && tag !== 'svg') assert.fail(`${name}: <${tag}> nicht selbstschliessend`);
    if (!selbst) stapel.push(tag);
  }
  assert.deepEqual(stapel, [], `${name}: nicht geschlossene Tags`);
}

test('Jede Revit-Fassung ist ein gültiges SVG: viewBox 0 0 32 32 bzw. 0 0 16 16, nur erlaubte Elemente, Pfade syntaktisch ok, alles im Zeichenfeld', () => {
  for (const n of rv.REVIT_NAMEN) {
    for (const [px, k] of [[32, 32], [16, 16]]) {
      const svg = rv.revitSymbol(n, px);
      assert.ok(svg.startsWith(`<svg class="sym sym-rv" data-sym="${n}" viewBox="0 0 ${k} ${k}" width="${px}" height="${px}" aria-hidden="true">`), `${n}: Kopf des SVG (${px})`);
      assert.ok(svg.endsWith('</svg>'));
      const innen = svg.slice(svg.indexOf('>') + 1, -'</svg>'.length);
      assert.ok(innen.length > 20, `${n} ${k}: Zeichnung leer`);
      pruefeSvg(innen, k, `${n} ${k}`);
    }
  }
});

test('Grössen: ab 21 px die 32-er Zeichnung, sonst die 16-er; die angezeigte Grösse steht in width und height; unbekannte Namen ergeben null', () => {
  assert.match(rv.revitSymbol('wand', 32), /viewBox="0 0 32 32" width="32" height="32"/);
  assert.match(rv.revitSymbol('wand', 28), /viewBox="0 0 32 32" width="28" height="28"/);
  assert.match(rv.revitSymbol('wand', 16), /viewBox="0 0 16 16" width="16" height="16"/);
  assert.match(rv.revitSymbol('anleitung', 18), /viewBox="0 0 16 16" width="18" height="18"/);
  assert.match(rv.revitSymbol('wand'), /viewBox="0 0 32 32" width="28"/);
  assert.equal(rv.revitSymbol('gibt-es-nicht', 32), null);
  assert.equal(rv.revitInhalt('gibt-es-nicht', true), null);
  assert.equal(rv.hatRevitFassung('constructor'), false);
});

test('Die 16-er Zeichnung ist eigens gezeichnet: bei keinem Symbol gleich der 32-er, und sie benutzt nur kleine Koordinaten', () => {
  for (const n of rv.REVIT_NAMEN) {
    const a = rv.revitInhalt(n, false), b = rv.revitInhalt(n, true);
    assert.notEqual(a, b, `${n}: 16 ist gleich 32`);
    const zahlen = (b.match(/-?\d*\.?\d+/g) ?? []).map(Number);
    assert.ok(zahlen.every((z) => Math.abs(z) <= 16.01), `${n}: 16-er Zeichnung mit Zahl über 16 (verkleinert statt gezeichnet?)`);
  }
});

// ------------------------------------------------------------------------------------------------ Farben

const KLASSEN_CSS = new Set([...css.matchAll(/svg\.sym-rv \.([A-Za-z0-9]+)\s*\{/g)].map((m) => m[1]));
const FARBKLASSEN = [...KLASSEN_CSS].filter((k) => /^(f(?!p)|s)[a-zA-Z0-9]*$/.test(k) && !['fp'].includes(k) && !/^w\d/.test(k));

test('Keine festen Farben in den Zeichnungen: weder fill/stroke/style noch #-Farben, rgb() oder currentColor; Dokumentierte Ausnahmen: keine', () => {
  for (const n of rv.REVIT_NAMEN) {
    for (const k of [true, false]) {
      const t = rv.revitInhalt(n, k);
      assert.ok(!/\b(fill|stroke|style|opacity|stroke-width|stroke-dasharray)=/.test(t), `${n}: Darstellungsattribut statt Klasse`);
      assert.ok(!/#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|currentColor|var\(/.test(t), `${n}: feste Farbe`);
    }
  }
});

test('Jede verwendete Klasse ist in css/symbole-revit.css definiert', () => {
  const benutzt = new Set();
  for (const n of rv.REVIT_NAMEN) for (const k of [true, false]) for (const m of rv.revitInhalt(n, k).matchAll(/class="([^"]*)"/g)) for (const c of m[1].split(/\s+/)) benutzt.add(c);
  const undefiniert = [...benutzt].filter((c) => !KLASSEN_CSS.has(c));
  assert.deepEqual(undefiniert, [], 'Klassen ohne Regel');
  assert.ok(benutzt.size >= 20);
});

test('CSS: alle --sy-Variablen, die die Regeln benutzen, sind für hell (:root) und dunkel (:root[data-design="dunkel"]) definiert; Hell- und Dunkelwert unterscheiden sich, ausser Weiss', () => {
  const hell = css.match(/:root\s*\{([^}]*)\}/)[1];
  const dunkel = css.match(/:root\[data-design="dunkel"\]\s*\{([^}]*)\}/)[1];
  const defs = (t) => Object.fromEntries([...t.matchAll(/(--sy-[a-z-]+)\s*:\s*([^;]+);/g)].map((m) => [m[1], m[2].trim()]));
  const h = defs(hell), d = defs(dunkel);
  const benutzt = new Set([...css.matchAll(/var\((--sy-[a-z-]+)\)/g)].map((m) => m[1]));
  for (const v of benutzt) { assert.ok(v in h, `${v} fehlt für hell`); assert.ok(v in d, `${v} fehlt für dunkel`); }
  for (const v of Object.keys(h)) { assert.ok(v in d, `${v} nur für hell`); if (v !== '--sy-weiss') assert.notEqual(h[v], d[v], `${v}: gleicher Wert in hell und dunkel`); }
  for (const w of Object.values({ ...h, ...d })) assert.match(w, /^#[0-9a-f]{6}$/i, `Farbwert ${w}`);
  // Medienfarben wie im Katalog (kataloge/medien.json): Heizung Vorlauf rot, Rücklauf blau, Sanitär grün, Abwasser braun, Luft Zuluft rot, Abluft gelb, Aussenluft grün
  for (const v of ['--sy-heiz-vl', '--sy-heiz-rl', '--sy-sanitaer', '--sy-abwasser', '--sy-luft-zu', '--sy-luft-ab', '--sy-luft-aussen', '--sy-blau', '--sy-rot', '--sy-gelb', '--sy-orange']) assert.ok(v in h, `Variable ${v}`);
});

test('Mehrfarbig: jedes Symbol benutzt mindestens eine Farbklasse (Blau, Medienfarbe, Gelb/Orange, Rot) oder abgestufte Grautöne (f1 f2 f3)', () => {
  const grau = new Set(['f1', 'f2', 'f3']);
  const farbe = new Set(['fb', 'fl', 'fr', 'fy', 'fo', 'fvl', 'frl', 'fsa', 'fab', 'fzu', 'fal', 'fau', 'sb', 'sr', 'sy', 'so', 'svl', 'srl', 'ssa', 'sab', 'szu', 'sal', 'sau']);
  let ohneFarbe = [];
  for (const n of rv.REVIT_NAMEN) {
    for (const k of [true, false]) {
      const klassen = new Set([...rv.revitInhalt(n, k).matchAll(/class="([^"]*)"/g)].flatMap((m) => m[1].split(/\s+/)));
      const hatFarbe = [...klassen].some((c) => farbe.has(c));
      const hatGrau = [...klassen].filter((c) => grau.has(c)).length >= 2;
      if (!hatFarbe && !hatGrau) ohneFarbe.push(`${n} ${k ? 16 : 32}`);
    }
  }
  assert.deepEqual(ohneFarbe, []);
  assert.ok(FARBKLASSEN.length > 10);
});

test('Medienfarben: Rohr in Heizungsrot, Formteil in Sanitärgrün, Kanal mit Strömungspfeil in Luftfarbe, Apparat mit Vor- und Rücklauf, Löschen in Rot', () => {
  assert.match(rv.revitInhalt('rohr', false), /class="fvl"/);
  assert.match(rv.revitInhalt('formteil', false), /class="fsa"/);
  assert.match(rv.revitInhalt('kanal', false), /fzu/);
  assert.match(rv.revitInhalt('apparat', false), /fvl/);
  assert.match(rv.revitInhalt('apparat', false), /frl/);
  assert.match(rv.revitInhalt('loeschen', false), /class="sr /);
  assert.match(rv.revitInhalt('loeschen', true), /class="sr /);
});

// ------------------------------------------------------------------------------------------------ Umschalten

test('symbol() und symbolWz(): Vorgabe «strich» liefert unverändert den Strich; «revit» liefert die Revit-Fassung; zurück auf «strich» stellt den Strich wieder her', () => {
  assert.equal(stil.symbolstil(), 'strich');
  const s0 = strich.symbol('wand', 32), w0 = wz.symbolWz('achse', 16);
  assert.match(s0, /stroke="currentColor"/);
  assert.ok(!s0.includes('sym-rv'));
  assert.match(s0, /data-sym="wand"/);
  assert.match(w0, /data-sym="achse"/);
  stil.symbolstilSetzen('revit');
  try {
    assert.equal(stil.symbolstil(), 'revit');
    assert.equal(strich.symbol('wand', 32), rv.revitSymbol('wand', 32));
    assert.equal(strich.symbol('wand', 16), rv.revitSymbol('wand', 16));
    assert.equal(wz.symbolWz('achse', 32), rv.revitSymbol('achse', 32));
    assert.equal(wz.symbolWz('gibt-es-nicht', 32), null);
    assert.match(strich.symbol('gibt-es-nicht', 32), /stroke-dasharray="3 3"/, 'unbekannter Name: neutrales Quadrat wie bisher');
    assert.ok(!/data-sym/.test(strich.symbol('gibt-es-nicht', 32)));
    assert.ok(!/sym-rv/.test(strich.symbol('gibt-es-nicht', 32)));
  } finally { stil.symbolstilSetzen('strich'); }
  assert.equal(strich.symbol('wand', 32), s0);
  assert.equal(wz.symbolWz('achse', 16), w0);
});

test('symbolstilSetzen: nur «revit» und «strich»; alles andere ergibt «strich»', () => {
  assert.equal(stil.symbolstilSetzen('revit'), 'revit');
  assert.equal(stil.revitAktiv(), true);
  assert.equal(stil.symbolstilSetzen('bunt'), 'strich');
  assert.equal(stil.symbolstilSetzen(undefined), 'strich');
  assert.equal(stil.revitAktiv(), false);
  assert.deepEqual(stil.SYMBOLSTILE, ['strich', 'revit']);
  assert.equal(stil.EINSTELLUNG_SYMBOLE, 'oberflaeche.symbole');
});

test('Ohne Revit-Fassung (später dazugekommenes Symbol) fällt symbol() auf den Strich zurück', () => {
  // Ein Name, den es im Strichsatz gibt, aber nicht im Revit-Satz, wird hier nachgestellt: der Rückfall hängt nur an ersatzSymbol() = null
  stil.symbolstilSetzen('revit');
  try { assert.equal(stil.ersatzSymbol('gibt-es-nicht', 32), null); } finally { stil.symbolstilSetzen('strich'); }
  assert.equal(stil.ersatzSymbol('wand', 32), null, 'bei «strich» nie ein Ersatz');
});

// ------------------------------------------------------------------------------------------------ Verdrahtung

test('Verdrahtung: index.html lädt das Stilblatt, app.js richtet den Symbolsatz ein, der Schalter steht in Ansicht › Paletten (kein neues Register, keine neue Gruppe)', () => {
  assert.match(lies('../../index.html'), /href="css\/symbole-revit\.css"/);
  assert.match(lies('../app.js'), /symbolstilEinrichten\(\)/);
  const q = lies('./aktionen-paletten.js');
  assert.match(q, /Symbole im Revit-Stil \(Probe\)/);
  assert.match(q, /symbolstilUmschalten\(\)/);
  const u = lies('./symbolstil-umschalten.js');
  assert.match(u, /benutzerSetzen\(EINSTELLUNG_SYMBOLE/);
  assert.match(u, /benutzerWert\(EINSTELLUNG_SYMBOLE/);
  assert.match(lies('./symbolstil.js'), /oberflaeche\.symbole/);
  const m = lies('./menueband.js');
  assert.ok(!/symbole-revit|symbolstil/.test(m), 'das Menüband bleibt unverändert: der Satz wird in symbol()/symbolWz() gewählt');
});

test('OBERFLAECHE.md: Abschnitt «Symbole im Revit-Stil (Teil S)» mit Schalter, Farben und Zuordnung', () => {
  const t = lies('../../../OBERFLAECHE.md');
  assert.match(t, /## Symbole im Revit-Stil \(Teil S\)/);
  assert.match(t, /oberflaeche\.symbole/);
  assert.match(t, /symbole_kontaktbogen\.mjs/);
});

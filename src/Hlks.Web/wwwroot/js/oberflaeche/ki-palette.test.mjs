// Tests der KI-Palette «Claude» (Modul 3c, Teil O) ohne Browser: Formatierung (inkl. Einschleusungsversuche), Links, Kontext und Chips (auch mit Internet), Texte,
// Merken (nur Wahl, keine Nachrichten), Zustand des Verlaufs und Ereignisverarbeitung (Streaming zusammensetzen, doppelte und verspätete Ereignisse, Reihenfolge),
// Verdrahtung (Palette angemeldet, Ereignis «ki» im Strom, Kürzel KI frei, Symbol «claude», kein innerHTML mit fremdem Text).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/ki-palette.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as T from './ki-text.js';
import * as Z from './ki-zustand.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.join(hier, rel), 'utf8');

/** Alle Knoten eines Block-/Inline-Baums, flach. */
function knoten(x, aus = []) {
  if (Array.isArray(x)) { for (const k of x) knoten(k, aus); return aus; }
  if (!x || typeof x !== 'object') return aus;
  aus.push(x);
  for (const f of ['kinder', 'bloecke', 'kopf', 'zeilen']) if (x[f]) knoten(x[f], aus);
  if (x.punkte) for (const p of x.punkte) knoten(p.kinder, aus);
  return aus;
}
const ERLAUBT = new Set(['text', 'umbruch', 'code', 'fett', 'kursiv', 'link', 'absatz', 'ueberschrift', 'linie', 'zitat', 'liste', 'tabelle']);
const art = (k) => k.t ?? k.art;

// ------------------------------------------------------------------------------------------------ Links

test('linkSicher: nur absolute http-, https- und mailto-Adressen', () => {
  assert.equal(T.linkSicher('https://www.sia.ch/de/'), 'https://www.sia.ch/de/');
  assert.equal(T.linkSicher('http://localhost:5179/hilfe/'), 'http://localhost:5179/hilfe/');
  assert.equal(T.linkSicher('mailto:christian.steiner@hkplanung.ch'), 'mailto:christian.steiner@hkplanung.ch');
  for (const schlecht of ['javascript:alert(1)', 'JaVaScRiPt:alert(1)', ' javascript:alert(1)', 'java\tscript:alert(1)', 'java\nscript:alert(1)', 'data:text/html;base64,PHNjcmlwdD4=',
    'vbscript:msgbox(1)', 'file:///C:/Windows/win.ini', '//evil.example/x', '/api/status', 'relativ/pfad', 'ftp://example.org', 'blob:https://x/y', '', null, undefined,
    'https://', 'https://exa mple.org', 'https://x.example/"onmouseover="x', "https://x.example/'><script>", 'https://x.example/<b>', 'javascript&#58;alert(1)', '\u0001javascript:alert(1)']) {
    assert.equal(T.linkSicher(schlecht), null, `müsste abgelehnt werden: ${JSON.stringify(schlecht)}`);
  }
});

// ------------------------------------------------------------------------------------------------ Formatierung

test('Formatierung: Absätze, Umbrüche, Fett, Kursiv, Code, Link', () => {
  const b = T.formatieren('Das ist **fett**, *kursiv* und `code`.\nZweite Zeile mit [SIA](https://www.sia.ch).\n\nNeuer Absatz.');
  assert.equal(b.length, 2);
  assert.equal(b[0].art, 'absatz');
  const k = b[0].kinder;
  assert.deepEqual(k.filter((x) => x.t === 'fett').map((x) => T.klartext(x)), ['fett']);
  assert.deepEqual(k.filter((x) => x.t === 'kursiv').map((x) => T.klartext(x)), ['kursiv']);
  assert.deepEqual(k.filter((x) => x.t === 'code').map((x) => x.s), ['code']);
  assert.ok(k.some((x) => x.t === 'umbruch'));
  const l = k.find((x) => x.t === 'link');
  assert.equal(l.href, 'https://www.sia.ch/');
  assert.equal(T.klartext(l), 'SIA');
  assert.equal(T.klartext(b[1]), 'Neuer Absatz.');
});

test('Formatierung: Überschriften, Linie, Zitat', () => {
  const b = T.formatieren('# Titel\n## Unter\ntext\n---\n> zitiert\n> weiter');
  assert.deepEqual(b.map((x) => x.art), ['ueberschrift', 'ueberschrift', 'absatz', 'linie', 'zitat']);
  assert.equal(b[0].stufe, 1); assert.equal(b[1].stufe, 2);
  assert.equal(T.klartext(b[4].bloecke), 'zitiert\nweiter');
});

test('Formatierung: Listen (Aufzählung, nummeriert, mit Fortsetzungszeile und Fett im Punkt)', () => {
  const b = T.formatieren('Schritte:\n\n1. Erstens **wichtig**\n2. Zweitens\n   geht weiter\n3. Drittens\n\n- a\n- b\n* c');
  assert.deepEqual(b.map((x) => x.art), ['absatz', 'liste', 'liste']);
  assert.equal(b[1].geordnet, true);
  assert.equal(b[1].punkte.length, 3);
  assert.equal(T.klartext(b[1].punkte[1].kinder), 'Zweitens geht weiter');
  assert.ok(b[1].punkte[0].kinder.some((x) => x.t === 'fett'));
  assert.equal(b[2].geordnet, false);
  assert.equal(b[2].punkte.length, 3);
});

test('Formatierung: Listenbeginn mitten im Absatz trennt den Absatz', () => {
  const b = T.formatieren('Folgendes:\n- eins\n- zwei');
  assert.deepEqual(b.map((x) => x.art), ['absatz', 'liste']);
});

test('Formatierung: Codeblock bleibt wörtlich (kein HTML, kein Markdown darin); fehlender Zaun endet mit dem Text', () => {
  const b = T.formatieren('Vorher\n```js\nconst a = "<b>x</b>"; // **nicht fett**\n```\nNachher');
  assert.deepEqual(b.map((x) => x.art), ['absatz', 'code', 'absatz']);
  assert.equal(b[1].s, 'const a = "<b>x</b>"; // **nicht fett**');
  assert.equal(b[1].sprache, 'js');
  const offen = T.formatieren('```\nunvollständig <script>');
  assert.equal(offen.length, 1);
  assert.equal(offen[0].art, 'code');
  assert.equal(offen[0].s, 'unvollständig <script>');
});

test('Formatierung: Tabellen mit Ausrichtung, ungleiche Zeilen werden aufgefüllt', () => {
  const b = T.formatieren('| Norm | Wert | Mitte |\n|:--|--:|:-:|\n| SIA 9999 | 2 % | ok |\n| kurz |\n\nDanach');
  assert.deepEqual(b.map((x) => x.art), ['tabelle', 'absatz']);
  const t = b[0];
  assert.deepEqual(t.ausrichtung, ['links', 'rechts', 'mitte']);
  assert.equal(t.kopf.length, 3);
  assert.equal(t.zeilen.length, 2);
  assert.equal(T.klartext(t.zeilen[0][1]), '2 %');
  assert.equal(t.zeilen[1].length, 3);
});

test('Formatierung: unvollständige Auszeichnung und snake_case bleiben Text, Escapes funktionieren', () => {
  assert.equal(T.klartext(T.formatieren('**nie geschlossen')), '**nie geschlossen');
  assert.equal(T.klartext(T.formatieren('wert_mit_unterstrich und 2 * 3 * 4 und a * b')), 'wert_mit_unterstrich und 2 * 3 * 4 und a * b');
  assert.equal(T.klartext(T.formatieren('\\*kein Stern\\* und \\`kein Code\\`')), '*kein Stern* und `kein Code`');
  assert.equal(T.klartext(T.formatieren('Leere Klammer [x]() und [y](')), 'Leere Klammer [x]() und [y](');
});

test('Formatierung: Einschleusungsversuche bleiben Text – <script>, <img onerror>, <iframe>, Ereignisattribute, Formular', () => {
  const eingabe = [
    '<script>window.__gehackt = 1</script>',
    '<img src=x onerror="window.__gehackt=1">',
    '<iframe src="javascript:alert(1)"></iframe>',
    '<a href="javascript:alert(1)" onclick="x()">Klick</a>',
    '<svg onload=alert(1)>',
    '<form action="https://evil.example"><input name=pw></form>',
    '**<b onmouseover=alert(1)>fett</b>**',
    '| <script> | <img onerror=x> |\n|--|--|\n| <b> | `<i onclick=y>` |',
    '> <style>*{display:none}</style>',
    '- <object data="x">',
  ].join('\n\n');
  const baum = T.formatieren(eingabe);
  const alle = knoten(baum);
  for (const k of alle) assert.ok(ERLAUBT.has(art(k)), `unerwarteter Knoten ${art(k)}`);
  // die Zeichen stehen als Text im Baum (werden von ki-darstellung.js nur als Textknoten ausgegeben)
  const klar = T.klartext(baum);
  for (const s of ['<script>window.__gehackt = 1</script>', '<img src=x onerror="window.__gehackt=1">', '<iframe src="javascript:alert(1)"></iframe>', '<svg onload=alert(1)>', '<form action="https://evil.example">']) {
    assert.ok(klar.includes(s), `müsste als Text erhalten bleiben: ${s}`);
  }
  assert.equal(alle.filter((k) => k.t === 'link').length, 0, 'kein Link aus HTML');
  // Knoten tragen nur ihre bekannten Felder (kein beliebiges Attribut aus der Eingabe)
  for (const k of alle) for (const f of Object.keys(k)) assert.ok(['t', 'art', 's', 'kinder', 'bloecke', 'kopf', 'zeilen', 'ausrichtung', 'punkte', 'geordnet', 'start', 'stufe', 'sprache', 'href'].includes(f), `Feld ${f}`);
});

test('Formatierung: Markdown-Links mit javascript:, data: und anderen Schemata werden nie zum Link', () => {
  const schlecht = ['javascript:alert(1)', 'JAVASCRIPT:alert(1)', 'data:text/html,<script>alert(1)</script>', 'vbscript:x', 'file:///etc/passwd', '//evil.example', 'ftp://x.example/'];
  for (const s of schlecht) {
    const b = T.formatieren(`[Hier klicken](${s}) und [noch einmal](${s})`);
    const k = knoten(b);
    assert.equal(k.filter((x) => x.t === 'link').length, 0, `Link zu ${s}`);
    assert.ok(T.klartext(b).includes('Hier klicken'), s);
  }
  const gut = knoten(T.formatieren('[ok](https://www.sia.ch/a?b=1&c=2) [mail](mailto:a@b.ch)')).filter((x) => x.t === 'link');
  assert.deepEqual(gut.map((x) => x.href), ['https://www.sia.ch/a?b=1&c=2', 'mailto:a@b.ch']);
  // die Adresse im Link ist immer eine, die linkSicher selbst ausgibt
  for (const l of gut) assert.equal(T.linkSicher(l.href), l.href);
});

test('Formatierung: lange und böse Eingaben sind schnell (keine katastrophale Rückverfolgung) und werfen nie', () => {
  const t0 = Date.now();
  for (const s of ['*'.repeat(50000), '_'.repeat(50000), '['.repeat(20000), '`'.repeat(20000), '|'.repeat(20000), '> '.repeat(5000) + 'x', '- '.repeat(20000), '[a](' + 'b'.repeat(50000) + ')', '*a ' .repeat(10000), '| a | b |\n|--|--|\n' + '| 1 | 2 |\n'.repeat(5000)]) {
    assert.doesNotThrow(() => T.formatieren(s));
  }
  assert.ok(Date.now() - t0 < 5000, `zu langsam: ${Date.now() - t0} ms`);
  assert.deepEqual(T.formatieren(''), []);
  assert.deepEqual(T.formatieren(null), []);
  assert.deepEqual(T.formatieren('   \n\n  '), []);
});

// ------------------------------------------------------------------------------------------------ Kontext und Chips

const daten = (mehr = {}) => ({
  ansicht: { kennzeichen: 'AN-GR-EG', name: 'Grundriss EG Sanitär', art: 'grundriss' },
  geschoss: { kennzeichen: 'EG', name: 'Erdgeschoss' },
  auswahl: ['SW-E.02-WC', 'SW-E.02-1'],
  bildMoeglich: true,
  ...mehr,
});

test('Kontext-Chips: Vorgabe – Ansicht, Geschoss und Auswahl an, Bild aus', () => {
  const c = T.kontextChips(daten(), {}, false);
  assert.deepEqual(c.map((x) => x.id), ['ansicht', 'geschoss', 'auswahl', 'bild']);
  assert.deepEqual(c.map((x) => x.an), [true, true, true, false]);
  assert.deepEqual(c.map((x) => x.gesperrt), [false, false, false, false]);
  assert.equal(c[0].text, 'Ansicht: Grundriss EG Sanitär');
  assert.equal(c[1].text, 'Geschoss: Erdgeschoss');
  assert.equal(c[2].text, 'Auswahl: 2 Elemente');
  assert.equal(T.kontextChips(daten({ auswahl: ['A-1'] }), {}, false)[2].text, 'Auswahl: A-1');
});

test('Kontext-Chips: abgewählt ergibt aus; fehlendes ist gesperrt und nie an', () => {
  const c = T.kontextChips(daten({ ansicht: null, geschoss: null, auswahl: [], bildMoeglich: false }), { auswahl: true, bild: true }, false);
  assert.ok(c.every((x) => !x.an && x.gesperrt));
  const ab = T.kontextChips(daten(), { ansicht: false, bild: true }, false);
  assert.deepEqual(ab.map((x) => x.an), [false, true, true, true]);
});

test('Kontext-Chips mit Internet: alle ausgegraut (gesperrt, nicht an) mit Tooltip «ohne Projektzugriff»', () => {
  const c = T.kontextChips(daten(), { ansicht: true, geschoss: true, auswahl: true, bild: true }, true);
  assert.ok(c.every((x) => x.gesperrt && !x.an));
  for (const x of c) { assert.match(x.tipp, /ohne Projektzugriff/); assert.match(x.tipp, /Projektkontext/); }
});

test('Kontext der Frage: nur was angewählt ist; Auswahl höchstens 200; Bild nur, wenn gewünscht und vorhanden', () => {
  const k = T.kontextKoerper(daten(), {}, false);
  assert.deepEqual(k, { ansicht: 'AN-GR-EG', geschoss: 'EG', auswahl: ['SW-E.02-WC', 'SW-E.02-1'] });
  assert.deepEqual(T.kontextKoerper(daten(), { ansicht: false, geschoss: false, auswahl: false }, false), {});
  assert.deepEqual(T.kontextKoerper(daten(), { bild: true }, false, 'data:image/png;base64,AAAA').bild, 'data:image/png;base64,AAAA');
  assert.equal(T.kontextKoerper(daten(), {}, false, 'data:image/png;base64,AAAA').bild, undefined, 'Bild ist aus, solange es nicht angewählt ist');
  const viele = Array.from({ length: 350 }, (_, i) => `E-${i}`);
  assert.equal(T.kontextKoerper(daten({ auswahl: viele }), {}, false).auswahl.length, 200);
  assert.match(T.kontextChips(daten({ auswahl: viele }), {}, false)[2].tipp, /ersten 200/);
});

test('Kontext der Frage mit Internet: immer leer – nichts aus dem Projekt geht mit, auch kein Bild', () => {
  assert.deepEqual(T.kontextKoerper(daten(), { ansicht: true, geschoss: true, auswahl: true, bild: true }, true, 'data:image/png;base64,AAAA'), {});
  const k = T.frageKoerper({ text: '  Was sagt die Norm?  ', kontext: T.kontextKoerper(daten(), {}, true), modell: 'opus', internet: true });
  assert.deepEqual(k, { text: 'Was sagt die Norm?', kontext: {}, modell: 'opus', internet: true });
  assert.deepEqual(T.frageKoerper({ text: 'x', kontext: {}, internet: false }), { text: 'x', kontext: {}, internet: false });
});

test('Frage prüfen: leer, zu lang', () => {
  assert.match(T.frageProbleme('   '), /Frage eingeben/);
  assert.match(T.frageProbleme('x'.repeat(8001)), /zu lang/);
  assert.equal(T.frageProbleme('x'.repeat(8000)), null);
});

// ------------------------------------------------------------------------------------------------ Texte

test('Texte: Fehlerarten, verfügbar, Werkzeuge, Gesprächszeile', () => {
  assert.match(T.fehlerText({ status: 'fehler', fehlerart: 'anmeldung' }), /nicht angemeldet.*Heute/);
  assert.match(T.fehlerText({ status: 'fehler', fehlerart: 'zeitlimit', fehler: 'Zeitlimit erreicht' }, { zeitlimitMinuten: 10 }), /10 Minuten/);
  assert.match(T.fehlerText({ status: 'fehler', fehlerart: 'start', fehler: 'claude.exe fehlt' }), /nicht gestartet.*claude\.exe fehlt/);
  assert.equal(T.fehlerText({ status: 'abgebrochen' }), 'Abgebrochen.');
  assert.equal(T.fehlerText({ status: 'fertig' }), '');
  assert.equal(T.verfuegbarText({ verfuegbar: true }), '');
  assert.match(T.verfuegbarText({ verfuegbar: false, grund: 'Claude Code wurde nicht gefunden' }), /nicht verfügbar.*nicht gefunden/);
  assert.equal(T.werkzeugSumme([{ ok: true }, { ok: false }, { ok: null }]), '3 Werkzeugaufrufe, 1 mit Fehler, 1 läuft');
  assert.equal(T.werkzeugSumme([{ ok: true }]), '1 Werkzeugaufruf');
  assert.deepEqual(T.werkzeugZeile({ name: 'element_aendern', kurz: 'WT-E.02', ok: true }).zeichen, '✓');
  assert.equal(T.werkzeugZeile({ ok: null }).zustand, 'laeuft');
  assert.match(T.gespraechLabel({ titel: 'Strang prüfen', fragen: 2, geaendert: '2026-10-20T14:05:00', laeuft: true }), /^Strang prüfen · 2 Fragen · .*läuft$/);
  // Meldung des Kerns als (gekürztes) JSON: nur «fehler» und «vorschlag» für den Menschen
  assert.equal(T.werkzeugMeldung('{"angenommen":false,"befehl":"element_aendern","fehler":"Elemente werden gerade bearbeitet.","vorschlag":"Später erneut versuchen.","aenderungen":[],"neueBef…'), 'Elemente werden gerade bearbeitet. – Später erneut versuchen.');
  assert.equal(T.werkzeugMeldung('{"fehler":"Sagt \\"nein\\""}'), 'Sagt "nein"');
  assert.equal(T.werkzeugMeldung('Einfacher Text'), 'Einfacher Text');
  assert.equal(T.werkzeugMeldung('{"ohne":"Fehler"}'), '{"ohne":"Fehler"}');
  assert.equal(T.werkzeugMeldung(null), '');
  assert.equal(T.serverFehlerText({ fehler: 'Claude arbeitet gerade an einer Frage im Gespräch «X».', vorschlag: 'Warten oder abbrechen (POST /api/ki/gespraeche/1/abbrechen).' }, 409), 'Claude arbeitet gerade an einer Frage im Gespräch «X». – Bitte warten, bis die Antwort da ist, oder sie dort abbrechen.');
  assert.equal(T.serverFehlerText({ fehler: 'Text zu lang.', vorschlag: 'Höchstens 8000 Zeichen.' }, 400), 'Text zu lang. – Höchstens 8000 Zeichen.');
  assert.equal(T.serverFehlerText(null, 503), 'Fehler 503');
  assert.equal(T.dauerText(3.2), '3 s');
  assert.equal(T.dauerText(75), '1 min 15 s');
  assert.equal(T.modellName('opus', [{ id: 'opus', name: 'Opus' }]), 'Opus');
});

test('Ablehnung «spaeter_geaendert» (409): verständlicher Titel, spätere Pakete aufgezählt, nichts Technisches', () => {
  const a = T.ablehnungText({
    fehler: 'Das Paket lässt sich nicht zurücknehmen.', vorschlag: 'Zuerst die späteren Änderungen zurücknehmen.', ablehnung: 'spaeter_geaendert',
    spaeter: [{ paket: 'p2', akteur: 'Claude', akteurArt: 'ki', zeit: '2026-10-20T14:07:00', befehl: 'element_aendern', elemente: ['WT-E.02'] },
      { paket: 'p3', akteur: 'CS', akteurArt: 'mensch', zeit: '2026-10-20T14:09:00', befehl: 'element_aendern', elemente: ['WT-E.02', 'WT-E.03'] }],
  }, 409);
  assert.match(a.titel, /Danach wurde dasselbe noch einmal geändert/);
  assert.match(a.zeilen.join(' '), /nichts verändert/);
  assert.equal(a.spaeter.length, 2);
  assert.match(a.spaeter[0], /^Claude: element_aendern an WT-E\.02/);
  assert.match(a.spaeter[1], /^CS: element_aendern an WT-E\.02, WT-E\.03/);
  assert.doesNotMatch(a.titel + a.zeilen.join(' '), /spaeter_geaendert/);
});

test('Ablehnung: übrige Gründe', () => {
  assert.match(T.ablehnungText({ ablehnung: 'schon_zurueckgenommen' }).titel, /schon zurückgenommen/);
  assert.match(T.ablehnungText({ ablehnung: 'verwendet' }).titel, /verwendet/);
  assert.match(T.ablehnungText({ ablehnung: 'gesperrt', gesperrt: [{ element: 'WT-E.02' }] }).zeilen.join(' '), /WT-E\.02/);
  assert.match(T.ablehnungText({ ablehnung: 'unbekannt' }, 404).titel, /gibt es nicht mehr/);
  assert.match(T.ablehnungText({ ablehnung: 'fremd' }, 400).titel, /Nur Änderungen von Claude/);
  assert.match(T.ablehnungText({ ablehnung: 'ablage' }, 503).titel, /Projektablage/);
  const unbekannt = T.ablehnungText({ fehler: 'Claude arbeitet gerade an einer Frage.', vorschlag: 'Abbrechen oder warten.' }, 409);
  assert.match(unbekannt.titel, /im Moment nicht möglich/);
  assert.deepEqual(unbekannt.zeilen, ['Claude arbeitet gerade an einer Frage.', 'Abbrechen oder warten.']);
});

test('Kontext einer früheren Frage (Chips unter der Nachricht des Menschen)', () => {
  assert.deepEqual(T.kontextText({ kontext: { ansicht: { kennzeichen: 'AN-1', name: 'Grundriss' }, geschoss: { kennzeichen: 'EG', name: 'Erdgeschoss' }, auswahl: ['A', 'B', 'C'], bild: true } }),
    ['Ansicht: Grundriss', 'Geschoss: Erdgeschoss', 'Auswahl: 3 Elemente', 'Bild der Ansicht']);
  assert.deepEqual(T.kontextText({ kontext: { ansicht: { kennzeichen: 'AN-9', gefunden: false } } }), ['Ansicht AN-9 (nicht gefunden)']);
  assert.deepEqual(T.kontextText({ kontext: {}, kontextVerworfen: true }), []);
});

// ------------------------------------------------------------------------------------------------ Bild

test('Bild: Grösse (längste Kante höchstens 1600, nie vergrössern), Bytes und Prüfung auf 4 MB', () => {
  assert.deepEqual(T.bildGroesse(3200, 1600), { b: 1600, h: 800 });
  assert.deepEqual(T.bildGroesse(800, 600), { b: 800, h: 600 });
  assert.deepEqual(T.bildGroesse(3200, 1600, 1600, 0.5), { b: 800, h: 400 });
  assert.deepEqual(T.bildGroesse(0, 0), { b: 1, h: 1 });
  assert.equal(T.dataUrlBytes('data:image/png;base64,AAAA'), 3);
  assert.equal(T.dataUrlBytes('data:image/png;base64,AAA='), 2);
  assert.equal(T.dataUrlBytes('kaputt'), 0);
  assert.equal(T.bildPasst('data:image/png;base64,AAAA'), true);
  const gross = 'data:image/png;base64,' + 'A'.repeat(Math.ceil(4194305 / 3) * 4);
  assert.equal(T.bildPasst(gross), false);
  assert.equal(T.bildPasst('data:image/gif;base64,AAAA'), false);
  assert.equal(T.bildPasst(''), false);
});

// ------------------------------------------------------------------------------------------------ Merken

test('Merken: nur Gespräch, Modell und Internet je Projekt – nie Nachrichten oder Texte; Ungültiges wird verworfen', () => {
  const ort = 'C:\\Projekte\\A\\e_KICAD';
  let s = T.wahlSchreiben({}, ort, { gespraech: 'g-1', modell: 'opus', internet: true, nachrichten: [{ text: 'GEHEIM' }], text: 'GEHEIM' });
  assert.deepEqual(s, { [ort]: { gespraech: 'g-1', modell: 'opus', internet: true } });
  assert.doesNotMatch(JSON.stringify(s), /GEHEIM/);
  assert.deepEqual(T.wahlLesen(s, ort), { gespraech: 'g-1', modell: 'opus', internet: true });
  assert.deepEqual(T.wahlLesen(s, 'anderes'), { gespraech: null, modell: null, internet: null });
  s = T.wahlSchreiben(s, 'B', { gespraech: null, modell: 'sonnet', internet: false });
  assert.equal(Object.keys(s).length, 2);
  assert.deepEqual(T.wahlLesen(s, 'B'), { gespraech: null, modell: 'sonnet', internet: false });
  for (const roh of [null, 'x', 5, [], { [ort]: 'x' }, { [ort]: { gespraech: '../../x', modell: '<b>', internet: 'ja' } }]) {
    assert.deepEqual(T.wahlLesen(roh, ort), { gespraech: null, modell: null, internet: null }, JSON.stringify(roh));
  }
  let viele = {};
  for (let i = 0; i < 80; i++) viele = T.wahlSchreiben(viele, `P${i}`, { gespraech: `g${i}`, modell: 'haiku', internet: false });
  assert.ok(Object.keys(viele).length <= 30);
  assert.ok(viele.P79, 'das zuletzt benutzte Projekt bleibt');
});

test('Wahl des Gesprächs, des Modells und des Internet-Schalters', () => {
  const liste = [{ id: 'neu' }, { id: 'alt' }];
  assert.equal(T.gespraechWaehlen(liste, 'alt'), 'alt');
  assert.equal(T.gespraechWaehlen(liste, 'weg'), 'neu', 'unbekannt: das neueste');
  assert.equal(T.gespraechWaehlen([], 'alt'), null);
  const einst = { modelle: [{ id: 'sonnet' }, { id: 'opus' }, { id: 'haiku' }], vorgabeModell: 'sonnet', internetVorgabe: false };
  assert.equal(T.modellWaehlen(einst, 'opus'), 'opus');
  assert.equal(T.modellWaehlen(einst, 'gpt'), 'sonnet');
  assert.equal(T.modellWaehlen(einst, null), 'sonnet');
  assert.equal(T.internetWaehlen(einst, null), false, 'Internet aus als Vorgabe');
  assert.equal(T.internetWaehlen(einst, true), true);
});

// ------------------------------------------------------------------------------------------------ Zustand und Ereignisse

const mensch = (frage, text = 'Frage?', mehr = {}) => ({ rolle: 'mensch', frage, text, zeit: '2026-10-20T14:00:00', kontext: {}, modell: 'sonnet', internet: false, ...mehr });
const ereignis = (typ, frage, mehr = {}, gespraech = 'g1') => ({ art: 'ki', gespraech, frage, typ, ...mehr });
const leeresGespraech = (mehr = {}) => ({ id: 'g1', titel: 'Test', laeuft: false, frage: null, nachrichten: [], ...mehr });
const neuZ = (g = leeresGespraech()) => { const z = Z.neu(); Z.gespraechSetzen(z, g); return z; };
const claude = (z, frage = 'f1') => Z.nachricht(z, 'claude', frage);

test('Ereignisse: frage → text → werkzeug → aenderung → fertig setzt den Verlauf zusammen', () => {
  const z = neuZ();
  const r = Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1', 'Ändere WT-E.02') }));
  assert.equal(r.nachricht, 'f1');
  assert.equal(z.gespraech.nachrichten.length, 2, 'Mensch und Platzhalter für Claude');
  assert.equal(claude(z).status, 'laeuft');
  assert.equal(Z.laeuft(z), true);
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'Ich ändere ', ab: 0 }));
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'WT-E.02.', ab: 11 }));
  assert.equal(claude(z).text, 'Ich ändere WT-E.02.');
  Z.ereignisAnwenden(z, ereignis('werkzeug', 'f1', { werkzeug: { id: 'w1', name: 'element_aendern', kurz: 'WT-E.02', ok: null } }));
  assert.deepEqual(claude(z).werkzeuge.map((w) => w.ok), [null]);
  Z.ereignisAnwenden(z, ereignis('werkzeug', 'f1', { werkzeug: { id: 'w1', name: 'element_aendern', kurz: 'WT-E.02', ok: true } }));
  assert.deepEqual(claude(z).werkzeuge.map((w) => w.ok), [true], 'gleiche Id: Ergebnis ersetzt den Beginn');
  Z.ereignisAnwenden(z, ereignis('aenderung', 'f1', { aenderung: { paket: 'p1', beschreibung: 'element_aendern: WT-E.02 geändert', elemente: ['WT-E.02'], ids: ['i1'], zurueckgenommen: false } }));
  assert.equal(claude(z).aenderungen.length, 1);
  const fertig = { rolle: 'claude', frage: 'f1', status: 'fertig', text: 'Ich ändere WT-E.02.\n\nFertig.', werkzeuge: [{ id: 'w1', name: 'element_aendern', ok: true }], aenderungen: [{ paket: 'p1', beschreibung: 'x', elemente: ['WT-E.02'], ids: ['i1'], zurueckgenommen: false }] };
  const e = Z.ereignisAnwenden(z, ereignis('fertig', 'f1', { nachricht: fertig }));
  assert.equal(e.ende, true);
  assert.equal(claude(z).status, 'fertig');
  assert.equal(claude(z).text, 'Ich ändere WT-E.02.\n\nFertig.', 'die fertige Nachricht ersetzt den gestreamten Text');
  assert.equal(Z.laeuft(z), false);
  assert.equal(z.gespraech.nachrichten.length, 2);
});

test('Ereignisse: doppelte Ereignisse ändern nichts (frage, text, werkzeug, aenderung, fertig)', () => {
  const z = neuZ();
  const folge = [
    ereignis('frage', 'f1', { nachricht: mensch('f1') }),
    ereignis('text', 'f1', { teil: 'Eins ', ab: 0 }),
    ereignis('werkzeug', 'f1', { werkzeug: { id: 'w1', name: 'a', ok: null } }),
    ereignis('werkzeug', 'f1', { werkzeug: { id: 'w1', name: 'a', ok: true } }),
    ereignis('aenderung', 'f1', { aenderung: { paket: 'p1', elemente: [], ids: [] } }),
    ereignis('text', 'f1', { teil: 'zwei.', ab: 5 }),
  ];
  for (const e of folge) Z.ereignisAnwenden(z, e);
  const nachEinmal = JSON.stringify(z.gespraech);
  for (const e of folge) Z.ereignisAnwenden(z, e);   // alles ein zweites Mal
  assert.equal(JSON.stringify(z.gespraech), nachEinmal);
  assert.equal(claude(z).text, 'Eins zwei.');
  assert.equal(claude(z).werkzeuge.length, 1);
  assert.equal(claude(z).aenderungen.length, 1);
  assert.equal(z.gespraech.nachrichten.filter((n) => n.rolle === 'mensch').length, 1);
  const fertig = { rolle: 'claude', frage: 'f1', status: 'fertig', text: 'Eins zwei.', werkzeuge: [], aenderungen: [] };
  Z.ereignisAnwenden(z, ereignis('fertig', 'f1', { nachricht: fertig }));
  const nachFertig = JSON.stringify(z.gespraech);
  Z.ereignisAnwenden(z, ereignis('fertig', 'f1', { nachricht: fertig }));
  assert.equal(JSON.stringify(z.gespraech), nachFertig);
  assert.equal(z.gespraech.nachrichten.length, 2);
});

test('Ereignisse: die Antwort der Frage (POST) und das Ereignis «frage» ergeben zusammen eine Nachricht, egal was zuerst kommt', () => {
  for (const reihenfolge of [['post', 'sse'], ['sse', 'post']]) {
    const z = neuZ();
    for (const was of reihenfolge) {
      if (was === 'post') Z.frageGesendet(z, { gespraech: 'g1', frage: 'f1', nachricht: mensch('f1') });
      else Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
    }
    assert.equal(z.gespraech.nachrichten.length, 2, reihenfolge.join());
  }
});

test('Ereignisse: die Antwort der Frage (POST) kommt erst nach dem Ende der Antwort an – der Lauf bleibt beendet', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'Schnell', ab: 0 }));
  Z.ereignisAnwenden(z, ereignis('fertig', 'f1', { nachricht: { rolle: 'claude', frage: 'f1', status: 'fertig', text: 'Schnell' } }));
  assert.equal(Z.laeuft(z), false);
  Z.frageGesendet(z, { gespraech: 'g1', frage: 'f1', nachricht: mensch('f1') });   // verspätet
  assert.equal(Z.laeuft(z), false, 'kein Lauf wieder aufgemacht (sonst stünde «Abbrechen» für immer da)');
  assert.equal(z.gespraech.laeuft, false);
  assert.equal(z.gespraech.frage, null);
  assert.equal(z.gespraech.nachrichten.length, 2);
  assert.equal(claude(z).status, 'fertig');
});

test('Ereignisse: Reihenfolge – Text, Werkzeug und Änderung vor «frage» (Palette mitten im Lauf) legen den Platzhalter an', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('werkzeug', 'f9', { werkzeug: { id: 'w1', name: 'a', ok: null } }));
  Z.ereignisAnwenden(z, ereignis('text', 'f9', { teil: 'Mitten drin', ab: 0 }));
  assert.equal(claude(z, 'f9').status, 'laeuft');
  assert.equal(claude(z, 'f9').text, 'Mitten drin');
  Z.ereignisAnwenden(z, ereignis('fertig', 'f9', { nachricht: { rolle: 'claude', frage: 'f9', status: 'fertig', text: 'Mitten drin, fertig.' } }));
  assert.equal(claude(z, 'f9').text, 'Mitten drin, fertig.');
  assert.equal(Z.laeuft(z), false);
});

test('Ereignisse: verspätete Teile nach dem Ende werden ignoriert; fertig vor dem letzten Text bleibt fertig', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'Hallo', ab: 0 }));
  Z.ereignisAnwenden(z, ereignis('fertig', 'f1', { nachricht: { rolle: 'claude', frage: 'f1', status: 'fertig', text: 'Hallo Welt' } }));
  const r = Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: ' Welt', ab: 5 }));
  assert.equal(r.ignoriert, true);
  assert.equal(claude(z).text, 'Hallo Welt');
  const w = Z.ereignisAnwenden(z, ereignis('werkzeug', 'f1', { werkzeug: { id: 'spaet', name: 'x', ok: true } }));
  assert.equal(w.ignoriert, true);
  assert.equal(claude(z).werkzeuge.length, 0);
});

test('Ereignisse: fehler und abgebrochen beenden den Lauf mit Status, Fehlerart und Nachricht', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
  Z.ereignisAnwenden(z, ereignis('fehler', 'f1', { fehler: 'Nicht angemeldet', fehlerart: 'anmeldung', nachricht: { rolle: 'claude', frage: 'f1', status: 'fehler', text: '' } }));
  assert.equal(claude(z).status, 'fehler');
  assert.equal(claude(z).fehlerart, 'anmeldung');
  assert.equal(claude(z).fehler, 'Nicht angemeldet');
  assert.equal(Z.laeuft(z), false);
  const z2 = neuZ();
  Z.ereignisAnwenden(z2, ereignis('frage', 'f2', { nachricht: mensch('f2') }));
  Z.ereignisAnwenden(z2, ereignis('abgebrochen', 'f2', {}));   // ohne Nachricht: Status trotzdem gesetzt
  assert.equal(claude(z2, 'f2').status, 'abgebrochen');
  assert.equal(Z.laeuft(z2), false);
});

test('Ereignisse: anderes Gespräch, fremde Arten und Unsinn werden übergangen; «gespraeche» verlangt die Liste', () => {
  const z = neuZ();
  assert.equal(Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }, 'anderes')).ignoriert, true);
  assert.equal(z.gespraech.nachrichten.length, 0);
  assert.equal(Z.ereignisAnwenden(z, { art: 'aenderung', ids: [] }).ignoriert, true);
  assert.equal(Z.ereignisAnwenden(z, null).ignoriert, true);
  assert.equal(Z.ereignisAnwenden(z, ereignis('text', undefined, { teil: 'x' })).ignoriert, true);
  assert.equal(Z.ereignisAnwenden(z, ereignis('unbekannt', 'f1')).ignoriert, true);
  assert.equal(Z.ereignisAnwenden(z, { art: 'ki', typ: 'gespraeche' }).liste, true);
  const ohne = Z.neu();
  assert.equal(Z.ereignisAnwenden(ohne, ereignis('text', 'f1', { teil: 'x' })).ignoriert, true, 'ohne gewähltes Gespräch');
});

test('Streaming: nachgeladener Stand (GET mitten im Lauf) und danach Teile – nichts doppelt, nichts fehlt', () => {
  // Stand des Servers: «Hallo Welt» (ungekürzt), 10 Zeichen
  const z = neuZ(leeresGespraech({ laeuft: true, frage: 'f1', nachrichten: [mensch('f1'), { rolle: 'claude', frage: 'f1', status: 'laeuft', text: 'Hallo Welt', werkzeuge: [], aenderungen: [] }] }));
  assert.equal(Z.laeuft(z), true);
  assert.equal(Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'Welt', ab: 6 })).ignoriert, true, 'steckt schon im Stand');
  assert.equal(claude(z).text, 'Hallo Welt');
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: 'Welt und mehr', ab: 6 }));   // überlappt: nur der neue Rest
  assert.equal(claude(z).text, 'Hallo Welt und mehr');
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: '.', ab: 19 }));
  assert.equal(claude(z).text, 'Hallo Welt und mehr.');
  Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: ' Neu', ab: 100 }));   // Lücke (Teil verpasst): trotzdem anhängen, die fertige Nachricht richtet es
  assert.equal(claude(z).text, 'Hallo Welt und mehr. Neu');
});

test('Streaming: Teile ohne Zähler (ältere Server) werden angehängt', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
  for (const t of ['a', 'b', 'c']) Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: t }));
  assert.equal(claude(z).text, 'abc');
});

test('Streaming: Absätze zwischen Textblöcken (\\n\\n im Teil) ergeben genau den Text der fertigen Nachricht', () => {
  const z = neuZ();
  Z.ereignisAnwenden(z, ereignis('frage', 'f1', { nachricht: mensch('f1') }));
  const teile = ['Ich ändere WT-E.02.', '\n\n', 'Ich habe WT-E.02 umbenannt.'];
  let ab = 0;
  for (const t of teile) { Z.ereignisAnwenden(z, ereignis('text', 'f1', { teil: t, ab })); ab += t.length; }
  assert.equal(claude(z).text, 'Ich ändere WT-E.02.\n\nIch habe WT-E.02 umbenannt.');
  // und formatiert: zwei Absätze
  assert.equal(T.formatieren(claude(z).text).length, 2);
});

test('Zurücknehmen: Ereignis und Antwort markieren das Paket in allen Nachrichten; ein zweites Mal ändert nichts', () => {
  const z = neuZ(leeresGespraech({ nachrichten: [
    mensch('f1'), { rolle: 'claude', frage: 'f1', status: 'fertig', text: 'ok', aenderungen: [{ paket: 'p1', elemente: ['A'], ids: ['i'], zurueckgenommen: false }, { paket: 'p2', elemente: ['A'], ids: ['i'], zurueckgenommen: false }] },
  ] }));
  assert.equal(Z.ereignisAnwenden(z, { art: 'ki', gespraech: 'g1', typ: 'zurueckgenommen', paket: 'p2', rueckgaengig: 'r1' }).gespraech, true);
  const a = claude(z).aenderungen;
  assert.deepEqual(a.map((x) => x.zurueckgenommen), [false, true]);
  assert.equal(a[1].zurueckgenommenPaket, 'r1');
  assert.equal(Z.ereignisAnwenden(z, { art: 'ki', gespraech: 'g1', typ: 'zurueckgenommen', paket: 'p2' }).ignoriert, true);
  assert.equal(Z.zurueckgenommen(z, 'p1', null), true);
  assert.equal(Z.zurueckgenommen(z, 'p9', null), false);
});

test('Gespräch setzen und Liste: der Verlauf wird kopiert (der Server-Stand bleibt unverändert), Listen werden übernommen', () => {
  const g = leeresGespraech({ nachrichten: [mensch('f1')] });
  const z = Z.neu();
  Z.gespraechSetzen(z, g);
  Z.ereignisAnwenden(z, ereignis('frage', 'f2', { nachricht: mensch('f2') }));
  assert.equal(g.nachrichten.length, 1, 'Eingabe nicht verändert');
  assert.equal(z.aktuell, 'g1');
  assert.deepEqual(z.gespraech.nachrichten[0].werkzeuge, []);
  Z.listeSetzen(z, { gespraeche: [{ id: 'a' }, { id: 'b' }] });
  assert.equal(z.gespraeche.length, 2);
  Z.listeSetzen(z, null);
  assert.deepEqual(z.gespraeche, []);
  Z.gespraechSetzen(z, null);
  assert.equal(z.gespraech, null);
  assert.equal(Z.laeuft(z), false);
});

// ------------------------------------------------------------------------------------------------ Verdrahtung

test('Verdrahtung: Palette «claude» rechts angedockt, nur auf Verlangen, Kürzel KI frei, Ereignis «ki» im Strom, Stilblatt, Symbole', () => {
  const palette = lies('ki-palette.js');
  assert.match(palette, /anmelden\(\{[\s\S]*id: ID[\s\S]*ort: 'rechts'[\s\S]*vorgabeSichtbar: false/);
  assert.match(palette, /export const ID = 'claude'/);
  assert.match(palette, /abonnieren\('ki', beiEreignis\)/);
  assert.match(palette, /istZweitfenster\(\)/, 'im Zweitfenster wird nichts geladen');
  const aktion = lies('aktionen-ki.js');
  assert.match(aktion, /kuerzel: 'KI'/);
  assert.match(aktion, /umschalten\('claude'\)/);
  assert.match(lies('aktionen.js'), /import '\.\/aktionen-ki\.js'/);
  assert.match(lies('../ereignisse.js'), /case 'ki':[^\n]*\n\s+melden\('ki', d\)/);
  assert.match(lies('../../index.html'), /css\/ki-palette\.css/);
  assert.ok(statSync(path.join(hier, '../../css/ki-palette.css')).size > 1000);
  assert.match(lies('symbole.js'), /^ {2}claude: '/m);
  assert.match(lies('symbole-revit.js'), /^s\('claude',/m);
});

test('Kürzel KI ist frei: kein anderes Kürzel beginnt mit dieser Folge, KS und kein Strg-Kürzel stört', () => {
  const alle = [];
  const gehe = (ordner) => {
    for (const n of readdirSync(ordner)) {
      const p = path.join(ordner, n);
      if (statSync(p).isDirectory()) { if (n !== 'lib') gehe(p); continue; }
      if (!n.endsWith('.js')) continue;
      for (const m of readFileSync(p, 'utf8').matchAll(/kuerzel(?:Alt)?:\s*(\[[^\]]*\]|'[^']*')/g)) alle.push([path.relative(path.join(hier, '..'), p), m[1]]);
    }
  };
  gehe(path.join(hier, '..'));
  const kuerzel = alle.flatMap(([, w]) => [...w.matchAll(/'([A-Za-z0-9+]+)'/g)].map((x) => x[1]));
  assert.equal(kuerzel.filter((k) => k.toUpperCase() === 'KI').length, 1, `KI mehrfach oder fehlt: ${kuerzel.filter((k) => k.toUpperCase() === 'KI').length}`);
  assert.ok(kuerzel.includes('KS'), 'KS (Tastaturkürzel) besteht weiter');
});

test('Keine Logik und kein HTML aus fremdem Text: die Palette nutzt weder innerHTML mit Antworttext noch insertAdjacentHTML noch eval', () => {
  for (const datei of ['ki-palette.js', 'ki-darstellung.js', 'ki-bild.js']) {
    const q = lies(datei).replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|\s)\/\/.*$/gm, '$1');   // ohne Kommentare
    assert.doesNotMatch(q, /insertAdjacentHTML|outerHTML|document\.write|\beval\(|new Function/, datei);
    const stellen = [...q.matchAll(/(\w[\w.]*)\.innerHTML\s*=\s*([^;\n]+)/g)].map((m) => m[0]);
    for (const s of stellen) assert.match(s, /symbol\('claude', 14\)/, `innerHTML nur für das eigene Symbol: ${s}`);
  }
  // Nachrichten nie im Browserspeicher: die Palette schreibt nur über wahlSchreiben
  const palette = lies('ki-palette.js');
  assert.equal([...palette.matchAll(/merken\.schreiben\(([^)]*)\)/g)].length, 1);
  assert.match(palette, /merken\.schreiben\(T\.MERKEN_SCHLUESSEL, T\.wahlSchreiben\(/);
  assert.doesNotMatch(palette, /localStorage|sessionStorage|indexedDB/);
  // Links nur mit noopener
  assert.match(lies('ki-darstellung.js'), /a\.rel = 'noopener noreferrer'/);
  assert.match(lies('ki-darstellung.js'), /a\.target = '_blank'/);
});

test('OBERFLAECHE.md: Abschnitt «KI-Palette (3c, Teil O)» am Ende', () => {
  const md = readFileSync(path.join(hier, '../../../OBERFLAECHE.md'), 'utf8');
  const i = md.indexOf('## KI-Palette (3c, Teil O)');
  assert.ok(i > 0, 'Abschnitt fehlt');
  assert.ok(i > md.indexOf('## KI-Palette: Schnittstelle (3c, Teil K)'), 'steht nach dem Abschnitt von Teil K');
  const abschnitt = md.slice(i);
  for (const stichwort of ['Kürzel', 'KI', 'Internet', 'ohne Projektzugriff', 'Zurücknehmen', 'Normen', 'ki-palette.browser.mjs', 'Zweitfenster']) assert.ok(abschnitt.includes(stichwort), `«${stichwort}» fehlt im Abschnitt`);
});

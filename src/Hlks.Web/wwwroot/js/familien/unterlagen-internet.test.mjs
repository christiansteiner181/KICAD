// Tests der Internet-Recherche in der Oberfläche «Familie aus Unterlagen» (Modul 2, Auftrag C2, Ergänzung): Schalter im Auftrag, Liste «Aus dem Internet abgerufen»,
// Link nur für http(s), URL bei der Quelle (Abdeckung, «Quelle ansehen»), Webseiten (Art html) ohne Seitenbild. Kein Download, keine Netzverbindung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/familien/unterlagen-internet.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const U = await import('./unterlagen-text.js');
const H = await import('./pruefansicht-html.js');
const Q = U;
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');

const ELEMENTE = [
  { id: 'a', kennzeichen: 'UL-2', klasse: 'Unterlage', werte: { zweck: 'familie', art: 'pdf', datei: 'aaa000000000_hersteller.pdf', original: 'https://hersteller.example/datenblatt.pdf', abgerufen: '2026-10-18T14:03:55+02:00', bytes: 2097152 } },
  { id: 'b', kennzeichen: 'UL-10', klasse: 'Unterlage', werte: { zweck: 'familie', art: 'html', datei: 'bbb000000000_seite.html', original: 'https://hersteller.example/typen' } },
  { id: 'c', kennzeichen: 'UL-1', klasse: 'Unterlage', werte: { zweck: 'familie', art: 'pdf', datei: 'ccc000000000_lokal.pdf', original: 'C:\\Daten\\lokal.pdf' } },
];

test('Schalter «internet» geht immer ausdrücklich an POST /api/auftraege (Vorgabe ein)', () => {
  assert.equal(U.auftragParameter({ unterlagen: ['UL-1'] }).internet, true);
  assert.equal(U.auftragParameter({ unterlagen: ['UL-1'], internet: true }).internet, true);
  assert.equal(U.auftragParameter({ unterlagen: ['UL-1'], internet: false }).internet, false);
  const d = lies('unterlagen-dialog.js');
  assert.match(d, /class="fu-internet" checked> Fehlende Angaben im Internet recherchieren/);
  assert.match(d, /nur aus heruntergeladenen und abgelegten Dokumenten mit Quelle/);
  assert.match(d, /Adressen im eigenen Netz sind gesperrt/);
  assert.match(d, /internet: q\('\.fu-internet'\)\.checked/);
  assert.ok(d.includes('internetListeHtml(U.internetUnterlagen(alleElemente()))'), 'Liste im Ergebnis des Auftrags');
});

test('Adressen: nur http(s) wird ein Link (neuer Tab, ohne Verweis); Grösse und Zeit lesbar', () => {
  for (const s of ['https://www.beispiel.ch/datenblatt.pdf', 'http://beispiel.ch/a?b=1']) assert.equal(U.istUrl(s), true, s);
  for (const s of ['ftp://x.ch/a', 'javascript:alert(1)', 'C:\\Daten\\a.pdf', 'cbec2941ef74_datenblatt.pdf', 'https://a.ch/ x', '']) assert.equal(U.istUrl(s), false, s);
  const l = U.urlLink('https://www.beispiel.ch/a?x=1&y=2');
  assert.ok(l.includes('href="https://www.beispiel.ch/a?x=1&amp;y=2"') && l.includes('target="_blank"') && l.includes('rel="noopener noreferrer"'), l);
  assert.ok(!U.urlLink('javascript:alert(1)').includes('<a '), 'kein Link für fremde Schemata');
  assert.equal(U.groesseText(1536), '1.5 kB');
  assert.equal(U.groesseText(5242880), '5 MB');
  assert.equal(U.groesseText(undefined), '');
  assert.equal(U.zeitKurz('2026-10-18T14:03:55+02:00'), '2026-10-18 14:03');
});

test('Liste «Aus dem Internet abgerufen»: nur Unterlagen mit URL als original (natürlich sortiert), mit Kennzeichen, Link, Art, Grösse, Zeit', () => {
  const liste = U.internetUnterlagen(ELEMENTE);
  assert.deepEqual(liste.map((u) => u.kz), ['UL-2', 'UL-10']);
  assert.deepEqual([liste[0].art, liste[0].groesse, liste[0].abgerufen, liste[0].datei], ['pdf', '2 MB', '2026-10-18 14:03', 'hersteller.pdf']);
  assert.deepEqual([liste[1].groesse, liste[1].abgerufen], ['', '']);
  const h = U.internetListeHtml(liste);
  assert.match(h, /Aus dem Internet abgerufen \(2\)/);
  assert.ok(h.includes('href="https://hersteller.example/datenblatt.pdf"'));
  assert.ok(h.includes('<td>Webseite</td>') && h.includes('<td>PDF</td>'));
  assert.ok(h.includes('<td>2 MB</td>') && h.includes('<td>2026-10-18 14:03</td>'));
  assert.equal(U.internetListeHtml([]), '');
  // Prüfansicht: dieselbe Liste im Bericht
  assert.match(H.berichtHtml({ definition: {} }, [], liste), /Aus dem Internet abgerufen \(2\)/);
  assert.ok(!H.berichtHtml({ definition: {} }, [], []).includes('Aus dem Internet'));
});

test('Kandidaten tragen die URL; «Quelle ansehen» und die Abdeckung zeigen sie als Text und Link', () => {
  const def = { unterlagen: [{ unterlage: 'a', datei: 'hersteller.pdf', pruefsumme: 'aaa000000000ffff', art: 'pdf' }] };
  const k = U.unterlagenKandidaten(def, 'FAM-1', ELEMENTE);
  assert.equal(k.find((x) => x.ref === 'UL-2').url, 'https://hersteller.example/datenblatt.pdf');
  assert.equal(k.find((x) => x.ref === 'UL-2').quelle, 'definition');
  assert.equal(k.find((x) => x.ref === 'UL-1').url, '', 'ein lokaler Pfad ist keine URL');
  const erg = { id: 'bbb000000000#7', befund: { id: 'bbb000000000#7', art: 'zelle', wert: 'Rp 1/2', zusatz: { zeile: 2, spalte: 3, kopf: 'Anschluss', text: 'Rp 1/2' } }, unterlage: { datei: 'bbb000000000_seite.html', art: 'html', url: 'https://hersteller.example/typen' } };
  assert.ok(Q.quellenZeile(erg).endsWith('aus dem Internet: https://hersteller.example/typen'));
  assert.ok(Q.quellenHtml(erg).includes('aus dem Internet: <a class="fu-url" href="https://hersteller.example/typen"'));
  assert.ok(!Q.quellenHtml({ ...erg, unterlage: { datei: 'x.pdf', art: 'pdf', url: '' } }).includes('Internet'));
  assert.ok(!Q.quellenZeile({ ...erg, unterlage: { datei: 'x.pdf', art: 'pdf', url: '' } }).includes('Internet'));
  assert.match(Q.quellenHtml({ id: 'x#1', fehler: 'Befund fehlt <b>' }), /Befund fehlt &lt;b&gt;/, 'Fehlertexte werden maskiert');
});

test('Webseiten (Art html): kein Seitenbild – «Quelle ansehen» zeigt den Befundtext samt Tabellenzelle und den Link; der Befund-Browser auch', () => {
  assert.equal(U.ART_NAMEN.html, 'Webseite');
  const v = Q.befundVolltext({ id: 'b#7', art: 'zelle', wert: 'Rp 1/2', zusatz: { zeile: 2, spalte: 3, kopf: 'Anschluss', text: 'Rp 1/2' } });
  assert.match(v, /^Rp 1\/2\n/);
  assert.ok(v.includes('Tabellenzelle Zeile 2, Spalte 3 · Spalte «Anschluss»'));
  assert.equal(Q.befundVolltext({ art: 'text', wert: 'Der Rp-Anschluss ist 1/2 Zoll.', zusatz: { text: 'Der Rp-Anschluss ist 1/2 Zoll.' } }), 'Der Rp-Anschluss ist 1/2 Zoll.');
  assert.ok(Q.befundVolltext({ art: 'zelle', wert: 67, einheit: 'mm' }).startsWith('67 mm'));
  const qa = lies('quelle-ansehen.js');
  assert.ok(qa.includes("if (u.art === 'html') return;"), 'kein unterlage_bild für Webseiten');
  assert.ok(qa.includes('fu-quelle-html') && qa.includes('urlLink(u.url)'));
  const bb = lies('befunde-browser.js');
  assert.ok(bb.includes("ui.eintrag.art === 'html'") && bb.includes('Webseite: kein Seitenbild') && bb.includes('befundVolltext(b)') && bb.includes('Aus dem Internet:'));
  // Kontrollbild: Webseiten sind keine Unterlage fürs Kontrollbild
  const kand = [{ ref: 'UL-9', datei: 'seite.html', art: 'html', praefix: 'b' }, { ref: 'UL-1', datei: 'a.pdf', art: 'pdf', praefix: 'a' }];
  assert.deepEqual(H.vergleichbareUnterlagen(kand).map((x) => x.ref), ['UL-1']);
});

test('CSS und Verdrahtung der Internet-Anzeige', () => {
  const css = lies('../../css/familien-unterlagen.css');
  for (const k of ['.fu-url', '.fu-inet', '.fu-internet-feld', '.fu-bb-url']) assert.ok(css.includes(k), k);
  assert.ok(lies('pruefansicht.js').includes('U.internetUnterlagen(ctx.elemente())'));
  assert.ok(lies('pruefansicht.js').includes('quellenHtml(e)'));
});

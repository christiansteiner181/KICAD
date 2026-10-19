// Tests der reinen Logik von «Familie aus Unterlagen» (Modul 2, Auftrag C2) ohne Browser: Status-Farben, Gründe der gesperrten Freigabe, Wahl im Konflikt →
// Parameter von konflikt_entscheiden, Passpunkte → Parameter von familie_vergleichen, Ereignis «auftrag», Boxen und Klicks im Bild, «als Quelle übernehmen»,
// das HTML der Prüfansicht und die Verdrahtung (Aktionen, Reiter, CSS, Befehle des Kerns). Aufruf:
//   node --test src/Hlks.Web/wwwroot/js/familien/unterlagen.test.mjs
// Alle Werte sind fiktiv (Testventil der synthetischen Unterlagen tests/daten/unterlagen/). Der Browser-Test familie-unterlagen.browser.mjs prüft den Rest gegen den echten Kern.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const U = await import('./unterlagen-text.js');
const H = await import('./pruefansicht-html.js');
const A = await import('./familien-ansicht.js');
const D = await import('./familie-unterlagen-def.mjs');
const T = await import('./familien-text.js');
const sym = await import('../oberflaeche/symbole.js');
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const klon = (x) => JSON.parse(JSON.stringify(x));
const nah = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) <= eps, `${a} ≠ ${b}`);

// Antwort der Abfrage familie (gekürzt) für das Testventil mit dem Widerspruch L = 67 / 70 und B ohne Quelle
const FAM = {
  kennzeichen: 'FAM-1', name: 'Testventil C2 (fiktiv)', kategorie: 'Ventil', status: 'entwurf', version: 1, gueltig: true, definitionsfehler: [], hinweise: [],
  typen: [{ name: 'V-15', gueltig: true, auswertungsfehler: [] }, { name: 'V-20', gueltig: true, auswertungsfehler: [] }],
  definition: {
    parameter: [{ name: 'dn', art: 'typ', datentyp: 'dn', gruppe: 'anschluss' }, { name: 'L', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', einheit: 'mm' }, { name: 'B', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', wert: 30 }],
    typen: [{ name: 'V-15', werte: { dn: 15, L: 70 }, quellen: { dn: ['aaa#33'], L: ['aaa#34', 'bbb#1'] } }, { name: 'V-20', werte: { dn: 20, L: 78 }, quellen: { dn: ['aaa#37'], L: ['aaa#38'] } }],
    abdeckung: {
      'V-15/dn': { quellen: ['aaa#33'], pflicht: true, status: 'geprüft', fehlt: false, wert: 15 },
      'V-15/L': { quellen: ['aaa#34', 'bbb#1'], pflicht: true, konflikt: 'k-1', status: 'konflikt', fehlt: false, wert: 70 },
      'V-20/dn': { quellen: ['aaa#37'], pflicht: true, status: 'geprüft', fehlt: false, wert: 20 },
      'V-20/L': { quellen: ['aaa#38'], pflicht: true, status: 'ungeprüft-bild', fehlt: false, wert: 78, vermerk: 'aus Bild gelesen' },
      B: { quellen: [], pflicht: true, status: 'fehlt', fehlt: true, wert: 30, vermerk: 'ohne Quelle' },
    },
    konflikte: [{
      id: 'k-1', parameter: 'L', typ: 'V-15', einheit: 'mm', toleranz: 'Länge max(1 mm; 0.5 %) = ±1 mm', vorschlag: 70, vorrang: 'DWG/DXF-Bemassung vor PDF-Zelle',
      werte: [{ wert: 70, quellen: ['bbb#1'], stellen: ['massbild.dxf, Layer MASSE (bbb#1, mass)'], text: ['70 mm'], vorrang: 'DWG/DXF-Bemassung' },
        { wert: 67, quellen: ['aaa#34'], stellen: ['datenblatt.pdf, Seite 1, Zeile 1, Spalte 3 (aaa#34, zelle)'], text: ['67 mm'], vorrang: 'PDF-Zelle' }],
    }],
    unterlagen: [{ unterlage: 'id-pdf', datei: 'datenblatt.pdf', pruefsumme: 'aaa0000000000000', art: 'pdf' }, { unterlage: 'id-dxf', datei: 'massbild.dxf', pruefsumme: 'bbb0000000000000', art: 'dxf' }],
    bericht: 'Gelesen: Tabelle.',
  },
};
const MASSE_SCHLECHT = { masse: 2, bestanden: 1, ausserhalb: 1, ohneVergleich: 0, typen: [{ typ: 'V-15', bestanden: false, abweichungen: [
  { mass: 'L', typ: 'V-15', ansicht: 'vorne', soll: 70, ist: 67, abweichung: -3, toleranz: 1, bestanden: false, befund: 'bbb#1', quelle: 'massbild.dxf' },
  { mass: 'H', typ: 'V-15', ansicht: 'vorne', soll: 35, ist: 35, abweichung: 0, toleranz: 1, bestanden: true }] }] };

// ================================================================================================ Dateien und Dialog

test('Dateien: erlaubte Endungen, Prüfsumme vor dem Namen, Zeile je Datei mit Art, Seiten, Befunden, Tabellen, Hinweisen, Fehler', () => {
  for (const n of ['a.pdf', 'b.DWG', 'c.dxf', 'd.step', 'e.stp', 'f.iges', 'g.igs', 'h.stl', 'i.obj', 'j.ifc', 'k.xlsx', 'l.csv', 'm.txt', 'n.png', 'o.jpg', 'p.jpeg']) assert.equal(U.dateiPruefen(n), true, n);
  assert.match(U.dateiPruefen('x.tif'), /wird nicht gelesen/);
  assert.match(U.dateiPruefen('x.rfa'), /RFA/);
  assert.match(U.dateiPruefen('ohne'), /keine Dateiendung/);
  assert.equal(U.ohnePruefsumme('cbec2941ef74_datenblatt.pdf'), 'datenblatt.pdf');
  assert.equal(U.ohnePruefsumme('datenblatt.pdf'), 'datenblatt.pdf');
  const z = U.dateiZeile({ unterlage: 'UL-1', art: 'pdf', seiten: 4, befunde: 55, jeArt: { zelle: 12, wort: 22 }, tabellen: [{ id: 'x#1' }], hinweise: ['h1'] });
  assert.deepEqual([z.art, z.seiten, z.tabellen, z.fehler], ['PDF', '4 S.', '1 Tabelle', '']);
  assert.match(z.befunde, /55 Befunde \(12 zelle, 22 wort\)/);
  assert.deepEqual(z.hinweise, ['h1']);
  assert.equal(U.dateiZeile({ tabellen: [1, 2], tabellenWeitere: 3 }).tabellen, '5 Tabellen');
  assert.match(U.dateiZeile({ unterlage: 'x', fehler: 'kaputt', vorschlag: 'neu laden' }).fehler, /kaputt – neu laden/);
  assert.equal(U.dateiZeile(null).fehler, '');
});

test('Kategorie: familienfähige Klassen aus dem Katalog (familie: true), ältere Antworten über «familientyp»; «Neue Familie» nimmt dieselbe Liste', () => {
  const klassen = [{ name: 'Wand' }, { name: 'Ventil', familie: true }, { name: 'Pumpe', familie: true }, { name: 'Raum', familie: false, eigenschaften: ['familientyp'] }, { name: 'Alt', eigenschaften: ['familientyp', 'x'] }];
  assert.deepEqual(U.kategorienWahl(klassen), ['Alt', 'Pumpe', 'Ventil']);
  assert.deepEqual(T.familienKategorien(klassen), ['Alt', 'Pumpe', 'Ventil']);
});

test('Start des KI-Laufs: Parameter von POST /api/auftraege und Prüfung vor dem Start', () => {
  assert.deepEqual(U.auftragParameter({ unterlagen: ['UL-1', 'UL-2'], kategorie: 'Ventil', hinweis: '  nur DN 15  ', familie: 'FAM-3' }),
    { art: 'familie_aus_unterlagen', unterlagen: ['UL-1', 'UL-2'], internet: true, kategorie: 'Ventil', hinweis: 'nur DN 15', familie: 'FAM-3' });
  assert.deepEqual(U.auftragParameter({ unterlagen: ['UL-1'], kategorie: '', hinweis: ' ', familie: null }), { art: 'familie_aus_unterlagen', unterlagen: ['UL-1'], internet: true });
  assert.equal(U.auftragParameter({ unterlagen: ['UL-1'], internet: false }).internet, false, 'der Schalter geht immer ausdrücklich mit (Vorgabe ein)');
  assert.match(U.startPruefen({ dateien: [] }), /mindestens eine Unterlage/);
  assert.match(U.startPruefen({ dateien: [{ status: 'laedt' }] }), /hochgeladen bzw. gelesen/);
  assert.match(U.startPruefen({ dateien: [{ status: 'fehler' }] }), /mindestens eine Unterlage/, 'Dateien mit Fehler zählen nicht');
  assert.match(U.startPruefen({ dateien: [{ status: 'bereit' }], ersetzen: true, familie: '' }), /Welche Familie/);
  assert.equal(U.startPruefen({ dateien: [{ status: 'bereit' }, { status: 'fehler' }], ersetzen: false }), true);
});

// ================================================================================================ Status und Abdeckung

test('Status der Abdeckung: fünf Status, jeder mit Farbe im CSS (hell und dunkel), Tabelle Parameter × Typ, Summen', () => {
  assert.deepEqual(U.STATUS.map((s) => s.id), ['geprüft', 'ungeprüft-bild', 'fehlt', 'konflikt', 'mensch']);
  assert.deepEqual(U.STATUS.map((s) => s.klasse), ['geprueft', 'bild', 'fehlt', 'konflikt', 'mensch']);
  assert.equal(U.statusInfo('konflikt').text, 'Konflikt');
  assert.equal(U.statusInfo('gibtsnicht').klasse, 'unbekannt');
  const css = lies('../../css/familien-unterlagen.css').replace(/\/\*[\s\S]*?\*\//g, '');
  const hell = /:root\s*\{([^}]*)\}/.exec(css)[1], dunkel = /:root\[data-design="dunkel"\]\s*\{([^}]*)\}/.exec(css)[1];
  for (const s of [...U.STATUS.map((x) => x.klasse), 'unbekannt']) {
    assert.match(css, new RegExp(`\\.fu-st-${s}\\s*\\{[^}]*background:\\s*var\\(--fu-st-${s}\\)`), `Klasse .fu-st-${s}`);
    assert.ok(hell.includes(`--fu-st-${s}:`), `Farbe ${s} (hell)`);
    if (s !== 'unbekannt') assert.ok(dunkel.includes(`--fu-st-${s}:`), `Farbe ${s} (dunkel)`);
  }
  const t = U.abdeckungTabelle(FAM.definition);
  assert.deepEqual(t.typen, ['V-15', 'V-20']);
  assert.deepEqual(t.zeilen.map((z) => z.parameter), ['dn', 'L', 'B'], 'Reihenfolge der Parameter; ohne Eintrag entfällt die Zeile');
  const L = t.zeilen.find((z) => z.parameter === 'L');
  assert.equal(L.vorgabe, null);
  assert.deepEqual(L.typen.map((c) => c.status), ['konflikt', 'ungeprüft-bild']);
  assert.equal(L.typen[0].konflikt, 'k-1');
  assert.deepEqual(L.typen[0].quellen, ['aaa#34', 'bbb#1']);
  const B = t.zeilen.find((z) => z.parameter === 'B');
  assert.deepEqual([B.vorgabe.status, B.vorgabe.fehlt, B.typen.every((c) => c === null)], ['fehlt', true, true]);
  assert.deepEqual(U.abdeckungSummen(FAM.definition), { 'geprüft': 2, 'ungeprüft-bild': 1, fehlt: 1, konflikt: 1, mensch: 0 });
  assert.equal(U.bestaetigbar(B.vorgabe), true);
  assert.equal(U.bestaetigbar(L.typen[1]), true, 'ungeprüft-bild lässt sich bestätigen');
  assert.equal(U.bestaetigbar(L.typen[0]), false, 'ein Konflikt wird entschieden, nicht bestätigt');
  assert.equal(U.bestaetigbar(null), false);
  assert.match(U.bildHinweis(FAM.definition), /1 Wert aus Bildern gelesen/);
  assert.equal(U.bildHinweis({ abdeckung: {} }), '');
  // Schlüssel ohne passenden Parameter bleiben sichtbar
  assert.equal(U.abdeckungTabelle({ parameter: [], typen: [], abdeckung: { 'Z/x': { status: 'fehlt', quellen: [] } } }).uebrig.length, 1);
});

// ================================================================================================ Freigabe

test('Freigabe-Gründe: offene Konflikte, Fehler, Pflichtwerte ohne Quelle und Masse ausserhalb der Toleranz sperren – mit Ziel für den Sprung', () => {
  const g = U.freigabeGruende(FAM, MASSE_SCHLECHT);
  assert.deepEqual(g.map((x) => x.art), ['konflikt', 'fehlt', 'mass']);
  assert.match(g[0].text, /Konflikt offen: «L» · Typ V-15 – 70 \/ 67/);
  assert.deepEqual(g[0].ziel, { abschnitt: 'konflikt', id: 'k-1' });
  assert.deepEqual(g[1].ziel, { abschnitt: 'abdeckung', schluessel: 'B' });
  assert.match(g[2].text, /Mass L, Typ V-15: Soll 70, Ist 67 \(Toleranz ±1 mm\)/);
  assert.deepEqual(g[2].ziel, { abschnitt: 'masse', schluessel: 'V-15/L' });
  assert.equal(U.freigabeMoeglich(FAM, MASSE_SCHLECHT), false);
  // Fehler der Definition und der Typen kommen zuerst und springen in den Editor
  const mitFehler = klon(FAM);
  mitFehler.definitionsfehler = [{ stelle: 'parameter «x».formel', meldung: 'unbekannter Name «y»', vorschlag: '' }];
  mitFehler.typen[1].auswertungsfehler = [{ stelle: 'koerper «a».bis', meldung: 'Parameter «H» hat keinen Wert' }];
  const g2 = U.freigabeGruende(mitFehler, null);
  assert.equal(g2[0].art, 'fehler');
  assert.deepEqual(g2[0].ziel.ziel, { tab: 'parameter', name: 'x', feld: 'eingabe' });
  assert.deepEqual(g2.slice(0, 2).map((x) => x.art), ['fehler', 'fehler']);
  assert.deepEqual(g2[1].ziel.ziel, { tab: 'typen', name: 'V-20', feld: 'H' }, 'fehlender Typwert: Sprung in die Zelle des Typkatalogs');
  // alles erledigt: Konflikt entschieden, B bestätigt, Masse bestanden → frei
  const ok = klon(FAM);
  ok.definition.konflikte[0].entscheid = { wert: 70, akteur: 'T' };
  ok.definition.abdeckung.B = { quellen: [], pflicht: true, status: 'mensch', fehlt: false, wert: 30 };
  const masseOk = klon(MASSE_SCHLECHT); masseOk.typen[0].abweichungen[0].bestanden = true;
  assert.deepEqual(U.freigabeGruende(ok, masseOk), []);
  assert.equal(U.freigabeMoeglich(ok, masseOk), true);
  assert.equal(U.freigabeMoeglich({ ...ok, status: 'freigegeben' }, masseOk), false, 'schon freigegeben');
  assert.equal(U.freigabeMoeglich(null, null), false);
  // Werte aus Bildern sperren nicht
  assert.equal(U.freigabeGruende(ok, masseOk).some((x) => /Bild/.test(x.text)), false);
  // ohne Massangaben (Kern kennt sie nicht) bleibt es bei den Gründen aus der Definition
  assert.deepEqual(U.freigabeGruende(FAM, null).map((x) => x.art), ['konflikt', 'fehlt']);
});

// ================================================================================================ Konflikte und Bestätigen

test('Konflikt-Wahl → Parameter von konflikt_entscheiden (Zahl, anderer Wert, Begründung); Vorschlag hervorgehoben, nie vorgewählt', () => {
  const k = FAM.definition.konflikte[0];
  const o = U.konfliktOptionen(k);
  assert.deepEqual(o.map((x) => [x.wert, x.vorschlag, x.text]), [[70, true, '70 mm'], [67, false, '67 mm']]);
  assert.deepEqual(o[1].quellen, ['aaa#34']);
  assert.match(o[1].stellen[0], /Seite 1, Zeile 1, Spalte 3/);
  assert.deepEqual(U.konfliktEntscheid('FAM-1', k, { index: 1 }, '  PDF gilt  '), { parameter: { familie: 'FAM-1', konflikt: 'k-1', wert: 67, begruendung: 'PDF gilt' } });
  assert.deepEqual(U.konfliktEntscheid('FAM-1', k, { index: '0' }, ''), { parameter: { familie: 'FAM-1', konflikt: 'k-1', wert: 70 } });
  assert.deepEqual(U.konfliktEntscheid('FAM-1', k, { wert: '68,5' }, 'Rücksprache').parameter.wert, 68.5);
  assert.equal(U.konfliktEntscheid('FAM-1', k, { wert: 'rot' }, '').parameter.wert, 'rot', 'Text bleibt Text (Textparameter)');
  assert.match(U.konfliktEntscheid('FAM-1', k, {}, '').fehler, /einen der Werte wählen/);
  assert.match(U.konfliktEntscheid('FAM-1', k, { index: 5 }, '').fehler, /gibt es im Konflikt nicht/);
  assert.match(U.konfliktEntscheid('', k, { index: 0 }, '').fehler, /fehlt/);
  assert.equal(U.konfliktTitel(k), '«L» · Typ V-15');
  // HTML: Vorschlag markiert, kein Radio vorgewählt, ein Knopf «Quelle ansehen» je Quelle
  const h = H.konflikteHtml(FAM.definition, {});
  assert.match(h, /Konflikte \(1, davon 1 offen\)/);
  assert.equal((h.match(/type="radio"/g) ?? []).length, 3, 'zwei Werte und «anderer Wert»');
  assert.ok(!/checked/.test(h), 'nichts vorgewählt');
  assert.equal((h.match(/class="fu-k-wert vorschlag"/g) ?? []).length, 1);
  assert.equal((h.match(/data-pa="quelle"/g) ?? []).length, 2);
  assert.match(h, /data-pa="entscheiden" data-k="k-1">Entscheiden</);
  // gewählt und entschieden
  const e = klon(FAM.definition); e.konflikte[0].entscheid = { wert: 67, akteur: 'Christian', zeit: '2026-10-18T12:00:00+02:00', begruendung: 'PDF gilt' };
  const h2 = H.konflikteHtml(e, {});
  assert.match(h2, /Entschieden: <b>67 mm<\/b> von Christian am 2026-10-18 12:00 – PDF gilt/);
  assert.match(h2, /Neu entscheiden/);
  assert.match(h2, /value="1"[^>]*checked/, 'der Entscheid ist angekreuzt');
});

test('Wert bestätigen → Parameter von wert_bestaetigen (nur ohne Quelle oder aus Bild)', () => {
  const t = U.abdeckungTabelle(FAM.definition);
  const B = t.zeilen.find((z) => z.parameter === 'B').vorgabe;
  assert.deepEqual(U.bestaetigenParameter('FAM-1', B, ' vom Hersteller bestätigt '), { parameter: { familie: 'FAM-1', parameter: 'B', begruendung: 'vom Hersteller bestätigt' } });
  const L20 = t.zeilen.find((z) => z.parameter === 'L').typen[1];
  assert.deepEqual(U.bestaetigenParameter('FAM-1', L20, '').parameter, { familie: 'FAM-1', parameter: 'L', typ: 'V-20' });
  assert.match(U.bestaetigenParameter('FAM-1', t.zeilen[0].typen[0], '').fehler, /wird nicht bestätigt/);
  assert.match(U.bestaetigenParameter('FAM-1', null, '').fehler, /fehlt/);
});

// ================================================================================================ Kontrollbild und Passpunkte

test('Passpunkte → Parameter von familie_vergleichen: leer, unvollständig, Zahlen mit Komma, gleiche Punkte; Seite nur bei PDF', () => {
  const leer = {};
  assert.deepEqual(U.passpunkteLesen(leer), { leer: true });
  assert.match(U.passpunkteLesen({ a_u: '0', a_v: '0' }).fehler, /zwei Paare mit je vier Zahlen/);
  assert.match(U.passpunkteLesen({ a_u: '0', a_v: '0', a_x: '1', a_y: '2', b_u: '5', b_v: 'x', b_x: '9', b_y: '9' }).fehler, /«x» ist keine Zahl/);
  assert.match(U.passpunkteLesen({ a_u: '0', a_v: '0', a_x: '1', a_y: '2', b_u: '0', b_v: '0', b_x: '9', b_y: '9' }).fehler, /Punkte der Familie fallen zusammen/);
  assert.match(U.passpunkteLesen({ a_u: '0', a_v: '0', a_x: '1', a_y: '2', b_u: '5', b_v: '5', b_x: '1', b_y: '2' }).fehler, /Punkte der Unterlage fallen zusammen/);
  const f = { a_u: '0', a_v: '0', a_x: '100', a_y: '342', b_u: '67', b_v: '0', b_x: '300', b_y: '342,5' };
  assert.deepEqual(U.passpunkteLesen(f), { passpunkte: [{ familie: [0, 0], unterlage: [100, 342] }, { familie: [67, 0], unterlage: [300, 342.5] }] });
  const form = { familie: 'FAM-1', typ: 'V-15', unterlage: 'UL-1', ansicht: 'vorne', art: 'pdf', seite: '1', detailgrad: 'mittel', felder: f };
  const p = U.vergleichParameter(form).parameter;
  assert.deepEqual(p, { familie: 'FAM-1', typ: 'V-15', unterlage: 'UL-1', ansicht: 'vorne', seite: 1, detailgrad: 'mittel', breite: 1200, passpunkte: [{ familie: [0, 0], unterlage: [100, 342] }, { familie: [67, 0], unterlage: [300, 342.5] }] });
  const dxf = U.vergleichParameter({ ...form, unterlage: 'UL-2', art: 'dxf', felder: {} }).parameter;
  assert.ok(!('seite' in dxf) && !('passpunkte' in dxf), 'DXF: keine Seite; ohne Passpunkte entscheidet der Massbezug der Familie');
  assert.match(U.vergleichParameter({ ...form, typ: '' }).fehler, /Typ wählen/);
  assert.match(U.vergleichParameter({ ...form, unterlage: '' }).fehler, /Unterlage wählen/);
  assert.match(U.vergleichParameter({ ...form, seite: '0' }).fehler, /ganze Zahl ab 1/);
  assert.match(U.vergleichParameter({ ...form, felder: { a_u: '1' } }).fehler, /zwei Paare/);
  // Anklicken: vier Klicks in fester Folge, Rundung
  assert.deepEqual(U.PICK_FOLGE.map(([p, w]) => `${p}${w[0]}`), ['af', 'au', 'bf', 'bu']);
  assert.match(U.pickText(0), /Passpunkt 1 in der Familienansicht/);
  assert.match(U.pickText(3), /Passpunkt 2 in der Unterlage/);
  assert.match(U.pickText(4), /Passpunkt 1 in der Familienansicht/, 'die Folge beginnt von vorn');
  assert.deepEqual(U.pickFamilie('a', 66.96, 0.04), { a_u: '67', a_v: '0' });
  assert.deepEqual(U.pickUnterlage('b', 300.004, 342.126), { b_x: '300', b_y: '342.13' });
});

test('Kontrollbild: schematisch bei PDF (Vermerk), massstäblich bei DXF; Abweichungsliste und geometrischer Vergleich', () => {
  const pdf = { familie: 'FAM-1', name: 'T', typ: 'V-15', ansicht: 'vorne', datei: 'datenblatt.pdf', seite: 1, mime: 'image/png', base64: 'AAAA', massstaeblich: false,
    hinweis: 'schematisch, Überlagerung nur Anschauung: Massbilder in Datenblättern sind nicht massstäblich', einpassung: { quelle: 'passpunkte', massstab: 2.99, drehung: 0, einheit: 'pt je mm der Familie' },
    abweichungen: MASSE_SCHLECHT.typen[0].abweichungen };
  assert.equal(U.istSchematisch(pdf), true);
  const h = H.vergleichErgebnisHtml(pdf);
  assert.match(h, /class="fu-schematisch"[^>]*>schematisch</);
  assert.match(h, /Überlagerung nur Anschauung/);
  assert.match(h, /data:image\/png;base64,AAAA/);
  assert.match(h, /fu-masse-schlecht/, 'Abweichungen rot');
  assert.ok(!/fu-massstaeblich/.test(h));
  const dxf = { ...pdf, massstaeblich: true, hinweis: undefined, datei: 'massbild.dxf', seite: null, geometrisch: { kanten: { anzahl: 4, bestanden: 4, ausserhalb: 0, groessterAbstand: 0 }, masse: [{ befund: 'bbb#1', wert: 70, einheit: 'mm', abstandPunkt1: 0, abstandPunkt2: 0, toleranz: 1, bestanden: true }] } };
  const h2 = H.vergleichErgebnisHtml(dxf, true);
  assert.match(h2, /fu-massstaeblich/);
  assert.ok(!/class="fu-schematisch"/.test(h2));
  assert.match(h2, /Kanten der Zeichnung: 4 von 4 innerhalb der Toleranz, grösster Abstand 0 mm/);
  assert.match(h2, /Stand vor der letzten Änderung/);
  assert.equal(H.vergleichErgebnisHtml(null), '');
  // Formular: Passpunkt-Felder, Unterlagen und Typen aus der Familie, Seite nur bei PDF
  const kand = [{ praefix: 'aaa', ref: 'UL-1', datei: 'datenblatt.pdf', art: 'pdf' }, { praefix: 'bbb', ref: 'UL-2', datei: 'massbild.dxf', art: 'dxf' }, { praefix: 'c', ref: 'UL-3', datei: 'x.xlsx', art: 'xlsx' }];
  const form = H.vergleichFormHtml(FAM, kand, { unterlage: 'UL-2', seite: '1', ansicht: 'vorne', typ: 'V-15', detailgrad: 'mittel', felder: { a_u: '0' } }, true);
  for (const n of ['a_u', 'a_v', 'a_x', 'a_y', 'b_u', 'b_v', 'b_x', 'b_y']) assert.ok(form.includes(`data-vg-pp="${n}"`), n);
  assert.ok(form.includes('value="UL-2" selected') && !form.includes('x.xlsx'), 'Excel ist keine Unterlage fürs Kontrollbild');
  assert.match(form, /fu-vg-seite" hidden/, 'DXF hat keine Seite');
  assert.match(form, /<details class="fu-passpunkte" open>/);
});

// ================================================================================================ KI-Aufträge (Ereignis «auftrag»)

test('Ereignis «auftrag»: Stand nachführen (nie rückwärts), laufender Auftrag, Dauer, letzte Schritte, Familien des Ergebnisses', () => {
  const a1 = { id: 'A', status: 'laeuft', start: '2026-10-18T10:00:00+02:00', schrittAnzahl: 2, schritte: [{ zeit: '2026-10-18T10:00:01+02:00', text: 'MCP-Server hlks: connected' }] };
  let s = U.auftragUebernehmen(new Map(), a1);
  assert.equal(s.get('A').schrittAnzahl, 2);
  s = U.auftragUebernehmen(s, { ...a1, schrittAnzahl: 5 });
  assert.equal(s.get('A').schrittAnzahl, 5);
  s = U.auftragUebernehmen(s, { ...a1, schrittAnzahl: 3 });
  assert.equal(s.get('A').schrittAnzahl, 5, 'ein älteres Ereignis überschreibt nichts');
  assert.equal(U.aktuellerAuftrag(s).id, 'A');
  assert.equal(U.auftragLaeuft(s.get('A')), true);
  const ende = { ...a1, status: 'fertig', schrittAnzahl: 9, ergebnis: { familien: [{ kennzeichen: 'FAM-1', aktion: 'angelegt', imModell: true, offeneKonflikte: 1 }] } };
  s = U.auftragUebernehmen(s, ende);
  assert.equal(s.get('A').status, 'fertig');
  s = U.auftragUebernehmen(s, { ...a1, schrittAnzahl: 7 });
  assert.equal(s.get('A').status, 'fertig', 'ein verspätetes «läuft» macht den Auftrag nicht wieder lebendig');
  // die volle Antwort (mit Bericht) bleibt, wenn danach ein Ereignis ohne Bericht kommt
  s = U.auftragUebernehmen(s, { ...ende, ergebnis: { ...ende.ergebnis, bericht: 'Fertig.' } });
  s = U.auftragUebernehmen(s, ende);
  assert.equal(s.get('A').ergebnis.bericht, 'Fertig.');
  assert.equal(U.auftragLaeuft(s.get('A')), false);
  assert.equal(U.auftragEnde(s.get('A')), true);
  assert.deepEqual(U.ergebnisFamilien(s.get('A')).map((f) => f.kennzeichen), ['FAM-1']);
  assert.deepEqual(U.ergebnisFamilien({ ergebnis: { familien: [{ kennzeichen: 'FAM-2', aktion: 'geloescht' }, { kennzeichen: 'FAM-3', aktion: 'angelegt', imModell: false }] } }), []);
  // laufender Auftrag gewinnt gegen einen früheren beendeten
  const zwei = U.auftragUebernehmen(s, { id: 'B', status: 'laeuft', start: '2026-10-18T11:00:00+02:00' });
  assert.equal(U.aktuellerAuftrag(zwei).id, 'B');
  assert.equal(U.auftragUebernehmen(new Map(), {}).size, 0, 'ohne Id nichts');
  assert.equal(U.dauerText(5), '5 s'); assert.equal(U.dauerText(65), '1 min 05 s'); assert.equal(U.dauerText(-3), '0 s');
  const z = U.schrittZeilen({ schritte: Array.from({ length: 12 }, (_, i) => ({ zeit: '2026-10-18T10:00:00', text: `Schritt ${i}` })) }, 8);
  assert.equal(z.length, 8); assert.equal(z.at(-1).text, 'Schritt 11');
  assert.deepEqual(Object.keys(U.STATUS_AUFTRAG), ['wartet', 'laeuft', 'fertig', 'fehler', 'abgebrochen']);
});

test('Ereignisstrom: die Art «auftrag» wird an die Oberfläche gemeldet; auftrag.js führt sie nach und spricht die Endpunkte der Aufträge an', () => {
  const e = lies('../ereignisse.js');
  assert.match(e, /case 'auftrag':\s*(\/\/[^\n]*\n\s*)?melden\('auftrag', d\)/);
  const a = lies('auftrag.js');
  assert.match(a, /abonnieren\('auftrag'/);
  for (const s of ["'/api/auftraege'", '`/api/auftraege/${encodeURIComponent(id)}`', '/abbrechen']) assert.ok(a.includes(s), s);
  // Server: das Ereignis hat die Form { art: 'auftrag', auftrag: {…} }
  const cs = path.resolve(hier, '../../../AuftragDienst.cs');
  if (existsSync(cs)) assert.match(readFileSync(cs, 'utf8'), /\["art"\] = "auftrag", \["auftrag"\] = Zustand\(a, false\)/);
});

// ================================================================================================ Bilder und Befunde

test('Quelle ansehen: Ausschnitt um die Box (mit Rand), Marke im Bild, Klick zurück in Unterlagen-Koordinaten (PDF/DXF y nach oben, Bild y nach unten)', () => {
  // Zelle L von V-15 im PDF: Box [220, 612, 230.01, 618.47] pt
  const box = [220, 612, 230.01, 618.47];
  assert.deepEqual(U.befundAusschnitt(box, 'pdf'), [140, 532, 310.01, 698.47], 'Rand mindestens 80 pt');
  assert.deepEqual(U.befundAusschnitt([0, -12, 70, 0], 'dxf'), [-70, -82, 140, 70], 'Rand so gross wie der Befund');
  assert.deepEqual(U.befundAusschnitt([10, 10, 12, 11], 'dxf'), [0, 0, 22, 21], 'mindestens 10 Zeichnungseinheiten');
  assert.equal(U.befundAusschnitt(null, 'pdf'), null);
  assert.equal(U.befundAusschnitt([1, 2, 'x', 4], 'pdf'), null);
  const aus = U.befundAusschnitt(box, 'pdf');
  // Bild 900 px breit über den Bereich
  const breite = 900, hoehe = Math.round(900 * (aus[3] - aus[1]) / (aus[2] - aus[0]));
  const m = U.boxImBild(box, aus, breite, hoehe, 'pdf');
  nah(m.x, (220 - 140) * breite / 170.01, 0.2); nah(m.b, 10.01 * breite / 170.01, 0.2);
  nah(m.y, (698.47 - 618.47) * hoehe / 166.47, 0.2); nah(m.h, 6.47 * hoehe / 166.47, 0.2);
  assert.ok(m.x >= 0 && m.x + m.b <= breite && m.y >= 0 && m.y + m.h <= hoehe, 'die Marke liegt im Bild');
  // Klick auf die Mitte der Marke → Mitte der Box
  const [x, y] = U.bildPunktZuUnterlage(m.x + m.b / 2, m.y + m.h / 2, aus, breite, hoehe, 'pdf');
  nah(x, 225, 0.2); nah(y, 615.2, 0.2);
  // Mindestgrösse der Marke (Punkt-Boxen)
  const punkt = U.boxImBild([100, 100, 100, 100], [0, 0, 200, 200], 400, 400, 'pdf');
  assert.deepEqual([punkt.b, punkt.h], [6, 6]);
  // Bild: y nach unten
  const b = U.boxImBild([10, 20, 30, 40], [0, 0, 100, 100], 200, 200, 'bild');
  assert.deepEqual([b.x, b.y, b.b, b.h], [20, 40, 40, 40]);
  assert.deepEqual(U.bildPunktZuUnterlage(20, 40, [0, 0, 100, 100], 200, 200, 'bild'), [10, 20]);
  assert.deepEqual(U.bildPunktZuUnterlage(20, 40, [0, 0, 100, 100], 200, 200, 'pdf'), [10, 80], 'PDF: y nach oben');
  assert.equal(U.boxImBild(null, [0, 0, 1, 1], 10, 10, 'pdf'), null);
  // Befund unter dem Klick: der kleinste gewinnt
  const befunde = [{ id: 'a#1', box: [0, 0, 100, 100] }, { id: 'a#2', box: [10, 10, 20, 20] }, { id: 'a#3' }];
  assert.equal(U.befundUnterPunkt(befunde, [15, 15]).id, 'a#2');
  assert.equal(U.befundUnterPunkt(befunde, [50, 50]).id, 'a#1');
  assert.equal(U.befundUnterPunkt(befunde, [500, 500]), null);
});

test('Befund-Texte und Stellen; Unterlagen der Familie (Definition und Bezug); Unterlage zur Befund-Id', () => {
  const zelle = { id: 'aaa#34', art: 'zelle', wert: 67, einheit: 'mm', seite: 1, zusatz: { zeile: 1, spalte: 3 } };
  assert.equal(U.befundText(zelle), '67 mm');
  assert.equal(U.befundStelle(zelle), 'Seite 1 · Zeile 1, Spalte 3');
  assert.match(U.befundText({ art: 'tabelle', wert: { zeilen: 3, spalten: 4, kopf: ['Typ', 'DN'] } }), /3 Zeilen × 4 Spalten · Typ \| DN/);
  assert.equal(U.befundStelle({ layout: 'Model', layer: 'MASSE' }), 'Blatt Model · Layer MASSE');
  assert.equal(U.befundText({ art: 'linie' }), '');
  const el = [
    { id: 'id-pdf', kennzeichen: 'UL-1', klasse: 'Unterlage', name: 'datenblatt.pdf', werte: { zweck: 'familie', art: 'pdf', datei: 'aaa000000000_datenblatt.pdf' } },
    { id: 'id-dxf', kennzeichen: 'UL-2', klasse: 'Unterlage', name: 'massbild.dxf', werte: { zweck: 'familie', art: 'dxf', datei: 'bbb000000000_massbild.dxf' } },
    { id: 'id-frei', kennzeichen: 'UL-3', klasse: 'Unterlage', name: 'frei.pdf', werte: { zweck: 'familie', art: 'pdf', datei: 'ccc000000000_frei.pdf' } },
    { id: 'id-plan', kennzeichen: 'UL-4', klasse: 'Unterlage', name: 'plan.dwg', geschoss: 'EG', werte: { art: 'dwg', datei: 'ddd000000000_plan.dwg' } },
    { id: 'id-fremd', kennzeichen: 'UL-5', klasse: 'Unterlage', name: 'fremd.pdf', bezuege: { familie: 'FAM-9' }, werte: { zweck: 'familie', art: 'pdf', datei: 'eee000000000_fremd.pdf' } },
  ];
  const def = { unterlagen: [{ unterlage: 'id-pdf', datei: 'datenblatt.pdf', pruefsumme: 'aaa000000000ffff', art: 'pdf' }, { unterlage: 'id-weg', datei: 'weg.pdf', pruefsumme: 'fff000000000aaaa', art: 'pdf' }] };
  const l = U.unterlagenDerFamilie(def, 'FAM-1', el);
  assert.deepEqual(l.map((u) => u.ref), ['UL-1', 'id-weg'], 'nur Unterlagen der Definition (UL-2/UL-3 ohne Bezug gehören noch niemandem; UL-5 einer anderen Familie)');
  assert.equal(U.unterlagenKandidaten(def, 'FAM-1', el).find((u) => u.ref === 'UL-3').quelle, 'frei');
  assert.ok(!U.unterlagenKandidaten(def, 'FAM-1', el).some((u) => ['UL-4', 'UL-5'].includes(u.ref)), 'Plan-Unterlagen und fremde Familien zählen nicht');
  assert.equal(U.unterlageZuBefund(l, 'aaa000000000#34').ref, 'UL-1');
  assert.equal(U.unterlageZuBefund(l, 'zzz#1'), null);
  // mit Bezug zur Familie gehört die Unterlage ihr
  el[1].bezuege = { familie: 'FAM-1' };
  assert.ok(U.unterlagenDerFamilie(def, 'FAM-1', el).some((u) => u.ref === 'UL-2'));
});

// ================================================================================================ Als Quelle übernehmen

test('«Als Quelle übernehmen»: ops für typ_setzen bzw. parameter_setzen mit quellen (und auf Wunsch dem Wert), Fehler ohne Zelle, Befund oder bei Formeln', () => {
  const def = klon(FAM.definition);
  const befund = { id: 'aaa#42', art: 'zelle', wert: 87, einheit: 'mm' };
  const zelleTyp = { art: 'typ', typ: 'V-20', parameter: 'L' };
  const r = U.quelleUebernehmenOps(def, zelleTyp, befund, { wertUebernehmen: true });
  assert.equal(r.ops.length, 1);
  assert.equal(r.ops[0].op, 'typ_setzen');
  assert.equal(r.ops[0].typ.name, 'V-20');
  assert.deepEqual(r.ops[0].typ.quellen.L, ['aaa#38', 'aaa#42'], 'angehängt, nichts ersetzt');
  assert.equal(r.ops[0].typ.werte.L, 87);
  assert.equal(r.ops[0].typ.werte.dn, 20, 'übrige Werte bleiben');
  assert.deepEqual(def.typen[1].quellen.L, ['aaa#38'], 'die Definition selbst bleibt unverändert');
  const nurQuelle = U.quelleUebernehmenOps(def, zelleTyp, befund);
  assert.equal(nurQuelle.ops[0].typ.werte.L, 78, 'ohne Wunsch bleibt der Wert');
  assert.deepEqual(U.quelleUebernehmenOps(def, zelleTyp, befund).ops[0].typ.quellen.L, ['aaa#38', 'aaa#42']);
  assert.deepEqual(U.quelleUebernehmenOps(def, zelleTyp, { id: 'aaa#38' }).ops[0].typ.quellen.L, ['aaa#38'], 'doppelt wird nicht angehängt');
  const p = U.quelleUebernehmenOps(def, { art: 'parameter', parameter: 'B' }, { id: 'aaa#50', art: 'zelle', wert: '31,5' }, { wertUebernehmen: true });
  assert.equal(p.ops[0].op, 'parameter_setzen');
  assert.deepEqual([p.ops[0].parameter.name, p.ops[0].parameter.quellen, p.ops[0].parameter.wert], ['B', ['aaa#50'], 31.5]);
  assert.match(U.quelleUebernehmenOps(def, zelleTyp, null).fehler, /Befund wählen/);
  assert.match(U.quelleUebernehmenOps(def, undefined, befund).fehler, /Zelle im Typkatalog/);
  assert.match(U.quelleUebernehmenOps(def, { art: 'typ', typ: 'V-99', parameter: 'L' }, befund).fehler, /Typ «V-99» gibt es nicht/);
  assert.match(U.quelleUebernehmenOps(def, { art: 'typ', typ: 'V-15', parameter: 'x' }, befund).fehler, /Parameter «x» gibt es nicht/);
  assert.match(U.quelleUebernehmenOps(def, zelleTyp, { id: 'a#1', art: 'tabelle', wert: { zeilen: 3 } }, { wertUebernehmen: true }).fehler, /keinen einzelnen Wert/);
  const mitFormel = klon(def); mitFormel.parameter.find((x) => x.name === 'B').formel = 'L / 2';
  assert.match(U.quelleUebernehmenOps(mitFormel, { art: 'parameter', parameter: 'B' }, befund).fehler, /hat eine Formel/);
  assert.equal(U.befundWert({ wert: 'Stahl' }, 'text'), 'Stahl');
  assert.equal(U.befundWert({ wert: '12,5' }, 'laenge'), 12.5);
  assert.equal(U.befundWert({ wert: 3 }, 'ja_nein'), null);
  // markierte Zelle des Editors
  assert.deepEqual(U.zelleMerken('typen', 'V-15', 'L'), { art: 'typ', typ: 'V-15', parameter: 'L' });
  assert.deepEqual(U.zelleMerken('parameter', 'B', undefined), { art: 'parameter', parameter: 'B' });
  assert.equal(U.zelleMerken('koerper', 'x', 'L'), undefined);
  assert.equal(U.zelleText({ art: 'typ', typ: 'V-15', parameter: 'L' }), 'Typ V-15 · L');
});

// ================================================================================================ HTML der Prüfansicht

test('Prüfansicht: Freigabe-Knopf gesperrt mit Gründen, Sprung je Grund; frei ohne Gründe; freigegeben mit Speichern; Abdeckung farbig mit Quellenzahl; Massvergleich rot', () => {
  const g = U.freigabeGruende(FAM, MASSE_SCHLECHT);
  const k = H.kopfHtml(FAM, g, {});
  assert.match(k, /data-pa="freigeben" disabled/);
  assert.match(k, /Freigabe gesperrt – 3 Gründe/);
  assert.equal((k.match(/data-pa="grund"/g) ?? []).length, 3);
  assert.match(k, /data-pa="speichern" disabled/, 'speichern erst nach der Freigabe');
  assert.match(k, /1 Konflikt/); assert.match(k, /1 fehlt/);
  const frei = H.kopfHtml(FAM, [], {});
  assert.ok(!/data-pa="freigeben" disabled/.test(frei));
  assert.match(frei, /die Familie lässt sich freigeben/);
  const f = { ...klon(FAM), status: 'freigegeben', freigabe: { akteur: 'Christian', zeit: '2026-10-18T12:00:00+02:00', version: 3, geprueft: 8, ausBild: 1, mensch: 2, bestaetigt: 1, konflikteEntschieden: 1, masseBestanden: 6 }, bibliothek: { datei: 'testventil.familie.json' } };
  const fh = H.kopfHtml(f, [], { datei: 'x' });
  assert.match(fh, /data-pa="freigeben" disabled/);
  assert.ok(!/data-pa="speichern" disabled/.test(fh));
  assert.match(fh, /Freigegeben von Christian am 2026-10-18 12:00 \(Version 3\): 8 Wert\(e\) gegen die Quelle geprüft, 1 aus Bildern/);
  assert.match(fh, /testventil\.familie\.json/);
  const a = H.abdeckungHtml(FAM.definition, { zelle: 'V-15/L' });
  for (const s of ['konflikt', 'bild', 'fehlt', 'geprueft']) assert.ok(a.includes(`fu-st fu-st-${s}`), s);
  assert.match(a, /fu-st-konflikt gewaehlt" data-pa="zelle" data-s="V-15\/L"/);
  assert.match(a, /<sup>2<\/sup>/, 'Zahl der Quellen');
  assert.match(a, /<th>V-15<\/th><th>V-20<\/th>/);
  const d = H.abdeckungDetailHtml(FAM.definition, 'B', { bestBegr: 'ok' });
  assert.match(d, /data-pa="bestaetigen" data-s="B"/);
  assert.match(d, /Keine Quelle genannt/);
  const dk = H.abdeckungDetailHtml(FAM.definition, 'V-15/L', {});
  assert.equal((dk.match(/data-pa="quelle"/g) ?? []).length, 2);
  assert.match(dk, /data-pa="zum-konflikt" data-id="k-1"/);
  assert.ok(!dk.includes('data-pa="bestaetigen"'), 'ein Konflikt wird nicht bestätigt');
  const m = H.masseHtml(MASSE_SCHLECHT, '');
  assert.match(m, /Massvergleich \(1 bestanden, <span class="fehlertext">1 ausserhalb<\/span>\)/);
  assert.match(m, /<tr class="fu-masse-schlecht" data-mk="V-15\/L">/);
  assert.match(m, /✗ ausserhalb/); assert.match(m, /✓ bestanden/);
  assert.match(m, /−?3|-3/);
  assert.match(H.masseHtml(null, 'Die Definition hat Fehler'), /fehlertext/);
  assert.match(H.masseHtml({ masse: 0, bestanden: 0, ausserhalb: 0, ohneVergleich: 0, typen: [], hinweis: 'Die Familie hat keine Massbezüge' }, ''), /keine Massbezüge/);
  assert.match(H.berichtHtml(FAM, [{ praefix: 'aaa000000000' }]), /Gelesen: Tabelle\./);
  assert.match(H.berichtHtml({ definition: {} }), /Kein Bericht/);
  assert.equal(H.attr('data-s', 'V-15/L'), '[data-s="V-15/L"]');
});

test('Testdefinition des Browser-Tests: nennt den absichtlichen Widerspruch L = 67 (PDF) gegen 70 (DXF) und eine Tiefe ohne Quelle', () => {
  const def = D.testDefinition({ zelle: (z, s) => `pdf#${z}${s}`, dxfMass: 'dxf#1', bild: 'png#1' });
  assert.equal(def.kategorie, 'Ventil');
  const v15 = def.typen.find((t) => t.name === 'V-15');
  assert.equal(v15.werte.L, 67);
  assert.deepEqual(v15.quellen.L, ['pdf#13', 'dxf#1']);
  assert.equal(def.typen.find((t) => t.name === 'V-20').quellen.L.length, 1);
  assert.deepEqual(def.typen.find((t) => t.name === 'V-20').quellen.H, ['png#1'], 'H von V-20 stützt sich nur auf das Bild (ungeprüft-bild)');
  assert.equal(def.parameter.find((p) => p.name === 'B').wert, 30);
  assert.ok(!def.parameter.find((p) => p.name === 'B').quellen);
  assert.deepEqual(def.masse.map((m) => m.name), ['L', 'H']);
  assert.ok(def.bericht.includes('Widerspruch'));
});

// ================================================================================================ Familienansicht: Punkte anklicken

test('Passpunkte in der Familienansicht: nächster Eckpunkt der Plangrafik (ohne Anschlussmarken), Markierung als SVG', () => {
  const plan = { teile: [
    { ebene: 'geraet', primitive: [{ art: 'polygon', punkte: [[0, 0], [67, 0], [67, 35], [0, 35]] }, { art: 'kreis', punkte: [[33, 17]], radius: 5 }] },
    { ebene: 'symbol', primitive: [{ art: 'linie', punkte: [[66, 1], [90, 1]] }] },
  ] };
  assert.deepEqual(A.naechsterPunkt(plan, 66.2, 0.9, 5), [67, 0]);
  assert.deepEqual(A.naechsterPunkt(plan, 33.4, 16.8, 3), [33, 17], 'Kreismitte');
  assert.equal(A.naechsterPunkt(plan, 50, 20, 5), null, 'zu weit von jeder Ecke');
  assert.equal(A.naechsterPunkt(plan, 90, 1, 3), null, 'Anschlussmarken zählen nicht');
  assert.equal(A.naechsterPunkt(null, 0, 0, 5), null);
  const svg = A.pickPunkteSvg([{ u: 67, v: 0, label: 'P1' }], 1);
  assert.match(svg, /class="fu-pickpunkt"/); assert.match(svg, />P1</);
  assert.equal(A.pickPunkteSvg([], 1), '');
});

// ================================================================================================ Verdrahtung

test('Verdrahtung: Reiter, Aktionen, Register «Unterlagen», Symbole, Projektbrowser, CSS, Befehle und Abfragen des Kerns', () => {
  const ed = lies('editor.js');
  assert.match(ed, /import entwurfTab from '\.\/pruefansicht\.js'/);
  assert.match(ed, /import befundeTab from '\.\/befunde-browser\.js'/);
  assert.match(ed, /const TABS = \[parameterTab, leitungTab, typenTab, koerperTab, anschlussTab, sinnbildTab, pruefenTab, entwurfTab, befundeTab\]/);
  assert.match(ed, /case 'entwurf-pruefen'/);
  assert.match(ed, /zelleMerken/);
  const a = lies('aktionen-unterlagen.js');
  for (const id of ['familie.aus-unterlagen', 'familie.aus-unterlagen-register', 'familie.entwurf-pruefen', 'familie.unterlagen', 'familie.entwurf-pruefen-browser', 'familie.ersetzen-aus-unterlagen']) assert.ok(a.includes(`id: '${id}'`), id);
  assert.match(a, /register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen'/);
  assert.match(a, /register: 'familie', gruppe: 'Unterlagen'/);
  const unbekannt = sym.symbol('gibtsnicht');
  for (const s of ['famUnterlagen', 'famEntwurf', 'famBefunde']) assert.notEqual(sym.symbol(s), unbekannt, `Symbol ${s}`);
  const mb = lies('../oberflaeche/menueband.js');
  assert.match(/familie: \[([^\]]*)\]/.exec(mb)[1], /'Prüfen', 'Unterlagen'/);
  const pb = lies('../oberflaeche/projektbrowser.js');
  for (const s of ["'familie.aus-unterlagen'", "'familie.entwurf-pruefen-browser'", "'familie.ersetzen-aus-unterlagen'", 'pb-knopf']) assert.ok(pb.includes(s), s);
  assert.match(lies('browser-knoten.js'), /pruefen = entwurf \? \{ aktion: 'familie\.entwurf-pruefen-browser'/);   // C3: Knöpfe als Liste (Prüfen, Aktualisieren …)
  // Knoten im Projektbrowser: Entwürfe mit Knopf «Prüfen», Knoten «Familien» mit Knopf «Familie aus Unterlagen»
  const B = './browser-knoten.js';
  return import(B).then((b) => {
    const k = b.familienKnoten({ kennzeichen: 'FAM-1', name: 'T', kategorie: 'Ventil', status: 'entwurf', typen: ['V-15'], version: 1 });
    assert.equal(k.knopf.aktion, 'familie.entwurf-pruefen-browser');
    assert.deepEqual(k.knopf.param, { familie: 'FAM-1' });
    assert.equal(b.familienKnoten({ kennzeichen: 'FAM-2', name: 'U', kategorie: 'Ventil', status: 'freigegeben', typen: [], version: 1 }).knopf, undefined);
    assert.equal(b.familienAst([], null).knopf.aktion, 'familie.aus-unterlagen');
  }).then(() => {
    // CSS: feste Farben nur in :root, Schriftgrössen 12/11/9, kein zoom, eingebunden
    const css = lies('../../css/familien-unterlagen.css').replace(/\/\*[\s\S]*?\*\//g, '');
    const ohneVar = css.replace(/:root(\[[^\]]*\])?\s*\{[^}]*\}/g, '');
    assert.deepEqual(ohneVar.match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], [], 'feste Farben');
    for (const m of css.matchAll(/font-size:\s*([\d.]+)px/g)) assert.ok(['12', '11', '9'].includes(m[1]), `Schriftgrösse ${m[1]}`);
    assert.doesNotMatch(css, /(^|[;{\s])zoom\s*:/);
    assert.match(lies('../../index.html'), /href="css\/familien-unterlagen\.css"/);
    // Befehle und Abfragen, die die Oberfläche sendet, gibt es im Kern
    const gesendet = ['pruefansicht.js', 'unterlagen-dialog.js', 'befunde-browser.js', 'quelle-ansehen.js', 'unterlagen-bild.js', 'auftrag.js'].map(lies).join('\n');
    const kern = path.resolve(hier, '../../../../Hlks.Kern');
    const befehle = { konflikt_entscheiden: 'Familien/FamilienFreigabe.cs', familie_freigeben: 'Familien/FamilienFreigabe.cs', familie_speichern: 'Familien/FamilienFreigabe.cs', wert_bestaetigen: 'Familien', unterlage_einfuegen: 'Grundlagen/UnterlageEinfuegen.cs' };
    const abfragen = { familie_masse: 'Familien/FamilienAbfragen.cs', familie_vergleichen: 'Familien/FamilienAbfragen.cs', unterlagen_lesen: '', befunde: '', unterlage_bild: '' };
    for (const n of [...Object.keys(befehle), ...Object.keys(abfragen)]) assert.ok(gesendet.includes(`'${n}'`), `die Oberfläche benutzt ${n}`);
    assert.ok(gesendet.includes("'familie_aendern'") || lies('editor.js').includes("'familie_aendern'"));
    if (existsSync(kern)) {
      const kernText = ['Familien/FamilienFreigabe.cs', 'Familien/FamilienAbfragen.cs', 'Familien/FamilienKontrollbild.cs', 'Grundlagen/UnterlageEinfuegen.cs', 'Unterlagen/UnterlagenBefehle.cs'].filter((f) => existsSync(path.join(kern, f))).map((f) => readFileSync(path.join(kern, f), 'utf8')).join('\n');
      for (const n of ['konflikt_entscheiden', 'familie_freigeben', 'familie_speichern', 'familie_masse', 'familie_vergleichen', 'unterlage_einfuegen']) assert.ok(kernText.includes(`"${n}"`), `Kern kennt ${n}`);
      for (const p of ['konflikt', 'wert', 'begruendung', 'datei', 'zweck', 'bezuege', 'passpunkte', 'ansicht', 'unterlage']) assert.ok(kernText.includes(`new("${p}"`), `Parameter ${p}`);
    }
    // keine Fachlogik in der Oberfläche: kein element_aendern in den Dateien von C2
    for (const d of ['pruefansicht.js', 'befunde-browser.js', 'unterlagen-dialog.js', 'aktionen-unterlagen.js']) assert.ok(!lies(d).includes('element_aendern'), d);
    // Dateikopf: jede Datei sagt, was sie tut
    for (const d of ['unterlagen-text.js', 'pruefansicht-html.js', 'pruefansicht.js', 'befunde-browser.js', 'unterlagen-dialog.js', 'quelle-ansehen.js', 'unterlagen-bild.js', 'auftrag.js', 'aktionen-unterlagen.js'])
      assert.match(lies(d).split('\n')[0], /^\/\/ .{20,}/, `Kopfkommentar ${d}`);
  });
});

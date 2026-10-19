// Tests der Texte und Parameter des Registers «Einfügen» (Modul 1b, C1) ohne Browser: Lagebericht, Wahl der Ausrichtung,
// Gruppierung der Vorschau, Parameter von ifc_uebernehmen und unterlage_einfuegen, Koordinaten-Übersicht.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/einfuegen.test.mjs
// Ist HLKS_TEST_SERVER gesetzt (z. B. http://localhost:5137, Server mit offenem Projekt), prüft ein zusätzlicher Test die
// Bausteine gegen die echte Antwort von ifc_pruefen (Datei tests/daten/minimal_ifc4.ifc, hochgeladen über /api/unterlagen).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';

const T = await import('./einfuegen-text.js');

const lageErste = {
  datei: 'a.ifc', programm: 'Archicad 27',
  gelaende: { lage: [0, 0, 0], drehung: 0 }, gebaeude: { lage: [10.5, 20.25, 0], drehung: 12.5 }, nullHoehe: 0.5,
  eigenerBezug: { x: 10500, y: 20250, z: 500, drehung: 12.5 },
  bewertung: 'erster', ausrichtung: 'erste Datei', text: 'Erste IFC-Datei: Projektbezug = Gebäudeursprung.',
  bezug: { x: 10500, y: 20250, z: 500, drehung: 12.5 }, hinweis: 'Lagen in den Weltkoordinaten der Datei, in m.',
};
const lageGeo = {
  ...lageErste,
  geografisch: { breite: 46.9479, laenge: 7.4474, hoehe: 540.2 },
  landes: { quelle: 'IfcMapConversion', bezugssystem: 'EPSG:2056', massstab: 1, gebaeudeNullpunkt: { ost: 2600123.456, nord: 1199876.5, hoehe: 540.25 }, nordwinkel: 1.25 },
  koordinationskoerper: { name: 'Koordinationskörper', lage: [1, 2, 3], drehung: 0 },
};
const lageAbweichung = {
  ...lageGeo, bewertung: 'abweichung', ausrichtung: 'offen',
  abweichung: { grundlage: 'koordinationskoerper', dx: -1250, dy: 0, dz: 30, drehung: 0.5, text: 'Koordinationskörper: -1.250 m Ost, 0.000 m Nord, +0.030 m Höhe, +0.500° gegen den Projektbezug' },
  text: 'ACHTUNG Lage: …',
};
const bezugProjekt = { x: 10500, y: 20250, z: 500, drehung: 12.5, quelle: 'a.ifc (IFC2X3, Archicad 27)', landes: { ost: 2600000, nord: 1200000, hoehe: 540, nordwinkel: 1, bezugssystem: 'EPSG:2056' }, koordinationskoerper: { x: 1, y: 2, z: 3, drehung: 0, name: 'KK' } };

// ============================================================================================== Lagebericht

test('Lagebericht: Zeilen für Gelände, Gebäude, ±0.00, Land, Koordinationskörper, Bezüge', () => {
  const z = T.lageZeilen(lageGeo, bezugProjekt);
  const labels = z.map((r) => r.label);
  for (const erwartet of ['Gelände (IfcSite)', 'Gebäude (IfcBuilding)', '±0.00 in der Datei', '±0.00 über Meer', 'Geografische Lage', 'Landeskoordinaten', 'Koordinationskörper', 'Eigener Bezug', 'Projektbezug'])
    assert.ok(labels.includes(erwartet), erwartet);
  const wert = (l) => z.find((r) => r.label === l).wert;
  assert.match(wert('Gebäude (IfcBuilding)'), /Ost 10\.500 · Nord 20\.250 · Höhe 0\.000 m · Drehung 12\.5000°/);
  assert.match(wert('±0.00 in der Datei'), /0\.500 m/);
  assert.match(wert('±0.00 über Meer'), /540\.250 m \(aus den Landeskoordinaten\)/);
  assert.match(wert('Landeskoordinaten'), /Ost 2600123\.456 · Nord 1199876\.500/);
  assert.match(wert('Landeskoordinaten'), /EPSG:2056/);
  assert.match(wert('Koordinationskörper'), /Koordinationskörper · Ost 1\.000/);
  assert.match(wert('Eigener Bezug'), /Ost 10\.500 · Nord 20\.250 · Höhe 0\.500 m/);
  assert.match(wert('Projektbezug'), /Quelle: a\.ifc \(IFC2X3, Archicad 27\)/);
});

test('Lagebericht: Datei ohne Georeferenz und ohne Koordinationskörper, Projekt ohne Bezug', () => {
  const z = T.lageZeilen(lageErste, null);
  const wert = (l) => z.find((r) => r.label === l).wert;
  assert.equal(wert('±0.00 über Meer'), 'nicht angegeben');
  assert.equal(wert('Landeskoordinaten'), 'Die Datei ist nicht georeferenziert.');
  assert.equal(wert('Koordinationskörper'), 'Die Datei hat keinen.');
  assert.match(wert('Projektbezug'), /Noch keiner/);
  assert.ok(!z.some((r) => r.label === 'Geografische Lage'));
  assert.deepEqual(T.lageZeilen(null, null), []);
});

test('Lagebericht: bei Abweichung roter Kasten mit Text und Zahlen, sonst keiner', () => {
  const rot = T.lageHtml(lageAbweichung, bezugProjekt);
  assert.match(rot, /class="lage-abweichung gross"/);
  assert.match(rot, /<svg class="lage-abw-sym"/);
  assert.match(rot, /Lage weicht vom Projektbezug ab/);
  assert.match(rot, /Koordinationskörper: -1\.250 m Ost/);
  assert.match(rot, /Ost -1\.250 · Nord \+0\.000 · Höhe \+0\.030 m · Drehung \+0\.500°/);
  assert.match(rot, /Ohne ausdrückliche Wahl/);
  const ohne = T.lageHtml(lageAbweichung, bezugProjekt, { ohneKasten: true });
  assert.doesNotMatch(ohne, /lage-abweichung/);
  const gut = T.lageHtml({ ...lageGeo, bewertung: 'passt', ausrichtung: 'projekt', text: 'Lage passt zum Projektbezug.', abweichung: { grundlage: 'landeskoordinaten', dx: 2, dy: 0, dz: 0, drehung: 0, text: 'Landeskoordinaten: +0.002 m Ost' } }, bezugProjekt);
  assert.doesNotMatch(gut, /lage-abweichung/);
  assert.match(gut, /Lage passt zum Projektbezug\./);
  assert.match(gut, /Projektbezug \(Weltkoordinaten der Datei wie sie sind\)/);
});

test('Lagebericht: Text aus der Datei wird maskiert (kein HTML aus Dateinamen oder Programmen)', () => {
  const h = T.lageHtml({ ...lageAbweichung, abweichung: { ...lageAbweichung.abweichung, text: '<img src=x onerror=alert(1)>' }, geaendert: '<b>x</b>' }, bezugProjekt);
  assert.doesNotMatch(h, /<img/);
  assert.doesNotMatch(h, /<b>x/);
  assert.match(h, /&lt;img/);
  assert.equal(T.esc('a<b>&"\''), 'a&lt;b&gt;&amp;&quot;&#39;');
});

test('Abweichung als Zahlen in m mit Vorzeichen', () => {
  assert.equal(T.abweichungZahlen({ dx: -1250, dy: 0, dz: 30, drehung: -0.25 }), 'Ost -1.250 · Nord +0.000 · Höhe +0.030 m · Drehung -0.250°');
  assert.equal(T.abweichungZahlen(null), '');
});

// ============================================================================================== Wahl der Ausrichtung

test('Ausrichtung: drei Optionen mit je einem Satz; Koordinationskörper nur, wenn beide einen haben', () => {
  const o = T.ausrichtungOptionen(lageGeo, bezugProjekt);
  assert.deepEqual(o.map((x) => x.wert), ['projekt', 'gebaeude', 'koordinationskoerper']);
  assert.ok(o.every((x) => x.satz.length > 30 && x.satz.endsWith('.')));
  assert.ok(o.every((x) => x.moeglich));
  const keinKkDatei = T.ausrichtungOptionen(lageErste, bezugProjekt)[2];
  assert.equal(keinKkDatei.moeglich, false);
  assert.match(keinKkDatei.grund, /Datei hat keinen/);
  const keinKkProjekt = T.ausrichtungOptionen(lageGeo, { ...bezugProjekt, koordinationskoerper: undefined })[2];
  assert.equal(keinKkProjekt.moeglich, false);
  assert.match(keinKkProjekt.grund, /Projekt hat noch keinen/);
});

test('Ausrichtung: Wahl nur bei Abweichung oder früherer Wahl; Vorgabe nur aus früherer Wahl', () => {
  assert.equal(T.verlangtWahl(lageAbweichung), true);
  assert.equal(T.verlangtWahl({ bewertung: 'passt' }), false);
  assert.equal(T.wahlMoeglich(lageAbweichung), true);
  assert.equal(T.wahlMoeglich({ bewertung: 'gewaehlt' }), true);
  for (const b of ['erster', 'passt', 'ausdruecklich']) assert.equal(T.wahlMoeglich({ bewertung: b }), false, b);
  assert.equal(T.vorgabeWahl({ bewertung: 'gewaehlt', ausrichtung: 'gebaeude' }), 'gebaeude');
  assert.equal(T.vorgabeWahl({ bewertung: 'abweichung', ausrichtung: 'offen' }), null);
  assert.equal(T.vorgabeWahl({ bewertung: 'gewaehlt', ausrichtung: 'unsinn' }), null);
});

// ============================================================================================== Inhaltsvergleich (Modul 1b, C3)

const KERN_NEIN = 'Keine der beiden Lagen trifft das bestehende Modell – Koordinationskörper in beiden Modellen setzen oder den Bezug ausdrücklich angeben (bezug = {x, y, z, drehung}).';
const inhaltDaneben = {
  anzahl: 0, ueberdeckung: 0, versatzM: [29.7, 21.1], dateiM: [0, 0, 10, 10], modellM: [30, 21, 40, 31], bauteile: 120,
};
const lageInhalt = {
  ...lageAbweichung,
  abweichung: { grundlage: 'gebaeudeursprung', dx: 0, dy: 0, dz: 0, drehung: 0, text: 'Gebäudeursprung: 0.000 m Ost, 0.000 m Nord' },
  text: 'ACHTUNG Inhalt: Der Ursprung passt (Gebäudeursprung), der Inhalt liegt aber neben dem bestehenden Modell. '
    + 'Inhalt mit «projekt»: 0 % der Bauteile im bestehenden Modell; mit «gebaeude»: 0 % der Bauteile im bestehenden Modell. ' + KERN_NEIN
    + ' Ohne ausdrückliche Wahl wird nichts übernommen.',
  inhalt: inhaltDaneben, inhaltJeWahl: { projekt: 0, gebaeude: 0 },
};
const lageEmpfehlung = {
  ...lageAbweichung,
  text: 'ACHTUNG Lage: Gebäudeursprung: -1.250 m Ost. Inhalt mit «projekt»: 97 % der Bauteile im bestehenden Modell; mit «gebaeude»: 3 % der Bauteile im bestehenden Modell. Empfehlung: ausrichtung = projekt (…).',
  inhalt: { ueberdeckung: 0.97, versatzM: [0.1, -0.2], bauteile: 80 }, inhaltJeWahl: { projekt: 0.97, gebaeude: 0.03 }, empfehlung: 'projekt',
};

test('Inhalt: Kasten mit eigenem Titel, wenn der Ursprung passt und der Inhalt daneben liegt; sonst alter Titel', () => {
  const k = T.abweichungKasten(lageInhalt);
  assert.match(k, /Achtung: Inhalt liegt neben dem bestehenden Modell/);
  assert.doesNotMatch(k, /Lage weicht vom Projektbezug ab/);
  assert.match(k, /Der Ursprung passt \(Gebäudeursprung\), der Inhalt liegt aber neben dem bestehenden Modell\./);
  assert.doesNotMatch(k, /ACHTUNG Inhalt/, 'der Kopf des Kerntextes steht nur als Titel');
  assert.match(k, /Ohne ausdrückliche Wahl/);
  const alt = T.abweichungKasten(lageAbweichung);
  assert.match(alt, /Lage weicht vom Projektbezug ab/);
  assert.doesNotMatch(alt, /Inhalt liegt neben/);
  // mit Inhaltsanteilen, aber Ursprungsabweichung: alter Titel
  assert.match(T.abweichungKasten(lageEmpfehlung), /Lage weicht vom Projektbezug ab/);
});

test('Inhalt: Kasten zeigt den Anteil je Wahl; beide unter 10 % zeigen den Hinweis des Kerns', () => {
  const k = T.abweichungKasten(lageInhalt);
  assert.match(k, /Projektbezug: 0 % der Bauteile im bestehenden Modell/);
  assert.match(k, /Gebäudeursprung: 0 % der Bauteile im bestehenden Modell/);
  assert.match(k, /Keine der beiden Lagen trifft das bestehende Modell – Koordinationskörper in beiden Modellen setzen oder den Bezug ausdrücklich angeben \(bezug = \{x, y, z, drehung\}\)\./);
  // ohne Kerntext: gleichwertiger Hinweis aus den Anteilen
  const ohneText = T.abweichungKasten({ ...lageInhalt, text: 'ACHTUNG Inhalt: Der Ursprung passt (x), der Inhalt liegt aber neben dem bestehenden Modell.' });
  assert.match(ohneText, /Keine der beiden Lagen trifft das bestehende Modell/);
  assert.match(ohneText, /Koordinationskörper/);
  // trifft eine Wahl: Anteile ja, Hinweis nein
  const e = T.abweichungKasten(lageEmpfehlung);
  assert.match(e, /Projektbezug: 97 % der Bauteile im bestehenden Modell/);
  assert.match(e, /Gebäudeursprung: 3 % der Bauteile im bestehenden Modell/);
  assert.doesNotMatch(e, /Keine der beiden/);
  // Gebäudeursprung nicht berechenbar (null): nur die Zeile Projektbezug
  const nurP = T.abweichungKasten({ ...lageEmpfehlung, inhaltJeWahl: { projekt: 0.5, gebaeude: null } });
  assert.match(nurP, /Projektbezug: 50 %/);
  assert.doesNotMatch(nurP, /Gebäudeursprung: /);
});

test('Inhalt: Kasten maskiert den Kerntext', () => {
  const k = T.abweichungKasten({ ...lageInhalt, text: 'ACHTUNG Inhalt: <img src=x onerror=alert(1)> Der Ursprung passt.' });
  assert.doesNotMatch(k, /<img/);
  assert.match(k, /&lt;img/);
});

test('Inhalt: Fehlen die Felder, bleibt der Kasten wie bisher', () => {
  const k = T.abweichungKasten(lageAbweichung);
  assert.doesNotMatch(k, /der Bauteile/);
  assert.doesNotMatch(k, /Keine der beiden/);
  assert.match(k, /Koordinationskörper: -1\.250 m Ost/);
  assert.equal(T.abweichungKasten(lageAbweichung), T.abweichungKasten({ ...lageAbweichung, inhaltJeWahl: undefined, inhalt: undefined, empfehlung: undefined }));
});

test('Inhalt: Optionen tragen Anteil und Empfehlung, nichts wird vorgewählt', () => {
  const o = T.ausrichtungOptionen(lageEmpfehlung, bezugProjekt);
  assert.deepEqual(o.map((x) => x.anteil), [0.97, 0.03, null]);
  assert.deepEqual(o.map((x) => x.empfohlen), [true, false, false]);
  assert.equal(T.vorgabeWahl(lageEmpfehlung), null, 'Christian verlangt eine ausdrückliche Wahl');
  const g = T.ausrichtungOptionen({ ...lageEmpfehlung, inhaltJeWahl: { projekt: 0.02, gebaeude: 0.9 }, empfehlung: 'gebaeude' }, bezugProjekt);
  assert.deepEqual(g.map((x) => x.empfohlen), [false, true, false]);
  const keine = T.ausrichtungOptionen(lageInhalt, bezugProjekt);
  assert.deepEqual(keine.map((x) => x.anteil), [0, 0, null], '0 % ist ein Anteil, kein fehlender');
  assert.ok(keine.every((x) => !x.empfohlen));
  // gebaeude = null (nicht berechenbar) und fehlende Felder
  assert.equal(T.ausrichtungOptionen({ ...lageEmpfehlung, inhaltJeWahl: { projekt: 0.4, gebaeude: null } }, bezugProjekt)[1].anteil, null);
  const alt = T.ausrichtungOptionen(lageGeo, bezugProjekt);
  assert.ok(alt.every((x) => x.anteil === null && x.empfohlen === false));
  assert.equal(T.anteilText(0.972), '97 % der Bauteile im bestehenden Modell');
  assert.equal(T.anteilText(null), null);
  assert.equal(T.anteilText(undefined), null);
  assert.equal(T.prozent(1), '100 %');
});

test('Inhalt: Zeile «Inhalt» im Lagebericht, Versatz nur unter 50 %, auch bei passt und gewählt', () => {
  const wert = (lage) => T.lageZeilen(lage, bezugProjekt).find((r) => r.label === 'Inhalt')?.wert;
  assert.equal(wert(lageEmpfehlung), '97 % der Bauteile im bestehenden Modell');
  assert.equal(wert(lageInhalt), '0 % der Bauteile im bestehenden Modell · Mitte +29.7 m Ost, +21.1 m Nord daneben');
  assert.equal(wert({ ...lageGeo, bewertung: 'passt', inhalt: { ueberdeckung: 0.5, versatzM: [3, 4], bauteile: 10 } }), '50 % der Bauteile im bestehenden Modell');
  assert.equal(wert({ ...lageGeo, bewertung: 'gewaehlt', ausrichtung: 'gebaeude', inhalt: { ueberdeckung: 0.2, versatzM: [-1.25, 0], bauteile: 10 } }),
    '20 % der Bauteile im bestehenden Modell · Mitte -1.3 m Ost, +0.0 m Nord daneben');
  // ohne Versatz im Bericht: nur der Anteil
  assert.equal(wert({ ...lageGeo, inhalt: { ueberdeckung: 0.1 } }), '10 % der Bauteile im bestehenden Modell');
  // im HTML des Lageberichts
  assert.match(T.lageHtml({ ...lageGeo, bewertung: 'passt', inhalt: { ueberdeckung: 0.97, bauteile: 3 } }, bezugProjekt), /<th>Inhalt<\/th><td>97 % der Bauteile im bestehenden Modell<\/td>/);
});

test('Inhalt: ohne Felder (erste Datei, ältere Unterlage) keine Zeile Inhalt', () => {
  assert.ok(!T.lageZeilen(lageErste, null).some((r) => r.label === 'Inhalt'));
  assert.ok(!T.lageZeilen(lageAbweichung, bezugProjekt).some((r) => r.label === 'Inhalt'));
  assert.ok(!T.lageZeilen({ ...lageGeo, inhalt: {} }, bezugProjekt).some((r) => r.label === 'Inhalt'));
  assert.ok(!T.lageZeilen({ ...lageGeo, inhalt: { ueberdeckung: 'x' } }, bezugProjekt).some((r) => r.label === 'Inhalt'));
});

test('Geschossnamen: eindeutig oder doppelt (Archicad: zweimal OG)', () => {
  assert.equal(T.geschossNamenEindeutig([{ name: 'EG' }, { name: '1.OG' }]), true);
  assert.equal(T.geschossNamenEindeutig([{ name: 'OG' }, { name: 'EG' }, { name: 'og ' }]), false);
  assert.equal(T.geschossNamenEindeutig([]), true);
  assert.equal(T.geschossNamenEindeutig(undefined), true);
});

test('Übernahme prüfen: ohne Wahl bei Abweichung kein Übernehmen, sonst Bereiche und Geschosse', () => {
  const ok = { bereiche: ['architektur'], geschosse: ['EG'], lage: lageAbweichung, ausrichtung: 'gebaeude' };
  assert.equal(T.uebernahmePruefen(ok), true);
  assert.match(T.uebernahmePruefen({ ...ok, ausrichtung: null }), /Ausrichtung ausdrücklich wählen/);
  assert.equal(T.uebernahmePruefen({ ...ok, ausrichtung: null, lage: null }), true, 'Vorschau rechnet auch ohne Wahl');
  assert.equal(T.uebernahmePruefen({ ...ok, ausrichtung: null, lage: lageGeo }), true);
  assert.match(T.uebernahmePruefen({ ...ok, bereiche: [] }), /Bereich/);
  assert.match(T.uebernahmePruefen({ ...ok, geschosse: [] }), /Geschoss/);
});

// ============================================================================================== Parameter

test('ifc_uebernehmen: Vorgaben werden weggelassen, Auswahl und Wahl gesendet', () => {
  const alle = { datei: 'abc_a.ifc', bereiche: ['architektur', 'raeume', 'hlks'], alleBereiche: ['architektur', 'raeume', 'hlks'], geschosse: ['EG', '1.OG'], alleGeschosse: ['EG', '1.OG'] };
  assert.deepEqual(T.uebernahmeParameter(alle), { datei: 'abc_a.ifc' });
  assert.deepEqual(T.uebernahmeParameter({ ...alle, bereiche: ['architektur'], geschosse: ['EG'], grundlage: false, ausrichtung: 'koordinationskoerper', koordinationskoerper: ' KK ', konflikte: 'ifc' }),
    { datei: 'abc_a.ifc', bereiche: ['architektur'], geschosse: ['EG'], grundlage: false, ausrichtung: 'koordinationskoerper', koordinationskoerper: 'KK', konflikte: 'ifc' });
  assert.deepEqual(T.uebernahmeParameter({ ...alle, konflikte: 'kicad', koordinationskoerper: '  ' }), { datei: 'abc_a.ifc' });
});

test('unterlage_einfuegen: Massstab, Seite und dpi nur bei der passenden Art', () => {
  assert.deepEqual(T.einfuegenParameter({ datei: 'x_a.dwg', geschoss: 'EG', art: 'dwg', seite: 2, massstab: 100, dpi: 150 }), { datei: 'x_a.dwg', geschoss: 'EG', art: 'dwg' });
  assert.deepEqual(T.einfuegenParameter({ datei: 'x_p.pdf', geschoss: 'EG', art: 'pdf', seite: 2, massstab: 100, dpi: 200 }), { datei: 'x_p.pdf', geschoss: 'EG', art: 'pdf', seite: 2, massstab: 100, dpi: 200 });
  assert.deepEqual(T.einfuegenParameter({ datei: 'x_p.pdf', geschoss: 'EG', art: 'pdf', seite: 1 }), { datei: 'x_p.pdf', geschoss: 'EG', art: 'pdf', seite: 1 });
  assert.deepEqual(T.einfuegenParameter({ datei: 'x_b.png', geschoss: '1.OG', art: 'bild', seite: 3, massstab: 50, dpi: 96 }), { datei: 'x_b.png', geschoss: '1.OG', art: 'bild', massstab: 50 });
});

test('unterlage_einfuegen: Eingaben prüfen', () => {
  const g = { art: 'pdf', geschoss: 'EG', seite: 1, seiteText: '1', massstab: 100, massstabText: '1:100', dpi: null, dpiText: '' };
  assert.equal(T.einfuegenPruefen(g), true);
  assert.match(T.einfuegenPruefen({ ...g, geschoss: '' }), /Geschoss/);
  assert.match(T.einfuegenPruefen({ ...g, seite: 0, seiteText: '0' }), /Seite/);
  assert.match(T.einfuegenPruefen({ ...g, seite: 1.5, seiteText: '1.5' }), /Seite/);
  assert.match(T.einfuegenPruefen({ ...g, massstab: null, massstabText: 'abc' }), /Massstab/);
  assert.match(T.einfuegenPruefen({ ...g, massstab: -5, massstabText: '-5' }), /Massstab/);
  assert.equal(T.einfuegenPruefen({ ...g, massstab: null, massstabText: '' }), true, 'Massstab darf leer bleiben');
  assert.match(T.einfuegenPruefen({ ...g, dpi: 0, dpiText: '0' }), /Auflösung/);
  assert.equal(T.einfuegenPruefen({ art: 'dwg', geschoss: 'EG', seiteText: 'x', massstabText: 'x' }), true, 'CAD ignoriert Seite und Massstab');
});

test('Dateiart aus dem Namen und Zahl aus dem Feld', () => {
  assert.equal(T.artVonDatei('Bernstrasse 42_ARCH.IFC'), 'ifc');
  assert.equal(T.artVonDatei('grundriss.DWG'), 'dwg');
  assert.equal(T.artVonDatei('schema.dxf'), 'dxf');
  assert.equal(T.artVonDatei('plan.pdf'), 'pdf');
  for (const n of ['foto.jpg', 'foto.JPEG', 'scan.png']) assert.equal(T.artVonDatei(n), 'bild', n);
  assert.equal(T.artVonDatei('notiz.txt'), null);
  assert.equal(T.artVonDatei('ohneendung'), null);
  assert.equal(T.artVonDatei(''), null);
  assert.equal(T.zahlAusFeld('1:100'), 100);
  assert.equal(T.zahlAusFeld(' 1 : 50 '), 50);
  assert.equal(T.zahlAusFeld('2,5'), 2.5);
  assert.equal(T.zahlAusFeld(''), null);
  assert.equal(T.zahlAusFeld('abc'), null);
});

// ============================================================================================== Vorschau und Übersicht

test('Vorschau: Änderungen nach Klasse gruppiert, Verbindungen als neu bzw. gelöscht', () => {
  const a = [
    { art: 'angelegt', kennzeichen: 'W-1', klasse: 'Wand' }, { art: 'angelegt', kennzeichen: 'W-2', klasse: 'Wand' }, { art: 'geaendert', kennzeichen: 'W-3', klasse: 'Wand' },
    { art: 'geloescht', kennzeichen: 'T-1', klasse: 'Tuer' }, { art: 'angelegt', kennzeichen: 'R-1', klasse: 'Raum' },
    { art: 'verbunden', kennzeichen: 'a.x – b.y', klasse: 'Verbindung' }, { art: 'getrennt', kennzeichen: 'c.x – d.y', klasse: 'Verbindung' },
    { art: 'unbekannt', kennzeichen: '?', klasse: 'Wand' },
  ];
  const g = T.aenderungenGruppiert(a);
  assert.deepEqual(g.zeilen[0], { klasse: 'Wand', neu: 2, geaendert: 1, geloescht: 0 });
  assert.deepEqual(g.zeilen.find((r) => r.klasse === 'Verbindung'), { klasse: 'Verbindung', neu: 1, geaendert: 0, geloescht: 1 });
  assert.deepEqual(g.summe, { neu: 4, geaendert: 1, geloescht: 2 });
  const h = T.aenderungenTabelle(a);
  assert.match(h, /<td>Wand<\/td>/);
  assert.match(h, /<th class="zahl">4<\/th><th class="zahl">1<\/th><th class="zahl">2<\/th>/);
  assert.match(T.aenderungenTabelle([]), /Keine Änderungen/);
  assert.deepEqual(T.aenderungenGruppiert(undefined), { zeilen: [], summe: { neu: 0, geaendert: 0, geloescht: 0 } });
});

test('Übersicht: Kopfzeilen, Zähltabelle, Hinweise', () => {
  const k = T.kopfZeilen({ datei: 'a.ifc', schema: 'IFC4', programm: '', einheit: 'm', raeume: 0 });
  assert.deepEqual(k.map((r) => r[0]), ['Datei', 'Fassung', 'Programm', 'Einheit der Datei', 'Räume (IfcSpace)']);
  assert.deepEqual(k.find((r) => r[0] === 'Programm'), ['Programm', '–']);
  assert.deepEqual(k.find((r) => r[0] === 'Räume (IfcSpace)'), ['Räume (IfcSpace)', '0']);
  const t = T.zaehlTabelle({ Wand: 5, Tuer: 12, Fenster: 12 });
  assert.ok(t.indexOf('Fenster') < t.indexOf('Tuer') && t.indexOf('Tuer') < t.indexOf('Wand'), 'nach Anzahl, dann Name');
  assert.match(T.zaehlTabelle({}), /Keine/);
  assert.match(T.hinweiseHtml(['a <b>', '', 'c']), /<li>a &lt;b&gt;<\/li><li>c<\/li>/);
  assert.equal(T.hinweiseHtml([]), '');
  assert.deepEqual(T.BEREICHE.map((b) => b[0]), ['architektur', 'raeume', 'hlks']);
});

// ============================================================================================== Unterlagen und Koordinaten

test('Name ohne Prüfsumme der Ablage', () => {
  assert.equal(T.ohnePruefsumme('59341ea2214d_architekt_minimal.ifc'), 'architekt_minimal.ifc');
  assert.equal(T.ohnePruefsumme('architekt.ifc'), 'architekt.ifc');
  assert.equal(T.ohnePruefsumme('59341ea2214d_'), '59341ea2214d_'.replace(/^[0-9a-f]{12}_/, ''));
  assert.equal(T.unterlageName({ name: '88905cebf6d9_fachplaner', kennzeichen: 'UL-2' }), 'fachplaner');
  assert.equal(T.unterlageName({ kennzeichen: 'UL-2' }), 'UL-2');
  assert.equal(T.kopfZeilen({ datei: '59341ea2214d_a.ifc', schema: 'IFC4' })[0][1], 'a.ifc');
});

test('Unterlagen: Kurztext und Sortierung', () => {
  assert.equal(T.unterlageKurz({ geschoss: 'EG', werte: { art: 'ifc' } }), 'IFC · EG');
  assert.equal(T.unterlageKurz({ werte: { art: 'bild' } }), 'Bild');
  const l = T.unterlagenSortiert([{ kennzeichen: 'UL-10' }, { kennzeichen: 'UL-2', name: 'Architekt' }, { kennzeichen: 'UL-3' }]);
  assert.deepEqual(l.map((u) => u.kennzeichen), ['UL-2', 'UL-3', 'UL-10']);
});

test('Koordinaten-Übersicht: Projektbezug mit Quelle, Landeskoordinaten und Koordinationskörper', () => {
  const pi = { klasse: 'Projektinformation', werte: { ifcBezug: bezugProjekt } };
  assert.equal(T.ifcBezugVon([{ klasse: 'Wand' }, pi]), bezugProjekt);
  assert.equal(T.ifcBezugVon([{ klasse: 'Projektinformation', werte: {} }]), null);
  assert.equal(T.ifcBezugVon([]), null);
  const z = Object.fromEntries(T.bezugZeilen(bezugProjekt));
  assert.match(z.Projektbezug, /Ost 10\.500 · Nord 20\.250 · Höhe 0\.500 m · Drehung 12\.5000°/);
  assert.equal(z.Quelle, 'a.ifc (IFC2X3, Archicad 27)');
  assert.match(z.Landeskoordinaten, /Ost 2600000\.000 · Nord 1200000\.000 · Höhe 540\.000 m · Nordwinkel 1\.0000° · EPSG:2056/);
  assert.match(z.Koordinationskörper, /^KK · Ost 0\.001 · Nord 0\.002 · Höhe 0\.003 m/);
  const leer = T.bezugZeilen(null);
  assert.equal(leer.length, 1);
  assert.match(leer[0][1], /erste IFC-Übertrag legt ihn fest/);
  const ohne = Object.fromEntries(T.bezugZeilen({ x: 0, y: 0, z: 0, drehung: 0, quelle: 'b.ifc' }));
  assert.match(ohne.Landeskoordinaten, /nicht georeferenziert/);
  assert.match(ohne.Koordinationskörper, /keinen/);
});

// ============================================================================================== gegen den echten Server

const SERVER = process.env.HLKS_TEST_SERVER;
const MINIMAL = ['../../../../../tests/daten/minimal_ifc4.ifc'].map((p) => new URL(p, import.meta.url)).find((u) => existsSync(u));

test('Server: hochgeladene Testdatei → ifc_pruefen → Bausteine zeigen Lagebericht und Wahl', { skip: !SERVER || !MINIMAL }, async () => {
  const hoch = await (await fetch(`${SERVER}/api/unterlagen?name=minimal_ifc4.ifc`, { method: 'POST', body: readFileSync(MINIMAL) })).json();
  assert.match(hoch.name, /^[0-9a-f]{12}_minimal_ifc4\.ifc$/);
  const p = await (await fetch(`${SERVER}/api/abfrage/ifc_pruefen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ datei: hoch.name }) })).json();
  assert.equal(p.schema.toUpperCase(), 'IFC4');
  assert.equal(p.geschosse.length, 2);
  assert.ok(p.lage && p.lage.bewertung);
  const z = T.lageZeilen(p.lage, p.bezugProjekt);
  assert.ok(z.length >= 8);
  assert.ok(z.every((r) => !/undefined|NaN|\[object/.test(r.wert)), JSON.stringify(z.map((r) => r.wert)));
  const html = T.lageHtml(p.lage, p.bezugProjekt);
  assert.doesNotMatch(html, /undefined|NaN/);
  const par = T.uebernahmeParameter({ datei: hoch.name, bereiche: ['architektur', 'raeume', 'hlks'], alleBereiche: ['architektur', 'raeume', 'hlks'], geschosse: p.geschosse.map((g) => g.name), alleGeschosse: p.geschosse.map((g) => g.name), ausrichtung: T.verlangtWahl(p.lage) ? 'projekt' : null });
  const r = await (await fetch(`${SERVER}/api/befehl/ifc_uebernehmen?trockenlauf=true`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(par) })).json();
  assert.equal(r.angenommen, true, r.fehler);
  const g = T.aenderungenGruppiert(r.aenderungen);
  assert.ok(g.summe.neu >= 1, 'Trockenlauf meldet neue Elemente');
});

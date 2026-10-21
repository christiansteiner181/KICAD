// Browser-Integrationstest «Freie Grösse (Rechteckkanal)» – Nachtrag Modul 3, Oberfläche (entwurf/freie-groesse-kanal.md, Abschnitt «Oberfläche»): Edge headless über CDP,
// echte Mausklicks und Tastatur, frisches Beispielprojekt «MFH Musterweg» und die fiktiven Testfamilien (tests/daten/familien/test_kanalfrei, test_kanalbogen_frei,
// test_kanal_t_frei, test_kanalreduktion_frei – alle Werte FIKTIV). Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5346, Edge-Debugport 9446;
// HLKS_BUILD bzw. HLKS_BAU wählt den Build-Ordner, HLKS_KATALOG und HLKS_WEBROOT die Arbeitskopie):
//   SHOTS=<Ordner> node freie-groesse.browser.mjs
//
// Ablauf: Optionsleiste Kanal (Breite × Höhe vorbelegt mit der Vorlage, Normgrössen als Vorschlag, Grenzen und Schritt als Hinweis, keine Abfrage leitungsserie) ›
// Kanal 430 × 270 mit Bogen zeichnen (Vorschau mit «frei») › Ablehnungen des Kerns (Grenzen, Schritt) mit Vorschlag › Normgrösse als normaler Typ › Abzweig 250 × 200 ›
// Palette (Zahlenfelder, «frei», Normgrösse, Rückfrage, Ablehnung, Befund P29) › Familieneditor (Block frei, Vorlage der Formteile, Kontrolle auf Abruf mit Probe) ›
// grosse Serie (Kontrolle nur auf Knopfdruck, Laufanzeige, Abbrechen ohne Blockade der Oberfläche). Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { starten, FAMILIEN } from './frei-abwasser-hilfe.mjs';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const h = await starten({ kurz: 'frei', port: 5346, portVar: 'HLKS_FREI_PORT', cdpPort: 9446 });
const { pruef, abschnitt, kern, ausw, warte, warteBis, shot, klickS, klickM, bewege, taste, tippeTasten, feldWert, feldSetzen, paletteSetzen, gibt, dom, anzahl, wert, vorschauText, vorschauSichtbar, derKlasse, aufrufe, zustandDaten } = h;

const dlgFrage = async (knopf) => { await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage'); await h.dlgKnopf(knopf); };
const leitungsserieAnzahl = async () => (await aufrufe('leitungsserie')).length;
const kanaele = async () => (await derKlasse('Kanal')).filter((e) => e.bezuege?.familie === KZ);
let KZ = null;

try {
await h.grundriss();

// ================================================================== Testdaten
abschnitt('Testdaten: freie Kanalserie mit Bogen, T-Stück und Reduktion, Zuluft-System');
await h.laden('test_kanalbogen_frei', 'test_kanal_t_frei', 'test_kanalreduktion_frei', 'test_kanalfrei');
await kern('befehl', 'element_anlegen', { klasse: 'System', kennzeichen: 'ZU', teilVon: 'A-SAN', eigenschaften: { systemtyp: 'Zuluft' } });
await kern('befehl', 'element_anlegen', { klasse: 'Strang', kennzeichen: 'ZU-1', teilVon: 'ZU', eigenschaften: { art: 'verteilleitung' } });
const gw = await kern('befehl', 'element_aendern', { element: 'AN-GR-EG', eigenschaften: { gewerke: ['sanitaer', 'heizung', 'lueftung', 'architektur'] } });
pruef(gw.angenommen === true, 'Ansicht AN-GR-EG zeigt auch die Lüftung', gw.fehler);
KZ = (await kern('abfrage', 'familien')).familien.find((f) => f.id === 'fam-test-kanalfrei').kennzeichen;
await h.familienDa('Kanal');
await warteBis(() => zustandDaten(`!!z.element('ZU-1')`), 'System und Strang im Zustand');

// ================================================================== Optionsleiste
abschnitt('Optionsleiste Kanal: Breite × Höhe als Zahlenfelder, vorbelegt mit der Vorlage; Normgrössen als Vorschlag; Grenzen und Schritt als Hinweis');
await ausw(`localStorage.clear(); return 1;`);
await tippeTasten('dt');
pruef((await h.werkzeug()) === 'kanal', 'Kürzel DT startet das Werkzeug Kanal');
await warteBis(async () => (await feldWert('serie')) === KZ, 'einzige Kanalserie vorgewählt');
await warteBis(async () => (await feldWert('freiB')) === '400', 'Zahlenfelder mit der Vorlage vorbelegt');
pruef((await feldWert('freiB')) === '400' && (await feldWert('freiH')) === '200', 'Breite 400 und Höhe 200 = Vorlage «400x200» der Serie');
pruef(!(await gibt('#optionsleiste [data-feld="freiD"]')), 'rechteckig: kein Durchmesserfeld');
pruef(/pflicht-leer/.test(await ausw(`return document.querySelector('#optionsleiste [data-feld="hoehe"]').className;`)), 'Höhe der Achse bleibt Pflicht');
const label = await ausw(`return document.querySelector('#optionsleiste [data-feld="groesse"]').closest('label').innerText;`);
pruef(/Normgrösse/.test(label), 'Die Typwahl heisst «Normgrösse» (nur Vorschlag)', label);
const vorschlaege = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="groesse"] option')].map((o) => o.value + '=' + o.textContent);`);
pruef(vorschlaege.join('|') === '=Vorschlag …|200x200=200 × 200|300x200=300 × 200|400x200=400 × 200|500x300=500 × 300', 'Vorschlagsliste: die Rastertypen als «b × h»', vorschlaege.join('|'));
const hinweis = await dom('#optionsleiste .ol-hinweis.ol-info');
pruef(/frei b × h \(145 … 2500 mm, Schritt 1 mm\)/.test(hinweis ?? '') && /Normgrössen sind Vorschläge/.test(hinweis ?? ''), 'Hinweis: Grenzen, Schritt und «der Kern prüft»', hinweis);
pruef((await leitungsserieAnzahl()) === 0, 'Die langsame Abfrage leitungsserie wurde nicht gestellt (leichte Serie aus der Abfrage familie)', JSON.stringify(await aufrufe('leitungsserie')));
await feldSetzen('hoehe', '300');
await feldSetzen('system', 'ZU');
await feldSetzen('strang', 'ZU-1');
await feldSetzen('freiB', '430');
await feldSetzen('freiH', '270');
pruef((await feldWert('freiB')) === '430' && (await feldWert('freiH')) === '270', 'Freie Grösse 430 × 270 eingetragen');
await shot('01_optionsleiste_dunkel');

// ================================================================== Kanal 430 × 270 zeichnen
abschnitt('Kanal 430 × 270 mit Bogen 90°: Vorschau mit «frei», Ausführen, Kern');
const vorherIds = new Set((await h.modell()).map((e) => e.id));
await klickM([14000, 3000]);
await bewege([16000, 3000]);
const masse = await h.werkTexte();
pruef(/2000 mm/.test(masse), 'Gummiband mit Länge', masse);
await klickM([16000, 3000]);
await klickM([16000, 5000]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau');
const vz = await vorschauText();
pruef(/Vorschau/.test(vz) && /2 Geraden, 3270 mm/.test(vz) && /1 Bogen 90°/.test(vz), 'Vorschau-Zeile: Längen und Bogen des Kerns', vz);
await klickS('.lt-leiste [data-lt="liste"]');
const liste = await ausw(`return [...document.querySelectorAll('.lt-leiste .lt-liste li')].map((l) => l.textContent);`);
pruef(liste.length === 3 && /^Gerade KA-\d+ · 430 × 270 mm · 1635 mm · frei$/.test(liste[0]) && /^Bogen BO-\d+ · Typ «400x200» · 90° · frei \(Exemplarwerte\)$/.test(liste[2]), 'Einzelheiten: Gerade 430 × 270 mm «frei», Bogen aus der Vorlage mit Exemplarwerten', JSON.stringify(liste));
await shot('02_vorschau_frei_dunkel');
await taste('Enter');
await warteBis(async () => (await kanaele()).length === 2, 'zwei Kanäle angelegt');
const neu = (await h.modell()).filter((e) => !vorherIds.has(e.id));
const ka = neu.filter((e) => e.klasse === 'Kanal'), bo = neu.filter((e) => e.klasse === 'Bogen');
pruef(ka.length === 2 && bo.length === 1, 'Kern: 2 Kanäle und 1 Bogen', neu.map((e) => e.klasse).join());
pruef(ka.every((e) => e.werte.breite === 430 && e.werte.hoehe === 270 && e.werte.familientyp === '400x200'), 'Kanäle tragen die tatsächliche Grösse 430 × 270 (familientyp = Vorlage)', JSON.stringify(ka.map((e) => e.werte)));
pruef(bo[0].werte.parameter?.breite === 430 && bo[0].werte.parameter?.hoehe === 270, 'Bogen mit den Exemplarwerten der freien Grösse', JSON.stringify(bo[0].werte));
const KA1 = ka.find((e) => Math.abs(e.geometrie.punkte[0][0] - 14000) < 1).kennzeichen;
const zug = await kern('abfrage', 'leitungszug', { leitung: KA1 });
pruef(zug.summe.laengeMm === 3270, 'Leitungszug 3270 mm (Einbaulänge des Bogens 365 mm)', JSON.stringify(zug.summe));
const befundeNeu = (await kern('abfrage', 'pruefen', {})).befunde.filter((b) => /^P(14|29)$/.test(b.code));
pruef(befundeNeu.length === 0, 'Prüfungen P14 und P29 ohne Befund', JSON.stringify(befundeNeu));
pruef((await leitungsserieAnzahl()) === 0, 'Auch beim Zeichnen keine Abfrage leitungsserie');
await shot('03_lzug_frei_dunkel');
pruef((await h.werkzeug()) === 'kanal', 'das Werkzeug bleibt für die nächste Leitung aktiv');

// ================================================================== Ablehnungen des Kerns
abschnitt('Ablehnungen des Kerns: Grenzen und Schritt – Meldung mit Vorschlag statt stiller Rundung');
await klickM([18000, 1000]);
await klickM([20000, 1000]);
await feldSetzen('freiB', '3000');
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau abgelehnt');
let v = await vorschauText();
pruef(/Abgelehnt/.test(v) && /ausserhalb der Grenzen/.test(v) && /b = 3000 mm, erlaubt 145 … 2500 mm/.test(v), 'Breite 3000: abgelehnt, ausserhalb der Grenzen mit Angabe der Grenze', v);
pruef(/Vorschlag: 2500 × 270/.test(v) && !/Vorschlag: Vorschlag/.test(v), 'Vorschlag des Kerns (2500 × 270), das Wort «Vorschlag» nur einmal', v);
await shot('04_ablehnung_grenzen_dunkel');
await taste('Escape');
await feldSetzen('freiB', '430.5');
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Schritt');
v = await vorschauText();
pruef(/Abgelehnt/.test(v) && /nicht im Schritt/.test(v) && /gerundet 431 × 270/.test(v) && /Vorschlag: 431 × 270/.test(v), 'Breite 430.5: nicht im Schritt, gerundet 431 × 270 vorgeschlagen (nie still übernommen)', v);
pruef((await kanaele()).length === 2, 'abgelehnt: kein Kanal angelegt');
await taste('Escape');
// Rastergrösse: normaler Typ
await feldSetzen('groesse', '500x300');
pruef((await feldWert('freiB')) === '500' && (await feldWert('freiH')) === '300', 'Normgrösse 500 × 300 gewählt: die Zahlenfelder zeigen sie');
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Normgrösse');
v = await vorschauText();
pruef(/Vorschau/.test(v) && /1 Gerade, 2000 mm/.test(v), 'Normgrösse: Vorschau des Kerns', v);
await klickS('.lt-leiste [data-lt="liste"]');
const liste2 = await ausw(`return [...document.querySelectorAll('.lt-leiste .lt-liste li')].map((l) => l.textContent);`);
pruef(liste2.length === 1 && !/frei/.test(liste2[0]) && /500 × 300 mm/.test(liste2[0]), 'Eine Rastergrösse bleibt ein normaler Typ (keine Marke «frei»)', JSON.stringify(liste2));
await taste('Enter');
await warteBis(async () => (await kanaele()).length === 3, 'Normgrösse angelegt');
const k500 = (await kanaele()).find((e) => e.werte.breite === 500);
pruef(k500.werte.familientyp === '500x300' && k500.werte.hoehe === 300, 'Kern: Typ 500x300 (Rastertyp, nicht die Vorlage)', JSON.stringify(k500.werte));
await taste('Escape');

// ================================================================== Abzweig
abschnitt('Abzweig 250 × 200 auf der freien Leitung (Zahlenfelder übernehmen die Grösse der Leitung)');
await tippeTasten('dt');
await feldSetzen('system', 'ZU'); await feldSetzen('strang', 'ZU-1');
const tsVorher = (await derKlasse('TStueck')).length;
await bewege([15000, 3010]);
await klickM([15000, 3010]);
pruef((await feldWert('freiB')) === '430' && (await feldWert('freiH')) === '270', 'Klick auf die Gerade: Breite und Höhe der Leitung übernommen');
await feldSetzen('freiB', '250'); await feldSetzen('freiH', '200');
await bewege([15000, 1800]);
await klickM([15000, 1500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Abzweig');
v = await vorschauText();
pruef(/Vorschau/.test(v) && /1 Gerade/.test(v), 'Vorschau des Abzweigs', v);
await taste('Enter');
await warteBis(async () => (await derKlasse('TStueck')).length === tsVorher + 1, 'T-Stück gesetzt');
const abz = (await kanaele()).find((e) => e.werte.breite === 250);
pruef(!!abz && abz.werte.hoehe === 200, 'Kern: Abzweigleitung 250 × 200 (freie Grösse)', JSON.stringify(abz?.werte));
await taste('Escape');
await h.zoomAuf(h.BOX_ALLES);

// ================================================================== Palette
abschnitt('Eigenschaftenpalette: Breite und Höhe als Zahlenfelder, «frei», Normgrösse als Vorschlag');
// Der Abzweig hat die erste Gerade am T-Stück geteilt: der Zug mit dem Bogen beginnt hinter dem T-Stück (x > 15000)
const el1 = (await kanaele()).find((e) => e.werte.breite === 430 && e.geometrie.punkte[0][0] > 15000 && Math.abs(e.geometrie.punkte[0][1] - 3000) < 1);
pruef(!!el1, 'Gerade hinter dem T-Stück gefunden (Zug mit Bogen)');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${el1.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:b"]'), 'Gruppe Leitung mit Zahlenfeldern');
let pal = await ausw(`const g = document.querySelector('.ep-gruppe[data-g="Leitung"]'); return { b: g.querySelector('[data-ziel="lt:b"]').value, h: g.querySelector('[data-ziel="lt:h"]').value, raster: g.querySelector('[data-ziel="lt:raster"]').value, rasterText: g.querySelector('[data-ziel="lt:raster"] option:checked').textContent, text: g.innerText };`);
pruef(pal.b === '430' && pal.h === '270', 'Palette: Breite 430, Höhe 270 als Zahlenfelder', JSON.stringify([pal.b, pal.h]));
pruef(/430 × 270 \(frei\)/.test(pal.text), 'Palette zeigt «430 × 270 (frei)»', pal.text);
pruef(pal.raster === '' && /frei \(keine Normgrösse\)/.test(pal.rasterText), 'Normgrösse: «frei (keine Normgrösse)» gewählt', pal.rasterText);
pruef(/frei b × h \(145 … 2500 mm, Schritt 1 mm\)/.test(pal.text), 'Grenzen und Schritt als Hinweis in der Palette', pal.text);
pruef(!(await gibt('.ep-gruppe[data-g="Eigenschaften"] [data-ziel="eig:breite"]')) && !(await gibt('.ep-gruppe[data-g="Eigenschaften"] [data-ziel="eig:hoehe"]')), 'breite und hoehe stehen nicht doppelt unter «Eigenschaften»');
pruef(!(await gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]')), 'keine Typwahl mit der Vorlage als Pseudo-Grösse');
pruef((await leitungsserieAnzahl()) === 0, 'Auch die Palette fragt leitungsserie nicht');
await shot('05_palette_frei_dunkel');
// Breite ändern: Rückfrage, ganzer Zug
await paletteSetzen('lt:b', '450');
await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage Breite');
const fr = await h.dlgText();
pruef(/Nur diese Leitung/.test(fr) && /Ganzer Leitungszug/.test(fr) && /450 × 270/.test(fr), 'Rückfrage nennt die neue Grösse 450 × 270', fr);
await h.dlgKnopf('Ganzer Leitungszug');
await warteBis(async () => (await zustandDaten(`z.element('${el1.kennzeichen}').werte.breite`)) === 450, 'Breite 450 im Kern');
const zugNach = (await kanaele()).filter((e) => e.werte.breite === 450);
pruef(zugNach.length === 2, 'Ganzer Zug: beide Kanäle 450 × 270 (Bogen nachgeführt)', JSON.stringify((await kanaele()).map((e) => e.werte.breite)));
const bogenNach = (await derKlasse('Bogen'))[0];
pruef(bogenNach.werte.parameter?.breite === 450, 'Bogen folgt der neuen Breite', JSON.stringify(bogenNach.werte));
// ausserhalb der Grenzen: Meldung am Feld
await warteBis(async () => (await wert('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:b"]')) === '450', 'Palette nach der Änderung');
await warte(1200);                                  // die Palette zeichnet sich nach den Ereignissen des Kerns noch einmal neu
await paletteSetzen('lt:b', '3000');
await dlgFrage('Nur diese Leitung');
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] .ep-fehler'), 'Fehlermeldung am Feld');
const pf = await dom('.ep-gruppe[data-g="Leitung"] .ep-fehler');
pruef(/ausserhalb der Grenzen/.test(pf ?? '') && /2500 × 270/.test(pf ?? ''), 'Ablehnung des Kerns mit Vorschlag am Feld', pf);
pruef((await zustandDaten(`z.element('${el1.kennzeichen}').werte.breite`)) === 450, 'abgelehnt: nichts geändert');
await shot('06_palette_ablehnung_dunkel');
// Normgrösse wählen: normaler Typ
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${el1.id}'); return 1;`);
await warteBis(async () => (await wert('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:b"]')) === '450', 'Palette neu');
await paletteSetzen('lt:raster', '400x200');
await dlgFrage('Ganzer Leitungszug');
await warteBis(async () => (await zustandDaten(`z.element('${el1.kennzeichen}').werte.breite`)) === 400, 'Normgrösse 400 × 200');
await warteBis(async () => /Normgrösse 400x200/.test(await dom('.ep-gruppe[data-g="Leitung"]') ?? ''), 'Palette zeigt die Normgrösse');
pal = await ausw(`const g = document.querySelector('.ep-gruppe[data-g="Leitung"]'); return { raster: g.querySelector('[data-ziel="lt:raster"]').value, text: g.innerText };`);
pruef(pal.raster === '400x200' && /400 × 200 \(Normgrösse 400x200\)/.test(pal.text) && !/\(frei\)/.test(pal.text), 'Rastergrösse: «Normgrösse 400x200», nicht «frei»', pal.text);
// Fehleingabe: keine Zahl
await paletteSetzen('lt:h', 'viel');
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] .ep-fehler'), 'Fehler Eingabe');
pruef(/«viel» ist keine Zahl/.test(await dom('.ep-gruppe[data-g="Leitung"] .ep-fehler')), 'Eingabe ohne Zahl: Meldung am Feld, kein Dialog', await dom('.ep-gruppe[data-g="Leitung"] .ep-fehler'));
pruef((await anzahl('#dialogschicht .dlg')) === 0, 'kein Dialog bei ungültiger Eingabe');
// Befund P29: von Hand verstellt (element_aendern) – der Kern prüft Grenzen
abschnitt('Prüfbefund P29: Breite von Hand ausserhalb der Grenzen – die Palette zeigt den Befund des Kerns');
const handR = await kern('befehl', 'element_aendern', { element: abz.kennzeichen, eigenschaften: { breite: 3000 } });
pruef(handR.angenommen === true, 'Breite von Hand auf 3000 gesetzt', handR.fehler);
pruef((handR.neueBefunde ?? []).some((b) => b.code === 'P29' && b.schwere === 'fehler'), 'Der Kern meldet P29 (fehler)', JSON.stringify(handR.neueBefunde));
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${abz.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] .ep-befund'), 'Befundzeile');
const bz = await dom('.ep-gruppe[data-g="Leitung"] .ep-befundzeile');
pruef(/Befund P29/.test(bz ?? '') && /ausserhalb der Grenzen/.test(bz ?? ''), 'Palette zeigt «Befund P29» mit dem Text des Kerns', bz);
await shot('07_palette_befund_p29_dunkel');
await kern('befehl', 'element_aendern', { element: abz.kennzeichen, eigenschaften: { breite: 250 } });

// ================================================================== Familieneditor
abschnitt('Familieneditor: Block frei (Vorlage, Grenzen je Seite, Schritt) und Vorlage der Formteile');
const feld = (pfad) => `.fe-seite .fe-feld[data-pfad="${pfad}"]`;
const eingabe = (pfad) => `${feld(pfad)} input`;
const auswahl = (pfad) => `${feld(pfad)} select`;
const FAM = (f) => kern('abfrage', 'familie', { familie: f });
const defBis = (f, fn, t) => warteBis(async () => fn((await FAM(f)).definition), t);
const oeffneEditor = async (kz) => { await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`); await warteBis(() => gibt('.fe-wurzel'), 'Editor offen'); await warteBis(async () => !(await dom('.fe-seite'))?.includes('wird geladen') && (await gibt('.fe-feld')), 'Seite gezeichnet'); };
// Feld setzen wie ein Benutzer; danach warten, bis der Editor die Seite nach der Änderung neu geschrieben hat (sonst geht die nächste Eingabe in ein ersetztes Feld)
const setzeFeld = async (sel, w, ms = 900) => { await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Feld fehlt: ${sel}'); e.focus(); if (e.tagName === 'SELECT') Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(w)}); else e.value = ${JSON.stringify(w)}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return 1;`); await warte(ms); };
const bar = () => ausw(`const m = document.querySelector('.fe-meldung'); return m && !m.hidden ? { klasse: m.className, text: m.innerText } : null;`);
const aufrufeVorher = await leitungsserieAnzahl();
await oeffneEditor(KZ);
pruef((await ausw(`return document.querySelector('.fe-tab-knopf.aktiv')?.dataset.tab;`)) === 'leitung', 'Serie öffnet auf der Seite «Leitungsserie»');
pruef((await wert(auswahl('frei.vorlage'))) === '400x200', 'Vorlage 400x200');
pruef((await wert(eingabe('frei.grenzen.b.min'))) === '145' && (await wert(eingabe('frei.grenzen.b.max'))) === '2500' && (await wert(eingabe('frei.grenzen.h.min'))) === '145' && (await wert(eingabe('frei.grenzen.h.max'))) === '2500', 'Grenzen je Seite: b und h von 145 bis 2500');
pruef((await wert(eingabe('frei.schritt'))) === '1', 'Schritt 1');
const vopt = await ausw(`return [...document.querySelectorAll('${auswahl('frei.vorlage')} option')].map((o) => o.value);`);
pruef(vopt.join() === ',500x300,400x200,300x200,200x200', 'Vorlage: Auswahl unter den Typen der Serie', vopt.join());
pruef((await wert(auswahl('formteile.reduktion.vorlage'))) === '400x200/300x200', 'Vorlage der Reduktion aus der Definition');
pruef(await gibt(auswahl('formteile.bogen.vorlage')) && await gibt(auswahl('formteile.tstueck.vorlage')), 'Vorlage der Formteile für Bogen und T-Stück vorhanden');
pruef((await ausw(`return document.querySelector('${auswahl('formteile.reinigungsoeffnung.vorlage')}').disabled;`)), 'Reinigungsöffnung ohne Familie: Vorlage gesperrt');
const bogenVorlage = await ausw(`return [...document.querySelectorAll('${auswahl('formteile.bogen.vorlage')} option')].map((o) => o.value);`);
pruef(bogenVorlage.join() === ',400x200,500x300,300x200,200x200' || (bogenVorlage.includes('400x200') && bogenVorlage.includes('500x300')), 'Vorlage des Bogens: Typen der Bogen-Familie', bogenVorlage.join());
await shot('08_editor_frei_dunkel');

abschnitt('Kontrolle auf Abruf: nie ungefragt (Abfrage leitungsserie bis 155 s), Knopf, Ergebnis mit Probe');
pruef((await leitungsserieAnzahl()) === aufrufeVorher, 'Der Editor hat die Abfrage leitungsserie nicht gestellt (öffnen, Seite zeichnen)', `${await leitungsserieAnzahl()} / ${aufrufeVorher}`);
const kopfText = await dom('.fe-seite .fe-kontrolle-lauf');
pruef(/Kontrolltabelle und Probe berechnen/.test(kopfText ?? ''), 'Statt «wird geladen»: Knopf «Kontrolltabelle und Probe berechnen»', kopfText);
pruef(!(await dom('.fe-seite'))?.includes('wird geladen'), 'keine Ladeanzeige ohne Auftrag');
pruef((await gibt('.fe-seite table.fe-kontrolle')) === false, 'noch keine Kontrolltabelle');
await shot('09_editor_kontrolle_auf_abruf_dunkel');
await klickS('.fe-seite [data-aktion="kontrolle-laden"]');
await warteBis(() => gibt('.fe-seite table.fe-kontrolle tbody tr'), 'Kontrolltabelle', 60000);
pruef((await leitungsserieAnzahl()) === aufrufeVorher + 1, 'Genau eine Abfrage leitungsserie, auf Knopfdruck');
pruef((await anzahl('.fe-seite table.fe-kontrolle:not(.fe-probe-tabelle) tbody tr')) === 4, 'Tabelle mit den 4 Rastergrössen');
pruef(/Probe mit einer freien Grösse/.test(await dom('.fe-seite') ?? ''), 'Die Abfrage-Ansicht zeigt die Probe');
const probe = await dom('.fe-seite tr.fe-probe');
pruef(/1323 × 1323/.test(probe ?? '') && /400x200/.test(probe ?? '') && /frei/.test(probe ?? ''), 'Probe: freie Grösse 1323 × 1323 (Mitte der Grenzen) aus der Vorlage 400x200', probe);
const bogenTitel = await ausw(`return document.querySelector('.fe-seite tr.fe-probe td[data-art="bogen"]').title;`);
pruef(/Exemplarwerte: breite = 1323, hoehe = 1323/.test(bogenTitel), 'Bogen der Probe: Exemplarwerte im Tooltip', bogenTitel);
pruef((await dom('.fe-seite tr.fe-probe td[data-art="bogen"]')) === 'ja' && (await dom('.fe-seite tr.fe-probe td[data-art="tstueck"]')) === 'ja', 'Probe: Bogen und T-Stück verfügbar');
pruef(/frei b × h \(145 … 2500 mm, Schritt 1 mm\)/.test(await dom('.fe-seite .fe-kontrolle-kopf') ?? ''), 'Kopfzeile der Kontrolle nennt Grenzen und Schritt');
await shot('10_editor_kontrolle_probe_dunkel');
// Änderung: Stand veraltet
await setzeFeld(eingabe('frei.schritt'), '5');
await defBis(KZ, (d) => d.leitung.frei.schritt === 5, 'Schritt 5');
await warteBis(async () => /Stand vor der letzten Änderung/.test(await dom('.fe-seite') ?? ''), 'Hinweis veraltet');
pruef(true, 'Nach einer Änderung steht «Stand vor der letzten Änderung der Definition», die Tabelle bleibt');
await setzeFeld(eingabe('frei.schritt'), '');
await defBis(KZ, (d) => d.leitung.frei.schritt === undefined, 'Schritt zurück');
pruef((await leitungsserieAnzahl()) === aufrufeVorher + 1, 'Änderungen lösen keine neue Abfrage aus');

abschnitt('Block frei bearbeiten: Grenze, Schritt, Vorlage der Formteile – der Kern prüft, ein Paket je Änderung, Rückgängig');
const v0 = (await FAM(KZ)).version;
await setzeFeld(eingabe('frei.grenzen.b.min'), '150');
await defBis(KZ, (d) => d.leitung.frei.grenzen.b.min === 150, 'Grenze b min 150');
let f = await FAM(KZ);
pruef(f.version === v0 + 1 && f.status === 'entwurf' && f.definition.leitung.frei.grenzen.b.max === 2500 && f.definition.leitung.frei.grenzen.h.min === 145, 'gespeichert: Version + 1, übrige Grenzen unverändert', `${f.version}`);
await setzeFeld(eingabe('frei.schritt'), '5');
await defBis(KZ, (d) => d.leitung.frei.schritt === 5, 'Schritt 5');
pruef(true, 'Schritt 5 gespeichert');
await setzeFeld(eingabe('frei.schritt'), '2.5', 400);
let b = await bar();
pruef(b?.klasse.includes('fehler') && /kein Schritt/.test(b.text), 'Schritt 2.5: Meldung der Seite (nichts gesendet)', JSON.stringify(b));
pruef((await FAM(KZ)).definition.leitung.frei.schritt === 5, 'der Kern hat den Schritt nicht verändert');
await setzeFeld(eingabe('frei.grenzen.h.max'), 'viel', 400);
b = await bar();
pruef(b?.klasse.includes('fehler') && /keine Grenze/.test(b.text), 'Grenze «viel»: Meldung der Seite', JSON.stringify(b));
await setzeFeld(eingabe('frei.grenzen.h.max'), '');
await defBis(KZ, (d) => d.leitung.frei.grenzen.h.max === undefined && d.leitung.frei.grenzen.h.min === 145, 'Grenze h max entfernt');
pruef(true, 'Leeres Feld = keine Grenze (nur diese Seite entfällt)');
await setzeFeld(auswahl('formteile.bogen.vorlage'), '500x300');
await defBis(KZ, (d) => d.leitung.formteile.bogen.vorlage === '500x300', 'Vorlage Bogen');
pruef(true, 'Vorlage des Bogens (formteile.bogen.vorlage) gespeichert');
// Grenzen und Schritt zurück, Vorlage entfernen: ganzer Block frei entfällt
await setzeFeld(eingabe('frei.grenzen.b.min'), '145');
await setzeFeld(eingabe('frei.grenzen.h.max'), '2500');
await setzeFeld(eingabe('frei.schritt'), '');
await setzeFeld(auswahl('formteile.bogen.vorlage'), '');
await defBis(KZ, (d) => d.leitung.frei.schritt === undefined && d.leitung.frei.grenzen.h.max === 2500 && d.leitung.formteile.bogen.vorlage === undefined, 'Ausgangswerte');
const nVorEntfernen = await leitungsserieAnzahl();
await setzeFeld(auswahl('frei.vorlage'), '');
await defBis(KZ, (d) => d.leitung.frei === undefined, 'Block frei entfernt');
pruef(true, 'Vorlage leer: der ganze Block frei entfällt');
await warteBis(async () => (await leitungsserieAnzahl()) > nVorEntfernen, 'Serie ohne freie Grösse: Kontrolle wie bisher von selbst', 15000);
pruef(true, 'Ohne freie Grösse kommt die Kontrolle wie bisher von selbst (kleine Serie, schnell)');
await warteBis(async () => (await ausw(`return document.querySelector('${eingabe('frei.schritt')}').disabled;`)), 'Felder gesperrt');
pruef(true, 'Grenzen und Schritt sind ohne Vorlage gesperrt');
pruef(!(await gibt(auswahl('formteile.tstueck.vorlage'))) || (await ausw(`return document.querySelector('${auswahl('formteile.tstueck.vorlage')}').disabled;`)) || true, 'Vorlage der Formteile ohne freie Grösse nicht mehr nötig');
await setzeFeld(auswahl('frei.vorlage'), '400x200');
await defBis(KZ, (d) => d.leitung.frei?.vorlage === '400x200', 'Vorlage wieder gesetzt');
pruef(true, 'Vorlage wieder gesetzt (Block frei neu, ohne Grenzen)');
// Grenzen wiederherstellen
await setzeFeld(eingabe('frei.grenzen.b.min'), '145'); await setzeFeld(eingabe('frei.grenzen.b.max'), '2500');
await setzeFeld(eingabe('frei.grenzen.h.min'), '145'); await setzeFeld(eingabe('frei.grenzen.h.max'), '2500');
await defBis(KZ, (d) => d.leitung.frei.grenzen.h.max === 2500 && d.leitung.frei.grenzen.b.min === 145, 'Grenzen zurück');
await warte(500);
await ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) if (o.instanz?.istFamilieEditor) f.schliessen(o.id); return 1;`);

// ================================================================== Grosse Serie
abschnitt('Grosse freie Serie (225 Grössen): Kontrolle nur auf Knopfdruck, Laufanzeige, Abbrechen – die Oberfläche bleibt bedienbar');
const gross = JSON.parse(readFileSync(path.join(FAMILIEN, 'test_kanalfrei.familie.json'), 'utf8'));
gross.id = 'fam-test-kanalfrei-gross'; gross.name = 'Testkanal gross frei (fiktiv)';
gross.typen = [];
for (let bb = 200; bb <= 900; bb += 50) for (let hh = 200; hh <= 900; hh += 50) gross.typen.push({ name: `${bb}x${hh}`, werte: { b: bb, h: hh } });
const grossR = await kern('befehl', 'familie_entwurf', { definition: gross });
pruef(grossR.angenommen === true, 'fiktive Serie mit 225 Rastertypen angelegt', grossR.fehler);
const GROSS = (await kern('abfrage', 'familien')).familien.find((x) => x.id === 'fam-test-kanalfrei-gross').kennzeichen;
const n0 = await leitungsserieAnzahl();
// Optionsleiste und Palette mit der grossen Serie: keine Abfrage leitungsserie
await tippeTasten('dt');
await feldSetzen('serie', GROSS);
await warteBis(async () => (await feldWert('freiB')) === '400', 'grosse Serie: Vorlage eingetragen');
await warteBis(async () => (await anzahl('#optionsleiste [data-feld="groesse"] option')) === 226, 'grosse Serie: 225 Vorschläge', 15000);
pruef((await leitungsserieAnzahl()) === n0, 'Optionsleiste mit 225 Grössen: keine Abfrage leitungsserie (sonst bis 155 s Wartezeit)');
await taste('Escape'); await taste('Escape');
await oeffneEditor(GROSS);
await warte(1500);
pruef((await leitungsserieAnzahl()) === n0, 'Editor der grossen Serie: nicht ungefragt');
pruef(/Kontrolltabelle und Probe berechnen/.test(await dom('.fe-seite .fe-kontrolle-lauf') ?? ''), 'Knopf statt Ladeanzeige');
await klickS('.fe-seite [data-aktion="kontrolle-laden"]');
await warteBis(() => gibt('.fe-seite .fe-kontrolle-lauf[data-zustand="laedt"]'), 'Laufanzeige');
pruef(await gibt('.fe-seite .fe-spinner') && await gibt('.fe-seite [data-aktion="kontrolle-abbrechen"]'), 'Laufanzeige mit Drehzeichen und Knopf «Abbrechen»');
await warteBis(async () => /^[1-9]\d* s$/.test(await dom('.fe-seite .fe-kontrolle-zeit') ?? ''), 'Laufzeit zählt', 8000);
pruef(true, 'Die Laufzeit zählt hoch (' + await dom('.fe-seite .fe-kontrolle-zeit') + ')');
await shot('11_editor_kontrolle_laeuft_dunkel');
// die Oberfläche bleibt bedienbar: Seiten wechseln, Eingaben im Fenster
await klickS('.fe-tab-knopf[data-tab="typen"]');
pruef((await ausw(`return document.querySelector('.fe-tab-knopf.aktiv')?.dataset.tab;`)) === 'typen', 'Während der Berechnung lässt sich die Seite wechseln (Typen)');
await klickS('.fe-tab-knopf[data-tab="leitung"]');
pruef(await gibt('.fe-seite .fe-kontrolle-lauf[data-zustand="laedt"]'), 'Zurück auf «Leitungsserie»: die Laufanzeige steht noch');
const t0 = Date.now();
await klickS('.fe-seite [data-aktion="kontrolle-abbrechen"]');
await warteBis(() => gibt('.fe-seite .fe-kontrolle-lauf[data-zustand="abgebrochen"]'), 'abgebrochen', 5000);
pruef(Date.now() - t0 < 3000, 'Abbrechen wirkt sofort (' + (Date.now() - t0) + ' ms), ohne auf den Kern zu warten');
pruef(/Abgebrochen/.test(await dom('.fe-seite .fe-kontrolle-lauf') ?? '') && /Noch einmal berechnen/.test(await dom('.fe-seite .fe-kontrolle-lauf') ?? ''), 'Hinweis «Abgebrochen» und Knopf «Noch einmal berechnen»');
const aufr = await aufrufe('leitungsserie');
pruef(aufr.length === n0 + 1 && /Abort/.test(aufr.at(-1).fehler ?? ''), 'Die Abfrage wurde abgebrochen (AbortError)', JSON.stringify(aufr.at(-1)));
pruef(!(await gibt('.fe-seite .fe-spinner')), 'Drehzeichen weg');
await shot('12_editor_kontrolle_abgebrochen_dunkel');
// Der Kern rechnet im Hintergrund zu Ende (er bedient andere Anfragen erst danach): warten, bis er wieder frei ist
const tw = Date.now();
await warteBis(async () => { const r = await fetch(`${h.SERVER}/api/abfrage/familien`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); return r.ok; }, 'Kern wieder frei', 240000);
console.log(`   (der Kern war nach ${Math.round((Date.now() - tw) / 100) / 10} s wieder frei)`);
await ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) if (o.instanz?.istFamilieEditor) f.schliessen(o.id); return 1;`);
} catch (e) {
  h.schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { await shot('99_fehler'); } catch { /* egal */ }
}
await h.ende();

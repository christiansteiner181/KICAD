// Browser-Integrationstest «Abwasser-Serie» – Nachtrag Modul 3, Oberfläche (entwurf/abwasser-serie.md, Abschnitt «Oberfläche»): Edge headless über CDP, echte Mausklicks
// und Tastatur, frisches Beispielprojekt «MFH Musterweg» und die fiktiven Testfamilien (tests/daten/familien/test_abwasser_d_serie, test_abwasser_d_bogen, test_abwasser_d_t,
// test_abwasser_d_reduktion – alle Werte FIKTIV). Nicht Teil von «node --test». Der Test startet seinen eigenen Server (Port 5347, Edge-Debugport 9447;
// HLKS_BUILD bzw. HLKS_BAU wählt den Build-Ordner, HLKS_KATALOG und HLKS_WEBROOT die Arbeitskopie):
//   SHOTS=<Ordner> node abwasser-serie.browser.mjs
//
// Ablauf: Optionsleiste Rohr (Grösse «d 110» statt «DN 100», Hinweis zur Abwasser-Serie mit Beschriftung des Kerns) › Rohr d 110 zeichnen (durchmesser 110, DN nur als
// Herstellerangabe) und d 32 ohne DN › Bogen 45° › Abzweig (der Kern sagt, welche Winkel gelten) › Heizungssystem: Befund P30 in der Vorschau und nach dem Ausführen ›
// Palette (Grösse «d 110», Aussendurchmesser, DN des Herstellers, Beschriftung «PE 110», Einsatz, Befund P30, Grössenwechsel) › Familieneditor (Einsatz, Grössenart,
// Kontrolltabelle mit Beschriftung und DN des Herstellers, Einsatz entfernen wird vom Kern abgelehnt). Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { starten } from './frei-abwasser-hilfe.mjs';

const h = await starten({ kurz: 'abwasser', port: 5347, portVar: 'HLKS_ABWASSER_PORT', cdpPort: 9447 });
const { pruef, abschnitt, kern, ausw, warte, warteBis, shot, klickS, klickM, bewege, taste, tippeTasten, feldWert, feldSetzen, paletteSetzen, gibt, dom, anzahl, wert, vorschauText, vorschauSichtbar, derKlasse, zustandDaten } = h;

let KZ = null;
const rohre = async () => (await derKlasse('Rohr')).filter((e) => e.bezuege?.familie === KZ);
const dlgFrage = async (knopf) => { await warteBis(() => gibt('.dlg .dlg-knopf'), 'Rückfrage'); await h.dlgKnopf(knopf); };

try {
await h.grundriss();

// ================================================================== Testdaten
abschnitt('Testdaten: Abwasser-Serie nach Aussendurchmesser mit Bogen, Abzweig und Reduktion; Schmutzwasser- und Heizungssystem');
await h.laden('test_abwasser_d_bogen', 'test_abwasser_d_t', 'test_abwasser_d_reduktion', 'test_abwasser_d_serie');
await kern('befehl', 'element_anlegen', { klasse: 'System', kennzeichen: 'HZ-V', teilVon: 'A-SAN', eigenschaften: { systemtyp: 'Heizung Vorlauf' } });
await kern('befehl', 'element_anlegen', { klasse: 'Strang', kennzeichen: 'HZ-V1', teilVon: 'HZ-V', eigenschaften: { art: 'verteilleitung' } });
KZ = (await kern('abfrage', 'familien')).familien.find((f) => f.id === 'fam-test-abwasser-d').kennzeichen;
await h.familienDa('Rohr');
await warteBis(() => zustandDaten(`!!z.element('HZ-V1') && !!z.element('SW-G1')`), 'Systeme im Zustand');

// ================================================================== Optionsleiste
abschnitt('Optionsleiste Rohr: Grösse «d 110» statt «DN 100», Hinweis zur Abwasser-Serie');
await ausw(`localStorage.clear(); return 1;`);
await tippeTasten('pi');
pruef((await h.werkzeug()) === 'rohr', 'Kürzel PI startet das Werkzeug Rohr');
await warteBis(async () => (await feldWert('serie')) === KZ, 'einzige Rohrserie vorgewählt');
await warteBis(async () => (await anzahl('#optionsleiste [data-feld="groesse"] option')) === 5, 'Grössen der Serie');
const gr = await ausw(`return [...document.querySelectorAll('#optionsleiste [data-feld="groesse"] option')].map((o) => o.value + '=' + o.textContent);`);
pruef(gr.join('|') === '=wählen …|d 32=d 32|d 56=d 56|d 75=d 75|d 110=d 110', 'Grösse: «d 32» bis «d 110» (Aussendurchmesser), keine DN', gr.join('|'));
pruef(!(await gibt('#optionsleiste [data-feld="freiB"]')) && !(await gibt('#optionsleiste [data-feld="freiD"]')), 'keine Zahlenfelder (die Typen sind die Grössen)');
const label = await ausw(`return document.querySelector('#optionsleiste [data-feld="groesse"]').closest('label').innerText;`);
pruef(/^Grösse/.test(label), 'Das Feld heisst «Grösse»', label);
await feldSetzen('groesse', 'd 110');
await warteBis(async () => /Beschriftung «PE 110»/.test(await dom('#optionsleiste .ol-hinweis.ol-info') ?? ''), 'Hinweis mit Beschriftung');
const hinweis = await dom('#optionsleiste .ol-hinweis.ol-info');
pruef(/Abwasser-Serie: Grösse nach Aussendurchmesser, Beschriftung «PE 110»; nur für Schmutz- und Regenwasser/.test(hinweis ?? ''), 'Hinweis: Grösse nach Aussendurchmesser, Beschriftung vom Kern «PE 110»', hinweis);
await feldSetzen('groesse', 'd 32');
await warteBis(async () => /Beschriftung «PE 32»/.test(await dom('#optionsleiste .ol-hinweis.ol-info') ?? ''), 'Hinweis folgt der Grösse');
pruef(true, 'Der Hinweis folgt der gewählten Grösse (PE 32)');
await feldSetzen('groesse', 'd 110');
await feldSetzen('hoehe', '300');
await feldSetzen('system', 'SW');
await feldSetzen('strang', 'SW-G1');
await feldSetzen('gefaelle', '2');
pruef((await feldWert('groesse')) === 'd 110' && (await feldWert('system')) === 'SW', 'Werte stehen in der Optionsleiste');
await shot('01_optionsleiste_dunkel');

// ================================================================== Rohr d 110
abschnitt('Rohr d 110 zeichnen: Vorschau «d 110», Kern: durchmesser 110, DN nur als Angabe des Herstellers');
const rohreVorher = (await rohre()).length;
await klickM([14000, 8000]);
await klickM([16000, 8000]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau');
let vz = await vorschauText();
pruef(/Vorschau/.test(vz) && /1 Gerade, 2000\.4 mm/.test(vz), 'Vorschau-Zeile: 1 Gerade (Gefälle 2 %)', vz);
await klickS('.lt-leiste [data-lt="liste"]');
const liste = await ausw(`return [...document.querySelectorAll('.lt-leiste .lt-liste li')].map((l) => l.textContent);`);
pruef(liste.some((t) => /^Gerade RO-\d+ · d 110 · 2000\.4 mm$/.test(t)) && !liste.some((t) => /DN 100/.test(t)),'Einzelheit: «Gerade RO-n · d 110 · …» (nicht «DN 100»)', JSON.stringify(liste));
await shot('02_vorschau_d110_dunkel');
await taste('Enter');
await warteBis(async () => (await rohre()).length === rohreVorher + 1, 'Rohr angelegt');
const r110 = (await rohre()).at(-1);
pruef(r110.werte.durchmesser === 110 && r110.werte.familientyp === 'd 110', 'Kern: durchmesser 110, Typ «d 110»', JSON.stringify(r110.werte));
pruef(r110.werte.dn === 100, 'dn 100 ist die Angabe des Herstellers (aus dem Typ), nicht aus d gerechnet', JSON.stringify(r110.werte));
const lesen = await kern('abfrage', 'element_lesen', { element: r110.kennzeichen });
pruef(lesen.abgeleitet?.rohrgroesse === '110', 'Beschriftung des Kerns: Grösse 110 (Aussendurchmesser)', JSON.stringify(lesen.abgeleitet));
pruef(!(await h.meldungen()).includes('P30'), 'Schmutzwasser: kein Befund P30', await h.meldungen());
await shot('03_rohr_d110_dunkel');

// d 32 ohne DN ist zeichenbar
abschnitt('Rohr d 32 ohne DN: zeichenbar');
await feldSetzen('groesse', 'd 32');
await klickM([14000, 9000]);
await klickM([16000, 9000]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau d 32');
vz = await vorschauText();
pruef(/Vorschau/.test(vz) && !/Abgelehnt/.test(vz), 'd 32: der Kern nimmt die Grösse ohne DN an', vz);
await taste('Enter');
await warteBis(async () => (await rohre()).length === rohreVorher + 2, 'd 32 angelegt');
const r32 = (await rohre()).at(-1);
pruef(r32.werte.durchmesser === 32 && r32.werte.dn === undefined, 'Kern: durchmesser 32, keine DN', JSON.stringify(r32.werte));

// Bogen 45° und Abzweig
abschnitt('Bogen 45° (Winkel der Serie) und Abzweig: der Kern sagt, welche Winkel gelten');
await feldSetzen('groesse', 'd 110');
await klickM([18000, 8000]);
await klickM([20000, 8000]);
const dir = 45 * Math.PI / 180;
const p45 = [20000 + 1400 * Math.cos(dir), 8000 + 1400 * Math.sin(dir)];
await bewege(p45);
pruef(/45\.0°/.test(await h.werkTexte()), 'Winkelfang rastet auf 45° ein (Winkel der Serie)', await h.werkTexte());
await klickM(p45);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau 45°');
vz = await vorschauText();
pruef(/2 Geraden/.test(vz) && /1 Bogen 45°/.test(vz), 'Vorschau: 2 Geraden und Bogen 45°', vz);
await taste('Enter');
await warteBis(async () => (await rohre()).length === rohreVorher + 4, 'Zug mit Bogen angelegt');
const bogen = (await derKlasse('Bogen')).find((b) => b.werte.winkel === 45 && b.werte.familientyp === 'd 110');
pruef(!!bogen, 'Kern: Bogen 45° der Abwasser-Serie, Typ «d 110»',JSON.stringify((await derKlasse('Bogen')).map((b) => b.werte)));
await shot('04_bogen45_dunkel');
await taste('Escape');
// Abzweig auf die erste Gerade des Zugs (rechtwinklig; die Serie erlaubt 45° und 88.5°)
await tippeTasten('pi');
await feldSetzen('system', 'SW'); await feldSetzen('strang', 'SW-G1');
await bewege([19000, 8010]);
await klickM([19000, 8010]);
pruef((await feldWert('groesse')) === 'd 110', 'Klick auf die Gerade: Serie und Grösse «d 110» der Leitung werden übernommen');
await feldSetzen('groesse', 'd 56');
await bewege([19000, 9300]);
await klickM([19000, 9300]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Abzweig');
vz = await vorschauText();
pruef(/Vorschau|Abgelehnt/.test(vz), 'Der Kern antwortet auf den rechtwinkligen Abzweig (Vorschau oder Ablehnung mit Vorschlag)', vz);
console.log('   Abzweig d 56:', (vz ?? '').replace(/\n/g, ' ').slice(0, 300));
await shot('05_abzweig_dunkel');
await taste('Escape'); await taste('Escape');

// ================================================================== Befund P30
abschnitt('Heizungssystem: Befund P30 «Abwasser-Serie für Heizung Vorlauf» in der Vorschau und nach dem Ausführen');
await tippeTasten('pi');
await feldSetzen('groesse', 'd 75');
await feldSetzen('system', 'HZ-V'); await feldSetzen('strang', 'HZ-V1');
await feldSetzen('gefaelle', '');
await klickM([14000, 6500]);
await klickM([16000, 6500]);
await taste('Enter');
await warteBis(vorschauSichtbar, 'Vorschau Heizung');
vz = await vorschauText();
pruef(/Vorschau/.test(vz) && /Prüfbefund bei Ausführung: P30 RO-\d+: Abwasser-Serie .*für Heizung Vorlauf/.test(vz), 'Die Vorschau nennt den Befund P30 des Kerns', vz);
await shot('06_vorschau_p30_dunkel');
await taste('Enter');
await warteBis(async () => (await rohre()).some((e) => e.system === 'HZ-V'), 'Rohr im Heizungssystem');
await warteBis(async () => /P30/.test(await h.meldungen()), 'Meldung mit Befund', 8000);
pruef(/P30 RO-\d+: Abwasser-Serie/.test(await h.meldungen()), 'Meldung nach dem Ausführen: P30 (Warnung)', await h.meldungen());
const rHz = (await rohre()).find((e) => e.system === 'HZ-V');
const bef = (await kern('abfrage', 'pruefen', {})).befunde.filter((b) => b.code === 'P30');
pruef(bef.length === 1 && bef[0].element === rHz.kennzeichen && bef[0].schwere === 'fehler', 'Kern: genau ein Befund P30 (fehler) am Heizungsrohr', JSON.stringify(bef.map((b) => [b.element, b.schwere])));
await taste('Escape'); await taste('Escape');

// ================================================================== Palette
abschnitt('Eigenschaftenpalette: «d 110», Aussendurchmesser, DN (Hersteller), Beschriftung «PE 110», Einsatz, Befund P30');
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${r110.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] [data-ziel="lt:groesse"]'), 'Gruppe Leitung');
await warteBis(async () => /Beschriftung/.test(await dom('.ep-gruppe[data-g="Leitung"]') ?? ''), 'Beschriftung in der Palette');
let pal = await ausw(`const g = document.querySelector('.ep-gruppe[data-g="Leitung"]'); return { serie: g.querySelector('[data-ziel="lt:serie"]').value, groesse: g.querySelector('[data-ziel="lt:groesse"]').value, text: g.innerText, opt: [...g.querySelectorAll('[data-ziel="lt:groesse"] option')].map((o) => o.textContent) };`);
pruef(pal.serie === KZ && pal.groesse === 'd 110', 'Palette: Serie und Grösse «d 110»', JSON.stringify([pal.serie, pal.groesse]));
pruef(pal.opt.join('|') === '–|d 32|d 56|d 75|d 110' || pal.opt.join('|') === 'd 32|d 56|d 75|d 110', 'Auswahl der Grösse: «d 32» … «d 110», keine DN', pal.opt.join('|'));
pruef(/Einsatz\s+Abwasser/.test(pal.text), 'Zeile «Einsatz: Abwasser»', pal.text);
pruef(/Aussendurchmesser\s+110 mm/.test(pal.text), 'Zeile «Aussendurchmesser 110 mm»', pal.text);
pruef(/DN \(Hersteller\)\s+DN 100/.test(pal.text), 'Zeile «DN (Hersteller): DN 100» (Angabe der Unterlage)', pal.text);
pruef(/Beschriftung\s+PE 110/.test(pal.text), 'Beschriftung wie im Kern: «PE 110»', pal.text);
pruef(!/Befund P30/.test(pal.text), 'Schmutzwasser: kein Befund');
pruef(!(await gibt('.ep-gruppe[data-g="Eigenschaften"] [data-ziel="eig:durchmesser"]')) && !(await gibt('.ep-gruppe[data-g="Eigenschaften"] [data-ziel="eig:dn"]')), 'durchmesser und dn stehen nicht noch einmal unter «Eigenschaften»');
await shot('07_palette_d110_dunkel');
// Grösse wechseln (Rückfrage), «d 75» hat keine DN
await paletteSetzen('lt:groesse', 'd 75');
await dlgFrage('Nur diese Leitung');
await warteBis(async () => (await zustandDaten(`z.element('${r110.kennzeichen}').werte.durchmesser`)) === 75, 'Grösse d 75 im Kern');
pruef((await zustandDaten(`z.element('${r110.kennzeichen}').werte.familientyp`)) === 'd 75', 'Kern: Typ «d 75»');
await warteBis(async () => /Beschriftung\s+PE 75/.test(await dom('.ep-gruppe[data-g="Leitung"]') ?? ''), 'Palette folgt', 8000);
pal = await ausw(`const g = document.querySelector('.ep-gruppe[data-g="Leitung"]'); return { groesse: g.querySelector('[data-ziel="lt:groesse"]').value, text: g.innerText };`);
pruef(pal.groesse === 'd 75' && /Aussendurchmesser\s+75 mm/.test(pal.text) && !/DN \(Hersteller\)/.test(pal.text), 'Palette: d 75, Aussendurchmesser 75, keine DN des Herstellers', pal.text);
// zurück
await warte(1000);
await paletteSetzen('lt:groesse', 'd 110');
await dlgFrage('Nur diese Leitung');
await warteBis(async () => (await zustandDaten(`z.element('${r110.kennzeichen}').werte.durchmesser`)) === 110, 'zurück auf d 110');
// Befund P30 beim Heizungsrohr
await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen('${rHz.id}'); return 1;`);
await warteBis(() => gibt('.ep-gruppe[data-g="Leitung"] .ep-befund'), 'Befundzeile P30', 15000);
const bz = await dom('.ep-gruppe[data-g="Leitung"] .ep-befundzeile');
pruef(/Befund P30/.test(bz ?? '') && /Abwasser-Serie/.test(bz ?? '') && /Heizung Vorlauf/.test(bz ?? ''), 'Palette zeigt «Befund P30» mit dem Text des Kerns', bz);
pruef(/nur für die Entwässerung/.test(bz ?? ''), 'mit dem Wort «nur für die Entwässerung»', bz);
await shot('08_palette_befund_p30_dunkel');

// ================================================================== Familieneditor
abschnitt('Familieneditor: Einsatz anzeigen und setzen, Grössenart, Kontrolltabelle mit Beschriftung und DN des Herstellers');
const feld = (pfad) => `.fe-seite .fe-feld[data-pfad="${pfad}"]`;
const eingabe = (pfad) => `${feld(pfad)} input`;
const auswahl = (pfad) => `${feld(pfad)} select`;
const FAM = (f) => kern('abfrage', 'familie', { familie: f });
const oeffneEditor = async (kz) => { await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`); await warteBis(() => gibt('.fe-wurzel'), 'Editor offen'); await warteBis(async () => !(await dom('.fe-seite'))?.includes('wird geladen') && (await gibt('.fe-feld')), 'Seite gezeichnet'); };
const setzeFeld = async (sel, w, ms = 900) => { await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('Feld fehlt: ${sel}'); e.focus(); if (e.tagName === 'SELECT') Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(w)}); else e.value = ${JSON.stringify(w)}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return 1;`); await warte(ms); };
const bar = () => ausw(`const m = document.querySelector('.fe-meldung'); return m && !m.hidden ? { klasse: m.className, text: m.innerText } : null;`);
await oeffneEditor(KZ);
pruef((await ausw(`return document.querySelector('.fe-tab-knopf.aktiv')?.dataset.tab;`)) === 'leitung', 'Serie öffnet auf der Seite «Leitungsserie»');
pruef((await wert(auswahl('einsatz'))) === 'abwasser', 'Einsatz: Abwasser');
const eOpt = await ausw(`return [...document.querySelectorAll('${auswahl('einsatz')} option')].map((o) => o.value + '=' + o.textContent);`);
pruef(eOpt.join('|') === '=(nicht angegeben)|abwasser=Abwasser (Schmutz-, Regenwasser)', 'Auswahl Einsatz', eOpt.join('|'));
pruef((await wert(auswahl('groessenart'))) === 'd', 'Grösse geführt nach: Aussendurchmesser d');
pruef((await wert(auswahl('groesse.d'))) === 'd' && !(await gibt(auswahl('groesse.dn'))), 'Grössenparameter d (kein dn)');
const lab = await ausw(`return [...document.querySelectorAll('.fe-fz-l')].map((l) => l.textContent);`);
pruef(lab.includes('Einsatz') && lab.includes('Grösse geführt nach') && lab.includes('Grösse Aussendurchmesser d'), 'Beschriftungen: Einsatz, Grösse geführt nach, Grösse Aussendurchmesser d', lab.join('|'));
pruef((await wert(eingabe('kuerzel'))) === 'PE', 'Kürzel PE');
await shot('09_editor_einsatz_dunkel');
// Kontrolltabelle (kleine Serie: kommt wie bisher von selbst)
await warteBis(() => gibt('.fe-seite table.fe-kontrolle tbody tr'), 'Kontrolltabelle', 30000);
const kopf = await dom('.fe-seite .fe-kontrolle-kopf');
pruef(/Einsatz Abwasser \(Grösse nach Aussendurchmesser\)/.test(kopf ?? ''), 'Kopfzeile: Einsatz Abwasser, Grösse nach Aussendurchmesser', kopf);
const zeile110 = (await dom('.fe-seite table.fe-kontrolle tr[data-typ="d 110"]')).replace(/\s+/g, ' ');
pruef(/^d 110 d 110 110 102 4 PE 110 100 /.test(zeile110), 'Zeile d 110: Grösse «d 110», aussen 110, Beschriftung «PE 110», DN Hersteller 100', zeile110);
const zeile32 = (await dom('.fe-seite table.fe-kontrolle tr[data-typ="d 32"]')).replace(/\s+/g, ' ');
pruef(/^d 32 d 32 32 26 3 PE 32 – /.test(zeile32), 'Zeile d 32: Beschriftung «PE 32», keine DN des Herstellers (–)', zeile32);
pruef(!(await dom('.fe-seite') ?? '').includes('Probe mit einer freien Grösse') && !(await gibt('[data-aktion="kontrolle-laden"]')), 'keine Probe und kein Knopf «Kontrolle berechnen» (Abwasser-Serie ohne freie Grösse)');
await shot('10_editor_kontrolle_abwasser_dunkel');
// Einsatz entfernen: Grösse d verlangt den Einsatz – der Kern lehnt ab, nichts wird gespeichert
const v0 = (await FAM(KZ)).version;
await setzeFeld(auswahl('einsatz'), '', 500);
await warteBis(async () => (await bar())?.klasse.includes('fehler'), 'Fehlermeldung', 8000);
const m = await bar();
pruef(/einsatz|Abwasser|Aussendurchmesser/i.test(m.text), 'Einsatz entfernen: Meldung des Kerns', m.text);
pruef((await FAM(KZ)).version === v0 && (await FAM(KZ)).definition.leitung.einsatz === 'abwasser', 'nichts gespeichert, Einsatz bleibt');
await shot('11_editor_einsatz_abgelehnt_dunkel');
// Kürzel ändern: gespeichert; Kontrolltabelle folgt
await ausw(`document.querySelector('${auswahl('einsatz')}').value = 'abwasser'; return 1;`);
await setzeFeld(eingabe('kuerzel'), 'PE-HD', 900);
await warteBis(async () => (await FAM(KZ)).definition.leitung.kuerzel === 'PE-HD', 'Kürzel gespeichert');
await warteBis(async () => /PE-HD 110/.test(await dom('.fe-seite table.fe-kontrolle tr[data-typ="d 110"]') ?? ''), 'Beschriftung in der Kontrolle', 15000);
pruef(true, 'Kürzel PE-HD: die Beschriftung des Kerns folgt («PE-HD 110»)');
await setzeFeld(eingabe('kuerzel'), 'PE', 900);
await warteBis(async () => (await FAM(KZ)).definition.leitung.kuerzel === 'PE', 'Kürzel zurück');
} catch (e) {
  h.schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { await shot('99_fehler'); } catch { /* egal */ }
}
await h.ende();

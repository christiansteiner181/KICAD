// Tests der Texte, Sortierung, Suche und HTML des Startbildschirms (S1, entwurf/startbildschirm.md) und der Entscheidungen für das
// 3D-Vorschaubild ohne Browser: Zeittexte, Anfangsbuchstaben, Kacheln (angeheftet, offen, nicht erreichbar, ohne Bild), Liste «Alle
// KICAD-Projekte», Familien, Ordner-Browser (Erkennung von KICAD-Projekten, Öffnen-Ziel) und wann ein Vorschaubild fällig ist.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/start.test.mjs
// Die Zusammenführung der Listen mehrerer Rechner steht im Server (ZuletztListe.Vereinigen) und wird in C# getestet
// (tests/Hlks.Web.Tests/ZuletztListeTests.cs) – die Oberfläche bekommt die fertige Liste.

import { test } from 'node:test';
import assert from 'node:assert/strict';

const T = await import('./start-text.js');
const V = await import('./vorschau-logik.js');

const lokal = (j, m, t, h = 12, min = 0) => new Date(j, m - 1, t, h, min).toISOString();
const JETZT = new Date(2026, 9, 19, 15, 30);   // 19.10.2026 15:30 (Ortszeit)

// ---------------------------------------------------------------------------------------------- Zeit und Namen

test('zeitText: heute, gestern, älter, ungültig', () => {
  assert.equal(T.zeitText(lokal(2026, 10, 19, 14, 32), JETZT), 'Heute 14:32');
  assert.equal(T.zeitText(lokal(2026, 10, 18, 9, 5), JETZT), 'Gestern 09:05');
  assert.equal(T.zeitText(lokal(2026, 10, 12, 9, 5), JETZT), '12.10.2026');
  assert.equal(T.zeitText(lokal(2025, 1, 3), JETZT), '03.01.2025');
  assert.equal(T.zeitText(null, JETZT), '–');
  assert.equal(T.zeitText('keine Zeit', JETZT), '–');
});

test('zeitText: kurz nach Mitternacht ist «Heute», der Vortag «Gestern»', () => {
  const j = new Date(2026, 9, 19, 0, 10);
  assert.equal(T.zeitText(lokal(2026, 10, 19, 0, 5), j), 'Heute 00:05');
  assert.equal(T.zeitText(lokal(2026, 10, 18, 23, 59), j), 'Gestern 23:59');
});

test('vorText: gerade eben, Minuten, Stunden, Tage', () => {
  const t = (s) => new Date(JETZT.getTime() - s * 1000).toISOString();
  assert.equal(T.vorText(t(10), JETZT), 'gerade eben');
  assert.equal(T.vorText(t(180), JETZT), 'vor 3 min');
  assert.equal(T.vorText(t(3 * 3600), JETZT), 'vor 3 Std.');
  assert.equal(T.vorText(t(86400), JETZT), 'vor 1 Tag');
  assert.equal(T.vorText(t(4 * 86400), JETZT), 'vor 4 Tagen');
  assert.equal(T.vorText('', JETZT), '');
});

test('initialen: Platzhalter ohne Bild', () => {
  assert.equal(T.initialen('MFH Musterweg'), 'MM');
  assert.equal(T.initialen('Bernstrasse'), 'BE');
  assert.equal(T.initialen('Wohnhaus Seestrasse'), 'WS');
  assert.equal(T.initialen('worb_BES_bernstrasse'), 'WB');
  assert.equal(T.initialen('Ärztehaus Öst'), 'ÄÖ');
  assert.equal(T.initialen('  '), '?');
  assert.equal(T.initialen(null), '?');
  assert.equal(T.initialen('(1)'), '1');
});

test('farbton: gleicher Name gleiche Farbe, immer 0–359', () => {
  assert.equal(T.farbton('Gewerbebau Nord'), T.farbton('Gewerbebau Nord'));
  assert.notEqual(T.farbton('Gewerbebau Nord'), T.farbton('Schulhaus Ostring'));
  for (const n of ['', 'a', 'Ärztehaus', 'x'.repeat(500)]) { const h = T.farbton(n); assert.ok(h >= 0 && h < 360, n); }
});

test('Ordner vergleichen: Trenner, Schlusstrenner, Gross-/Kleinschreibung', () => {
  assert.equal(T.normOrdner('W:/Objekt/e_KICAD/'), 'w:\\objekt\\e_kicad');
  assert.ok(T.gleicherOrdner('W:\\Objekt\\e_KICAD', 'w:/objekt/e_kicad\\'));
  assert.ok(!T.gleicherOrdner('W:\\A', 'W:\\B'));
  assert.ok(!T.gleicherOrdner('', ''));
  assert.ok(!T.gleicherOrdner(null, 'W:\\A'));
});

test('objektName: drittletzter Teil bei …\\<Objekt>\\c_Pläne und Modelle\\e_KICAD, sonst letzter', () => {
  assert.equal(T.objektName('W:\\02_Projektdateien Objekte\\Worb_BES_Bernstrasse\\c_Pläne und Modelle\\e_KICAD'), 'Worb_BES_Bernstrasse');
  assert.equal(T.objektName('C:\\Test\\Ordner'), 'Ordner');
  assert.equal(T.objektName(''), '');
  assert.deepEqual(T.pfadTeile('W:\\a/b\\'), ['W:', 'a', 'b']);
});

test('vorschauUrl: Ordner kodiert, Version optional', () => {
  assert.equal(T.vorschauUrl('W:\\Objekt ä\\e_KICAD', 12345), '/api/projekt/vorschau?ordner=W%3A%5CObjekt%20%C3%A4%5Ce_KICAD&v=12345');
  assert.equal(T.vorschauUrl('C:\\x'), '/api/projekt/vorschau?ordner=C%3A%5Cx');
});

// ---------------------------------------------------------------------------------------------- Sortieren und Suchen

const liste = () => [
  { ordner: 'C:\\a', name: 'Alt', zeitIso: lokal(2026, 10, 1), angeheftet: false },
  { ordner: 'C:\\b', name: 'Neu', zeitIso: lokal(2026, 10, 18), angeheftet: false },
  { ordner: 'C:\\c', name: 'Angeheftet alt', zeitIso: lokal(2026, 9, 1), angeheftet: true },
  { ordner: 'C:\\d', name: 'Angeheftet neu', zeitIso: lokal(2026, 10, 10), angeheftet: true },
  { ordner: 'C:\\e', name: 'Ohne Zeit', angeheftet: false },
];

test('kachelnSortieren: angeheftete zuerst, dann nach Zeit, ohne die Eingabe zu ändern', () => {
  const l = liste();
  const s = T.kachelnSortieren(l);
  assert.deepEqual(s.map((k) => k.name), ['Angeheftet neu', 'Angeheftet alt', 'Neu', 'Alt', 'Ohne Zeit']);
  assert.deepEqual(l.map((k) => k.name), ['Alt', 'Neu', 'Angeheftet alt', 'Angeheftet neu', 'Ohne Zeit']);
  assert.deepEqual(T.kachelnSortieren(null), []);
});

test('kachelnSortieren: gleiche Zeit behält die Reihenfolge', () => {
  const z = lokal(2026, 10, 5);
  const s = T.kachelnSortieren([{ name: 'a', zeitIso: z }, { name: 'b', zeitIso: z }, { name: 'c', zeitIso: z }]);
  assert.deepEqual(s.map((k) => k.name), ['a', 'b', 'c']);
});

test('filtern: alle Wörter, ohne Gross-/Kleinschreibung, über mehrere Felder', () => {
  const p = [
    { name: 'Wohnhaus Seestrasse', objekt: 'Bern_SEE_Seestrasse 12', ordner: 'W:\\x', nummer: '2026-014' },
    { name: 'Schulhaus Ostring', objekt: 'Thun_OST_Ostring 4', ordner: 'W:\\y', nummer: null },
  ];
  assert.equal(T.filtern(p, 'see', T.FELDER_PROJEKT).length, 1);
  assert.equal(T.filtern(p, 'WOHNHAUS bern', T.FELDER_PROJEKT).length, 1);
  assert.equal(T.filtern(p, 'wohnhaus thun', T.FELDER_PROJEKT).length, 0);
  assert.equal(T.filtern(p, '2026-014', T.FELDER_PROJEKT)[0].name, 'Wohnhaus Seestrasse');
  assert.equal(T.filtern(p, '', T.FELDER_PROJEKT).length, 2);
  assert.equal(T.filtern(p, '   ', T.FELDER_PROJEKT).length, 2);
  assert.equal(T.filtern(p, 'null', T.FELDER_PROJEKT).length, 0, 'fehlende Felder zählen nicht als Text «null»');
  assert.equal(T.filtern(p, 'ostring', [(x) => x.objekt.toUpperCase()]).length, 1);
  assert.deepEqual(T.filtern(null, 'a', ['name']), []);
});

test('filtern: Familien nach Kategorie, Hersteller und Projekt', () => {
  const f = [
    { name: 'Heizkörper 22', kategorie: 'Heizkörper', hersteller: 'Fiktiv AG', projekt: 'Bernstrasse', projektNummer: '2026-007', status: 'freigegeben' },
    { name: 'Spültisch', kategorie: 'Sanitär', hersteller: 'Muster GmbH', projekt: 'Ostring', status: 'entwurf' },
  ];
  assert.equal(T.filtern(f, 'fiktiv', T.FELDER_FAMILIE).length, 1);
  assert.equal(T.filtern(f, 'ostring entwurf', T.FELDER_FAMILIE).length, 1);
  assert.equal(T.filtern(f, '2026-007', T.FELDER_FAMILIE)[0].name, 'Heizkörper 22');
});

// ---------------------------------------------------------------------------------------------- Kacheln

const ORDNER = 'W:\\02_Projektdateien Objekte\\Worb_BES_Bernstrasse\\c_Pläne und Modelle\\e_KICAD';
const kachel = (o = {}) => ({ ordner: ORDNER, name: 'Bernstrasse', zeitIso: lokal(2026, 10, 19, 14, 32), rechner: 'HK-PC12', angeheftet: false, vorschau: true, vorschauV: 777, vorhanden: true, ...o });

test('kachelHtml: Bild, Name, Objektordner, Zeit und Rechner', () => {
  const h = T.kachelHtml(kachel(), 3, { jetzt: JETZT });
  assert.match(h, /data-i="3"/);
  assert.match(h, /<img src="\/api\/projekt\/vorschau\?ordner=W%3A[^"]*&amp;v=777"/);
  assert.match(h, /<b class="sb-name">Bernstrasse<\/b>/);
  assert.match(h, /<span class="sb-objekt">Worb_BES_Bernstrasse<\/span>/);
  assert.match(h, /Heute 14:32 · HK-PC12/);
  assert.doesNotMatch(h, /sb-platzhalter/);
  assert.doesNotMatch(h, /angeheftet|sb-pin/);
  assert.match(h, /title="W:\\02_Projektdateien Objekte/);
});

test('kachelHtml: ohne Bild ein Platzhalter mit Anfangsbuchstaben und fester Farbe', () => {
  const h = T.kachelHtml(kachel({ name: 'Gewerbebau Nord', vorschau: false }), 0, { jetzt: JETZT });
  assert.match(h, /<span class="sb-platzhalter" style="--h:\d+">GN<\/span>/);
  assert.doesNotMatch(h, /<img/);
});

test('kachelHtml: angeheftet zeigt die Marke, offen die Marke «Offen», nicht erreichbar den Hinweis', () => {
  const pin = T.kachelHtml(kachel({ angeheftet: true }), 0, { jetzt: JETZT });
  assert.match(pin, /class="sb-kachel angeheftet"/);
  assert.match(pin, /class="sb-pin" title="Angeheftet"/);
  const offen = T.kachelHtml(kachel(), 0, { offen: 'w:/02_projektdateien objekte/worb_bes_bernstrasse/c_pläne und modelle/e_kicad/', jetzt: JETZT });
  assert.match(offen, /class="sb-kachel offen"/);
  assert.match(offen, /<span class="sb-marke-offen">Offen<\/span>/);
  const fehlt = T.kachelHtml(kachel({ vorhanden: false }), 0, { jetzt: JETZT });
  assert.match(fehlt, /class="sb-kachel fehlt"/);
  assert.match(fehlt, /nicht erreichbar/);
  assert.doesNotMatch(fehlt, /<img/, 'ein nicht erreichbares Projekt zeigt kein Bild vom NAS');
  const unbekannt = T.kachelHtml(kachel({ vorhanden: null }), 0, { jetzt: JETZT });
  assert.doesNotMatch(unbekannt, /sb-marke-fehlt/, 'unbekannt (Zeitlimit) ist nicht «fehlt»');
});

test('kachelHtml: Namen werden maskiert (kein Einschleusen von HTML)', () => {
  const h = T.kachelHtml(kachel({ name: '<img src=x onerror=alert(1)>', ordner: 'C:\\a"b\\x\\y\\e_KICAD', rechner: '<b>' }), 0, { jetzt: JETZT });
  assert.doesNotMatch(h, /<img src=x/);
  assert.match(h, /&lt;img src=x onerror=alert\(1\)&gt;/);
  assert.doesNotMatch(h, /title="C:\\a"b/);
  assert.match(h, /&lt;b&gt;/);
});

test('kachelnHtml: leere Liste zeigt den Text, sonst eine Kachel je Eintrag', () => {
  assert.match(T.kachelnHtml([], {}, 'Nichts da.'), /<p class="sb-leer">Nichts da\.<\/p>/);
  assert.match(T.kachelnHtml(null), /Noch keine Projekte geöffnet/);
  const h = T.kachelnHtml([kachel(), kachel({ name: 'B' })], { jetzt: JETZT });
  assert.equal((h.match(/<article /g) ?? []).length, 2);
  assert.match(h, /data-i="1"/);
});

test('kachelMenue: Öffnen, Anheften/Lösen, Entfernen, Explorer', () => {
  const m = T.kachelMenue(kachel());
  assert.deepEqual(m.filter((e) => e.aktion).map((e) => e.aktion), ['oeffnen', 'anheften', 'entfernen', 'explorer']);
  assert.equal(m.find((e) => e.aktion === 'anheften').titel, 'Anheften');
  assert.equal(T.kachelMenue(kachel({ angeheftet: true })).find((e) => e.aktion === 'anheften').titel, 'Lösen');
  assert.equal(T.kachelMenue(kachel(), { offen: ORDNER }).find((e) => e.aktion === 'oeffnen').titel, 'Zum Projekt');
  assert.ok(m.some((e) => e.trenner));
});

// ---------------------------------------------------------------------------------------------- Liste «Alle KICAD-Projekte»

const proj = (o = {}) => ({ name: 'Bernstrasse', objekt: 'Worb_BES_Bernstrasse', ordner: ORDNER, nummer: '2026-007', geaendert: lokal(2026, 10, 18, 8, 15), ...o });

test('ablageStatusText: Anzahl, Alter, Dauer; nicht erreichbar zeigt den Hinweis des Servers', () => {
  const t = new Date(JETZT.getTime() - 5 * 60 * 1000).toISOString();
  assert.equal(T.ablageStatusText({ erreichbar: true, projekte: [proj(), proj()], gesucht: t, dauerMs: 1234 }, JETZT), '2 Projekte · gesucht vor 5 min (1,2 s)');
  assert.equal(T.ablageStatusText({ erreichbar: true, projekte: [proj()], gesucht: t }, JETZT), '1 Projekt · gesucht vor 5 min');
  assert.equal(T.ablageStatusText({ erreichbar: true, projekte: [], gesucht: t, dauerMs: 10, hinweis: '2 Ordner waren nicht lesbar' }, JETZT), '0 Projekte · gesucht vor 5 min (0,0 s) · 2 Ordner waren nicht lesbar');
  assert.equal(T.ablageStatusText({ erreichbar: false, hinweis: 'W: ist nicht erreichbar.' }, JETZT), 'W: ist nicht erreichbar.');
  assert.match(T.ablageStatusText({ erreichbar: false }, JETZT), /nicht erreichbar/);
  assert.equal(T.ablageStatusText(null, JETZT), 'Suche läuft …');
});

test('ablageTitel: Laufwerk aus dem Pfad', () => {
  assert.equal(T.ablageTitel({ pfad: 'W:\\02_Projektdateien Objekte' }), 'Alle KICAD-Projekte auf W:');
  assert.equal(T.ablageTitel({ pfad: '\\\\NAS\\Projekte' }), 'Alle KICAD-Projekte');
  assert.equal(T.ablageTitel(null), 'Alle KICAD-Projekte');
});

test('ablageHtml: Spalten Name, Objekt, Projektnummer, zuletzt geändert; offenes Projekt markiert', () => {
  const h = T.ablageHtml([proj(), proj({ name: 'Zweites <b>', nummer: null, ordner: 'C:\\z\\y\\e_KICAD' })], { offen: ORDNER, jetzt: JETZT });
  assert.match(h, /<th>Name<\/th><th>Objekt<\/th><th>Projektnummer<\/th><th>Zuletzt geändert<\/th>/);
  assert.match(h, /2026-007/);
  assert.match(h, /18\.10\.2026|Gestern 08:15/);
  assert.equal((h.match(/class="sb-zeile offen"/g) ?? []).length, 1);
  assert.match(h, /Zweites &lt;b&gt;/);
  assert.match(h, /<td>–<\/td>/, 'ohne Projektnummer ein Strich');
  assert.match(T.ablageHtml([], { leerText: 'Keine.' }), /Keine\./);
});

// ---------------------------------------------------------------------------------------------- Familien

const fam = (o = {}) => ({ id: 'fam-1', name: 'Heizkörper 22', kategorie: 'Heizkörper', hersteller: 'Fiktiv AG', status: 'freigegeben', version: 3, projekt: 'Bernstrasse', projektNummer: '2026-007', projektOrdner: ORDNER, zeitIso: lokal(2026, 10, 19, 11, 0), ...o });

test('statusText: bekannte Status deutsch, sonst roh, leer = Entwurf', () => {
  assert.equal(T.statusText('entwurf'), 'Entwurf');
  assert.equal(T.statusText('freigegeben'), 'Freigegeben');
  assert.equal(T.statusText('geprueft'), 'geprueft');
  assert.equal(T.statusText(null), 'Entwurf');
});

test('familienHtml: Name, Kategorie, Hersteller, Status, Version, Projekt und Zeit', () => {
  const h = T.familienHtml([fam(), fam({ name: 'Ohne Projekt', projekt: null, projektOrdner: null, status: 'entwurf', hersteller: null })], { jetzt: JETZT });
  assert.match(h, /<b class="sb-name">Heizkörper 22<\/b>/);
  assert.match(h, /st-freigegeben">Freigegeben</);
  assert.match(h, /v3/);
  assert.match(h, /Heizkörper · Fiktiv AG/);
  assert.match(h, /Bernstrasse \(2026-007\) · Heute 11:00/);
  assert.match(h, /Projekt unbekannt/);
  assert.equal((h.match(/class="sb-familie"/g) ?? []).length, 2);
  assert.match(T.familienHtml([], { leerText: 'Leer.' }), /Leer\./);
});

test('bibliothekTabelleHtml: Tabelle der ganzen Bibliothek, maskiert', () => {
  const h = T.bibliothekTabelleHtml([fam({ name: '<x>' })], JETZT);
  assert.match(h, /<table class="tab">/);
  assert.match(h, /&lt;x&gt;/);
  assert.match(T.bibliothekTabelleHtml([]), /Keine Familien/);
});

// ---------------------------------------------------------------------------------------------- Ordner-Browser

const WURZEL = { pfad: '', eltern: null, ordner: [], laufwerke: [{ name: 'C:\\', pfad: 'C:\\', art: 'Lokaler Datenträger', bezeichnung: 'System' }, { name: 'W:\\', pfad: 'W:\\', art: 'Netzlaufwerk' }], ablage: 'W:\\02_Projektdateien Objekte', kicadOrdner: 'c_Pläne und Modelle\\e_KICAD' };
const LISTING = {
  pfad: 'W:\\02_Projektdateien Objekte', eltern: 'W:\\', istProjekt: false, kicadOrdner: 'c_Pläne und Modelle\\e_KICAD', ablage: 'W:\\02_Projektdateien Objekte',
  ordner: [
    { name: 'Bern_ARK', pfad: 'W:\\02_Projektdateien Objekte\\Bern_ARK', projekt: false, enthaeltProjekt: false },
    { name: 'Worb_BES', pfad: 'W:\\02_Projektdateien Objekte\\Worb_BES', projekt: false, enthaeltProjekt: true },
    { name: 'e_KICAD', pfad: 'W:\\02_Projektdateien Objekte\\e_KICAD', projekt: true, enthaeltProjekt: false },
  ],
};

test('schnellzugriff: Projektablage, Laufwerke, zuletzt benutzte Ordner', () => {
  const s = T.schnellzugriff(WURZEL.laufwerke, WURZEL.ablage, ['W:\\a\\b', 'C:\\x', ORDNER]);
  assert.deepEqual(s.map((e) => e.gruppe), ['Projektablage', 'Laufwerke', 'Laufwerke', 'Zuletzt benutzt', 'Zuletzt benutzt', 'Zuletzt benutzt']);
  assert.equal(s[0].titel, '02_Projektdateien Objekte');
  assert.equal(s[1].titel, 'C:\\ System');
  assert.equal(s[2].titel, 'W:\\ (Netz)');
  assert.equal(s[3].titel, 'a\\b');
  assert.equal(s[4].titel, 'C:\\x');
  assert.equal(s[5].titel, 'Worb_BES_Bernstrasse', 'ein Projektordner heisst nach seinem Objekt');
  assert.deepEqual(T.schnellzugriff([], '', []), []);
});

test('ordnerMerken: neuester zuerst, ohne Doppelte, höchstens max', () => {
  assert.deepEqual(T.ordnerMerken(['C:\\a', 'C:\\b'], 'C:\\c'), ['C:\\c', 'C:\\a', 'C:\\b']);
  assert.deepEqual(T.ordnerMerken(['C:\\a', 'C:\\b'], 'c:/B/'), ['c:/B/', 'C:\\a']);
  assert.equal(T.ordnerMerken(['1', '2', '3'], '4', 3).length, 3);
  assert.deepEqual(T.ordnerMerken(['x'], ''), ['x']);
  assert.deepEqual(T.ordnerMerken(null, 'y'), ['y']);
});

test('schnellzugriffHtml: Gruppen, aktueller Ordner hervorgehoben, maskiert', () => {
  const h = T.schnellzugriffHtml(T.schnellzugriff(WURZEL.laufwerke, WURZEL.ablage, ['C:\\"x<']), 'c:\\');
  assert.equal((h.match(/class="ob-gruppe"/g) ?? []).length, 3);
  assert.match(h, /class="ob-ziel aktiv" data-pfad="C:\\"/);
  assert.match(h, /C:\\&quot;x&lt;/);
  assert.match(T.schnellzugriffHtml([]), /Keine Orte/);
});

test('ordnerListeHtml: Laufwerke, KICAD-Projekte hervorgehoben, «enthält Projekt», Auswahl', () => {
  const l = T.ordnerListeHtml(WURZEL);
  assert.equal((l.match(/class="ob-ordner"/g) ?? []).length, 2);
  assert.match(l, /Netzlaufwerk/);

  const h = T.ordnerListeHtml(LISTING, 'W:\\02_Projektdateien Objekte\\Worb_BES');
  assert.match(h, /ob-ordner projekt[^>]*data-projekt="1"/);
  assert.match(h, /KICAD-Projekt<\/span>/);
  assert.match(h, /ob-ordner enthaelt gewaehlt[^>]*data-enthaelt="1"/);
  assert.match(h, /enthält Projekt/);
  assert.equal((h.match(/class="ob-ordner"/g) ?? []).length, 1);
  assert.doesNotMatch(h, /ob-hinweis/);

  const p = T.ordnerListeHtml({ ...LISTING, istProjekt: true, ordner: [] });
  assert.match(p, /Dieser Ordner ist ein KICAD-Projekt/);
  assert.match(p, /Keine Unterordner/);
  const u = T.ordnerListeHtml({ ...LISTING, projektUnter: 'W:\\X\\c_Pläne und Modelle\\e_KICAD', ordner: [] });
  assert.match(u, /enthält ein KICAD-Projekt: <span class="mono">W:\\X\\c_Pläne und Modelle\\e_KICAD/);
  assert.match(T.ordnerListeHtml({ ...LISTING, gekuerzt: true, hinweis: 'Zugriff <verweigert>' }), /gekürzt[\s\S]*Zugriff &lt;verweigert&gt;/);
  assert.match(T.ordnerListeHtml(null), /Wird geladen/);
});

test('oeffnenZiel: Projektordner, Objektordner mit Projekt, aktueller Ordner, eingetippter Pfad', () => {
  const gewaehlt = (pfad) => ({ eingabe: pfad, daten: LISTING, gewaehlt: pfad });
  assert.equal(T.oeffnenZiel(gewaehlt('W:\\02_Projektdateien Objekte\\e_KICAD')), 'W:\\02_Projektdateien Objekte\\e_KICAD');
  assert.equal(T.oeffnenZiel(gewaehlt('W:\\02_Projektdateien Objekte\\Worb_BES')), 'W:\\02_Projektdateien Objekte\\Worb_BES\\c_Pläne und Modelle\\e_KICAD');
  assert.equal(T.oeffnenZiel({ eingabe: 'W:\\02_Projektdateien Objekte\\Worb_BES\\', daten: LISTING, gewaehlt: 'W:\\02_Projektdateien Objekte\\Worb_BES' }), 'W:\\02_Projektdateien Objekte\\Worb_BES\\c_Pläne und Modelle\\e_KICAD');
  assert.equal(T.oeffnenZiel(gewaehlt('W:\\02_Projektdateien Objekte\\Bern_ARK')), 'W:\\02_Projektdateien Objekte\\Bern_ARK', 'kein Projekt: der Server prüft und meldet');
  // aktueller Ordner ist selbst ein Projekt bzw. enthält eines
  assert.equal(T.oeffnenZiel({ eingabe: 'W:\\P\\e_KICAD', daten: { ...LISTING, pfad: 'W:\\P\\e_KICAD', istProjekt: true }, gewaehlt: '' }), 'W:\\P\\e_KICAD');
  assert.equal(T.oeffnenZiel({ eingabe: 'W:\\O', daten: { ...LISTING, pfad: 'W:\\O', projektUnter: 'W:\\O\\c_Pläne und Modelle\\e_KICAD' }, gewaehlt: '' }), 'W:\\O\\c_Pläne und Modelle\\e_KICAD');
  // eingetippt weicht vom angezeigten Ordner ab: gilt der getippte Pfad
  assert.equal(T.oeffnenZiel({ eingabe: '  X:\\anders  ', daten: LISTING, gewaehlt: '' }), 'X:\\anders');
  assert.equal(T.oeffnenZiel({ eingabe: '', daten: null, gewaehlt: '' }), '');
});

// ---------------------------------------------------------------------------------------------- Vorschaubild: wann und wie

const MIN = V.MIN_ABSTAND_MS;

test('vorschauEntscheid: noch kein Bild → jetzt; gleicher Stand → nichts', () => {
  assert.deepEqual(V.vorschauEntscheid({ gemerkt: null, stand: 'abc', jetzt: 1000 }), { aktion: 'jetzt', wartenMs: 0 });
  assert.deepEqual(V.vorschauEntscheid({ gemerkt: { stand: 'abc', zeit: 0 }, stand: 'abc', jetzt: 10 * MIN }), { aktion: 'nichts', wartenMs: 0 });
  assert.deepEqual(V.vorschauEntscheid({ gemerkt: null, stand: null, jetzt: 1000 }), { aktion: 'nichts', wartenMs: 0 }, 'ohne Stand kein Bild');
});

test('vorschauEntscheid: geänderter Stand höchstens alle 10 Minuten', () => {
  const g = { stand: 'alt', zeit: 1_000_000 };
  const kurz = V.vorschauEntscheid({ gemerkt: g, stand: 'neu', jetzt: 1_000_000 + 3 * 60 * 1000 });
  assert.equal(kurz.aktion, 'warten');
  assert.equal(kurz.wartenMs, MIN - 3 * 60 * 1000);
  assert.equal(V.vorschauEntscheid({ gemerkt: g, stand: 'neu', jetzt: 1_000_000 + MIN }).aktion, 'jetzt');
  assert.equal(V.vorschauEntscheid({ gemerkt: g, stand: 'neu', jetzt: 1_000_000 + MIN - 1 }).aktion, 'warten');
});

test('vorschauEntscheid: nach einem Fehlversuch (Stand null) erst nach dem Mindestabstand erneut', () => {
  const g = { stand: null, zeit: 5000 };
  assert.equal(V.vorschauEntscheid({ gemerkt: g, stand: 'abc', jetzt: 5000 + 1000 }).aktion, 'warten');
  assert.equal(V.vorschauEntscheid({ gemerkt: g, stand: 'abc', jetzt: 5000 + MIN }).aktion, 'jetzt');
  assert.equal(V.vorschauEntscheid({ gemerkt: g, stand: 'abc', jetzt: 6500, abstandMs: 1000 }).aktion, 'jetzt', 'Abstand einstellbar (Test)');
});

test('teilZeigen: Räume und Hintergrund-Netze kommen nicht ins Bild', () => {
  assert.equal(V.teilZeigen('raum', { art: 'quader' }), false);
  assert.equal(V.teilZeigen('wand', { art: 'netz' }), false);
  assert.equal(V.teilZeigen('wand', { art: 'quader' }), true);
  assert.equal(V.teilZeigen('leitung', { art: 'zylinder' }), true);
});

test('teilStil: Architektur grau, Gebäudetechnik in der Systemfarbe des Kerns', () => {
  const wand = V.teilStil('wand', { art: 'quader', farbe: '#ff0000' });
  assert.match(wand.farbe, /^#[0-9a-f]{6}$/i);
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(wand.farbe.slice(i, i + 2), 16));
  assert.ok(Math.abs(r - g) < 24 && Math.abs(g - b) < 24, 'Architektur ist grau, auch wenn der Kern eine Farbe liefert');
  assert.equal(wand.kanten, true);
  assert.ok(wand.deckkraft < 1);

  const rohr = V.teilStil('leitung', { art: 'zylinder', farbe: '#2e8b57', material: 'rohr' });
  assert.deepEqual(rohr, { farbe: '#2e8b57', deckkraft: 1, kanten: false });
  assert.equal(V.teilStil('leitung', { art: 'zylinder' }).farbe, V.SYSTEM_VORGABE, 'ohne Systemfarbe die Vorgabe');
  assert.equal(V.teilStil('leitung', { art: 'zylinder', farbe: 'rot; background:url(x)' }).farbe, V.SYSTEM_VORGABE, 'nur Hex-Farben');
  assert.equal(V.teilStil('sonst', { art: 'quader', material: 'geraet', farbe: '#112233' }).farbe, '#112233', 'Gerät mit Material gilt als Gebäudetechnik');
  assert.equal(V.istGebaeudetechnik('apparat', {}), true);
  assert.equal(V.istGebaeudetechnik('wand', { material: 'wand' }), false);
});

test('Vorschaubild: Grösse 640 × 400, Blick von Südwesten, Adresse zum Speichern', () => {
  assert.equal(V.BILD_BREITE, 640);
  assert.equal(V.BILD_HOEHE, 400);
  const [x, y, z] = V.BLICK_VON;
  assert.ok(x < 0, 'Westen liegt bei −x');
  assert.ok(z > 0, 'Süden liegt bei +z (z = −Nord)');
  assert.ok(y > 0, 'von oben');
  assert.equal(V.vorschauSpeicherUrl('abc', 'C:\\a b'), '/api/projekt/vorschau?stand=abc&ort=C%3A%5Ca%20b');
  assert.equal(V.vorschauSpeicherUrl(null, null), '/api/projekt/vorschau?stand=&ort=');
});

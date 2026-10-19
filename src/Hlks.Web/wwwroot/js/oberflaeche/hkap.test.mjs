// Tests der Texte, Tabellen und Parameter für den Austausch mit HKAP (Modul 1d, Auftrag C) ohne Browser: Verbindung, Verknüpfung, Abgleich-Tabellen,
// Konflikte mit Wahl, Parameter von hkap_uebernehmen / hkap_senden / hkap_befunde_senden / hkap_raum_zuordnen, Ergebnisanzeige, nicht platzierte Räume.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/hkap.test.mjs
// Die Beispieldaten haben die Form der Antworten des Kerns (HkapBefehle.cs, HkapAbgleich.Vorschau, HkapPendenzen.Vorschau).
// Ist HLKS_TEST_SERVER gesetzt (Server mit offenem Projekt), prüft ein zusätzlicher Test die Bausteine gegen die echte Antwort von hkap_verbindung.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const T = await import('./hkap-text.js');

// ---------------------------------------------------------------------------------------------- Beispieldaten (Form wie im Kern)

const link = { projekt: 'P-005', projektierung: 'PJ-2026-001', kuerzel: 'TP', name: 'Testprojekt', url: null };
const info = { klasse: 'Projektinformation', kennzeichen: 'PI-1', werte: { hkap: { projekt: 'P-005', projektierung: 'PJ-2026-001', kuerzel: 'TP', name: 'Testprojekt' } } };

const verbindungOk = {
  url: 'http://localhost:5098', verknuepfung: null, erreichbar: true, fassung: '1.0', datenarten: [{ name: 'projekt', felder: 8 }, { name: 'geschosse', felder: 5 }],
  projekte: [{ id: 'P-004', name: 'Altbau', phase: 'Ausführung' }, { id: 'P-005', name: 'Testprojekt', phase: 'Vorprojekt' }],
  projektierungen: [{ id: 'PJ-2026-001', name: 'Variante A' }, { id: 'PJ-2026-002', name: 'Variante B' }],
};
const verbindungAus = {
  url: 'http://localhost:5013', verknuepfung: null, erreichbar: false,
  meldung: 'HKAP ist unter http://localhost:5013 nicht erreichbar (Es konnte keine Verbindung hergestellt werden).',
  vorschlag: 'HKAP starten (es beendet sich 12 s nach dem Schliessen seines Browserfensters) und erneut versuchen; Adresse prüfen (http://localhost:5013). Nichts ist verloren: KICAD hat nichts geändert, der Basisstand bleibt.',
};

const konfliktBez = { feld: 'bezeichnung', kicad: 'Bad', hkap: 'WC/Bad', basis: 'Nasszelle', schluessel: 'raeume/E1-S3/bezeichnung', datenart: 'raeume', hkapId: 'E1-S3', element: '1.02' };
const konfliktNutz = { feld: 'nutzung', kicad: 'Bad', hkap: 'Dusche', basis: null, schluessel: 'raeume/E1-S4/nutzung', datenart: 'raeume', hkapId: 'E1-S4', element: '1.03' };

const raeume = {
  name: 'raeume',
  nachKicad: {
    neu: [{ hkapId: 'E1-S5', werte: { etage: 'E1', raumNummer: '1.05', bezeichnung: 'Büro' }, hinweis: 'wird nicht platzierter Raum' }],
    geaendert: [{ hkapId: 'E1-S1', element: '1.01', felder: [{ feld: 'nutzung', kicad: 'Wohnen', hkap: 'Essen', basis: 'Wohnen', ueberschreibt: null, grund: null }, { feld: 'bezeichnung', kicad: 'Zimmer', hkap: 'Esszimmer', basis: 'Zimmer', ueberschreibt: true, grund: 'HKAP führt' }] }],
    geloescht: [{ hkapId: 'E2-S9', element: '2.09', hinweis: 'in HKAP gelöscht – KICAD-Element von Hand löschen (Vorschlag)' }],
  },
  nachHkap: {
    neu: [{ element: '1.07', werte: { etage: 'E1', raumNummer: '1.07', flaecheM2: 12.5 }, hinweis: null }],
    geaendert: [{ hkapId: 'E1-S2', element: '1.02', felder: [{ feld: 'kicad.flaecheM2', kicad: 14.25, hkap: 13.9, basis: 13.9, ueberschreibt: null, grund: null }] }],
    geloescht: [],
  },
  konflikte: [konfliktBez, konfliktNutz],
  abweichend: [{ hkapId: 'E1-S6', element: '1.06', feld: 'tempWinter', kicad: null, hkap: 21, basis: 21, grund: 'KICAD leer, HKAP-Wert bleibt' }],
  nurLesen: [{ hkapId: 'E1-S1', element: '1.01', werte: { personen: 4, zuluft: 120, flaecheHkapM2: 13.2 }, geaendertInHkap: ['zuluft'], flaecheKicadM2: 13.4 }],
  unveraendert: 7,
  nichtPlatziert: ['1.05', '1.08'],
  zuordnungsvorschlaege: [{ hkapId: 'E1-S8', raum: '1.08', grund: 'gleiche Raumnummer' }],
  hinweise: ['Geschoss E1: Höhe 3000 mm als Platzhalter'],
};
const geschosse = { name: 'geschosse', nachKicad: { neu: [{ hkapId: 'E3', werte: { name: 'Attika', koteMm: 6000 } }], geaendert: [], geloescht: [] }, nachHkap: { neu: [], geaendert: [], geloescht: [] }, konflikte: [], abweichend: [], nurLesen: [], unveraendert: 2 };
// Stränge (Modul 3, B5): ein HKAP-Strang bündelt mehrere KICAD-Stränge; Form wie HkapAbgleich.Vorschau + HkapStraenge.VorschauErgaenzen
const strangBlock = [
  { id: 'a1', kennzeichen: 'HZ-RL-1', name: 'Heizung 1', system: 'HZ-RL', systemtyp: 'Heizung Rücklauf', geschosse: ['E1'], endgeraete: 1, laengen: [{ serie: 'Testrohrserie (fiktiv)', groesse: 'DN 20', laengeM: 2 }] },
  { id: 'a2', kennzeichen: 'HZ-VL-1', name: 'Heizung 1', system: 'HZ-VL', systemtyp: 'Heizung Vorlauf', geschosse: ['E2', 'E1'], endgeraete: 2, laengen: [{ serie: 'Testrohrserie (fiktiv)', groesse: 'DN 20', laengeM: 4.45 }, { serie: 'Testrohrserie (fiktiv)', groesse: 'DN 25', laengeM: 1 }] },
];
const straenge = {
  name: 'straenge',
  nachKicad: {
    neu: [
      { hkapId: 'ST3', werte: { nummer: 'L_01', bezeichnung: 'Zuluft Nord', medium: 'Zuluft' }, hinweis: 'wird nicht platzierter Strang im System ZUL' },
      { hkapId: 'ST4', werte: { nummer: 'H_02', bezeichnung: 'Heizung Haus', medium: 'Heizung' }, hinweis: 'nicht angelegt: mehrere passende Systeme (HZ-RL, HZ-VL) – Wahl «system» angeben' },
    ],
    geaendert: [{ hkapId: 'ST1', element: 'HZ-RL-1, HZ-VL-1', felder: [{ feld: 'bezeichnung', kicad: 'Heizung 1', hkap: 'Heizung Nord', basis: 'Heizung 1', ueberschreibt: null, grund: null }] }],
    geloescht: [],
  },
  nachHkap: {
    neu: [{ element: 'KW-1, SW-1, WW-1', werte: { bezeichnung: 'Sanitär 1', medium: 'Sanitär', 'kicad.straenge': strangBlock }, hinweis: '3 KICAD-Stränge in einem neuen HKAP-Strang gebündelt' }],
    geaendert: [{ hkapId: 'ST1', element: 'HZ-RL-1, HZ-VL-1', felder: [{ feld: 'kicad.straenge', kicad: strangBlock, hkap: null, basis: null, ueberschreibt: null, grund: 'KICAD führt' }] }],
    geloescht: [],
  },
  konflikte: [{ feld: 'bezeichnung', kicad: 'Heizung Süd', hkap: 'Heizung Nord', basis: 'Heizung 1', schluessel: 'straenge/ST1/bezeichnung', datenart: 'straenge', hkapId: 'ST1', element: 'HZ-RL-1, HZ-VL-1' }],
  abweichend: [], nurLesen: [], unveraendert: 0,
  nichtPlatziert: ['L_01'],
  zuordnungsvorschlaege: [{ hkapId: 'ST5', strang: 'HZ-RL-2, HZ-VL-2', grund: 'gleiche Bezeichnung' }],
  systemWahl: [{
    hkapId: 'ST4', medium: 'Heizung', bezeichnung: 'Heizung Haus', kandidaten: ['HZ-RL', 'HZ-VL'],
    systeme: [{ kennzeichen: 'HZ-RL', systemtyp: 'Heizung Rücklauf' }, { kennzeichen: 'HZ-VL', systemtyp: 'Heizung Vorlauf' }, { kennzeichen: 'KW', systemtyp: 'Trinkwasser kalt' }],
  }],
  buendeln: 'auto', hinweise: ['SO-VL-1: der Systemtyp «Sole Vorlauf» hat kein HKAP-Medium (Annahme) – das Medium bleibt leer.'],
};
const projektDaten = { name: 'projekt', nachKicad: { neu: [], geaendert: [{ hkapId: 'P-005', element: 'PI-1', felder: [{ feld: 'bauherr', kicad: 'Muster AG', hkap: 'Muster Immobilien AG', basis: 'Muster AG', ueberschreibt: true, grund: 'HKAP führt' }] }], geloescht: [] }, nachHkap: { neu: [], geaendert: [], geloescht: [] }, konflikte: [], abweichend: [], nurLesen: [], unveraendert: 1 };
const pendenzen = {
  name: 'pendenzen', hinweis: 'Vorschau von hkap_befunde_senden mit schwere fehler.',
  neu: [{ quelleId: 'P01|1.02', aufgabe: 'Raum 1.02: Fläche fehlt', raum: '1.02', prioritaet: 'hoch' }],
  geaendert: [{ id: 'PD-3', felder: { aufgabe: 'Raum 1.03: Höhe fehlt' } }],
  erledigtVorschlag: [{ id: 'PD-1', aufgabe: 'Raum 1.01: …', status: 'offen' }],
  erledigtInHkap: [{ id: 'PD-2', aufgabe: 'Raum 1.09: …', hinweis: 'Befund besteht in KICAD weiter – Pendenz in HKAP wieder öffnen?' }],
  unveraendert: 2, fremd: 1,
};
const abgleich = {
  verknuepft: true, url: 'http://localhost:5098', projekt: 'P-005', projektierung: 'PJ-2026-001', erreichbar: true, fassung: '1.0',
  basis: { ort: 'austausch/hkap.json', stand: '2026-10-18 14:02:11', datensaetze: 14 },
  warnungen: ['HKAP-Projekt P-005: Name «Altname» → «Testprojekt» – Verknüpfung prüfen.'],
  zusammenfassung: { nachKicad: 3, nachHkap: 2, konflikte: 2, vorschlaege: 2 },
  konflikte: [konfliktBez, konfliktNutz],
  datenarten: [projektDaten, geschosse, raeume, straenge, pendenzen],
  hinweis: 'Raumanforderungen …',
};

// ---------------------------------------------------------------------------------------------- Verbindung und Verknüpfung

test('wertText: leer, Zahl, Wahrheitswert, Objekt, Liste, lange Texte', () => {
  assert.equal(T.wertText(null), '–');
  assert.equal(T.wertText(''), '–');
  assert.equal(T.wertText(12.34567), '12.346');
  assert.equal(T.wertText(true), 'ja');
  assert.equal(T.wertText({ a: 1, b: null, c: 'x' }), 'a: 1 · c: x');
  assert.equal(T.wertText([1, 'a']), '1, a');
  assert.equal(T.wertText('x'.repeat(300), 20).length, 20);
});

test('Verknüpfung aus Projektinformation.hkap; ohne Projekt keine', () => {
  assert.deepEqual(T.verknuepfung(info), link);
  assert.equal(T.verknuepfung({ werte: {} }), null);
  assert.equal(T.verknuepfung({ werte: { hkap: { projekt: ' ' } } }), null);
  assert.equal(T.verknuepfung(null), null);
  assert.equal(T.projektinfo([{ klasse: 'Wand', kennzeichen: 'A' }, { klasse: 'Projektinformation', kennzeichen: 'PI-2' }, { klasse: 'Projektinformation', kennzeichen: 'PI-1' }]).kennzeichen, 'PI-1');
  assert.match(T.verknuepfungText(link), /P-005 «Testprojekt» \(TP\) · Projektierung PJ-2026-001/);
  assert.equal(T.verknuepfungText(null), 'nicht verknüpft');
});

test('Adresse: Vorgabe aus der Verknüpfung, sonst Standard; Prüfung', () => {
  assert.equal(T.urlVorgabe(null), 'http://localhost:5013');
  assert.equal(T.urlVorgabe({ ...link, url: 'http://localhost:5098' }), 'http://localhost:5098');
  assert.equal(T.urlNormal(' http://localhost:5098/ '), 'http://localhost:5098');
  assert.ok(T.urlGueltig('http://localhost:5098'));
  assert.ok(T.urlGueltig('https://hkap.intern/app/'));
  assert.ok(!T.urlGueltig('localhost:5098'));
  assert.ok(!T.urlGueltig('ftp://x'));
  assert.deepEqual(T.verbindungParameter(' http://localhost:5098/ ', 'P-005'), { url: 'http://localhost:5098', projekt: 'P-005' });
  assert.deepEqual(T.verbindungParameter('', null), {});
});

test('hkap_verbindung: erreichbar zeigt Fassung, nicht erreichbar zeigt Kern-Meldung und Vorschlag als Hinweiskasten (kein Fehlerkasten)', () => {
  const ok = T.verbindungStatus(verbindungOk);
  assert.equal(ok.ok, true);
  assert.equal(ok.fassung, '1.0');
  const h = T.verbindungHtml(verbindungOk);
  assert.match(h, /hkap-ok/);
  assert.match(h, /Austauschfassung 1\.0/);
  assert.match(h, /2 Datenarten/);

  const aus = T.verbindungStatus(verbindungAus);
  assert.equal(aus.ok, false);
  assert.equal(aus.titel, 'HKAP ist nicht erreichbar');
  const ha = T.verbindungHtml(verbindungAus);
  assert.match(ha, /hkap-kasten/);
  assert.ok(!/fehlerkasten|role="alert"/.test(ha), 'ohne Fehlerton');
  assert.ok(ha.includes('nicht erreichbar (Es konnte keine Verbindung hergestellt werden)'));
  assert.ok(ha.includes('Vorschlag: HKAP starten'));
  assert.ok(ha.includes('Nichts ist verloren'));
  // erreichbar, aber der Austauschkatalog fehlt: auch das ein Hinweis mit Vorschlag
  const k = T.verbindungHtml({ erreichbar: true, meldung: 'HKAP kennt Austauschkatalog nicht (404).', vorschlag: 'HKAP-Fassung mit Modul 1d starten.' });
  assert.match(k, /antwortet, aber nicht wie erwartet/);
  assert.match(k, /HKAP-Fassung mit Modul 1d starten/);
  assert.equal(T.verbindungHtml(null), '');
});

test('Projekt- und Projektierungsliste: gewählte Zeile, bestehende Verknüpfung markiert, Namen maskiert', () => {
  const h = T.projekteHtml([...verbindungOk.projekte, { id: 'P-006', name: '<b>x</b>', phase: '' }], 'P-005', link);
  assert.match(h, /<tr data-projekt="P-005" class="gewaehlt"/);
  assert.match(h, /P-005 <span class="empfohlen">verknüpft/);
  assert.ok(!/P-004<span class="empfohlen"/.test(h));
  assert.ok(h.includes('&lt;b&gt;x&lt;/b&gt;'));
  assert.match(T.projekteHtml([], null, null), /keine Projekte/);
  const p = T.projektierungenHtml(verbindungOk.projektierungen, 'PJ-2026-002', link, 'P-005');
  assert.match(p, /data-projektierung="PJ-2026-002" class="gewaehlt"/);
  assert.match(p, /PJ-2026-001 <span class="empfohlen">verknüpft/);
  assert.match(T.projektierungenHtml(null), /Zuerst ein Projekt wählen/);
  assert.match(T.projektierungenHtml([]), /keine Projektierung/);
});

test('Projektliste: Suche nach Id, Name und Phase', () => {
  const l = [{ id: 'P-004', name: 'Bern_ARK_Murtenstrasse', phase: 'Ausführung' }, { id: 'P-005', name: 'Bern_BKWH2', phase: null }, { id: 'P-023', name: 'Spiegel_BUR', phase: 'Ausschreibung' }];
  const ids = (h) => [...h.matchAll(/data-projekt="([^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(ids(T.projekteHtml(l, null, null, '')), ['P-004', 'P-005', 'P-023']);
  assert.deepEqual(ids(T.projekteHtml(l, null, null, 'bern')), ['P-004', 'P-005']);
  assert.deepEqual(ids(T.projekteHtml(l, null, null, ' P-023 ')), ['P-023']);
  assert.deepEqual(ids(T.projekteHtml(l, null, null, 'ausschr')), ['P-023']);
  assert.match(T.projekteHtml(l, null, null, 'bern'), /2 von 3 Projekten/);
  assert.match(T.projekteHtml(l, null, null, 'xyz<'), /Kein Projekt passt zu «xyz&lt;» \(3 in HKAP\)/);
});

test('Warnungen vor dem Verknüpfen: Wechsel von Projekt oder Projektierung, Name weicht ab', () => {
  assert.deepEqual(T.wechselWarnungen(null, 'P-005'), []);
  assert.deepEqual(T.wechselWarnungen(link, 'P-005', 'PJ-2026-001'), []);
  const w1 = T.wechselWarnungen(link, 'P-007', 'PJ-2026-009');
  assert.equal(w1.length, 1);
  assert.match(w1[0], /verknüpft; es wird auf P-007 umgestellt/);
  assert.match(w1[0], /Basisstand des alten Abgleichs gilt dann nicht mehr/);
  const w2 = T.wechselWarnungen(link, 'P-005', 'PJ-2026-002');
  assert.match(w2[0], /Projektierung wechselt von PJ-2026-001 auf PJ-2026-002/);
  assert.match(T.wechselWarnungen(link, 'P-005', null)[0], /auf ohne Projektierung/);

  // Name und Kürzel nennt der Kern im Trockenlauf von hkap_verknuepfen (die Projektliste von HKAP trägt einen anderen Namen)
  const angaben = T.angabenAusHinweis('Verknüpft mit HKAP P-005 «Testprojekt» (TP), Projektierung PJ-2026-001. Weiter mit hkap_abgleich.');
  assert.deepEqual(angaben, { projekt: 'P-005', name: 'Testprojekt', kuerzel: 'TP' });
  assert.equal(T.angabenAusHinweis('Schon verknüpft mit HKAP P-005/PJ-2026-001.'), null);
  assert.deepEqual(T.angabenAusHinweis('Verknüpft mit HKAP P-007 «BKW HS, Kühlen» (BKWH2) – ohne Projektierung'), { projekt: 'P-007', name: 'BKW HS, Kühlen', kuerzel: 'BKWH2' });
  assert.equal(T.namensAbweichung(link, angaben), null);
  assert.equal(T.namensAbweichung(link, { ...angaben, name: 'testprojekt' }), null, 'Gross/Klein ist keine Abweichung');
  assert.match(T.namensAbweichung(link, { ...angaben, name: 'Neuer Name' }), /heisst jetzt «Neuer Name» \(TP\); verknüpft war «Testprojekt» \(TP\)/);
  assert.match(T.namensAbweichung(link, { ...angaben, kuerzel: 'NEU' }), /\(NEU\); verknüpft war «Testprojekt» \(TP\)/);
  assert.match(T.namensAbweichung(link, { ...angaben, name: 'Anders' }), /Id kann wiederkehren/);
  assert.equal(T.namensAbweichung(link, { ...angaben, projekt: 'P-006', name: 'Anders' }), null, 'anderes Projekt: kein Namensvergleich (der Wechsel wird getrennt gemeldet)');
  assert.equal(T.namensAbweichung(null, angaben), null);
  assert.equal(T.namensAbweichung(link, null), null);
});

test('hkap_verknuepfen: Parameter – Adresse nur bei Abweichung vom bisher Wirksamen', () => {
  assert.deepEqual(T.verknuepfenParameter({ projekt: ' P-005 ', projektierung: 'PJ-2026-001', url: 'http://localhost:5013' }), { projekt: 'P-005', projektierung: 'PJ-2026-001' });
  assert.deepEqual(T.verknuepfenParameter({ projekt: 'P-005', url: 'http://localhost:5098/' }), { projekt: 'P-005', url: 'http://localhost:5098' });
  assert.deepEqual(T.verknuepfenParameter({ projekt: 'P-005', projektierung: '', url: '' }), { projekt: 'P-005' });
  // bestehende Verknüpfung mit eigener Adresse: unverändert → nichts senden; zurück auf den Standard → ausdrücklich senden
  const mitUrl = { ...link, url: 'http://localhost:5098' };
  assert.deepEqual(T.verknuepfenParameter({ projekt: 'P-005', url: 'http://localhost:5098', link: mitUrl }), { projekt: 'P-005' });
  assert.deepEqual(T.verknuepfenParameter({ projekt: 'P-005', url: 'http://localhost:5013', link: mitUrl }), { projekt: 'P-005', url: 'http://localhost:5013' });
  assert.equal(T.verknuepfenPruefen({ projekt: 'P-005', url: 'http://localhost:5098' }), true);
  assert.match(T.verknuepfenPruefen({ projekt: '', url: '' }), /Projekt aus der Liste/);
  assert.match(T.verknuepfenPruefen({ projekt: 'P-005', url: 'blabla' }), /ungültig/);
});

// ---------------------------------------------------------------------------------------------- Abgleich

test('hkap_abgleich: nicht verknüpft und nicht erreichbar zeigen Kern-Meldung und Vorschlag als Hinweiskasten', () => {
  const nv = { verknuepft: false, meldung: 'Das Projekt ist nicht mit HKAP verknüpft.', vorschlag: 'hkap_verknuepfen { projekt: "P-…" } aufrufen.' };
  assert.equal(T.abgleichStatus(nv).art, 'nichtVerknuepft');
  const h = T.abgleichKastenHtml(nv);
  assert.match(h, /noch nicht mit HKAP verknüpft/);
  assert.match(h, /Das Projekt ist nicht mit HKAP verknüpft\./);
  assert.ok(!/fehlerkasten/.test(h));
  const na = { verknuepft: true, erreichbar: false, meldung: verbindungAus.meldung, vorschlag: verbindungAus.vorschlag };
  assert.equal(T.abgleichStatus(na).art, 'nichtErreichbar');
  const h2 = T.abgleichKastenHtml(na);
  assert.match(h2, /HKAP ist nicht erreichbar/);
  assert.ok(h2.includes('Nichts ist verloren'));
  assert.match(h, /Vorschlag: Im Register Verwalten, Gruppe HKAP, «Verbinden» wählen/, 'Vorschlag der Oberfläche statt des MCP-Aufrufs');
  assert.ok(!/hkap_verknuepfen/.test(h));
  assert.equal(T.abgleichKastenHtml(abgleich), '');
  assert.equal(T.abgleichStatus(abgleich).art, 'ok');
});

test('Zusammenfassung und Zählung', () => {
  assert.equal(T.zusammenfassungText(abgleich), '← 3 aus HKAP · → 2 nach HKAP · 2 Konflikte · 2 Vorschläge');
  assert.equal(T.zusammenfassungText({ zusammenfassung: { nachKicad: 0, nachHkap: 0, konflikte: 1, vorschlaege: 0 } }), '← 0 aus HKAP · → 0 nach HKAP · 1 Konflikt');
  assert.deepEqual(T.zaehlen(raeume), { nachKicad: 2, nachHkap: 2, konflikte: 2, vorschlaege: 2, unveraendert: 7 });
  assert.deepEqual(T.zaehlen(pendenzen), { nachKicad: 0, nachHkap: 2, konflikte: 0, vorschlaege: 2, unveraendert: 2 });
  assert.equal(T.abschnittKopf(raeume), '← 2 · → 2 · 2 Konflikte · 2 Vorschläge · 7 unverändert');
  assert.equal(T.abschnittKopf({ name: 'x' }), 'nichts zu tun');
  assert.match(T.basisText(abgleich.basis), /^vom 2026-10-18 14:02:11 \(14 Datensätze im Basisstand\)$/);
  assert.match(T.basisText({ stand: null }), /Noch kein Abgleich/);
});

test('Änderungszeilen: Richtung, Art, alt/neu je Richtung', () => {
  const z = T.zeilenVon(raeume);
  assert.equal(z.length, 1 + 2 + 1 + 1 + 1);   // neu←, 2 Felder←, gelöscht←, neu→, geändert→
  const nutzung = z.find((x) => x.feld === 'nutzung');
  assert.deepEqual([nutzung.richtung, nutzung.art, nutzung.datensatz, nutzung.hkapId, nutzung.alt, nutzung.neu], ['nachKicad', 'geaendert', '1.01', 'E1-S1', 'Wohnen', 'Essen']);
  const flaeche = z.find((x) => x.feld === 'kicad.flaecheM2');
  assert.deepEqual([flaeche.richtung, flaeche.alt, flaeche.neu], ['nachHkap', 13.9, 14.25], 'nach HKAP: alt = HKAP-Wert, neu = KICAD-Wert');
  assert.match(z.find((x) => x.feld === 'bezeichnung').hinweis, /überschreibt den Wert der anderen Seite · HKAP führt/);
  assert.equal(z.find((x) => x.art === 'neu' && x.richtung === 'nachKicad').datensatz, 'E1-S5');
  assert.equal(z.find((x) => x.art === 'neu' && x.richtung === 'nachHkap').datensatz, '1.07');
  const g = z.find((x) => x.art === 'geloescht');
  assert.equal(g.richtung, 'nachKicad');
  assert.match(g.hinweis, /Vorschlag/);
  assert.deepEqual(T.zeilenVon({}), []);
});

test('Änderungstabelle: Richtung ← / →, neu / geändert / gelöscht (Vorschlag), Feldwerte alt und neu', () => {
  const h = T.aenderungenZeilenHtml(T.zeilenVon(raeume));
  assert.match(h, /← aus HKAP/);
  assert.match(h, /→ nach HKAP/);
  assert.match(h, /<td class="art-neu">neu<\/td>/);
  assert.match(h, /<td class="art-geaendert">geändert<\/td>/);
  assert.match(h, /<td class="art-geloescht">gelöscht \(Vorschlag\)<\/td>/);
  assert.match(h, /<td class="hkap-wert">Wohnen<\/td><td class="hkap-wert">Essen<\/td>/);
  assert.match(h, /etage: E1 · raumNummer: 1\.05 · bezeichnung: Büro/);
  assert.match(h, /1\.01 <span class="leise">E1-S1<\/span>/);
  assert.match(T.aenderungenZeilenHtml([]), /Keine Änderungen/);
});

test('Konflikte: beide Fassungen und Basis, Wahl je Zeile; ohne Wahl bleibt offen', () => {
  const h = T.konflikteHtml(raeume.konflikte, {}, null);
  assert.match(h, /<th>KICAD<\/th><th>HKAP<\/th><th>Stand beim letzten Abgleich<\/th><th>Wahl<\/th>/);
  assert.ok(h.includes('data-schluessel="raeume/E1-S3/bezeichnung"'));
  assert.match(h, /<td class="hkap-wert">Bad<\/td><td class="hkap-wert">WC\/Bad<\/td><td class="hkap-wert leise">Nasszelle<\/td>/);
  assert.equal((h.match(/<select/g) ?? []).length, 2);
  assert.ok(!/value="(kicad|hkap)" selected/.test(h), 'Vorgabe: keine Wahl');
  assert.match(h, /<option value="" selected>offen lassen<\/option>/);
  const h2 = T.konflikteHtml(raeume.konflikte, { 'raeume/E1-S3/bezeichnung': 'hkap' }, null);
  assert.match(h2, /<option value="hkap" selected>HKAP<\/option>/);
  assert.match(h2, /class="hkap-konflikt entschieden"/);
  assert.equal(T.konflikteHtml([], {}, null), '');
  // «alle übrigen» steht im Auswahlfeld der Zeilen ohne eigene Wahl
  assert.match(T.konflikteHtml(raeume.konflikte, {}, 'kicad'), /übrige: KICAD/);
});

test('Konfliktwahl → Parameter konflikte (mit «*» für alle übrigen), bereinigen, offene zählen', () => {
  assert.equal(T.konfliktParameter({}, null), null);
  assert.equal(T.konfliktParameter({ 'a/b/c': '' }, null), null);
  assert.deepEqual(T.konfliktParameter({ 'raeume/E1-S3/bezeichnung': 'kicad' }, null), { 'raeume/E1-S3/bezeichnung': 'kicad' });
  assert.deepEqual(T.konfliktParameter({ 'raeume/E1-S3/bezeichnung': 'kicad' }, 'hkap'), { 'raeume/E1-S3/bezeichnung': 'kicad', '*': 'hkap' });
  assert.deepEqual(T.konfliktParameter({}, 'kicad'), { '*': 'kicad' });
  assert.deepEqual(T.konfliktSchluessel(abgleich), ['raeume/E1-S3/bezeichnung', 'raeume/E1-S4/nutzung']);
  assert.equal(T.offeneKonflikte(abgleich, {}, null), 2);
  assert.equal(T.offeneKonflikte(abgleich, { 'raeume/E1-S3/bezeichnung': 'kicad' }, null), 1);
  assert.equal(T.offeneKonflikte(abgleich, {}, 'hkap'), 0);
  assert.deepEqual(T.wahlBereinigen({ 'raeume/E1-S3/bezeichnung': 'kicad', 'raeume/E9-S9/name': 'hkap' }, abgleich), { 'raeume/E1-S3/bezeichnung': 'kicad' });
});

test('Abschnitt einer Datenart: Räume mit Konflikten, Zuordnungsvorschlag, nicht platzierte, nur Lesen, abweichend', () => {
  const h = T.datenartHtml(raeume, {}, null);
  assert.match(h, /<h3 class="hkap-abschnitt" data-datenart="raeume">Räume/);
  assert.match(h, /Konflikte \(2\) – beide Seiten haben seit dem letzten Abgleich geändert/);
  assert.match(h, /class="dlg-knopf h-zuordnen" data-hkap="E1-S8" data-raum="1\.08"/);
  assert.match(h, /Nicht platzierte Räume \(2\):<\/b> 1\.05, 1\.08/);
  assert.match(h, /Nur zum Lesen: Anforderungen aus HKAP \(1\)/);
  assert.match(h, /KICAD 13\.4 m² · HKAP 13\.2 m²/);
  assert.match(h, /seit dem letzten Abgleich in HKAP geändert: zuluft/);
  assert.match(h, /Abweichend, aber ohne Fluss \(1\)/);
  assert.match(h, /Geschoss E1: Höhe 3000 mm als Platzhalter/);
});

test('Viele nicht platzierte Räume: der Abschnitt nennt zwölf und zählt den Rest', () => {
  const viele = Array.from({ length: 30 }, (_, i) => `R-${i + 1}`);
  const h = T.datenartHtml({ ...raeume, nichtPlatziert: viele });
  assert.match(h, /Nicht platzierte Räume \(30\):<\/b> R-1, R-2, R-3, R-4, R-5, R-6, R-7, R-8, R-9, R-10, R-11, R-12 … und 18 weitere/);
  assert.ok(!h.includes('R-13'));
});

test('Datenart ohne Projektierung zeigt nur den Hinweis; Pendenzen mit Befundvorschau', () => {
  const h = T.datenartHtml({ name: 'geschosse', hinweise: ['keine Projektierung verknüpft'] });
  assert.match(h, /keine Projektierung verknüpft/);
  assert.ok(!/<table/.test(h));
  const p = T.datenartHtml(pendenzen);
  assert.match(p, /Pendenzen/);
  assert.match(p, /P01\|1\.02/);
  assert.match(p, /Priorität hoch/);
  assert.match(p, /erledigt \(Vorschlag\)/);
  assert.match(p, /in HKAP erledigt/);
  assert.match(p, /2 schon aktuell · 1 KICAD-Pendenz\(en\) aus anderen Projekten/);
  assert.match(p, /Prüfbefunde senden/);
});

test('Alle Abschnitte in fester Reihenfolge, Warnungen des Kerns als Kasten', () => {
  const h = T.abgleichHtml(abgleich, {}, null);
  const pos = ['projekt', 'geschosse', 'raeume', 'straenge', 'pendenzen'].map((n) => h.indexOf(`data-datenart="${n}"`));
  assert.ok(pos.every((p) => p > 0) && [...pos].sort((a, b) => a - b).join() === pos.join(), 'Reihenfolge ' + pos);
  const w = T.warnungenHtml(abgleich.warnungen);
  assert.match(w, /Bitte prüfen/);
  assert.match(w, /Name «Altname» → «Testprojekt»/);
  assert.equal(T.warnungenHtml([]), '');
  assert.equal(T.warnungenHtml(undefined), '');
});

test('Alles Fremde wird maskiert (Namen und Werte aus HKAP)', () => {
  const frech = { name: 'raeume', nachKicad: { neu: [{ hkapId: 'E1-S1', werte: { bezeichnung: '<img src=x onerror=alert(1)>' } }], geaendert: [], geloescht: [] }, nachHkap: { neu: [], geaendert: [], geloescht: [] }, konflikte: [{ ...konfliktBez, kicad: '<script>x</script>' }] };
  const h = T.datenartHtml(frech, {}, null);
  assert.ok(!/<img|<script/.test(h));
  assert.ok(h.includes('&lt;img src=x onerror=alert(1)&gt;'));
  assert.ok(h.includes('&lt;script&gt;x&lt;/script&gt;'));
});

// ---------------------------------------------------------------------------------------------- Übernehmen und Senden

test('hkap_uebernehmen: Datenarten nur bei Teilauswahl, Konflikte nur mit Wahl', () => {
  assert.deepEqual(T.uebernehmenParameter(), {});
  assert.deepEqual(T.uebernehmenParameter({ datenarten: ['projekt', 'geschosse', 'raeume', 'straenge'] }), {});
  assert.deepEqual(T.uebernehmenParameter({ datenarten: ['projekt', 'geschosse', 'raeume'] }), { datenarten: ['projekt', 'geschosse', 'raeume'] });   // ohne Stränge ist eine Teilauswahl
  assert.deepEqual(T.uebernehmenParameter({ datenarten: ['raeume', 'geschosse'] }), { datenarten: ['geschosse', 'raeume'] });
  assert.deepEqual(T.uebernehmenParameter({ datenarten: ['geschosse', 'pendenzen'] }), { datenarten: ['geschosse'] });
  assert.deepEqual(T.uebernehmenParameter({ wahl: { 'raeume/E1-S3/bezeichnung': 'hkap' }, uebrige: 'kicad' }), { konflikte: { 'raeume/E1-S3/bezeichnung': 'hkap', '*': 'kicad' } });
  assert.equal(T.uebernehmenPruefen(['pendenzen']).length > 5, true);
  assert.equal(T.uebernehmenPruefen(['geschosse']), true);
});

test('hkap_senden: Projektdaten führt HKAP und fallen weg; Datenarten nur bei Teilauswahl', () => {
  assert.deepEqual(T.sendenParameter(), {});
  assert.deepEqual(T.sendenParameter({ datenarten: ['projekt', 'geschosse', 'raeume', 'straenge'] }), {});
  assert.deepEqual(T.sendenParameter({ datenarten: ['geschosse', 'raeume'] }), { datenarten: ['geschosse', 'raeume'] });
  assert.deepEqual(T.sendenParameter({ datenarten: ['projekt', 'raeume'] }), { datenarten: ['raeume'] });
  assert.equal(T.sendenPruefen(['geschosse']), true);
  assert.match(T.sendenPruefen(['projekt']), /Geschosse, Räume oder Stränge/);
});

test('Ergebnis von hkap_senden für KICAD: «Keine Änderung.» als Meldung, die Zusammenfassung steht in Hinweise[0]', () => {
  const r = {
    angenommen: true, befehl: 'hkap_senden', trockenlauf: true, meldung: 'Keine Änderung.', aenderungen: [],
    hinweise: ['Trockenlauf – HKAP würde annehmen (P-005/PJ-2026-001): 1 neu, 2 geändert, 5 unverändert, 0 Konflikte (nichts überschrieben), 0 abgelehnt, 0 Vorschläge.', 'Raum 1.07 → HKAP raeume E1-S7: neu (Trockenlauf) (flaecheM2 12.5)'],
  };
  const e = T.ergebnisAnzeige(r);
  assert.equal(e.art, 'ok');
  assert.equal(e.trockenlauf, true);
  assert.match(e.titel, /Vorschau \(Trockenlauf/);
  assert.match(e.zusammenfassung, /^Trockenlauf – HKAP würde annehmen/);
  assert.ok(!/Keine Änderung/.test(e.zusammenfassung));
  assert.equal(e.zeilen.length, 1);
  const h = T.ergebnisHtml(r);
  assert.match(h, /<p class="hkap-summe"><b>Trockenlauf – HKAP würde annehmen/);
  assert.ok(!/Keine Änderung/.test(h));
  // ohne Hinweise bleibt die Meldung
  assert.equal(T.ergebnisAnzeige({ angenommen: true, meldung: 'Keine Änderung.', hinweise: [] }).zusammenfassung, 'Keine Änderung.');
});

test('Ergebnis mit HKAP-Konflikten beim Senden: Warnung, nichts überschrieben, Zeilen hervorgehoben', () => {
  const r = {
    angenommen: true, befehl: 'hkap_senden', trockenlauf: false, meldung: 'Keine Änderung.',
    hinweise: ['An HKAP gesendet (P-005/PJ-2026-001): 0 neu, 1 geändert, 0 unverändert, 1 Konflikte (nichts überschrieben), 1 abgelehnt, 0 Vorschläge.',
      '1.02 → HKAP E1-S2: geändert (kicad.flaecheM2 14.25)',
      'KONFLIKT 1.03 (HKAP E1-S3): HKAP hat den Datensatz inzwischen geändert (jetzt Version abc) – nichts überschrieben; hkap_abgleich zeigt den neuen Stand in der Konfliktliste.',
      '1.04: von HKAP abgelehnt (Etage fehlt).'],
  };
  const e = T.ergebnisAnzeige(r);
  assert.equal(e.art, 'warnung');
  assert.equal(e.konflikte, 1);
  assert.equal(e.abgelehnt, 1);
  assert.equal(e.titel, 'Ergebnis');
  const h = T.ergebnisHtml(r);
  assert.match(h, /1 Datensatz in HKAP inzwischen geändert – nichts überschrieben/);
  assert.match(h, /<li class="konflikt">KONFLIKT 1\.03/);
  assert.match(h, /<li class="abgelehnt">1\.04: von HKAP abgelehnt/);
  assert.match(h, /<li class="">1\.02 → HKAP E1-S2/);
});

test('Abgelehnter Befehl: Fehlerkasten mit Vorschlag; HKAP-Ausfall dagegen als Hinweiskasten', () => {
  const abgelehnt = { angenommen: false, fehler: 'Das Projekt ist nicht mit HKAP verknüpft.', vorschlag: 'Zuerst verknüpfen.' };
  assert.equal(T.ergebnisAnzeige(abgelehnt).art, 'fehler');
  const h = T.ergebnisHtml(abgelehnt);
  assert.match(h, /fehlerkasten/);
  assert.match(h, /Vorschlag: Zuerst verknüpfen\./);
  const aus = { angenommen: false, fehler: verbindungAus.meldung, vorschlag: verbindungAus.vorschlag };
  assert.equal(T.ergebnisAnzeige(aus).art, 'ausfall');
  const ha = T.ergebnisHtml(aus);
  assert.match(ha, /hkap-kasten/);
  assert.ok(!/fehlerkasten|role="alert"/.test(ha));
  assert.ok(ha.includes('Nichts ist verloren'));
  assert.equal(T.ergebnisAnzeige(null).art, 'fehler');
});

test('Hinweis für Menschen: MCP-Rat am Ende entfällt, Befehlsnamen heissen wie die Knöpfe', () => {
  assert.equal(T.hinweisFuerMenschen('E.02 ist jetzt HKAP-Raum E3-S2; hkap_abgleich zeigt, welche Werte abweichen.'), 'E.02 ist jetzt HKAP-Raum E3-S2; «Abgleich» zeigt, welche Werte abweichen.');
  assert.equal(T.hinweisFuerMenschen('KICAD-Fassung «x» bleibt – mit hkap_senden an HKAP schicken.'), 'KICAD-Fassung «x» bleibt – mit «Nach HKAP senden» an HKAP schicken.');
  assert.equal(T.hinweisFuerMenschen('hkap_verknuepfen { projekt: "P-…", projektierung: "PJ-…" } aufrufen; hkap_verbindung listet die Projekte.'), '«Verbinden» aufrufen; «Verbinden» listet die Projekte.');
  assert.equal(T.hinweisFuerMenschen('hkap_unbekannt bleibt'), 'hkap_unbekannt bleibt');
  assert.equal(T.hinweisFuerMenschen('nicht angelegt: KICAD-Raum E.03 hat dieselbe Nummer – zuerst hkap_raum_zuordnen'), 'nicht angelegt: KICAD-Raum E.03 hat dieselbe Nummer – zuerst zuordnen (Abgleich, «Zuordnungsvorschläge»)');
  assert.equal(T.hinweisFuerMenschen('mit hkap_raum_zuordnen zuordnen (oder Nummer ändern).'), 'im Abgleich unter «Zuordnungsvorschläge» zuordnen (oder Nummer ändern).');
  // die Tabellen zeigen die Kern-Texte in dieser Form
  const h = T.datenartHtml({ ...raeume, nachKicad: { neu: [{ hkapId: 'E2-S37', werte: { raumNummer: '1.03' }, hinweis: 'nicht angelegt: KICAD-Raum E.03 hat dieselbe Nummer – zuerst hkap_raum_zuordnen' }], geaendert: [], geloescht: [] } });
  assert.ok(!/hkap_raum_zuordnen/.test(h));
  assert.match(h, /zuerst zuordnen \(Abgleich, «Zuordnungsvorschläge»\)/);
  assert.equal(T.hinweisFuerMenschen('Verknüpft mit HKAP P-005 «Testprojekt» (TP), Projektierung PJ-2026-001. Weiter mit hkap_abgleich.'), 'Verknüpft mit HKAP P-005 «Testprojekt» (TP), Projektierung PJ-2026-001.');
  assert.equal(T.hinweisFuerMenschen('Schon verknüpft mit HKAP P-005/PJ-2026-001.'), 'Schon verknüpft mit HKAP P-005/PJ-2026-001.');
});

test('Übernahme mit Änderungen: Zusammenfassung aus Hinweise[0], Tabelle wird eingebaut', () => {
  const r = { angenommen: true, befehl: 'hkap_uebernehmen', meldung: 'Paket 12', aenderungen: [{ art: 'angelegt', klasse: 'Raum', kennzeichen: '1.05' }],
    hinweise: ['Aus HKAP P-005/PJ-2026-001: 1 neu, 0 geändert (0 Felder), 0 Konflikte entschieden, 2 offen, 0 Vorschläge.', 'Raum 1.05 (1.05 Büro) aus HKAP E1-S5 übernommen – noch nicht platziert: Grundriss zeichnen …'] };
  const h = T.ergebnisHtml(r, '<table class="tab" id="t"></table>');
  assert.match(h, /Aus HKAP P-005\/PJ-2026-001: 1 neu/);
  assert.match(h, /id="t"/);
  assert.match(h, /noch nicht platziert/);
});

// ---------------------------------------------------------------------------------------------- Prüfbefunde senden

test('hkap_befunde_senden: Schwere wird ausdrücklich gesendet, Vorgabe fehler', () => {
  assert.deepEqual(T.befundeParameter(), { schwere: 'fehler' });
  assert.deepEqual(T.befundeParameter('warnung'), { schwere: 'warnung' });
  assert.deepEqual(T.befundeParameter('hinweis'), { schwere: 'hinweis' });
  assert.deepEqual(T.befundeParameter('quatsch'), { schwere: 'fehler' });
  assert.deepEqual(T.SCHWEREN.map((s) => s[0]), ['fehler', 'warnung', 'hinweis']);
  for (const [s] of T.SCHWEREN) assert.ok(T.SCHWERE_ERKLAERUNG[s]);
});

test('Vorschau der Befunde (Trockenlauf): Zusammenfassung und Zeilen des Kerns', () => {
  const r = { angenommen: true, trockenlauf: true, meldung: 'Keine Änderung.', hinweise: ['Prüfbefunde ab «fehler» an HKAP P-005 (Trockenlauf): 2 neue Pendenzen, 0 geändert, 0 unverändert, 0 schon aktuell, 1 behoben (Vorschlag erledigt), 0 Konflikte, 0 abgelehnt.',
    'Befund P01 1.02 → Pendenz ?: neu (Trockenlauf)', 'Pendenz PD-1: Befund behoben – Vorschlag: in HKAP auf «erledigt» setzen.'] };
  const e = T.ergebnisAnzeige(r);
  assert.match(e.zusammenfassung, /2 neue Pendenzen/);
  assert.equal(e.zeilen.length, 2);
  assert.equal(e.zeilen[1].art, 'vorschlag');
  assert.equal(e.art, 'ok');
});

// ---------------------------------------------------------------------------------------------- nicht platzierte Räume

const raumOffen = { id: 'r1', klasse: 'Raum', kennzeichen: '1.05', name: 'Büro', geschoss: 'OG1', werte: { nummer: '1.05', nutzung: 'Büro' } };
const raumGezeichnet = (id, kz, extra = {}) => ({ id, klasse: 'Raum', kennzeichen: kz, geschoss: 'OG1', werte: { nummer: kz }, geometrie: { art: 'polygon', punkte: [[0, 0, 0], [1, 0, 0], [1, 1, 0]] }, ...extra });

test('Nicht platziert = Raum ohne Geometrie (mit Kennungen: nur mit Kennung hkap)', () => {
  assert.ok(T.istNichtPlatziert(raumOffen));
  assert.ok(T.istNichtPlatziert({ ...raumOffen, geometrie: { art: 'polygon', punkte: [] } }));
  assert.ok(!T.istNichtPlatziert(raumGezeichnet('r2', '1.01')));
  assert.ok(!T.istNichtPlatziert({ klasse: 'Wand', kennzeichen: 'W1' }));
  assert.ok(T.istNichtPlatziert({ ...raumOffen, kennungen: { hkap: 'PJ-2026-001/E1-S5' } }));
  assert.ok(!T.istNichtPlatziert({ ...raumOffen, kennungen: { ifc: 'abc' } }), 'mit Kennungen zählt nur hkap');
  const l = T.nichtPlatzierte([raumGezeichnet('r2', '1.01'), { ...raumOffen, id: 'r3', kennzeichen: '1.10', werte: { nummer: '1.10' } }, raumOffen, { ...raumOffen, id: 'r4', kennzeichen: '1.9', werte: { nummer: '1.9' } }]);
  assert.deepEqual(l.map((r) => r.kennzeichen), ['1.05', '1.9', '1.10'], 'natürliche Sortierung');
  assert.equal(T.raumTitel(raumOffen), '1.05 Büro');
  assert.equal(T.raumTitel({ kennzeichen: 'R-3', werte: { nutzung: 'Bad' } }), 'R-3 Bad');
});

test('HKAP-Raum-Id: aus der Kennung, sonst aus der Herkunft (element_lesen)', () => {
  assert.equal(T.hkapIdAusKennung('PJ-2026-001/E1-S5'), 'E1-S5');
  assert.equal(T.hkapIdAusKennung('PJ-2026-001/E1-S5', 'pj-2026-001'), 'E1-S5');
  assert.equal(T.hkapIdAusKennung('PJ-2026-002/E1-S5', 'PJ-2026-001'), null, 'andere Projektierung');
  assert.equal(T.hkapIdAusKennung('ohne', null), null);
  const quelle = 'hkap, T@c1d, 2026-10-18 14:05, HKAP P-005/PJ-2026-001/E1-S5, Feld raumNummer, Stand 3fa91c';
  assert.equal(T.hkapIdAusHerkunft(quelle), 'E1-S5');
  assert.equal(T.hkapIdAusHerkunft(quelle, 'PJ-2026-001'), 'E1-S5');
  assert.equal(T.hkapIdAusHerkunft(quelle, 'PJ-2026-009'), null);
  assert.equal(T.hkapIdAusHerkunft('kicad, T@c1d, 2026-10-18 14:05'), null);
  const lesen = { eigenschaften: { lichteHoehe: { wert: 2500, herkunft: 'kicad, T@c1d, 2026-10-18 14:00' }, nummer: { wert: '1.05', herkunft: quelle } } };
  assert.equal(T.hkapRaumId(raumOffen, lesen, 'PJ-2026-001'), 'E1-S5');
  assert.equal(T.hkapRaumId({ ...raumOffen, kennungen: { hkap: 'PJ-2026-001/E1-S9' } }, null, 'PJ-2026-001'), 'E1-S9', 'Kennung geht vor');
  assert.equal(T.hkapRaumId(raumOffen, { eigenschaften: {} }, null), null);
});

test('Zuordnen: Kandidaten (gleiche Nummer zuerst), Parameter, Vorbelegung des Raum-Werkzeugs', () => {
  const alle = [raumOffen, raumGezeichnet('a', '1.01'), raumGezeichnet('b', 'R-7', { werte: { nummer: '1.05' } }), raumGezeichnet('c', '1.02', { geschoss: 'EG' }), raumGezeichnet('d', '1.03', { kennungen: { hkap: 'PJ/E1-S1' } }), { klasse: 'Wand', id: 'w', kennzeichen: 'W' }];
  const k = T.zuordnenKandidaten(alle, raumOffen);
  assert.deepEqual(k.map((x) => x.raum.kennzeichen), ['R-7', '1.01', '1.02']);
  assert.deepEqual(k.map((x) => [x.gleicheNummer, x.gleichesGeschoss]), [[true, true], [false, true], [false, false]]);
  assert.deepEqual(T.zuordnenParameter('R-7', 'E1-S5'), { raum: 'R-7', hkapRaum: 'E1-S5' });
  assert.deepEqual(T.raumVorbelegung(raumOffen), { nummer: '1.05', nutzung: 'Büro' });
  assert.deepEqual(T.raumVorbelegung({ werte: {} }), { nummer: '', nutzung: '' });
});

test('Platzieren: neu gezeichneter Raum wird erkannt, der Grundriss des Geschosses gefunden', () => {
  const vorher = new Set(['r1', 'a']);
  const jetzt = [raumOffen, raumGezeichnet('a', '1.01'), { id: 'x', klasse: 'Raum', kennzeichen: 'R-9', werte: {} }];
  assert.equal(T.neuGezeichneterRaum(vorher, jetzt), null, 'ein Raum ohne Geometrie zählt nicht');
  jetzt.push(raumGezeichnet('n', 'R-10'));
  assert.equal(T.neuGezeichneterRaum(vorher, jetzt).kennzeichen, 'R-10');
  assert.equal(T.neuGezeichneterRaum(['r1', 'a', 'n'], jetzt), null);
  const ansichten = [
    { klasse: 'Ansicht', kennzeichen: 'AN-3D', geschoss: 'OG1', werte: { art: '3d' } },
    { klasse: 'Ansicht', kennzeichen: 'VL-GR', geschoss: 'OG1', werte: { art: 'grundriss', vorlage: true } },
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-OG1', geschoss: 'OG1', werte: { art: 'grundriss' } },
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-EG', geschoss: 'EG', werte: { art: 'grundriss' } },
  ];
  assert.equal(T.grundrissFuer(ansichten, raumOffen).kennzeichen, 'AN-GR-OG1');
  assert.equal(T.grundrissFuer(ansichten, { ...raumOffen, geschoss: 'UG' }), null);
  assert.equal(T.grundrissFuer(ansichten, { ...raumOffen, geschoss: null }), null);
});

// ---------------------------------------------------------------------------------------------- Stränge (Modul 3, B5)

test('Stränge: Datenart in Reihenfolge, übernehmbar und sendbar; der Dialog ist sonst datenartneutral', () => {
  assert.deepEqual(T.DATENARTEN.map(([n]) => n), ['projekt', 'geschosse', 'raeume', 'straenge', 'pendenzen']);
  assert.equal(T.DATENART_NAMEN.straenge, 'Stränge');
  assert.ok(T.UEBERNEHMBAR.includes('straenge') && T.SENDBAR.includes('straenge'));
  assert.deepEqual(T.zaehlen(straenge), { nachKicad: 3, nachHkap: 2, konflikte: 1, vorschlaege: 1, unveraendert: 0 });
  assert.equal(T.abschnittKopf(straenge), '← 3 · → 2 · 1 Konflikt · 1 Vorschlag');
});

test('Stränge: Block kicad.straenge als Kurztext (Kennzeichen, Systemtyp, Endgeräte, Länge), nicht als JSON', () => {
  assert.equal(T.wertText(strangBlock, 300), 'HZ-RL-1 (Heizung Rücklauf, 1 Endgerät, 2 m) · HZ-VL-1 (Heizung Vorlauf, 2 Endgeräte, 5.45 m)');
  assert.equal(T.strangListeText([{ kennzeichen: 'S-1', systemtyp: 'Zuluft', endgeraete: 0, laengen: [] }]), 'S-1 (Zuluft)');
  assert.equal(T.wertText([{ kennzeichen: 'x' }]), 'kennzeichen: x');   // keine Strangliste: wie jede andere Liste
  const z = T.zeilenVon(straenge);
  const neu = z.find((r) => r.richtung === 'nachHkap' && r.art === 'neu');
  assert.equal(neu.datensatz, 'KW-1, SW-1, WW-1');
  const h = T.aenderungenZeilenHtml(z);
  assert.match(h, /kicad\.straenge/);
  assert.match(h, /HZ-VL-1 \(Heizung Vorlauf, 2 Endgeräte, 5\.45 m\)/);
  assert.ok(!h.includes('&quot;kennzeichen&quot;'));
});

test('Stränge: Abschnitt mit Konflikt, Zuordnungsvorschlag (Knopf mit KICAD-Strängen), nicht platzierten Strängen und Hinweisen', () => {
  const h = T.datenartHtml(straenge, {}, null);
  assert.match(h, /<h3 class="hkap-abschnitt" data-datenart="straenge">Stränge/);
  assert.match(h, /Konflikte \(1\) – beide Seiten haben seit dem letzten Abgleich geändert/);
  assert.match(h, /data-schluessel="straenge\/ST1\/bezeichnung"/);
  assert.match(h, /class="dlg-knopf h-zuordnen" data-hkap="ST5" data-strang="HZ-RL-2, HZ-VL-2"/);
  assert.ok(!/data-raum=/.test(h), 'der Knopf für Stränge trägt kein data-raum');
  assert.match(h, /Nicht platzierte Stränge \(1\):<\/b> L_01/);
  assert.ok(!/Nicht platzierte Räume/.test(h));
  assert.match(h, /Sole Vorlauf/);
  assert.match(h, /wird nicht platzierter Strang im System ZUL/);
  assert.match(h, /3 KICAD-Stränge in einem neuen HKAP-Strang gebündelt/);
});

test('Stränge: Systemwahl je HKAP-Strang (passende Systeme zuerst, weitere einklappbar), Haken aus der Wahl', () => {
  const h = T.systemWahlHtml(straenge.systemWahl, { ST4: ['HZ-VL'] });
  assert.match(h, /Systemwahl für neue Stränge aus HKAP/);
  assert.match(h, /<input type="checkbox" class="s-wahl" data-hkap="ST4" value="HZ-RL"> HZ-RL/);
  assert.match(h, /<input type="checkbox" class="s-wahl" data-hkap="ST4" value="HZ-VL" checked> HZ-VL/);
  assert.match(h, /weitere Systeme \(1\)[\s\S]*value="KW"/);
  assert.match(h, /Heizung Haus · Medium Heizung/);
  assert.equal(T.systemWahlHtml([], {}), '');
  assert.equal(T.systemWahlHtml(undefined, {}), '');
  assert.match(T.datenartHtml(straenge, {}, null, {}), /class="s-wahl"/);
  // keine passenden Systeme: Hinweis, alle Systeme zur freien Wahl
  const keins = T.systemWahlHtml([{ hkapId: 'ST9', medium: 'Kälte', kandidaten: [], systeme: [{ kennzeichen: 'KW', systemtyp: 'Trinkwasser kalt' }] }], {});
  assert.match(keins, /kein passendes System/);
  assert.match(keins, /weitere Systeme \(1\)/);
});

test('Stränge: Systemwahl → Parameter system von hkap_uebernehmen; nur Stränge, die der Abgleich noch zur Wahl stellt', () => {
  const a = { datenarten: [straenge] };
  assert.deepEqual(T.systemWahlIds(a), ['ST4']);
  assert.equal(T.systemWahlParameter({}, a), null);
  assert.equal(T.systemWahlParameter({ ST4: [] }, a), null);
  assert.deepEqual(T.systemWahlParameter({ ST4: ['HZ-VL', 'HZ-RL'] }, a), { ST4: ['HZ-VL', 'HZ-RL'] });
  assert.equal(T.systemWahlParameter({ ST9: ['KW'] }, a), null);
  assert.deepEqual(T.systemWahlBereinigen({ ST4: ['HZ-VL'], ST9: ['KW'] }, a), { ST4: ['HZ-VL'] });
  assert.deepEqual(T.uebernehmenParameter({ systemWahl: { ST4: ['HZ-VL'] }, abgleich: a }), { system: { ST4: ['HZ-VL'] } });
  assert.deepEqual(T.uebernehmenParameter({ datenarten: ['geschosse'], systemWahl: { ST4: ['HZ-VL'] }, abgleich: a }), { datenarten: ['geschosse'] });   // ohne Stränge keine Systemwahl
  assert.deepEqual(T.uebernehmenParameter({ systemWahl: { ST4: ['HZ-VL'] }, abgleich: a, wahl: { 'straenge/ST1/bezeichnung': 'hkap' } }),
    { konflikte: { 'straenge/ST1/bezeichnung': 'hkap' }, system: { ST4: ['HZ-VL'] } });
});

test('Stränge: hkap_strang_zuordnen-Parameter; Texte des Kerns heissen wie die Knöpfe der Oberfläche', () => {
  assert.deepEqual(T.strangZuordnenParameter('HZ-VL-1, HZ-RL-1', 'ST1'), { strang: 'HZ-VL-1, HZ-RL-1', hkap: 'ST1' });
  assert.equal(T.hinweisFuerMenschen('nicht angelegt: KICAD-Strang HZ-VL-2 hat dieselbe Bezeichnung – zuerst hkap_strang_zuordnen'),
    'nicht angelegt: KICAD-Strang HZ-VL-2 hat dieselbe Bezeichnung – zuerst zuordnen (Abgleich, «Zuordnungsvorschläge»)');
  assert.equal(T.hinweisFuerMenschen('HKAP-Strang ST1 nicht angelegt: KICAD-Strang HZ-VL-1 hat dieselbe Bezeichnung – mit hkap_strang_zuordnen zuordnen (oder Namen ändern).'),
    'HKAP-Strang ST1 nicht angelegt: KICAD-Strang HZ-VL-1 hat dieselbe Bezeichnung – im Abgleich unter «Zuordnungsvorschläge» zuordnen (oder Namen ändern).');
  assert.equal(T.hinweisFuerMenschen('HKAP-Strang ST4 nicht angelegt: mehrere passende Systeme (HZ-RL, HZ-VL). Wahl: hkap_uebernehmen mit system { "ST4": "<System>" } (mehrere Systeme: Liste).'),
    'HKAP-Strang ST4 nicht angelegt: mehrere passende Systeme (HZ-RL, HZ-VL). Wahl: im Abgleich unter «Systemwahl» das System ankreuzen.');
  assert.equal(T.hinweisFuerMenschen('Strang L_01 angelegt – oder mit hkap_strang_zuordnen einem gezeichneten Strang zuordnen.'),
    'Strang L_01 angelegt – oder einem gezeichneten Strang zuordnen (Abgleich, «Zuordnungsvorschläge»).');
  assert.equal(T.hinweisFuerMenschen('Nur mit hkap_strang_zuordnen'), 'Nur mit «Zuordnen»');
});

test('Stränge: Maskierung fremder Werte (Bezeichnung und Kennzeichen aus HKAP bzw. KICAD)', () => {
  const frech = { ...straenge, zuordnungsvorschlaege: [{ hkapId: 'ST5', strang: '<b>x</b>', grund: '<i>g</i>' }], nichtPlatziert: ['<u>L</u>'],
    systemWahl: [{ hkapId: 'ST4', medium: '<m>', bezeichnung: '<s>', kandidaten: [], systeme: [{ kennzeichen: '<k>', systemtyp: '<t>' }] }] };
  const h = T.datenartHtml(frech, {}, null, {});
  assert.ok(!/<b>x|<i>g|<u>L|<m>|<s>|<k>|<t>/.test(h));
  assert.ok(h.includes('&lt;b&gt;x&lt;/b&gt;'));
});

// ---------------------------------------------------------------------------------------------- Einbau in die Oberfläche

test('Einbau: Symbole, Register-Gruppe, Importe stehen da', () => {
  const lies = (p) => readFileSync(new URL(p, import.meta.url), 'utf8');
  const symbole = lies('./symbole.js');
  for (const s of ['hkapVerbinden', 'hkapAbgleich', 'hkapBefunde', 'raumOffen']) assert.match(symbole, new RegExp(`\\b${s}:`), s);
  assert.match(lies('./menueband.js'), /verwalten: \[[^\]]*'HKAP'/);
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-hkap\.js'/);
  const aktionen = lies('./aktionen-hkap.js');
  for (const id of ['hkap.verbinden', 'hkap.abgleich', 'hkap.befunde', 'hkap.raum-platzieren', 'hkap.raum-zuordnen']) assert.ok(aktionen.includes(`'${id}'`), id);
  for (const b of ['hkap_verknuepfen', 'hkap_uebernehmen', 'hkap_senden', 'hkap_befunde_senden', 'hkap_raum_zuordnen', 'hkap_strang_zuordnen']) assert.ok(aktionen.includes(`'${b}'`), b);
  for (const a of ['hkap_verbindung', 'hkap_abgleich']) assert.ok(aktionen.includes(`'${a}'`), a);
  assert.match(lies('./projektbrowser.js'), /Nicht platzierte Räume/);
  assert.match(lies('./projektbrowser.js'), /'hkap\.raum-platzieren'/);
  // keine Logik und keine Rechnung in der Oberfläche: der Dialog ruft HKAP nie selbst (kein CORS, Server zu Server)
  assert.ok(!/fetch\(|XMLHttpRequest/.test(aktionen), 'die Oberfläche spricht nur mit KICAD');
});

// ---------------------------------------------------------------------------------------------- gegen einen laufenden Server (optional)

test('Server: hkap_verbindung → Bausteine zeigen Status und Listen', { skip: !process.env.HLKS_TEST_SERVER }, async () => {
  const server = process.env.HLKS_TEST_SERVER;
  const url = process.env.HLKS_HKAP_TEST_URL ?? 'http://localhost:5098';
  const antwort = await fetch(`${server}/api/abfrage/hkap_verbindung`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(T.verbindungParameter(url, null)) });
  assert.equal(antwort.status, 200);
  const v = await antwort.json();
  assert.equal(typeof v.erreichbar, 'boolean');
  const html = T.verbindungHtml(v);
  assert.ok(html.length > 0);
  if (v.erreichbar && !v.meldung) {
    assert.match(html, /hkap-ok/);
    assert.ok(Array.isArray(v.projekte));
    assert.match(T.projekteHtml(v.projekte), /<table/);
  } else assert.match(html, /hkap-kasten/);
});

test('Nur-Lesen-Kasten: Titel nach Datenart (Stränge: Auslegung, Räume: Anforderungen)', () => {
  const liste = [{ element: 'ST-1', hkapId: 'ST1', werte: { tempVL: 50, tempRL: 40 } }];
  assert.match(T.nurLesenHtml(liste, 'straenge'), /Nur zum Lesen: Auslegung der Stränge aus HKAP \(1\)/);
  assert.match(T.nurLesenHtml(liste), /Nur zum Lesen: Anforderungen aus HKAP \(1\)/);
});

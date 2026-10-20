// Listenansicht «Sanitär-Zusammenstellung (W3)» (Modul 4 Teil A-O, entwurf/modul-4.md): macht aus der Antwort der Abfrage sanitaer_zusammenstellung drei Tabellen im Aufbau
// von Christians Excel «LU_DU Zusammenstellung_.xlsx» –
//   1. Apparate: Apparateart · LU KW · LU WW · DU (je Stück) · eine Spalte je Geschoss (Stückzahl) · Anzahl · ΣLU KW · ΣLU WW · ΣDU, darunter die Totalzeile;
//   2. Trinkwasser: Total LU, Volumenstrom, Spitzendurchfluss der beiden Kurven «grösster LU n» (die massgebende fett, die andere klein; n aus dem Katalog über die Antwort,
//      kurvenwahl.kurven[].grosserLu = rechnung.spitzeKurve3/5.grosserLu – nicht fest im Code, Prüfbericht 4A), Spezialinstallationen, Spitzendurchfluss gesamt;
//   3. Entwässerung: ΣDU, Abflusskennzahl K, Q_WW, Spezialinstallationen, Q_tot;
//   4. Hinweise des Kerns (nur wenn vorhanden; Prüfbericht 4A): Herkunft der Kurvenwahl, Volumenstrom ausserhalb des Bereichs einer Kurve, Apparate ohne Netzverbindung
//      bzw. ohne Weg im Netz (P26) in den Stückzahl-Summen u. a. – der Kopf nennt nur ihre Zahl.
// Reine Funktionen (Node-testbar, sanitaer-belastung.test.mjs). Keine Rechnung: alle Zahlen, die Wahl der massgebenden Kurve und die Formeln mit Excel-Zelle kommen vom Kern;
// hier werden sie nur gerundet (Regeln in oberflaeche/sanitaer-text.js) und mit der Formel als Tooltip beschriftet. Die Formeln stehen ausserdem in einer Spalte, die nur die CSV-Datei
// enthält (nurCsv). Fehlt die Abflusskennzahl K, steht «K fehlt» statt einer Zahl (kein Q_WW, kein Q_tot; Befund P25).

import { formatLu, formatDu, formatLs, formatStueck, formatEingabe, genau } from '../oberflaeche/sanitaer-text.js';

export const TITEL = 'Sanitär-Zusammenstellung (W3)';
export const K_FEHLT = 'K fehlt';

const komma = { komma: true };
const fL = (v) => formatLu(v, komma);
const fD = (v) => formatDu(v, komma);
const fS = (v) => formatLs(v, komma);
const fE = (v) => formatEingabe(v, komma);   // von Hand eingetragene Werte der Spezialinstallationen
const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
const istSchluessel = (s) => typeof s === 'string' && s !== '';

/** Tabelle 1 (Apparate): Spalten, Zeilen, Totalzeile. */
function apparateTabelle(d) {
  const geschosse = Array.isArray(d.geschosse) ? d.geschosse : [];
  const spalten = [
    { schluessel: 'art', titel: 'Apparateart', art: 'text' },
    { schluessel: 'luKw', titel: 'LU KW', art: 'zahl', format: fL },
    { schluessel: 'luWw', titel: 'LU WW', art: 'zahl', format: fL },
    { schluessel: 'du', titel: 'DU', art: 'zahl', format: fD },
    ...geschosse.map((g, i) => ({ schluessel: `g${i}`, titel: g.kennzeichen ?? 'ohne Geschoss', art: 'zahl', format: formatStueck,
      tooltip: `Stück im Geschoss ${g.name ?? g.kennzeichen ?? 'ohne Geschoss'}${zahl(g.kote) ? ` (Kote ${g.kote} mm)` : ''}` })),
    { schluessel: 'anzahl', titel: 'Anzahl', art: 'zahl', format: (v) => (zahl(v) ? String(v) : '') },
    { schluessel: 'summeLuKw', titel: 'ΣLU KW', art: 'zahl', format: fL },
    { schluessel: 'summeLuWw', titel: 'ΣLU WW', art: 'zahl', format: fL },
    { schluessel: 'summeDu', titel: 'ΣDU', art: 'zahl', format: fD },
  ];
  const zeilen = (Array.isArray(d.zeilen) ? d.zeilen : []).map((z) => {
    const r = {
      art: z.name ?? z.art, luKw: z.luKw, luWw: z.luWw, du: z.du, anzahl: z.anzahl, summeLuKw: z.summeLuKw, summeLuWw: z.summeLuWw, summeDu: z.summeDu,
      titel: { art: [z.quelle, Array.isArray(z.apparate) && z.apparate.length ? `Apparate: ${z.apparate.join(', ')}` : ''].filter(Boolean).join('\n') },
    };
    geschosse.forEach((_, i) => { r[`g${i}`] = Array.isArray(z.jeGeschoss) ? z.jeGeschoss[i] : null; });
    return r;
  });
  const total = {
    art: 'Total', anzahl: d.apparate, summeLuKw: d.summeLuKw, summeLuWw: d.summeLuWw, summeDu: d.summeDu,
    titel: { '*': `Total aller Apparate der Anlage${d.formeln?.totalLu ? `\nTotal LU: ${d.formeln.totalLu}` : ''}` },
  };
  return { titel: 'Apparate (LU und DU je Stück, Stück je Geschoss, Summen)', schluessel: null, klasse: 'li-san', spalten, zeilen, fuss: [total] };
}

/** Spalten der Blocktabellen Trinkwasser und Entwässerung. */
const BLOCK_SPALTEN = [
  { schluessel: 'groesse', titel: 'Grösse', art: 'text' },
  { schluessel: 'wert', titel: 'Wert', art: 'zahl' },
  { schluessel: 'einheit', titel: 'Einheit', art: 'text' },
  { schluessel: 'formel', titel: 'Formel (Excel-Zelle)', art: 'text', nurCsv: true },
];

/** Eine Zeile der Blocktabelle. wert: Zahl (formatiert mit f) oder null → «K fehlt», wenn fehlt; klasse: 'massgebend' | 'nebenwert' | ''; formel: Text mit Excel-Zelle. */
function blockZeile(groesse, wert, einheit, f, { formel = '', klasse = '', fehlt = false, genauEinheit = einheit } = {}) {
  const zelle = fehlt ? K_FEHLT : zahl(wert) ? f(wert) : '';
  const titel = [formel, zahl(wert) ? `genau: ${genau(wert, genauEinheit)}` : fehlt ? 'Ohne Abflusskennzahl K kein Q_WW und kein Q_tot (Befund P25)' : ''].filter(Boolean).join('\n');
  return {
    groesse, wert: zelle, einheit: fehlt ? '' : einheit, formel, klasse: klasse ? `li-${klasse}` : '',
    zellklassen: fehlt ? { wert: 'li-fehlt' } : {}, titel: { '*': titel },
  };
}

/** Spezialinstallationen als Zeilen: eine je Eintrag (Versorgung bzw. Entsorgung) und die Summe. */
function spezialZeilen(d, welche) {
  const feld = welche === 'versorgung' ? 'versorgung' : 'entsorgung';
  const liste = (Array.isArray(d.spezialinstallationen) ? d.spezialinstallationen : []).filter((s) => zahl(s[feld]) && s[feld] > 0);   // der Kern liefert fehlende Werte als 0: ein Eintrag steht nur im Block, in dem er etwas trägt
  const summe = welche === 'versorgung' ? d.spezialVersorgung : d.spezialEntsorgung;
  const z = liste.map((s) => blockZeile(`Spezialinstallation: ${s.bezeichnung}`, s[feld], 'l/s', fE, { formel: 'Von Hand an der Anlage eingetragen (Eigenschaft spezialinstallationen)' }));
  z.push(blockZeile(`Spezialinstallationen ${welche === 'versorgung' ? 'Versorgung' : 'Entsorgung'} gesamt`, summe ?? 0, 'l/s', fE,
    { formel: `Summe der Spezialinstallationen ${welche === 'versorgung' ? 'Versorgung' : 'Entsorgung'} (Anlage › spezialinstallationen)` }));
  return z;
}

/**
 * «Grösster LU» der beiden Kurven aus dem Katalog (Prüfbericht 4A): Antwort kurvenwahl.kurven[] { schluessel, grosserLu } = Katalog rechnung.spitzeKurve3.grosserLu bzw.
 * rechnung.spitzeKurve5.grosserLu – nicht fest im Code. Fehlt die Angabe, steht «?» (nicht raten).
 */
export function kurvenNummern(d) {
  const kurven = Array.isArray(d?.kurvenwahl?.kurven) ? d.kurvenwahl.kurven : [];
  const lu = (schl) => { const k = kurven.find((x) => x && x.schluessel === schl); return zahl(k?.grosserLu) ? k.grosserLu : null; };
  return { kurve3: lu('spitzeKurve3'), kurve5: lu('spitzeKurve5') };
}

const kurvenName = (n) => `Kurve «grösster LU ${zahl(n) ? n : '?'}»`;

/** Tabelle 2 (Trinkwasser). Kurvennamen aus dem Katalog (kurvenNummern), die massgebende Kurve (Nummer) wählt der Kern. */
function trinkwasserTabelle(d) {
  const f = d.formeln ?? {};
  const kurve = zahl(d.massgebendeKurve) ? d.massgebendeKurve : null;
  const { kurve3: n3, kurve5: n5 } = kurvenNummern(d);
  const k3 = kurve !== null && kurve === n3, k5 = kurve !== null && kurve === n5;
  const wahlBekannt = k3 || k5;   // ohne Nummern bzw. ohne Wahl des Kerns kein «(nicht massgebend)»
  const zusatz = (ja) => (ja ? ' – massgebend' : wahlBekannt ? ' (nicht massgebend)' : '');
  const zeilen = [
    blockZeile('Total LU', d.totalLu, 'LU', fL, { formel: f.totalLu, genauEinheit: 'LU' }),
    blockZeile('Volumenstrom', d.volumenstrom, 'l/s', fS, { formel: f.volumenstrom }),
    blockZeile(`Spitzendurchfluss ${kurvenName(n3)}${zusatz(k3)}`, d.spitzeKurve3, 'l/s', fS, { formel: f.spitzeKurve3, klasse: k3 ? 'massgebend' : 'nebenwert' }),
    blockZeile(`Spitzendurchfluss ${kurvenName(n5)}${zusatz(k5)}`, d.spitzeKurve5, 'l/s', fS, { formel: f.spitzeKurve5, klasse: k5 ? 'massgebend' : 'nebenwert' }),
  ];
  if (zahl(d.groessterLu)) zeilen.push(blockZeile('Grösster Einzel-LU', d.groessterLu, 'LU', fL, { formel: f.massgebendeKurve, klasse: 'nebenwert', genauEinheit: 'LU' }));
  zeilen.push(...spezialZeilen(d, 'versorgung'));
  zeilen.push(blockZeile(`Spitzendurchfluss gesamt (${kurvenName(kurve)} + Spezialinstallationen)`, d.spitzeGesamt, 'l/s', fS, { formel: f.spitzeGesamt, klasse: 'massgebend' }));
  const andere = k5 ? n3 : n5;
  const andereWert = k5 ? d.spitzeGesamtKurve3 : d.spitzeGesamtKurve5;
  if (wahlBekannt && zahl(andereWert)) zeilen.push(blockZeile(`Spitzendurchfluss gesamt ${kurvenName(andere)} (nicht massgebend)`, andereWert, 'l/s', fS, { formel: f.spitzeGesamt, klasse: 'nebenwert' }));
  return { titel: 'Trinkwasser', schluessel: null, klasse: 'li-san li-block', ohneSortieren: true, spalten: BLOCK_SPALTEN, zeilen };
}

/** Tabelle 3 (Entwässerung). Ohne K: «K fehlt» statt K, Q_WW und Q_tot. */
function entwaesserungTabelle(d) {
  const f = d.formeln ?? {};
  const ohneK = !zahl(d.abflusskennzahl);
  const zeilen = [
    blockZeile('ΣDU', d.summeDu, 'DU', fD, { formel: 'Summe der DU aller Apparate (Spalte ΣDU der Tabelle Apparate)', genauEinheit: 'DU' }),
    { ...blockZeile('Abflusskennzahl K', d.abflusskennzahl, '', (v) => String(v).replace('.', ','), { formel: d.abflusskennzahlHerkunft ?? 'Eigenschaft abflusskennzahl der Anlage bzw. der Projektinformation', fehlt: ohneK }) },
    blockZeile('Schmutzwasserabfluss Q_WW', d.schmutzwasser, 'l/s', fS, { formel: f.schmutzwasser, fehlt: ohneK || !zahl(d.schmutzwasser) }),
    ...spezialZeilen(d, 'entsorgung'),
    blockZeile('Gesamtabfluss Q_tot', d.gesamtabfluss, 'l/s', fS, { formel: f.gesamtabfluss, klasse: 'massgebend', fehlt: ohneK || !zahl(d.gesamtabfluss) }),
  ];
  return { titel: 'Entwässerung', schluessel: null, klasse: 'li-san li-block', ohneSortieren: true, spalten: BLOCK_SPALTEN, zeilen };
}

/** Hinweise des Kerns (sanitaer_zusammenstellung.hinweise) als Texte; leere Einträge fallen weg. */
const hinweiseVon = (d) => (Array.isArray(d.hinweise) ? d.hinweise : []).filter((h) => h !== null && h !== undefined && h !== '').map(String);

/**
 * Tabelle 4 (Hinweise des Kerns) unter den Blöcken, nur wenn es welche gibt (Prüfbericht 4A): Kurvenwahl nicht im Excel und nicht bestätigt, Volumenstrom ausserhalb
 * des Bereichs einer Kurve, Stückzahl-Summen mit Apparaten ohne Netzverbindung bzw. ohne Weg im Netz (Befund P26), K fehlt, Apparate ohne Art … – Texte wörtlich vom Kern.
 */
function hinweisTabelle(d) {
  const h = hinweiseVon(d);
  if (!h.length) return null;
  return {
    titel: 'Hinweise', schluessel: null, klasse: 'li-san li-hinweise', ohneSortieren: true,
    spalten: [{ schluessel: 'hinweis', titel: 'Hinweis', art: 'text' }],
    zeilen: h.map((t) => ({ hinweis: t, titel: { '*': t } })),
  };
}

/** Kopfzeilen über den Tabellen: Anlage, Stückzahl, Quelle, Verweis auf die Hinweise unter den Tabellen, «keine Dimensionierung». */
export function zusammenfassung(d) {
  const z = [];
  const q = d.quelle ?? {};
  if (d.anlage) z.push(`Anlage ${d.anlage}${d.name ? ' ' + d.name : ''} · ${d.apparate ?? 0} ${d.apparate === 1 ? 'Apparat' : 'Apparate'}`);
  if (q.dokument) z.push(`Quelle: ${q.dokument}${q.objekt ? ` (${q.objekt}` : ''}${q.stand ? `, Stand ${q.stand}` : ''}${q.objekt ? ')' : ''}`);
  const n = hinweiseVon(d).length;
  if (n) z.push(`${n} ${n === 1 ? 'Hinweis' : 'Hinweise'} des Kerns unter den Tabellen`);
  if (d.hinweis) z.push(String(d.hinweis));
  return z;
}

/** Ergänzt den Kopf der aufbereiteten Liste (listenformat.aufbereiten) um Titel, Zusammenfassung, die drei Tabellen und – falls vorhanden – die Hinweise. */
export function aufbereitenSanitaer(d, kopf) {
  kopf.titel = TITEL;
  kopf.projekt = [d.anlage, d.name].filter(istSchluessel).join(' ');
  kopf.zusammenfassung.push(...zusammenfassung(d));
  kopf.tabellen.push(apparateTabelle(d), trinkwasserTabelle(d), entwaesserungTabelle(d));
  const h = hinweisTabelle(d);
  if (h) kopf.tabellen.push(h);
  return kopf;
}

// Erzeugt die Test-PDF der Normensuche (Modul 3c, Teil K) – selbst geschriebener, ERFUNDENER Text, keine echte Norm.
// Aufbau wie der Normenordner W:\05_Normen und Merkblätter: Unterordner SIA_Normen (deutsche Fassung _d, französische _f, die die Suche
// weglässt) und ein Merkblatt im Hauptordner. Aufruf: node tests/daten/normen/erzeugen.mjs (schreibt neben dieses Skript).
// Die PDF sind schlicht (PDF 1.4, Schrift Helvetica, WinAnsi-Kodierung, eine Textzeile je Zeile), damit PdfPig den Text sicher liest.

import { writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));

// WinAnsi: Latin-1 plus Gedankenstrich und typografische Zeichen
const sonder = { '–': 0x96, '—': 0x97, '‰': 0x89, '’': 0x92, '„': 0x84, '“': 0x93, '”': 0x94 };
function kodieren(text) {
  const bytes = [];
  for (const z of text) {
    let b = sonder[z] ?? z.codePointAt(0);
    if (b > 0xff) b = 0x3f;   // ?
    if (b === 0x28 || b === 0x29 || b === 0x5c) bytes.push(0x5c);   // ( ) \ maskieren
    bytes.push(b);
  }
  return Buffer.from(bytes);
}

function pdf(titel, seiten) {
  const teile = [];
  const offsets = [];
  let laenge = 0;
  const add = (buf) => { teile.push(buf); laenge += buf.length; };
  const obj = (nr, inhalt) => { offsets[nr] = laenge; add(Buffer.concat([Buffer.from(`${nr} 0 obj\n`, 'latin1'), inhalt, Buffer.from('\nendobj\n', 'latin1')])); };
  add(Buffer.from('%PDF-1.4\n%\xe2\xe3\xcf\xd3\n', 'latin1'));
  const n = seiten.length;
  const schrift = 3 + 2 * n, info = schrift + 1;
  obj(1, Buffer.from('<< /Type /Catalog /Pages 2 0 R >>', 'latin1'));
  const kids = seiten.map((_, i) => `${3 + 2 * i} 0 R`).join(' ');
  obj(2, Buffer.from(`<< /Type /Pages /Kids [${kids}] /Count ${n} >>`, 'latin1'));
  seiten.forEach((zeilen, i) => {
    const seite = 3 + 2 * i, inhalt = seite + 1;
    obj(seite, Buffer.from(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 ${schrift} 0 R >> >> /Contents ${inhalt} 0 R >>`, 'latin1'));
    const strom = [Buffer.from('BT\n/F1 11 Tf\n15 TL\n56 780 Td\n', 'latin1')];
    zeilen.forEach((z, k) => {
      if (k > 0) strom.push(Buffer.from('T*\n', 'latin1'));
      strom.push(Buffer.from('(', 'latin1'), kodieren(z), Buffer.from(') Tj\n', 'latin1'));
    });
    strom.push(Buffer.from('ET', 'latin1'));
    const s = Buffer.concat(strom);
    obj(inhalt, Buffer.concat([Buffer.from(`<< /Length ${s.length} >>\nstream\n`, 'latin1'), s, Buffer.from('\nendstream', 'latin1')]));
  });
  obj(schrift, Buffer.from('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>', 'latin1'));
  obj(info, Buffer.concat([Buffer.from('<< /Title (', 'latin1'), kodieren(titel), Buffer.from(') /Producer (HLKS-CAD Testdaten) >>', 'latin1')]));
  const xref = laenge;
  let x = `xref\n0 ${info + 1}\n0000000000 65535 f \n`;
  for (let i = 1; i <= info; i++) x += `${String(offsets[i]).padStart(10, '0')} 00000 n \n`;
  x += `trailer\n<< /Size ${info + 1} /Root 1 0 R /Info ${info} 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  add(Buffer.from(x, 'latin1'));
  return Buffer.concat(teile);
}

// Eine lange Seite für die Grenze des Auszugs (höchstens 600 Zeichen)
const fuell = Array.from({ length: 30 }, (_, i) => `Fülltext ${i + 1}: Dieser Satz ist erfunden und dient nur der Länge der Seite im Test.`);

const deutsch = pdf('Testnorm Prüfleitungen (fiktiv)', [
  ['SIA 9999:2026', 'Bauwesen', 'Testnorm Prüfleitungen', 'für die Normensuche von KICAD', 'Fiktiver Text für Softwaretests – keine echte Norm'],
  ['1 Geltungsbereich', 'Diese Testnorm gilt nur für erfundene Prüfleitungen in Softwaretests.', '2 Begriffe', '2.1 Prüfleitung',
    'Eine Prüfleitung ist eine erfundene Leitung, die nur in Tests vorkommt.'],
  ['4 Bemessung', '4.2 Gefälle', '4.2.1 Mindestgefälle der Prüfleitung',
    'Das Mindestgefälle einer Prüfleitung beträgt in dieser Testnorm 7 Promille.', 'Für Testwerkstoff gilt die erfundene Tabelle 9.', ...fuell],
  ['5 Spülung', 'Nach jedem Testlauf wird die Prüfleitung gespült.'],
]);
const franzoesisch = pdf('Norme fictive (francais)', [
  ['SIA 9999:2026', 'Norme fictive pour les tests', 'Mindestgefälle Prüfleitung: texte français fictif, à ignorer'],
]);
const merkblatt = pdf('Testmerkblatt Prüfleitungen 2025', [
  ['Merkblatt Prüfleitungen 2025 (fiktiv)', 'Die Spülung der Prüfleitung erfolgt mit Testwasser.', 'Dieses Merkblatt ist erfunden.'],
]);

mkdirSync(path.join(hier, 'SIA_Normen'), { recursive: true });
writeFileSync(path.join(hier, 'SIA_Normen', '9999_2026_d.pdf'), deutsch);
writeFileSync(path.join(hier, 'SIA_Normen', '9999_2026_f.pdf'), franzoesisch);
writeFileSync(path.join(hier, 'Testmerkblatt_Pruefleitungen_2025.pdf'), merkblatt);
console.log('Test-PDF geschrieben:', hier);

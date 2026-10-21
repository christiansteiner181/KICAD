// Baut die KICAD Kontrollliste (Artifact) aus berichte/anleitung/inventar.json und der Vorlage.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = dirname(fileURLToPath(import.meta.url));
const repo = 'C:/Users/chris/OneDrive/Claude_Code/HLKS_CAD';
const inv = JSON.parse(readFileSync(join(repo, 'berichte/anleitung/inventar.json'), 'utf8'));

const status = {
  start: 'Grundfunktionen der OberflÃ¤che',
  m0a: 'Abgenommen 17.10.2026', m0b: 'Abgenommen 17.10.2026', m1a: 'Abgenommen 17.10.2026',
  m1b: 'Abgenommen 18.10.2026', m1c: 'Abgenommen 18.10.2026', m1d: 'Abgenommen 18.10.2026',
  m2: 'Gebaut', m3: 'Abgenommen unter Vorbehalt 19.10.2026 Â· NachtrÃ¤ge freie GrÃ¶sse und Abwasser-Serie 21.10.2026',
  m3b: 'Gebaut, Abnahme offen', m3c: 'Gebaut, Abnahmelauf mit echtem Claude bestanden, Abnahme offen',
  m4: 'Teil A gebaut (Teil B wartet auf Normen), Abnahme offen', m7: 'Teil A und B gebaut, Abnahme offen',
  zusatz: 'Zusatzbausteine, u. a. mittlere Maustaste und NavigationswÃ¼rfel (N10/N11, ab nÃ¤chstem Desktop-Stand)',
};

const kapitel = inv.kapitel.map((k) => ({
  id: k.id, titel: k.titel, modul: k.modul, status: status[k.id] || '', einleitung: k.einleitung || '',
  funktionen: (k.funktionen || []).map((f) => ({
    id: f.id, titel: f.titel, wo: f.wo || null, schritte: f.schritte || [], erwartung: f.erwartung || '',
    pruefpunkte: f.pruefpunkte || [], hinweis: f.hinweis || '',
  })),
}));

// Feilarbeiten vom 21.10.2026 (nachbearbeitung.md), soweit umgesetzt
kapitel.push({
  id: 'feil', titel: 'Feilarbeiten 21.10.2026', modul: 'â€“',
  status: 'GeprÃ¼ft (Stand 6072556), nach einem Klick aufs KICAD-Icon auf dem Desktop',
  einleitung: 'Befunde aus deiner PrÃ¼fung vom 21.10.2026 (nachbearbeitung.md N6â€“N8). FÃ¼r N6 und N7 im Projekt Â«Test_praxisÂ» das IFC neu Ã¼bernehmen.',
  funktionen: [
    { id: 'n6-tuerhoehe', titel: 'N6 â€“ TÃ¼rhÃ¶he aus dem TÃ¼rkÃ¶rper (Archicad-IFC)',
      wo: { register: 'EinfÃ¼gen', gruppe: 'IFC', knopf: 'IFC Ã¼bernehmen', kuerzel: '', sonst: 'Projekt Â«Test_praxisÂ»' },
      schritte: ['Â«Test_praxisÂ» Ã¶ffnen.', 'EinfÃ¼gen â€º IFC Ã¼bernehmen: das Architekten-IFC neu Ã¼bernehmen (Abgleich Ã¼ber die GlobalId).', '3D-Ansicht und Grundriss des Attikageschosses ansehen.'],
      erwartung: 'Die 23 TÃ¼ren mit OverallHeight 5,19 m stehen mit 2,15 m HÃ¶he (TÃ¼rkÃ¶rper) und ragen nicht mehr durch Decke und Dach. Ein Importhinweis listet jede TÃ¼r mit Â«2150 statt 5190 mmÂ».',
      pruefpunkte: ['Keine TÃ¼r ragt Ã¼ber die Decke bzw. das Dach.', 'Eigenschaften einer TÃ¼r: HÃ¶he 2150, die Herkunft nennt KÃ¶rper und OverallHeight.', 'Der Importhinweis nennt die betroffenen TÃ¼ren.', 'Die Wandaussparung Ã¼ber der TÃ¼r ist zu (Wand oberhalb 2,15 m sichtbar).'],
      hinweis: 'Regel: Weicht der KÃ¶rper um mehr als 400 mm und mehr als 15 % ab, gilt der KÃ¶rper.' },
    { id: 'n7-unterzuege', titel: 'N7 â€“ Stahl-UnterzÃ¼ge liegen im GebÃ¤ude',
      wo: { register: 'EinfÃ¼gen', gruppe: 'IFC', knopf: 'IFC Ã¼bernehmen', kuerzel: '', sonst: 'Projekt Â«Test_praxisÂ», UZ-14 (1.OG) und UZ-62 (2.OG)' },
      schritte: ['Nach der NeuÃ¼bernahme (siehe N6) die 3D-Ansicht Ã¶ffnen.', 'UZ-14 und UZ-62 anklicken.'],
      erwartung: 'Beide UnterzÃ¼ge liegen innerhalb des GebÃ¤udes, keiner ragt mehr 3â€“4 m hinaus.',
      pruefpunkte: ['Kein TrÃ¤ger ragt aus dem GebÃ¤ude.', 'UZ-14 und UZ-62 liegen unter der Decke Ã¼ber ihrem Geschoss.'],
      hinweis: 'Ursache war ein Rechenfehler beim Schwerpunkt eines lotrechten Profils weit vom Ursprung.' },
    { id: 'n8-schraffur', titel: 'N8 â€“ Schraffur folgt der Wandachse',
      wo: { register: 'Architektur', gruppe: 'Bauteile', knopf: 'Wand', kuerzel: 'WA', sonst: 'Grundriss mit schrÃ¤gen WÃ¤nden' },
      schritte: ['Einen Grundriss mit schrÃ¤gen WÃ¤nden Ã¶ffnen (z. B. Bernstrasse) oder eine Wand unter 30Â° bzw. 45Â° zeichnen.', 'Den Plan als PDF ausgeben.'],
      erwartung: 'Die Schraffur jeder Wand ist an ihrer Achse ausgerichtet, auf dem Bildschirm und im PDF. WÃ¤nde auf derselben Achse haben ein durchlaufendes Muster.',
      pruefpunkte: ['Kreuzschraffur an einer 45Â°-Wand liegt parallel bzw. senkrecht zur Wand.', 'Im PDF gleich wie auf dem Bildschirm.', 'Decken und RÃ¤ume bleiben achsparallel.', 'Im Schnitt unverÃ¤ndert.'],
      hinweis: '' },
  ],
});

const standHtml = `<b>GeprÃ¼fter Stand:</b> <code>6072556</code> (21.10.2026 12:25) â€“ ein Klick aufs KICAD-Icon holt ihn auf den Desktop. Neu darin: deine HKAP-Heizungssymbole, PDF-Leser 3, schnelle Serienkontrolle mit Abzweigwinkel, N6â€“N8 und N10/N11 (mittlere Maustaste, NavigationswÃ¼rfel). Noch in Arbeit: N9 Ausrichtung der Ansichten. ZurÃ¼ckgestellt: N12 IFC Original/Nachbau, N13 3D-Schnittbereich, Modul 5.`;

const daten = { stand: inv.stand, standHtml, kapitel };
const json = JSON.stringify(daten).replace(/</g, '\\u003c');
const vorlage = readFileSync(join(hier, 'vorlage.html'), 'utf8');
const seite = vorlage.replace('__DATEN__', () => json);
writeFileSync(join(hier, 'kicad_kontrollliste.html'), seite, 'utf8');
console.log('Kapitel', kapitel.length, 'Funktionen', kapitel.reduce((s, k) => s + k.funktionen.length, 0), 'Bytes', Buffer.byteLength(seite));

// Baut die KICAD Kontrollliste (Artifact) aus berichte/anleitung/inventar.json und der Vorlage.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = dirname(fileURLToPath(import.meta.url));
const repo = 'C:/Users/chris/OneDrive/Claude_Code/HLKS_CAD';
const inv = JSON.parse(readFileSync(join(repo, 'berichte/anleitung/inventar.json'), 'utf8'));

const status = {
  start: 'Grundfunktionen der Oberfläche',
  m0a: 'Abgenommen 17.10.2026', m0b: 'Abgenommen 17.10.2026', m1a: 'Abgenommen 17.10.2026',
  m1b: 'Abgenommen 18.10.2026', m1c: 'Abgenommen 18.10.2026', m1d: 'Abgenommen 18.10.2026',
  m2: 'Gebaut', m3: 'Abgenommen unter Vorbehalt 19.10.2026 · Nachträge freie Grösse und Abwasser-Serie 21.10.2026',
  m3b: 'Gebaut, Abnahme offen', m3c: 'Gebaut, Abnahmelauf mit echtem Claude bestanden, Abnahme offen',
  m4: 'Teil A gebaut (Teil B wartet auf Normen), Abnahme offen', m7: 'Teil A und B gebaut, Abnahme offen',
  zusatz: 'Zusatzbausteine, u. a. mittlere Maustaste und Navigationswürfel (N10/N11, ab nächstem Desktop-Stand)',
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
  id: 'feil', titel: 'Feilarbeiten 21.10.2026', modul: '–',
  status: 'In main, auf dem Desktop ab dem nächsten geprüften Stand',
  einleitung: 'Befunde aus deiner Prüfung vom 21.10.2026 (nachbearbeitung.md N6–N8). Für N6 und N7 im Projekt «Test_praxis» das IFC neu übernehmen.',
  funktionen: [
    { id: 'n6-tuerhoehe', titel: 'N6 – Türhöhe aus dem Türkörper (Archicad-IFC)',
      wo: { register: 'Einfügen', gruppe: 'IFC', knopf: 'IFC übernehmen', kuerzel: '', sonst: 'Projekt «Test_praxis»' },
      schritte: ['«Test_praxis» öffnen.', 'Einfügen › IFC übernehmen: das Architekten-IFC neu übernehmen (Abgleich über die GlobalId).', '3D-Ansicht und Grundriss des Attikageschosses ansehen.'],
      erwartung: 'Die 23 Türen mit OverallHeight 5,19 m stehen mit 2,15 m Höhe (Türkörper) und ragen nicht mehr durch Decke und Dach. Ein Importhinweis listet jede Tür mit «2150 statt 5190 mm».',
      pruefpunkte: ['Keine Tür ragt über die Decke bzw. das Dach.', 'Eigenschaften einer Tür: Höhe 2150, die Herkunft nennt Körper und OverallHeight.', 'Der Importhinweis nennt die betroffenen Türen.', 'Die Wandaussparung über der Tür ist zu (Wand oberhalb 2,15 m sichtbar).'],
      hinweis: 'Regel: Weicht der Körper um mehr als 400 mm und mehr als 15 % ab, gilt der Körper.' },
    { id: 'n7-unterzuege', titel: 'N7 – Stahl-Unterzüge liegen im Gebäude',
      wo: { register: 'Einfügen', gruppe: 'IFC', knopf: 'IFC übernehmen', kuerzel: '', sonst: 'Projekt «Test_praxis», UZ-14 (1.OG) und UZ-62 (2.OG)' },
      schritte: ['Nach der Neuübernahme (siehe N6) die 3D-Ansicht öffnen.', 'UZ-14 und UZ-62 anklicken.'],
      erwartung: 'Beide Unterzüge liegen innerhalb des Gebäudes, keiner ragt mehr 3–4 m hinaus.',
      pruefpunkte: ['Kein Träger ragt aus dem Gebäude.', 'UZ-14 und UZ-62 liegen unter der Decke über ihrem Geschoss.'],
      hinweis: 'Ursache war ein Rechenfehler beim Schwerpunkt eines lotrechten Profils weit vom Ursprung.' },
    { id: 'n8-schraffur', titel: 'N8 – Schraffur folgt der Wandachse',
      wo: { register: 'Architektur', gruppe: 'Bauteile', knopf: 'Wand', kuerzel: 'WA', sonst: 'Grundriss mit schrägen Wänden' },
      schritte: ['Einen Grundriss mit schrägen Wänden öffnen (z. B. Bernstrasse) oder eine Wand unter 30° bzw. 45° zeichnen.', 'Den Plan als PDF ausgeben.'],
      erwartung: 'Die Schraffur jeder Wand ist an ihrer Achse ausgerichtet, auf dem Bildschirm und im PDF. Wände auf derselben Achse haben ein durchlaufendes Muster.',
      pruefpunkte: ['Kreuzschraffur an einer 45°-Wand liegt parallel bzw. senkrecht zur Wand.', 'Im PDF gleich wie auf dem Bildschirm.', 'Decken und Räume bleiben achsparallel.', 'Im Schnitt unverändert.'],
      hinweis: '' },
  ],
});

const standHtml = `<b>Desktop-Stand:</b> <code>2a4a3db</code> (geprüft 21.10.2026 06:08). In main und in Prüfung, auf dem Desktop ab dem nächsten geprüften Stand: deine HKAP-Heizungssymbole, PDF-Leser 3, schnelle Serienkontrolle mit Abzweigwinkel, N6–N8 und N10/N11 (mittlere Maustaste, Navigationswürfel). Noch in Arbeit: N9 Ausrichtung der Ansichten. Zurückgestellt: N12 IFC Original/Nachbau, N13 3D-Schnittbereich, Modul 5.`;

const daten = { stand: inv.stand, standHtml, kapitel };
const json = JSON.stringify(daten).replace(/</g, '\\u003c');
const vorlage = readFileSync(join(hier, 'vorlage.html'), 'utf8');
const seite = vorlage.replace('__DATEN__', () => json);
writeFileSync(join(hier, 'kicad_kontrollliste.html'), seite, 'utf8');
console.log('Kapitel', kapitel.length, 'Funktionen', kapitel.reduce((s, k) => s + k.funktionen.length, 0), 'Bytes', Buffer.byteLength(seite));

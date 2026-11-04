// Tests zu kontextmenue-logik.js (Bauteilmenü und Werkzeugkiste, 04.11.2026).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { bauteilEintraege, kisteGruppen, radialLagen, ausrollSeite } from './kontextmenue-logik.js';

const aktionen = [
  { id: 'aendern.verschieben', titel: 'Verschieben', register: 'aendern', ausfuehren() {} },
  { id: 'aendern.spiegeln-waehlen', titel: 'Spiegeln – Achse wählen', register: 'aendern', ausfuehren() {} },
  { id: 'kontext.eigenschaften', titel: 'Eigenschaften', register: 'kontext', ausfuehren() {} },
  { id: 'kontext.loeschen', titel: 'Löschen', register: 'kontext', ausfuehren() {} },
  { id: 'kontext.im-schema-zeigen', titel: 'Im Schema zeigen', register: 'kontext', ausfuehren() {} },
  { id: 'kontext.fliessrichtung', titel: 'Fliessrichtung ab hier', register: 'kontext', ausfuehren() {} },
  { id: 'arch.wand', titel: 'Wand', register: 'architektur', symbol: 'wand', kuerzel: 'WA', ausfuehren() {} },
  { id: 'datei.neu', titel: 'Neu', register: 'datei', ausfuehren() {} },
];

test('Bauteilmenü: auch ohne Verbindungen ein Menü; Schema eingebaut statt ersetzt; nicht verfügbare Kontext-Aktionen fehlen; Löschen und Auswahl aufheben am Schluss', () => {
  const gestartet = [];
  const e = bauteilEintraege({
    kennzeichen: 'W-1', aktionen, aktiv: (id) => id !== 'kontext.fliessrichtung', start: (id, p) => gestartet.push([id, p]),
    schema: [{ titel: 'Im Schema zeigen: S-1', aktion() {} }], aufheben: () => gestartet.push(['aufheben']),
  });
  const titel = e.map((x) => (x.trenner ? '|' : x.titel));
  assert.deepEqual(titel, ['Im Schema zeigen: S-1', '|', 'Verschieben', 'Spiegeln', '|', 'Eigenschaften', '|', 'Löschen', 'Auswahl aufheben']);
  e.find((x) => x.titel === 'Eigenschaften').aktion();
  assert.deepEqual(gestartet[0], ['kontext.eigenschaften', { element: 'W-1' }]);
});

test('Werkzeugkiste: Hauptgruppen aus den Registern, im Kreis ab oben, Liste rollt nach aussen', () => {
  const g = kisteGruppen(aktionen);
  assert.deepEqual(g.map((x) => x.register), ['architektur', 'aendern']);   // Register ohne Werkzeuge fallen weg, «datei» gehört nicht dazu
  assert.deepEqual(g[0].werkzeuge[0], { id: 'arch.wand', titel: 'Wand', symbol: 'wand', kuerzel: 'WA' });
  const l = radialLagen(4, 100);
  assert.deepEqual([l[0].x, l[0].y], [0, -100]);
  assert.deepEqual([l[1].x, l[1].y], [100, 0]);
  assert.equal(ausrollSeite(l[3]).seite, 'links');
  assert.equal(ausrollSeite(l[1]).seite, 'rechts');
  assert.equal(ausrollSeite(l[2]).unten, true);
});

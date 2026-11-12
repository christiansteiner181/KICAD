// Gesamtprüfung 13.11.2026: Ansicht duplizieren (Schnittlinie geht mit), neue Ansicht aus Vorlage, Gewerkefilter in 3D (filter.js).
import test from 'node:test';
import assert from 'node:assert/strict';
import { duplikatParameter, vorlageEigenschaften } from './ansicht-logik.js';
import { sichtbar } from '../ansichten/filter.js';

test('Schnitt duplizieren: die Kopie hat die Schnittlinie, eine Vorlage nicht', () => {
  const schnitt = { kennzeichen: 'AN-S1', geometrie: { art: 'linie', punkte: [[0, 0], [5000, 0]] }, werte: { art: 'schnitt', blick: 'links', tiefe: 3000 } };
  const p = duplikatParameter(schnitt, 'AN-S1-2', 'Schnitt 1 – Kopie');
  assert.deepEqual(p.geometrie, schnitt.geometrie);
  assert.notEqual(p.geometrie, schnitt.geometrie);          // eigene Kopie, nicht dasselbe Objekt
  assert.equal(p.eigenschaften.tiefe, 3000);
  assert.equal(p.geschoss, undefined);
  const grundriss = duplikatParameter({ kennzeichen: 'AN-GR-EG', geschoss: 'EG', werte: { art: 'grundriss' } }, 'AN-GR-EG-2', 'x');
  assert.equal(grundriss.geschoss, 'EG');
  assert.equal('geometrie' in grundriss, false);
  assert.equal('geometrie' in duplikatParameter({ ...schnitt, werte: { ...schnitt.werte, vorlage: true } }, 'VL-2', 'x'), false);
});

test('Neue Ansicht aus Vorlage: Schnitthöhe nur im Grundriss, ausgeblendete Klassen und IFC-Darstellung gehen mit', () => {
  const v = { kennzeichen: 'VL-1', werte: { art: 'grundriss', schnitthoehe: 1200, schnitthoeheNachSia: false, klassenAusblenden: ['Moebel'], ifcDarstellung: 'original' } };
  assert.deepEqual(vorlageEigenschaften(v, 'grundriss'),
    { ansichtsvorlage: 'VL-1', schnitthoehe: 1200, schnitthoeheNachSia: false, klassenAusblenden: ['Moebel'], ifcDarstellung: 'original' });
  assert.deepEqual(vorlageEigenschaften(v, '3d'), { ansichtsvorlage: 'VL-1', klassenAusblenden: ['Moebel'], ifcDarstellung: 'original' });
  assert.deepEqual(vorlageEigenschaften({ kennzeichen: 'VL-2' }, 'grundriss'), { ansichtsvorlage: 'VL-2' });
});

test('Gewerkefilter in 3D: alle Gewerke des Elements zählen (wie Grafik.Sichtbar im Kern)', () => {
  const ansicht = { werte: { art: '3d', gewerke: ['sanitaer', 'architektur'] } };
  const el = (x) => ({ klasse: 'Rohr', schicht: 'system', geometrie: {}, ...x });
  assert.equal(sichtbar(el({ gewerk: 'heizung', gewerke: ['heizung'] }), ansicht), false);
  assert.equal(sichtbar(el({ gewerk: 'sanitaer', gewerke: ['sanitaer'] }), ansicht), true);
  // Wassererwärmer zwischen Heizung und Sanitär: das eine abgeleitete Gewerk (erstes System) wäre «heizung» – er gehört aber zu beiden
  assert.equal(sichtbar(el({ klasse: 'Wassererwaermer', gewerk: 'heizung', gewerke: ['sanitaer', 'heizung'] }), ansicht), true);
  assert.equal(sichtbar(el({ gewerke: [] }), ansicht), true);                       // kein Gewerk bekannt
  assert.equal(sichtbar(el({ klasse: 'Aussparung', gewerke: ['allgemein'] }), ansicht), true);
  assert.equal(sichtbar(el({ gewerk: 'heizung' }), ansicht), false);                 // ältere Daten ohne «gewerke»
  assert.equal(sichtbar(el({ gewerk: 'heizung', gewerke: ['heizung'] }), { werte: { art: '3d' } }), true);   // ohne Filter alles
});

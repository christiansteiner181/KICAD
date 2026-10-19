// Darstellung «Hülle» (Modul 1c, Auftrag C1b): reine Funktionen ohne DOM und ohne three – Farben, Legende, Infotext.
// Die Farben sind der Spiegel von HuelleGrafik.cs im Kern (Annahme Farbe, frei gewählt, keine Normvorgabe); der Node-Test
// huelle-darstellung.test.mjs vergleicht beide. Grundriss (grundriss.js) und 3D (ansicht3d.js, huelle3d.js) benutzen dieselben Farben.
// Die Zahlen kommen aus der Abfrage «raumgrenzen» des Kerns (summen.jeAngrenzung bzw. zeilen); hier wird nichts gerechnet.

import { esc } from './hilfen.js';

/** Angrenzungen in der Reihenfolge des Kerns (Raumgrenzen.Angrenzungen). */
export const ANGRENZUNGEN = Object.freeze(['aussen', 'erdreich', 'unbeheizt', 'beheizt']);

/** Annahme Farbe: wie HuelleGrafik.FarbeAussen … FarbeBeheizt. */
export const HUELLE_FARBEN = Object.freeze({ aussen: '#e0482f', erdreich: '#8a6a3b', unbeheizt: '#3f7fd1', beheizt: '#5aa469' });

/** Anzeigenamen. */
export const HUELLE_NAMEN = Object.freeze({ aussen: 'aussen', erdreich: 'Erdreich', unbeheizt: 'unbeheizt', beheizt: 'beheizt' });

/** Deckkraft der Flächen im 3D (Annahme: beheizt nur als Hauch, damit die Hülle auffällt). */
export const HUELLE_DECKKRAFT_3D = Object.freeze({ aussen: 0.55, erdreich: 0.55, unbeheizt: 0.55, beheizt: 0.1 });

/** Soll die Ansicht die Hülle zeigen? (Ansichtseigenschaft darstellung = huelle; Grundriss und 3D) */
export const istHuelle = (ansicht) => ansicht?.werte?.darstellung === 'huelle' && ['grundriss', '3d'].includes(ansicht?.werte?.art ?? 'grundriss');

/** m² mit einer Nachkommastelle und Hochkomma als Tausendertrenner (Schweiz). */
export function formatM2(wert) {
  const n = Number(wert);
  if (!Number.isFinite(n)) return '–';
  const [ganz, rest] = Math.abs(n).toFixed(1).split('.');
  return `${n < 0 ? '−' : ''}${ganz.replace(/\B(?=(\d{3})+(?!\d))/g, '’')}.${rest}`;
}

/** Zeilen der Legende aus der Antwort der Abfrage raumgrenzen: immer alle vier Angrenzungen, fehlende mit 0. */
export function legendeDaten(antwort) {
  const je = antwort?.summen?.jeAngrenzung ?? {};
  return ANGRENZUNGEN.map((a) => ({
    angrenzung: a, name: HUELLE_NAMEN[a], farbe: HUELLE_FARBEN[a],
    flaeche: Number(je[a]?.flaeche_m2) || 0, ohneU: Number(je[a]?.ohneU_m2) || 0,
  }));
}

/** HTML der Legende: Titel, je Angrenzung Farbfeld, Name und Fläche (m², Innenmass der Raumgrenzen), Fusszeile. */
export function legendeHtml(daten, { titel = 'Hülle', fehler = null } = {}) {
  if (fehler) return `<div class="hl-titel">${esc(titel)}</div><div class="hl-fehler">${esc(fehler)}</div>`;
  const zeilen = (daten ?? []).map((d) =>
    `<div class="hl-zeile" data-angrenzung="${esc(d.angrenzung)}"><span class="hl-feld" style="background:${esc(d.farbe)}"></span><span class="hl-name">${esc(d.name)}</span><span class="hl-flaeche">${formatM2(d.flaeche)} m²</span></div>`).join('');
  return `<div class="hl-titel">${esc(titel)}</div>${zeilen}<div class="hl-fuss">Raumgrenzen, Innenmass; beheizt je Raumseite</div>`;
}

const ART_NAMEN = { wand: 'Wand', fenster: 'Fenster', tuer: 'Tür', boden: 'Boden', decke: 'Decke', dach: 'Dach' };

/** Infotext einer Raumgrenze (Zeile der Abfrage raumgrenzen) als Liste von Zeilen { name, wert }. */
export function infoZeilen(z) {
  if (!z) return [];
  const aus = [
    { name: 'Raum', wert: z.raum ?? '–' },
    { name: 'Nachbar', wert: z.nachbar ?? '–' },
    { name: ART_NAMEN[z.art] ?? 'Bauteil', wert: z.bauteil ?? (z.offen ? 'offen (kein Bauteil)' : 'kein Bauteil') },
    { name: 'Angrenzung', wert: HUELLE_NAMEN[z.angrenzung] ?? z.angrenzung ?? '–' },
    { name: 'Fläche', wert: `${Number.isFinite(z.flaeche_m2) ? z.flaeche_m2.toFixed(2) : '–'} m²` },
  ];
  if (z.aufbau) aus.push({ name: 'Aufbau', wert: z.aufbau });
  if (Number.isFinite(z.u)) aus.push({ name: 'U', wert: `${z.u} W/(m²·K)` });
  return aus;
}

/** HTML des Infofelds im 3D. */
export function infoHtml(z) {
  const f = infoZeilen(z);
  if (f.length === 0) return '';
  const farbe = HUELLE_FARBEN[z.angrenzung];
  return `<div class="hl-titel">${farbe ? `<span class="hl-feld" style="background:${esc(farbe)}"></span>` : ''}Raumgrenze</div>`
    + f.map((x) => `<div class="hl-info"><span class="hl-name">${esc(x.name)}</span><span>${esc(x.wert)}</span></div>`).join('');
}

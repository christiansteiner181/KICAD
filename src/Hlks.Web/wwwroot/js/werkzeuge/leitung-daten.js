// Daten für das Zeichnen von Leitungen (Modul 3, C1): Leitungsserien (Abfrage leitungsserie), Anschlusslagen von Elementen (Abfrage
// anschlusslagen) und die Haken zwischen Optionsleiste und laufendem Werkzeug. Alles wird gelesen, nie berechnet; die Zwischenspeicher
// leeren sich bei Änderungen am Modell (Anschlüsse: bei jeder, Serien: wenn sich eine Familie ändert). Die Optionsleiste zeichnet sich
// neu, sobald eine Serie angekommen ist.

import { abfrage } from '../api.js';
import { abonnieren, element, alleElemente, klasse, zustand } from '../zustand.js';
import { familienListe } from '../familien/daten.js';
import { anschluesseAus, serienDerKategorie } from './leitung-logik.js';

/** Haken vom laufenden Werkzeug (leitung.js; hier keine Abhängigkeit von der Oberfläche, damit Node-Tests laden können):
 *  hoeheGeaendert() – in der Optionsleiste wurde die Höhe geändert (Höhenwechsel), optionenNeu() – Optionsleiste neu zeichnen. */
export const haken = { hoeheGeaendert: null, optionenNeu: null };

// ------------------------------------------------------------------------------------------------ Serien

const serien = new Map();       // Kennzeichen → { def, laedt }

export const serienListe = (kategorie) => serienDerKategorie(familienListe(), kategorie);

/** Antwort der Abfrage leitungsserie aus dem Zwischenspeicher (oder null, solange sie nicht da ist; stösst das Laden an). */
export function serieDef(kz) {
  if (!kz) return null;
  const e = serien.get(kz);
  if (e?.def) return e.def;
  if (!e) serieLaden(kz);
  return null;
}

export function serieLaden(kz) {
  if (!kz) return Promise.resolve(null);
  const e = serien.get(kz);
  if (e?.laedt) return e.laedt;
  if (e?.def) return Promise.resolve(e.def);
  const eintrag = { def: null, laedt: null };
  serien.set(kz, eintrag);
  eintrag.laedt = abfrage('leitungsserie', { familie: kz })
    .then((d) => { eintrag.def = d; eintrag.laedt = null; optionenNeu(); return d; })
    .catch((err) => { console.warn('Leitungsserie', kz, err?.message ?? err); serien.delete(kz); return null; });
  return eintrag.laedt;
}

const optionenNeu = () => haken.optionenNeu?.();

// ------------------------------------------------------------------------------------------------ Anschlusslagen

const anschluesse = new Map();  // Element-Id → { laedt, liste: [Anschluss] | null }

/** Hat das Element Anschlüsse, die man fangen könnte? (Katalogklasse mit Anschlüssen oder Familienelement) */
export function hatAnschluesse(el) {
  if (!el || !el.geometrie?.punkte?.length) return false;
  if (el.bezuege?.familie || el.rolle === 'leitung') return true;
  return (klasse(el.klasse)?.anschluesse ?? []).length > 0;
}

/** Zwischengespeicherte Anschlüsse eines Elements (Liste, auch leer) oder null (noch nicht geladen). */
export const anschluesseVon = (id) => anschluesse.get(id)?.liste ?? null;

/** Alle zwischengespeicherten Anschlüsse. */
export function alleAnschluesse() {
  const l = [];
  for (const e of anschluesse.values()) if (e.liste) l.push(...e.liste);
  return l;
}

/** Lädt die Anschlusslagen der Elemente (nur, was noch fehlt); ruft `fertig`, wenn etwas Neues angekommen ist. */
export function anschluesseLaden(elemente, fertig) {
  const fehlend = elemente.filter((el) => !anschluesse.has(el.id));
  if (!fehlend.length) return;
  let rest = fehlend.length;
  for (const el of fehlend) {
    const eintrag = { liste: null, laedt: true };
    anschluesse.set(el.id, eintrag);
    abfrage('anschlusslagen', { element: el.kennzeichen })
      .then((a) => { eintrag.liste = anschluesseAus(a); })
      .catch(() => { eintrag.liste = []; })
      .finally(() => { eintrag.laedt = false; if (--rest === 0) fertig?.(); });
  }
}

/** Elemente des Geschosses, deren Rahmen (alle Punkte) mit Rand um p liegt und die Anschlüsse haben könnten. */
export function elementeNahe(p, rand, geschossKz) {
  const g = element(geschossKz);
  const l = [];
  for (const el of alleElemente()) {
    if (el.schicht !== 'system' && !el.bezuege?.familie) continue;
    if (geschossKz && el.geschoss && g && element(el.geschoss)?.id !== g.id) {
      // Leitungen können über Geschosse reichen; das Geschoss des Elements gilt trotzdem für die Ansicht
      continue;
    }
    if (!hatAnschluesse(el)) continue;
    const pts = el.geometrie.punkte;
    let minx = Infinity, miny = Infinity, maxx = -Infinity, maxy = -Infinity;
    for (const q of pts) { minx = Math.min(minx, q[0]); maxx = Math.max(maxx, q[0]); miny = Math.min(miny, q[1]); maxy = Math.max(maxy, q[1]); }
    if (p[0] >= minx - rand && p[0] <= maxx + rand && p[1] >= miny - rand && p[1] <= maxy + rand) l.push(el);
  }
  return l;
}

abonnieren('modell', (d) => {
  anschluesse.clear();
  if (d?.alles || (d?.geloescht ?? []).length || (d?.ids ?? []).some((id) => element(id)?.klasse === 'Familie')) serien.clear();
});
abonnieren('status', () => { if (!zustand.status?.offen) { anschluesse.clear(); serien.clear(); } });

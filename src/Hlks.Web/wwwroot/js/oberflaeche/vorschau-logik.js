// Reine Entscheidungen und Farben für das 3D-Vorschaubild eines Projekts (S1, entwurf/startbildschirm.md): wann ein neues Bild
// fällig ist und wie die Teile eingefärbt werden. Kein Browser, kein three – in Node getestet (start.test.mjs).
//
// Bild: 640 × 400, ganzes Projekt, feste Isometrie von Südwesten, Hintergrund durchsichtig (passt in hell und dunkel),
// Architektur grau, Gebäudetechnik in den Systemfarben des Kerns (Teil.farbe).

export const BILD_BREITE = 640;
export const BILD_HOEHE = 400;
/** Höchstens alle 10 Minuten ein neues Bild, und nur bei geändertem Stand. */
export const MIN_ABSTAND_MS = 10 * 60 * 1000;
/** Blickrichtung zur Mitte des Projekts in three-Koordinaten (x Ost, y oben, z = −Nord): von Südwesten (−x, +z) schräg oben. */
export const BLICK_VON = Object.freeze([-1, 0.82, 1]);

const HLKS_EBENEN = new Set(['leitung', 'formteil', 'armatur', 'apparat']);
const HLKS_MATERIAL = new Set(['rohr', 'leitung', 'formteil', 'armatur', 'apparat', 'geraet']);
const hex = (f) => (typeof f === 'string' && /^#[0-9a-fA-F]{6}$/.test(f) ? f : null);

/** Gehört das Teil zur Gebäudetechnik? (Ebene des Körpers oder Material des Teils.) */
export const istGebaeudetechnik = (ebene, teil) => HLKS_EBENEN.has(ebene) || HLKS_MATERIAL.has(teil?.material);

const ARCHITEKTUR = {
  wand: { farbe: '#b8bec7', deckkraft: 0.4 },
  decke: { farbe: '#9aa3af', deckkraft: 0.5 },
  oeffnung: { farbe: '#8f96a0', deckkraft: 0.8 },
  sonst: { farbe: '#a9afb8', deckkraft: 0.7 },
};
/** Farbe von Gebäudetechnik ohne Systemfarbe aus dem Kern. */
export const SYSTEM_VORGABE = '#4f8fd0';

/** Räume und Hintergrund-Netze (IFC-Unterlagen) kommen nicht ins Bild. */
export function teilZeigen(ebene, teil) {
  if (ebene === 'raum') return false;
  if (teil?.art === 'netz') return false;
  return true;
}

/** Farbe, Deckkraft und Kanten eines Teils im Vorschaubild. */
export function teilStil(ebene, teil) {
  if (istGebaeudetechnik(ebene, teil)) return { farbe: hex(teil?.farbe) ?? SYSTEM_VORGABE, deckkraft: 1, kanten: false };
  const a = ARCHITEKTUR[ebene] ?? ARCHITEKTUR.sonst;
  return { farbe: a.farbe, deckkraft: a.deckkraft, kanten: true };
}

/**
 * Ist ein neues Bild fällig?
 * gemerkt: { stand, zeit } des letzten Versuchs für dieses Projekt (oder null); stand: Prüfsumme jetzt; jetzt: ms.
 * Ergebnis: { aktion: 'jetzt' | 'warten' | 'nichts', wartenMs }.
 *  - nichts: derselbe Stand wie beim letzten Bild
 *  - warten: Stand geändert, aber das letzte Bild ist jünger als der Mindestabstand
 *  - jetzt: noch nie ein Bild oder Stand geändert und Abstand eingehalten
 */
export function vorschauEntscheid({ gemerkt, stand, jetzt, abstandMs = MIN_ABSTAND_MS }) {
  if (!stand) return { aktion: 'nichts', wartenMs: 0 };
  if (!gemerkt) return { aktion: 'jetzt', wartenMs: 0 };
  if (gemerkt.stand === stand) return { aktion: 'nichts', wartenMs: 0 };
  const vergangen = jetzt - (gemerkt.zeit ?? 0);
  if (vergangen < abstandMs) return { aktion: 'warten', wartenMs: abstandMs - vergangen };
  return { aktion: 'jetzt', wartenMs: 0 };
}

/** Adresse zum Speichern des Bilds. */
export const vorschauSpeicherUrl = (stand, ort) => `/api/projekt/vorschau?stand=${encodeURIComponent(stand ?? '')}&ort=${encodeURIComponent(ort ?? '')}`;

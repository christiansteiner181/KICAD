// Elementtypen der Geberit Vorwand für die Auswahl im Werkzeug «Montageelement» (aus der Abfrage vorwand_stueckliste, einmal geladen).
import { abfrage } from '../api.js';

let typen = [];

/** Lädt die Elementtypen (einmal je Sitzung; Fehler lassen die Liste leer). */
export async function elementtypenLaden() {
  if (typen.length) return typen;
  try { typen = (await abfrage('vorwand_stueckliste', {}))?.elementtypen ?? []; } catch { typen = []; }
  return typen;
}

/** Breite des Elementtyps in mm (für die Vorschau); 500 wie die Vorgabe des Kerns, solange der Typ unbekannt ist. */
export const elementBreite = (id) => typen.find((t) => t.id === id)?.breite ?? 500;

/** Auswahlwerte [[id, Titel (Artikel)], …]. */
export const elementWerte = () => typen.map((t) => [t.id, `${t.titel}${t.artikel ? ` (${t.artikel})` : ''}`]);

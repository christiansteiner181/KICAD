// Lagen je Geschoss für Optionsleiste und Lagemanager (Christian 05.11.2026), ohne DOM: Abfrage lagen (Kern Leitungen/Lagen.cs), einmal geladen.

import { abfrage } from '../api.js';
import { element } from '../zustand.js';

let cache = null;
let laden = null;

/** Lagen aller Geschosse (einmal geladen; neu = nach dem Speichern im Lagemanager). */
export function lagenLaden(neu = false) {
  if (neu) { cache = null; laden = null; }
  laden ??= abfrage('lagen', {}).then((d) => { cache = d?.geschosse ?? []; return cache; }).catch(() => { cache = []; return cache; });
  return laden;
}

/** Lagen des Geschosses (Kennzeichen oder Id): [{ nr, abstand, hoehe }] – leer, solange nicht geladen oder ohne UK roh Decke. */
export function lagenFuer(geschoss) {
  if (!cache) { lagenLaden(); return []; }
  const g = element(geschoss);
  const e = cache.find((x) => x.geschoss === (g?.kennzeichen ?? geschoss));
  return (e?.lagen ?? []).filter((l) => Number.isFinite(l.hoehe));
}

/** UK roh Decke des Geschosses in mm über der Kote (Lagemanager bzw. Modell) oder null – für die Lüftung (Christian 09.11.2026: «Lüftungen beziehen
 *  sich nicht auf lagen. diese werden für Rohre immer als Achse = -xy UKD und bei kanälen immer OK = -xy UKD angegeben und gezeichnet.»). */
export function ukdFuer(geschoss) {
  if (!cache) { lagenLaden(); return null; }
  const g = element(geschoss);
  const e = cache.find((x) => x.geschoss === (g?.kennzeichen ?? geschoss));
  return Number.isFinite(e?.ukRohdecke) ? e.ukRohdecke : null;
}

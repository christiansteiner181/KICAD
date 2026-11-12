// Lagen je Geschoss für Optionsleiste und Lagemanager (Christian 05.11.2026), ohne DOM: Abfrage lagen (Kern Leitungen/Lagen.cs), einmal geladen.
// Der Stand folgt dem Modell (Gesamtprüfung 13.11.2026): Wird ein anderes Projekt geladen, ist er leer (vorher blieben die Lagen des letzten
// Projekts stehen und passten über das Geschoss-Kennzeichen «EG» auf das neue). Ändert sich ein Geschoss, das Gebäude oder eine Decke
// (Kote, UK roh Decke, Lagen, Decke aus dem IFC) oder wird etwas gelöscht, holt der nächste Zugriff die Lagen neu; bis sie da sind, gilt der alte Stand.

import { abfrage } from '../api.js';
import { abonnieren, element } from '../zustand.js';

let cache = null;
let laden = null;
let veraltet = false;
let lauf = 0;   // ein Laden, das von einem neueren überholt wurde, schreibt nicht mehr

/** Lagen aller Geschosse (einmal geladen; neu = nach dem Speichern im Lagemanager bzw. nach einer Änderung am Modell). */
export function lagenLaden(neu = false) {
  if (neu || veraltet) laden = null;
  if (!laden) {
    const mein = ++lauf;
    veraltet = false;
    laden = abfrage('lagen', {})
      .then((d) => d?.geschosse ?? [], () => [])
      .then((g) => { if (mein === lauf) cache = g; return g; });
  }
  return laden;
}

const KLASSEN = new Set(['Geschoss', 'Gebaeude', 'Decke']);

abonnieren('modell', (d) => {
  if (d?.alles) { cache = null; laden = null; veraltet = false; lauf++; return; }
  if (!cache && !laden) return;
  if ((d?.geloescht ?? []).length || (d?.ids ?? []).some((id) => KLASSEN.has(element(id)?.klasse))) veraltet = true;
});

/** Eintrag des Geschosses (Kennzeichen oder Id) aus dem geladenen Stand; stösst das Laden an, wenn er fehlt oder veraltet ist. */
function eintrag(geschoss) {
  if (!cache || veraltet) lagenLaden(veraltet);
  if (!cache) return null;
  const g = element(geschoss);
  return cache.find((x) => x.geschoss === (g?.kennzeichen ?? geschoss)) ?? null;
}

/** Lagen des Geschosses (Kennzeichen oder Id): [{ nr, abstand, hoehe }] – leer, solange nicht geladen oder ohne UK roh Decke. */
export function lagenFuer(geschoss) {
  return (eintrag(geschoss)?.lagen ?? []).filter((l) => Number.isFinite(l.hoehe));
}

/** UK roh Decke des Geschosses in mm über der Kote (Lagemanager bzw. Modell) oder null – für die Lüftung (Christian 09.11.2026: «Lüftungen beziehen
 *  sich nicht auf lagen. diese werden für Rohre immer als Achse = -xy UKD und bei kanälen immer OK = -xy UKD angegeben und gezeichnet.»). */
export function ukdFuer(geschoss) {
  const e = eintrag(geschoss);
  return Number.isFinite(e?.ukRohdecke) ? e.ukRohdecke : null;
}

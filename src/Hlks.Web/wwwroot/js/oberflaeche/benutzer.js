// Benutzereinstellungen der Oberfläche (G1): lesen beim Start, schreiben beim Wechsel – über /api/benutzer/einstellungen (Server: BenutzerEinstellungen.cs).
// Die Einstellungen gelten für den Benutzer überall (alle Projekte, alle seine Rechner); der Server vereinigt die Dateien der Rechner (der neueste Wert gilt).
// Hier steht nur der Zwischenspeicher der letzten Antwort – keine Logik.

import { holen, senden } from '../api.js';

let werte = {};
let geladen = false;
let zaehler = 0;        // jeder Lauf und jedes Schreiben zählt: eine ältere Antwort überholt nie ein neueres Schreiben
let letztesSchreiben = 0;

/** Holt die Einstellungen vom Server. Ein Fehler (Server alt, Ablage weg) ergibt leere Einstellungen: die Vorgaben der Oberfläche gelten. */
export async function benutzerLaden() {
  const mein = ++zaehler;
  let neu = {};
  try { neu = (await holen('/api/benutzer/einstellungen')).werte ?? {}; } catch { neu = {}; }
  if (mein < letztesSchreiben) return werte;   // inzwischen hat der Benutzer etwas geändert: seine Wahl gilt
  werte = neu;
  geladen = true;
  return werte;
}

export const benutzerGeladen = () => geladen;
/** Alle Einstellungen als Objekt (Kopie), z. B. für die Profile des XR-Labors (Schlüssel «labor.profil.<kennung>»). */
export const benutzerAlle = () => ({ ...werte });
export const benutzerWert = (schluessel, vorgabe) => (schluessel in werte && werte[schluessel] !== null ? werte[schluessel] : vorgabe);

/** Schreibt eine Einstellung (wert null setzt sie zurück). Wirft ApiFehler mit Meldung und Vorschlag, wenn der Server ablehnt. */
export async function benutzerSetzen(schluessel, wert) {
  letztesSchreiben = ++zaehler;
  const alt = werte;
  werte = { ...werte };
  if (wert === null) delete werte[schluessel]; else werte[schluessel] = wert;
  try { return await senden('/api/benutzer/einstellungen', { schluessel, wert }); }
  catch (e) { werte = alt; throw e; }
}

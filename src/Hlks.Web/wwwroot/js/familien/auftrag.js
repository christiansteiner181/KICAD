// KI-Aufträge «Familie aus Unterlagen» (Modul 2, C2): Zugang zu /api/auftraege und der Stand der Aufträge, nachgeführt aus dem Ereignis «auftrag»
// des Ereignisstroms (ereignisse.js gibt es als Art «auftrag» an zustand.js weiter). Die Oberfläche startet, zeigt und bricht ab – der Lauf selbst,
// die Werkzeuge der KI und das Zeitlimit liegen im Server (AuftragDienst). Reine Teile (Stand zusammenführen, Texte): unterlagen-text.js.

import { holen, senden } from '../api.js';
import { abonnieren } from '../zustand.js';
import { auftragUebernehmen, aktuellerAuftrag, auftragLaeuft } from './unterlagen-text.js';

let stand = new Map();        // id → Auftrag (letzter bekannter Stand)
const hoerer = new Set();
let abgemeldet = null;

function melden(auftrag, ereignisArt) {
  for (const fn of hoerer) { try { fn(auftrag, ereignisArt); } catch (e) { console.error(e); } }
}

/** Ereignis «auftrag» (oder eine Antwort des Servers) einarbeiten. Gibt den Stand des Auftrags zurück. */
export function auftragEinarbeiten(auftrag, ereignisArt = 'ereignis') {
  if (!auftrag?.id) return null;
  const vorher = stand.get(auftrag.id);
  stand = auftragUebernehmen(stand, auftrag);
  const jetzt = stand.get(auftrag.id);
  if (jetzt !== vorher) melden(jetzt, ereignisArt);
  return jetzt;
}

/** Hörer für jeden neuen Stand eines Auftrags: fn(auftrag). Gibt die Abmeldung zurück. */
export function beiAuftrag(fn) { hoerer.add(fn); return () => hoerer.delete(fn); }

export const auftragStand = () => stand;
export const laufenderAuftrag = () => [...stand.values()].find(auftragLaeuft) ?? null;
export const letzterAuftrag = () => aktuellerAuftrag(stand);
export function auftraegeLeeren() { stand = new Map(); }

/** Ereignis «auftrag» abonnieren (einmal beim Laden der Oberfläche). */
export function auftragAbonnieren() {
  if (abgemeldet) return;
  abgemeldet = abonnieren('auftrag', (d) => { if (d?.auftrag) auftragEinarbeiten(d.auftrag, 'ereignis'); });
}
auftragAbonnieren();

/** POST /api/auftraege → Auftrag (wartet/laeuft). Fehler als ApiFehler (409: im Projekt läuft schon einer). */
export async function auftragStarten(parameter) {
  const a = await senden('/api/auftraege', parameter);
  return auftragEinarbeiten(a, 'start') ?? a;
}

/** GET /api/auftraege/{id} (mit Bericht und Familien des Ergebnisses). */
export async function auftragLesen(id) {
  const a = await holen(`/api/auftraege/${encodeURIComponent(id)}`);
  return auftragEinarbeiten(a, 'abfrage') ?? a;
}

/** GET /api/auftraege: Aufträge des offenen Projekts seit dem Start des Servers (neueste zuerst, ohne Bericht). */
export async function auftraegeListe() {
  const r = await holen('/api/auftraege');
  return r?.auftraege ?? [];
}

/** POST /api/auftraege/{id}/abbrechen: beendet Claude samt MCP-Server und antwortet, wenn der Auftrag beendet ist. */
export async function auftragAbbrechen(id) {
  const a = await senden(`/api/auftraege/${encodeURIComponent(id)}/abbrechen`, {});
  return auftragEinarbeiten(a, 'abbruch') ?? a;
}

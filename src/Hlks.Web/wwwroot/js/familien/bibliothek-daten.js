// Familienbibliothek (Modul 2, C3): die Antwort der Abfrage familien_bibliothek für Projektbrowser (Explorer), Dialoge und Statusleiste – einmal geholt,
// geteilt. Gelesen wird nur über die Abfrage (keine Dateien, keine Pfade in der Oberfläche). Neu geholt wird: beim Start, nach Änderungen an Familien
// (Ereignis «modell»), beim Öffnen eines Projekts, beim Zurückkehren ins Fenster und alle 15 s (damit Änderungen aus anderen Projekten erscheinen und die
// Statusleiste Offline/Warteschlange zeigt). Die Familienliste des Projekts (daten.js, bibliothekStand) wird beim Takt mit neu geholt.

import { abfrage } from '../api.js';
import { abonnieren, zustand, element } from '../zustand.js';
import { familienLaden } from './daten.js';

const TAKT_MS = 15000;
let antwort = null;      // Antwort familien_bibliothek ohne Parameter (alle Familien beider Quellen, Feld auswahl, doppel, bibliothek)
let nachholung = null;   // zuletzt gesehene Nachholung der Warteschlange { anzahl, zeit } (der Kern meldet «nachgeholt» nur in der Antwort, die sie ausgelöst hat)
let signatur = null;
let nr = 0;
let timer = null;
const hoerer = new Set();

export const bibliothekAntwort = () => antwort;
export const bibliothekFamilien = () => antwort?.familien ?? [];
/** Kopfdaten der Bibliothek (Pfad, Quelle, erreichbar, offline, Warteschlange, nachgeholt …) plus die zuletzt gesehene Nachholung (nachgeholtMerk). */
export const bibliothekInfo = () => (antwort?.bibliothek ? { ...antwort.bibliothek, nachgeholtMerk: nachholung } : null);
export function beiBibliothek(fn) { hoerer.add(fn); return () => hoerer.delete(fn); }
const melden = () => { for (const fn of hoerer) { try { fn(antwort); } catch (e) { console.error(e); } } };

/** Holt die Antwort neu (ohne offenes Projekt bleibt sie leer). Meldet nur, wenn sich etwas geändert hat. */
export async function bibliothekLaden() {
  const mein = ++nr;
  let neu = null;
  if (zustand.status?.offen !== false) {
    try { neu = await abfrage('familien_bibliothek'); } catch { neu = null; }
  }
  if (mein !== nr) return antwort;
  if ((neu?.bibliothek?.nachgeholt ?? 0) > 0) nachholung = { anzahl: neu.bibliothek.nachgeholt, zeit: new Date().toISOString() };
  const sig = JSON.stringify([neu, nachholung]);
  if (sig !== signatur) { signatur = sig; antwort = neu; melden(); }
  return antwort;
}

/** Bibliothek und Familienliste des Projekts neu holen (nach einem Befehl, beim Takt). Erst die Bibliothek: ihr Lesen holt die Warteschlange nach und meldet es. */
export async function bibliothekAktualisieren() {
  await bibliothekLaden();
  await familienLaden();
  return antwort;
}

/** Eine Familie der Bibliothek im Detail (Versionen, Abzweige, Log, Vorschau gegenüber dem Projekt); version wählt die Fassung der Vorschau. */
export async function bibliothekDetail(familie, version) {
  return abfrage('familien_bibliothek', { familie, ...(version ? { version: String(version) } : {}) });
}

function planen(ms = 250) { clearTimeout(timer); timer = setTimeout(bibliothekAktualisieren, ms); }

abonnieren('modell', (d) => {
  if (d.alles || (d.geloescht ?? []).length > 0 || (d.ids ?? []).some((id) => element(id)?.klasse === 'Familie' || element(id)?.klasse === 'Projektinformation')) planen();
});
abonnieren('status', (s) => { if (s?.offen) planen(100); else if (antwort) { antwort = null; signatur = null; melden(); } });

if (typeof window !== 'undefined') {
  window.addEventListener('focus', () => { if (zustand.status?.offen) planen(100); });
  setInterval(() => { if (zustand.status?.offen && !document.hidden) bibliothekAktualisieren(); }, TAKT_MS);
}

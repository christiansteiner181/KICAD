// Zugang zum lokalen Server. Jede Änderung läuft über befehl(), jede Auskunft über abfrage() – dieselbe Tür,
// die auch die KI (MCP) benutzt. Fehler kommen als ApiFehler mit Meldung und Vorschlag.

export class ApiFehler extends Error {
  constructor(meldung, vorschlag, status) { super(meldung); this.vorschlag = vorschlag; this.status = status; }
}

async function antwort(r) {
  const typ = r.headers.get('content-type') || '';
  const inhalt = typ.includes('application/json') ? await r.json() : await r.text();
  if (!r.ok) {
    if (inhalt && typeof inhalt === 'object') throw new ApiFehler(inhalt.fehler || `Fehler ${r.status}`, inhalt.vorschlag, r.status);
    throw new ApiFehler(String(inhalt || `Fehler ${r.status}`), null, r.status);
  }
  return inhalt;
}

export async function holen(pfad) { return antwort(await fetch(pfad, { cache: 'no-store' })); }

export async function senden(pfad, koerper = {}, { signal } = {}) {
  return antwort(await fetch(pfad, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(koerper), signal }));
}

/** Abfrage über ihren Namen (siehe faehigkeiten). Ergebnis: Objekt oder Text (Textansichten).
 *  opt.signal (AbortSignal): bricht das Warten auf die Antwort ab (lange Abfragen wie leitungsserie einer grossen freien Serie); der Server rechnet weiter. */
export function abfrage(name, parameter = {}, opt = {}) { return senden(`/api/abfrage/${encodeURIComponent(name)}`, parameter, opt); }

/** Befehl über seinen Namen. Ergebnis: BefehlsErgebnis { angenommen, meldung, fehler, vorschlag, aenderungen, neueBefunde, … }.
 *  Ein abgelehnter Befehl ist kein Ausnahmefall, sondern angenommen=false. */
export function befehl(name, parameter = {}, { trockenlauf = false } = {}) {
  return senden(`/api/befehl/${encodeURIComponent(name)}?trockenlauf=${trockenlauf}`, parameter);
}

// Befehle an den Kern mit einheitlicher Fehler- und Ergebnisbehandlung. Die Oberfläche ändert nie selbst etwas:
// sie schickt den Befehl und wartet, bis das Ereignis vom Server zurückkommt.

import { befehl } from '../api.js';
import { meldung } from './dialoge.js';
import { esc } from './werkzeug.js';

/** Sendet einen Befehl. Netzwerk- und Serverfehler werden zu einem abgelehnten Ergebnis (nie eine Ausnahme). */
export async function sende(name, parameter = {}, opt = {}) {
  try { return await befehl(name, parameter, opt); }
  catch (e) {
    console.error(`Befehl ${name}`, e);
    return { angenommen: false, befehl: name, fehler: e.message ?? String(e), vorschlag: e.vorschlag ?? null,
      aenderungen: [], neueBefunde: [], behobeneBefunde: [], hinweise: [], konflikte: [], gesperrt: [] };
  }
}

/** Zeigt das Ergebnis eines echten Befehls als Meldung. Gibt zurück, ob er angenommen wurde. */
export function ergebnisMelden(r, okText = null) {
  if (!r.angenommen) {
    meldung(r.fehler || r.meldung || 'Der Befehl wurde abgelehnt.', 'warnung', { vorschlag: r.vorschlag });
    return false;
  }
  const hinweise = (r.hinweise ?? []).filter(Boolean);
  if ((r.aenderungen ?? []).length === 0 && hinweise.length > 0) meldung(hinweise.join(' '), 'info');
  else meldung(okText ?? r.meldung ?? 'Erledigt.', 'ok');
  if ((r.gesperrt ?? []).length > 0) meldung('Gesperrt (reserviert): ' + r.gesperrt.map((g) => g?.element ?? g?.kennzeichen ?? (typeof g === 'string' ? g : JSON.stringify(g))).join(', '), 'warnung');
  if ((r.konflikte ?? []).length > 0) meldung('Konflikt: ' + r.konflikte.map((k) => (typeof k === 'string' ? k : k?.meldung ?? JSON.stringify(k))).join('; '), 'warnung');
  return true;
}

const ART_TEXT = { angelegt: 'angelegt', geaendert: 'geändert', geloescht: 'gelöscht', getrennt: 'getrennt', verbunden: 'verbunden' };

/** HTML-Liste der Änderungen eines (Trocken-)Laufs. */
export function aenderungenHtml(aenderungen) {
  if (!aenderungen || aenderungen.length === 0) return '<p class="leise">Keine Änderungen.</p>';
  return `<table class="tab"><thead><tr><th>Art</th><th>Element</th><th>Klasse</th><th>Detail</th></tr></thead><tbody>${aenderungen.map((a) =>
    `<tr class="art-${esc(a.art)}"><td>${esc(ART_TEXT[a.art] ?? a.art)}</td><td>${esc(a.kennzeichen)}</td><td>${esc(a.klasse ?? '')}</td><td>${esc(a.detail ?? '')}</td></tr>`).join('')}</tbody></table>`;
}

/** HTML-Liste von Prüfbefunden (neu entstanden oder behoben). */
export function befundeHtml(befunde) {
  if (!befunde || befunde.length === 0) return '<p class="leise">Keine.</p>';
  return `<table class="tab"><thead><tr><th>Schwere</th><th>Code</th><th>Element</th><th>Befund</th><th>Vorschlag</th></tr></thead><tbody>${befunde.map((b) =>
    `<tr><td class="schwere-${esc(b.schwere)}">${esc(b.schwere)}</td><td>${esc(b.code)}</td><td>${esc(b.element ?? '')}</td><td>${esc(b.text)}</td><td>${esc(b.vorschlag ?? '')}</td></tr>`).join('')}</tbody></table>`;
}

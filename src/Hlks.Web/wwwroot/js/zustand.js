// Zentraler Zustand der Oberfläche: ein Abbild des Modells (Darstellungsdaten vom Kern), Auswahl, aktive Ansicht.
// Alle Teile (Menüband, Projektbrowser, Eigenschaften, Ansichten) lesen hier und melden sich für Änderungen an.
// Ereignisarten:
//   'modell'    { alles: true } nach dem Laden | { ids, geloescht, quelle, akteur, akteurArt, paket } nach einer Änderung
//   'auswahl'   id | null
//   'ansicht'   Kennzeichen der aktiven Ansicht | null
//   'status'    Status vom Server (/api/status)
//   'abgleich'  { fehler } – Abgleich mit der Projektablage
//   'konflikt'  { konflikte }
//   'verbindung'{ ok } – Ereignisstrom zum Server
//   'ki'        { gespraech, frage, typ, … } – Gespräch mit Claude (Modul 3c, Palette «Claude»)

import { holen, senden } from './api.js';

export const zustand = {
  status: null,                 // /api/status
  katalog: null,                // abfrage('katalog'): { klassen, medien, systemtypen }
  elemente: new Map(),          // id → Darstellung (siehe Kern Darstellung.Element)
  nachKennzeichen: new Map(),   // kennzeichen (klein) → id
  verbindungen: [],             // [{ id, von, vonAnschluss, nach, nachAnschluss }] (Kennzeichen)
  auswahl: null,                // id des gewählten Elements
  aktiveAnsicht: null,          // Kennzeichen der aktiven Ansicht (Element der Klasse Ansicht)
  stand: null,                  // { pakete, letztesPaket, pruefsumme, elemente, verbindungen }
  geladen: null,                // Ablageort des Projekts, dessen Katalog und Modell geladen sind (sonst null)
};

const abos = new Map();

/** Meldet fn für eine Ereignisart an; gibt eine Abmeldefunktion zurück. */
export function abonnieren(art, fn) {
  if (!abos.has(art)) abos.set(art, new Set());
  abos.get(art).add(fn);
  return () => abos.get(art)?.delete(fn);
}

export function melden(art, daten) {
  for (const fn of abos.get(art) ?? []) {
    try { fn(daten); } catch (e) { console.error(`Fehler in einem Abo für «${art}»`, e); }
  }
}

/** Element über Id oder Kennzeichen. */
export function element(bezug) {
  if (!bezug) return null;
  return zustand.elemente.get(bezug) ?? zustand.elemente.get(zustand.nachKennzeichen.get(String(bezug).toLowerCase())) ?? null;
}

export const alleElemente = () => [...zustand.elemente.values()];
export const derKlasse = (klasse) => alleElemente().filter(e => e.klasse === klasse);

function eintragen(e) {
  const alt = zustand.elemente.get(e.id);
  if (alt && alt.kennzeichen.toLowerCase() !== e.kennzeichen.toLowerCase()) zustand.nachKennzeichen.delete(alt.kennzeichen.toLowerCase());
  zustand.elemente.set(e.id, e);
  zustand.nachKennzeichen.set(e.kennzeichen.toLowerCase(), e.id);
}

function austragen(id) {
  const alt = zustand.elemente.get(id);
  if (!alt) return;
  zustand.elemente.delete(id);
  zustand.nachKennzeichen.delete(alt.kennzeichen.toLowerCase());
  if (zustand.auswahl === id) auswaehlen(null);
}

/** Lädt das ganze Modell neu (Start, Projektwechsel). */
export async function modellLaden() {
  const m = await holen('/api/modell');
  zustand.elemente.clear();
  zustand.nachKennzeichen.clear();
  for (const e of m.elemente) eintragen(e);
  zustand.verbindungen = m.verbindungen;
  zustand.stand = m.stand;
  melden('modell', { alles: true });
}

/** Leert alles Projektbezogene (Schliessen, Projektwechsel): Modell, Katalog, Auswahl, aktive Ansicht. */
export function projektLeeren() {
  auswaehlen(null);
  ansichtAktivieren(null);
  zustand.elemente.clear();
  zustand.nachKennzeichen.clear();
  zustand.verbindungen = [];
  zustand.stand = null;
  zustand.katalog = null;
  zustand.geladen = null;
  melden('modell', { alles: true });
}

/** Lädt geänderte Elemente nach (nach einem Ereignis) und meldet sie. info = Ereignisdaten (quelle, akteur …). */
export async function nachladen(ids, info = {}) {
  if (!ids || ids.length === 0) return;
  const r = await senden('/api/elemente', { ids });
  // Verbindungen der betroffenen Elemente ersetzen (auch unter dem alten Kennzeichen): /api/elemente liefert sie mit, das ganze
  // Modell nachzuladen kostete am echten Projekt je Änderung ~200 ms und ~450 kB (Gesamtschau H2, 30.10.2026)
  const kz = new Set();
  for (const id of ids) { const alt = zustand.elemente.get(id); if (alt) kz.add(alt.kennzeichen); }
  for (const e of r.elemente) { kz.add(e.kennzeichen); eintragen(e); }
  for (const id of r.geloescht) austragen(id);
  zustand.stand = r.stand;
  zustand.verbindungen = zustand.verbindungen.filter(v => !kz.has(v.von) && !kz.has(v.nach)).concat(r.verbindungen ?? []);
  melden('modell', { ...info, ids: r.elemente.map(e => e.id), geloescht: r.geloescht });
}

export function auswaehlen(id) {
  if (zustand.auswahl === id) return;
  zustand.auswahl = id;
  melden('auswahl', id);
}

export function ansichtAktivieren(kennzeichen) {
  if (zustand.aktiveAnsicht === kennzeichen) return;
  zustand.aktiveAnsicht = kennzeichen;
  melden('ansicht', kennzeichen);
}

/** Klassendefinition aus dem Katalog (Name, Schicht, Eigenschaften mit Typ/Einheit/Auswahl, Anschlüsse). */
export function klasse(name) {
  return zustand.katalog?.klassen?.find(k => k.name === name) ?? null;
}

/** Kleine Hilfe für localStorage – darf nie stören (privates Fenster, gesperrt). */
export const merken = {
  lesen(schluessel, vorgabe) { try { const t = localStorage.getItem('hlks.' + schluessel); return t ? JSON.parse(t) : vorgabe; } catch { return vorgabe; } },
  schreiben(schluessel, wert) { try { localStorage.setItem('hlks.' + schluessel, JSON.stringify(wert)); } catch { /* egal */ } },
  entfernen(schluessel) { try { localStorage.removeItem('hlks.' + schluessel); } catch { /* egal */ } },
};

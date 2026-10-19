// Eigenes Fenster für eine Ansicht (Teil W2, entwurf/fenster-revit.md, Punkt 2): ein zweites App-Fenster derselben Oberfläche im Modus
// «nur Ansicht», z. B. für den zweiten Bildschirm. Gleicher Server, gleiche Sitzung: Änderungen und Rückgängig laufen über den
// Ereignisstrom des Servers (/api/ereignisse, jedes Browserfenster hat seinen eigenen Strom), die Auswahl und das Wissen, wer welche
// Ansicht hält, über einen BroadcastChannel («hlks-fenster»).
//
// Adresse des zweiten Fensters:  /?ansicht=<Kennzeichen>[,<Kennzeichen>…]&fenster=<n>      (n = 2, 3 … ; das Hauptfenster ist 1)
//
// Nachrichten (alle mit von = Fensternummer des Absenders):
//   { art: 'auswahl', id }              Auswahl wurde geändert (beide Richtungen)
//   { art: 'wer' }                      Hauptfenster fragt, welche Zweitfenster leben → jedes antwortet mit 'da'
//   { art: 'da', kz: [..] }             Zweitfenster meldet die Ansichten, die es hält
//   { art: 'weg', kz: [..] }            Zweitfenster wird geschlossen (oder neu geladen): Ansichten gehen zurück ans Hauptfenster,
//                                       wenn nicht binnen ZURUECK_MS wieder ein 'da' für sie kommt (Neuladen)
//   { art: 'zurueck', kz: [..] }        Zweitfenster gibt Ansichten ausdrücklich zurück (Knopf «Zurück ins Hauptfenster»)
//   { art: 'fokus', kz }                Hauptfenster bittet das Fenster, das kz hält, nach vorn zu kommen
// Dieses Modul kennt fenster.js nicht (keine Kreisabhängigkeit): fenster.js übergibt in einrichten() eine Schnittstelle.

import { zustand, abonnieren, auswaehlen } from '../zustand.js';

export const KANAL = 'hlks-fenster';
export const ZURUECK_MS = 1200;     // so lange wartet das Hauptfenster nach 'weg', ob dieselbe Ansicht (Neuladen) wieder auftaucht
export const ERKUNDEN_MS = 300;     // so lange wartet das Hauptfenster beim Start auf Antworten der Zweitfenster

// ---------------------------------------------------------------------------------------------- reine Teile

/** Liest Adressteil «?ansicht=…&fenster=…»: { nummer (1 = Hauptfenster), ansichten: [kz …] }. Ungültiges ergibt das Hauptfenster. */
export function parameterLesen(search) {
  const p = new URLSearchParams(search ?? '');
  const n = Number(p.get('fenster'));
  const nummer = Number.isInteger(n) && n >= 2 && n <= 9 ? n : 1;
  const ansichten = nummer >= 2 ? (p.get('ansicht') ?? '').split(',').map((s) => s.trim()).filter(Boolean) : [];
  return { nummer, ansichten };
}

/** Adresse des Zweitfensters für die Ansichten (Kennzeichen) mit der Nummer n. basis = Ursprung, z. B. «http://localhost:5178/». */
export function fensterUrl(basis, kennzeichen, nummer) {
  const liste = Array.isArray(kennzeichen) ? kennzeichen : [kennzeichen];
  const u = new URL(basis);
  u.search = '';
  u.hash = '';
  u.searchParams.set('ansicht', liste.join(','));
  u.searchParams.set('fenster', String(nummer));
  return u.toString();
}

/** Kleinste freie Fensternummer ab 2 (belegt = Menge der Nummern, die ein lebendes Zweitfenster trägt). */
export function freieNummer(belegt) {
  const b = new Set(belegt);
  for (let n = 2; n <= 9; n++) if (!b.has(n)) return n;
  return null;
}

/** Fensterfunktionen für window.open: Lage an der Stelle des Loslassens (Bildschirmkoordinaten), sonst Vorgabegrösse. */
export function fensterOptionen({ x = null, y = null, breite = 1100, hoehe = 750 } = {}) {
  const teile = ['popup=yes', `width=${Math.round(breite)}`, `height=${Math.round(hoehe)}`];
  if (Number.isFinite(x) && Number.isFinite(y)) teile.push(`left=${Math.max(0, Math.round(x - 60))}`, `top=${Math.max(0, Math.round(y - 20))}`);
  return teile.join(',');
}

// ---------------------------------------------------------------------------------------------- Zustand dieses Fensters

const lage = () => parameterLesen(globalThis.location?.search ?? '');
export const fensterNummer = () => lage().nummer;
export const istZweitfenster = () => lage().nummer >= 2;
export const startAnsichten = () => lage().ansichten;

let kanal = null;
let host = null;
let empfange = false;                 // gerade eine Auswahl aus dem anderen Fenster übernommen: nicht zurückschicken
const zweitfenster = new Map();       // Hauptfenster: Kennzeichen (klein) → { nummer, kz }
const wartend = new Map();            // Hauptfenster: Kennzeichen (klein) → Zeitgeber der Rückgabe nach 'weg'
const lc = (s) => String(s ?? '').toLowerCase();

function sende(daten) {
  try { kanal?.postMessage({ ...daten, von: fensterNummer() }); } catch (e) { console.warn('Fensterkanal', e); }
}

/**
 * Schliesst dieses Fenster an den Kanal an. schnittstelle:
 *   ansichten()                 Kennzeichen der Ansichten, die dieses Fenster hält
 *   hat(kz)                     hält dieses Fenster die Ansicht?
 *   uebernommen(kz)             (Hauptfenster) Ansicht wird nun von einem Zweitfenster gehalten: aus der eigenen Anordnung nehmen
 *   zurueck(kz)                 (Hauptfenster) Ansicht kommt zurück: wieder öffnen
 */
export function einrichten(schnittstelle) {
  host = schnittstelle;
  if (typeof BroadcastChannel !== 'function') return;
  kanal?.close();
  kanal = new BroadcastChannel(KANAL);
  kanal.onmessage = (e) => { try { nachricht(e.data); } catch (err) { console.error('Fensterkanal', err); } };
  abonnieren('auswahl', (id) => { if (!empfange) sende({ art: 'auswahl', id }); });
  if (istZweitfenster()) {
    window.addEventListener('pagehide', () => { if (!beendet) sende({ art: 'weg', kz: host.ansichten() }); });
  }
}

let beendet = false;   // Zweitfenster hat seine Ansichten schon ausdrücklich zurückgegeben

function nachricht(d) {
  if (!d || typeof d !== 'object' || d.von === fensterNummer()) return;
  const haupt = !istZweitfenster();
  switch (d.art) {
    case 'auswahl':
      empfange = true;
      try { auswaehlen(d.id ?? null); } finally { empfange = false; }
      break;
    case 'wer':
      if (!haupt) anmelden();
      break;
    case 'da':
      if (!haupt) break;
      for (const kz of d.kz ?? []) {
        zweitfenster.set(lc(kz), { nummer: d.von, kz });
        const z = wartend.get(lc(kz));
        if (z) { clearTimeout(z); wartend.delete(lc(kz)); }
        if (host.hat(kz)) host.uebernommen(kz);
      }
      sende({ art: 'auswahl', id: zustand.auswahl });   // das neue Fenster zeigt dieselbe Auswahl
      break;
    case 'weg':
      if (!haupt) break;
      for (const kz of d.kz ?? []) {
        if (zweitfenster.get(lc(kz))?.nummer !== d.von) continue;
        clearTimeout(wartend.get(lc(kz)));
        wartend.set(lc(kz), setTimeout(() => { wartend.delete(lc(kz)); zweitfenster.delete(lc(kz)); host.zurueck(kz); }, ZURUECK_MS));
      }
      break;
    case 'zurueck':
      if (!haupt) break;
      for (const kz of d.kz ?? []) { clearTimeout(wartend.get(lc(kz))); wartend.delete(lc(kz)); zweitfenster.delete(lc(kz)); host.zurueck(kz); }
      break;
    case 'fokus':
      if (!haupt && host.hat(d.kz)) { try { window.focus(); } catch { /* egal */ } }
      break;
  }
}

/** Zweitfenster: meldet dem Hauptfenster, welche Ansichten es hält. */
export function anmelden() { if (istZweitfenster()) sende({ art: 'da', kz: host?.ansichten() ?? [] }); }

/** Zweitfenster: gibt die Ansichten ausdrücklich ans Hauptfenster zurück (der Aufrufer schliesst danach das Fenster). */
export function zurueckgeben() {
  beendet = true;
  sende({ art: 'zurueck', kz: host?.ansichten() ?? [] });
}

/** Hauptfenster beim Start: fragt die Zweitfenster, welche Ansichten sie halten. Ergebnis: Menge der Kennzeichen (klein). */
export function erkunden(wartezeit = ERKUNDEN_MS) {
  if (!kanal || istZweitfenster()) return Promise.resolve(new Set());
  sende({ art: 'wer' });
  return new Promise((fertig) => setTimeout(() => fertig(new Set([...zweitfenster.keys()])), wartezeit));
}

/** Hauptfenster: hält ein Zweitfenster diese Ansicht? */
export const wirdGehalten = (kz) => zweitfenster.has(lc(kz));
/** Bittet das Zweitfenster, das die Ansicht hält, nach vorn zu kommen. */
export function fokussieren(kz) { sende({ art: 'fokus', kz }); }

/**
 * Hauptfenster: öffnet die Ansicht in einem eigenen App-Fenster. pos = { x, y } in Bildschirmkoordinaten (Stelle des Loslassens).
 * Ergebnis: { ok, nummer } oder { ok: false, grund }. Das Fenster wird sofort als Halter eingetragen; meldet es sich nicht, holt
 * der Aufrufer die Ansicht über 'weg'/'zurueck' zurück.
 */
export function inEigenemFensterOeffnen(kz, pos = {}) {
  if (istZweitfenster()) return { ok: false, grund: 'Schon in einem eigenen Fenster.' };
  const nummer = freieNummer([...zweitfenster.values()].map((z) => z.nummer));
  if (nummer == null) return { ok: false, grund: 'Es sind schon acht eigene Fenster offen.' };
  const url = fensterUrl(globalThis.location.href, kz, nummer);
  let w = null;
  try { w = window.open(url, `hlks-fenster-${nummer}`, fensterOptionen(pos)); } catch { w = null; }
  if (!w) return { ok: false, grund: 'Der Browser hat das neue Fenster blockiert. Pop-ups für HLKS-CAD erlauben und noch einmal versuchen.' };
  zweitfenster.set(lc(kz), { nummer, kz });
  return { ok: true, nummer };
}

/** Test- und Diagnosehilfe: welche Ansichten halten Zweitfenster? */
export const zweitfensterListe = () => [...zweitfenster.values()].map((z) => ({ ...z }));

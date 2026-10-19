// Gemerkte Lage und Grösse der Dialoge (Auftrag W1): rein, ohne DOM und ohne Speicher – dialoge.js liest und schreibt sie über zustand.js «merken» (je Rechner).
// Format (Schlüssel «dialoge»): { "<Dialogart>": { x, y, b, h } } – x, y fehlen = in der Mitte, b, h fehlen = Grösse wie vorgegeben.
//   dialogArt({ art, titel, klasse })   Schlüssel der Dialogart: ausdrücklich angegebene Art, sonst der Titel ohne Namen in «…» und ohne Ziffern
//   bereinigen(roh)                      liest gemerkte Werte, verwirft Ungültiges (kein Objekt, keine Zahlen, unvernünftige Grössen)
//   setzen(karte, art, teil)             neue Karte mit dem Eintrag (zuletzt benutzte Art zuletzt); bei mehr als MAX Einträgen fallen die ältesten weg
//   positionVergessen(karte, art)        entfernt x und y (Doppelklick auf die Titelleiste = zurück zur Mitte), Grösse bleibt

export const MAX_EINTRAEGE = 80;
export const SCHLUESSEL = 'dialoge';
const MAX_ART = 60;
const GRENZE = 20000;   // alles darüber ist ein Fehler (nicht gemerkt)

/** Schlüssel der Dialogart. */
export function dialogArt({ art = null, titel = '', klasse = '' } = {}) {
  const roh = String(art ?? '').trim() || String(titel ?? '')
    .replace(/«[^»]*»/g, ' ').replace(/"[^"]*"/g, ' ').replace(/'[^']*'/g, ' ')
    .replace(/\d+/g, ' ').replace(/[(){}\[\]:;,.!?…]/g, ' ');
  const sauber = roh.toLowerCase().replace(/\s+/g, ' ').trim().slice(0, MAX_ART);
  if (sauber) return sauber;
  const k = String(klasse ?? '').split(/\s+/).find((c) => c && !c.startsWith('dlg-')) ?? '';
  return k ? 'klasse:' + k.toLowerCase().slice(0, MAX_ART) : 'dialog';
}

const gueltig = (w, min, max = GRENZE) => typeof w === 'number' && Number.isFinite(w) && w >= min && w <= max;

function eintragPruefen(e) {
  if (!e || typeof e !== 'object' || Array.isArray(e)) return null;
  const aus = {};
  if (gueltig(e.x, -GRENZE) && gueltig(e.y, -GRENZE)) { aus.x = Math.round(e.x); aus.y = Math.round(e.y); }
  if (gueltig(e.b, 120) && gueltig(e.h, 60)) { aus.b = Math.round(e.b); aus.h = Math.round(e.h); }
  return Object.keys(aus).length > 0 ? aus : null;
}

/** Liest die gemerkten Werte; Ungültiges wird verworfen (nie ein Fehler). */
export function bereinigen(roh) {
  if (!roh || typeof roh !== 'object' || Array.isArray(roh)) return {};
  const karte = {};
  for (const [art, e] of Object.entries(roh)) {
    if (typeof art !== 'string' || art === '' || art.length > MAX_ART + 10) continue;
    const ok = eintragPruefen(e);
    if (ok) karte[art] = ok;
  }
  const arten = Object.keys(karte);
  if (arten.length > MAX_EINTRAEGE) for (const a of arten.slice(0, arten.length - MAX_EINTRAEGE)) delete karte[a];
  return karte;
}

/** Karte mit dem geänderten Eintrag; teil darf x, y und/oder b, h enthalten (das Übrige bleibt). */
export function setzen(karte, art, teil) {
  const neu = { ...bereinigen(karte) };
  const alt = neu[art] ?? {};
  delete neu[art];
  const e = eintragPruefen({ ...alt, ...teil }) ?? null;
  if (e) neu[art] = e;
  const arten = Object.keys(neu);
  if (arten.length > MAX_EINTRAEGE) for (const a of arten.slice(0, arten.length - MAX_EINTRAEGE)) delete neu[a];
  return neu;
}

/** Entfernt die gemerkte Lage (Mitte); die gemerkte Grösse bleibt. */
export function positionVergessen(karte, art) {
  const neu = { ...bereinigen(karte) };
  const e = neu[art];
  if (!e) return neu;
  const { b, h } = e;
  delete neu[art];
  if (b !== undefined) neu[art] = { b, h };
  return neu;
}

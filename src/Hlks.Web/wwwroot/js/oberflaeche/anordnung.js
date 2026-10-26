// Anordnung der Ansichten (Teil W2, entwurf/fenster-revit.md): reine Daten und Rechnung, ohne DOM, in Node getestet.
//
// Die offenen Ansichten liegen in einem Baum aus Gruppen und Teilungen (wie Revit bzw. Visual Studio):
//   Gruppe    { art: 'gruppe', id: 'g1', ansichten: [name …], aktiv: name }      eigene Reiterleiste, eine Ansicht davon sichtbar
//   Teilung   { art: 'teilung', richtung: 'nebeneinander' | 'untereinander', kinder: [Knoten …], anteile: [0.5, 0.5] }
//   Anordnung { version: 1, wurzel: Knoten | null, aktiveGruppe: 'g1' | null, ausgelagert: [name …] }
// «name» ist, was der Aufrufer will (im Browser die Id der Ansicht, gemerkt wird das Kennzeichen). «ausgelagert» sind Ansichten,
// die in einem eigenen Fenster (zweites App-Fenster) offen sind und hier keinen Platz einnehmen.
//
// Alle Funktionen sind rein: sie ändern ihre Eingabe nie und liefern eine neue Anordnung. Eine Anordnung ist nach jeder
// Funktion «sauber»: keine leere Gruppe, keine Teilung mit weniger als zwei Kindern, keine Teilung in einer Teilung gleicher
// Richtung, Anteile > 0 mit Summe 1, jede Ansicht höchstens einmal, aktiveGruppe und aktiv gültig.

export const VERSION = 1;
export const RICHTUNGEN = ['nebeneinander', 'untereinander'];
export const ORTE = ['mitte', 'links', 'rechts', 'oben', 'unten'];
export const MIN_ANTEIL = 0.05;
export const TIEFE_MAX = 8;
export const LEISTE = 22;       // Höhe der Reiterleiste einer Gruppe in px (wie --hoehe-ansichtsreiter)
export const TRENNER = 4;       // Breite eines Trenners in px
export const RAND_ZONE = 14;    // px am Rand der ganzen Fläche: Ziel «an den Rand»
export const LEITPUNKT = 28;    // Kantenlänge der Andockziele (Kreuz) in px

const klon = (x) => JSON.parse(JSON.stringify(x));
const istOrt = (o) => ORTE.includes(o);

/** Leere Anordnung. */
export function leer() { return { version: VERSION, wurzel: null, aktiveGruppe: null, ausgelagert: [] }; }

// ---------------------------------------------------------------------------------------------- Abfragen

/** Alle Gruppen in Reihenfolge des Baums (links nach rechts, oben nach unten). */
export function gruppen(a) {
  const liste = [];
  const geh = (k) => { if (!k) return; if (k.art === 'gruppe') liste.push(k); else k.kinder.forEach(geh); };
  geh(a?.wurzel);
  return liste;
}
export const findeGruppe = (a, id) => gruppen(a).find((g) => g.id === id) ?? null;
export const gruppeVon = (a, name) => gruppen(a).find((g) => g.ansichten.includes(name)) ?? null;
/** Alle Ansichten im Baum, in Reihenfolge der Gruppen und Reiter. */
export const ansichten = (a) => gruppen(a).flatMap((g) => g.ansichten);
/** Die aktive Gruppe (oder die erste). */
export const aktiveGruppe = (a) => findeGruppe(a, a?.aktiveGruppe) ?? gruppen(a)[0] ?? null;
/** Die aktive Ansicht der aktiven Gruppe oder null. */
export const aktiveAnsicht = (a) => aktiveGruppe(a)?.aktiv ?? null;
/**
 * Der nächste (schritt = 1) bzw. vorherige (schritt = -1) Reiter in der aktiven Gruppe, am Ende der Reihe von vorn bzw. hinten weiter (Modul G, Gesten, «ansicht.reiter-vor»).
 * Ergebnis: Name der Ansicht oder null, wenn die aktive Gruppe weniger als zwei Reiter hat.
 */
export function reiterNachbar(a, schritt = 1) {
  const g = aktiveGruppe(a);
  const liste = g?.ansichten ?? [];
  if (liste.length < 2) return null;
  const i = Math.max(0, liste.indexOf(g.aktiv));
  return liste[(((i + (schritt < 0 ? -1 : 1)) % liste.length) + liste.length) % liste.length];
}
/** Zahl der Reiter der aktiven Gruppe. */
export const reiterZahl = (a) => aktiveGruppe(a)?.ansichten.length ?? 0;

/** 'reiter' = höchstens eine Gruppe, 'nebeneinander' = mehrere Gruppen. */
export const modus = (a) => (gruppen(a).length > 1 ? 'nebeneinander' : 'reiter');

/** Eltern einer Gruppe oder Teilung: { knoten, index } oder null (Wurzel). */
function elternVon(a, ziel) {
  let treffer = null;
  const geh = (k) => {
    if (!k || k.art !== 'teilung' || treffer) return;
    const i = k.kinder.indexOf(ziel);
    if (i >= 0) { treffer = { knoten: k, index: i }; return; }
    k.kinder.forEach(geh);
  };
  geh(a.wurzel);
  return treffer;
}

/** Knoten am Pfad (Liste von Kindindizes ab der Wurzel). */
export function knotenAn(a, pfad) {
  let k = a?.wurzel ?? null;
  for (const i of pfad ?? []) { if (!k || k.art !== 'teilung' || !k.kinder[i]) return null; k = k.kinder[i]; }
  return k;
}

function neueId(a) {
  let n = 0;
  for (const g of gruppen(a)) { const m = /^g(\d+)$/.exec(g.id); if (m) n = Math.max(n, Number(m[1])); }
  return 'g' + (n + 1);
}

// ---------------------------------------------------------------------------------------------- Aufräumen

/** Anteile > 0, Summe 1, jeder mindestens MIN_ANTEIL (oder 1/n, wenn das nicht geht). */
export function anteileNormieren(roh, n = roh?.length ?? 0) {
  if (n <= 0) return [];
  let w = Array.from({ length: n }, (_, i) => (Number.isFinite(roh?.[i]) && roh[i] > 0 ? roh[i] : 0));
  const summe = w.reduce((s, x) => s + x, 0);
  if (summe <= 0) return Array(n).fill(1 / n);
  w = w.map((x) => x / summe);
  const min = Math.min(MIN_ANTEIL, 1 / n);
  for (let runde = 0; runde < n; runde++) {
    const klein = w.map((x) => x < min - 1e-12);
    if (!klein.some(Boolean)) break;
    const nKlein = klein.filter(Boolean).length;
    const restSumme = w.reduce((s, x, i) => s + (klein[i] ? 0 : x), 0);
    const faktor = restSumme > 0 ? (1 - nKlein * min) / restSumme : 0;
    w = w.map((x, i) => (klein[i] ? min : x * faktor));
  }
  const s2 = w.reduce((s, x) => s + x, 0);
  return w.map((x) => x / s2);
}

/** Entfernt leere Gruppen, fasst Teilungen mit einem Kind zusammen und gleichrichtige Teilungen ineinander. */
function saeubern(k) {
  if (!k) return null;
  if (k.art === 'gruppe') return k.ansichten.length ? k : null;
  const kinder = [], anteile = [];
  const n = k.kinder.length;
  k.kinder.forEach((c, i) => {
    const s = saeubern(c);
    if (!s) return;
    const w = Number.isFinite(k.anteile?.[i]) && k.anteile[i] > 0 ? k.anteile[i] : 1 / n;
    if (s.art === 'teilung' && s.richtung === k.richtung) s.kinder.forEach((cc, j) => { kinder.push(cc); anteile.push(w * s.anteile[j]); });
    else { kinder.push(s); anteile.push(w); }
  });
  if (kinder.length === 0) return null;
  if (kinder.length === 1) return kinder[0];
  return { art: 'teilung', richtung: k.richtung, kinder, anteile: anteileNormieren(anteile, kinder.length) };
}

/** Räumt eine (schon kopierte) Anordnung auf und stellt aktiv/aktiveGruppe richtig. */
function abschliessen(a) {
  a.wurzel = saeubern(a.wurzel);
  const liste = gruppen(a);
  for (const g of liste) if (!g.ansichten.includes(g.aktiv)) g.aktiv = g.ansichten.at(-1);
  if (!liste.some((g) => g.id === a.aktiveGruppe)) a.aktiveGruppe = liste.at(-1)?.id ?? null;
  const drin = new Set(ansichten(a));
  a.ausgelagert = [...new Set(a.ausgelagert ?? [])].filter((x) => !drin.has(x));
  a.version = VERSION;
  return a;
}

// ---------------------------------------------------------------------------------------------- Ändern

/** Legt eine Ansicht als Reiter in eine Gruppe (Vorgabe: aktive Gruppe, ans Ende) und macht sie aktiv. Ist sie schon da, wird sie nur aktiv. */
export function hinzufuegen(anordnung, name, { gruppe = null, index = null } = {}) {
  const a = klon(anordnung);
  a.ausgelagert = (a.ausgelagert ?? []).filter((x) => x !== name);
  const vorhanden = gruppeVon(a, name);
  if (vorhanden) { vorhanden.aktiv = name; a.aktiveGruppe = vorhanden.id; return abschliessen(a); }
  let g = (gruppe && findeGruppe(a, gruppe)) || aktiveGruppe(a);
  if (!g) { g = { art: 'gruppe', id: 'g1', ansichten: [], aktiv: name }; a.wurzel = g; }
  const i = Number.isInteger(index) ? Math.max(0, Math.min(index, g.ansichten.length)) : g.ansichten.length;
  g.ansichten.splice(i, 0, name);
  g.aktiv = name;
  a.aktiveGruppe = g.id;
  return abschliessen(a);
}

/** Nimmt eine Ansicht heraus (geschlossen oder ausgelagert). Wird die aktive Ansicht einer Gruppe geschlossen, tritt die letzte der Gruppe an ihre Stelle. */
export function entfernen(anordnung, name) {
  const a = klon(anordnung);
  a.ausgelagert = (a.ausgelagert ?? []).filter((x) => x !== name);
  const g = gruppeVon(a, name);
  if (!g) return abschliessen(a);
  const vorher = gruppen(a);
  const gIndex = vorher.indexOf(g);
  g.ansichten.splice(g.ansichten.indexOf(name), 1);
  if (g.aktiv === name) g.aktiv = g.ansichten.at(-1);
  if (g.ansichten.length === 0 && a.aktiveGruppe === g.id) {
    const rest = vorher.filter((x) => x !== g);
    a.aktiveGruppe = rest[Math.min(gIndex, rest.length - 1)]?.id ?? null;
  }
  return abschliessen(a);
}

/** Nimmt eine Ansicht aus dem Baum und merkt sie als ausgelagert (eigenes Fenster). */
export function auslagern(anordnung, name) {
  const a = entfernen(anordnung, name);
  if (!a.ausgelagert.includes(name)) a.ausgelagert.push(name);
  return a;
}

/** Macht eine Ansicht (und ihre Gruppe) aktiv. */
export function aktivieren(anordnung, name) {
  const a = klon(anordnung);
  const g = gruppeVon(a, name);
  if (!g) return a;
  g.aktiv = name;
  a.aktiveGruppe = g.id;
  return a;
}

/** Macht nur die Gruppe aktiv (ihre sichtbare Ansicht wird die aktive). */
export function gruppeAktivieren(anordnung, id) {
  const a = klon(anordnung);
  if (findeGruppe(a, id)) a.aktiveGruppe = id;
  return a;
}

function ersetzen(a, alt, neu) {
  const e = elternVon(a, alt);
  if (e) e.knoten.kinder[e.index] = neu; else a.wurzel = neu;
}

/** Teilt die Gruppe g: neue Gruppe `neu` an den Ort (links/rechts/oben/unten), halber Platz. */
function teilen(a, g, ort, neu) {
  const richtung = ort === 'links' || ort === 'rechts' ? 'nebeneinander' : 'untereinander';
  const vorn = ort === 'links' || ort === 'oben';
  const e = elternVon(a, g);
  if (e && e.knoten.richtung === richtung) {
    const t = e.knoten, w = t.anteile[e.index], pos = vorn ? e.index : e.index + 1;
    t.anteile[e.index] = w / 2;
    t.kinder.splice(pos, 0, neu);
    t.anteile.splice(pos, 0, w / 2);
  } else {
    ersetzen(a, g, { art: 'teilung', richtung, kinder: vorn ? [neu, g] : [g, neu], anteile: [0.5, 0.5] });
  }
}

/** Neue Gruppe an den Rand der ganzen Fläche (links/rechts/oben/unten) mit dem Anteil `anteil`. */
function amRand(a, ort, neu, anteil = 0.3) {
  const richtung = ort === 'links' || ort === 'rechts' ? 'nebeneinander' : 'untereinander';
  const vorn = ort === 'links' || ort === 'oben';
  const w = a.wurzel;
  if (w.art === 'teilung' && w.richtung === richtung) {
    const rest = w.anteile.map((x) => x * (1 - anteil));
    if (vorn) { w.kinder.unshift(neu); w.anteile = [anteil, ...rest]; } else { w.kinder.push(neu); w.anteile = [...rest, anteil]; }
  } else {
    a.wurzel = { art: 'teilung', richtung, kinder: vorn ? [neu, w] : [w, neu], anteile: vorn ? [anteil, 1 - anteil] : [1 - anteil, anteil] };
  }
}

/**
 * Verschiebt eine Ansicht. ziel:
 *   { gruppe: id, ort: 'mitte', index? }            als Reiter in die Gruppe (an Stelle index, sonst ans Ende)
 *   { gruppe: id, ort: 'links'|'rechts'|'oben'|'unten' }   teilt die Gruppe, die Ansicht bekommt die neue Hälfte
 *   { rand: 'links'|'rechts'|'oben'|'unten' }       neue Gruppe am Rand der ganzen Fläche
 * Die einzige Ansicht einer Gruppe lässt sich nicht an den Rand ihrer eigenen Gruppe ziehen (nichts würde sich ändern).
 * Ist die Ansicht nicht im Baum (z. B. ausgelagert), wird sie neu eingefügt.
 */
export function verschieben(anordnung, name, ziel) {
  const a = klon(anordnung);
  a.ausgelagert = (a.ausgelagert ?? []).filter((x) => x !== name);
  const quelle = gruppeVon(a, name);
  const ort = ziel?.rand ?? ziel?.ort;
  if (!istOrt(ort)) return abschliessen(a);
  if (ziel.rand && ort === 'mitte') return abschliessen(a);
  const zielG = ziel.rand ? null : findeGruppe(a, ziel.gruppe);
  if (!ziel.rand && !zielG) return abschliessen(a);
  if (quelle && zielG === quelle && quelle.ansichten.length === 1) { quelle.aktiv = name; a.aktiveGruppe = quelle.id; return abschliessen(a); }

  // Ansicht aus ihrer Gruppe lösen (die Gruppe bleibt bis zum Aufräumen stehen, damit Ziele gültig bleiben)
  let index = ziel.index;
  if (quelle) {
    const i = quelle.ansichten.indexOf(name);
    quelle.ansichten.splice(i, 1);
    if (quelle.aktiv === name) quelle.aktiv = quelle.ansichten.at(-1);
    if (zielG === quelle && Number.isInteger(index) && i < index) index -= 1;
  }
  let landung;
  if (ziel.rand) {
    if (!a.wurzel) { a.wurzel = landung = { art: 'gruppe', id: neueId(a), ansichten: [name], aktiv: name }; }
    else { landung = { art: 'gruppe', id: neueId(a), ansichten: [name], aktiv: name }; amRand(a, ort, landung); }
  } else if (ort === 'mitte') {
    const i = Number.isInteger(index) ? Math.max(0, Math.min(index, zielG.ansichten.length)) : zielG.ansichten.length;
    zielG.ansichten.splice(i, 0, name);
    zielG.aktiv = name;
    landung = zielG;
  } else {
    landung = { art: 'gruppe', id: neueId(a), ansichten: [name], aktiv: name };
    teilen(a, zielG, ort, landung);
  }
  a.aktiveGruppe = landung.id;
  return abschliessen(a);
}

/**
 * Verschiebt den Trenner zwischen Kind `index` und `index+1` der Teilung am Pfad auf die Stelle `position`
 * (Anteil der ganzen Teilung, 0…1). Beide Nachbarn behalten mindestens `mindest` (Anteil der Teilung), die übrigen Kinder bleiben.
 */
export function trennerSetzen(anordnung, pfad, index, position, mindest = 0.1) {
  const a = klon(anordnung);
  const t = knotenAn(a, pfad);
  if (!t || t.art !== 'teilung' || !(index >= 0 && index < t.kinder.length - 1) || !Number.isFinite(position)) return a;
  const lo = t.anteile.slice(0, index).reduce((s, x) => s + x, 0);
  const hi = lo + t.anteile[index] + t.anteile[index + 1];
  const min = Math.min(Math.max(mindest, 0), (hi - lo) / 2);
  const c = Math.max(lo + min, Math.min(hi - min, position));
  t.anteile[index] = c - lo;
  t.anteile[index + 1] = hi - c;
  return a;
}

/** Ordnet alle Ansichten gleichmässig an (Revit «Kacheln»): Spalten = ⌈√n⌉, jede Ansicht eine Gruppe, Reihen füllen die Breite. */
export function kacheln(anordnung) {
  const a = klon(anordnung);
  const namen = ansichten(a);
  const aktiv = aktiveAnsicht(a);
  if (namen.length <= 1) return abschliessen(a);
  const spalten = Math.ceil(Math.sqrt(namen.length));
  const reihen = [];
  for (let i = 0; i < namen.length; i += spalten) reihen.push(namen.slice(i, i + spalten));
  let n = 0;
  const gruppe = (name) => ({ art: 'gruppe', id: 'g' + (++n), ansichten: [name], aktiv: name });
  const zeilen = reihen.map((r) => (r.length === 1 ? gruppe(r[0]) : { art: 'teilung', richtung: 'nebeneinander', kinder: r.map(gruppe), anteile: Array(r.length).fill(1 / r.length) }));
  a.wurzel = zeilen.length === 1 ? zeilen[0] : { art: 'teilung', richtung: 'untereinander', kinder: zeilen, anteile: Array(zeilen.length).fill(1 / zeilen.length) };
  const g = gruppeVon(a, aktiv);
  a.aktiveGruppe = g?.id ?? null;
  return abschliessen(a);
}

/** Legt alle Ansichten als Reiter in eine Gruppe (Revit «Fenster als Reiter»). */
export function zusammenlegen(anordnung) {
  const a = klon(anordnung);
  const namen = ansichten(a);
  if (namen.length === 0) return abschliessen(a);
  const aktiv = aktiveAnsicht(a);
  a.wurzel = { art: 'gruppe', id: 'g1', ansichten: namen, aktiv };
  a.aktiveGruppe = 'g1';
  return abschliessen(a);
}

/** Wendet fn auf jeden Namen an (z. B. Id → Kennzeichen); null/undefiniert = Ansicht entfällt. */
export function umbenennen(anordnung, fn) {
  const a = klon(anordnung);
  for (const g of gruppen(a)) {
    const neu = g.ansichten.map((x) => fn(x)).filter((x) => x != null);
    const aktiv = fn(g.aktiv);
    g.ansichten = [...new Set(neu)];
    g.aktiv = aktiv != null && g.ansichten.includes(aktiv) ? aktiv : g.ansichten.at(-1);
  }
  a.ausgelagert = (a.ausgelagert ?? []).map((x) => fn(x)).filter((x) => x != null);
  const g = findeGruppe(a, a.aktiveGruppe);
  // doppelte Namen nach der Umbenennung: nur der erste bleibt
  const gesehen = new Set();
  for (const gr of gruppen(a)) gr.ansichten = gr.ansichten.filter((x) => (gesehen.has(x) ? false : (gesehen.add(x), true)));
  if (!g) a.aktiveGruppe = null;
  return abschliessen(a);
}

// ---------------------------------------------------------------------------------------------- Merken und Laden

/**
 * Macht aus beliebigen (gemerkten, womöglich kaputten) Daten eine saubere Anordnung. `bekannt` (Set) beschränkt auf vorhandene Ansichten;
 * unbekannte Namen, Doppelte, leere Gruppen, falsche Anteile, zu tiefe Verschachtelung und unbekannte Richtungen werden bereinigt.
 * Ist nichts Brauchbares da, kommt die leere Anordnung.
 */
export function normalisieren(roh, bekannt = null) {
  const a = leer();
  if (!roh || typeof roh !== 'object' || Array.isArray(roh)) return a;
  const gesehen = new Set();
  const ids = new Set();
  const annehmen = (name) => {
    if (typeof name !== 'string' || name === '' || gesehen.has(name) || (bekannt && !bekannt.has(name))) return false;
    gesehen.add(name);
    return true;
  };
  const bau = (k, tiefe) => {
    if (!k || typeof k !== 'object' || tiefe > TIEFE_MAX) return null;
    if (k.art === 'gruppe') {
      const liste = Array.isArray(k.ansichten) ? k.ansichten.filter(annehmen) : [];
      if (liste.length === 0) return null;
      let id = typeof k.id === 'string' && /^g\d+$/.test(k.id) && !ids.has(k.id) ? k.id : null;
      if (id) ids.add(id);
      return { art: 'gruppe', id, ansichten: liste, aktiv: liste.includes(k.aktiv) ? k.aktiv : liste.at(-1) };
    }
    if (k.art === 'teilung' && RICHTUNGEN.includes(k.richtung) && Array.isArray(k.kinder)) {
      const kinder = [], anteile = [];
      k.kinder.forEach((c, i) => { const s = bau(c, tiefe + 1); if (s) { kinder.push(s); anteile.push(Array.isArray(k.anteile) ? k.anteile[i] : undefined); } });
      if (kinder.length === 0) return null;
      return { art: 'teilung', richtung: k.richtung, kinder, anteile };
    }
    return null;
  };
  a.wurzel = bau(roh.wurzel, 0);
  // fehlende oder doppelte Gruppen-Ids vergeben
  let n = 0;
  for (const g of gruppen(a)) if (g.id) n = Math.max(n, Number(g.id.slice(1)));
  for (const g of gruppen(a)) if (!g.id) g.id = 'g' + (++n);
  a.aktiveGruppe = typeof roh.aktiveGruppe === 'string' ? roh.aktiveGruppe : null;
  a.ausgelagert = Array.isArray(roh.ausgelagert) ? roh.ausgelagert.filter((x) => typeof x === 'string' && x !== '' && (!bekannt || bekannt.has(x)) && !gesehen.has(x)) : [];
  return abschliessen(a);
}

/** Text → Anordnung (kaputtes JSON gibt die leere Anordnung). */
export function ausText(text, bekannt = null) {
  try { return normalisieren(JSON.parse(text), bekannt); } catch { return leer(); }
}
export const alsText = (a) => JSON.stringify(a);

// ---------------------------------------------------------------------------------------------- Geometrie

/**
 * Rechnet die Anordnung in Rechtecke um (px, ganze Zahlen, lückenlos).
 * Ergebnis: { gruppen: [{ id, x, y, b, h, leiste: {x,y,b,h}, inhalt: {x,y,b,h} }], trenner: [{ pfad, index, richtung, x, y, b, h, start, gesamt }] }
 * trenner.start = Beginn der Teilung entlang der Achse, gesamt = Platz für die Kinder (ohne Trenner) entlang der Achse.
 */
export function layoutBerechnen(a, breite, hoehe, { leiste = LEISTE, trenner = TRENNER } = {}) {
  const ergebnis = { gruppen: [], trenner: [] };
  if (!a?.wurzel || !(breite > 0) || !(hoehe > 0)) return ergebnis;
  const rechne = (k, x, y, b, h, pfad) => {
    if (k.art === 'gruppe') {
      const lh = Math.max(0, Math.min(leiste, h));
      ergebnis.gruppen.push({ id: k.id, x, y, b, h, leiste: { x, y, b, h: lh }, inhalt: { x, y: y + lh, b, h: Math.max(0, h - lh) } });
      return;
    }
    const waag = k.richtung === 'nebeneinander';
    const n = k.kinder.length;
    const gesamt = Math.max(0, (waag ? b : h) - trenner * (n - 1));
    const start = waag ? x : y;
    let pos = 0, kumul = 0;
    k.kinder.forEach((c, i) => {
      kumul += k.anteile[i];
      const ende = i === n - 1 ? gesamt : Math.round(gesamt * kumul);
      const laenge = Math.max(0, ende - pos);
      if (waag) rechne(c, x + pos + i * trenner, y, laenge, h, [...pfad, i]);
      else rechne(c, x, y + pos + i * trenner, b, laenge, [...pfad, i]);
      if (i < n - 1) {
        const abstand = ende + i * trenner;
        ergebnis.trenner.push(waag
          ? { pfad, index: i, richtung: k.richtung, x: x + abstand, y, b: trenner, h, start, gesamt }
          : { pfad, index: i, richtung: k.richtung, x, y: y + abstand, b, h: trenner, start, gesamt });
      }
      pos = ende;
    });
  };
  rechne(a.wurzel, 0, 0, Math.round(breite), Math.round(hoehe), []);
  return ergebnis;
}

/** Anteil der Teilung (0…1) für einen Trenner, dessen Mitte bei Zeigerstelle (px, py) liegen soll; passt zu trennerSetzen. */
export function trennerPosition(t, px, py, trennerBreite = TRENNER) {
  const p = t.richtung === 'nebeneinander' ? px : py;
  if (!(t.gesamt > 0)) return 0.5;
  return (p - t.start - trennerBreite / 2 - t.index * trennerBreite) / t.gesamt;
}

// ---------------------------------------------------------------------------------------------- Andockziele

/** Die fünf Andockziele (Kreuz) in der Mitte einer Fläche r: mitte, links, rechts, oben, unten (je LEITPUNKT px, 4 px Abstand). */
export function leitpunkte(r, k = LEITPUNKT) {
  const cx = r.x + r.b / 2, cy = r.y + r.h / 2, d = k + 4;
  const quad = (x, y) => ({ x: Math.round(x - k / 2), y: Math.round(y - k / 2), b: k, h: k });
  return { mitte: quad(cx, cy), links: quad(cx - d, cy), rechts: quad(cx + d, cy), oben: quad(cx, cy - d), unten: quad(cx, cy + d) };
}

const innen = (r, x, y) => x >= r.x && x < r.x + r.b && y >= r.y && y < r.y + r.h;

/** Vorschau-Rechteck: wohin die Ansicht käme. */
function vorschauVon(r, ort) {
  switch (ort) {
    case 'links': return { x: r.x, y: r.y, b: Math.round(r.b / 2), h: r.h };
    case 'rechts': return { x: r.x + Math.round(r.b / 2), y: r.y, b: r.b - Math.round(r.b / 2), h: r.h };
    case 'oben': return { x: r.x, y: r.y, b: r.b, h: Math.round(r.h / 2) };
    case 'unten': return { x: r.x, y: r.y + Math.round(r.h / 2), b: r.b, h: r.h - Math.round(r.h / 2) };
    default: return { ...r };
  }
}

/**
 * Was passiert, wenn man eine Ansicht bei (x, y) loslässt? Ergebnis oder null (kein Ziel):
 *   { art: 'gruppe', gruppe, ort, index?, vorschau, marke? }   in/an einer Gruppe; marke = Einfügestelle in der Reiterleiste
 *   { art: 'rand', ort, vorschau }                              am Rand der ganzen Fläche (links, rechts, unten)
 * layout = layoutBerechnen(...); optionen: breite/hoehe der ganzen Fläche (für den Rand), tabs = { gruppenId: [{ x, b }] } (Reiter in der Leiste),
 * quelle = { gruppe, nurEine } (Herkunft der gezogenen Ansicht: keine sinnlosen Ziele).
 */
export function dropZiel(layout, x, y, { breite = null, hoehe = null, tabs = {}, quelle = null, randZone = RAND_ZONE } = {}) {
  const gr = layout?.gruppen ?? [];
  if (gr.length === 0) return null;
  const gesamt = gr.reduce((m, g) => ({ b: Math.max(m.b, g.x + g.b), h: Math.max(m.h, g.y + g.h) }), { b: 0, h: 0 });
  const B = breite ?? gesamt.b, H = hoehe ?? gesamt.h;
  if (x < 0 || y < 0 || x >= B || y >= H) return null;
  const eigeneEinzige = (g) => !!quelle && quelle.gruppe === g.id && quelle.nurEine;

  // Rand der ganzen Fläche (nur wenn es etwas zu teilen gibt: mit einer einzigen Einzel-Ansicht wäre es Unsinn)
  const nurEigene = gr.length === 1 && eigeneEinzige(gr[0]);
  if (!nurEigene) {
    const aufLeiste = gr.some((g) => innen(g.leiste, x, y));   // auf einer Reiterleiste gilt «Reiter einfügen», nicht «an den Rand»
    const randOrt = x < randZone ? 'links' : x >= B - randZone ? 'rechts' : (y >= H - randZone ? 'unten' : null);
    if (randOrt && !aufLeiste) {
      const anteil = 0.3;
      const v = randOrt === 'links' ? { x: 0, y: 0, b: Math.round(B * anteil), h: H }
        : randOrt === 'rechts' ? { x: B - Math.round(B * anteil), y: 0, b: Math.round(B * anteil), h: H }
          : { x: 0, y: H - Math.round(H * anteil), b: B, h: Math.round(H * anteil) };
      return { art: 'rand', ort: randOrt, vorschau: v };
    }
  }

  const g = gr.find((q) => innen(q, x, y)) ?? gr.find((q) => x >= q.x - TRENNER && x < q.x + q.b + TRENNER && y >= q.y - TRENNER && y < q.y + q.h + TRENNER);
  if (!g) return null;

  if (innen(g.leiste, x, y)) {
    const liste = tabs[g.id] ?? [];
    let index = 0;
    for (const t of liste) if (x > t.x + t.b / 2) index++;
    // eigene Gruppe mit einer einzigen Ansicht: Umsortieren ohne Wirkung
    if (eigeneEinzige(g)) return null;
    const mx = liste.length === 0 ? g.leiste.x : index < liste.length ? liste[index].x : liste[liste.length - 1].x + liste[liste.length - 1].b;
    return { art: 'gruppe', gruppe: g.id, ort: 'mitte', index, vorschau: { ...g.inhalt }, marke: { x: Math.round(mx), y: g.leiste.y, b: 3, h: g.leiste.h } };
  }

  // Andockziele (Kreuz)
  const lp = leitpunkte(g.inhalt);
  let ort = null;
  for (const o of ORTE) if (innen(lp[o], x, y)) { ort = o; break; }
  if (!ort) {
    const r = g.inhalt;
    const fx = (x - r.x) / r.b, fy = (y - r.y) / r.h;
    const dl = fx, dr = 1 - fx, dobe = fy, du = 1 - fy;
    const m = Math.min(dl, dr, dobe, du);
    ort = m >= 0.25 ? 'mitte' : m === dl ? 'links' : m === dr ? 'rechts' : m === dobe ? 'oben' : 'unten';
  }
  if (eigeneEinzige(g)) return null;   // die einzige Ansicht lässt sich nicht an der eigenen Gruppe andocken
  return { art: 'gruppe', gruppe: g.id, ort, vorschau: vorschauVon(g.inhalt, ort) };
}

/** Wendet ein Ziel aus dropZiel an. */
export function zielAnwenden(anordnung, name, ziel) {
  if (!ziel) return anordnung;
  if (ziel.art === 'rand') return verschieben(anordnung, name, { rand: ziel.ort });
  return verschieben(anordnung, name, { gruppe: ziel.gruppe, ort: ziel.ort, index: ziel.index });
}

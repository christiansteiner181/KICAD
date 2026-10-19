// Anordnung der Paletten als Daten (Auftrag W1, entwurf/fenster-revit.md): rein, ohne DOM und ohne Speicher – paletten.js zeichnet sie, zustand.js «merken» speichert sie (je Rechner).
// Alle Änderungen sind Funktionen, die eine neue Anordnung zurückgeben (die alte bleibt unverändert); ein ungültiger Wunsch gibt die alte Anordnung selbst zurück.
//
// Format der Anordnung (Schlüssel «paletten», Version 1):
//   {
//     version: 1,
//     bereiche: {
//       links:  { groesse: 280 | null, gruppen: [ Gruppe, … ] },     // groesse = Breite in px, null = Vorgabe
//       rechts: { groesse: null,       gruppen: [] },
//       unten:  { groesse: null,       gruppen: [ … ] },             // groesse = Höhe in px; die Gruppen stehen nebeneinander
//     },
//     schwebend: [ Gruppe + { x, y, b, h }, … ],                     // frei über der Zeichenfläche; die letzte liegt oben
//     versteckt: [ 'koordination', … ],                              // angemeldet, aber ausgeblendet
//     zuletzt:   { koordination: { bereich: 'unten' } | { schwebend: { x, y, b, h } } },   // wohin beim Wiedereinblenden
//   }
//   Gruppe = { paletten: [ 'eigenschaften', … ], aktiv: 'eigenschaften', anteil: 1 }
//     – mehrere Paletten in einer Gruppe = Reiter; anteil = Gewicht der Gruppe im Bereich (Höhe bzw. Breite im Verhältnis zu den anderen).
//
// Angemeldete Paletten (Registry): { id, ort: 'links'|'rechts'|'unten', vorgabeSichtbar: true, fluechtig: false, anteil: 1 }
//   ort = Vorgabeort; fluechtig = wird beim Laden nicht sichtbar wiederhergestellt (z. B. Paletten, die ein offenes Projekt brauchen).
//
// Funktionen: vorgabe · normalisieren · ort · sichtbar · zeigen · verbergen · aktivSetzen · loesen · andocken · schwebendSetzen · nachVorne ·
//             groesseSetzen · trennerSetzen · zuruecksetzen · sichtbareIds

export const VERSION = 1;
export const BEREICHE = ['links', 'rechts', 'unten'];
export const MIN_GROESSE = 100;
export const MAX_GROESSE = 4000;
export const MIN_FENSTER = 80;   // kleinste gemerkte Breite/Höhe einer schwebenden Palette

const kopie = (x) => JSON.parse(JSON.stringify(x));
const zahl = (n) => typeof n === 'number' && Number.isFinite(n);
const ganz = (n) => Math.round(n);
const bereichOk = (b) => BEREICHE.includes(b);

const gruppe = (paletten, anteil = 1, aktiv = paletten[0]) => ({ paletten: [...paletten], aktiv, anteil });
const leer = () => ({
  version: VERSION,
  bereiche: { links: { groesse: null, gruppen: [] }, rechts: { groesse: null, gruppen: [] }, unten: { groesse: null, gruppen: [] } },
  schwebend: [],
  versteckt: [],
  zuletzt: {},
});

const def = (registry, id) => registry.find((d) => d.id === id) ?? null;
const vorgabeOrt = (d) => (bereichOk(d?.ort) ? d.ort : 'links');

function geometrie(g) {
  if (!g || ![g.x, g.y, g.b, g.h].every(zahl)) return null;
  return { x: ganz(g.x), y: ganz(g.y), b: Math.max(MIN_FENSTER, ganz(g.b)), h: Math.max(MIN_FENSTER, ganz(g.h)) };
}

// ------------------------------------------------------------------------------------------------ Vorgabe und Prüfen

/** Die Vorgabe-Anordnung: jede sichtbare Palette in ihrem Vorgabeort, in der Reihenfolge der Registry; die übrigen sind versteckt. */
export function vorgabe(registry) {
  const l = leer();
  for (const d of registry) {
    if (d.vorgabeSichtbar === false) l.versteckt.push(d.id);
    else l.bereiche[vorgabeOrt(d)].gruppen.push(gruppe([d.id], zahl(d.anteil) && d.anteil > 0 ? d.anteil : 1));
  }
  return l;
}

function gruppePruefen(g, registry, gesehen) {
  if (!g || typeof g !== 'object' || !Array.isArray(g.paletten)) return null;
  const ids = [];
  for (const id of g.paletten) {
    if (typeof id !== 'string' || gesehen.has(id) || !def(registry, id)) continue;
    gesehen.add(id);
    ids.push(id);
  }
  if (ids.length === 0) return null;
  return { paletten: ids, aktiv: ids.includes(g.aktiv) ? g.aktiv : ids[0], anteil: zahl(g.anteil) && g.anteil > 0 && g.anteil < 1000 ? g.anteil : 1 };
}

/**
 * Macht aus gemerkten (möglicherweise ungültigen oder veralteten) Daten eine gültige Anordnung:
 * unbekannte und doppelte Paletten fallen weg, ungültige Zahlen werden verworfen, neu angemeldete Paletten kommen an ihren Vorgabeort.
 * Ist nichts Brauchbares da, gilt die Vorgabe. opt.fluechtigAusblenden: flüchtige Paletten werden versteckt (beim Laden).
 */
export function normalisieren(roh, registry, opt = {}) {
  if (!roh || typeof roh !== 'object' || Array.isArray(roh) || roh.version !== VERSION) return vorgabe(registry);
  const l = leer();
  const gesehen = new Set();
  for (const b of BEREICHE) {
    const r = roh.bereiche?.[b];
    if (!r || typeof r !== 'object') continue;
    l.bereiche[b].groesse = zahl(r.groesse) && r.groesse >= MIN_GROESSE && r.groesse <= MAX_GROESSE ? ganz(r.groesse) : null;
    for (const g of Array.isArray(r.gruppen) ? r.gruppen : []) {
      const ok = gruppePruefen(g, registry, gesehen);
      if (ok) l.bereiche[b].gruppen.push(ok);
    }
  }
  for (const g of Array.isArray(roh.schwebend) ? roh.schwebend : []) {
    const ok = gruppePruefen(g, registry, gesehen);
    const geom = geometrie(g);
    if (!ok) continue;
    if (!geom) { for (const id of ok.paletten) gesehen.delete(id); continue; }   // ohne Lage: die Paletten kommen unten wie neu angemeldet an ihren Ort
    l.schwebend.push({ ...ok, ...geom });
  }
  for (const id of Array.isArray(roh.versteckt) ? roh.versteckt : []) {
    if (typeof id === 'string' && !gesehen.has(id) && def(registry, id)) { gesehen.add(id); l.versteckt.push(id); }
  }
  if (roh.zuletzt && typeof roh.zuletzt === 'object') {
    for (const [id, z] of Object.entries(roh.zuletzt)) {
      if (!def(registry, id) || !z || typeof z !== 'object') continue;
      if (bereichOk(z.bereich)) l.zuletzt[id] = { bereich: z.bereich };
      else if (geometrie(z.schwebend)) l.zuletzt[id] = { schwebend: geometrie(z.schwebend) };
    }
  }
  // neu angemeldete Paletten (weder platziert noch versteckt)
  for (const d of registry) {
    if (gesehen.has(d.id)) continue;
    gesehen.add(d.id);
    if (d.vorgabeSichtbar === false) l.versteckt.push(d.id);
    else l.bereiche[vorgabeOrt(d)].gruppen.push(gruppe([d.id], zahl(d.anteil) && d.anteil > 0 ? d.anteil : 1));
  }
  let ergebnis = l;
  if (opt.fluechtigAusblenden) for (const d of registry) if (d.fluechtig && sichtbar(ergebnis, d.id)) ergebnis = verbergen(ergebnis, d.id);
  return ergebnis;
}

// ------------------------------------------------------------------------------------------------ Abfragen

/** Wo ist die Palette? { art: 'bereich', bereich, gruppe } | { art: 'schwebend', gruppe } | { art: 'versteckt' } | null (unbekannt). */
export function ort(l, id) {
  for (const b of BEREICHE) {
    const i = l.bereiche[b].gruppen.findIndex((g) => g.paletten.includes(id));
    if (i >= 0) return { art: 'bereich', bereich: b, gruppe: i };
  }
  const s = l.schwebend.findIndex((g) => g.paletten.includes(id));
  if (s >= 0) return { art: 'schwebend', gruppe: s };
  return l.versteckt.includes(id) ? { art: 'versteckt' } : null;
}

/** Ist die Palette eingeblendet (angedockt oder schwebend)? */
export function sichtbar(l, id) {
  const o = ort(l, id);
  return !!o && o.art !== 'versteckt';
}

/** Alle eingeblendeten Paletten (angedockt zuerst, dann schwebend). */
export function sichtbareIds(l) {
  const ids = [];
  for (const b of BEREICHE) for (const g of l.bereiche[b].gruppen) ids.push(...g.paletten);
  for (const g of l.schwebend) ids.push(...g.paletten);
  return ids;
}

/** Die Gruppe mit dieser Palette (Verweis in l) oder null. */
export function gruppeVon(l, id) {
  for (const b of BEREICHE) for (const g of l.bereiche[b].gruppen) if (g.paletten.includes(id)) return g;
  return l.schwebend.find((g) => g.paletten.includes(id)) ?? null;
}

// ------------------------------------------------------------------------------------------------ Entfernen und Einfügen (intern)

/** Nimmt die Paletten aus ihren Gruppen (leere Gruppen fallen weg) und aus «versteckt». Ändert l selbst; gibt die entfernten Gruppen zurück. */
function herausnehmen(l, ids) {
  const weg = new Set(ids);
  const entfernt = new Set();
  const aufraeumen = (liste) => {
    for (let i = liste.length - 1; i >= 0; i--) {
      const g = liste[i];
      if (!g.paletten.some((p) => weg.has(p))) continue;
      g.paletten = g.paletten.filter((p) => !weg.has(p));
      if (g.paletten.length === 0) { entfernt.add(g); liste.splice(i, 1); } else if (!g.paletten.includes(g.aktiv)) g.aktiv = g.paletten[0];
    }
  };
  for (const b of BEREICHE) aufraeumen(l.bereiche[b].gruppen);
  aufraeumen(l.schwebend);
  l.versteckt = l.versteckt.filter((p) => !weg.has(p));
  return entfernt;
}

const mittlererAnteil = (gruppen) => (gruppen.length === 0 ? 1 : gruppen.reduce((s, g) => s + g.anteil, 0) / gruppen.length);
const vorhanden = (l, registry, ids) => ids.length > 0 && ids.every((id) => typeof id === 'string' && (!registry || def(registry, id)));

// ------------------------------------------------------------------------------------------------ Einblenden und Ausblenden

/** Merkt, wo die Palette war, damit «zeigen» sie dorthin zurückbringt. */
function ortMerken(l, id) {
  const o = ort(l, id);
  if (!o || o.art === 'versteckt') return;
  if (o.art === 'bereich') l.zuletzt[id] = { bereich: o.bereich };
  else { const g = l.schwebend[o.gruppe]; l.zuletzt[id] = { schwebend: { x: g.x, y: g.y, b: g.b, h: g.h } }; }
}

/** Blendet die Palette aus; sie merkt sich ihren Ort. */
export function verbergen(l, id) {
  const o = ort(l, id);
  if (!o || o.art === 'versteckt') return l;
  const n = kopie(l);
  ortMerken(n, id);
  herausnehmen(n, [id]);
  n.versteckt.push(id);
  return n;
}

/** Blendet die Palette ein, am gemerkten Ort, sonst am Vorgabeort; ist sie schon da, wird sie nur zur aktiven (und schwebend nach vorn). */
export function zeigen(l, id, registry) {
  const d = def(registry, id);
  if (!d) return l;
  const o = ort(l, id);
  if (o && o.art !== 'versteckt') return aktivSetzen(l, id);
  const n = kopie(l);
  herausnehmen(n, [id]);
  const z = n.zuletzt[id];
  if (z?.schwebend && geometrie(z.schwebend)) n.schwebend.push({ ...gruppe([id]), ...geometrie(z.schwebend) });
  else {
    const b = bereichOk(z?.bereich) ? z.bereich : vorgabeOrt(d);
    n.bereiche[b].gruppen.push(gruppe([id], zahl(d.anteil) && d.anteil > 0 ? d.anteil : mittlererAnteil(n.bereiche[b].gruppen)));
  }
  return n;
}

/** Macht die Palette zur aktiven ihrer Gruppe (Reiter); eine schwebende Gruppe kommt nach vorn. */
export function aktivSetzen(l, id) {
  const g = gruppeVon(l, id);
  if (!g) return l;
  const n = kopie(l);
  const ng = gruppeVon(n, id);
  ng.aktiv = id;
  const i = n.schwebend.indexOf(ng);
  if (i >= 0) { n.schwebend.splice(i, 1); n.schwebend.push(ng); }
  return n;
}

// ------------------------------------------------------------------------------------------------ Lösen und Andocken

/**
 * Löst die Paletten (eine Gruppe oder ein einzelner Reiter) aus ihrem Platz und lässt sie als neue Gruppe schweben.
 * geom: { x, y, b, h }. Gleichzeitig angedockt oder verborgene Paletten werden zuerst herausgenommen.
 */
export function loesen(l, ids, geom, registry = null) {
  const g = geometrie(geom);
  if (!g || !vorhanden(l, registry, ids) || ids.some((id) => !ort(l, id))) return l;
  const n = kopie(l);
  herausnehmen(n, ids);
  n.schwebend.push({ ...gruppe(ids), ...g });
  return n;
}

/**
 * Dockt die Paletten an:
 *   ziel { bereich, index }            als neue Gruppe an Stelle index (0 … Anzahl) des Bereichs
 *   ziel { palette, lage }             lage 'davor' | 'danach' (neben bzw. über/unter der Gruppe der Palette; nur in einem Bereich)
 *                                      oder 'reiter' (als weitere Reiter in deren Gruppe, auch bei schwebenden Gruppen)
 * Ungültiges (unbekannte Paletten, Ziel in den Paletten selbst, davor/danach an einer schwebenden Gruppe) gibt l selbst zurück.
 */
export function andocken(l, ids, ziel, registry = null) {
  if (!vorhanden(l, registry, ids) || !ziel || ids.some((id) => !ort(l, id))) return l;
  const n = kopie(l);

  // Das Ziel wird auf Verweise in n abgebildet, bevor etwas entfernt wird (die Indizes verschieben sich sonst).
  let zielGruppe = null, nachbar = null, lage = null, bereich = null;
  if (typeof ziel.palette === 'string') {
    zielGruppe = gruppeVon(n, ziel.palette);
    if (!zielGruppe || !['davor', 'danach', 'reiter'].includes(ziel.lage)) return l;
    lage = ziel.lage;
    if (ids.every((id) => zielGruppe.paletten.includes(id)) && (lage === 'reiter' || zielGruppe.paletten.length === ids.length)) return l;   // auf die eigene Gruppe: nichts zu tun
    if (lage !== 'reiter') {
      const o = ort(n, ziel.palette);
      if (o.art !== 'bereich') return l;
      bereich = o.bereich;
    }
  } else if (bereichOk(ziel.bereich) && zahl(ziel.index)) {
    bereich = ziel.bereich;
    const liste = n.bereiche[bereich].gruppen;
    nachbar = liste[Math.max(0, Math.min(liste.length, Math.round(ziel.index)))] ?? null;
    lage = 'index';
  } else return l;

  const anteilZiel = zielGruppe?.anteil ?? 1;
  herausnehmen(n, ids);

  if (lage === 'reiter') {
    for (const id of ids) if (!zielGruppe.paletten.includes(id)) zielGruppe.paletten.push(id);
    zielGruppe.aktiv = ids[0];
    const i = n.schwebend.indexOf(zielGruppe);
    if (i >= 0) { n.schwebend.splice(i, 1); n.schwebend.push(zielGruppe); }
    return n;
  }

  const liste = n.bereiche[bereich].gruppen;
  if (lage === 'index') {
    // vor die gemerkte Nachbargruppe, falls sie noch da ist (sonst ans Ende)
    const pos = nachbar ? liste.indexOf(nachbar) : -1;
    liste.splice(pos < 0 ? liste.length : pos, 0, gruppe(ids, mittlererAnteil(liste)));
    return n;
  }
  const pos = liste.indexOf(zielGruppe);
  if (pos < 0) return l;
  const haelfte = Math.max(0.05, anteilZiel / 2);
  zielGruppe.anteil = haelfte;
  liste.splice(lage === 'davor' ? pos : pos + 1, 0, gruppe(ids, haelfte));
  return n;
}

// ------------------------------------------------------------------------------------------------ Grössen und Lage

/** Merkt Lage und Grösse einer schwebenden Gruppe. */
export function schwebendSetzen(l, id, geom) {
  const g = geometrie(geom);
  const o = ort(l, id);
  if (!g || o?.art !== 'schwebend') return l;
  const n = kopie(l);
  Object.assign(n.schwebend[o.gruppe], g);
  return n;
}

/** Legt die schwebende Gruppe nach vorn. */
export function nachVorne(l, id) {
  const o = ort(l, id);
  if (o?.art !== 'schwebend' || o.gruppe === l.schwebend.length - 1) return l;
  const n = kopie(l);
  const [g] = n.schwebend.splice(o.gruppe, 1);
  n.schwebend.push(g);
  return n;
}

/** Breite (links, rechts) bzw. Höhe (unten) des Bereichs in px; null = Vorgabe. */
export function groesseSetzen(l, bereich, wert) {
  if (!bereichOk(bereich)) return l;
  const neu = wert === null ? null : (zahl(wert) ? Math.max(MIN_GROESSE, Math.min(MAX_GROESSE, ganz(wert))) : undefined);
  if (neu === undefined || l.bereiche[bereich].groesse === neu) return l;
  const n = kopie(l);
  n.bereiche[bereich].groesse = neu;
  return n;
}

/**
 * Verschiebt den Trenner zwischen den Gruppen index und index + 1 eines Bereichs: anteilErste (0 … 1) ist der Anteil der ersten am gemeinsamen Gewicht
 * der beiden (zwischen 0,1 und 0,9). Die übrigen Gruppen bleiben, wie sie sind.
 */
export function trennerSetzen(l, bereich, index, anteilErste) {
  if (!bereichOk(bereich) || !zahl(anteilErste)) return l;
  const liste = l.bereiche[bereich].gruppen;
  if (!Number.isInteger(index) || index < 0 || index + 1 >= liste.length) return l;
  const n = kopie(l);
  const [a, b] = [n.bereiche[bereich].gruppen[index], n.bereiche[bereich].gruppen[index + 1]];
  const summe = a.anteil + b.anteil;
  const t = Math.max(0.1, Math.min(0.9, anteilErste));
  a.anteil = summe * t;
  b.anteil = summe * (1 - t);
  return n;
}

/**
 * «Paletten zurücksetzen»: die Vorgabe (jede Palette an ihrem Vorgabeort), wobei flüchtige Paletten ihre Sichtbarkeit behalten
 * (eine geöffnete Koordinationsliste bleibt offen, kommt aber an ihren Vorgabeort); die Grössen der Bereiche fallen auf die Vorgabe.
 */
export function zuruecksetzen(l, registry) {
  let n = vorgabe(registry);
  for (const d of registry) {
    if (!d.fluechtig || !l) continue;
    const jetzt = sichtbar(l, d.id);
    if (jetzt && !sichtbar(n, d.id)) n = zeigen(n, d.id, registry);
    else if (!jetzt && sichtbar(n, d.id)) n = verbergen(n, d.id);
  }
  return n;
}

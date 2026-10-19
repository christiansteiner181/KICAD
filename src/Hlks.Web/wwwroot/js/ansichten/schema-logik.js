// Schema-Ansicht (Modul 3, Teil C2): die reine Logik ohne DOM – in Node getestet (schema.test.mjs).
// Das Schema ist eine Sicht auf dasselbe Modell: die Anordnung rechnet der Kern (Abfrage «schema», Plangrafik der Ansicht);
// hier steht nur, was die Oberfläche braucht, um Klicks zu deuten und Befehle aufzubauen – Treffer auf Knoten und Linien,
// Rundung und Einrasten einer Hand-Lage, Zelle (Geschoss × Strang) unter dem Mauszeiger, Parameter der Befehle (schema_lage,
// schema_anordnen, verbinden, trennen, element_anlegen, element_aendern), die Verbindungen einer Linie zum Trennen und die
// Einträge der Kontextmenüs. Keine Fachlogik: ob etwas verbunden, getrennt oder angelegt werden darf, sagt der Kern.
//
// Koordinaten: Papier-mm der Schema-Ansicht, x nach rechts, y nach oben (wie die Abfrage «schema» und die Plangrafik).
// Annahmen (Anzeige, keine Norm): Trefferfläche eines Knotens ±4.5 × ±3.5 mm (Symbole sind höchstens 8 × 6 mm), Hand-Lagen
// werden auf 0.5 mm gerundet und rasten innerhalb von 8 Bildschirmpixeln auf Stranglinien und Achsen anderer Knoten ein.

export const KNOTEN_HALB_X = 4.5;
export const KNOTEN_HALB_Y = 3.5;
export const RUNDUNG_MM = 0.5;
export const FANG_PX = 8;
export const TREFFER_PX = 5;
/** Klassen, die der Kern nicht als Knoten, sondern als Teil einer Schemalinie zeichnet (SchemaGraph.IstLinienelement). */
export const LINIENKLASSEN = Object.freeze(['Rohr', 'Kanal', 'Bogen', 'Reduktion']);

const gueltig = (v) => Number.isFinite(Number(v));
const zahlOder = (v, d = 0) => (gueltig(v) ? Number(v) : d);

// ---------------------------------------------------------------------------------------------- Rundung und Einrasten

/** Rundet auf ein Vielfaches von «schritt» (Vorgabe 0.5 mm); nie -0, nie Rundungsrauschen. */
export function runden(v, schritt = RUNDUNG_MM) {
  const r = Math.round(Number(v) / schritt) * schritt;
  return Math.abs(r) < 1e-9 ? 0 : Number(r.toFixed(4));
}

/**
 * Lage eines verschobenen Knotens: rastet auf die x-Linie eines senkrechten Strangs bzw. die x/y-Achse eines anderen Knotens ein,
 * wenn sie näher als «toleranz» (mm) liegen, sonst gerundet. Liefert { x, y, fangX, fangY }; fang* = { art, wert, ... } oder null.
 * daten = Antwort der Abfrage «schema»; ausser = Element-Id des bewegten Knotens.
 */
export function lageRunden(p, { daten = null, ausser = null, toleranz = 1.5 } = {}) {
  let x = zahlOder(p?.x), y = zahlOder(p?.y);
  let fangX = null, fangY = null;
  if (daten) {
    let besteX = toleranz, besteY = toleranz;
    for (const s of daten.spalten ?? []) {
      if (s.art !== 'senkrecht') continue;
      const d = Math.abs(x - s.linie);
      if (d <= besteX) { besteX = d; fangX = { art: 'strang', wert: s.linie, kennzeichen: s.kennzeichen }; }
    }
    for (const k of daten.knoten ?? []) {
      if (k.element === ausser) continue;
      const dx = Math.abs(x - k.x), dy = Math.abs(y - k.y);
      if (dx < besteX) { besteX = dx; fangX = { art: 'knoten', wert: k.x, kennzeichen: k.kennzeichen }; }   // bei Gleichstand gewinnt die Stranglinie
      if (dy < besteY) { besteY = dy; fangY = { art: 'knoten', wert: k.y, kennzeichen: k.kennzeichen }; }
    }
  }
  return { x: fangX ? fangX.wert : runden(x), y: fangY ? fangY.wert : runden(y), fangX, fangY };
}

// ---------------------------------------------------------------------------------------------- Treffer

export const knotenIndex = (daten) => new Map((daten?.knoten ?? []).map((k) => [k.element, k]));

/** Die Schemalinie, zu der ein Linienelement (Leitung, Bogen, Reduktion) gehört. */
export function linieVonElement(daten, id) {
  if (!id) return null;
  return (daten?.linien ?? []).find((l) => (l.elemente ?? []).includes(id)) ?? null;
}

/** Abstand eines Punkts zu einer Strecke (mm) mit Lotfusspunkt und Anteil t (0…1). */
export function abstandZurStrecke(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
  const t = l2 === 0 ? 0 : Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l2));
  const x = ax + t * dx, y = ay + t * dy;
  return { abstand: Math.hypot(px - x, py - y), t, x, y };
}

/**
 * Knoten, dessen Symbolfläche (±KNOTEN_HALB_X/Y, mindestens «toleranz») den Punkt enthält; bei mehreren der nächste.
 * bevorzugt: Id eines Knotens (das angeklickte Teil). Liegt er in Reichweite, gewinnt er – auch wenn ein anderer gleich nah oder
 * näher liegt. Das braucht der Kern: Knoten verschiedener Systeme können genau aufeinander liegen (z. B. die Etagen-T-Stücke von
 * Schmutz- und Kaltwasser am selben Endgerät, beide auf der Etagenleitung unter dem Gerät); der Zeiger meint dann den obenliegenden.
 */
export function knotenBei(knoten, x, y, toleranz = 0, bevorzugt = null) {
  let beste = null, bestD = Infinity;
  for (const k of knoten ?? []) {
    const hx = Math.max(KNOTEN_HALB_X, toleranz), hy = Math.max(KNOTEN_HALB_Y, toleranz);
    const dx = Math.abs(x - k.x), dy = Math.abs(y - k.y);
    if (dx > hx || dy > hy) continue;
    if (bevorzugt && k.element === bevorzugt) return k;
    const d = Math.hypot(dx, dy);
    if (d < bestD) { bestD = d; beste = k; }
  }
  return beste;
}

/** Die Linie, die dem Punkt innerhalb von «toleranz» (mm) am nächsten liegt: { linie, abstand, segment, x, y } oder null. */
export function naechsteLinie(daten, x, y, toleranz) {
  let beste = null;
  for (const l of daten?.linien ?? []) {
    const pts = l.punkte ?? [];
    for (let i = 1; i < pts.length; i++) {
      const a = abstandZurStrecke(x, y, pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1]);
      if (a.abstand <= toleranz && (!beste || a.abstand < beste.abstand)) beste = { linie: l, abstand: a.abstand, segment: i - 1, x: a.x, y: a.y };
    }
  }
  return beste;
}

/**
 * Was liegt unter dem Mauszeiger? p = { x, y } in Papier-mm, toleranz in mm (Bildschirmpixel / Zoom), domId = data-id des
 * angeklickten SVG-Teils (oder null). Reihenfolge: Knoten (Symbolfläche) › Linienelement laut Teil › Linie nach Geometrie ›
 * anderes Teil (Geschoss, Strang) › nichts.
 * Ergebnis: { art: 'knoten', knoten, id } | { art: 'linie', linie, id, element, nurGeometrie? } | { art: 'sonst', id } | { art: 'leer' }.
 */
export function treffer(daten, p, toleranz, domId = null) {
  const k = knotenBei(daten?.knoten, p.x, p.y, toleranz, domId);
  if (k) return { art: 'knoten', knoten: k, id: k.element };
  if (domId) {
    const kd = (daten?.knoten ?? []).find((x) => x.element === domId);
    if (kd) return { art: 'knoten', knoten: kd, id: kd.element };
    const l = linieVonElement(daten, domId);
    if (l) return { art: 'linie', linie: l, id: domId, element: domId };
  }
  const n = naechsteLinie(daten, p.x, p.y, toleranz);
  if (n) {
    const e = n.linie.elemente?.[0] ?? null;
    return { art: 'linie', linie: n.linie, id: e, element: e, nurGeometrie: true, ort: { x: n.x, y: n.y } };
  }
  if (domId) return { art: 'sonst', id: domId };
  return { art: 'leer' };
}

/** Zelle (Spalte × Zeile) unter dem Punkt oder null (ausserhalb des Rasters). */
export function zelleBei(daten, x, y) {
  const spalte = (daten?.spalten ?? []).find((s) => x >= s.x && x < s.x + s.breite);
  const zeile = (daten?.zeilen ?? []).find((z) => y >= z.y && y < z.y + z.hoehe);
  return spalte && zeile ? { spalte, zeile } : null;
}

/** Text zur Zelle für die Statusleiste: «Geschoss EG × Strang SW-F1». */
export function zelleText(zelle) {
  if (!zelle) return 'ausserhalb des Rasters';
  const g = zelle.zeile.zone ? 'ohne Geschoss' : `Geschoss ${zelle.zeile.kennzeichen}`;
  const s = zelle.spalte.art === 'ohneStrang' ? `${zelle.spalte.system ?? ''} ohne Strang`.trim()
    : zelle.spalte.art === 'ohneSystem' ? 'ohne System' : `Strang ${zelle.spalte.kennzeichen}`;
  return `${g} × ${s}`;
}

/** Rechteck {minx, miny, maxx, maxy} um einen Knoten oder eine Linie (für «Im Schema zeigen»). */
export function umrandung(daten, id) {
  const k = (daten?.knoten ?? []).find((x) => x.element === id);
  if (k) return { minx: k.x - 12, miny: k.y - 10, maxx: k.x + 12, maxy: k.y + 10 };
  const l = linieVonElement(daten, id);
  if (!l || !(l.punkte ?? []).length) return null;
  const xs = l.punkte.map((q) => q[0]), ys = l.punkte.map((q) => q[1]);
  return { minx: Math.min(...xs) - 8, miny: Math.min(...ys) - 8, maxx: Math.max(...xs) + 8, maxy: Math.max(...ys) + 8 };
}

/** Wo zeigt eine Schema-Antwort dieses Element? 'knoten' | 'linie' | null. */
export function zeigtElement(daten, id) {
  if (!id || !daten) return null;
  if ((daten.knoten ?? []).some((k) => k.element === id)) return 'knoten';
  if ((daten.linien ?? []).some((l) => (l.elemente ?? []).includes(id))) return 'linie';
  return null;
}

/** Nicht platzierte Knoten (nur im Schema vorhanden) und Zahl der nicht platzierten Linienelemente: { knoten[], linien } */
export function nichtPlatziert(daten, elementVonId = null) {
  const knoten = (daten?.knoten ?? []).filter((k) => k.platziert === false);
  let linien = 0;
  if (elementVonId) for (const l of daten?.linien ?? []) for (const id of l.elemente ?? []) { const e = elementVonId(id); if (e && !e.geometrie) linien++; }
  return { knoten, linien };
}

// ---------------------------------------------------------------------------------------------- Parameter der Befehle

const anschlussTeil = (e, a) => (a ? { element: e, anschluss: a } : { element: e });

/** schema_lage mit x, y (Papier-mm, gerundet). */
export function lageParameter(ansicht, element, x, y) {
  return { ansicht, element, x: runden(x), y: runden(y) };
}
/** schema_lage mit zuruecksetzen = true (Hand-Lage entfernen). */
export function lageZuruecksetzenParameter(ansicht, element) {
  return { ansicht, element, zuruecksetzen: true };
}
/** schema_anordnen; alle = true entfernt alle Hand-Lagen, sonst räumt es nur die verwaisten auf. */
export function anordnenParameter(ansicht, alle = false) {
  return alle ? { ansicht, alle: true } : { ansicht };
}
/**
 * verbinden zweier Knoten. Die Elemente gehen immer als Objekt { element, anschluss? } (Kennzeichen wie «WC-E.02» enthalten
 * einen Punkt – als Text «KZ.anschluss» würde der Kern ihn falsch trennen). Ohne Anschlussname wählt der Kern den freien, passenden.
 */
export function verbindenParameter(von, nach, vonAnschluss = null, nachAnschluss = null) {
  return { von: anschlussTeil(von, vonAnschluss), nach: anschlussTeil(nach, nachAnschluss) };
}
/** trennen über die Id der Verbindung. */
export function trennenParameter(verbindungId) { return { verbindung: verbindungId }; }

/** Kandidaten «a–b, c–d» aus der Ablehnung «Mehrere passende Anschlusspaare zwischen X und Y: a–b, c–d.» (sonst null). */
export function mehrfachKandidaten(fehlertext) {
  const t = String(fehlertext ?? '');
  if (!/^Mehrere passende Anschlusspaare/.test(t)) return null;
  const m = t.match(/:\s*(.+?)\.?\s*$/);
  if (!m) return [];
  const paare = [];
  for (const teil of m[1].split(/,\s*/)) {
    const s = teil.split('–');
    if (s.length === 2 && s[0].trim() && s[1].trim()) paare.push([s[0].trim(), s[1].trim()]);
  }
  return paare;
}

// ---------------------------------------------------------------------------------------------- Element einfügen

/**
 * Welche Klassen lassen sich im Schema als Knoten einfügen? katalog = zustand.katalog.klassen (Kurzform), rollen = Map
 * Klasse → Rolle im Netz (aus der vollen Klassendefinition, Abfrage katalog { klasse }). Endgerät = Rolle verbraucher, Gerät =
 * quelle/senke/zentrale, Armatur = Basis Armatur, Formteil = Basis Formteil (ohne Bogen und Reduktion: sie sind Teil einer Linie).
 * Ergebnis: { endgeraete[], geraete[], armaturen[], formteile[] } mit { name, art, beschreibung, familie }.
 */
export function einfuegbareKlassen(katalog, rollen) {
  const aus = { endgeraete: [], geraete: [], armaturen: [], formteile: [] };
  for (const k of katalog ?? []) {
    if (k.abstrakt || LINIENKLASSEN.includes(k.name)) continue;
    const rolle = rollen?.get ? rollen.get(k.name) : rollen?.[k.name];
    const art = knotenArt(k, rolle);
    if (!art) continue;
    const eintrag = { name: k.name, art, beschreibung: k.beschreibung ?? '', familie: k.familie === true };
    (art === 'endgeraet' ? aus.endgeraete : art === 'geraet' ? aus.geraete : art === 'armatur' ? aus.armaturen : aus.formteile).push(eintrag);
  }
  for (const l of Object.values(aus)) l.sort((a, b) => a.name.localeCompare(b.name, 'de'));
  return aus;
}

/** Knotenart einer Klasse laut Schemagraph: endgeraet | geraet | armatur | formteil | null (kein Knoten). */
export function knotenArt(klasse, rolle) {
  if (!klasse || LINIENKLASSEN.includes(klasse.name)) return null;
  if (rolle === 'verbraucher') return 'endgeraet';
  if (rolle === 'quelle' || rolle === 'senke' || rolle === 'zentrale') return 'geraet';
  if (klasse.basis === 'Armatur') return 'armatur';
  if (klasse.basis === 'Formteil') return 'formteil';
  return null;
}

/** Familien (Abfrage «familien»), die sich als Knoten einfügen lassen: gültig, mit Typen, Kategorie ein Knoten. */
export function einfuegbareFamilien(familien, klassenInfo) {
  const aus = [];
  for (const f of familien ?? []) {
    if (f.gueltig === false || !(f.typen ?? []).length) continue;
    const art = klassenInfo?.get?.(f.kategorie);
    if (!art) continue;
    aus.push({ kennzeichen: f.kennzeichen, name: f.name ?? f.kennzeichen, kategorie: f.kategorie, art, typen: [...f.typen] });
  }
  return aus.sort((a, b) => String(a.name).localeCompare(String(b.name), 'de'));
}

/**
 * Zuordnung aus der Zelle: geschoss aus der Zeile, teilVon (Strang) aus der Spalte; eine Zone «ohne Strang» gibt das System (system),
 * «ohne System» nichts. Annahme: auch Endgeräte und Geräte bekommen den Strang der Spalte (teilVon; der Kern übernimmt das System des
 * Strangs und ordnet sie in dessen Spalte ein, ohne dass dafür eine Zone «ohne Strang» entsteht, die das ganze Raster verschiebt).
 * Das gilt für alle Arten (endgeraet, geraet, armatur, formteil).
 */
export function zuordnungAusZelle(zelle, art) {
  const z = {};
  if (!zelle) return z;
  if (!zelle.zeile.zone && zelle.zeile.kennzeichen) z.geschoss = zelle.zeile.kennzeichen;
  const s = zelle.spalte;
  const strang = s.art === 'senkrecht' || s.art === 'waagrecht';
  if (strang && s.kennzeichen) z.teilVon = s.kennzeichen;
  else if (s.system) z.system = s.system;
  return z;
}

/** element_anlegen für ein Klassenelement ohne Geometrie (nicht platziert). */
export function klasseAnlegenParameter(klasse, zelle, art, { kennzeichen = '', name = '' } = {}) {
  const p = { klasse, ...zuordnungAusZelle(zelle, art) };
  if (kennzeichen.trim()) p.kennzeichen = kennzeichen.trim();
  if (name.trim()) p.name = name.trim();
  return p;
}

/**
 * element_anlegen für ein Familienelement ohne Geometrie: familie_platzieren verlangt einen Punkt; ohne Punkt legt man das Element
 * der Kategorie mit bezuege.familie und familientyp an (der Kern führt Anschlüsse und Kennwerte nach, Modul 3 B1).
 */
export function familieAnlegenParameter(familie, typ, zelle, art, opt = {}) {
  const p = klasseAnlegenParameter(familie.kategorie, zelle, art, opt);
  p.eigenschaften = { familientyp: typ };
  p.bezuege = { familie: familie.kennzeichen };
  return p;
}

/** Kennzeichen des neu angelegten Elements aus dem Ergebnis eines Befehls (aenderungen: art «angelegt»). */
export function angelegtesElement(ergebnis, klasse = null) {
  const a = (ergebnis?.aenderungen ?? []).find((x) => x.art === 'angelegt' && (!klasse || x.klasse === klasse)) ?? (ergebnis?.aenderungen ?? []).find((x) => x.art === 'angelegt');
  return a?.kennzeichen ?? null;
}

/** element_aendern: nicht platziertes Punktelement im Grundriss auf einen Punkt setzen (x, y in mm; z folgt der Kote des Geschosses). */
export function platzierenParameter(element, punkt) {
  return { element, geometrie: { art: 'punkt', punkte: [[Math.round(punkt[0] * 10) / 10, Math.round(punkt[1] * 10) / 10]] } };
}

/** element_anlegen für eine Schema-Ansicht (Strangschema). Gewerke und Systeme (Kennzeichen) nur, wenn gewählt. */
export function schemaAnlegenParameter({ kennzeichen, name, gewerke = [], systeme = [], detailgrad = 'mittel' }) {
  const eig = { art: 'schema', schemaart: 'strangschema', detailgrad };
  if (gewerke.length) eig.gewerke = [...gewerke];
  if (systeme.length) eig.systeme = [...systeme];
  return { klasse: 'Ansicht', kennzeichen, name, eigenschaften: eig };
}
/** Vorschlag für Kennzeichen und Name einer neuen Schema-Ansicht. */
export function schemaVorschlag(systeme = []) {
  return systeme.length === 1 ? { kennzeichen: `AN-SCHEMA-${systeme[0]}`, name: `Schema ${systeme[0]}` } : { kennzeichen: 'AN-SCHEMA', name: 'Schema' };
}

// ---------------------------------------------------------------------------------------------- Verbindungen zum Trennen

const kleinKz = (kz) => String(kz ?? '').toLowerCase();
const gleichKz = (a, b) => kleinKz(a) === kleinKz(b);

/** Verbindungen eines Elements (zustand.verbindungen mit Kennzeichen): [{ id, anschluss, gegen, gegenAnschluss }] in Anschlussreihenfolge der Daten. */
export function verbindungenVon(kz, verbindungen) {
  const aus = [];
  for (const v of verbindungen ?? []) {
    if (gleichKz(v.von, kz)) aus.push({ id: v.id, anschluss: v.vonAnschluss, gegen: v.nach, gegenAnschluss: v.nachAnschluss });
    else if (gleichKz(v.nach, kz)) aus.push({ id: v.id, anschluss: v.nachAnschluss, gegen: v.von, gegenAnschluss: v.vonAnschluss });
  }
  return aus;
}

const endeText = (kz, anschluss) => (anschluss ? `${kz}.${anschluss}` : kz);

/**
 * Die Verbindungen einer Schemalinie in Fliessrichtung: Knoten von › Element 1 › … › Element n › Knoten nach (offene Enden haben
 * keine Verbindung). Zwei direkt verbundene Knoten (Linie ohne Elemente) haben die Verbindung der Linie.
 * kzVonId: Funktion Element-Id → Kennzeichen. Ergebnis: [{ id, a, b, text, mit: [Element-Ids] }].
 */
export function linienVerbindungen(linie, verbindungen, kzVonId) {
  if (!linie) return [];
  const kette = [];   // Kennzeichen der Glieder in Fliessrichtung (null = offenes Ende)
  const ids = [];
  const vonKz = linie.von ? kzVonId(linie.von) : null;
  const nachKz = linie.nach ? kzVonId(linie.nach) : null;
  if ((linie.elemente ?? []).length === 0) {
    if (!linie.verbindung) return [];
    return [{ id: linie.verbindung, a: endeText(vonKz, linie.vonAnschluss), b: endeText(nachKz, linie.nachAnschluss), text: `${endeText(vonKz, linie.vonAnschluss)} ↔ ${endeText(nachKz, linie.nachAnschluss)}`, mit: [linie.von, linie.nach].filter(Boolean) }];
  }
  kette.push(vonKz); ids.push(linie.von ?? null);
  for (const e of linie.elemente) { kette.push(kzVonId(e)); ids.push(e); }
  kette.push(nachKz); ids.push(linie.nach ?? null);
  const aus = [];
  for (let i = 1; i < kette.length; i++) {
    const a = kette[i - 1], b = kette[i];
    if (!a || !b) continue;
    let treffer = (verbindungen ?? []).filter((v) => (gleichKz(v.von, a) && gleichKz(v.nach, b)) || (gleichKz(v.von, b) && gleichKz(v.nach, a)));
    // mehrere Verbindungen zwischen denselben Elementen (z. B. T-Stück mit zwei Anschlüssen an derselben Leitung): die Anschlüsse am Rand der Linie unterscheiden
    if (treffer.length > 1) {
      const rand = i === 1 ? linie.vonAnschluss : i === kette.length - 1 ? linie.nachAnschluss : null;
      const enger = rand ? treffer.filter((v) => (gleichKz(v.von, a) ? v.vonAnschluss : v.nachAnschluss) === rand || (gleichKz(v.von, b) ? v.vonAnschluss : v.nachAnschluss) === rand) : [];
      if (enger.length >= 1) treffer = enger;
    }
    const v = treffer[0];
    if (!v) continue;
    const aA = gleichKz(v.von, a) ? v.vonAnschluss : v.nachAnschluss;
    const bA = gleichKz(v.von, a) ? v.nachAnschluss : v.vonAnschluss;
    aus.push({ id: v.id, a: endeText(a, aA), b: endeText(b, bA), text: `${endeText(a, aA)} ↔ ${endeText(b, bA)}`, mit: [ids[i - 1], ids[i]].filter(Boolean) });
  }
  return aus;
}

/**
 * Trennoptionen beim Klick auf ein Element der Linie: zuerst die Verbindungen, die dieses Element berühren (davor, danach), dann
 * die übrigen der Linie. Klickt man die Linie ohne bestimmtes Element, kommen alle in Fliessrichtung.
 */
export function trennOptionen(linie, verbindungen, kzVonId, elementId = null) {
  const alle = linienVerbindungen(linie, verbindungen, kzVonId);
  if (!elementId) return alle;
  const nah = alle.filter((o) => o.mit.includes(elementId));
  return nah.length ? nah : alle;
}

// ---------------------------------------------------------------------------------------------- Kontextmenüs
// Einträge: { titel, aktion (Id einer Aktion), param, aktiv, hinweis } oder { trenner: true }. Die Oberfläche macht daraus
// ein Kontextmenü; ausgeführt wird immer eine Aktion (aktionen-schema.js).

const MAX_TRENNEN = 8;

/**
 * Menü auf einem Knoten. ctx: { knoten, element (Darstellung oder null), verbindungen, kzVonId, tool }.
 */
export function knotenMenue({ knoten, element = null, verbindungen = [] }) {
  const kz = knoten.kennzeichen;
  const e = { element: knoten.element, kennzeichen: kz };
  const platziert = knoten.platziert !== false;
  const aus = [];
  aus.push({
    titel: 'Im Grundriss zeigen', aktion: 'schema.im-grundriss-zeigen', param: e,
    aktiv: platziert && !!element?.geschoss,
    hinweis: !platziert ? `${kz} ist nicht platziert (steht nur im Schema) – zuerst im Grundriss platzieren.` : 'Dem Element fehlt das Geschoss.',
  });
  if (!platziert) {
    aus.push({
      titel: 'Im Grundriss platzieren …', aktion: 'schema.im-grundriss-platzieren', param: e,
      aktiv: !!element?.geschoss, hinweis: 'Zuerst das Geschoss setzen (Eigenschaften).',
    });
  }
  aus.push({ trenner: true });
  aus.push({
    titel: 'Automatische Lage', aktion: 'schema.lage-zuruecksetzen', param: e,
    aktiv: knoten.manuell === true, hinweis: `${kz} hat keine Hand-Lage; es gilt schon die automatische Anordnung.`,
  });
  aus.push({ titel: 'Verbinden …', aktion: 'schema.verbinden', param: { von: knoten.element } });
  const vb = verbindungenVon(kz, verbindungen);
  for (const v of vb.slice(0, MAX_TRENNEN)) {
    aus.push({ titel: `Trennen: ${kz}.${v.anschluss} ↔ ${endeText(v.gegen, v.gegenAnschluss)}`, aktion: 'schema.trennen-verbindung', param: { verbindung: v.id } });
  }
  aus.push({ trenner: true });
  aus.push({ titel: 'Löschen …', aktion: 'aendern.loeschen', param: { element: kz } });
  return aus;
}

/** Menü auf einer Linie. ctx: { linie, elementId (angeklicktes Teil oder null), element (Darstellung), optionen (trennOptionen) }. */
export function linieMenue({ elementId = null, element = null, optionen = [] }) {
  const aus = [];
  for (const o of optionen.slice(0, MAX_TRENNEN)) aus.push({ titel: `Trennen: ${o.text}`, aktion: 'schema.trennen-verbindung', param: { verbindung: o.id } });
  if (optionen.length === 0) aus.push({ titel: 'Trennen', aktion: 'schema.trennen-verbindung', param: {}, aktiv: false, hinweis: 'Diese Linie hat keine Verbindung, die sich trennen lässt (offenes Ende).' });
  if (elementId && element) {
    aus.push({ trenner: true });
    const platziert = !!element.geometrie;
    aus.push({
      titel: 'Im Grundriss zeigen', aktion: 'schema.im-grundriss-zeigen', param: { element: elementId, kennzeichen: element.kennzeichen },
      aktiv: platziert && !!element.geschoss,
      hinweis: !platziert ? `${element.kennzeichen} ist nicht platziert (steht nur im Schema).` : 'Dem Element fehlt das Geschoss.',
    });
    aus.push({ titel: 'Löschen …', aktion: 'aendern.loeschen', param: { element: element.kennzeichen } });
  }
  return aus;
}

/** Menü auf leerer Fläche; zelle = Zelle unter dem Zeiger (oder null). */
export function leerMenue({ zelle = null, hatHandLagen = true } = {}) {
  return [
    { titel: zelle ? `Element einfügen … (${zelleText(zelle)})` : 'Element einfügen …', aktion: 'schema.einfuegen', param: {} },
    { trenner: true },
    { titel: 'Schema neu anordnen …', aktion: 'schema.neu-anordnen', param: {}, aktiv: hatHandLagen, hinweis: 'Das Schema hat keine Hand-Lagen; es ist ganz automatisch angeordnet.' },
    { titel: 'Zoom alles', aktion: 'schema.zoom-alles', param: {} },
  ];
}

/**
 * Menü im Grundriss oder 3D für ein Element: «Im Schema zeigen». treffer = Liste { ansicht (Kennzeichen), name, wo } der
 * Schema-Ansichten, die das Element zeigen; keine → ausgegraut mit Hinweis.
 */
export function grundrissMenue({ element, treffer = [], schemaAnzahl = 0 }) {
  if (treffer.length === 0) {
    return [{
      titel: 'Im Schema zeigen', aktion: 'schema.im-schema-zeigen', param: { element: element?.kennzeichen }, aktiv: false,
      hinweis: schemaAnzahl === 0 ? 'Es gibt keine Schema-Ansicht (Register Ansicht › Schema).' : 'Keine Schema-Ansicht zeigt dieses Element (Gewerke, Systeme und ausgeblendete Klassen der Schema-Ansicht prüfen).',
    }];
  }
  if (treffer.length === 1) return [{ titel: 'Im Schema zeigen', aktion: 'schema.im-schema-zeigen', param: { element: element.kennzeichen, ansicht: treffer[0].ansicht } }];
  return treffer.map((t) => ({ titel: `Im Schema zeigen: ${t.name || t.ansicht}`, aktion: 'schema.im-schema-zeigen', param: { element: element.kennzeichen, ansicht: t.ansicht } }));
}

// ---------------------------------------------------------------------------------------------- Grundriss zum Zeigen und Platzieren

/**
 * Welcher Grundriss zeigt ein Element? ansichten = Darstellungen der Ansichten, el = Darstellung des Elements, offen =
 * Kennzeichen der offenen Ansichten. Gleiches Geschoss, keine Vorlage; vorn die offenen, die das Gewerk zeigen.
 * Ergebnis { ansicht, sichtbar } (sichtbar = Gewerk und Klasse sind in der Ansicht nicht ausgefiltert) oder null.
 */
export function grundrissFuer(ansichten, el, offen = []) {
  if (!el?.geschoss) return null;
  const kandidaten = (ansichten ?? []).filter((a) => a.klasse === 'Ansicht' && a.werte?.art === 'grundriss' && a.werte?.vorlage !== true && a.geschoss === el.geschoss);
  if (kandidaten.length === 0) return null;
  const sichtbar = (a) => {
    const g = a.werte?.gewerke;
    const aus = a.werte?.klassenAusblenden;
    if (Array.isArray(aus) && (aus.includes(el.klasse) || (el.basis && aus.includes(el.basis)))) return false;
    return !(Array.isArray(g) && g.length && el.gewerk && el.gewerk !== 'allgemein' && !g.includes(el.gewerk));
  };
  const rang = (a) => (sichtbar(a) ? 0 : 2) + (offen.includes(a.kennzeichen) ? 0 : 1);
  const beste = [...kandidaten].sort((a, b) => rang(a) - rang(b) || String(a.kennzeichen).localeCompare(String(b.kennzeichen), 'de', { numeric: true }))[0];
  return { ansicht: beste, sichtbar: sichtbar(beste) };
}

/** Rechteck in Modell-mm für «Im Grundriss zeigen»: um einen Punkt 3 m, sonst die Begrenzung der Geometrie. */
export function grundrissAusschnitt(el, halb = 1500) {
  const pts = (el?.geometrie?.punkte ?? []).filter((p) => Array.isArray(p) && gueltig(p[0]) && gueltig(p[1]));
  if (!pts.length) return null;
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  const b = { minx: Math.min(...xs), miny: Math.min(...ys), maxx: Math.max(...xs), maxy: Math.max(...ys) };
  if (b.maxx - b.minx < 2 * halb) { const m = (b.minx + b.maxx) / 2; b.minx = m - halb; b.maxx = m + halb; }
  if (b.maxy - b.miny < 2 * halb) { const m = (b.miny + b.maxy) / 2; b.miny = m - halb; b.maxy = m + halb; }
  return b;
}

/** Eine Klasse mit Geometrie «punkt»: nur dann lässt sie sich mit einem Klick im Grundriss platzieren. */
export const istPunktklasse = (klassenDef) => klassenDef?.geometrie === 'punkt';

// ---------------------------------------------------------------------------------------------- Banner

/** Zusammenfassung der Hinweise des Kerns für das Banner: { anzahl, erste, alle[] }. */
export function hinweisBanner(daten) {
  const alle = (daten?.hinweise ?? []).filter(Boolean);
  return { anzahl: alle.length, erste: alle[0] ?? '', alle };
}

// VR-Startseite /vr (Modul XR, 21.10.2026, Christian Steiner: «ich möchte in VR lediglich ein Icon anklicken um im Modell zu landen»):
// die Rechenregeln, rein und ohne DOM und three (Node-Test: vr-start.test.mjs). Die Seite selbst steht in vr-start.js.
//
// Startpunkt in dieser Reihenfolge:
//   1. ein vom PC gesandter Raum (Adresse ?raum=<Id>, geprüft gegen das Muster einer Element-Id des Kerns; Kennzeichen wie «R-1» gehen auch),
//   2. der zuletzt in VR benutzte Standpunkt (Benutzereinstellung vr.letzter_standpunkt, gemerkt beim Sitzungsende),
//   3. der erste Raum des untersten oberirdischen Geschosses (Kote ab ±0.00, in der Regel das EG) über die Abfrage raum_begehen,
//   4. der Standpunkt der 3D-Gesamtansicht (kein Standort: vr3d.js nimmt die Kamera der Ansicht).
// Ob ein Raum als Startpunkt taugt, entscheidet der Kern (raum_begehen lehnt Räume ohne Umriss ab); hier wird nur der nächste Kandidat versucht.

const GRAD = Math.PI / 180;
const zahl = (x) => typeof x === 'number' && Number.isFinite(x);

/** Schlüssel der Benutzereinstellung (Kleinbuchstaben: Muster von BenutzerEinstellungen.cs). */
export const SCHLUESSEL = 'vr.letzter_standpunkt';
/** So viele Räume des Erdgeschosses werden höchstens versucht, bis einer einen Standort liefert. */
export const EG_VERSUCHE = 5;
/** Die Quellen des Startpunkts (Anzeige und Prüfhilfe data-startquelle). */
export const QUELLEN = Object.freeze({ parameter: 'parameter', gemerkt: 'gemerkt', eg: 'eg', gesamt: 'gesamt' });

const ID_MUSTER = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;   // Element-Id des Kerns (Guid)
const KENNZEICHEN_MUSTER = /^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$/;                          // Kennzeichen eines Elements, z. B. R-1

/** Prüft einen Raumbezug (Id oder Kennzeichen); true nur für ein gültiges Muster. */
export const raumGueltig = (t) => typeof t === 'string' && (ID_MUSTER.test(t) || KENNZEICHEN_MUSTER.test(t));

/** Der Raum aus der Adresse (Suchteil «?raum=…» oder URLSearchParams): der Bezug, wenn er dem Muster entspricht, sonst null (wird ignoriert). */
export function raumParameter(suche) {
  let p;
  try { p = suche instanceof URLSearchParams ? suche : new URLSearchParams(String(suche ?? '')); } catch { return null; }
  const alle = p.getAll('raum');
  if (alle.length !== 1) return null;   // fehlt oder mehrdeutig
  return raumGueltig(alle[0]) ? alle[0] : null;
}

// ---------------------------------------------------------------------------------------------------- gemerkter Standpunkt

/**
 * Der Standpunkt am Sitzungsende als Wert der Benutzereinstellung.
 * ende: { x, z (three, m), boden (m), gieren (rad) } – Kopfstand der Welt, wie ihn vr3d.js beim Ende meldet.
 * vorher: Standort, mit dem die Sitzung begann (Raum und Geschoss für die Anzeige); ort: Ablageort des Projekts (der Standpunkt gilt nur dort).
 * Rückgabe der kleine Wert (Länge in mm, Winkel in Grad) oder null, wenn die Angaben nicht brauchbar sind.
 */
export function standpunktMerken(ende, vorher, ort, zeit = null) {
  if (!ende || ![ende.x, ende.z, ende.boden, ende.gieren].every(zahl) || typeof ort !== 'string' || ort === '') return null;
  const grad = ((ende.gieren / GRAD) % 360 + 360) % 360;
  return {
    ort,
    x: Math.round(ende.x * 1000), y: Math.round(-ende.z * 1000), bodenOk: Math.round(ende.boden * 1000),
    azimutGrad: Math.round(grad * 100) / 100,
    raum: vorher?.raum ?? null, name: vorher?.name ?? null, nummer: vorher?.nummer ?? null,
    geschoss: vorher?.geschoss ?? null, geschossName: vorher?.geschossName ?? null,
    ...(typeof zeit === 'string' ? { zeit } : {}),
  };
}

/** Der gemerkte Wert als Standort im Format der Abfrage raum_begehen; null, wenn er fehlt, kaputt ist oder zu einem anderen Projekt gehört. */
export function standortAusGemerkt(g, ort) {
  if (!g || typeof g !== 'object' || ![g.x, g.y, g.bodenOk, g.azimutGrad].every(zahl)) return null;
  if (typeof ort !== 'string' || g.ort !== ort) return null;
  if ([g.x, g.y, g.bodenOk].some((v) => Math.abs(v) > 1e7)) return null;   // 10 km: sicher kein Gebäudestandpunkt
  const text = (t) => (typeof t === 'string' ? t : null);
  return {
    raum: text(g.raum), name: text(g.name) ?? 'Letzter Standpunkt', nummer: text(g.nummer) ?? '',
    geschoss: text(g.geschoss) ?? '', geschossName: text(g.geschossName) ?? '',
    standpunkt: { x: g.x, y: g.y, z: g.bodenOk, art: 'vr' },
    bodenOk: g.bodenOk,
    blick: { azimutGrad: g.azimutGrad, nickenGrad: 0 },
  };
}

// ---------------------------------------------------------------------------------------------------- Erdgeschoss

const natuerlich = (a, b) => String(a).localeCompare(String(b), 'de', { numeric: true });

/**
 * Kennzeichen der Räume, die als Startpunkt im untersten oberirdischen Geschoss in Frage kommen, in der Reihenfolge der Kennzeichen.
 * geschosse: [{ kennzeichen, werte: { kote } }], raeume: [{ kennzeichen, geschoss, geometrie }] (Darstellung des Kerns).
 * «Oberirdisch» heisst hier Kote ≥ 0 (Kote = OK fertig Boden über ±0.00, Katalog «Geschoss»). Ohne solches Geschoss mit Raum bleibt die Liste leer.
 * Nur Räume mit Umriss (mindestens drei Punkte) zählen; sonst gibt es keinen Standpunkt.
 */
export function egRaeume(geschosse, raeume) {
  const ober = (geschosse ?? []).filter((g) => zahl(Number(g?.werte?.kote)) && Number(g.werte.kote) >= 0)
    .sort((a, b) => Number(a.werte.kote) - Number(b.werte.kote) || natuerlich(a.kennzeichen, b.kennzeichen));
  for (const g of ober) {
    const liste = (raeume ?? []).filter((r) => r?.geschoss === g.kennzeichen && (r.geometrie?.punkte?.length ?? 0) >= 3).map((r) => r.kennzeichen).sort(natuerlich);
    if (liste.length > 0) return liste;
  }
  return [];
}

// ---------------------------------------------------------------------------------------------------- Wahl

/**
 * Wählt den Startpunkt in der Reihenfolge der Quellen (siehe Kopf). Rückgabe { quelle, standort } (standort null bei «gesamt»).
 * o: { parameter (geprüfter Raumbezug | null), gemerkt (Standort | null), raeumeEg (Kennzeichen), raumStandort(bezug) → Promise<Standort | null> (wirft nie nötig: Fehler zählen als «nicht brauchbar») }
 */
export async function startpunktWaehlen({ parameter = null, gemerkt = null, raeumeEg = [], raumStandort }) {
  const versuch = async (bezug) => {
    try { const s = await raumStandort(bezug); return s && typeof s === 'object' ? s : null; } catch { return null; }
  };
  if (parameter) {
    const s = await versuch(parameter);
    if (s) return { quelle: QUELLEN.parameter, standort: s };
  }
  if (gemerkt) return { quelle: QUELLEN.gemerkt, standort: gemerkt };
  for (const kz of raeumeEg.slice(0, EG_VERSUCHE)) {
    const s = await versuch(kz);
    if (s) return { quelle: QUELLEN.eg, standort: s };
  }
  return { quelle: QUELLEN.gesamt, standort: null };
}

// ---------------------------------------------------------------------------------------------------- Anzeige

/** Zeilen der Startseite zum Startpunkt: { geschoss, raum, quelle }. Texte Deutsch (Schweiz). */
export function startAnzeige(punkt) {
  const s = punkt?.standort ?? null;
  const quelle = {
    [QUELLEN.parameter]: 'Vom PC gewählter Raum',
    [QUELLEN.gemerkt]: 'Dein letzter Standpunkt in VR',
    [QUELLEN.eg]: 'Erster Raum im Erdgeschoss',
    [QUELLEN.gesamt]: 'Standpunkt der 3D-Gesamtansicht',
  }[punkt?.quelle] ?? '';
  if (!s) return { geschoss: '', raum: 'Aktueller Standpunkt der 3D-Ansicht', quelle };
  const geschoss = [s.geschossName, s.geschoss && s.geschoss !== s.geschossName ? `(${s.geschoss})` : ''].filter(Boolean).join(' ');
  const raum = [s.nummer, s.name].filter((t) => typeof t === 'string' && t !== '').join(' ') || s.raum || '';
  return { geschoss, raum, quelle };
}

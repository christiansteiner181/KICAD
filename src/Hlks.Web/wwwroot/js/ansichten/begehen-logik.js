// «Raum begehen» (Nachbearbeitung N15, Ich-Perspektive in der 3D-Ansicht): die Rechenregeln, rein und ohne DOM und three (in Node getestet:
// begehen.test.mjs). Kamera, Tasten und Maus stehen in begehen3d.js; den Standort (Standpunkt, Augenhöhe, Blick) rechnet der Kern (Abfrage raum_begehen).
//
// ANNAHMEN (Christian am Freitag zu bestätigen; siehe OBERFLAECHE.md «N15»):
//   Pfeil hoch/runter = vor/zurück, 100 mm je Tastendruck, bei gehaltener Taste flüssig mit 1.4 m/s (nach 250 ms);
//   Pfeil links/rechts = drehen, 5° je Tastendruck, bei gehaltener Taste 90°/s;
//   Umschalt = dreifach schneller; Bild auf/ab = Augenhöhe ±100 mm; Maus ziehen (links) = umsehen, Nicken auf ±80° begrenzt;
//   Mitte ziehen = seitlich und hoch/runter verschieben (Greifen: die Welt folgt der Maus); Esc = beenden;
//   keine Kollision (man geht durch Wände); die Höhe über Boden bleibt beim Gehen gleich.
//
// Koordinaten der 3D-Ansicht (three): x = Ost, y = oben, z = −Nord, Einheit Meter. Die Blickrichtung führt der Zustand als Gieren (Winkel gegen den
// Uhrzeigersinn von oben ab Projekt-x, Bogenmass – dieselbe Zählung wie der Azimut des Kerns: 90° = Projekt-Nord) und Nicken (über der Waagrechten positiv).

const GRAD = Math.PI / 180;

/** Augenhöhe über OK Boden in mm, wenn nichts eingestellt ist (Benutzereinstellung ansicht3d.augenhoehe). */
export const AUGENHOEHE_VORGABE = 1700;
export const AUGENHOEHE_MIN = 300;
export const AUGENHOEHE_MAX = 5000;
/** Schlüssel der Benutzereinstellung für die Augenhöhe (mm). */
export const AUGENHOEHE_SCHLUESSEL = 'ansicht3d.augenhoehe';
/** Öffnungswinkel (Grad, senkrecht) der perspektivischen Kamera beim Begehen. */
export const FOV_GRAD = 75;
/** Nahe Schnittebene beim Begehen in m (drinnen sieht man Wände aus Zentimetern). */
export const NAH_M = 0.05;
export const FERN_M = 2000;

export const SCHRITT_MM = 100;           // ein Tastendruck vor/zurück
export const GESCHWINDIGKEIT = 1.4;      // m/s bei gehaltener Taste
export const DREH_SCHRITT_GRAD = 5;      // ein Tastendruck links/rechts
export const DREH_GESCHWINDIGKEIT = 90;  // Grad/s bei gehaltener Taste
export const SCHNELL = 3;                // Umschalt
export const HOEHE_SCHRITT_MM = 100;     // Bild auf/ab
export const NICKEN_MAX_GRAD = 80;
export const UMSEHEN_GRAD_PRO_PX = 0.25; // Maus ziehen
export const VERSCHIEBEN_MM_PRO_PX = 5;  // Mitte ziehen
/** Zeit in ms, nach der eine gehaltene Taste vom einzelnen Schritt ins flüssige Gehen übergeht. */
export const HALTEN_MS = 250;

const klemme = (x, a, b) => Math.min(b, Math.max(a, x));
const zahl = (v) => typeof v === 'number' && Number.isFinite(v);
const runden = (x, n = 1) => { const f = 10 ** n; const r = Math.round(x * f) / f; return r === 0 ? 0 : r; };

/** Gültige Augenhöhe in mm: eine Zahl im erlaubten Bereich, sonst die Vorgabe. */
export function augenhoeheLesen(wert, vorgabe = AUGENHOEHE_VORGABE) {
  const n = Number(wert);
  return wert !== null && wert !== undefined && wert !== '' && Number.isFinite(n) && n >= AUGENHOEHE_MIN && n <= AUGENHOEHE_MAX ? n : vorgabe;
}

// ---------------------------------------------------------------------------------------------------- Tastenbelegung

/** Taste (KeyboardEvent.code) → Aktion. */
export const TASTEN = Object.freeze({
  ArrowUp: 'vor', ArrowDown: 'zurueck', ArrowLeft: 'links', ArrowRight: 'rechts',
  PageUp: 'hoeher', PageDown: 'tiefer', Escape: 'ende',
});

/** Aktion zu einer Taste (code), null wenn sie beim Begehen nichts tut. */
export const tastenAktion = (code) => TASTEN[code] ?? null;

/** Aktionen, die bei gehaltener Taste flüssig weiterlaufen (Gehen und Drehen); die Höhe ändert nur der Tastendruck (samt Wiederholung des Systems). */
export const FLUESSIG = Object.freeze(['vor', 'zurueck', 'links', 'rechts']);

/** Tastenhilfe für die Einblendung (eine Zeile je Taste). */
export const TASTENHILFE = Object.freeze([
  ['↑ ↓', 'vor / zurück'],
  ['← →', 'drehen'],
  ['Umschalt', 'dreifach schneller'],
  ['Bild ↑ ↓', 'Augenhöhe ± 10 cm'],
  ['Maus ziehen', 'umsehen'],
  ['Mitte ziehen', 'seitlich / hoch verschieben'],
  ['Esc', 'beenden'],
]);

// ---------------------------------------------------------------------------------------------------- Zustand

/**
 * Anfangszustand aus der Antwort der Abfrage raum_begehen (Standpunkt und Blick in Projekt-mm, Grad).
 * Rückgabe { x, z (three, m), boden (m, OK Boden), augen (m über Boden), gieren (rad), nicken (rad) }.
 */
export function zustandAusStandort(standort, augenhoeheMm = null) {
  const s = standort?.standpunkt, b = standort?.blick;
  if (!s || !zahl(s.x) || !zahl(s.y) || !zahl(standort.bodenOk) || !b || !zahl(b.azimutGrad)) return null;
  const augenMm = zahl(augenhoeheMm) ? augenhoeheMm : zahl(standort.augenhoehe) ? standort.augenhoehe : AUGENHOEHE_VORGABE;
  return {
    x: s.x / 1000, z: -s.y / 1000,
    boden: standort.bodenOk / 1000,
    augen: klemme(augenMm, AUGENHOEHE_MIN, AUGENHOEHE_MAX) / 1000,
    gieren: b.azimutGrad * GRAD,
    nicken: klemme(zahl(b.nickenGrad) ? b.nickenGrad : 0, -NICKEN_MAX_GRAD, NICKEN_MAX_GRAD) * GRAD,
  };
}

/** Blickrichtung (three, Länge 1). */
export function blickVektor(z) {
  const c = Math.cos(z.nicken);
  return { x: c * Math.cos(z.gieren), y: Math.sin(z.nicken), z: -c * Math.sin(z.gieren) };
}

/** Waagrechte Blickrichtung (three, Länge 1): geradeaus gehen. */
export function vorwaerts(z) { return { x: Math.cos(z.gieren), z: -Math.sin(z.gieren) }; }

/** Waagrecht nach rechts (three, Länge 1). */
export function rechtsVektor(z) { return { x: Math.sin(z.gieren), z: Math.cos(z.gieren) }; }

/** Position der Augen (three, m). */
export const augenPosition = (z) => ({ x: z.x, y: z.boden + z.augen, z: z.z });

/** Kamera: Position und Blickziel (1 m voraus) in three-Koordinaten. */
export function kameraWerte(z) {
  const p = augenPosition(z), b = blickVektor(z);
  return { position: p, ziel: { x: p.x + b.x, y: p.y + b.y, z: p.z + b.z } };
}

/** Standort im Projekt (mm; y nach Nord, z = Augenhöhe absolut) und Blick (Azimut Grad, Nicken Grad) – für Anzeige und Prüfung. */
export function projektWerte(z) {
  return {
    x: runden(z.x * 1000), y: runden(-z.z * 1000), z: runden((z.boden + z.augen) * 1000),
    augenhoehe: runden(z.augen * 1000), bodenOk: runden(z.boden * 1000),
    azimutGrad: runden((((z.gieren / GRAD) % 360) + 360) % 360, 3), nickenGrad: runden(z.nicken / GRAD, 3),
  };
}

// ---------------------------------------------------------------------------------------------------- Bewegen (alle Funktionen geben einen neuen Zustand zurück)

/** Geradeaus gehen um dist m (negativ = zurück); die Höhe über Boden bleibt, auch über Geschossgrenzen am selben Geschoss. */
export function gehen(z, dist) {
  const v = vorwaerts(z);
  return { ...z, x: z.x + v.x * dist, z: z.z + v.z * dist };
}

/** Seitlich verschieben um dist m (positiv = nach rechts). */
export function seitwaerts(z, dist) {
  const r = rechtsVektor(z);
  return { ...z, x: z.x + r.x * dist, z: z.z + r.z * dist };
}

/** Drehen um dGrad (positiv = nach links, gegen den Uhrzeigersinn). */
export function drehen(z, dGrad) { return { ...z, gieren: z.gieren + dGrad * GRAD }; }

/** Umsehen mit der Maus: dx, dy in Pixeln (Ziehen nach rechts = nach rechts schauen, Ziehen nach oben = nach oben schauen); Nicken auf ±80° begrenzt. */
export function umsehen(z, dx, dy, gradProPx = UMSEHEN_GRAD_PRO_PX) {
  return { ...z, gieren: z.gieren - dx * gradProPx * GRAD, nicken: klemme(z.nicken - dy * gradProPx * GRAD, -NICKEN_MAX_GRAD * GRAD, NICKEN_MAX_GRAD * GRAD) };
}

/** Augenhöhe um dMm ändern (auf 300 … 5000 mm begrenzt). */
export function augenhoeheAendern(z, dMm) {
  return { ...z, augen: klemme(z.augen + dMm / 1000, AUGENHOEHE_MIN / 1000, AUGENHOEHE_MAX / 1000) };
}

/** Mittlere Maustaste ziehen (Greifen, die Welt folgt der Maus): nach rechts ziehen = Kamera nach links, nach unten ziehen = Kamera nach oben. */
export function verschieben(z, dx, dy, mmProPx = VERSCHIEBEN_MM_PRO_PX) {
  return augenhoeheAendern(seitwaerts(z, (-dx * mmProPx) / 1000), (dy * mmProPx));
}

/** Ein einzelner Tastendruck (Aktion aus TASTEN); schnell = Umschalt gehalten. Unbekannte Aktionen ändern nichts. */
export function tippen(z, aktion, schnell = false) {
  const f = schnell ? SCHNELL : 1;
  switch (aktion) {
    case 'vor': return gehen(z, (SCHRITT_MM * f) / 1000);
    case 'zurueck': return gehen(z, (-SCHRITT_MM * f) / 1000);
    case 'links': return drehen(z, DREH_SCHRITT_GRAD * f);
    case 'rechts': return drehen(z, -DREH_SCHRITT_GRAD * f);
    case 'hoeher': return augenhoeheAendern(z, HOEHE_SCHRITT_MM * f);
    case 'tiefer': return augenhoeheAendern(z, -HOEHE_SCHRITT_MM * f);
    default: return z;
  }
}

/** Flüssige Bewegung über dt Sekunden mit den gehaltenen Aktionen (Menge); Vor und Zurück bzw. Links und Rechts zugleich heben sich auf. */
export function fliessend(z, gehalten, dt, schnell = false) {
  const f = schnell ? SCHNELL : 1;
  const hat = (a) => gehalten.has(a);
  const gehe = (hat('vor') ? 1 : 0) - (hat('zurueck') ? 1 : 0);
  const dreh = (hat('links') ? 1 : 0) - (hat('rechts') ? 1 : 0);
  let n = z;
  if (dreh) n = drehen(n, dreh * DREH_GESCHWINDIGKEIT * f * dt);
  if (gehe) n = gehen(n, gehe * GESCHWINDIGKEIT * f * dt);
  return n;
}

// ---------------------------------------------------------------------------------------------------- Tastenverlauf (einzelner Schritt, dann flüssig)

/**
 * Verfolgt die gehaltenen Tasten: der erste Tastendruck gibt einen einzelnen Schritt, nach HALTEN_MS läuft die Bewegung flüssig weiter.
 *   unten(code, jetzt, wiederholung) → Aktion für einen einzelnen Schritt (oder null)
 *   oben(code)                       → Taste losgelassen
 *   fliessend(jetzt)                 → Menge der Aktionen, die jetzt flüssig laufen (Zeit seit Tastendruck ≥ HALTEN_MS)
 *   haelt                            → ist irgendeine Taste gehalten (die Schleife läuft nur dann)
 * Die Wiederholung des Betriebssystems (wiederholung = true) gibt bei Gehen und Drehen keinen weiteren Schritt, bei Bild auf/ab schon.
 */
export function tastenVerlauf(halten = HALTEN_MS) {
  const gehalten = new Map();   // Aktion → Zeitpunkt des Tastendrucks
  return {
    unten(code, jetzt, wiederholung = false) {
      const a = tastenAktion(code);
      if (!a || a === 'ende') return null;
      if (FLUESSIG.includes(a)) {
        if (wiederholung || gehalten.has(a)) return null;
        gehalten.set(a, jetzt);
        return a;
      }
      return a;
    },
    oben(code) { const a = tastenAktion(code); if (a) gehalten.delete(a); },
    alleLos() { gehalten.clear(); },
    fliessend(jetzt) { return new Set([...gehalten].filter(([, t]) => jetzt - t >= halten).map(([a]) => a)); },
    get haelt() { return gehalten.size > 0; },
  };
}

// ---------------------------------------------------------------------------------------------------- Einblendung

/** Zeilen der Einblendung oben links: Titel (Kennzeichen und Name), Geschoss, Augenhöhe in m. */
export function einblendung(standort, z) {
  const kz = standort?.raum ?? '';
  const name = standort?.name && standort.name !== kz ? standort.name : '';
  const nummer = standort?.nummer && standort.nummer !== kz ? standort.nummer : '';
  const geschoss = standort?.geschossName ? `${standort.geschoss ?? ''} – ${standort.geschossName}` : (standort?.geschoss ?? '');
  const m = z.augen.toFixed(2);
  return {
    titel: [kz, nummer, name].filter(Boolean).join(' · '),
    geschoss: geschoss ? `Geschoss ${geschoss}` : '',
    augenhoehe: `Augenhöhe ${m} m über OK Boden`,
  };
}

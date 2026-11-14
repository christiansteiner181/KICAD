// Bezugspunkt der 3D-Ansicht (Gesamtprüfung Frage 32, Christian 13.11.2026: «Ja, oft LV95 → 3D-Ansicht mit Bezugspunkt wie der XR-Export (Rechnung
// bleibt in Landeskoordinaten)»). three.js rechnet die Geometrie in float32: bei 2 600 000 m (LV95) liegen zwei Werte 0.25 m auseinander. Die 3D-Ansicht
// zieht deshalb – wie Hlks.Kern/Xr/XrKoordinaten.cs – einen Bezugspunkt ab: je Achse 0, solange alles innerhalb ±5 km liegt, sonst die Mitte der
// Ausdehnung auf ganze Meter gerundet. Nur Darstellung: Modell, Befehle und gespeicherte Werte (Schnittbereich, Startansicht, Begehen) bleiben in
// Projekt-mm; umgerechnet wird an den Stellen Modell ↔ three (koerper3d vek, huelle3d, netzdatei, schnittbereich-logik, begehen-logik, xr-mess-logik).
// Reine Funktionen, in Node testbar.

/** Grenze je Achse (mm), bis zu der der Projekt-Nullpunkt gilt (wie XrKoordinaten.NahGrenzeMm). */
export const NAH_GRENZE_MM = 5_000_000;

/** Aktueller Bezugspunkt in Projekt-mm [x, y, z] (wird von der 3D-Ansicht gesetzt; 0 = Projekt-Nullpunkt). */
export const B = [0, 0, 0];

/** Bezugspunkt aus Begrenzungen [minx, miny, minz, maxx, maxy, maxz] (mm), Regel wie XrKoordinaten.Bezugspunkt. */
export function bezugAus(begrenzungen) {
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  let etwas = false;
  for (const b of begrenzungen ?? []) {
    if (!Array.isArray(b) || b.length < 6 || !b.every(Number.isFinite)) continue;
    for (let k = 0; k < 3; k++) { min[k] = Math.min(min[k], b[k]); max[k] = Math.max(max[k], b[k + 3]); }
    etwas = true;
  }
  const aus = [0, 0, 0];
  if (!etwas) return aus;
  for (let k = 0; k < 3; k++)
    if (Math.max(Math.abs(min[k]), Math.abs(max[k])) > NAH_GRENZE_MM) {
      const m = (min[k] + max[k]) / 2 / 1000;
      aus[k] = Math.sign(m) * Math.round(Math.abs(m)) * 1000;   // auf ganze Meter, Hälften von null weg (wie MidpointRounding.AwayFromZero)
    }
  return aus;
}

/** Begrenzung aus Punkten [[x, y, z?] …] (mm); null ohne Punkte. */
export function begrenzungVon(punkte) {
  const lo = [Infinity, Infinity, Infinity], hi = [-Infinity, -Infinity, -Infinity];
  let n = 0;
  for (const p of punkte ?? []) {
    if (!Array.isArray(p) || p.length < 2) continue;
    const q = [p[0], p[1], p[2] ?? 0];
    if (!q.every(Number.isFinite)) continue;
    for (let k = 0; k < 3; k++) { lo[k] = Math.min(lo[k], q[k]); hi[k] = Math.max(hi[k], q[k]); }
    n++;
  }
  return n ? [...lo, ...hi] : null;
}

/** Bezugspunkt setzen; true, wenn er sich geändert hat (dann sind gebaute Körper neu zu bauen). */
export function bezugSetzen(b) {
  const neu = [b?.[0] || 0, b?.[1] || 0, b?.[2] || 0];
  const anders = neu.some((x, k) => x !== B[k]);
  for (let k = 0; k < 3; k++) B[k] = neu[k];
  return anders;
}

/** Bezugspunkt in three-Koordinaten (m): (bx, bz, −by) / 1000. */
export const bezugDrei = () => [B[0] / 1000, B[2] / 1000, -B[1] / 1000];

/** Punkte der Körper des Kerns (/api/koerper: teile mit mitte, von, nach, grundriss + unten/oben, ecken) in Projekt-mm – für die Begrenzung. */
export function punkteAusKoerpern(koerper) {
  const aus = [];
  for (const k of koerper ?? []) for (const t of k?.teile ?? []) {
    for (const s of ['mitte', 'von', 'nach']) if (Array.isArray(t[s])) aus.push(t[s]);
    for (const p of t.grundriss ?? []) if (Array.isArray(p)) { aus.push([p[0], p[1], t.unten ?? 0]); aus.push([p[0], p[1], t.oben ?? 0]); }
    for (const p of t.ecken ?? []) if (Array.isArray(p)) aus.push(p);
  }
  return aus;
}

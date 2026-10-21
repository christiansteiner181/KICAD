// Gedrehte Grundrisse (Nachbearbeitung N9): reine Rechenhilfen für die Plan-Ansicht (grundriss.js), ohne DOM (in Node testbar).
// Der Kern liefert die Plangrafik in Modellkoordinaten und dazu «drehung» (Grad, Gegenuhrzeigersinn, Modell → Ansicht), «nord» (Richtung von
// geografisch Nord auf dem Blatt, Grad im Uhrzeigersinn ab oben) und «begrenzungAnsicht» (Begrenzung in den Achsen der Ansicht). Die
// Ansicht dreht nur an einer Stelle: die Gruppe «gr-welt» bekommt transform="scale(1 -1) rotate(drehung)". Beschriftungen hat der Kern
// schon gegengedreht (Ansichtsausrichtung.cs). Die Umrechnung Bildschirm ↔ Modell (zuModell, zuBildschirm) und die Ausschnitte rechnen
// hier: Ansicht = Modell um +drehung gedreht, zurück um −drehung.

const RAD = Math.PI / 180;

/** Punkt [x, y] um den Ursprung gedreht (Grad, Gegenuhrzeigersinn). */
export function drehen(p, grad) {
  if (!grad) return [p[0], p[1]];
  const c = Math.cos(grad * RAD), s = Math.sin(grad * RAD);
  return [p[0] * c - p[1] * s, p[0] * s + p[1] * c];
}

/** Modell-mm → Achsen der Ansicht. */
export const zuAnsicht = (p, drehung) => drehen(p, drehung);
/** Achsen der Ansicht → Modell-mm. */
export const ausAnsicht = (v, drehung) => drehen(v, -(drehung || 0));

/** Drehung aus der Plangrafik (Zahl, sonst 0). */
export const drehungVon = (plan) => (Number.isFinite(plan?.drehung) ? plan.drehung : 0);

/** Modell-mm → SVG-Koordinaten der Ansicht (viewBox: x nach rechts, y nach unten) – dasselbe wie transform der Gruppe gr-welt. */
export function svgAusModell(p, drehung) {
  const v = drehen(p, drehung);
  return [v[0], -v[1]];
}

/** SVG-Koordinaten der Ansicht → Modell-mm (Umkehrung von svgAusModell; Klick → Modell). */
export const modellAusSvg = (s, drehung) => drehen([s[0], -s[1]], -(drehung || 0));

/** transform der Gruppe gr-welt: y spiegeln (Modell y nach oben) und um die Drehung der Ansicht drehen. */
export function weltTransform(drehung) {
  const d = Number(drehung) || 0;
  return d ? `scale(1 -1) rotate(${Math.round(d * 1e6) / 1e6})` : 'scale(1 -1)';
}

/** Rechteck {minx, miny, maxx, maxy} im Modell → umschliessendes Rechteck in den Achsen der Ansicht (vier Ecken gedreht). */
export function boxInAnsicht(b, drehung) {
  if (!b) return null;
  if (!drehung) return { ...b };
  const ecken = [[b.minx, b.miny], [b.maxx, b.miny], [b.maxx, b.maxy], [b.minx, b.maxy]].map((p) => drehen(p, drehung));
  return {
    minx: Math.min(...ecken.map((p) => p[0])), miny: Math.min(...ecken.map((p) => p[1])),
    maxx: Math.max(...ecken.map((p) => p[0])), maxy: Math.max(...ecken.map((p) => p[1])),
  };
}

const gueltig = (p) => Array.isArray(p) && Number.isFinite(p[0]) && Number.isFinite(p[1]);

/**
 * Begrenzung der Plangrafik in den Achsen der Ansicht: plan.begrenzungAnsicht (Kern), sonst aus den Punkten gerechnet (gedreht), ohne Texte
 * wie begrenzungVon. null wenn leer.
 */
export function ansichtsBegrenzung(plan) {
  const b = plan?.begrenzungAnsicht;
  if (Array.isArray(b) && b.length === 4 && b.every(Number.isFinite)) return { minx: b[0], miny: b[1], maxx: b[2], maxy: b[3] };
  const d = drehungVon(plan);
  if (!d && Array.isArray(plan?.begrenzung) && plan.begrenzung.length === 4 && plan.begrenzung.every(Number.isFinite)) {
    const m = plan.begrenzung;
    return { minx: m[0], miny: m[1], maxx: m[2], maxy: m[3] };
  }
  let r = null;
  const nimm = (p) => {   // p in den Achsen der Ansicht
    r = r ? { minx: Math.min(r.minx, p[0]), miny: Math.min(r.miny, p[1]), maxx: Math.max(r.maxx, p[0]), maxy: Math.max(r.maxy, p[1]) } : { minx: p[0], miny: p[1], maxx: p[0], maxy: p[1] };
  };
  for (const t of plan?.teile ?? []) for (const p of t.primitive ?? []) {
    if (p.art === 'text') continue;
    const rad = (p.art === 'kreis' || p.art === 'bogen') && p.radius > 0 ? p.radius : 0;
    for (const q of p.punkte ?? []) {
      if (!gueltig(q)) continue;
      const v = drehen(q, d);
      nimm([v[0] - rad, v[1] - rad]); nimm([v[0] + rad, v[1] + rad]);
    }
  }
  return r;
}

/** CSS-Drehung des Nordpfeils der Ansicht (Grad im Uhrzeigersinn, wie CSS rotate). */
export const nordCss = (nord) => `rotate(${Math.round((Number(nord) || 0) * 1e3) / 1e3}deg)`;

/** Winkel auf (−180, 180]. */
export function norm180(w) {
  let n = ((Number(w) % 360) + 360) % 360;
  if (n > 180) n -= 360;
  return n;
}

/** Kurztext der Ausrichtung für die Anzeige am Massstab, z. B. «Geometrie, gedreht −37.0°»; '' ohne Drehung und ohne Art. */
export function ausrichtungText(plan) {
  const d = drehungVon(plan);
  const art = plan?.ausrichtung === 'himmelsrichtung' ? 'Himmelsrichtung' : plan?.ausrichtung === 'geometrie' ? 'Geometrie' : '';
  if (!art) return '';
  return d ? `${art}, gedreht ${(Math.round(norm180(d) * 10) / 10).toFixed(1).replace('-', '−')}°` : `${art}, nicht gedreht`;
}

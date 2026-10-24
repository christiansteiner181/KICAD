// Anzeige «Brille sieht den Bildschirm» (Modul XR, XR-I «Desktop in der Brille», Quest App V1c; Auftrag: «Solange die Brille mitsieht, zeigt KICAD das deutlich …
// mit Knopf ‹Beenden›»). Reine Regeln ohne DOM; die Verdrahtung steht in xr-desktop-status.js, der Server in XrDesktop.cs.
// Zustand = Antwort von GET /api/xr/desktop/zustand: { erfassung, bildschirme[], verbunden, aktiv, stroeme[{ id, brille, name, gestartet, pausiert, monitor,
// monitorName, breite, hoehe, bildrate, ruheBildrate, ruhig, bilderJeSekunde, kbitJeSekunde, … }] }. «ruhig»: am PC hat sich 2 s nichts geändert, KICAD erfasst nur
// noch mit der Ruhe-Bildrate (Nachtrag V1c).

export const TAKT_MS = 2000;
export const ZUSTAND = '/api/xr/desktop/zustand';
export const BEENDEN = '/api/xr/desktop/beenden';

const zahl = (x) => (Number.isFinite(Number(x)) ? Number(x).toFixed(1) : '–');
const name = (s) => String(s?.name || s?.brille || 'Brille');

/** Ströme, die angemeldet sind («start» kam). */
export function stroeme(z) {
  return Array.isArray(z?.stroeme) ? z.stroeme.filter((s) => s && s.gestartet !== false) : [];
}

/**
 * Was die Statusleiste zeigt: null (keine Brille verbunden) oder { stufe, text, titel, knopf }.
 * stufe «aktiv»: mindestens eine Brille sieht mit (rot, pulsierend, roter Rand ums Fenster); «pausiert»: verbunden, aber in der Brille ausgeblendet (grau).
 */
export function desktopAnzeige(z) {
  const alle = stroeme(z);
  if (alle.length === 0) return null;
  const aktiv = alle.filter((s) => !s.pausiert);
  if (aktiv.length > 0) {
    const s = aktiv[0];
    const wer = aktiv.length === 1 ? `«${name(s)}»` : `${aktiv.length} Brillen`;
    const monitor = s.monitorName ? `${s.monitorName}${s.breite ? `, ${s.breite} × ${s.hoehe}` : ''}` : 'Bildschirm';
    return {
      stufe: 'aktiv',
      text: aktiv.length === 1 ? 'Brille sieht den Bildschirm' : `${aktiv.length} Brillen sehen den Bildschirm`,
      titel: `${wer} ${aktiv.length === 1 ? 'sieht' : 'sehen'} diesen Bildschirm live (${monitor}; ${zahl(s.bilderJeSekunde)} Bilder/s, ${zahl(s.kbitJeSekunde)} kbit/s` +
        `${s.ruhig ? `; ruhig – nur ${zahl(s.ruheBildrate)} Erfassungen/s` : ''}). ` +
        'Bedient wird nur hier mit Maus und Tastatur. «Beenden» trennt die Übertragung.',
      knopf: 'Beenden',
    };
  }
  return {
    stufe: 'pausiert',
    text: 'Bildschirm in der Brille pausiert',
    titel: `${alle.length === 1 ? `«${name(alle[0])}»` : `${alle.length} Brillen`} verbunden, der Bildschirm ist in der Brille ausgeblendet – es wird nichts übertragen. ` +
      '«Beenden» trennt die Verbindung.',
    knopf: 'Beenden',
  };
}

/** Meldung nach «Beenden» (Antwort von POST /api/xr/desktop/beenden: { beendet, zustand }). */
export function beendetText(a) {
  const n = Number(a?.beendet ?? 0);
  return n === 0 ? 'Keine Brille sah den Bildschirm.' : n === 1 ? 'Übertragung an die Brille beendet.' : `Übertragung an ${n} Brillen beendet.`;
}

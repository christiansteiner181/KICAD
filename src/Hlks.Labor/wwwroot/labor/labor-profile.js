// Profilquelle des Labors (L1) für den Inhalt des Dialogs «XR-Labor» (js/oberflaeche/xr-labor-dialog.js, Option «profile»): die Profile liegen nicht in den
// Benutzereinstellungen von KICAD, sondern im Labor – %OneDrive%\HLKS-CAD\labor\profile (Server: LaborProfile.cs, /api/labor/profile). Keine Logik hier: die Prüfung der
// Profile und das Speichern macht der Server. Form der Quelle: siehe benutzerProfilquelle() im Dialog (laden, liste, holen, aktiv, speichern, aktivSetzen, loeschen, aktualisieren).

import { holen, senden, ApiFehler } from '../js/api.js';
import { profileListe, profilSchluessel, PROFIL_VORSATZ } from '../js/oberflaeche/xr-labor-text.js';

const PFAD = '/api/labor/profile';

async function loeschenApi(pfad) {
  const r = await fetch(pfad, { method: 'DELETE', cache: 'no-store' });
  const typ = r.headers.get('content-type') || '';
  const inhalt = typ.includes('application/json') ? await r.json() : await r.text();
  if (!r.ok) {
    if (inhalt && typeof inhalt === 'object') throw new ApiFehler(inhalt.fehler || `Fehler ${r.status}`, inhalt.vorschlag, r.status);
    throw new ApiFehler(String(inhalt || `Fehler ${r.status}`), null, r.status);
  }
  return inhalt;
}

export function laborProfilquelle() {
  let stand = { aktiv: null, profile: [] };   // letzte Antwort von GET /api/labor/profile
  const laden = async () => {
    try { stand = await holen(PFAD); } catch { /* der Server ist weg oder alt: die letzte Liste bleibt stehen */ }
  };
  // dieselbe Form wie die Benutzereinstellungen, damit profileListe() (Name sortiert, Zahl der Werte) unverändert gilt
  const alsWerte = () => Object.fromEntries((stand.profile ?? []).map((p) => [profilSchluessel(p.kennung), { name: p.name, werte: p.werte ?? {} }]));
  return {
    laden,
    aktualisieren: laden,
    liste: () => profileListe(alsWerte(), stand.aktiv ?? null),
    holen: (kennung) => alsWerte()[PROFIL_VORSATZ + kennung] ?? null,
    aktiv: () => stand.aktiv ?? null,
    async speichern(kennung, profil) { await senden(PFAD, { kennung, profil }); await laden(); },
    async aktivSetzen(kennung) { await senden(`${PFAD}/aktiv`, { kennung }); await laden(); },
    async loeschen(kennung) { await loeschenApi(`${PFAD}/${encodeURIComponent(kennung)}`); await laden(); },
  };
}

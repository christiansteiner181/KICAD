// Einstellung «Brillen in der 3D-Ansicht zeigen» (Modul XR, Quest App P7): ein kleines Modul ohne three.js, damit das Menüband (aktionen-quest.js) und der Dialog «Quest verbinden» sie
// lesen und schalten können, ohne die 3D-Bibliothek beim Start zu laden. Die Anzeige selbst steht in xr-teilnehmer.js.
//
// Benutzereinstellung ansicht3d.brillen_zeigen (Ja/Nein, Vorgabe ja; gilt für den Benutzer auf allen Rechnern) und das Ereignis «hlks-xr-brille» (window), das offene 3D-Ansichten beim Umschalten
// aufweckt.

import { benutzerWert, benutzerSetzen } from '../oberflaeche/benutzer.js';

/** Benutzereinstellung (Ja/Nein, Vorgabe ja) und Ereignis beim Umschalten. */
export const EINSTELLUNG = 'ansicht3d.brillen_zeigen';
export const EREIGNIS = 'hlks-xr-brille';

/** Ist die Anzeige eingeschaltet? Vorgabe ja; nur ein ausdrückliches «nein» schaltet aus. */
export const brilleZeigenAn = () => benutzerWert(EINSTELLUNG, true) !== false;

/** Schaltet die Anzeige um (gespeichert je Benutzer) und meldet es den offenen 3D-Ansichten. */
export async function brilleZeigenSetzen(an) {
  await benutzerSetzen(EINSTELLUNG, !!an);
  window.dispatchEvent(new CustomEvent(EREIGNIS, { detail: { an: !!an } }));
}

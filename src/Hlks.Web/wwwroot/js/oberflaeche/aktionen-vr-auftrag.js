// Aktionen «In VR begehen (Brille)» und «Im Sitzungsraum zeigen (Brille)» (Modul XR): Kontextmenü eines Raums im Grundriss (ansichten/grundriss.js). Sie setzen den VR-Auftrag des Servers
// (vr-auftrag.js); die VR-Startseite /vr in der Quest führt ihn aus. Ohne Register-Eintrag (nur Kontextmenü, wie «Rand für Raum im 3D zeigen»); keine Änderung am Modell.

import { registrieren } from './register.js';
import { auftragSenden } from './vr-auftrag.js';
import { element } from '../zustand.js';

const raumVon = (p) => { const kz = p?.raeume?.[0]; const e = kz ? element(kz) : null; return e?.klasse === 'Raum' ? e : null; };

registrieren({
  id: 'ansicht.vrBegehen', titel: 'In VR begehen', symbol: 'raumBegehen', kuerzel: null,
  aktiv: (p) => !!raumVon(p), hinweis: 'Zuerst einen Raum wählen.',
  ausfuehren: (p) => { const r = raumVon(p); return r ? auftragSenden({ modus: 'begehen', raum: r.id }) : undefined; },
});
registrieren({
  id: 'ansicht.vrSitzungsraum', titel: 'Im Sitzungsraum zeigen (VR)', symbol: 'raumBegehen', kuerzel: null,
  aktiv: (p) => !!raumVon(p)?.geschoss, hinweis: 'Zuerst einen Raum mit Geschoss wählen.',
  ausfuehren: (p) => { const r = raumVon(p); return r ? auftragSenden({ modus: 'sitzungsraum', raum: r.id }) : undefined; },
});

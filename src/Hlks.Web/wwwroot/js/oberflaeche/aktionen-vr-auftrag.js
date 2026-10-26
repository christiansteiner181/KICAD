// Aktionen «In VR begehen», «Im Sitzungsraum zeigen», «Raum in AR» und «An die Brille senden» (Modul XR): Sie setzen den VR-Auftrag des Servers (vr-auftrag.js); die VR-Startseite /vr bzw. die Quest App
// in der Brille führt ihn aus. Keine Änderung am Modell.
// Orte (Christian, 26.10.2026): Register XR › Gruppe «Zeigen» (die drei Raumaktionen nehmen den gewählten Raum) und das Kontextmenü eines Raums im Grundriss (ansichten/grundriss.js: «Raum begehen in VR»,
// «Modell am Sitzungstisch», «Raum in AR»; dort bekommt die Aktion den Raum als Parameter { raeume: [Kennzeichen] }). Eine Aktion, zwei Aufrufer.

import { registrieren } from './register.js';
import { auftragSenden, anBrilleSenden } from './vr-auftrag.js';
import { element, zustand } from '../zustand.js';

/** Der Raum der Aktion: aus dem Kontextmenü (Parameter) oder, im Menüband ohne Parameter, der gewählte Raum. */
const raumVon = (p) => { const kz = p?.raeume ? p.raeume[0] : zustand.auswahl; const e = kz ? element(kz) : null; return e?.klasse === 'Raum' ? e : null; };

registrieren({
  id: 'ansicht.vrBegehen', titel: 'In VR begehen', symbol: 'raumBegehen', kuerzel: 'XV', register: 'xr', gruppe: 'Zeigen', groesse: 'gross',
  aktiv: (p) => !!raumVon(p), hinweis: 'Zuerst einen Raum wählen.',
  ausfuehren: (p) => { const r = raumVon(p); return r ? auftragSenden({ modus: 'begehen', raum: r.id }) : undefined; },
});
registrieren({
  id: 'ansicht.vrSitzungsraum', titel: 'Im Sitzungsraum zeigen', symbol: 'sitzungstisch', kuerzel: 'XS', register: 'xr', gruppe: 'Zeigen', groesse: 'gross',
  aktiv: (p) => !!raumVon(p)?.geschoss, hinweis: 'Zuerst einen Raum mit Geschoss wählen.',
  ausfuehren: (p) => { const r = raumVon(p); return r ? auftragSenden({ modus: 'sitzungsraum', raum: r.id }) : undefined; },
});
// «Raum in AR» (früher «Raum zeigen (VR)»): das Gegenstück zu «Raum in 3D» (N14) für die Brille – der Sitzungsraum zeigt auf dem Tisch nur den Raum plus Rand (derselbe Quader, vom Kern berechnet)
registrieren({
  id: 'ansicht.vrRaumZeigen', titel: 'Raum in AR', symbol: 'raumAr', kuerzel: 'XR', register: 'xr', gruppe: 'Zeigen', groesse: 'gross',
  aktiv: (p) => !!raumVon(p), hinweis: 'Zuerst einen Raum wählen.',
  ausfuehren: (p) => { const r = raumVon(p); return r ? auftragSenden({ modus: 'sitzungsraum', raum: r.id, ausschnitt: true }) : undefined; },
});
// «An die Brille senden»: derselbe Auftrag wie der Knopf in der 3D-Ansicht (vr3d.js) – läuft das Begehen, geht dessen Raum an die Brille, sonst die aktive 3D-Ansicht als Sitzungsraum, sonst das ganze Modell
registrieren({
  id: 'xr.anBrille', titel: 'An die Brille senden', symbol: 'anBrille', kuerzel: 'XA', register: 'xr', gruppe: 'Zeigen', groesse: 'gross',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => anBrilleSenden(),
});

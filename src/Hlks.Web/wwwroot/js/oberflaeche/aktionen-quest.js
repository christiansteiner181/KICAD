// Register XR (Christian, 26.10.2026: «wir nehmen alles was VR/XR ist in einen eigenen Menue Tab im CAD»): alles, was mit der Brille (Meta Quest), der Quest App und dem VR/AR-Zeigen zu tun
// hat, steht in einem eigenen Register «XR» (menueband.js) mit den Gruppen Brille · Zeigen · Koordination (Gruppe «Labor» mit «XR-Labor …» entfällt seit dem Umzug nach HK_XR, 26.10.2026). Jede Funktion steht nur dort; die Kontextmenüs (Raum, Grundriss) behalten ihre
// Einträge und lösen dieselben Aktionen aus (aktionen-vr-auftrag.js, ansichten/grundriss.js). Die Aktions-Ids («ansicht.…», «koordination.problemMarker») bleiben, wie sie waren: sie sind
// Bezeichner für Anleitung, Kürzelliste und Tests, keine Orte.
//
// Gruppe «Brille»: «Quest verbinden …» (Dialog quest-dialog.js; die Weiterleitung selbst setzt der Server: POST /api/quest/verbinden, QuestDienst.cs, adb reverse), «H+K XR installieren …» (bis E1 «App installieren …»)
// (derselbe Dialog; sein erster Teil ist «Quest App» mit dem Knopf «App installieren/aktualisieren»), «Autostart» (Schalter, Einstellung quest.autostart: die Begehung startet beim
// Aufsetzen der Brille) und «Brille zeigen» (Quest App P7: Figuren der verbundenen Brillen in der 3D-Ansicht am PC, ansichten/xr-teilnehmer.js; hier nur das kleine Modul
// ansichten/xr-brille-einstellung.js ohne three.js, Einstellung ansicht3d.brillen_zeigen, Vorgabe ein).
// Gruppe «Zeigen»: aktionen-vr-auftrag.js. Gruppe «Koordination»: «Problem-Marker» in problemmarker-panel.js.
// Alles reine Rechner-Funktionen, kein Befehl des Kerns und für die KI nicht freigegeben (KiEinordnung.RechnerFunktionen). Immer verfügbar (auch ohne offenes Projekt).
// Eingetragen wird nur über registrieren() (register.js).
//
// Kürzel des Registers: X plus Anfangsbuchstabe (XQ Quest, XI Installieren, XU aUtostart, XB Brille, XV VR, XS Sitzungsraum, XR Raum in AR, XA An die Brille, XM Marker).

import { registrieren } from './register.js';
import { questDialog } from './quest-dialog.js';
import { brilleZeigenAn, brilleZeigenSetzen } from '../ansichten/xr-brille-einstellung.js';
import { benutzerLaden, benutzerSetzen, benutzerWert } from './benutzer.js';
import { meldung } from './dialoge.js';
import { AUTOSTART_SCHLUESSEL, autostartAn } from './quest-text.js';

registrieren({
  id: 'ansicht.questVerbinden', titel: 'Quest verbinden …', symbol: 'questVerbinden', kuerzel: 'XQ', register: 'xr', gruppe: 'Brille', groesse: 'gross',
  ausfuehren: () => questDialog(),
});

// «H+K XR installieren …» (H+K XR E1: eine App «H+K XR» für alle Werkzeuge; vorher «App installieren …»): öffnet denselben Dialog; die Quest App steht dort als erster Teil, mit «App installieren/aktualisieren»
// (adb über WLAN oder Kabel; Teil aus HK_XR, /hkxr/brillen-app-dialog.js) und – solange die alten Apps «KICAD» und «XR-Labor» in der Brille sind – «Alte Apps entfernen …». Aktions-Id und Kürzel XI bleiben.
registrieren({
  id: 'xr.appInstallieren', titel: 'H+K XR installieren …', symbol: 'appInstallieren', kuerzel: 'XI', register: 'xr', gruppe: 'Brille', groesse: 'gross',
  ausfuehren: () => questDialog(),
});

// «Autostart»: die Begehung startet, sobald die Brille aufgesetzt ist (Einstellung quest.autostart, Vorgabe ein; derselbe Schalter wie im Dialog «Quest verbinden»)
registrieren({
  id: 'xr.autostart', titel: 'Autostart', symbol: 'autostart', kuerzel: 'XU', register: 'xr', gruppe: 'Brille', groesse: 'gross',
  gedrueckt: () => autostartAn(benutzerWert(AUTOSTART_SCHLUESSEL, true)),
  async ausfuehren() {
    try { await benutzerLaden(); } catch { /* ohne Einstellungen gilt die Vorgabe */ }
    const an = !autostartAn(benutzerWert(AUTOSTART_SCHLUESSEL, true));
    await benutzerSetzen(AUTOSTART_SCHLUESSEL, an);
    meldung(an ? 'Autostart ein: Nach dem Klick am PC startet die Begehung, sobald die Brille aufgesetzt ist.' : 'Autostart aus: In der Brille die VR-Startseite von Hand öffnen und tippen.', 'info');
  },
});

registrieren({
  id: 'ansicht.brilleZeigen', titel: 'Brille zeigen', symbol: 'brilleZeigen', kuerzel: 'XB', register: 'xr', gruppe: 'Brille', groesse: 'gross',
  gedrueckt: () => brilleZeigenAn(),
  async ausfuehren() {
    try { await benutzerLaden(); } catch { /* ohne Einstellungen gilt die Vorgabe */ }
    const an = !brilleZeigenAn();
    await brilleZeigenSetzen(an);
    meldung(an ? 'Verbundene Brillen werden in der 3D-Ansicht gezeigt.' : 'Die Brillen werden in der 3D-Ansicht nicht mehr gezeigt.', 'info');
  },
});


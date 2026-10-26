// Aktion «Quest verbinden …» (Modul XR): kleiner Knopf im Register Ansicht, Gruppe «Fenster» (dort hat die zweite Spalte Platz; eine eigene Gruppe «VR» machte das
// Menüband bei 1920 px zu breit: menueband.browser.mjs verlangt keine zusammengeklappte Gruppe). Öffnet den Dialog (quest-dialog.js); die Weiterleitung selbst setzt der Server
// (POST /api/quest/verbinden, QuestDienst.cs: adb reverse). Reine Rechner-Funktion, kein Befehl des Kerns und für die KI nicht freigegeben (KiEinordnung.RechnerFunktionen).
// Immer verfügbar (auch ohne offenes Projekt). Eingetragen wird nur über registrieren() (register.js).
//
// Quest App P7: «Brille zeigen» schaltet die Figuren der verbundenen Quest-Brillen in der 3D-Ansicht am PC ein und aus (Anzeige: ansichten/xr-teilnehmer.js; hier nur das kleine Modul ansichten/xr-brille-einstellung.js ohne three.js,
// Benutzereinstellung ansicht3d.brillen_zeigen, Vorgabe ein). Immer verfügbar; ohne verbundene Brille ändert sich im Bild nichts.

import { registrieren } from './register.js';
import { questDialog } from './quest-dialog.js';
import { brilleZeigenAn, brilleZeigenSetzen } from '../ansichten/xr-brille-einstellung.js';
import { benutzerLaden } from './benutzer.js';
import { meldung } from './dialoge.js';

registrieren({
  id: 'ansicht.questVerbinden', titel: 'Quest verbinden …', symbol: 'questVerbinden', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  ausfuehren: () => questDialog(),
});

registrieren({
  id: 'ansicht.brilleZeigen', titel: 'Brille zeigen', symbol: 'brilleZeigen', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  gedrueckt: () => brilleZeigenAn(),
  async ausfuehren() {
    try { await benutzerLaden(); } catch { /* ohne Einstellungen gilt die Vorgabe */ }
    const an = !brilleZeigenAn();
    await brilleZeigenSetzen(an);
    meldung(an ? 'Verbundene Brillen werden in der 3D-Ansicht gezeigt.' : 'Die Brillen werden in der 3D-Ansicht nicht mehr gezeigt.', 'info');
  },
});

// Quest App P6: «XR-Labor …» – die Werkbank für das Interface-Labor der App (Stellschrauben live, Profile, Varianten, Prüfaufgaben, Auswertung, Fragebogen). Öffnet den Dialog
// (xr-labor-dialog.js, erst beim Klick geladen); alles läuft über /api/xr/… (XrEndpunkte.cs, XrLaborEndpunkte.cs). Reine Rechner-Funktion, für die KI nicht freigegeben
// (KiEinordnung.RechnerFunktionen «xr_labor»). Immer verfügbar (auch ohne offenes Projekt).
registrieren({
  id: 'ansicht.xrLabor', titel: 'XR-Labor …', symbol: 'xrLabor', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  ausfuehren: async () => (await import('./xr-labor-dialog.js')).xrLaborDialogOeffnen(),
});

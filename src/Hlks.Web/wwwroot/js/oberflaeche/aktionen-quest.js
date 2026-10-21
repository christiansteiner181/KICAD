// Aktion «Quest verbinden …» (Modul XR): kleiner Knopf im Register Ansicht, Gruppe «Fenster» (dort hat die zweite Spalte Platz; eine eigene Gruppe «VR» machte das
// Menüband bei 1920 px zu breit: menueband.browser.mjs verlangt keine zusammengeklappte Gruppe). Öffnet den Dialog (quest-dialog.js); die Weiterleitung selbst setzt der Server
// (POST /api/quest/verbinden, QuestDienst.cs: adb reverse). Reine Rechner-Funktion, kein Befehl des Kerns und für die KI nicht freigegeben (KiEinordnung.RechnerFunktionen).
// Immer verfügbar (auch ohne offenes Projekt). Eingetragen wird nur über registrieren() (register.js).

import { registrieren } from './register.js';
import { questDialog } from './quest-dialog.js';

registrieren({
  id: 'ansicht.questVerbinden', titel: 'Quest verbinden …', symbol: 'questVerbinden', kuerzel: null, register: 'ansicht', gruppe: 'Fenster', groesse: 'klein',
  ausfuehren: () => questDialog(),
});

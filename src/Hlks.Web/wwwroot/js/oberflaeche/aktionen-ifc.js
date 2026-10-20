// Aktion «IFC exportieren» (Modul 7 Teil B2): Knopf im Register Datei, Gruppe «Ausgabe». Öffnet den Dialog (ifc-export-dialog.js); der Export selbst
// läuft im Kern (POST /api/ifc/export, Abfrage ifc_export). Aktiv nur mit offenem Projekt. Eingetragen wird nur über registrieren() (register.js).

import { zustand } from '../zustand.js';
import { registrieren } from './register.js';
import { ifcExportDialog } from './ifc-export-dialog.js';

registrieren({
  id: 'datei.ifc-export', titel: 'IFC exportieren', symbol: 'ifcExport', kuerzel: null, register: 'datei', gruppe: 'Ausgabe',
  aktiv: () => !!zustand.status?.offen, hinweis: 'Zuerst ein Projekt öffnen.',
  ausfuehren: () => ifcExportDialog(),
});

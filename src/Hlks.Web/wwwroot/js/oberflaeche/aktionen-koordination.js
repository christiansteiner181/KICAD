// Aktionen des Registers «Koordination» (Modul 3b, Auftrag C1): Projektvorgaben, Kollisionen prüfen, Kollisionsliste, Aussparungen vorschlagen / nummerieren,
// Aussparungsliste, Abgleich, Aussparungsplan, IFC für den Architekten, BCF exportieren und importieren. Eigenes Register (wie «Hülle»): das Modul hat zehn Knöpfe
// in vier Gruppen und gehört weder zum Zeichnen (Gebäudetechnik) noch zur Verwaltung; Revit führt Austausch und Prüfung ebenfalls in einem eigenen Register
// («Zusammenarbeit»). Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns, die Dialoge stehen in koordination-dialoge.js, die Listen in
// koordination-panel.js, Texte und Parameter in koordination-text.js (in Node getestet).

import { zustand, derKlasse, abonnieren } from '../zustand.js';
import { abfrage } from '../api.js';
import { registrieren } from './register.js';
import * as D from './koordination-dialoge.js';
import { panelOeffnen } from './koordination-panel.js';
import * as T from './koordination-text.js';

const projektOffen = () => !!zustand.status?.offen;
const KEIN_PROJEKT = 'Zuerst ein Projekt öffnen.';
const REGISTER = 'koordination';

// Fähigkeiten des Kerns (für den Knopf «IFC für Architekt»: die Abfrage aussparungen_ifc baut Teil B4b). Einmal je Projekt gelesen.
let faehig = null, faehigFuer = null, faehigLaeuft = false;
function faehigkeitenLaden() {
  if (faehigLaeuft || !projektOffen()) return;
  const fuer = zustand.geladen ?? zustand.status?.ort ?? null;
  faehigLaeuft = true;
  abfrage('faehigkeiten').then((f) => { faehig = T.ifcFaehigkeit(f); faehigFuer = fuer; }).catch(() => { faehig = { vorhanden: false, parameter: new Map() }; faehigFuer = fuer; })
    .finally(() => { faehigLaeuft = false; });
}
const ifcFaehig = () => {
  const fuer = zustand.geladen ?? zustand.status?.ort ?? null;
  if (faehig && faehigFuer === fuer) return faehig;
  faehigkeitenLaden();
  return null;
};
abonnieren('modell', (d) => { if (d?.alles) faehigkeitenLaden(); });

const hatGeschoss = () => derKlasse('Geschoss').length > 0;
const hatIfc = () => T.ifcUnterlagen(derKlasse('Unterlage')).length > 0;

// ---------------------------------------------------------------------------------------------- Gruppe Vorgaben

registrieren({
  id: 'koordination.vorgaben', titel: 'Projektvorgaben', symbol: 'koVorgaben', kuerzel: null, register: REGISTER, gruppe: 'Vorgaben',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => D.vorgabenDialog(),
});

// ---------------------------------------------------------------------------------------------- Gruppe Typenliste (Nachtrag C1c)

registrieren({
  id: 'koordination.typenliste', titel: 'Typenliste', symbol: 'koTypenliste', kuerzel: null, register: REGISTER, gruppe: 'Typenliste',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => D.typenDialog(),
});

// ---------------------------------------------------------------------------------------------- Gruppe Kollisionen

registrieren({
  id: 'koordination.pruefen', titel: 'Kollisionen prüfen', symbol: 'koPruefen', kuerzel: null, register: REGISTER, gruppe: 'Kollisionen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => D.pruefenDialog(),
});
registrieren({
  id: 'koordination.kollisionsliste', titel: 'Kollisionsliste', symbol: 'koListe', kuerzel: null, register: REGISTER, gruppe: 'Kollisionen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => panelOeffnen('kollisionen'),
});

// ---------------------------------------------------------------------------------------------- Gruppe Aussparungen

registrieren({
  id: 'koordination.aus-vorschlagen', titel: 'Aussparungen vorschlagen', symbol: 'koAusVorschlagen', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => D.vorschlagenDialog(),
});
registrieren({
  id: 'koordination.aus-nummerieren', titel: 'Aussparungen nummerieren', symbol: 'koAusNummer', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: () => projektOffen() && derKlasse('Aussparung').length > 0, hinweis: () => (projektOffen() ? 'Es gibt noch keine Aussparung (zuerst «Aussparungen vorschlagen»).' : KEIN_PROJEKT),
  ausfuehren: () => D.nummerierenDialog(),
});
registrieren({
  id: 'koordination.aus-liste', titel: 'Aussparungsliste', symbol: 'koAusListe', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => panelOeffnen('aussparungen'),
});
registrieren({
  id: 'koordination.aus-abgleich', titel: 'Abgleich', symbol: 'koAbgleich', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: () => projektOffen() && hatIfc(), hinweis: () => (projektOffen() ? 'Zuerst die Architekten-IFC übernehmen (Register Einfügen › IFC übernehmen).' : KEIN_PROJEKT),
  ausfuehren: () => D.abgleichDialog(),
});
registrieren({
  id: 'koordination.aus-plan', titel: 'Aussparungsplan anlegen', symbol: 'koAusPlan', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: () => projektOffen() && hatGeschoss(), hinweis: () => (projektOffen() ? 'Im Modell gibt es noch kein Geschoss.' : KEIN_PROJEKT),
  ausfuehren: () => D.planDialog(),
});
registrieren({
  id: 'koordination.aus-ifc', titel: 'IFC für Architekt', symbol: 'koAusIfc', kuerzel: null, register: REGISTER, gruppe: 'Aussparungen',
  aktiv: () => projektOffen() && !!ifcFaehig()?.vorhanden,
  hinweis: () => (!projektOffen() ? KEIN_PROJEKT : ifcFaehig() === null ? 'Der Kern wird noch befragt …' : T.IFC_FEHLT_HINWEIS),
  ausfuehren: () => D.ifcDialog(ifcFaehig()),
});

// ---------------------------------------------------------------------------------------------- Gruppe BCF

registrieren({
  id: 'koordination.bcf-export', titel: 'BCF exportieren', symbol: 'koBcfExport', kuerzel: null, register: REGISTER, gruppe: 'BCF',
  aktiv: () => projektOffen() && derKlasse('Kollision').length > 0, hinweis: () => (projektOffen() ? 'Es gibt noch keine Kollision (zuerst «Kollisionen prüfen»).' : KEIN_PROJEKT),
  ausfuehren: () => D.bcfExportDialog(),
});
registrieren({
  id: 'koordination.bcf-import', titel: 'BCF importieren', symbol: 'koBcfImport', kuerzel: null, register: REGISTER, gruppe: 'BCF',
  aktiv: projektOffen, hinweis: KEIN_PROJEKT, ausfuehren: () => D.bcfImportDialog(),
});

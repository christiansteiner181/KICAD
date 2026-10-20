// Aktionen des Registers «Koordination» (Modul 3b, Auftrag C1): Projektvorgaben, Kollisionen prüfen, Kollisionsliste, Aussparungen vorschlagen / nummerieren,
// Aussparungsliste, Abgleich, Aussparungsplan, IFC für den Architekten, BCF exportieren und importieren, seit Modul 7 Teil B3 dazu Koordinationsplan und Aussparungsboxen. Eigenes Register (wie «Hülle»): das Modul hat zehn Knöpfe
// in vier Gruppen und gehört weder zum Zeichnen (Gebäudetechnik) noch zur Verwaltung; Revit führt Austausch und Prüfung ebenfalls in einem eigenen Register
// («Zusammenarbeit»). Keine Fachlogik: alles geht über Befehle und Abfragen des Kerns, die Dialoge stehen in koordination-dialoge.js, die Listen in
// koordination-panel.js, Texte und Parameter in koordination-text.js (in Node getestet).

import { zustand, element, derKlasse, abonnieren } from '../zustand.js';
import { abfrage } from '../api.js';
import * as fenster from '../fenster.js';
import { registrieren } from './register.js';
import { meldung } from './dialoge.js';
import { sende, ergebnisMelden } from './befehle.js';
import { aufElement, freiesKennzeichen, freierName } from './werkzeug.js';
import * as D from './koordination-dialoge.js';
import { panelOeffnen } from './koordination-panel.js';
import * as T from './koordination-text.js';
import * as KP from './koordinationsplan.js';

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

// ---------------------------------------------------------------------------------------------- Gruppe Koordinationsplan (Modul 7 Teil B3)
// «Koordinationsplan» legt für das Geschoss der aktiven Ansicht einen Grundriss mit darstellung = koordination und Aussparungsboxen an (element_anlegen) oder öffnet den
// vorhandenen; «Aussparungsboxen» schaltet die Option aussparungsboxen der aktiven Grundrissansicht (element_aendern). Die Bausteine stehen in koordinationsplan.js (Node-Test),
// gezeichnet und gefärbt wird vom Kern.

const aktiveAnsicht = () => element(zustand.aktiveAnsicht);
const geschosseListe = () => derKlasse('Geschoss');

registrieren({
  id: 'koordination.plan', titel: 'Koordinations​plan', symbol: 'koPlan', kuerzel: null, register: REGISTER, gruppe: 'Koordinationsplan',
  aktiv: () => projektOffen() && KP.aktivesGeschoss(aktiveAnsicht(), geschosseListe()) !== null,
  hinweis: () => KP.planHinweis({ projektOffen: projektOffen(), aktiveAnsicht: aktiveAnsicht(), geschosse: geschosseListe() }),
  async ausfuehren() {
    const aktiv = aktiveAnsicht();
    const g = KP.aktivesGeschoss(aktiv, geschosseListe());
    if (!g) return;
    const vorhanden = KP.vorhandener(derKlasse('Ansicht'), g);
    if (vorhanden) { await fenster.ansichtOeffnen(vorhanden.kennzeichen); return; }
    const kz = freiesKennzeichen(KP.kennzeichen(g));
    const anzeige = freierName(KP.name(g), derKlasse('Ansicht').map((a) => a.name || a.kennzeichen));
    const r = await sende('element_anlegen', KP.anlegenParameter({ geschoss: g, massstab: KP.massstabFuer(aktiv), kz, anzeigeName: anzeige }));
    if (!ergebnisMelden(r, `Koordinationsplan «${anzeige}» angelegt.`)) return;
    if (await aufElement(kz)) await fenster.ansichtOeffnen(kz);
    else meldung('Der neue Koordinationsplan ist noch nicht eingetroffen.', 'warnung', { vorschlag: 'Im Projektbrowser nachsehen.' });
  },
});

registrieren({
  id: 'koordination.boxen', titel: 'Aussparungs​boxen', symbol: 'koBoxen', kuerzel: null, register: REGISTER, gruppe: 'Koordinationsplan',
  gedrueckt: () => KP.boxenAn(aktiveAnsicht()),
  aktiv: () => projektOffen() && KP.boxenHinweis(aktiveAnsicht()) === '',
  hinweis: () => (projektOffen() ? KP.boxenHinweis(aktiveAnsicht()) : KEIN_PROJEKT),
  async ausfuehren() {
    const a = aktiveAnsicht();
    if (!a) return;
    ergebnisMelden(await sende('element_aendern', KP.boxenParameter(a)), KP.boxenMeldung(a));
  },
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

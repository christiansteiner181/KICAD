// Aktionen der Zeichen-, Beschriftungs- und Ändern-Werkzeuge (Modul 1a, Phase C3): Menüband (Register Architektur, Beschriften,
// Ansicht, Ändern) und Revit-Kürzel. Jede Aktion startet nur ein Werkzeug aus js/werkzeuge/; die Werkzeuge arbeiten in der
// aktiven Plan-Ansicht (Schnittstelle Ansicht ↔ Werkzeug) und senden ihre Befehle über befehle.js.
//
// Kürzel (Revit): Wand WA, Tür DR, Fenster WN, Raum RM, Achse GR · Bemassung DI, Text TX, Detaillinie DL, Höhenkote EL ·
// Verschieben MV, Kopieren CO, Drehen RO, Spiegeln MM. Füllbereich, Nordpfeil, Schnitt haben (noch) kein Kürzel.
// Diese Datei wird vom Menüband geladen und überschreibt die Platzhalter «Wand, Tür, Fenster, Raum» aus aktionen.js (gleiche Ids).

import { registrieren } from './register.js';
import { planAnsicht, werkzeugStarten, werkzeugAktiv } from '../werkzeuge/basis.js';
import { hatAuswahl } from '../werkzeuge/auswahl.js';
import { griffeEinrichten } from '../werkzeuge/griffe.js';
import { wandWerkzeug } from '../werkzeuge/wand.js';
import { oeffnungWerkzeug } from '../werkzeuge/oeffnung.js';
import { raumWerkzeug } from '../werkzeuge/raum.js';
import { achseWerkzeug } from '../werkzeuge/achse.js';
import { bemassungWerkzeug } from '../werkzeuge/bemassung.js';
import { textWerkzeug } from '../werkzeuge/text.js';
import { linieWerkzeug } from '../werkzeuge/linie.js';
import { fuellbereichWerkzeug } from '../werkzeuge/fuellbereich.js';
import { koteWerkzeug, nordpfeilWerkzeug } from '../werkzeuge/kote.js';
import { schnittWerkzeug } from '../werkzeuge/schnitt.js';
import { verschiebenWerkzeug, kopierenWerkzeug, drehenWerkzeug, spiegelnWerkzeug } from '../werkzeuge/aendern.js';

const imGrundriss = () => planAnsicht()?.art === 'grundriss';
const inPlan = () => !!planAnsicht();
const mitAuswahl = () => inPlan() && hatAuswahl();

const nurGrundriss = () => (planAnsicht() ? 'Geht nur in einem Grundriss.' : 'Zuerst einen Grundriss öffnen und aktivieren.');
const nurPlan = () => 'Zuerst eine Plan-Ansicht (Grundriss, Schnitt oder Ansicht) öffnen und aktivieren.';
const nurAuswahl = () => (inPlan() ? 'Zuerst ein oder mehrere Elemente wählen (Strg+Klick für mehrere).' : nurPlan());

function werkzeug({ id, titel, symbol, kuerzel = null, register, gruppe, groesse, wo, fabrik, wid }) {
  const aktivFn = wo === 'grundriss' ? imGrundriss : wo === 'auswahl' ? mitAuswahl : inPlan;
  registrieren({
    id, titel, symbol, kuerzel, register, gruppe, ...(groesse ? { groesse } : {}),
    aktiv: aktivFn,
    hinweis: wo === 'grundriss' ? nurGrundriss : wo === 'auswahl' ? nurAuswahl : nurPlan,
    gedrueckt: () => werkzeugAktiv() === wid,
    ausfuehren() { werkzeugStarten(fabrik()); },
  });
}

// ---------------------------------------------------------------------------------------------- Architektur (nur Grundriss)

werkzeug({ id: 'arch.wand', titel: 'Wand', symbol: 'wand', kuerzel: 'WA', register: 'architektur', gruppe: 'Bauteile', wo: 'grundriss', wid: 'wand', fabrik: wandWerkzeug });
werkzeug({ id: 'arch.tuer', titel: 'Tür', symbol: 'tuer', kuerzel: 'DR', register: 'architektur', gruppe: 'Bauteile', wo: 'grundriss', wid: 'tuer', fabrik: () => oeffnungWerkzeug('Tuer') });
werkzeug({ id: 'arch.fenster', titel: 'Fenster', symbol: 'fenster', kuerzel: 'WN', register: 'architektur', gruppe: 'Bauteile', wo: 'grundriss', wid: 'fenster', fabrik: () => oeffnungWerkzeug('Fenster') });
werkzeug({ id: 'arch.raum', titel: 'Raum', symbol: 'raum', kuerzel: 'RM', register: 'architektur', gruppe: 'Raum', wo: 'grundriss', wid: 'raum', fabrik: raumWerkzeug });
werkzeug({ id: 'arch.achse', titel: 'Achse', symbol: 'achse', kuerzel: 'GR', register: 'architektur', gruppe: 'Raster', wo: 'grundriss', wid: 'achse', fabrik: achseWerkzeug });

// ---------------------------------------------------------------------------------------------- Beschriften (alle Plan-Ansichten)

werkzeug({ id: 'beschriften.bemassung', titel: 'Bemassung', symbol: 'bemassung', kuerzel: 'DI', register: 'beschriften', gruppe: 'Bemassung', wo: 'plan', wid: 'bemassung', fabrik: bemassungWerkzeug });
werkzeug({ id: 'beschriften.kote', titel: 'Höhenkote', symbol: 'kote', kuerzel: 'EL', register: 'beschriften', gruppe: 'Bemassung', wo: 'plan', wid: 'kote', fabrik: koteWerkzeug });
werkzeug({ id: 'beschriften.text', titel: 'Text', symbol: 'text', kuerzel: 'TX', register: 'beschriften', gruppe: 'Text', wo: 'plan', wid: 'text', fabrik: textWerkzeug });
werkzeug({ id: 'beschriften.linie', titel: 'Detail­linie', symbol: 'linie', kuerzel: 'DL', register: 'beschriften', gruppe: 'Detail', wo: 'plan', wid: 'linie', fabrik: linieWerkzeug });
werkzeug({ id: 'beschriften.fuellbereich', titel: 'Füll­bereich', symbol: 'fuellbereich', register: 'beschriften', gruppe: 'Detail', wo: 'plan', wid: 'fuellbereich', fabrik: fuellbereichWerkzeug });
werkzeug({ id: 'beschriften.nordpfeil', titel: 'Nord­pfeil', symbol: 'nordpfeil', register: 'beschriften', gruppe: 'Symbol', wo: 'plan', wid: 'nordpfeil', fabrik: nordpfeilWerkzeug });

// ---------------------------------------------------------------------------------------------- Ansicht: Schnitt

werkzeug({ id: 'ansicht.schnitt', titel: 'Schnitt', symbol: 'schnittlinie', register: 'ansicht', gruppe: 'Erstellen', wo: 'grundriss', wid: 'schnitt', fabrik: schnittWerkzeug });

// ---------------------------------------------------------------------------------------------- Ändern (mit Auswahl)

werkzeug({ id: 'aendern.verschieben', titel: 'Verschieben', symbol: 'verschieben', kuerzel: 'MV', register: 'aendern', gruppe: 'Verändern', groesse: 'klein', wo: 'auswahl', wid: 'verschieben', fabrik: verschiebenWerkzeug });
werkzeug({ id: 'aendern.kopieren', titel: 'Kopieren', symbol: 'duplizieren', kuerzel: 'CO', register: 'aendern', gruppe: 'Verändern', groesse: 'klein', wo: 'auswahl', wid: 'kopieren', fabrik: kopierenWerkzeug });
werkzeug({ id: 'aendern.drehen', titel: 'Drehen', symbol: 'drehen', kuerzel: 'RO', register: 'aendern', gruppe: 'Verändern', groesse: 'klein', wo: 'auswahl', wid: 'drehen', fabrik: drehenWerkzeug });
werkzeug({ id: 'aendern.spiegeln', titel: 'Spiegeln', symbol: 'spiegeln', kuerzel: 'MM', register: 'aendern', gruppe: 'Verändern', groesse: 'klein', wo: 'auswahl', wid: 'spiegeln', fabrik: spiegelnWerkzeug });

griffeEinrichten();

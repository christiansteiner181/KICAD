// Zeilen des Dialogs «Tastaturkürzel» (KS), nach Register geordnet (Nachbearbeitung N3, Punkt 4). Rein (ohne DOM), in Node getestet.
// Jedes Register des Menübands hat hier seinen Namen und seine Stelle in der Reihenfolge des Menübands; nur Aktionen ohne Register (z. B. Strg+D)
// oder mit einem unbekannten Register stehen unter «Allgemein» – kein Kürzel eines Registers geht mehr dorthin verloren.

/** Registerkennung → Anzeigename, in der Reihenfolge des Menübands (menueband.js REGISTER), dahinter Auswahl (Kontextregister) und Familie. */
export const REGISTER_NAMEN = {
  datei: 'Datei', architektur: 'Architektur', huelle: 'Hülle', gebaeudetechnik: 'Gebäudetechnik', einfuegen: 'Einfügen', beschriften: 'Beschriften',
  aendern: 'Ändern', koordination: 'Koordination', ansicht: 'Ansicht', verwalten: 'Verwalten', kontext: 'Auswahl', familie: 'Familie',
};
export const ALLGEMEIN = 'Allgemein';

const ohneWeich = (t) => String(t ?? '').replace(/[​­]/g, '');

/** Name des Registers einer Aktion für die Liste. */
export const registerName = (a) => REGISTER_NAMEN[a?.register] ?? ALLGEMEIN;

/**
 * Zeilen der Liste: Aktionen mit Kürzel, nach Register (Reihenfolge des Menübands, «Allgemein» zuletzt), innerhalb eines Registers in der Reihenfolge der Registrierung.
 *   aktionen  Aktionen { titel, kuerzel, kuerzelAlt[], register, aktiv(), … }
 *   hinweisVon(a) → Text, der erscheint, wenn die Aktion im Moment nicht verfügbar ist (aus register.js hinweisText)
 * Ergebnis: [{ titel, kuerzel, register, hinweis }]
 */
export function kuerzelZeilen(aktionen, hinweisVon = () => '') {
  const reihenfolge = [...Object.values(REGISTER_NAMEN), ALLGEMEIN];
  const rang = (r) => reihenfolge.indexOf(r);
  const zeilen = [...aktionen].filter((a) => a.kuerzel).map((a, i) => ({
    titel: ohneWeich(a.titel),
    kuerzel: [a.kuerzel, ...(a.kuerzelAlt ?? [])].join(' / '),
    register: registerName(a),
    hinweis: !a.aktiv?.() ? hinweisVon(a) : '',
    i,
  }));
  zeilen.sort((x, y) => rang(x.register) - rang(y.register) || x.i - y.i);
  return zeilen.map(({ i, ...z }) => z);
}

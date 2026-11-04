// Gliederung der Eigenschaftenpalette (Christian 04.11.2026: «die darstellung der eigenschaften etwas besser strukturieren») – rein, in Node getestet.
// - Beschriftung: lesbarer Titel statt des internen Namens (verlAnfang → «Verlängerung Anfang»); der Name steht im Tooltip.
// - Gruppen wie Revit: Eigenschaften (Auswahl, Text, Schalter), Abmessungen (Längen, Flächen, Winkel), Kennwerte (alle übrigen
//   Einheiten: Leistung, Temperatur, Volumenstrom, U-Wert …), Weitere (Werte ohne Katalogeintrag). Herkunft ist anfangs zugeklappt.

/** Feste Titel für Abkürzungen und Fachbegriffe; alles andere entsteht aus dem Namen (Binnenmajuskel → Wörter, ae/oe/ue in Wortstämmen). */
const TITEL = {
  verlAnfang: 'Verlängerung Anfang', verlEnde: 'Verlängerung Ende', lichteHoehe: 'Lichte Höhe', hoeheUeberMeer: 'Höhe über Meer',
  thetaE: 'Aussentemperatur θe', tempWinter: 'Raumtemperatur Winter', tempSommer: 'Raumtemperatur Sommer', co2Max: 'CO₂ max.',
  rWert: 'R-Wert', uWert: 'U-Wert', psiG: 'ψ-Wert', vlTemp: 'Vorlauftemperatur', rlTemp: 'Rücklauftemperatur',
  abgangVl: 'Abgang Vorlauf', abgangRl: 'Abgang Rücklauf', normVl: 'Norm-Vorlauf', normRl: 'Norm-Rücklauf', normRaum: 'Norm-Raumtemperatur',
  volumenstromMin: 'Volumenstrom min.', volumenstromMax: 'Volumenstrom max.', sanitaerArt: 'Apparateart', ifcDarstellung: 'IFC-Darstellung',
  ifcBezug: 'IFC-Bezug', ifcStandard: 'IFC-Standard', ifcSerien: 'IFC-Serien', ifcSchema: 'IFC-Schema', ifcProgramm: 'IFC-Programm',
  schnitthoeheNachSia: 'Schnitthöhe nach SIA 400', masseKnicke: 'Masse an Knicken', dn: 'DN', bu: 'Faktor bu', kote: 'Kote',
  familientyp: 'Familientyp', teilVon: 'Übergeordnet',
};

/** Wortstämme mit Umlaut, wie sie in den Namen ohne Umlaut stehen. */
const UMLAUTE = [['Hoehe', 'Höhe'], ['Laenge', 'Länge'], ['Flaeche', 'Fläche'], ['Waerme', 'Wärme'], ['Rueck', 'Rück'], ['Groesse', 'Grösse'],
  ['Zaehler', 'Zähler'], ['Druck', 'Druck'], ['Kuehl', 'Kühl'], ['Luefter', 'Lüfter'], ['Lueftung', 'Lüftung'], ['Oeffnung', 'Öffnung'],
  ['Gebaeude', 'Gebäude'], ['Waende', 'Wände'], ['Daemm', 'Dämm'], ['Gefaelle', 'Gefälle'], ['Gaeng', 'Gäng'], ['Vorschlaeg', 'Vorschläg'], ['Uebergang', 'Übergang'], ['Ueber', 'Über'], ['Aussen', 'Aussen'], ['Schluessel', 'Schlüssel']];

/** Lesbarer Titel einer Eigenschaft. */
export function eigenschaftTitel(name) {
  if (!name) return '';
  if (TITEL[name]) return TITEL[name];
  let t = String(name).replace(/([a-zäöü0-9])([A-Z])/g, '$1 $2');
  t = t.charAt(0).toUpperCase() + t.slice(1);
  for (const [a, b] of UMLAUTE) t = t.replace(new RegExp(`\\b${a}`, 'g'), b).replace(new RegExp(`\\b${a.toLowerCase()}`, 'g'), b.toLowerCase());
  return t;
}

/** Einheiten, die eine Abmessung angeben. */
const ABMESSUNG = new Set(['mm', 'm', 'm²', 'm2', 'm³', 'm3', '°', 'grad', 'Grad', 'DN']);

/** Gruppe einer Katalogeigenschaft: 'Eigenschaften' | 'Abmessungen' | 'Kennwerte'. */
export function eigenschaftsGruppe(d) {
  const e = d?.einheit;
  if (!e || d.typ !== 'zahl') return e && ABMESSUNG.has(e) ? 'Abmessungen' : e ? 'Kennwerte' : 'Eigenschaften';
  return ABMESSUNG.has(e) ? 'Abmessungen' : 'Kennwerte';
}

/** Reihenfolge der Gruppen aus dem Katalog in der Palette (nach Identität, Familie/Leitung und Zuordnung). */
export const GRUPPEN_FOLGE = ['Eigenschaften', 'Abmessungen', 'Kennwerte', 'Weitere'];

/** Gruppen, die beim ersten Öffnen zugeklappt sind (bis der Benutzer sie selbst öffnet). */
export const ANFANGS_ZU = ['Herkunft'];

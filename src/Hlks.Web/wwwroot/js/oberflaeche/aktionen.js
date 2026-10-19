// Register aller Aktionen der Bedienoberfläche. Menüband, Kontextmenüs und Tastaturkürzel lösen nur Aktionen aus:
//   ausfuehren(id, param?)   führt eine Aktion aus (nicht verfügbare zeigen ihren Hinweis)
//   aktionen                 Map id → Aktion { id, titel, symbol, kuerzel, kuerzelAlt, gruppe, register, groesse,
//                            ausfuehren(), aktiv(), hinweis, gedrueckt() }
// Die Aktionen stehen in aktionen-bearbeiten.js (Ändern, Kontext), aktionen-ansicht.js (Ansicht), aktionen-projekt.js
// (Datei, Verwalten), aktionen-plaene.js (Planblätter) und aktionen-zeichnen.js (Zeichen-, Beschriftungs- und
// Ändern-Werkzeuge, Modul 1a) und aktionen-einfuegen.js (Register Einfügen, Unterlagen, Koordinaten; Modul 1b) und aktionen-hkap.js (Verwalten › HKAP, nicht platzierte Räume; Modul 1d) und aktionen-huelle.js (Register Architektur › Decke, Boden, Dach und Register Hülle; Modul 1c) und familien/aktionen-familien.js (Kontextregister Familie, Familie platzieren, Aktionen des Projektbrowsers; Modul 2) und aktionen-schema.js (Schema anlegen, Werkzeuge der Schema-Ansicht, Im Grundriss/Schema zeigen; Modul 3, C2) und aktionen-leitung.js (Rohr PI und Kanal DT, Aufteilen mit Serie, Materialliste; Modul 3, C1) und aktionen-koordination.js (Register Koordination; Modul 3b, C1); die noch nicht verfügbaren Werkzeuge des Registers Gebäudetechnik stehen hier.

import { registrieren, aktionen } from './register.js';
import './aktionen-projekt.js';
import './aktionen-bearbeiten.js';
import './aktionen-ansicht.js';
import './aktionen-plaene.js';
import './aktionen-zeichnen.js';
import './aktionen-einfuegen.js';
import './aktionen-hkap.js';
import './aktionen-huelle.js';
import '../familien/aktionen-familien.js';
import '../familien/aktionen-unterlagen.js';
import '../familien/aktionen-bibliothek.js';   // Familienbibliothek: Explorer, Aktualisieren, Laden aus der Bibliothek (Modul 2, C3)
import './aktionen-schema.js';   // Schema-Ansicht (Modul 3, C2)
import './aktionen-leitung.js';   // Rohr und Kanal zeichnen, Aufteilen mit Serie, Materialliste (Modul 3, C1)
import './aktionen-koordination.js';   // Register Koordination: Projektvorgaben, Kollisionen, Aussparungen, BCF (Modul 3b, C1)
import './aktionen-serienpdf.js';   // Serien-PDF: Plansätze je Gewerk, Gewerk eines Planblatts (entwurf/serien-pdf.md)
import './aktionen-paletten.js';   // Register Ansicht › Paletten: je angemeldeter Palette ein Eintrag, Paletten zurücksetzen (W1)
import './aktionen-ki.js';   // Palette «Claude» (KI-Gespräch, Änderungen zurücknehmen, Normensuche) und Kürzel KI (Modul 3c, Teil O)

export { aktionen, ausfuehren, registrieren, istAktiv, hinweisText, modus, modusSetzen, beiModus, optionen, hinweis, hinweisSetzen, beiHinweis, familie, familieSetzen, beiFamilie } from './register.js';

// Aktionen, die im Menüband gross bleiben, bis ihre Gruppe zu einem Flyout zusammenklappt (Stufen M und K ändern sie nicht;
// Menüband-Teil R, entwurf/menueband-revit.md): die Hauptwerkzeuge, die man ständig anklickt.
export const IMMER_GROSS = [
  'aendern.auswaehlen',   // «Ändern» (Auswahl)
  'arch.wand',            // Wand
  'gt.rohr',              // Rohr zeichnen (PI)
  'gt.kanal',             // Kanal zeichnen (DT)
  'familie.platzieren',   // Familie platzieren (CM)
];
for (const id of IMMER_GROSS) { const a = aktionen.get(id); if (a) a.immerGross = true; else console.warn(`immerGross: Aktion ${id} fehlt`); }

// Platzhalter: sichtbar, aber gesperrt, bis die Module gebaut sind (Hinweis als Tooltip und Meldung).
const nochNicht = (modul) => `folgt in Modul ${modul}`;

[
  ['gt.formteil', 'Formteil', 'formteil', 'PF', 'Leitungen'],
  ['gt.sanitaerapparat', 'Sanitärapparat', 'apparat', 'PX', 'Apparate und Armaturen'],
  ['gt.armatur', 'Armatur', 'armatur', 'PA', 'Apparate und Armaturen'],
].forEach(([id, titel, symbol, kuerzel, gruppe]) => registrieren({
  id, titel, symbol, kuerzel, register: 'gebaeudetechnik', gruppe, aktiv: () => false, hinweis: nochNicht('2/3'), ausfuehren() { /* folgt */ },
}));

// Kontextmenü am Bauteil und Werkzeugkiste – reine Logik ohne DOM (in Node getestet).
// Bauteilmenü (Befund 04.11.2026: «kontextmenü bei aktiviertem bauteil zeigt nur "in schema zeigen"» – der Hörer des Schemas ersetzte das Menü
// des Grundrisses): ein Menü aus Ähnliches platzieren, Verbindungen, Schema, Bearbeiten (Verschieben, Kopieren, Drehen, Spiegeln), den
// verfügbaren Aktionen des Registers «kontext» und «Auswahl aufheben».
// Werkzeugkiste (Christian 04.11.2026: «im kontextmenü unten ein Rundbutton … bei hover radial angeordnete Hauptgruppen …, diese sollen dann bei
// hover die werkzeuge ausrollen»): Hauptgruppen = Register des Menübands mit Zeichen- und Bearbeitungswerkzeugen, Werkzeuge = ihre Aktionen.

/** Bearbeiten-Werkzeuge mit Auswahl, wie im Register Ändern. */
export const BEARBEITEN = ['aendern.verschieben', 'aendern.kopieren', 'aendern.drehen', 'aendern.spiegeln-waehlen'];

/** Kontext-Aktionen, die das Bauteilmenü schon anders anbietet (Verbindungen, Ähnliches, Schema) oder die im Grundriss nichts tun. */
const KONTEXT_OHNE = new Set(['kontext.aehnlich', 'kontext.verbinden', 'kontext.trennen', 'kontext.plus', 'kontext.minus', 'kontext.im-schema-zeigen', 'kontext.im-grundriss-zeigen']);

/** Kontext-Aktionen nur für Netzelemente (Leitungen, Formteile, Geräte mit Rolle); eine Wand hat kein Stromab. */
const NUR_NETZ = new Set(['kontext.netz-stromab', 'kontext.netz-stromauf', 'kontext.fliessrichtung', 'kontext.leitung-aufteilen', 'kontext.verbinden', 'kontext.trennen']);   // N36: Verbinden/Trennen dazu

/**
 * Einträge des Bauteilmenüs. istNetz: Element mit Rolle im Netz. aktionen: Liste der registrierten Aktionen ({ id, titel, register }), aktiv(id, param) → bool,
 * start(id, param) startet eine Aktion; aehnlich, netz und schema sind fertige Einträge ({ titel, aktion, aktiv?, hinweis? }).
 */
export function bauteilEintraege({ kennzeichen, aktionen, aktiv, start, aehnlich = [], netz = [], schema = [], aufheben, istNetz = true }) {
  const gruppen = [];
  const dazu = (liste) => { if (liste.length) gruppen.push(liste); };
  dazu([...aehnlich]);
  dazu([...netz]);
  dazu([...schema]);
  const nach = new Map(aktionen.map((a) => [a.id, a]));
  dazu(BEARBEITEN.filter((id) => nach.has(id) && aktiv(id)).map((id) => ({ titel: nach.get(id).titel.replace(/ – .*$/, ''), aktion: () => start(id) })));
  const param = { element: kennzeichen };
  const kontext = aktionen.filter((a) => a.register === 'kontext' && !KONTEXT_OHNE.has(a.id) && (istNetz || !NUR_NETZ.has(a.id)) && aktiv(a.id, param));
  const loeschen = kontext.filter((a) => a.id === 'kontext.loeschen');
  dazu(kontext.filter((a) => a.id !== 'kontext.loeschen').map((a) => ({ titel: a.titel, aktion: () => start(a.id, param) })));
  dazu([...loeschen.map((a) => ({ titel: a.titel, aktion: () => start(a.id, param) })), ...(aufheben ? [{ titel: 'Auswahl aufheben', aktion: aufheben }] : [])]);
  return gruppen.flatMap((g, i) => (i ? [{ trenner: true }, ...g] : g));
}

/** Hauptgruppen der Werkzeugkiste: Register mit Werkzeugen zum Zeichnen und Bearbeiten (Reihenfolge wie das Menüband). */
export const HAUPTGRUPPEN = [
  ['architektur', 'Architektur', 'wand'], ['huelle', 'Hülle', 'decke'], ['heizung', 'Heizung', 'rohr'], ['lueftung', 'Lüftung', 'kanal'],
  ['sanitaer', 'Sanitär', 'apparat'], ['msrl', 'MSRL', 'schema'],
  ['einfuegen', 'Einfügen', 'familie'], ['beschriften', 'Beschriften', 'planblatt'], ['aendern', 'Ändern', 'auswaehlen'],
];

/** Gruppen mit ihren Werkzeugen ({ id, titel, symbol, kuerzel }); leere Gruppen fallen weg. */
export function kisteGruppen(aktionen) {
  return HAUPTGRUPPEN.map(([register, titel, symbol]) => ({
    register, titel, symbol,
    werkzeuge: aktionen.filter((a) => [].concat(a.register ?? []).includes(register) && typeof a.ausfuehren === 'function' && a.titel)
      .map((a) => ({ id: a.id, titel: a.titel.replace(/[​­]/g, ''), symbol: a.symbol ?? null, kuerzel: a.kuerzel ?? null })),
  })).filter((g) => g.werkzeuge.length > 0);
}

/** Lage der Hauptgruppen auf dem Kreis um den Knopf (px, relativ zur Mitte), erste Gruppe oben, im Uhrzeigersinn. */
export function radialLagen(anzahl, radius) {
  return Array.from({ length: anzahl }, (_, i) => {
    const w = -Math.PI / 2 + (2 * Math.PI * i) / anzahl;
    return { x: Math.round(radius * Math.cos(w) * 10) / 10, y: Math.round(radius * Math.sin(w) * 10) / 10, winkel: w };
  });
}

/** Richtung, in die eine Gruppe ihre Werkzeuge ausrollt: nach aussen (links oder rechts vom ganzen Kranz, damit sie keine Gruppe verdeckt),
 *  oben bzw. unten bündig mit der Gruppe. */
export function ausrollSeite(lage) {
  return { seite: lage.x < -1 ? 'links' : 'rechts', unten: lage.y > 1 };
}

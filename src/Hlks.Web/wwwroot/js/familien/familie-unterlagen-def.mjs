// Testdefinition für den Browser-Test «Familie aus Unterlagen» (Modul 2, C2): die Familie, die die Attrappe (statt Claude) mit familie_entwurf anlegt.
// FIKTIV (kein Hersteller): Testventil mit den Typen V-15, V-20, V-25 aus der Tabelle des synthetischen PDF (tests/daten/unterlagen/datenblatt_testventil.pdf,
// erzeugt von tests/daten/unterlagen/erzeuger). ABSICHTLICHER WIDERSPRUCH: L von V-15 steht im PDF mit 67, in der DXF-Bemassung mit 70.
// B (Tiefe) = 30 hat keine Quelle (Vorgabewert der KI) → Abdeckung «fehlt», ein Mensch bestätigt ihn.
// (Die Tabellenzelle von H bei V-20 ist trotzdem der Soll-Wert des Massvergleichs: der Massbezug H nennt die Spalte.)
// Die Befund-Ids kennt erst der Kern nach dem Lesen der Dateien; der Test holt sie mit der Abfrage befunde und gibt sie hier hinein.

/**
 * befunde: { zelle(zeile, spalte) → Befund-Id der Tabellenzelle (Zeile/Spalte ab 1), dxfMass → Befund-Id der Bemassung 70 mm, bild → Befund-Id des Bildes
 * massbild_testventil.png }. H von V-20 stützt sich nur auf das Bild: Abdeckung «ungeprüft-bild» (nicht nachrechenbar, kein Fehler).
 */
export function testDefinition(befunde) {
  const { zelle, dxfMass, bild } = befunde;
  const typ = (name, zeile, dn, l, h, extraL = [], bildH = null) => ({
    name,
    werte: { dn, L: l, H: h },
    quellen: { dn: [zelle(zeile, 2)], L: [zelle(zeile, 3), ...extraL], H: [bildH ?? zelle(zeile, 4)] },
  });
  return {
    format: 1,
    name: 'Testventil C2 (fiktiv)',
    kategorie: 'Ventil',
    hersteller: 'keiner – Testfamilie (fiktiv)',
    produkt: 'Testventil',
    beschreibung: 'Synthetische Familie für den Browser-Test «Familie aus Unterlagen». Alle Werte fiktiv.',
    bericht: 'Gelesen: Tabelle Typ | DN | L | H auf Seite 1 des Datenblatts (drei Typen) und die Bemassung L im DXF-Massbild.\n'
      + 'Widerspruch: L von V-15 steht in der PDF-Tabelle mit 67 mm, in der DXF-Bemassung mit 70 mm – Entscheid nötig.\n'
      + 'Offen: die Tiefe B (30 mm) steht in keiner Unterlage; sie ist ein Vorgabewert der KI und braucht eine Bestätigung.',
    parameter: [
      { name: 'dn', art: 'typ', datentyp: 'dn', gruppe: 'anschluss' },
      { name: 'L', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', beschreibung: 'Baulänge' },
      { name: 'H', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', beschreibung: 'Bauhöhe' },
      { name: 'B', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', wert: 30, beschreibung: 'Bautiefe (nicht in der Unterlage)' },
    ],
    typen: [typ('V-15', 1, 15, 67, 35, [dxfMass]), typ('V-20', 2, 20, 78, 46, [], bild), typ('V-25', 3, 25, 87, 46)],
    koerper: [{ id: 'gehaeuse', name: 'Gehäuse', art: 'quader', material: 'armatur', von: [0, '-B / 2', 0], bis: ['L', 'B / 2', 'H'] }],
    anschluesse: [
      { name: 'ein', medium: '*', fluss: 'rein', art: 'rohr', form: 'rund', dn: 'dn', lage: [0, 0, 'H / 2'], richtung: [-1, 0, 0] },
      { name: 'aus', medium: '*', fluss: 'raus', art: 'rohr', form: 'rund', dn: 'dn', lage: ['L', 0, 'H / 2'], richtung: [1, 0, 0] },
    ],
    masse: [
      { name: 'L', ansicht: 'vorne', von: [0, 0, 0], bis: ['L', 0, 0], richtung: 'x', quellen: [zelle(1, 3), dxfMass] },
      { name: 'H', ansicht: 'vorne', parameter: 'H', quellen: [zelle(1, 4)] },
    ],
  };
}

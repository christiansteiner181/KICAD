// Arbeitsreihe eines Werkzeugs – rein, ohne Browser (in Node getestet). Ein Werkzeugschritt (Klick, Enter, Zifferneingabe, Esc) kann auf den
// Kern warten. Was der Benutzer in dieser Zeit tut, darf nicht verloren gehen (Prüfbefund Gesamtprüfung 13.11.2026: ein Klick während einer
// laufenden Abfrage wurde stumm verworfen – bei schnellem Zeichnen fehlte ein Punkt, Vorschau und Ergebnis wichen ab). Darum wird es in der
// Reihenfolge der Eingabe eingereiht und nach dem laufenden Schritt abgearbeitet.
//   ausfuehren(fn)  läuft sofort oder wird eingereiht (fn darf async sein)
//   Wirft ein Schritt, meldet `fehler` und die wartenden Schritte entfallen: sie bauten auf einem Stand auf, den es nun nicht gibt.
//   `danach` läuft einmal, wenn die Reihe leer ist (Vorschau und Hinweis nachführen); `fertig()` = true beendet die Reihe (Werkzeug beendet).

export const REIHE_MAX = 16;   // Annahme: mehr Eingaben staut niemand an, während ein Schritt rechnet; weitere entfallen

/** Wird ein Klick eingereiht, der kommt, während ein Schritt noch läuft? Nur bei Mehrpunkt-Werkzeugen (Wand, Leitung, Vorwand, Bemassung,
 *  Linien): dort ist er der nächste Punkt der Kette. Bei Einzelklick-Werkzeugen (Familie platzieren, Steig-/Fallleitung, Montageelement) ist
 *  ein zweiter Klick während der laufenden Abfrage Ungeduld – eingereiht setzte er ein zweites Bauteil an denselben Ort (Gegenprüfung B2);
 *  er entfällt wie früher. Ebenso, solange das Werkzeug auf den Benutzer wartet (offenes Textfeld). */
export function klickEinreihen(def, wartetAufBenutzer = false) { return !wartetAufBenutzer && !!def?.mehrpunkt; }

export class Arbeitsreihe {
  constructor({ fehler = () => {}, danach = () => {}, fertig = () => false } = {}) {
    this.fehler = fehler; this.danach = danach; this.fertig = fertig;
    this.wartend = [];
    this.beschaeftigt = false;
  }

  /** Wartende Schritte verwerfen (Werkzeug beendet). */
  leeren() { this.wartend.length = 0; }

  /** Wartet schon ein Schritt mit dieser Marke? (Esc wird nur einmal eingereiht.) */
  wartet(marke) { return this.wartend.some((w) => w.marke === marke); }

  async ausfuehren(fn, marke = null) {
    if (this.fertig()) return;
    if (this.beschaeftigt) { if (this.wartend.length < REIHE_MAX) this.wartend.push({ fn, marke }); return; }
    this.beschaeftigt = true;
    try {
      for (let schritt = { fn }; schritt && !this.fertig(); schritt = this.wartend.shift()) {
        try { await schritt.fn(); } catch (e) { this.wartend.length = 0; this.fehler(e); }
      }
    } finally {
      this.wartend.length = 0;
      this.beschaeftigt = false;
      this.danach();
    }
  }
}

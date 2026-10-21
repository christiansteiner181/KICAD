// «Durchgang» beim Begehen (Nachbearbeitung N22, Christian 21.10.2026: «bei begehung wände nur transparent wenn man durch geht +/- 50cm»):
// Wände, Decken, Dächer, Böden, Stützen und Unterzüge (Körperebenen wand und decke) sind während des Begehens undurchsichtig und werden nur durchscheinend,
// solange die Kamera näher als DURCHGANG_MM am Körper ist. Gemerkt werden die Werte der Materialien (Deckkraft, transparent, depthWrite), beim Beenden kommen
// genau diese zurück. Die Clipping-Ebenen des Schnittbereichs (N13) liegen auf denselben Materialien und werden nicht angefasst.
// Ohne three und DOM (die Körper kommen als Gruppen mit userData.ebene, .materialien, .formen, .huellbox aus koerper3d.js) – in Node getestet (durchgang.test.mjs).
// Angeschlossen in begehen3d.js (Option `bauteile`).

import { DURCHGANG_MM, DURCHGANG_EBENEN, durchgangDeckkraft, imDurchgang } from './begehen-logik.js';

/**
 * o: { bauteile() → Iterable<Gruppe> (alle Körper der Ansicht), kamera: { position: {x, y, z} }, anfordern(), beiAenderung?(zustand) }
 * Rückgabe { starten(), pruefen(), neueObjekte(), beenden(), aktiv, zustand() }
 */
export function erzeugeDurchgang(o) {
  const grenzeMm = o.grenzeMm ?? DURCHGANG_MM;
  let aktiv = false;
  let neu = true;                   // seit der letzten Prüfung sind Körper dazugekommen oder weggefallen
  let angemeldet = false;           // eine gesammelte Bewertung nach neuen Körpern steht an
  let lage = null;                  // Kamerastandort der letzten Prüfung (nur bei Bewegung neu rechnen)
  const verfolgt = new Map();       // Gruppe → { original: [{ m, deckkraft, transparent, depthWrite }], nah: bool | null }

  const istBauteil = (g) => DURCHGANG_EBENEN.includes(g?.userData?.ebene) && (g.userData.materialien?.length ?? 0) > 0;

  function materialSetzen(m, deckkraft, transparent, depthWrite) {
    if (m.transparent !== transparent) m.needsUpdate = true;   // «transparent» steckt im Shaderschlüssel
    m.opacity = deckkraft;
    m.transparent = transparent;
    m.depthWrite = depthWrite;
  }
  /** nah = true: durchscheinend (bisherige Deckkraft, sonst DURCHGANG_DECKKRAFT); false: undurchsichtig. */
  function darstellen(e, nah) {
    if (e.nah === nah) return false;
    e.nah = nah;
    for (const r of e.original) {
      if (nah) materialSetzen(r.m, durchgangDeckkraft(r.deckkraft), true, false);
      else materialSetzen(r.m, 1, false, true);
    }
    return true;
  }
  function zurueck(e) {
    for (const r of e.original) materialSetzen(r.m, r.deckkraft, r.transparent, r.depthWrite);
  }

  /** Gruppen abgleichen: neue merken und undurchsichtig machen, weggefallene vergessen. */
  function erfassen() {
    neu = false;
    const jetzt = new Set();
    for (const g of o.bauteile()) {
      if (!istBauteil(g)) continue;
      jetzt.add(g);
      if (verfolgt.has(g)) continue;
      const original = g.userData.materialien.map((m) => ({ m, deckkraft: m.opacity, transparent: m.transparent, depthWrite: m.depthWrite }));
      const e = { original, nah: null };
      verfolgt.set(g, e);
      darstellen(e, false);
    }
    for (const g of [...verfolgt.keys()]) if (!jetzt.has(g)) verfolgt.delete(g);   // (die Materialien weggefallener Körper sind schon freigegeben)
  }

  function kontrolle(geaendert) {
    const nah = [];
    let hart = 0;
    for (const [g, e] of verfolgt) {
      if (e.nah) nah.push({ id: g.userData.id, deckkraft: Math.round(durchgangDeckkraft(e.original[0]?.deckkraft) * 1000) / 1000 });
      else hart++;
    }
    const z = { grenzeMm, bauteile: verfolgt.size, undurchsichtig: hart, nah };
    o.beiAenderung?.(z);
    if (geaendert) o.anfordern?.();
    return z;
  }

  return {
    /** Beim Start des Begehens: alle Bauteile undurchsichtig, dann nach dem Standort der Kamera durchscheinend. */
    starten() {
      if (aktiv) { neu = true; this.pruefen(true); return; }   // Standort gewechselt (zweiter Aufruf von Begehen)
      aktiv = true;
      neu = true;
      lage = null;
      this.pruefen(true);
    },
    /** Nach jeder Bewegung der Kamera: Bauteile in Reichweite durchscheinend, die übrigen undurchsichtig. Ohne Bewegung (und ohne neue Körper) wird nichts gerechnet. */
    pruefen(erzwingen = false) {
      if (!aktiv) return;
      const p = o.kamera.position;
      if (!erzwingen && !neu && lage && lage.x === p.x && lage.y === p.y && lage.z === p.z) return;
      let geaendert = neu;
      if (neu) erfassen();
      lage = { x: p.x, y: p.y, z: p.z };
      for (const [g, e] of verfolgt) {
        if (darstellen(e, imDurchgang(lage, g.userData.formen, g.userData.huellbox, grenzeMm))) geaendert = true;
      }
      if (geaendert) kontrolle(true);
    },
    /** Es sind Körper dazugekommen oder weggefallen (Änderung am Modell während des Begehens). */
    neueObjekte() {
      if (!aktiv) return;
      neu = true;
      if (angemeldet) return;   // viele Änderungen hintereinander (alle Körper neu) werden einmal bewertet
      angemeldet = true;
      queueMicrotask(() => { angemeldet = false; this.pruefen(true); });
    },
    /** Beim Beenden: genau die vorherige Darstellung. */
    beenden() {
      if (!aktiv) return;
      aktiv = false;
      for (const e of verfolgt.values()) zurueck(e);
      verfolgt.clear();
      lage = null;
      o.beiAenderung?.(null);
    },
    get aktiv() { return aktiv; },
    zustand: () => kontrolle(false),
  };
}

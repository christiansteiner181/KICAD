// 3D-Schnittbereich der 3D-Ansicht (Nachbearbeitung N13, wie Revit «Schnittbereich»): sechs Clipping-Ebenen für alle Körper und die IFC-Netze,
// der Quader mit einem Griff je Fläche, «An Auswahl anpassen», «Auf Geschoss», «Zurücksetzen» und das Filtern der Treffer (gewählt wird nur,
// was sichtbar ist). Die Rechenregeln stehen in schnittbereich-logik.js (Node-getestet); gespeichert wird die Ansichtseigenschaft schnittbereich
// über den Kern (Funktion `speichern` der 3D-Ansicht), nie nur im Browser.
//
// Clipping: lokale Ebenen je Material (renderer.localClippingEnabled), damit Quader und Griffe selbst nicht abgeschnitten werden. Die Materialien
// kommen und gehen mit den Körpern; ansicht3d.js meldet neue Objekte mit neueObjekte(), die Ebenen werden vor dem nächsten Zeichnen gesetzt.
// Schnittflächen bleiben offen (kein Deckel): die Wände werden durchscheinend gezeichnet (Deckkraft ~0.3), ein undurchsichtiger Deckel würde
// sie verfälschen; Stencil-Kappen kosten je Ebene zwei weitere Durchläufe über das ganze Modell.
//
// Griffe: Kegel in der Mitte jeder Fläche, Spitze nach aussen, auf dem Bildschirm gleich gross und immer sichtbar. Ziehen verschiebt die Fläche entlang
// ihrer Achse (Strahl der Maus → nächster Punkt der Achse); gespeichert wird nach einer Pause von PAUSE_MS und beim Loslassen.

import * as THREE from 'three';
import { ebenen, griffe, ecken, weltGrenzen, enthaelt, schnittbereichLesen, fuerSpeichern, gleich, flaecheSchieben, achsenParameter, quaderSammler, aufGeschoss, winkelNormal, RAND_AUSWAHL } from './schnittbereich-logik.js';

/** Pause in ms nach der letzten Mausbewegung beim Ziehen, nach der gespeichert wird (sonst gäbe jeder Zug ein eigenes Paket). */
export const PAUSE_MS = 600;
const FARBE = 0xff8a00;
const FARBE_HOVER = 0xffd23f;
const GRIFF_GROESSE = 0.045;   // Höhe des Kegels im Verhältnis zum Abstand zur Kamera
const KANTEN = [[0, 1], [2, 3], [4, 5], [6, 7], [0, 2], [1, 3], [4, 6], [5, 7], [0, 4], [1, 5], [2, 6], [3, 7]];   // Index = 4·u + 2·v + z (Reihenfolge von ecken())

export function erzeugeSchnittbereich(o) {
  const { wurzel, dom, renderer, kamera, szene } = o;
  renderer.localClippingEnabled = true;

  const ebenenObjekte = Array.from({ length: 6 }, () => new THREE.Plane());
  let quader = null;           // { aktiv, min, max, winkel } – der angezeigte Stand (beim Ziehen vor dem Speichern)
  let schmutzig = true;        // Materialien müssen die Ebenen (wieder) bekommen
  let ziehend = null;          // { griff, start, t0, pointerId, geaendert }
  let ausstehend = 0;          // laufende Speicherungen
  let wartend = null;          // Stand, der nach der Pause gespeichert wird
  let timer = null;
  let hover = -1;
  let zerstoert = false;

  const gruppe = new THREE.Group();
  gruppe.visible = false;
  szene.add(gruppe);
  const linien = new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: FARBE, depthTest: false, transparent: true }));
  linien.renderOrder = 20;
  linien.raycast = () => {};
  linien.frustumCulled = false;
  gruppe.add(linien);
  const kegelGeo = new THREE.ConeGeometry(0.5, 1, 20);
  kegelGeo.translate(0, 0.5, 0);   // Fuss im Ursprung, Spitze bei +y
  const kegel = Array.from({ length: 6 }, (_, i) => {
    const m = new THREE.Mesh(kegelGeo, new THREE.MeshBasicMaterial({ color: FARBE, depthTest: false, transparent: true, opacity: 0.95 }));
    m.renderOrder = 21;
    m.userData.griff = i;
    m.frustumCulled = false;
    gruppe.add(m);
    return m;
  });
  let aktuelleGriffe = [];

  // ---------------------------------------------------------------------------------------------- Zustand

  const aktiv = () => !!quader?.aktiv;

  function darstellungAktualisieren() {
    gruppe.visible = aktiv();
    if (!quader) return;
    const e = ecken(quader);
    const pos = [];
    for (const [a, b] of KANTEN) pos.push(e[a].x, e[a].y, e[a].z, e[b].x, e[b].y, e[b].z);
    linien.geometry.dispose();
    linien.geometry = new THREE.BufferGeometry();
    linien.geometry.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    ebenen(quader).forEach((p, i) => ebenenObjekte[i].set(new THREE.Vector3(...p.normale), p.konstante));
    aktuelleGriffe = griffe(quader);
    wurzel.dataset.schnittbereich = JSON.stringify(aktiv() ? fuerSpeichern(quader) : null);
  }

  function uebernehmen(neu) {
    const war = aktiv();
    quader = neu;
    darstellungAktualisieren();
    if (war !== aktiv()) schmutzig = true;
    o.anfordern();
  }

  /** Stand aus dem Modell (Eigenschaft der Ansicht); während des Ziehens und Speicherns gilt der angezeigte Stand. */
  function setzen(roh) {
    if (zerstoert || ziehend || ausstehend > 0 || wartend) return;
    const neu = schnittbereichLesen(roh);
    if (gleich(neu, quader)) return;
    uebernehmen(neu);
  }

  async function abschicken() {
    clearTimeout(timer);
    if (!wartend) return;
    const wert = wartend;
    wartend = null;
    ausstehend++;
    let ok = false;
    try { ok = await o.speichern(wert); }
    catch (e) { console.error('3D: Schnittbereich nicht gespeichert', e); }
    finally { ausstehend--; }
    if (wartend && !ziehend) { abschicken(); return; }
    if (!ok && !ausstehend && !wartend) uebernehmen(schnittbereichLesen(o.gespeichert()));   // abgelehnt: Stand des Modells gilt
  }
  function vormerken(sb, sofort = false) {
    wartend = fuerSpeichern(sb);
    clearTimeout(timer);
    timer = setTimeout(abschicken, sofort ? 0 : PAUSE_MS);
  }

  /** Neuer Stand von aussen (Schalter, Auswahl, Geschoss, Zurücksetzen): sofort zeigen und speichern. */
  async function bearbeiten(sb) {
    uebernehmen(sb);
    vormerken(sb, true);
    await abschicken();
    return true;
  }

  // ---------------------------------------------------------------------------------------------- Quader bestimmen

  /** Alle Eckpunkte der Dreiecksnetze der Objekte in den Sammler (Weltpunkte in m). Linien (Kanten) zählen nicht. */
  function punkteSammeln(objekte, sammler) {
    const v = new THREE.Vector3();
    for (const obj of objekte) {
      obj.updateWorldMatrix(true, true);
      obj.traverse((m) => {
        const pos = m.isMesh ? m.geometry?.getAttribute('position') : null;
        if (!pos) return;
        for (let i = 0; i < pos.count; i++) { v.fromBufferAttribute(pos, i).applyMatrix4(m.matrixWorld); sammler.add(v.x, v.y, v.z); }
      });
    }
  }

  /** Quader um das ganze Modell (ohne Rand), in den Achsen der Gebäudeausrichtung; null, solange nichts geladen ist. */
  function standardQuader() {
    const s = quaderSammler(winkelNormal(o.ausrichtung()));
    punkteSammeln(o.objekte(), s);
    return s.quader(0);
  }

  // ---------------------------------------------------------------------------------------------- Bedienung (Aufrufe der Aktionen)

  const ergebnis = (ok, fehler = null) => ({ ok, fehler });

  const api = {
    get aktiv() { return aktiv(); },
    get quader() { return quader ? { ...quader, min: [...quader.min], max: [...quader.max] } : null; },
    get ziehend() { return !!ziehend; },
    get ausstehend() { return ausstehend > 0 || !!wartend; },
    setzen,
    neueObjekte() { schmutzig = true; o.anfordern(); },

    /** Schnittbereich ein- oder ausschalten; beim ersten Einschalten umschliesst er das ganze Modell. */
    async schalten(an) {
      const basis = quader ?? (an ? standardQuader() : null);
      if (!basis) return ergebnis(false, an ? 'Es ist noch nichts geladen, das der Schnittbereich umschliessen könnte.' : null);
      await bearbeiten({ ...basis, aktiv: !!an });
      return ergebnis(true);
    },
    /** Quader um die Auswahl plus RAND_AUSWAHL (300 mm). */
    async anAuswahl(rand = RAND_AUSWAHL) {
      const obj = o.auswahlObjekt();
      if (!obj) return ergebnis(false, 'Zuerst ein Element wählen, an das sich der Schnittbereich anpassen soll.');
      const s = quaderSammler(winkelNormal(quader?.winkel ?? o.ausrichtung()));
      punkteSammeln([obj], s);
      const q = s.quader(rand);
      if (!q) return ergebnis(false, 'Das gewählte Element hat in der 3D-Ansicht keine Körper.');
      await bearbeiten(q);
      return ergebnis(true);
    },
    /** Höhe auf OK/UK eines Geschosses (kote, hoehe in mm); der Grundriss bleibt, ohne Quader gilt das ganze Modell. */
    async aufGeschoss(kote, hoehe) {
      const basis = quader ?? standardQuader();
      if (!basis) return ergebnis(false, 'Es ist noch nichts geladen, das der Schnittbereich umschliessen könnte.');
      await bearbeiten({ ...aufGeschoss(basis, kote, hoehe), aktiv: true });
      return ergebnis(true);
    },
    /** Zurück auf das ganze Modell (der Schalter bleibt, wie er ist; ohne Quader schaltet es ein). */
    async zuruecksetzen() {
      const q = standardQuader();
      if (!q) return ergebnis(false, 'Es ist noch nichts geladen, das der Schnittbereich umschliessen könnte.');
      await bearbeiten({ ...q, aktiv: quader ? quader.aktiv : true });
      return ergebnis(true);
    },

    // ------------------------------------------------------------------------------------------ für ansicht3d.js
    /** Ebenen an die Materialien hängen (bei neuen Objekten) und die Griffe auf Bildschirmgrösse bringen; vor jedem Zeichnen. */
    vorRender() {
      if (schmutzig) {
        schmutzig = false;
        const planes = aktiv() ? ebenenObjekte : null;
        for (const g of o.gruppen()) g?.traverse((m) => {
          const mat = m.material;
          if (!mat) return;
          for (const x of Array.isArray(mat) ? mat : [mat]) { if (x.clippingPlanes !== planes) { x.clippingPlanes = planes; x.needsUpdate = true; } }
        });
      }
      if (!aktiv()) return;
      aktuelleGriffe.forEach((g, i) => {
        const k = kegel[i];
        const abstand = kamera.position.distanceTo(new THREE.Vector3(g.mitte.x, g.mitte.y, g.mitte.z));
        const s = Math.max(abstand * GRIFF_GROESSE, 0.02);
        k.scale.set(s * 0.8, s, s * 0.8);
        k.position.set(g.mitte.x, g.mitte.y, g.mitte.z);
        k.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), new THREE.Vector3(...g.nachAussen));
        k.material.color.setHex(i === hover || (ziehend && ziehend.griff === i) ? FARBE_HOVER : FARBE);
      });
    },
    /** Umschliessende Kugel des aktiven Quaders { mitte: Vector3, radius } in m, sonst null (Einpassen der Kamera). */
    kugelWelt() {
      if (!aktiv()) return null;
      const b = weltGrenzen(quader);
      const mitte = new THREE.Vector3((b.min.x + b.max.x) / 2, (b.min.y + b.max.y) / 2, (b.min.z + b.max.z) / 2);
      const e = ecken(quader);
      const r = Math.max(...e.map((p) => mitte.distanceTo(new THREE.Vector3(p.x, p.y, p.z))));
      return { mitte, radius: r };
    },
    /** Liegt der Weltpunkt im sichtbaren Bereich? (ohne aktiven Schnittbereich immer ja) */
    sichtbar(p) { return !aktiv() || enthaelt(quader, p); },
    /** Treffer des Strahls (three-Raycaster) auf das beschränken, was nicht weggeschnitten ist. */
    filtern(treffer) { return aktiv() ? treffer.filter((t) => enthaelt(quader, t.point)) : treffer; },
    zerstoeren() {
      zerstoert = true;
      clearTimeout(timer);
      wurzel.removeEventListener('pointerdown', unten, true);
      wurzel.removeEventListener('pointermove', bewegt, true);
      wurzel.removeEventListener('pointerup', oben, true);
      wurzel.removeEventListener('pointercancel', oben, true);
      linien.geometry.dispose(); linien.material.dispose();
      for (const k of kegel) k.material.dispose();
      kegelGeo.dispose();
      szene.remove(gruppe);
    },
  };

  // ---------------------------------------------------------------------------------------------- Griffe ziehen

  const strahl = new THREE.Raycaster();
  const ndc = new THREE.Vector2();
  function strahlFuer(e) {
    const r = dom.getBoundingClientRect();
    if (r.width < 1 || r.height < 1) return false;
    ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    strahl.setFromCamera(ndc, kamera);
    return true;
  }
  function griffAn(e) {
    if (!aktiv() || !strahlFuer(e)) return -1;
    kegel.forEach((k) => k.updateMatrixWorld(true));
    const t = strahl.intersectObjects(kegel, false)[0];
    return t ? t.object.userData.griff : -1;
  }
  /** Verschiebung der Fläche in mm aus der Mausposition, bezogen auf den Anfang des Ziehens. */
  function verschiebung(e) {
    const g = ziehend.griffDaten;
    if (!strahlFuer(e)) return null;
    const t = achsenParameter([strahl.ray.origin.x, strahl.ray.origin.y, strahl.ray.origin.z], [strahl.ray.direction.x, strahl.ray.direction.y, strahl.ray.direction.z],
      [g.mitte.x, g.mitte.y, g.mitte.z], g.nachAussen);
    return t === null ? null : t * 1000;
  }

  function unten(e) {
    if (e.button !== 0 || e.target !== dom || ziehend) return;
    const i = griffAn(e);
    if (i < 0) return;
    e.preventDefault();
    e.stopPropagation();
    o.beiBenutzt?.();
    const g = aktuelleGriffe[i];
    ziehend = { griff: i, griffDaten: g, start: { ...quader, min: [...quader.min], max: [...quader.max] }, t0: 0, pointerId: e.pointerId, geaendert: false };
    ziehend.t0 = verschiebung(e) ?? 0;
    try { dom.setPointerCapture(e.pointerId); } catch { /* egal */ }
    dom.style.cursor = 'grabbing';
    wurzel.dataset.griff = g.name;
    o.anfordern();
  }
  function bewegt(e) {
    if (ziehend) {
      if (e.pointerId !== ziehend.pointerId) return;
      e.stopPropagation();
      const d = verschiebung(e);
      if (d === null) return;
      const neu = flaecheSchieben(ziehend.start, ziehend.griffDaten.achse, ziehend.griffDaten.seite, d - ziehend.t0);
      if (gleich(neu, quader)) return;
      ziehend.geaendert = true;
      // die Fläche bleibt am Griff: die Achse des Griffs ändert sich beim Ziehen nicht (der Mittelpunkt der Fläche wandert nur entlang davon)
      uebernehmen(neu);
      vormerken(neu);
      return;
    }
    if (e.buttons || e.target !== dom) { if (hover >= 0) { hover = -1; dom.style.cursor = ''; o.anfordern(); } return; }
    const i = griffAn(e);
    if (i !== hover) { hover = i; dom.style.cursor = i >= 0 ? 'grab' : ''; o.anfordern(); }
  }
  function oben(e) {
    if (!ziehend || e.pointerId !== ziehend.pointerId) return;
    e.stopPropagation();
    const z = ziehend;
    ziehend = null;
    try { dom.releasePointerCapture(e.pointerId); } catch { /* egal */ }
    dom.style.cursor = hover >= 0 ? 'grab' : '';
    wurzel.dataset.griff = '';
    if (z.geaendert && e.type === 'pointerup') { vormerken(quader, true); abschicken(); }
    else if (z.geaendert) { uebernehmen(schnittbereichLesen(o.gespeichert()) ?? z.start); wartend = null; clearTimeout(timer); }   // abgebrochen: zurück auf den gespeicherten Stand
    o.anfordern();
  }
  wurzel.addEventListener('pointerdown', unten, true);
  wurzel.addEventListener('pointermove', bewegt, true);
  wurzel.addEventListener('pointerup', oben, true);
  wurzel.addEventListener('pointercancel', oben, true);

  setzen(o.gespeichert());
  return api;
}

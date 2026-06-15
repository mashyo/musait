(function () {
  "use strict";

  const ROLE_HEX = { tabletop: 0x2e2010, leg: 0x181210, apron: 0x221a0e };
  const ROLE_CSS = { tabletop: "#4a8fc0", leg: "#9b8fe8", apron: "#c89a3c", component: "#7a848d" };
  const SEL_HEX = 0x0e3828;
  const SEL_EDGE = 0x4caf82;
  const WARN_HEX = 0xc89a3c;
  const WARN_EDGE = 0xd4a84c;
  const EDGE_HEX_DARK = 0x6f7a83;
  const EDGE_HEX_LIGHT = 0x2f3940;

  let D = emptyDefinition();
  let ORIG = clone(D);
  let issues = [];
  let selId = null;
  let wireMode = false;
  let previewMode = "dark";
  let bindingNotice = "";
  let flexTimer = null;

  let scene = null;
  let camera = null;
  let renderer = null;
  let raycaster = null;
  let usingThree = false;
  let animationStarted = false;
  const meshById = {};
  let theta = 0.65;
  let phi = 1.05;
  let radius = 1750;
  let target = { x: 375, y: 370, z: 375 };
  let dragging = false;
  let didDrag = false;
  let lx = 0;
  let ly = 0;

  const cv = document.getElementById("cv");
  const fallbackScene = document.getElementById("fallbackScene");
  const componentList = document.getElementById("componentList");
  const parameterList = document.getElementById("parameterList");
  const componentHeader = document.getElementById("componentHeader");
  const parameterHeader = document.getElementById("parameterHeader");
  const editStrip = document.getElementById("editStrip");
  const issueStrip = document.getElementById("issueStrip");
  const vbadge = document.getElementById("vbadge");
  const meta = document.getElementById("familyMeta");
  const previewHint = document.getElementById("previewHint");
  const clearSelectionButton = document.getElementById("clearSelectionButton");

  function emptyDefinition() {
    return { category: "Family", host: "non-hosted", units: "mm", schema: "musait.family.rfa.v2", capability: "static", archetype: "", components: [], parameters: [], bindings: [], repeaters: [], diagnostics: [] };
  }

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function num(value, fallback) {
    const parsed = parseFloat(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  function cleanText(value, fallback) {
    const text = String(value || "").trim();
    return text || fallback;
  }

  function normalizeGeometry(value) {
    const text = cleanText(value, "extrusion").toLowerCase();
    if (text === "box" || text === "extrude" || text === "rectangular extrusion") return "extrusion";
    if (text === "cylinder" || text === "cylindrical" || text === "round" || text === "revolve" || text === "revolved") return "revolution";
    return text;
  }

  function isRevolution(component) {
    const geometry = normalizeGeometry(component.geometry);
    return geometry === "revolution" || geometry === "cylinder";
  }

  function normalizePayload(payload) {
    const raw = typeof payload === "string" ? JSON.parse(payload) : (payload || {});
    const root = raw.family || raw.familyDefinition || raw.definition || raw.data || raw.result || raw;
    const units = cleanText(root.units || root.unit, "mm");
    const components = Array.isArray(root.components) ? root.components : [];
    const parameters = Array.isArray(root.parameters) ? root.parameters : [];
    const bindings = Array.isArray(root.bindings) ? root.bindings : [];

    return {
      category: cleanText(root.category || root.familyCategory || root.revitCategory, "Family"),
      host: cleanText(root.host || root.hosting || root.hostType, "non-hosted"),
      units,
      schema: cleanText(root.schema, "musait.family.rfa.v2"),
      capability: cleanText(root.capability, "static"),
      archetype: cleanText(root.archetype, ""),
      components: components.map((component, index) => {
        const dims = component.dims || component.dimensions || {};
        const origin = component.origin || component.position || component.location || {};
        const rotation = component.rotation || component.rotate || {};
        const geometry = normalizeGeometry(component.geometry || component.geometryType || component.type);
        const radius = num(component.radius ?? component.r ?? dims.radius, NaN);
        const diameter = Number.isFinite(radius) && radius > 0 ? radius * 2 : 1;
        return {
          id: cleanText(component.id || component.name || component.label, "component_" + (index + 1)),
          geometry,
          role: cleanText(component.role || component.componentRole, "component"),
          origin: {
            x: num(origin.x ?? component.x ?? component.originX ?? component.positionX, 0),
            y: num(origin.y ?? component.y ?? component.originY ?? component.positionY, 0),
            z: num(origin.z ?? component.z ?? component.originZ ?? component.positionZ, 0)
          },
          rotation: { z: num(rotation.z ?? component.rotationZ, 0) },
          dims: {
            w: num(dims.w ?? dims.width ?? component.width, diameter),
            d: num(dims.d ?? dims.depth ?? component.depth, diameter),
            h: num(dims.h ?? dims.height ?? component.height, 1)
          },
          material: cleanText(component.material || component.materialName || component.finish, "Default"),
          finish: cleanText(component.finish || component.surfaceFinish || component.appearance, ""),
          radius: Number.isFinite(radius) && radius > 0 ? radius : null,
          isVoid: component.isVoid === true || component.void === true || component.is_void === true,
          isVisible: component.isVisible !== false && component.visible !== false && component.visibility !== false && component.hidden !== true
        };
      }),
      parameters: parameters.map((parameter, index) => ({
        name: cleanText(parameter.name || parameter.id || parameter.label, "Parameter" + (index + 1)),
        type: cleanText(parameter.type || parameter.parameterType || parameter.valueType, "Length"),
        default: parameter.default ?? parameter.defaultValue ?? parameter.value ?? "",
        instance: parameter.instance === true || parameter.isInstance === true || parameter.instanceParameter === true
      })),
      repeaters: Array.isArray(root.repeaters) ? root.repeaters : [],
      bindings: bindings.map(binding => ({
        parameter: cleanText(binding.parameter || binding.name || binding.parameterName, ""),
        inferred: binding.inferred === true,
        targets: (Array.isArray(binding.targets) ? binding.targets : []).map(target => ({
          component: cleanText(target.component || target.componentId || target.id, ""),
          path: cleanText(target.path || target.property || target.targetPath, ""),
          expression: cleanText(target.expression || target.expr || target.formula, "")
        }))
      })).filter(binding => binding.parameter && binding.targets.length),
      diagnostics: (Array.isArray(root.diagnostics) ? root.diagnostics : []).map((diagnostic, index) => ({
        id: "diagnostic-" + index,
        severity: cleanText(diagnostic.severity, "info").toLowerCase() === "error" ? "err" : "warn",
        label: cleanText(diagnostic.message, "Parametric diagnostic"),
        detail: [diagnostic.parameter, diagnostic.component].filter(Boolean).join(" · "),
        highlight: diagnostic.component ? [diagnostic.component] : [],
        fix: null
      }))
    };
  }

  function exportDefinition() {
    return {
      category: D.category,
      host: D.host,
      units: D.units,
      schema: "musait.family.v1",
      capability: D.capability || "static",
      archetype: D.archetype || "",
      components: D.components.map(component => ({
        id: component.id,
        geometry: component.geometry || "extrusion",
        role: component.role || "component",
        origin: {
          x: component.origin.x,
          y: component.origin.y,
          z: component.origin.z
        },
        rotation: {
          z: component.rotation?.z || 0
        },
        dims: {
          w: component.dims.w,
          d: component.dims.d,
          h: component.dims.h
        },
        material: component.material || "Default",
        finish: component.finish || "",
        radius: isRevolution(component) ? num(component.radius, Math.min(component.dims.w, component.dims.d) / 2) : component.radius,
        isVoid: component.isVoid === true,
        isVisible: component.isVisible !== false
      })),
      parameters: D.parameters.map(parameter => ({
        name: parameter.name,
        type: parameter.type || "Length",
        default: parameter.default,
        instance: parameter.instance === true
      })),
      bindings: (D.bindings || []).map(binding => ({
        parameter: binding.parameter,
        inferred: binding.inferred === true,
        targets: (binding.targets || []).map(target => ({
          component: target.component,
          path: target.path,
          expression: target.expression
        }))
      })),
      repeaters: D.repeaters || [],
      diagnostics: D.diagnostics || []
    };
  }

  function compBox(component) {
    return {
      x0: component.origin.x,
      y0: component.origin.y,
      z0: component.origin.z,
      x1: component.origin.x + component.dims.w,
      y1: component.origin.y + component.dims.d,
      z1: component.origin.z + component.dims.h
    };
  }

  function groupBox(components) {
    if (!components.length) return { x0: 0, y0: 0, z0: 0, x1: 1, y1: 1, z1: 1 };
    return components.reduce((box, component) => {
      const c = compBox(component);
      return {
        x0: Math.min(box.x0, c.x0),
        y0: Math.min(box.y0, c.y0),
        z0: Math.min(box.z0, c.z0),
        x1: Math.max(box.x1, c.x1),
        y1: Math.max(box.y1, c.y1),
        z1: Math.max(box.z1, c.z1)
      };
    }, { x0: 1e9, y0: 1e9, z0: 1e9, x1: -1e9, y1: -1e9, z1: -1e9 });
  }

  function validate() {
    const all = D.components.filter(component => component.isVisible !== false && !component.isVoid);
    if (!all.length) return [];

    const result = [];
    const allBox = groupBox(all);
    const tops = all.filter(component => component.role === "tabletop");
    const legs = all.filter(component => component.role === "leg");
    const aprons = all.filter(component => component.role === "apron");

    tops.forEach(top => {
      if (!legs.length) return;
      const lb = groupBox(legs);
      const tb = compBox(top);
      const overhang = Math.min(lb.x0 - tb.x0, tb.x1 - lb.x1, lb.y0 - tb.y0, tb.y1 - lb.y1);
      if (overhang < 5) {
        const rec = 25;
        result.push({
          id: "overhang",
          severity: "warn",
          label: "Tabletop overhang is " + (overhang < 0 ? "inverted" : overhang.toFixed(0) + D.units),
          detail: "Tabletop should read wider than the leg assembly in plan.",
          highlight: [top.id],
          fix: {
            type: "patch",
            id: top.id,
            changes: {
              "origin.x": lb.x0 - rec,
              "origin.y": lb.y0 - rec,
              "dims.w": (lb.x1 - lb.x0) + 2 * rec,
              "dims.d": (lb.y1 - lb.y0) + 2 * rec
            }
          }
        });
      }
    });

    const eps = 3;
    const legThk = legs.length ? Math.min(...legs.map(leg => Math.min(leg.dims.w, leg.dims.d))) : 20;
    const apronPatches = [];
    aprons.forEach(apron => {
      const ab = compBox(apron);
      const isYApron = apron.dims.d <= apron.dims.w;
      const changes = {};
      if (isYApron) {
        if (ab.y0 < allBox.y0 + eps) changes["origin.y"] = round1(allBox.y0 + legThk);
        if (ab.y1 > allBox.y1 - eps) changes["origin.y"] = round1(allBox.y1 - legThk - apron.dims.d);
      } else {
        if (ab.x0 < allBox.x0 + eps) changes["origin.x"] = round1(allBox.x0 + legThk);
        if (ab.x1 > allBox.x1 - eps) changes["origin.x"] = round1(allBox.x1 - legThk - apron.dims.w);
      }
      if (Object.keys(changes).length) apronPatches.push({ id: apron.id, changes });
    });

    if (apronPatches.length) {
      result.push({
        id: "apron-exposed",
        severity: "warn",
        label: apronPatches.length + " apron(s) flush with outer leg faces",
        detail: "Fix insets each apron by leg thickness (" + legThk + D.units + ").",
        highlight: apronPatches.map(patch => patch.id),
        fix: { type: "multi-patch", patches: apronPatches }
      });
    }

    for (let i = 0; i < all.length; i += 1) {
      for (let j = i + 1; j < all.length; j += 1) {
        const a = all[i];
        const b = all[j];
        if (a.origin.x === b.origin.x && a.origin.y === b.origin.y && a.origin.z === b.origin.z &&
          a.dims.w === b.dims.w && a.dims.d === b.dims.d && a.dims.h === b.dims.h) {
          result.push({
            id: "dup-" + a.id,
            severity: "err",
            label: "Duplicate geometry: " + a.id + " and " + b.id,
            detail: "Two non-void components share origin and dimensions.",
            highlight: [a.id, b.id],
            fix: null
          });
        }
      }
    }

    return result.concat(D.diagnostics || []);
  }

  function round1(value) {
    return Math.round(value * 10) / 10;
  }

  function setupThree() {
    if (!cv || typeof window.THREE === "undefined") {
      usingThree = false;
      cv.style.display = "none";
      fallbackScene.classList.add("on");
      previewHint.textContent = "CSS fallback preview · Click to select";
      return;
    }

    usingThree = true;
    cv.style.display = "block";
    fallbackScene.classList.remove("on");
    scene = new THREE.Scene();
    scene.background = new THREE.Color(previewMode === "light" ? 0xf6f7f8 : 0x080b0d);
    camera = new THREE.PerspectiveCamera(44, 1, 1, 12000);
    renderer = new THREE.WebGLRenderer({ canvas: cv, antialias: true });
    renderer.setPixelRatio(1);
    renderer.shadowMap.enabled = true;
    raycaster = new THREE.Raycaster();

    scene.add(new THREE.AmbientLight(0xffffff, 0.70));
    const light = new THREE.DirectionalLight(0xffffff, 1.05);
    light.position.set(700, 1400, 500);
    light.castShadow = true;
    scene.add(light);
    const fill = new THREE.DirectionalLight(0xdce8f3, 0.42);
    fill.position.set(-550, 650, 900);
    scene.add(fill);

    const grid = new THREE.GridHelper(
      2000,
      20,
      previewMode === "light" ? 0xb5c0c8 : 0x263039,
      previewMode === "light" ? 0xd2d9de : 0x182027
    );
    grid.position.set(375, 0, 375);
    scene.add(grid);

    const shadow = new THREE.Mesh(
      new THREE.PlaneGeometry(2500, 2500),
      new THREE.ShadowMaterial({ opacity: previewMode === "light" ? 0.28 : 0.2 })
    );
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.set(375, -1, 375);
    shadow.receiveShadow = true;
    scene.add(shadow);

    cv.addEventListener("mousedown", event => {
      dragging = true;
      didDrag = false;
      lx = event.clientX;
      ly = event.clientY;
    });
    window.addEventListener("mouseup", () => { dragging = false; });
    cv.addEventListener("mousemove", event => {
      if (!dragging) return;
      const dx = event.clientX - lx;
      const dy = event.clientY - ly;
      if (Math.abs(dx) + Math.abs(dy) > 2) didDrag = true;
      theta -= dx * 0.007;
      phi = Math.max(0.06, Math.min(1.48, phi - dy * 0.006));
      lx = event.clientX;
      ly = event.clientY;
    });
    cv.addEventListener("wheel", event => {
      radius = Math.max(120, Math.min(6000, radius + event.deltaY * 0.8));
      event.preventDefault();
    }, { passive: false });
    cv.addEventListener("click", event => {
      if (didDrag || !raycaster || !camera) return;
      const rect = cv.getBoundingClientRect();
      const mouse = new THREE.Vector2(
        ((event.clientX - rect.left) / rect.width) * 2 - 1,
        -((event.clientY - rect.top) / rect.height) * 2 + 1
      );
      raycaster.setFromCamera(mouse, camera);
      const hits = raycaster.intersectObjects(Object.values(meshById));
      select(hits.length ? hits[0].object.userData.id : null);
    });

    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);
    if (!animationStarted) {
      animationStarted = true;
      requestAnimationFrame(animate);
    }
  }

  function resizeCanvas() {
    if (!usingThree || !renderer || !camera || !cv) return;
    const rect = cv.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    cv.width = width * 2;
    cv.height = height * 2;
    renderer.setSize(width * 2, height * 2, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  }

  function buildMeshes() {
    if (!usingThree || !scene) {
      renderFallback();
      return;
    }

    Object.values(meshById).forEach(mesh => {
      scene.remove(mesh);
      mesh.traverse(child => {
        if (child.geometry) child.geometry.dispose();
        if (child.material) child.material.dispose();
      });
    });
    Object.keys(meshById).forEach(key => delete meshById[key]);

    const warnIds = new Set(issues.flatMap(issue => issue.highlight || []));
    D.components.forEach(component => {
      if (component.isVoid || component.isVisible === false) return;
      const dims = component.dims;
      const origin = component.origin;
      const geo = createGeometry(component);
      const isWarn = warnIds.has(component.id);
      const isSel = component.id === selId;
      const color = isSel ? SEL_HEX : (isWarn ? WARN_HEX : (ROLE_HEX[component.role] || 0x1a140e));
      const mat = new THREE.MeshPhongMaterial({ color, shininess: 6, wireframe: wireMode });
      if (isSel) mat.emissive.setHex(0x062415);

      const mesh = new THREE.Mesh(geo, mat);
      mesh.position.set(origin.x + dims.w / 2, origin.z + dims.h / 2, origin.y + dims.d / 2);
      mesh.rotation.y = -((component.rotation?.z || 0) * Math.PI / 180);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      mesh.userData = { id: component.id };

      const edges = new THREE.EdgesGeometry(geo);
      const edgeColor = isSel ? SEL_EDGE : (isWarn ? WARN_EDGE : (previewMode === "light" ? EDGE_HEX_LIGHT : EDGE_HEX_DARK));
      mesh.add(new THREE.LineSegments(edges, new THREE.LineBasicMaterial({
        color: edgeColor,
        transparent: true,
        opacity: isSel ? 0.8 : 0.42
      })));

      scene.add(mesh);
      meshById[component.id] = mesh;
    });
  }

  function createGeometry(component) {
    const dims = component.dims;
    if (isRevolution(component)) {
      const radius = Math.max(0.5, Math.min(dims.w, dims.d) / 2);
      return new THREE.CylinderGeometry(radius, radius, dims.h, 32);
    }

    return new THREE.BoxGeometry(dims.w, dims.h, dims.d);
  }

  function renderFallback() {
    const all = D.components.filter(component => component.isVisible !== false && !component.isVoid);
    const warnIds = new Set(issues.flatMap(issue => issue.highlight || []));
    const box = groupBox(all);
    const width = box.x1 - box.x0 || 1;
    const depth = box.y1 - box.y0 || 1;
    const scale = Math.max(0.06, Math.min(0.55, 130 / Math.max(width, depth)));
    fallbackScene.innerHTML = '<div class="fallback-model" id="fallbackModel"></div>';
    const model = document.getElementById("fallbackModel");

    all.forEach(component => {
      const el = document.createElement("button");
      const dims = component.dims;
      const origin = component.origin;
      el.type = "button";
      el.className = "fb-box" + (isRevolution(component) ? " cyl" : "") + (component.id === selId ? " sel" : "") + (warnIds.has(component.id) ? " warn" : "");
      el.style.setProperty("--c", ROLE_CSS[component.role] || "#7a848d");
      el.style.left = ((origin.x - box.x0 - width / 2) * scale) + "px";
      el.style.top = ((origin.y - box.y0 - depth / 2) * scale) + "px";
      el.style.width = Math.max(3, dims.w * scale) + "px";
      el.style.height = Math.max(3, dims.d * scale) + "px";
      el.title = component.id;
      el.addEventListener("click", event => {
        event.stopPropagation();
        select(component.id);
      });
      model.appendChild(el);
    });
  }

  function updateCamera() {
    if (!usingThree || !camera) return;
    const sp = Math.sin(phi);
    const cp = Math.cos(phi);
    const st = Math.sin(theta);
    const ct = Math.cos(theta);
    camera.position.set(target.x + radius * sp * st, target.y + radius * cp, target.z + radius * sp * ct);
    camera.lookAt(target.x, target.y, target.z);
  }

  function animate() {
    requestAnimationFrame(animate);
    if (!usingThree || !renderer || !scene || !camera) return;
    updateCamera();
    renderer.render(scene, camera);
  }

  function resetView() {
    if (flexTimer) {
      clearInterval(flexTimer);
      flexTimer = null;
      const button = document.getElementById("flexbtn");
      if (button) button.classList.remove("on");
    }

    D = clone(ORIG);
    selId = null;
    theta = 0.65;
    phi = 1.05;
    refreshAll(true);
  }

  function fitView() {
    const all = D.components.filter(component => component.isVisible !== false && !component.isVoid);
    const box = groupBox(all);
    target = {
      x: (box.x0 + box.x1) / 2,
      y: (box.z0 + box.z1) / 2,
      z: (box.y0 + box.y1) / 2
    };
    const longest = Math.max(box.x1 - box.x0, box.y1 - box.y0, box.z1 - box.z0, 200);
    radius = Math.max(500, longest * 2.25);
  }

  function toggleWire() {
    wireMode = !wireMode;
    document.getElementById("wirebtn").classList.toggle("on", wireMode);
    Object.values(meshById).forEach(mesh => { mesh.material.wireframe = wireMode; });
  }

  function flexAll() {
    const button = document.getElementById("flexbtn");
    if (flexTimer) {
      clearInterval(flexTimer);
      flexTimer = null;
      if (button) button.classList.remove("on");
      renderControls();
      return;
    }

    const numeric = D.parameters
      .map((parameter, index) => ({ parameter, index }))
      .filter(item => ["Length", "Number", "Integer"].includes(item.parameter.type) && Number.isFinite(num(item.parameter.default, NaN)))
      .map(item => {
        const original = num(item.parameter.default, 0);
        return { ...item, original, range: parameterRange(original, item.parameter.type) };
      });
    if (!numeric.length) return;

    const originals = numeric.map(item => ({ index: item.index, value: item.original }));
    const ticksPerParameter = 36;
    let tick = 0;
    if (button) button.classList.add("on");

    flexTimer = setInterval(() => {
      const parameterIndex = Math.floor(tick / ticksPerParameter);
      if (parameterIndex >= numeric.length) {
        clearInterval(flexTimer);
        flexTimer = null;
        originals.forEach(item => {
          D.parameters[item.index].default = item.value;
        });
        applyBindings(true);
        if (button) button.classList.remove("on");
        renderControls();
        return;
      }

      const item = numeric[parameterIndex];
      const phase = (tick % ticksPerParameter) / (ticksPerParameter - 1);
      const value = phase < 0.5
        ? item.range.min + (item.range.max - item.range.min) * (phase * 2)
        : item.range.max - (item.range.max - item.range.min) * ((phase - 0.5) * 2);
      updateParameter(item.index, item.parameter.type === "Integer" ? Math.round(value) : value, false);
      tick += 1;
    }, 70);
  }

  function setPreviewMode(mode) {
    previewMode = mode === "light" ? "light" : "dark";
    applyPreviewMode();
    if (usingThree && scene) {
      scene.background = new THREE.Color(previewMode === "light" ? 0xf6f7f8 : 0x080b0d);
    }
    buildMeshes();
  }

  function applyPreviewMode() {
    document.documentElement.classList.toggle("light-preview", previewMode === "light");
  }

  function select(id) {
    selId = id;
    buildMeshes();
    renderControls();
  }

  function updateComponent(id, objName, key, raw) {
    const value = parseFloat(raw);
    if (!Number.isFinite(value)) return;
    const component = D.components.find(item => item.id === id);
    if (!component) return;
    component[objName][key] = value;
    refreshAll(false);
  }

  function updateParameter(index, raw, renderUi) {
    const parameter = D.parameters[index];
    if (!parameter) return;
    if (parameter.type === "YesNo") {
      parameter.default = raw === true;
    } else if (parameter.type === "Integer") {
      parameter.default = Math.round(num(raw, 0));
    } else if (parameter.type === "Length" || parameter.type === "Number") {
      parameter.default = num(raw, 0);
    }
    applyBindings(renderUi !== false);
  }

  function applyBindings(renderUi) {
    ensureBindingFallback();
    const values = {};
    D.parameters.forEach(parameter => {
      const n = num(parameter.default, NaN);
      if (Number.isFinite(n)) values[parameter.name] = n;
    });

    (D.bindings || []).forEach(binding => {
      (binding.targets || []).forEach(target => {
        const component = D.components.find(item => item.id === target.component);
        if (!component) return;
        const value = evaluateExpression(target.expression || binding.parameter, values);
        if (!Number.isFinite(value)) return;
        setComponentPath(component, target.path, round1(Math.max(pathMinimum(target.path), value)));
      });
    });

    refreshAll(false, renderUi !== false);
  }

  function ensureBindingFallback() {
    if (D.bindings && D.bindings.length) return;
    const names = new Set(D.parameters.map(parameter => parameter.name));
    const needed = ["Width", "Depth", "Height"];
    if (!needed.every(name => names.has(name))) return;

    const top = D.components.find(component => component.role === "tabletop");
    const legs = D.components.filter(component => component.role === "leg");
    const aprons = D.components.filter(component => component.role === "apron");
    const legInset = names.has("LegInset") ? "LegInset" : "50";
    const legThickness = names.has("LegThickness") ? "LegThickness" : (names.has("LegWidth") ? "LegWidth" : "50");
    const topThickness = names.has("TabletopThickness") ? "TabletopThickness" : "30";
    const targets = [];

    if (top) {
      targets.push({ component: top.id, path: "dims.w", expression: "Width" });
      targets.push({ component: top.id, path: "dims.d", expression: "Depth" });
      targets.push({ component: top.id, path: "origin.z", expression: "Height - " + topThickness });
      targets.push({ component: top.id, path: "dims.h", expression: topThickness });
    }

    legs.forEach(leg => {
      const isRight = leg.origin.x > (top ? top.dims.w / 2 : 0);
      const isBack = leg.origin.y > (top ? top.dims.d / 2 : 0);
      targets.push({ component: leg.id, path: "dims.w", expression: legThickness });
      targets.push({ component: leg.id, path: "dims.d", expression: legThickness });
      targets.push({ component: leg.id, path: "dims.h", expression: "Height - " + topThickness });
      targets.push({ component: leg.id, path: "origin.x", expression: isRight ? "Width - " + legInset + " - " + legThickness : legInset });
      targets.push({ component: leg.id, path: "origin.y", expression: isBack ? "Depth - " + legInset + " - " + legThickness : legInset });
    });

    aprons.forEach(apron => {
      const isWide = apron.dims.w >= apron.dims.d;
      targets.push({ component: apron.id, path: isWide ? "dims.w" : "dims.d", expression: isWide ? "Width - (2 * " + legInset + ")" : "Depth - (2 * " + legInset + ")" });
      targets.push({ component: apron.id, path: "origin.z", expression: "Height - " + topThickness + " - " + (names.has("ApronHeight") ? "ApronHeight" : String(apron.dims.h)) });
      if (isWide) targets.push({ component: apron.id, path: "origin.x", expression: legInset });
      else targets.push({ component: apron.id, path: "origin.y", expression: legInset });
    });

    D.bindings = [{ parameter: "Width", inferred: true, targets }];
    bindingNotice = " · inferred bindings";
  }

  function evaluateExpression(expression, values) {
    const normalizedValues = createNormalizedValues(values || {});
    const normalizedExpression = normalizeExpression(String(expression || ""), values || {});
    const tokens = normalizedExpression.match(/[A-Za-z_][A-Za-z0-9_.]*|\d+(?:\.\d+)?|[()+\-*/]/g) || [];
    let index = 0;
    const peek = () => tokens[index];
    const take = () => tokens[index++];
    const factor = () => {
      const token = take();
      if (token === "(") {
        const value = expr();
        if (peek() === ")") take();
        return value;
      }
      if (token === "-") return -factor();
      if (/^\d/.test(token || "")) return parseFloat(token);
      return normalizedValues[token] ?? NaN;
    };
    const term = () => {
      let value = factor();
      while (peek() === "*" || peek() === "/") {
        const op = take();
        const right = factor();
        value = op === "*" ? value * right : value / right;
      }
      return value;
    };
    const expr = () => {
      let value = term();
      while (peek() === "+" || peek() === "-") {
        const op = take();
        const right = term();
        value = op === "+" ? value + right : value - right;
      }
      return value;
    };
    const value = expr();
    return index === tokens.length ? value : NaN;
  }

  function createNormalizedValues(values) {
    const normalized = {};
    Object.keys(values || {}).forEach(key => {
      normalized[key] = values[key];
      normalized[safeIdentifier(key)] = values[key];
    });
    return normalized;
  }

  function normalizeExpression(expression, values) {
    let result = expression || "";
    Object.keys(values || {}).sort((a, b) => b.length - a.length).forEach(key => {
      const safe = safeIdentifier(key);
      if (key === safe) return;
      const pattern = new RegExp("(^|[^A-Za-z0-9_.])" + escapeRegExp(key) + "(?=$|[^A-Za-z0-9_.])", "gi");
      result = result.replace(pattern, (_, prefix) => prefix + safe);
    });
    return result.replace(/\s*([()+\-*/])\s*/g, "$1").trim();
  }

  function safeIdentifier(value) {
    const safe = String(value || "").trim().replace(/[^\w.]+/g, "_").replace(/_+/g, "_").replace(/^_+|_+$/g, "");
    return safe || "Value";
  }

  function escapeRegExp(value) {
    return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }

  function setComponentPath(component, path, value) {
    const map = {
      "dims.w": ["dims", "w"],
      "dims.d": ["dims", "d"],
      "dims.h": ["dims", "h"],
      "origin.x": ["origin", "x"],
      "origin.y": ["origin", "y"],
      "origin.z": ["origin", "z"],
      "rotation.z": ["rotation", "z"]
    };
    const parts = map[path];
    if (!parts) return;
    component[parts[0]][parts[1]] = value;
  }

  function pathMinimum(path) {
    return path.startsWith("dims.") ? 1 : -100000;
  }

  function revertComp(id) {
    const original = ORIG.components.find(component => component.id === id);
    const live = D.components.find(component => component.id === id);
    if (!original || !live) return;
    live.origin = clone(original.origin);
    live.dims = clone(original.dims);
    live.rotation = clone(original.rotation);
    live.isVisible = original.isVisible !== false;
    refreshAll(false);
  }

  function toggleComponentVisibility(id) {
    const component = D.components.find(item => item.id === id);
    if (!component) return;
    component.isVisible = component.isVisible === false;
    refreshAll(true);
  }

  function applyFix(issueId) {
    const issue = issues.find(item => item.id === issueId);
    if (!issue || !issue.fix) return;
    const patch = (id, changes) => {
      const component = D.components.find(item => item.id === id);
      if (!component) return;
      Object.keys(changes).forEach(path => {
        const parts = path.split(".");
        component[parts[0]][parts[1]] = changes[path];
      });
    };

    if (issue.fix.type === "patch") patch(issue.fix.id, issue.fix.changes);
    if (issue.fix.type === "multi-patch") issue.fix.patches.forEach(item => patch(item.id, item.changes));
    refreshAll(false);
  }

  function refreshAll(fit, renderUi) {
    issues = validate();
    if (fit) fitView();
    buildMeshes();
    if (renderUi !== false) renderControls();
  }

  function renderControls() {
    renderHeader();
    renderComponents();
    renderParameters();
    renderEditStrip();
    renderIssues();
  }

  function renderHeader() {
    const visibleCount = D.components.filter(component => component.isVisible !== false && !component.isVoid).length;
    const issueCount = issues.length;
    const hasError = issues.some(issue => issue.severity === "err");
    const capability = capabilityLabel(D.capability);
    if (vbadge) {
      vbadge.className = "badge " + capabilityClass(D.capability, issueCount, hasError);
      vbadge.textContent = issueCount ? capability + " · " + issueCount + " issue" + (issueCount === 1 ? "" : "s") : capability;
    }
    meta.textContent = D.category + " · " + D.host + " · " + D.units + " · " + (D.schema || "v1") + (D.archetype ? " · " + D.archetype : "") + " · " + visibleCount + bindingNotice;
  }

  function capabilityLabel(value) {
    const text = String(value || "static").toLowerCase();
    if (text === "native_parametric") return "Native Parametric";
    if (text === "hybrid") return "Hybrid";
    return "Static";
  }

  function capabilityClass(value, issueCount, hasError) {
    if (hasError) return "be";
    if (issueCount) return "bw";
    const text = String(value || "static").toLowerCase();
    if (text === "native_parametric") return "bn";
    if (text === "hybrid") return "bw";
    return "bs";
  }

  function renderComponents() {
    const visible = D.components.filter(component => !component.isVoid);
    const roles = {};
    const warnIds = new Set(issues.flatMap(issue => issue.highlight || []));
    visible.forEach(component => {
      const role = component.role || "component";
      if (!roles[role]) roles[role] = [];
      roles[role].push(component);
    });

    componentHeader.textContent = "Components · " + visible.length;
    componentList.innerHTML = "";
    const roleOrder = ["tabletop", "leg", "apron"];
    roleOrder.concat(Object.keys(roles).filter(role => !roleOrder.includes(role))).forEach(role => {
      if (!roles[role]) return;
      roles[role].forEach(component => {
        const changed = componentChanged(component);
        const button = document.createElement("button");
        button.type = "button";
        button.className = "citem" +
          (component.id === selId ? " sel" : "") +
          (warnIds.has(component.id) ? " hi" : "") +
          (component.isVisible === false ? " off" : "");
        button.innerHTML =
          '<span class="cdot" style="--dot:' + (component.id === selId ? "#4caf82" : (warnIds.has(component.id) ? "#c89a3c" : (ROLE_CSS[role] || ROLE_CSS.component))) + '"></span>' +
          '<span class="cid">' + escapeHtml(component.id) + (changed ? ' <span style="color:#5a6470;font-size:9px">edited</span>' : "") + '</span>' +
          (component.isVisible === false ? '<span class="offmark">off</span>' : "") +
          '<span class="cdims' + (changed ? " changed" : "") + '">' + formatDims(component) + '</span>';
        button.addEventListener("click", event => {
          event.stopPropagation();
          select(component.id);
        });
        componentList.appendChild(button);
      });
    });
  }

  function renderParameters() {
    parameterHeader.textContent = "Parameters · " + D.parameters.length;
    parameterList.innerHTML = "";
    D.parameters.forEach((parameter, index) => {
      const type = parameter.type || "Text";
      if (type === "YesNo") {
        const row = document.createElement("div");
        row.className = "toggle-row";
        const on = parameter.default === true || String(parameter.default).toLowerCase() === "true";
        row.innerHTML = '<span class="tname">' + escapeHtml(parameter.name) + '</span><button type="button" class="toggle' + (on ? " on" : "") + '" title="Toggle"></button>';
        row.querySelector("button").addEventListener("click", () => {
          updateParameter(index, !on);
        });
        parameterList.appendChild(row);
        return;
      }

      const row = document.createElement("div");
      row.className = "prow";
      const isNumeric = type === "Length" || type === "Number" || type === "Integer";
      if (isNumeric && Number.isFinite(parseFloat(parameter.default))) {
        const value = num(parameter.default, 0);
        const range = parameterRange(value, type);
        const displayValue = formatNumber(value, type);
        row.innerHTML =
          '<div class="pname"><span>' + escapeHtml(parameter.name) + '</span><span class="ptype">' + escapeHtml(type) + '</span></div>' +
          '<div class="slider-row"><input type="number" class="pnum" min="' + range.min + '" max="' + range.max + '" step="' + range.step + '" value="' + displayValue + '" aria-label="' + escapeHtml(parameter.name) + ' value">' +
          '<input type="range" min="' + range.min + '" max="' + range.max + '" step="' + range.step + '" value="' + displayValue + '" aria-label="' + escapeHtml(parameter.name) + ' slider"></div>' +
          '<div class="range-meta"><span>' + formatNumber(range.min, type) + '</span><span>' + formatNumber(range.max, type) + '</span></div>';
        const numberInput = row.querySelector(".pnum");
        const slider = row.querySelector('input[type="range"]');
        slider.addEventListener("input", () => {
          const next = type === "Integer" ? Math.round(num(slider.value, value)) : num(slider.value, value);
          parameter.default = next;
          numberInput.value = formatNumber(next, type);
          updateParameter(index, next, false);
        });
        slider.addEventListener("change", () => renderControls());
        numberInput.addEventListener("input", () => {
          const next = type === "Integer" ? Math.round(num(numberInput.value, value)) : num(numberInput.value, value);
          if (!Number.isFinite(next)) return;
          const clamped = clamp(next, range.min, range.max);
          slider.value = String(clamped);
          updateParameter(index, clamped, false);
        });
        numberInput.addEventListener("change", () => {
          const next = clamp(num(numberInput.value, value), range.min, range.max);
          parameter.default = type === "Integer" ? Math.round(next) : next;
          renderControls();
        });
      } else {
        row.innerHTML =
          '<div class="pname"><span>' + escapeHtml(parameter.name) + '</span><span class="ptype">' + escapeHtml(type) + '</span></div>' +
          '<div class="text-param">' + escapeHtml(parameter.default === undefined ? "" : String(parameter.default)) + '</div>';
      }
      parameterList.appendChild(row);
    });
  }

  function renderEditStrip() {
    const component = selId ? D.components.find(item => item.id === selId) : null;
    if (!component) {
      editStrip.hidden = true;
      editStrip.innerHTML = "";
      return;
    }

    const d = component.dims;
    const o = component.origin;
    const edited = componentChanged(component);
    editStrip.hidden = false;
    editStrip.innerHTML =
      '<div class="es-head"><div class="es-label">Editing <span class="es-id">' + escapeHtml(component.id) + '</span></div>' +
      '<button class="vis-toggle' + (component.isVisible === false ? " off" : "") + '" type="button" onclick="toggleComponentVisibility(\'' + jsString(component.id) + '\')">' +
      (component.isVisible === false ? "Hidden" : "Visible") + '</button></div>' +
      '<div class="es-grid">' +
      editInput(component.id, "dims", "w", d.w, "W") +
      editInput(component.id, "dims", "d", d.d, "D") +
      editInput(component.id, "dims", "h", d.h, "H") +
      editInput(component.id, "origin", "x", o.x, "X") +
      editInput(component.id, "origin", "y", o.y, "Y") +
      editInput(component.id, "origin", "z", o.z, "Z") +
      '</div>' +
      (edited ? '<button class="revert" type="button" onclick="revertComp(\'' + jsString(component.id) + '\')">Revert this component</button>' : "");
  }

  function editInput(id, obj, key, value, label) {
    return '<label class="es-f"><span class="es-lbl">' + label + '</span>' +
      '<input class="es-inp" type="number" value="' + value + '" oninput="updateComponent(\'' + jsString(id) + '\',\'' + obj + '\',\'' + key + '\',this.value)"></label>';
  }

  function renderIssues() {
    issueStrip.hidden = issues.length === 0;
    issueStrip.innerHTML = "";
    issues.forEach(issue => {
      const row = document.createElement("div");
      row.className = "issue-row " + (issue.severity === "err" ? "err" : "warn");
      row.innerHTML =
        '<span class="idot"></span>' +
        '<span class="itext">' + escapeHtml(issue.label) + '<br><span style="color:#4a5460">' + escapeHtml(issue.detail) + '</span></span>' +
        (issue.fix ? '<button class="ifix" type="button">Fix</button>' : "");
      const fix = row.querySelector(".ifix");
      if (fix) fix.addEventListener("click", () => applyFix(issue.id));
      issueStrip.appendChild(row);
    });
  }

  function componentChanged(component) {
    const original = ORIG.components.find(item => item.id === component.id);
    if (!original) return false;
    return original.origin.x !== component.origin.x ||
      original.origin.y !== component.origin.y ||
      original.origin.z !== component.origin.z ||
      original.dims.w !== component.dims.w ||
      original.dims.d !== component.dims.d ||
      original.dims.h !== component.dims.h ||
      original.isVisible !== component.isVisible;
  }

  function formatDims(component) {
    if (isRevolution(component)) {
      return "dia " + component.dims.w + "x" + component.dims.h;
    }

    return component.dims.w + "x" + component.dims.d + "x" + component.dims.h;
  }

  function roundSlider(value, type) {
    if (!Number.isFinite(value)) return 0;
    const step = Math.abs(value) >= 100 ? 5 : 1;
    const rounded = Math.round(value / step) * step;
    return type === "Integer" ? Math.round(rounded) : rounded;
  }

  function parameterRange(value, type) {
    const abs = Math.max(Math.abs(value), 1);
    const spreadFactor = type === "Length" ? 0.35 : 0.5;
    const spreadFloor = type === "Length" ? Math.min(100, abs * 0.2) : Math.min(10, abs * 0.25);
    const spread = Math.max(spreadFloor, abs * spreadFactor);
    const minBase = type === "Length" ? Math.max(1, value - spread) : value - spread;
    const maxBase = type === "Length" ? Math.max(minBase + 1, value + spread) : value + spread;
    const step = niceStep((maxBase - minBase) / 160, type);
    return {
      min: roundToStep(minBase, step, type),
      max: roundToStep(maxBase, step, type),
      step
    };
  }

  function niceStep(raw, type) {
    if (type === "Integer") return Math.max(1, Math.round(raw));
    if (!Number.isFinite(raw) || raw <= 0) return 1;
    const power = Math.pow(10, Math.floor(Math.log10(raw)));
    const normalized = raw / power;
    const nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
    return Math.max(type === "Length" ? 1 : 0.01, nice * power);
  }

  function roundToStep(value, step, type) {
    const rounded = Math.round(value / step) * step;
    return type === "Integer" ? Math.round(rounded) : roundForDisplay(rounded);
  }

  function formatNumber(value, type) {
    return type === "Integer" ? String(Math.round(value)) : String(roundForDisplay(value));
  }

  function roundForDisplay(value) {
    return Math.round(value * 100) / 100;
  }

  function clamp(value, min, max) {
    if (!Number.isFinite(value)) return min;
    return Math.max(min, Math.min(max, value));
  }

  function escapeHtml(value) {
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function jsString(value) {
    return String(value).replace(/\\/g, "\\\\").replace(/'/g, "\\'");
  }

  function loadJSON(payload) {
    try {
      D = normalizePayload(payload);
      bindingNotice = (D.bindings || []).some(binding => binding.inferred === true) ? " · inferred bindings" : "";
      ensureBindingFallback();
      applyBindings(false);
      ORIG = clone(D);
      selId = null;
      refreshAll(true);
    } catch (error) {
      D = emptyDefinition();
      ORIG = clone(D);
      issues = [];
      buildMeshes();
      renderControls();
      vbadge.className = "badge be";
      vbadge.textContent = "Invalid";
      meta.textContent = "Preview payload failed";
    }
  }

  function exportJSON() {
    const blob = new Blob([JSON.stringify(exportDefinition(), null, 2)], { type: "application/json" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = "family.json";
    link.click();
    URL.revokeObjectURL(link.href);
  }

  function createRFA() {
    const payload = JSON.stringify(exportDefinition());
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ type: "create-rfa", action: "create-rfa", payload });
    } else {
      exportJSON();
    }
  }

  window.__loadJSON = loadJSON;
  window.resetView = resetView;
  window.fitView = fitView;
  window.toggleWire = toggleWire;
  window.flexAll = flexAll;
  window.exportJSON = exportJSON;
  window.createRFA = createRFA;
  window.updateComponent = updateComponent;
  window.revertComp = revertComp;
  window.toggleComponentVisibility = toggleComponentVisibility;
  window.__setTheme = setPreviewMode;

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", event => {
      const data = event.data;
      if (data && data.type === "theme") {
        setPreviewMode(data.mode);
        return;
      }

      loadJSON(data);
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    setupThree();
    if (clearSelectionButton) {
      clearSelectionButton.addEventListener("click", event => {
        event.stopPropagation();
        select(null);
      });
    }
    document.getElementById("previewRoot").addEventListener("click", event => {
      if (event.target.closest(".citem,.es-inp,.pnum,.revert,.vis-toggle,.issue-row,.preview-controls,.footer,.toggle-row,.prow")) return;
      if (event.target === cv) return;
      select(null);
    });
    applyPreviewMode();
    refreshAll(true);
  });
}());

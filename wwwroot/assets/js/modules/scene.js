/**
 * Three.js scene module.
 * Creates the monolith, satellite cubes, connection lines,
 * rings, particles, grid, and the animation loop.
 */
import * as THREE from 'three';

/** @type {boolean} */
let isClicking = false;
/** @type {number} */
let holdSpeed = 0;

/**
 * Initialize the 3D scene.
 * @param {HTMLCanvasElement} canvas - Target canvas element
 */
export function initScene(canvas) {
  // ── Renderer ──
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.setClearColor(0x000000, 1);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.2;

  // ── Camera ──
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 100);
  camera.position.set(0, 0, 7);

  // ── Mouse tracking ──
  const mouse = new THREE.Vector2();
  const targetMouse = new THREE.Vector2();
  document.addEventListener('mousemove', (e) => {
    targetMouse.x = (e.clientX / window.innerWidth - 0.5) * 2;
    targetMouse.y = -(e.clientY / window.innerHeight - 0.5) * 2;
  });

  // ── Lighting ──
  scene.add(new THREE.AmbientLight(0xffffff, 0.08));

  const spotLight1 = new THREE.SpotLight(0xffffff, 120);
  spotLight1.position.set(6, 8, 6);
  spotLight1.angle = Math.PI / 6;
  spotLight1.penumbra = 0.4;
  scene.add(spotLight1);

  const spotLight2 = new THREE.SpotLight(0xffffff, 60);
  spotLight2.position.set(-6, -4, 4);
  spotLight2.angle = Math.PI / 5;
  spotLight2.penumbra = 0.6;
  scene.add(spotLight2);

  const pointLight = new THREE.PointLight(0xffffff, 8, 12);
  pointLight.position.set(0, 0, 3);
  scene.add(pointLight);

  // ── Core Group ──
  const coreGroup = new THREE.Group();
  scene.add(coreGroup);

  // ── Materials ──
  const solidMat = new THREE.MeshStandardMaterial({
    color: 0x000000, metalness: 0.0, roughness: 0.05, emissive: 0x111111,
  });
  const wireMat = new THREE.MeshBasicMaterial({ color: 0xffffff, wireframe: true });

  // ── Central Monolith ──
  const monolithGeo = new THREE.BoxGeometry(1.6, 2.4, 1.6, 4, 4, 4);
  const monolithSolid = new THREE.Mesh(monolithGeo, solidMat.clone());
  const monolithWire = new THREE.Mesh(monolithGeo, wireMat.clone());
  monolithWire.material.opacity = 0.12;
  monolithWire.material.transparent = true;
  coreGroup.add(monolithSolid);
  coreGroup.add(monolithWire);

  const posAttr = monolithGeo.attributes.position;
  const origPositions = new Float32Array(posAttr.array);

  // ── Satellite Cubes ──
  const satellites = [];
  const satelliteCount = 8;
  const satGeo = new THREE.BoxGeometry(1, 1, 1);
  const satEdgesGeo = new THREE.EdgesGeometry(satGeo);

  for (let i = 0; i < satelliteCount; i++) {
    const angle = (i / satelliteCount) * Math.PI * 2;
    const radius = 2.8 + Math.random() * 0.6;
    const yOff = (Math.random() - 0.5) * 2.5;
    const scale = 0.35 + Math.random() * 0.3;

    const group = new THREE.Group();
    group.position.set(Math.cos(angle) * radius, yOff, Math.sin(angle) * radius * 0.5);
    group.scale.setScalar(scale);

    group.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), solidMat.clone()));
    group.add(new THREE.LineSegments(satEdgesGeo, new THREE.LineBasicMaterial({ color: 0xffffff })));
    coreGroup.add(group);

    satellites.push({
      group, baseAngle: angle, radius, yOff,
      speed: 0.2 + Math.random() * 0.4,
      rotSpeed: new THREE.Vector3(
        (Math.random() - 0.5) * 0.8,
        (Math.random() - 0.5) * 0.8,
        (Math.random() - 0.5) * 0.5
      ),
      phase: Math.random() * Math.PI * 2,
      baseScale: scale,
      driftSpeed1: 0.15 + Math.random() * 0.25,
      driftSpeed2: 0.08 + Math.random() * 0.15,
      driftPhase1: Math.random() * Math.PI * 2,
      driftPhase2: Math.random() * Math.PI * 2,
      driftAmount: 0.4 + Math.random() * 0.6,
    });
  }

  // ── Connection Lines ──
  const lineMaterial = new THREE.LineBasicMaterial({ color: 0xffffff, opacity: 0.08, transparent: true });
  const connectionLines = [];

  for (let i = 0; i < satelliteCount; i++) {
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(6), 3));
    const line = new THREE.Line(geo, lineMaterial.clone());
    scene.add(line);
    connectionLines.push(line);
  }

  // ── Background Grid ──
  const gridHelper = new THREE.GridHelper(30, 30, 0x1a1a1a, 0x111111);
  gridHelper.position.y = -4;
  gridHelper.material.opacity = 0.4;
  gridHelper.material.transparent = true;
  scene.add(gridHelper);

  // ── Particle Field ──
  const particleCount = 280;
  const pGeo = new THREE.BufferGeometry();
  const pPositions = new Float32Array(particleCount * 3);
  const pSizes = new Float32Array(particleCount);

  for (let i = 0; i < particleCount; i++) {
    pPositions[i * 3] = (Math.random() - 0.5) * 22;
    pPositions[i * 3 + 1] = (Math.random() - 0.5) * 16;
    pPositions[i * 3 + 2] = (Math.random() - 0.5) * 14 - 4;
    pSizes[i] = Math.random() * 1.8 + 0.4;
  }

  pGeo.setAttribute('position', new THREE.BufferAttribute(pPositions, 3));
  pGeo.setAttribute('size', new THREE.BufferAttribute(pSizes, 1));

  const particles = new THREE.Points(pGeo, new THREE.PointsMaterial({
    color: 0xffffff, size: 0.04, sizeAttenuation: true, opacity: 0.35, transparent: true,
  }));
  scene.add(particles);

  // ── Rings ──
  const ring = new THREE.Mesh(
    new THREE.TorusGeometry(2.2, 0.008, 4, 80),
    new THREE.MeshBasicMaterial({ color: 0xffffff, opacity: 0.25, transparent: true })
  );
  ring.rotation.x = Math.PI / 2.5;
  coreGroup.add(ring);

  const ring2 = new THREE.Mesh(
    new THREE.TorusGeometry(1.6, 0.005, 4, 60),
    new THREE.MeshBasicMaterial({ color: 0xffffff, opacity: 0.12, transparent: true })
  );
  ring2.rotation.x = Math.PI / 3.2;
  ring2.rotation.z = 0.4;
  coreGroup.add(ring2);

  // ── Interaction ──
  document.addEventListener('mousedown', () => { isClicking = true; });
  document.addEventListener('mouseup', () => { isClicking = false; });

  // ── Resize ──
  window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });

  // ── Animation Loop ──
  const clock = new THREE.Clock();
  // Pre-allocate reusable vectors to avoid GC pressure in the loop
  const _satWorldPos = new THREE.Vector3();
  const _monolithWorldPos = new THREE.Vector3();

  function animate() {
    requestAnimationFrame(animate);
    const t = clock.getElapsedTime();

    // Smooth mouse
    mouse.x += (targetMouse.x - mouse.x) * 0.06;
    mouse.y += (targetMouse.y - mouse.y) * 0.06;

    // Hold speed — ramps up while held, decays on release
    if (isClicking) {
      holdSpeed += (1 - holdSpeed) * 0.04;
    } else {
      holdSpeed *= 0.93;
    }
    const speedMul = 1 + holdSpeed * 4;

    // Core group rotation (follows mouse)
    coreGroup.rotation.y += (mouse.x * 0.6 - coreGroup.rotation.y) * 0.05;
    coreGroup.rotation.x += (-mouse.y * 0.35 - coreGroup.rotation.x) * 0.05;
    coreGroup.rotation.y += 0.003 * speedMul;

    // Monolith vertex morphing
    const pos = monolithGeo.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const ox = origPositions[i * 3];
      const oy = origPositions[i * 3 + 1];
      const oz = origPositions[i * 3 + 2];
      const len = Math.sqrt(ox * ox + oy * oy + oz * oz);
      const noise = Math.sin(t * 1.2 * speedMul + ox * 2.1 + oy * 1.8) * 0.06;
      pos.setXYZ(i, ox + (ox / len) * noise, oy + (oy / len) * noise * 0.7, oz + (oz / len) * noise);
    }
    pos.needsUpdate = true;
    monolithGeo.computeVertexNormals();

    monolithSolid.scale.setScalar(1);
    monolithWire.scale.setScalar(1.02);

    // Satellites
    satellites.forEach((s, i) => {
      const angle = s.baseAngle + t * s.speed * 0.3 * speedMul;
      const drift = 1 + s.driftAmount * (1 + holdSpeed * 2.5) * (
        Math.sin(t * s.driftSpeed1 * speedMul + s.driftPhase1) * 0.5 +
        Math.sin(t * s.driftSpeed2 * speedMul + s.driftPhase2) * 0.5
      );
      const r = s.radius * drift;
      s.group.position.x = Math.cos(angle) * r;
      s.group.position.z = Math.sin(angle) * r * 0.5;
      s.group.position.y = s.yOff + Math.sin(t * 0.5 * speedMul + s.phase) * 0.3;
      s.group.rotation.x += s.rotSpeed.x * 0.01 * speedMul;
      s.group.rotation.y += s.rotSpeed.y * 0.01 * speedMul;
      s.group.rotation.z += s.rotSpeed.z * 0.01 * speedMul;
      s.group.scale.setScalar(s.baseScale);

      // Connection lines — world positions
      const line = connectionLines[i];
      const lp = line.geometry.attributes.position;
      s.group.getWorldPosition(_satWorldPos);
      monolithSolid.getWorldPosition(_monolithWorldPos);
      lp.setXYZ(0, _monolithWorldPos.x, _monolithWorldPos.y, _monolithWorldPos.z);
      lp.setXYZ(1, _satWorldPos.x, _satWorldPos.y, _satWorldPos.z);
      lp.needsUpdate = true;
      line.material.opacity = 0.25 + holdSpeed * 0.25;
    });

    // Rings
    ring.rotation.z = t * 0.15 * speedMul;
    ring2.rotation.z = -t * 0.25 * speedMul;
    ring.material.opacity = 0.15 + holdSpeed * 0.25;

    // Particles
    particles.rotation.y = t * 0.015 * speedMul;
    particles.rotation.x = t * 0.006 * speedMul;

    // Point light
    pointLight.position.x = mouse.x * 3;
    pointLight.position.y = mouse.y * 3;
    pointLight.intensity = 8 + holdSpeed * 20;

    // Grid
    gridHelper.material.opacity = 0.35;

    renderer.render(scene, camera);
  }

  animate();
}

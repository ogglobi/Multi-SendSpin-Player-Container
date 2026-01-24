const jscad = require('@jscad/modeling')
const { cuboid, cylinder } = jscad.primitives
const { subtract, union } = jscad.booleans
const { translate, rotate, center } = jscad.transforms

const getParameterDefinitions = () => {
  return [
    { name: 'renderPart', type: 'choice', values: ['Base', 'Lid', 'Clamp_Relay', 'Clamp_DC', 'All_Preview'], initial: 'Base', caption: 'Part to Export:' },
    { name: 'pcbL', type: 'float', initial: 106, caption: 'PCB Length (mm):' },
    { name: 'pcbW', type: 'float', initial: 78, caption: 'PCB Width (mm):' },
    { name: 'wall', type: 'float', initial: 2.5, caption: 'Wall Thickness:' },
    { name: 'relayWireDia', type: 'float', initial: 2.8, caption: 'Relay Grip Diameter (mm):' },
    { name: 'dcWireDia', type: 'float', initial: 4.0, caption: 'DC Grip Diameter (mm):' },
    { name: 'wireClearance', type: 'float', initial: 0.8, caption: 'Wall Hole Clearance (mm):' }
  ]
}

const main = (params) => {
  const { renderPart, pcbL, pcbW, wall, relayWireDia, dcWireDia, wireClearance } = params
  const height = 30
  const clearance = 1.5
  const standoffH = 6

  const innerL = pcbL + (clearance * 2)
  const innerW = pcbW + (clearance * 2)
  const outerL = innerL + (wall * 2)
  const outerW = innerW + (wall * 2)
  const outerH = height + wall

  const porchW = 12
  const porchH_Relay = wall + (relayWireDia) + 1
  const dcExitZ = wall + standoffH + 1
  const porchH_DC = dcExitZ

  const relayOffsets = [ (pcbW/4 * 0.5), (pcbW/4 * 1.5), (pcbW/4 * 2.5), (pcbW/4 * 3.5) ];

  const makeBox = (w, l, h, x, y, z) => translate([x + w/2, y + l/2, z + h/2], cuboid({ size: [w, l, h] }))

  const smoothCyl = (h, r, axis = 'z') => {
    let cyl = cylinder({height: h, radius: r, segments: 128})
    if (axis === 'x') return rotate([0, Math.PI/2, 0], cyl)
    if (axis === 'y') return rotate([Math.PI/2, 0, 0], cyl)
    return cyl
  }

  const hexCutout = (x, y, z, size, rot) => {
    return translate([x, y, z], rotate(rot, cylinder({height: 35, radius: size, segments: 6})))
  }

  const createBase = () => {
    let shell = makeBox(outerL, outerW, outerH, 0, 0, 0)
    let porchLeft = makeBox(porchW, outerW, porchH_Relay, -porchW, 0, 0)
    let porchRight = makeBox(porchW, outerW, porchH_Relay, outerL, 0, 0)
    let porchDC = makeBox(32, porchW, porchH_DC, outerL/2 - 16, outerW, 0)
    let baseShell = union(shell, porchLeft, porchRight, porchDC)

    let cavity = makeBox(innerL, innerW, height + 20, wall, wall, wall)
    let usb = makeBox(18, wall + 10, 14, outerL/2 - 9, -5, wall + standoffH + 1)
    let dcExit = makeBox(24, wall + 10, 13, outerL/2 - 12, outerW - wall - 5, dcExitZ)

    const createBasePilot = (x, y, h) => translate([x, y, h - 5], cylinder({height: 12, radius: 1.2, segments: 32}))
    let clampPilotHoles = [
        createBasePilot(-porchW/2, 6, porchH_Relay), createBasePilot(-porchW/2, outerW/2, porchH_Relay), createBasePilot(-porchW/2, outerW - 6, porchH_Relay),
        createBasePilot(outerL + porchW/2, 6, porchH_Relay), createBasePilot(outerL + porchW/2, outerW/2, porchH_Relay), createBasePilot(outerL + porchW/2, outerW - 6, porchH_Relay),
        createBasePilot(outerL/2 - 12, outerW + porchW/2, porchH_DC), createBasePilot(outerL/2 + 12, outerW + porchW/2, porchH_DC)
    ]

    let extraCuts = []
    relayOffsets.forEach(offset => {
        const yPos = wall + clearance + offset;
        extraCuts.push(translate([-porchW/2 - 2, yPos, porchH_Relay], smoothCyl(porchW + 10, (relayWireDia + wireClearance)/2, 'x')))
        extraCuts.push(translate([outerL + porchW/2 + 2, yPos, porchH_Relay], smoothCyl(porchW + 10, (relayWireDia + wireClearance)/2, 'x')))
        for(let i=0; i<3; i++) {
            let z = outerH - 6 - (i * 7)
            if (z > porchH_Relay + 6) {
              extraCuts.push(hexCutout(-5, yPos, z, 3.2 - (i * 0.5), [0, Math.PI/2, 0]))
              extraCuts.push(hexCutout(outerL + 5, yPos, z, 3.2 - (i * 0.5), [0, Math.PI/2, 0]))
            }
        }
    })

    extraCuts.push(translate([outerL/2 - 6, outerW + porchW/2 + 2, porchH_DC], smoothCyl(porchW + 10, (dcWireDia + wireClearance)/2, 'y')))
    extraCuts.push(translate([outerL/2 + 6, outerW + porchW/2 + 2, porchH_DC], smoothCyl(porchW + 10, (dcWireDia + wireClearance)/2, 'y')))

    let baseFinal = subtract(baseShell, cavity, usb, dcExit, ...clampPilotHoles, ...extraCuts)

    // FIX: Standoffs now start exactly at 'wall' height so they don't poke through the bottom
    const sPos = [[wall+4, wall+4], [wall+innerL-4, wall+4], [wall+4, wall+innerW-4], [wall+innerL-4, wall+innerW-4]]
    let standoffs = sPos.map(p => {
        let post = translate([p[0], p[1], wall + standoffH/2], cylinder({height: standoffH, radius: 3.5, segments: 40}))
        let pin = translate([p[0], p[1], wall + standoffH + 2], cylinder({height: 4, radius: 1.55, segments: 32}))
        let split = translate([p[0], p[1], wall + standoffH + 3], cuboid({size: [0.8, 4, 4]}))
        return subtract(union(post, pin), split)
    })

    return center({axes: [true, true, false]}, union(baseFinal, ...standoffs))
  }

  const createLid = () => {
    let lidBase = makeBox(outerL, outerW, wall, 0, 0, 0)
    let lip = makeBox(innerL - 0.6, innerW - 0.6, 4.5, wall + 0.3, wall + 0.3, wall)
    let pryNotch = translate([outerL - 5, outerW - 5, wall/2], rotate([0, 0, Math.PI/4], cuboid({size: [10, 2, 5]})))
    const makeRib = (x, y, w, l) => makeBox(w, l, 4.5, x, y, wall)
    let ribs = [
      makeRib(wall + 0.1, outerW/3, 0.4, 4), makeRib(wall + 0.1, 2*outerW/3, 0.4, 4),
      makeRib(outerL - wall - 0.5, outerW/3, 0.4, 4), makeRib(outerL - wall - 0.5, 2*outerW/3, 0.4, 4),
      makeRib(outerL/3, wall + 0.1, 4, 0.4), makeRib(2*outerL/3, wall + 0.1, 4, 0.4),
      makeRib(outerL/3, outerW - wall - 0.5, 4, 0.4), makeRib(2*outerL/3, outerW - wall - 0.5, 4, 0.4)
    ]
    let hexVents = []
    for(let x = 15; x < outerL - 15; x += 7.5) {
      for(let y = 12; y < outerW - 12; y += 6.5) {
        let xOff = (Math.round(y/6.5) % 2 === 0) ? 0 : 3.75
        let dist = Math.sqrt(Math.pow(outerL/2 - (x + xOff), 2) + Math.pow(outerW/2 - y, 2))
        hexVents.push(hexCutout(x + xOff, y, wall/2, Math.max(1.2, 3.5 * (1 - (dist / (outerL/1.8)))), [0, 0, 0]))
      }
    }
    return center({axes: [true, true, false]}, subtract(union(lidBase, lip, ...ribs), pryNotch, ...hexVents))
  }

  const createClampRelay = () => {
    const barL = outerW, barW = 10, barH = 8
    let bar = makeBox(barW, barL, barH, 0, 0, 0)
    let hHoles = [
        translate([barW/2, 6, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 })),
        translate([barW/2, barL/2, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 })),
        translate([barW/2, barL - 6, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 }))
    ]
    let grooves = relayOffsets.map(offset => translate([barW/2, wall + clearance + offset, 0], smoothCyl(barW + 10, relayWireDia/2, 'x')))
    return center({axes: [true, true, false]}, subtract(bar, ...hHoles, ...grooves))
  }

  const createClampDC = () => {
    const barL = 32, barW = 10, barH = 8
    let bar = makeBox(barL, barW, barH, 0, 0, 0)
    let hHoles = [
        translate([4, barW/2, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 })),
        translate([barL - 4, barW/2, barH/2], cylinder({ height: 20, radius: 1.65, segments: 32 }))
    ]
    let grooves = [
        translate([barL/2 - 6, barW/2, 0], smoothCyl(barW+10, dcWireDia/2, 'y')),
        translate([barL/2 + 6, barW/2, 0], smoothCyl(barW+10, dcWireDia/2, 'y'))
    ]
    return center({axes: [true, true, false]}, subtract(bar, ...hHoles, ...grooves))
  }

  if (renderPart === 'Base') return createBase()
  if (renderPart === 'Lid') return createLid()
  if (renderPart === 'Clamp_Relay') return createClampRelay()
  if (renderPart === 'Clamp_DC') return createClampDC()

  return [
    createBase(),
    translate([0, outerW + 30, 0], createLid()),
    translate([-outerL/2 - 25, 0, 0], createClampRelay()),
    translate([outerL/2 + 25, 0, 0], createClampRelay()),
    translate([0, -outerW/2 - 30, 0], createClampDC())
  ]
}

module.exports = { main, getParameterDefinitions }

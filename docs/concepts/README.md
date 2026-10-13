# Bomb Me Baby — visual direction

These images are concept references generated with the built-in imagegen tool on 2026-10-13. They are not screenshots or finished Unity assets.

- `bomb-me-baby-visual-direction.png`: toy-like isometric arena, bomb characters, map readability, combat effect colors.
- `bomb-me-baby-hud-concept.png`: proposed in-match HUD layout.

## Direction for the prototype

- Keep the player silhouettes and floor paths clear at actual game camera distance. Do not copy the concept's dense decorative detail if it hides combat.
- Use orange for charge/explosion, cyan for shield, blue for boost, green for Energy, and magenta for Core.
- HUD: HP and size at top left; remaining players at top center; charge at bottom center; shield/boost with cooldown indicators at bottom right.
- Gameplay feedback: pickup pop, charge buildup, pre-explosion warning, hit flash, brief camera impulse, and distinct sounds for pickup, charge, explosion, shield, boost, damage, and victory/defeat.
- Build one polished arena first. Test line of sight, safe spawn points, routes around rocks, bushes for ambushes, and enough encounters despite the large map.
- Generate bitmap art only where useful (for example menu art and UI portraits). Build the 3D scene, VFX, lighting, and interactive HUD in Unity.

## Generation prompts

### Arena visual direction

> Use case: stylized-concept. Asset type: visual direction concept art for an in-development Unity 2.5D multiplayer arena game called Bomb Me Baby. Show a single polished gameplay scene from a 35-degree isometric camera: 4 expressive round bomb characters made of simple 3D shapes in a spacious arena, one small and nimble, one large and charging with a bright orange pulse, one protected by a translucent cyan spherical shield, one leaving a short blue boost trail. Scattered lime-green Energy Orbs and a magenta Core, chunky stone obstacles, soft bushes that can hide characters. The arena has readable traversable lanes and open combat spaces. Art direction: playful competitive party game, clean low-poly toy diorama, rounded geometry, bold silhouettes, clear effects that never obscure positions. Rich but restrained palette of teal grass, violet shadow, coral-orange explosions, cyan shield, magenta core; soft warm key light plus subtle cool rim light, gentle ambient occlusion. Composition should demonstrate visual hierarchy, readability, lighting, map and characters; 16:9 landscape image. No text, no labels, no logos, no watermark. This is concept art for reference, not a screenshot claim.

### HUD concept

> Use case: ui-mockup. Asset type: high-fidelity 16:9 PC gameplay HUD visual direction for a Unity 2.5D multiplayer arena game, matching a playful stylized low-poly bomb character game. Main view: legible 35-degree isometric arena with rounded black bomb characters, simple chunky rocks, bushes, glowing green energy orbs and magenta core; visible orange charge pulse, cyan shield, blue boost trail. Overlay UI designed for fast combat readability: top left compact HP bar and growth/size ring for local player; bottom center large orange charge meter that grows toward maximum; bottom right two large ability icons with clearly visible cooldown arcs for shield and boost; top center unobtrusive remaining players counter and round phase; subtle offscreen threat indicator. Friendly polished game UI with rounded dark translucent panels, warm white typography, orange for attack, cyan for defense, blue for movement, green for healing. The arena must stay largely unobstructed. Use only simple English labels if needed: HP, CHARGE, SHIELD, BOOST. Avoid dense text, menus, watermark, mobile controls, logos. Show it as a concept mockup, not a claim of implemented screenshot.

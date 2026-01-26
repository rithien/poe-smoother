# Quality of Life Features Analysis

> Analysis of gameplay quality improvements through game file modifications.

## Currently Implemented

| Feature | Description | Status |
|---------|-------------|--------|
| Map Reveal | Shows unexplored areas on minimap | Done |
| Camera Zoom | Extended zoom distance | Done |

---

## 1. Minimap Enhancements

### 1.1 Minimap Size/Scale

**Status:** Not Implemented

**Analysis:**
Minimap rendering parameters may be configurable in:
- `shaders/minimap*.hlsl`
- `metadata/ui/minimap*.ot`
- UI configuration files

**Potential Modifications:**
- Increase minimap render resolution
- Adjust minimap zoom level
- Change minimap position/size

### 1.2 Custom Minimap Colors

**Status:** Not Implemented

**Analysis (from map reveal patcher):**

Current minimap shaders in:
- `shaders/minimap_blending_pixel.hlsl`
- `shaders/minimap_visibility_pixel.hlsl`

**Customization Options:**

| Element | Current | Customizable |
|---------|---------|-------------|
| Walkable area | Light gray | User color |
| Explored area | White/visible | User opacity |
| Unexplored area | Black | User color/opacity |
| Edge detection | Blue tint | User color |

**Implementation:**

```hlsl
// In minimap_blending_pixel.hlsl
// Replace hardcoded colors with configurable values:
float4 walkable_color = float4(R, G, B, A); // User configurable
float4 explored_color = float4(R, G, B, A); // User configurable
```

---

## 2. Sound Volume Overrides

### Status: Not Implemented / Research Needed

**Analysis:**
Sound settings may be stored in:
- `metadata/audio/*.ot`
- `metadata/sounds/*.json`
- Audio bank configuration files

**Potential Features:**

| Feature | Description |
|---------|-------------|
| Skill volume override | Reduce loud skill sounds |
| Ambient volume cap | Limit environmental sounds |
| Loot filter sound boost | Louder item drop sounds |
| Monster sound reduction | Quieter enemy effects |

**Complexity:** High (requires audio system analysis)

---

## 3. Camera Shake Reduction/Removal

### Status: Not Implemented

**Analysis:**
Camera shake during impacts/skills affects gameplay comfort.

**Potential Files:**
- `metadata/effects/camera*.ot`
- `shaders/postprocess*.hlsl` (screen shake effects)
- Character/skill effect files

**Implementation Options:**

| Option | Effect |
|--------|--------|
| Disable completely | No camera shake |
| Reduce intensity | 50% shake reduction |
| Disable specific triggers | Only disable explosion shake |

---

## 4. Loading Screen Improvements

### Status: Not Implemented / Research Needed

**Analysis:**
Loading screens may have skippable animations or optimizable assets.

**Potential Files:**
- `metadata/ui/loading*.ot`
- Loading screen texture files
- Animation sequences

**Note:** Limited impact on actual loading time (mostly I/O bound)

---

## 5. Death Effect Reduction

### Status: Not Implemented

**Analysis:**
Death effects (screen darkening, slow-motion, camera effects) can be distracting.

**Potential Modifications:**
- Reduce death screen overlay opacity
- Disable death slow-motion effect
- Faster respawn transition

**Potential Files:**
- `shaders/postprocessuber.hlsl` (death overlay)
- `metadata/ui/death*.ot`
- Character death animation files

---

## 6. Skill Effect Simplification

### Status: Not Implemented

**Analysis:**
Many skills have excessive visual effects that obscure gameplay.

**Approach:**
Modify skill-specific .aoc files to reduce particle counts and effect intensity.

**Target Categories:**

| Category | Examples | Impact |
|----------|----------|--------|
| Auras | Herald effects, aura visuals | High (always active) |
| Channeling | Divine Ire, Cyclone | Medium |
| Projectiles | Arrows, projectile trails | Medium |
| AoE | Ground effects, explosions | High |

**Implementation:**
Per-skill .aoc file modification to reduce:
- Particle count
- Effect duration
- Alpha/opacity
- Effect scale

---

## 7. NPC Dialogue Skip

### Status: Not Implemented / Research Needed

**Analysis:**
Dialogue sequences may have skip parameters in:
- `metadata/npcs/*.ot`
- Dialogue configuration files
- Quest state files

**Potential Features:**
- Auto-skip tutorial dialogues
- Faster NPC text display
- Skip cutscene markers

**Complexity:** Medium-High

---

## 8. Inventory/Stash UI Improvements

### Status: Not Implemented / Research Needed

**Analysis:**
UI layout files may allow:
- Larger inventory grid
- Modified stash tab appearance
- Item tooltip customization

**Potential Files:**
- `metadata/ui/inventory*.ot`
- `metadata/ui/stash*.ot`
- UI shader files

**Complexity:** High (UI system modifications)

---

## 9. Buff/Debuff Display Enhancement

### Status: Not Implemented

**Analysis:**
Buff bar can be cluttered and hard to read.

**Potential Modifications:**

| Modification | Effect |
|--------------|--------|
| Increase buff icon size | Larger, easier to see |
| Add buff duration text | Show remaining time |
| Filter minor buffs | Hide flask effects, etc. |
| Custom buff positioning | Move buff bar location |

**Potential Files:**
- `metadata/ui/buffs*.ot`
- `shaders/ui/buff*.hlsl`

---

## 10. Monster Life Bar Enhancements

### Status: Not Implemented

**Analysis:**
Monster health bars can be enhanced for better visibility.

**Potential Modifications:**

| Modification | Effect |
|--------------|--------|
| Larger health bars | Easier to read |
| Numeric health display | Show actual HP values |
| Enhanced rare/unique bars | More visible boss health |
| Custom colors | Distinguish enemy types |

**Potential Files:**
- `metadata/ui/monster_health*.ot`
- `shaders/ui/healthbar*.hlsl`

---

## 11. Clock/Timer Display

### Status: Not Implemented / Research Needed

**Analysis:**
Add in-game time display for mapping, league mechanics timing.

**Potential Implementation:**
- UI overlay modification
- Shader-based timer display

**Complexity:** High (requires UI injection or overlay)

---

## 12. Skill Cooldown Visualization

### Status: Not Implemented

**Analysis:**
Enhanced cooldown indicators for skills.

**Potential Modifications:**
- Numeric cooldown display
- Larger cooldown overlay
- Audio cue when ready

**Potential Files:**
- `metadata/ui/skills*.ot`
- Skill bar shader files

---

## Priority Ranking

| Priority | Feature | User Impact | Complexity |
|----------|---------|-------------|------------|
| 1 | Camera Shake Reduction | High | Low-Medium |
| 2 | Skill Effect Simplification | Very High | Medium |
| 3 | Custom Minimap Colors | Medium | Low |
| 4 | Death Effect Reduction | Medium | Low |
| 5 | Buff/Debuff Enhancement | High | Medium |
| 6 | Monster Life Bar Enhancement | High | Medium |
| 7 | Sound Volume Overrides | High | High |
| 8 | NPC Dialogue Skip | Medium | Medium |
| 9 | Skill Cooldown Visualization | Medium | Medium |
| 10 | Inventory/Stash UI | Medium | High |

---

## Implementation Recommendations

### Phase 1: Quick Wins

1. **Camera Shake Reduction** - Likely a simple parameter modification
2. **Death Effect Reduction** - Post-process shader modification
3. **Custom Minimap Colors** - Build on existing map reveal patcher

### Phase 2: Medium Effort

1. **Skill Effect Simplification** - Per-skill .aoc modifications (batch processing)
2. **Buff/Debuff Enhancement** - UI file modifications
3. **Monster Life Bar Enhancement** - UI file modifications

### Phase 3: Complex Features

1. **Sound Volume Overrides** - Requires audio system understanding
2. **Inventory/Stash UI** - Deep UI system modifications
3. **NPC Dialogue Skip** - Quest/dialogue system analysis

---

## Technical Notes

### File Format Considerations

| Extension | Format | Encoding |
|-----------|--------|----------|
| .ot | Object Template | UTF-16 LE |
| .otc | Object Template Container | UTF-16 LE |
| .aoc | Animation Object Container | Binary/Text mixed |
| .mat | Material | JSON-like |
| .hlsl | Shader | UTF-8 |

### Modification Safety

| Risk Level | Description | Examples |
|------------|-------------|----------|
| Low | Visual-only changes | Colors, opacity, effects |
| Medium | Gameplay-adjacent | Minimap, UI size |
| High | Core mechanics | Hitboxes, timings |

**Recommendation:** Focus on Low and Medium risk modifications that enhance visual clarity without affecting core gameplay mechanics.

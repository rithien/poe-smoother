# POE Editor Patcher - Proposed Features Roadmap

> Master document summarizing all analyzed features and recommended implementation order.

## Current State (v0.1.2)

### Implemented Patchers

| Category | Patcher | Target | Status |
|----------|---------|--------|--------|
| **Performance** | Disable Bloom | postprocessuber.hlsl | Done |
| **Performance** | Disable DoF | postprocessuber.hlsl | Done |
| **Performance** | Simplify GI | globalillumination.hlsl | Done |
| **Performance** | Fog Removal | fog.ffx | Done |
| **Performance** | Disable SSAO | screenspaceambientocclusion.hlsl | Done |
| **Performance** | SS Shadows | screenspaceshadows.hlsl | Done |
| **Visual** | Camera Zoom | character.ot, *.otc | Done |
| **Visual** | Brightness Boost | postprocessuber.hlsl | Done |
| **Visual** | SDR Scale | oetf.hlsl | Done |
| **Visual** | Gamma Adjustment | oetf.hlsl | Done |
| **QoL** | Map Reveal | minimap_*.hlsl | Done |

---

## Recommended Implementation Phases

### Phase 1: Low-Hanging Fruit (Easy Implementation)

Estimated patchers: 6 | Complexity: Low

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Volumetric FX | Performance | volumetricfx.hlsl | High |
| 2 | SS Rays | Performance | screenspacerays.hlsl | High |
| 3 | Motion Blur | Performance | postprocessuber.hlsl | Medium |
| 4 | Chromatic Aberration | Visual | postprocessuber.hlsl | Low |
| 5 | Film Grain | Visual | postprocessuber.hlsl | Low |
| 6 | Vignette | Visual | postprocessuber.hlsl | Low |

**Rationale:** All target `postprocessuber.hlsl` or single shader files. Similar pattern to existing patchers (comment out function call).

---

### Phase 2: Visual Customization (Medium Complexity)

Estimated patchers: 4 | Complexity: Medium

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Saturation Control | Visual | postprocessuber.hlsl | Medium |
| 2 | Contrast Adjustment | Visual | postprocessuber.hlsl | Medium |
| 3 | Tonemapping Selection | Visual | tonemapping.hlsl | High |
| 4 | Custom Minimap Colors | QoL | minimap_*.hlsl | Medium |

**Rationale:** Builds on existing shader modification patterns. Adds user-configurable parameters.

---

### Phase 3: Weather & Effects (Medium Complexity)

Estimated patchers: 3 | Complexity: Medium

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Weather Effects | Performance | weather*.hlsl, *.aoc | Medium |
| 2 | Camera Shake | QoL | camera*.ot, effects | Medium |
| 3 | Death Effects | QoL | postprocessuber.hlsl | Low |

**Rationale:** Requires identifying specific effect files. May need .ot file parsing.

---

### Phase 4: Particle System (High Complexity)

Estimated patchers: 1-3 | Complexity: High

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Particle Removal (Selective) | Performance | *.aoc (59,541 files) | Very High |
| 2 | Skill Effect Simplification | QoL | skill/*.aoc | High |
| 3 | Ground Effect Clarity | QoL | ground/*.aoc | High |

**Rationale:** Highest performance impact. Requires:
- Categorization of particle types (safe vs essential)
- Batch processing capability
- Careful testing per category

---

### Phase 5: Material System (High Complexity)

Estimated patchers: 1-2 | Complexity: High

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Material Simplification | Performance | *.mat (136,006 files) | Medium |
| 2 | Texture LOD Forcing | Performance | texture sampling shaders | High |

**Rationale:** Requires understanding of material file format. Batch processing of many files.

---

### Phase 6: UI Enhancements (High Complexity)

Estimated patchers: 3-5 | Complexity: Very High

| # | Feature | Type | File(s) | Impact |
|---|---------|------|---------|--------|
| 1 | Buff/Debuff Display | QoL | ui/buffs*.ot | High |
| 2 | Monster Health Bars | QoL | ui/healthbar*.ot | High |
| 3 | Loot Filter Enhancement | QoL | ui/item*.ot | High |
| 4 | Enemy Highlighting | Visual | character shaders | High |

**Rationale:** Requires deep UI system analysis. May need custom shader injection.

---

## Feature Priority Matrix

### Performance Features

| Feature | FPS Impact | Complexity | Priority |
|---------|------------|------------|----------|
| Particle Removal | +20-40% | High | P1 |
| Volumetric FX | +5-15% | Low | P1 |
| SS Rays | +10-20% | Low | P1 |
| Material Simplification | +5-15% | High | P2 |
| Motion Blur | +2-5% | Low | P2 |
| Weather Effects | +5-10% | Medium | P2 |
| Texture LOD | +5-15% | Medium | P3 |
| Chromatic Aberration | +1-2% | Low | P3 |
| Film Grain | +1% | Low | P3 |
| Vignette | ~0% | Low | P4 |

### Visual Features

| Feature | User Value | Complexity | Priority |
|---------|------------|------------|----------|
| Tonemapping Selection | High | Medium | P1 |
| Saturation Control | Medium | Low | P1 |
| Contrast Adjustment | Medium | Low | P1 |
| Custom Minimap Colors | Medium | Low | P2 |
| Enemy Highlighting | Very High | High | P3 |
| Ground Effect Clarity | High | Medium | P2 |

### QoL Features

| Feature | User Value | Complexity | Priority |
|---------|------------|------------|----------|
| Camera Shake Reduction | High | Low-Medium | P1 |
| Skill Effect Simplification | Very High | Medium | P1 |
| Death Effect Reduction | Medium | Low | P2 |
| Buff/Debuff Enhancement | High | Medium | P2 |
| Monster Health Enhancement | High | Medium | P2 |
| Sound Volume Override | High | High | P3 |
| Loot Filter Enhancement | Very High | High | P3 |

---

## Quick Reference: File Targets

### Single File Modifications (Easy)

| File | Potential Patchers |
|------|-------------------|
| postprocessuber.hlsl | Motion Blur, Chromatic, Film Grain, Vignette, Saturation, Contrast, Death Effects |
| volumetricfx.hlsl | Volumetric FX |
| screenspacerays.hlsl | SS Rays |
| tonemapping.hlsl | Tonemapping Selection |
| minimap_*.hlsl | Custom Minimap Colors |

### Multi-File Modifications (Medium)

| Category | Files | Patchers |
|----------|-------|----------|
| Weather | weather*.hlsl, *.aoc | Weather Effects |
| Camera | camera*.ot | Camera Shake |
| UI | ui/*.ot | Buff Display, Health Bars |

### Batch Processing (High)

| Category | File Count | Patchers |
|----------|------------|----------|
| Particles | ~60,000 .aoc | Particle Removal, Skill Effects |
| Materials | ~136,000 .mat | Material Simplification |

---

## Suggested Next Steps

### Immediate (Phase 1)

1. **Implement Volumetric FX patcher** - High impact, low effort
2. **Implement SS Rays patcher** - High impact, low effort
3. **Add Motion Blur toggle** - Existing postprocessuber.hlsl pattern

### Short-term (Phase 2)

1. **Add Saturation/Contrast controls** - Simple post-process additions
2. **Tonemapping selection** - Medium effort, high user value
3. **Extend minimap customization** - Build on Map Reveal

### Medium-term (Phase 3-4)

1. **Particle categorization research** - Essential for particle removal
2. **Weather effect identification** - Find shader files
3. **Camera shake analysis** - Identify effect triggers

### Long-term (Phase 5-6)

1. **Material format analysis** - For batch material simplification
2. **UI system research** - For UI enhancement features
3. **Audio system analysis** - For sound volume overrides

---

## Technical Debt & Improvements

### Code Improvements

| Area | Suggestion |
|------|------------|
| Batch Processing | Add progress bar for multi-file operations |
| Backup System | Implement differential backups for large batches |
| Config System | Add config versioning for patch compatibility |
| Testing | Add unit tests for regex patterns |

### UI Improvements

| Area | Suggestion |
|------|------------|
| Patcher Organization | Sub-categories (Lighting, Effects, Post-Process) |
| Parameter UI | Slider controls for numeric values |
| Preview | Before/after screenshot comparison |
| Profiles | Save/load patch configurations |

---

## Related Analysis Documents

- [performance-optimizations.md](./performance-optimizations.md) - Detailed performance feature analysis
- [visual-enhancements.md](./visual-enhancements.md) - Visual quality feature analysis
- [qol-features.md](./qol-features.md) - Quality of life feature analysis
- [brightness.md](./brightness.md) - Original brightness shader analysis

---

*Last updated: 2026-01-22*

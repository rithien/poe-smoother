# Performance Optimizations Analysis

> Analysis of potential performance improvements for Path of Exile based on shader and configuration file patterns.

## Currently Implemented (Reference)

| Patcher | Target File | Technique | Impact |
|---------|-------------|-----------|--------|
| Disable Bloom | postprocessuber.hlsl | Comment out ApplyBloom() | High |
| Disable DoF | postprocessuber.hlsl | Comment out ApplyDoF() | Medium |
| Simplify GI | globalillumination.hlsl | Early return with static values | Very High |
| Fog Removal | fog.ffx | Early return in ApplyGlobalFog() | Medium |
| Disable SSAO | screenspaceambientocclusion.hlsl | Return 1.0f (no occlusion) | High |
| SS Shadows | screenspaceshadows.hlsl | Return white (no shadows) | High |

---

## 1. Volumetric Effects (volumetricfx.hlsl)

### Status: Planned

### Analysis

Volumetric effects (god rays, light shafts, fog volumes) are computationally expensive as they require ray marching through 3D volumes.

### Implementation Strategy

```hlsl
// Target file: shaders/volumetricfx.hlsl
// Look for main compute/pixel function

// Pattern to find:
float4 ComputeVolumetricFX(...) {
    // Expensive ray marching loop
}

// Replacement:
float4 ComputeVolumetricFX(...) {
    return float4(0.0f, 0.0f, 0.0f, 0.0f); //{{RITHIEN_volumetricfx}}
}
```

### Configuration Template

```json
{
  "enabled": true,
  "name": "Disable Volumetric FX",
  "description": "Disables volumetric lighting effects (god rays, light shafts) for improved performance",
  "category": "Lighting",
  "impactLevel": 6,
  "markerFile": "shaders/volumetricfx.hlsl",
  "marker": "{{RITHIEN_volumetricfx}}",
  "targets": {
    "files": ["shaders/volumetricfx.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

### Expected Impact: Medium-High (5-15% FPS gain in areas with heavy volumetrics)

---

## 2. Screen Space Rays (screenspacerays.hlsl)

### Status: Planned

### Analysis

Screen space reflections/rays are expensive post-processing effects that trace rays in screen space to calculate reflections and indirect lighting.

### Implementation Strategy

```hlsl
// Target file: shaders/screenspacerays.hlsl

// Pattern to find:
float4 ComputeScreenSpaceRays(...) {
    // Ray tracing loop in screen space
}

// Replacement:
float4 ComputeScreenSpaceRays(...) {
    return float4(0.0f, 0.0f, 0.0f, 0.0f); //{{RITHIEN_ssrays}}
}
```

### Configuration Template

```json
{
  "enabled": true,
  "name": "Disable SS Rays",
  "description": "Disables screen space ray tracing for reflections, significant performance gain",
  "category": "Lighting",
  "impactLevel": 7,
  "markerFile": "shaders/screenspacerays.hlsl",
  "marker": "{{RITHIEN_ssrays}}",
  "targets": {
    "files": ["shaders/screenspacerays.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

### Expected Impact: High (10-20% FPS gain)

---

## 3. Particle System Optimization (.aoc files)

### Status: Planned

### Analysis

Path of Exile has ~59,541 `.aoc` (Animation Object Container) files. Many contain particle effects that cause visual clutter and performance drops, especially in dense encounters.

### Approaches

#### 3.1 Complete Particle Removal (Aggressive)

Replace particle template files with empty/minimal versions.

```
Target: metadata/effects/**/*.aoc
Target: metadata/particles/**/*.aoc
```

#### 3.2 Selective Particle Removal (Recommended)

Categories to target:
- **Ground effects** (degen, burning ground, shocked ground)
- **Projectile trails** (arrow trails, spell projectiles)
- **Environmental particles** (dust, debris, rain)
- **Skill effect particles** (excess particle counts)

#### 3.3 Implementation Complexity

- Need to identify safe vs essential particles
- Some particles are visual-only, some indicate game mechanics
- Requires careful categorization

### Configuration Template

```json
{
  "enabled": true,
  "name": "Remove Particles",
  "description": "Removes visual particle effects for cleaner gameplay and better FPS",
  "category": "Effects",
  "impactLevel": 8,
  "markerFile": "metadata/effects/marker.aoc",
  "marker": "{{RITHIEN_particles}}",
  "targets": {
    "files": [],
    "extensions": [".aoc"],
    "basePaths": ["metadata/effects/", "metadata/particles/"]
  }
}
```

### Expected Impact: Very High (20-40% FPS gain in dense encounters)

---

## 4. Material Simplification (.mat files)

### Status: Planned

### Analysis

136,006 `.mat` material files define surface properties, texture maps, and shader parameters.

### Simplification Targets

| Property | Simplification |
|----------|---------------|
| Normal maps | Replace with flat normal (0.5, 0.5, 1.0) |
| Specular maps | Reduce intensity or remove |
| Detail textures | Remove secondary detail layers |
| Tessellation | Disable or reduce factor |
| Parallax mapping | Disable |

### Implementation Approach

```
// Material file pattern (JSON-like structure)
{
    "normalMap": "textures/surface_normal.dds",
    "specularIntensity": 0.8,
    "tessellationFactor": 4
}

// Simplified:
{
    "normalMap": "textures/flat_normal.dds",
    "specularIntensity": 0.0,
    "tessellationFactor": 0
}
```

### Expected Impact: Medium (5-15% FPS gain, mostly GPU-bound improvement)

---

## 5. Motion Blur Removal

### Status: Not Implemented

### Analysis

Motion blur adds perceived smoothness but reduces visual clarity and has performance cost.

### Target File: `shaders/motionblur.hlsl` or `postprocessuber.hlsl`

### Implementation

```hlsl
// Pattern to find:
colour = ApplyMotionBlur(colour, ...);

// Replacement:
//{{RITHIEN_motionblur}} colour = ApplyMotionBlur(colour, ...);
```

### Configuration Template

```json
{
  "enabled": true,
  "name": "Disable Motion Blur",
  "description": "Removes motion blur for clearer visuals during fast movement",
  "category": "Post-Processing",
  "impactLevel": 4,
  "markerFile": "shaders/postprocessuber.hlsl",
  "marker": "{{RITHIEN_motionblur}}",
  "targets": {
    "files": ["shaders/postprocessuber.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

### Expected Impact: Low-Medium (2-5% FPS, significant clarity improvement)

---

## 6. Chromatic Aberration Removal

### Status: Not Implemented

### Analysis

Chromatic aberration is a purely visual effect that separates color channels at screen edges.

### Target File: `shaders/postprocessuber.hlsl` or `shaders/chromaticaberration.hlsl`

### Implementation

```hlsl
// Pattern to find:
colour = ApplyChromaticAberration(colour, ...);

// Replacement:
//{{RITHIEN_chromatic}} colour = ApplyChromaticAberration(colour, ...);
```

### Expected Impact: Very Low (1-2% FPS, visual clarity improvement)

---

## 7. Film Grain Removal

### Status: Not Implemented

### Analysis

Film grain adds noise to the image for cinematic effect but reduces visual clarity.

### Target File: `shaders/postprocessuber.hlsl` or `shaders/filmgrain.hlsl`

### Implementation

```hlsl
// Pattern to find:
colour = ApplyFilmGrain(colour, ...);

// Replacement:
//{{RITHIEN_filmgrain}} colour = ApplyFilmGrain(colour, ...);
```

### Expected Impact: Very Low (1% FPS, visual clarity improvement)

---

## 8. Vignette Removal

### Status: Not Implemented

### Analysis

Vignette darkens screen corners for cinematic effect.

### Target File: `shaders/postprocessuber.hlsl`

### Implementation

```hlsl
// Pattern to find:
colour = ApplyVignette(colour, ...);

// Replacement:
//{{RITHIEN_vignette}} colour = ApplyVignette(colour, ...);
```

### Expected Impact: Negligible (visual preference)

---

## Priority Ranking for Implementation

| Priority | Feature | Impact | Complexity |
|----------|---------|--------|------------|
| 1 | Volumetric FX | High | Low |
| 2 | SS Rays | High | Low |
| 3 | Particle Removal | Very High | High |
| 4 | Motion Blur | Medium | Low |
| 5 | Material Simplification | Medium | High |
| 6 | Chromatic Aberration | Low | Low |
| 7 | Film Grain | Low | Low |
| 8 | Vignette | Negligible | Low |

---

## Combined Performance Estimate

Applying all optimizations simultaneously:

| Scenario | Expected FPS Gain |
|----------|-------------------|
| Low-end PC (GTX 1060) | 30-50% |
| Mid-range PC (RTX 3060) | 20-35% |
| High-end PC (RTX 4080) | 10-20% |

**Note:** PoE is often CPU-bound in dense encounters. Shader optimizations primarily help GPU-bound scenarios. Particle removal has the most significant impact on overall performance.

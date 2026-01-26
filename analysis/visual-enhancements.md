# Visual Enhancements Analysis

> Analysis of visual quality improvements and customization options for Path of Exile.

## Currently Implemented (Reference)

| Patcher | Effect | Category |
|---------|--------|----------|
| Camera Zoom | Extended zoom range | Visual |
| Brightness Boost | Increased scene brightness | Visual |
| SDR Scale | SDR output multiplier | Visual |
| Gamma Adjustment | Gamma curve modification | Visual |
| Map Reveal | Minimap exploration visibility | QoL |

---

## 1. Tonemapping Customization

### Status: Not Implemented

### Analysis (from brightness.md)

PoE implements 11 tonemapping algorithms in `shaders/include/tonemapping.hlsl`:

| ID | Algorithm | Characteristics |
|----|-----------|-----------------|
| 0 | None | No compression - brightest output |
| 1 | Reinhard | Soft, gentle compression |
| 2 | ACES Simple | Simplified cinematic curve |
| 3 | ACES | Full Academy Color Encoding System |
| 4 | Uncharted | Uncharted 2 filmic curve |
| 5 | AgX | Modern, color-preserving |
| 6 | Lottes | Controlled contrast |
| 7 | Uchimura | GT Tonemap (Gran Turismo style) |
| 8 | Drago | Logarithmic mapping |
| 9 | RomBinDaHouse | Exponential mapping |
| 10 | Ottosson | Perceptual (LMS color space) |

### Implementation Options

#### Option A: Force Specific Tonemapper

```hlsl
// In tonemapping.hlsl or postprocessuber.hlsl
// Force tonemap ID to user preference

#define FORCED_TONEMAP_ID 0  // None - no compression
```

#### Option B: Tonemap Bypass (Maximum Brightness)

```hlsl
// Pattern to find:
colour = ApplyToneMappingTransform(colour, ...);

// Replacement:
//{{RITHIEN_tonemap}} colour = ApplyToneMappingTransform(colour, ...);
```

### User-Configurable Parameters

| Parameter | Effect |
|-----------|--------|
| Algorithm ID | Select tonemapping curve |
| minLum | Minimum luminance threshold |
| maxLum | Maximum luminance threshold |

### Expected Result: Customizable HDR to SDR conversion, potential brightness boost

---

## 2. Color Saturation/Vibrance Control

### Status: Not Implemented

### Analysis (from brightness.md)

Functions in `shaders/include/util.hlsl`:

```hlsl
float3 Vibrance(float val, float3 color);
float4 Desaturate(float4 base_colour, float saturation);
```

### Implementation

Add saturation multiplier as post-process:

```hlsl
// In postprocessuber.hlsl, after final color calculation:
float saturation = 1.2f; // Configurable: 1.0 = normal, >1.0 = more saturated
colour.rgb = lerp(dot(colour.rgb, float3(0.30, 0.59, 0.11)), colour.rgb, saturation); //{{RITHIEN_saturation}}
```

### Configuration Template

```json
{
  "enabled": true,
  "name": "Saturation x{{MULTIPLIER}}",
  "description": "Adjusts color saturation by {{PERCENTAGE}}%",
  "category": "Visual",
  "impactLevel": 2,
  "markerFile": "shaders/postprocessuber.hlsl",
  "marker": "{{RITHIEN_saturation}}",
  "targets": {
    "files": ["shaders/postprocessuber.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

### Expected Result: More vivid/washed colors based on preference

---

## 3. Contrast Adjustment

### Status: Not Implemented

### Analysis

Contrast can be adjusted by remapping the luminance range.

### Implementation

```hlsl
// Simple contrast adjustment formula
float contrast = 1.3f; // Configurable: 1.0 = normal
colour.rgb = (colour.rgb - 0.5f) * contrast + 0.5f; //{{RITHIEN_contrast}}
```

### Expected Result: Deeper blacks, brighter whites

---

## 4. Loot Filter Visual Enhancement

### Status: Not Implemented / Research Needed

### Analysis

Loot highlighting might be controllable via:
- Item name plate shaders
- Text rendering shaders
- UI overlay shaders

### Potential Files

- `shaders/ui/*.hlsl`
- `shaders/text*.hlsl`
- `metadata/ui/*.ot`

### Implementation Ideas

| Feature | Description |
|---------|-------------|
| Increased item label size | Scale item name plates |
| Enhanced drop beam | Brighter/thicker loot beams |
| Background opacity | More opaque item backgrounds |

### Complexity: High (requires UI shader analysis)

---

## 5. Enemy Highlighting

### Status: Not Implemented / Research Needed

### Analysis

Potential to add outline/glow to enemies for better visibility in dense encounters.

### Approaches

#### 5.1 Screen-Space Outline

Add edge detection pass that highlights enemy silhouettes.

#### 5.2 Shader-Based Glow

Modify character/monster shaders to add rim lighting.

### Potential Files

- `shaders/character*.hlsl`
- `shaders/outline*.hlsl`
- `shaders/monster*.hlsl`

### Complexity: High

---

## 6. Shadow Quality Reduction (Not Removal)

### Status: Not Implemented

### Analysis

Instead of completely removing shadows (SS Shadows patcher), offer shadow quality reduction.

### Implementation

```hlsl
// In shadowmap or shadow calculation shader
// Reduce shadow resolution/samples

#define SHADOW_SAMPLES 2 // Reduced from default (e.g., 8)
```

### Expected Result: Softer shadows with better performance, maintains visual depth

---

## 7. Texture Detail Reduction

### Status: Not Implemented

### Analysis

Force lower MIP levels for texture sampling to reduce VRAM usage and improve performance.

### Implementation

```hlsl
// In texture sampling calls
// Original:
float4 tex = texture.Sample(sampler, uv);

// Modified (force lower detail):
float4 tex = texture.SampleLevel(sampler, uv, 2.0f); //{{RITHIEN_texlod}} Force MIP level 2
```

### Configuration Options

| MIP Level | Effect |
|-----------|--------|
| 0 | Full detail (default) |
| 1 | Half resolution |
| 2 | Quarter resolution |
| 3 | Eighth resolution |

### Expected Result: Lower VRAM usage, blurrier textures, better performance

---

## 8. UI Opacity Customization

### Status: Not Implemented / Research Needed

### Analysis

Modify UI element transparency for less screen clutter.

### Targets

- Health/Mana globes transparency
- Buff bar opacity
- Minimap opacity

### Potential Files

- `metadata/ui/*.ot`
- `shaders/ui/*.hlsl`

### Complexity: Medium

---

## 9. Weather Effects Control

### Status: Not Implemented

### Analysis

Weather effects (rain, snow, fog) impact visibility and performance.

### Targets

- `shaders/weather*.hlsl`
- `shaders/rain*.hlsl`
- `shaders/snow*.hlsl`
- `metadata/environments/weather/*.aoc`

### Implementation Options

| Option | Effect |
|--------|--------|
| Disable weather | Remove all weather particles |
| Reduce intensity | Fewer particles, less opacity |
| Visibility only | Keep visuals, remove screen effects |

### Expected Result: Cleaner visuals in weather-heavy areas

---

## 10. Ground Effect Clarity

### Status: Not Implemented

### Analysis

Dangerous ground effects (burning, caustic, shocked) are sometimes hard to see. Option to enhance or modify their visibility.

### Approaches

#### 10.1 Enhance Visibility (Safety)

```hlsl
// Increase saturation/contrast of dangerous ground
// Target: ground effect shaders or .aoc files
```

#### 10.2 Reduce Visual Noise

```hlsl
// Simplify ground effect rendering
// Keep indicator, remove excess particles
```

### Complexity: Medium-High

---

## Priority Ranking for Implementation

| Priority | Feature | Impact | Complexity |
|----------|---------|--------|------------|
| 1 | Saturation Control | Medium | Low |
| 2 | Contrast Adjustment | Medium | Low |
| 3 | Tonemapping Customization | High | Medium |
| 4 | Weather Effects Control | Medium | Medium |
| 5 | Shadow Quality Reduction | Medium | Low |
| 6 | Texture Detail Reduction | High | Medium |
| 7 | Ground Effect Clarity | High | Medium |
| 8 | Loot Filter Enhancement | High | High |
| 9 | Enemy Highlighting | High | High |
| 10 | UI Opacity Customization | Low | Medium |

---

## Implementation Recommendations

### Quick Wins (Low Complexity)

1. **Saturation Control** - Simple post-process modification
2. **Contrast Adjustment** - Simple post-process modification
3. **Shadow Quality Reduction** - Reduce sample counts

### Medium Effort

1. **Tonemapping Customization** - Requires understanding of existing tonemapping flow
2. **Weather Effects Control** - Identify and modify weather shaders
3. **Texture LOD Forcing** - Modify texture sampling calls

### High Effort (Future)

1. **Loot Filter Enhancement** - Requires UI system analysis
2. **Enemy Highlighting** - Complex shader modifications
3. **Ground Effect Clarity** - Balance safety and performance

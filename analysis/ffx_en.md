# FFX Shader Analysis

## 1. Base Module and Transformations (common.ffx)

This file forms the foundation of the rendering engine, defining global constants, scene data, and basic mathematical operations.

**Matrix Management**: Functions such as `GetViewProjectionTransform()`, `GetViewTransform()`, or `GetProjectionTransform()` provide necessary matrices to transform coordinates from world space to screen space.

**Terrain Analysis**: The `GetHeightmapSample()` function allows sampling terrain height at a specific world point, which is crucial for visual collision effects and terrain shading.

**Surface Burning System**: `GetBurnSample()` and `BurnTemperature()` calculate the visual progress of fire and charring effects on objects.

**Pipeline Optimization**: `PerformAlphaTestClip()` is responsible for discarding pixels with low opacity, preventing excessive GPU load (overdraw).

**Platform Adaptation**: `GetWavefrontSize()` identifies hardware architecture (PC, PlayStation, Xbox), optimizing code execution for specific GPU compute units.

## 2. Lighting and Surface Physics (lighting.ffx)

This module implements advanced lighting models, including the PBR (Physically Based Rendering) approach.

**GGX Lighting**: The `GGXSpecular()` function implements the industry standard for physically correct glossy reflections, accounting for microfacet structure.

**Anisotropy**: `WardSpecular()` allows rendering of materials with directional structure, such as brushed metal or hair.

**Ambient Occlusion**: `GetBentNormalSpecularOcclusion()` uses the "bent normals" technique to realistically darken reflections in geometry crevices.

**GI Reprojection**: `ReprojectLighting()` is an advanced Screen Space technique that stabilizes Global Illumination between animation frames.

**Point and Directional Lighting**: `ComputePointLightParams()` and `ComputeDirectionalLightParams()` calculate light attenuation and intensity for various sources.

## 3. Terrain Rendering and Blending (ground.ffx)

Focuses on specific ground rendering needs, enabling the blending of multiple textures.

**Material Blending**: `GroundMaterialMulAdd()` is a mathematical function combining properties (albedo, roughness, normals) of several different ground types into one final result.

**Fresnel Coefficient**: `GetReflectionCoefficient3()` calculates how strong the reflection should be depending on the player's viewing angle of the ground.

**UV Generation**: `GenerateBlendUVs()` creates coordinates for texture blending masks based on their world position, ensuring no visible "seams" on large surfaces.

## 4. Material Definitions and Post-processes (texturing.ffx)

Contains data structures and helper texturing functions.

**Material Initialization**: `InitMaterial()` functions prepare default parameters for various lighting models (Phong, GGX, Anisotropic).

**Vibrance Correction**: `Vibrance()` enables intelligent color saturation, protecting already saturated colors from clipping.

## 5. Shadow System (shadows.ffx)

Responsible for casting shadows and their visual quality.

**Soft Shadows (PCSS)**: `GetShadowmapBlockerDist()` calculates distance from an occluder, allowing `ShadowMap()` to dynamically blur shadow edges (the further from the object, the softer the shadow).

**Cloud Shadows**: `GetCloudsIntensity()` adds a layer of dynamic shadows cast by wind-driven clouds.

**Shadow Integration**: `IntegrateShadowMap()` performs multiple shadow map sampling, eliminating aliasing artifacts.

**VSM Moments**: `ComputeMoments()` calculates depth statistics used for Variance Shadow Maps technique to achieve very smooth tonal transitions in shadows.

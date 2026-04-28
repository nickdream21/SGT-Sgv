---
name: frontend-design
description: Create distinctive, production-grade frontend interfaces that avoid generic aesthetics — guides design thinking, typography, color, motion, and spatial composition before writing any HTML/CSS
license: MIT
compatibility: opencode
metadata:
  source: anthropics/skills
  audience: developers
  workflow: ui-design
---

# Frontend Design

This skill guides creation of distinctive, production-grade frontend interfaces that avoid generic "AI slop" aesthetics. Implement real working code with exceptional attention to aesthetic details and creative choices.

The user provides frontend requirements: a component, page, application, or interface to build. They may include context about the purpose, audience, or technical constraints.

> **Context WebSGV**: Este proyecto usa ASP.NET Web Forms (.aspx) con Bootstrap ya incluido en `WebSGV/Content/` y `WebSGV/Scripts/`. El diseño se aplica en archivos `.aspx` (markup) y `.css` bajo `Content/`. No hay compilación de assets — los cambios en CSS son directos.

## Design Thinking

Before coding, understand the context and commit to a BOLD aesthetic direction:

- **Purpose**: What problem does this interface solve? Who uses it?
- **Tone**: Pick an extreme: brutally minimal, maximalist chaos, retro-futuristic, organic/natural, luxury/refined, playful/toy-like, editorial/magazine, brutalist/raw, art deco/geometric, soft/pastel, industrial/utilitarian, etc.
- **Constraints**: Technical requirements (framework, performance, accessibility).
- **Differentiation**: What makes this UNFORGETTABLE? What's the one thing someone will remember?

**CRITICAL**: Choose a clear conceptual direction and execute it with precision. Bold maximalism and refined minimalism both work — the key is intentionality, not intensity.

## Frontend Aesthetics Guidelines

Focus on:

- **Typography**: Choose fonts that are beautiful, unique, and interesting. Avoid generic fonts like Arial and Inter; opt instead for distinctive choices that elevate the frontend's aesthetics.
- **Color & Theme**: Commit to a cohesive aesthetic. Use CSS variables for consistency. Dominant colors with sharp accents outperform timid, evenly-distributed palettes.
- **Motion**: Use animations for effects and micro-interactions. Prioritize CSS-only solutions. Focus on high-impact moments: one well-orchestrated page load with staggered reveals creates more delight than scattered micro-interactions.
- **Spatial Composition**: Unexpected layouts. Asymmetry. Overlap. Diagonal flow. Grid-breaking elements. Generous negative space OR controlled density.
- **Backgrounds & Visual Details**: Create atmosphere and depth. Apply gradient meshes, noise textures, geometric patterns, dramatic shadows, decorative borders.

NEVER use generic aesthetics like overused font families (Inter, Roboto, Arial), clichéd color schemes (purple gradients on white), predictable layouts, or cookie-cutter components.

## Implementation Notes for Web Forms

- Styles go in `WebSGV/Content/Site.css` or a new file linked in `Site.Master`
- Bootstrap classes are available — extend them, don't fight them
- Inline `<style>` blocks on `.aspx` pages are acceptable for page-specific styles
- JavaScript goes in `WebSGV/Scripts/` or inline `<script>` at bottom of `.aspx`
- Images and static assets go in `WebSGV/Content/` or `WebSGV/Images/`
- Globalization is `es-PE` — all UI text in Spanish

## Verification

After implementing a design change:
1. Open the page in IIS Express / Visual Studio F5
2. Check in at least two viewport sizes (desktop + tablet)
3. Verify all form controls remain accessible and functional
4. Confirm no Bootstrap grid breakage

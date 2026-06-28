# Beaver Board Branding Assets

## Source Files

- `source-mascot-favicon.png` — Original mascot with favicon variants
- `source-mascot-logos.png` — Original mascot with logo variations

## Generated Assets

### Web Assets (KittyClaw.Web/wwwroot/)

| File | Size | Usage |
|------|------|-------|
| `BeaverBoard.webp` | 256x256 | Onboarding logo, main icon |
| `BeaverBoard-Logo-Horizontal.webp` | 600x400 | Header logo |
| `BeaverBoard-Picto.webp` | 256x256 | Picto/icon variant |
| `favicon-16x16.png` | 16x16 | Browser tab icon |
| `favicon-32x32.png` | 32x32 | Browser tab icon (standard) |
| `favicon-48x48.png` | 48x48 | Browser tab icon (large) |
| `apple-touch-icon.png` | 180x180 | iOS home screen icon |
| `favicon.ico` | 32x32 | Legacy favicon |

### Raw Crops (branding/beaver-board/)

| File | Description |
|------|-------------|
| `icon-raw.png` | 400x400 crop of square icon |
| `favicon-raw.png` | 120x120 crop of favicon |
| `horizontal-raw.png` | 800x400 crop of horizontal logo |
| `picto-raw.png` | 350x350 crop of mascot |

## Color Palette

From the logo design:

| Color | Hex | Usage |
|-------|-----|-------|
| Primary Orange | `#F97316` | Hard hat, accents, CTA buttons |
| Amber | `#F59E0B` | Secondary accents, highlights |
| Beaver Brown | `#8B4513` | Mascot fur, warm tones |
| Dark Brown | `#5D3A1A` | Shadows, depth |
| White | `#FFFFFF` | Teeth, eyes, highlights |

## Regenerating Assets

If you need to regenerate assets from new source images:

```bash
# Crop icon from source
sips -c 400 400 --cropOffset 150 1050 source.png --out icon-raw.png

# Resize for web
sips -z 256 256 icon-raw.png --out BeaverBoard.webp
sips -z 32 32 icon-raw.png --out favicon-32x32.png
```

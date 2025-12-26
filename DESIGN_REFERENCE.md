# PANOMI UI Design Reference

## Source
Reference image from user's early concept - this is the target design to match.

---

## Colors

### Backgrounds
- **App background:** `#0D0D0F` (near black)
- **Header section:** `#1A1A1F` (slightly lighter dark)
- **Cards/Boxes:** `#1E1E24` (dark gray)
- **Search bar:** `#1E1E24` with `#2a2a35` border

### Text
- **Primary text:** `#FFFFFF` (white)
- **Secondary/Label text:** `#888888` (gray)
- **Placeholder text:** `#666666` (darker gray)

### Accent Colors
- **Primary accent / Launch button / Active filter:** `#00E5FF` (bright cyan)
- **Inactive filter buttons:** `#2a2a35` background, white text

---

## Layout

### Title Bar
- Height: ~40px
- Small logo (gradient square with "PAN/OMI" stacked)
- "PANOMI" text, white, semibold, ~16px

### Header Section
- Large logo: ~56px square, gradient purple-blue (`#667eea` to `#764ba2`)
- "PANOMI" title: ~36px, bold, white
- Buttons below logo: "Scan launchers & games", "Reset library"
  - Dark background `#1E1E24`
  - White text
  - No icons in this version
  - Rounded corners ~8px
  - Horizontal spacing ~12px between buttons
- Stats boxes on right:
  - 3 boxes: Total, Launchers, Games
  - Label in gray above (`#888`)
  - Number in white, large (~32px), bold
  - Dark background, rounded corners ~12px
  - Padding ~24px horizontal, ~16px vertical

### Search & Filter Row
- Search box: Full width minus filter buttons
- Placeholder: "Search by name or tag"
- Filter buttons (pill shaped):
  - "All" - active state: teal background, dark text
  - "Launchers", "Games" - inactive: dark background, white text
  - Border radius: ~20px (pill shape)
  - Padding: ~20px horizontal, ~10px vertical

### Library Section
- "Library" heading: ~24px, bold, white
- "X shown" counter: gray, right-aligned
- Grid: 3 columns
- Gap between cards: ~16px

### Cards
- Width: ~370px (flexible in grid)
- Height: ~160px
- Background: `#1E1E24` (no gradient on card itself)
- Border radius: ~16px
- Padding: ~16px

#### Card Contents
- **Badge** (top-left):
  - "Launcher" or "Game" text
  - Background: `#00000055` (semi-transparent black)
  - Border radius: ~12px
  - Padding: ~12px horizontal, ~5px vertical
  - Font size: ~11px
  
- **Name** (below badge):
  - White text
  - Font size: ~15px
  - Font weight: SemiBold
  - Max 2 lines, wrap

- **Launch Button** (bottom, full width):
  - Gradient background (teal → cyan → magenta)
  - Text: "Launch", dark color `#0D0D0F`
  - Font weight: SemiBold
  - Border radius: ~10px
  - Padding: ~10px vertical
  - Margin: ~12px from card edges

---

## Typography

- **App title:** 36px, Bold
- **Section headers:** 24px, Bold  
- **Card names:** 15px, SemiBold
- **Stats numbers:** 32px, Bold
- **Stats labels:** 13px, Regular
- **Badges:** 11px, Regular
- **Buttons:** 14px, SemiBold
- **Body text:** 14px, Regular

---

## Key Design Notes

1. **No colorful gradients on cards** - cards are solid dark, only the Launch button has gradient
2. **Minimal borders** - clean, borderless look except search box
3. **Consistent rounded corners** - softer UI feel
4. **High contrast** - white text on dark backgrounds
5. **Pill-shaped filter buttons** - not rectangular
6. **Stats boxes are separate** - not merged, with spacing between
7. **Clean button design** - no icons on Scan/Reset buttons in this concept

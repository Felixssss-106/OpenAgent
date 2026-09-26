#!/usr/bin/env python3
"""Turn design/tokens.css into the WinUI resource dictionary.

design/tokens.css is the single source of truth for colour, type, spacing,
radius and motion. WinUI cannot consume OKLCH, so this script converts the
selected palette to sRGB and emits Themes/Tokens.xaml. Re-run it whenever
tokens.css changes instead of hand-editing the XAML.

    python scripts/gen-tokens.py --palette graphite
"""

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TOKENS = ROOT / "design" / "tokens.css"
# The dictionary lives in the app project: it is merged from App.xaml, and a
# cross-assembly ResourceDictionary Source is unreliable in unpackaged apps.
OUTPUT = ROOT / "src" / "apps" / "windows" / "OpenAgent.Windows" / "Themes" / "Tokens.xaml"


def oklch_to_srgb(lightness: float, chroma: float, hue: float):
    """OKLCH -> sRGB (0..255). Standard Björn Ottosson conversion."""
    h = math.radians(hue)
    lab_l = lightness / 100.0
    lab_a = chroma * math.cos(h)
    lab_b = chroma * math.sin(h)

    l_ = lab_l + 0.3963377774 * lab_a + 0.2158037573 * lab_b
    m_ = lab_l - 0.1055613458 * lab_a - 0.0638541728 * lab_b
    s_ = lab_l - 0.0894841775 * lab_a - 1.2914855480 * lab_b

    l = l_ ** 3
    m = m_ ** 3
    s = s_ ** 3

    r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s
    g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s
    b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s

    def encode(channel: float) -> int:
        channel = max(0.0, min(1.0, channel))
        if channel <= 0.0031308:
            value = 12.92 * channel
        else:
            value = 1.055 * (channel ** (1 / 2.4)) - 0.055
        return round(value * 255)

    return encode(r), encode(g), encode(b)


def to_hex(value: str) -> str:
    """'oklch(52.0% 0.145 258)' or 'oklch(24% 0.012 250 / 0.32)' -> '#AARRGGBB'."""
    match = re.search(
        r"oklch\(\s*([\d.]+)%\s+([\d.]+)\s+([\d.]+)\s*(?:/\s*([\d.]+)\s*)?\)", value
    )
    if not match:
        raise ValueError(f"unsupported colour: {value}")

    lightness, chroma, hue, alpha = match.groups()
    r, g, b = oklch_to_srgb(float(lightness), float(chroma), float(hue))
    a = round(float(alpha) * 255) if alpha else 255
    return f"#{a:02X}{r:02X}{g:02X}{b:02X}"


def parse_block(css: str, selector: str) -> dict:
    # Anchored at the start of a line: an unanchored search would mistake
    # '[data-theme="light"]' for the tail of '[data-palette="graphite"][data-theme="light"]'
    # and silently drop the shared risk/status block.
    pattern = r"(?m)^[ \t]*" + re.escape(selector) + r"\s*\{(.*?)\}"
    match = re.search(pattern, css, flags=re.S)
    if not match:
        raise ValueError(f"selector not found: {selector}")
    # Comments can sit between declarations; drop them before splitting.
    body = re.sub(r"/\*.*?\*/", "", match.group(1), flags=re.S)
    out = {}
    # tokens.css packs several declarations per line, so split on ';' not on '\n'
    for declaration in body.split(";"):
        declaration = declaration.strip()
        if not declaration or declaration.startswith("/*") or not declaration.startswith("--"):
            continue
        name, _, value = declaration.partition(":")
        out[name.strip()[2:]] = value.strip()
    return out


# Tokens that are exposed as brushes in WinUI.
COLOUR_TOKENS = [
    "bg-canvas", "bg-surface", "bg-raised", "bg-sunken", "bg-inset",
    "border-subtle", "border-default", "border-strong",
    "text-primary", "text-secondary", "text-tertiary", "text-quaternary", "text-inverse",
    "accent", "accent-hover", "accent-quiet", "accent-text", "on-accent",
    "focus-ring-color", "scrim",
]

RISK_TOKENS = [
    "risk-safe", "risk-safe-bg", "risk-low", "risk-low-bg", "risk-medium", "risk-medium-bg",
    "risk-high", "risk-high-bg", "risk-critical", "risk-critical-bg",
    "status-online", "status-pending", "status-offline", "status-error",
]


def xaml_name(token: str) -> str:
    parts = token.split("-")
    return "".join(p.capitalize() for p in parts)


def theme_dictionary(name: str, colours: dict) -> str:
    lines = [f'        <ResourceDictionary x:Key="{name}">']
    for token in COLOUR_TOKENS + RISK_TOKENS:
        if token not in colours:
            continue
        lines.append(
            f'            <Color x:Key="{xaml_name(token)}Color">{to_hex(colours[token])}</Color>'
        )
    for token in COLOUR_TOKENS + RISK_TOKENS:
        if token not in colours:
            continue
        lines.append(
            f'            <SolidColorBrush x:Key="{xaml_name(token)}Brush" '
            f'Color="{{StaticResource {xaml_name(token)}Color}}" />'
        )
    lines.append("        </ResourceDictionary>")
    return "\n".join(lines)


def emit(palette: str) -> str:
    css = TOKENS.read_text(encoding="utf-8")

    light = parse_block(css, f'[data-palette="{palette}"][data-theme="light"]')
    dark = parse_block(css, f'[data-palette="{palette}"][data-theme="dark"]')
    light |= parse_block(css, '[data-theme="light"]')
    dark |= parse_block(css, '[data-theme="dark"]')

    def px(token: str) -> str:
        value = parse_block(css, ":root")[token]
        return value.replace("px", "").strip()

    root = parse_block(css, ":root")

    return f"""<!-- GENERATED by scripts/gen-tokens.py from design/tokens.css (palette: {palette}).
     Do not edit by hand — edit the tokens and re-run the generator. -->
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ResourceDictionary.ThemeDictionaries>
{theme_dictionary("Light", light)}
{theme_dictionary("Dark", dark)}
    </ResourceDictionary.ThemeDictionaries>

    <!-- Type -->
    <FontFamily x:Key="UiFont">Segoe UI Variable Text, Segoe UI, PingFang SC, Microsoft YaHei UI</FontFamily>
    <FontFamily x:Key="DisplayFont">Segoe UI Variable Display, Segoe UI, PingFang SC, Microsoft YaHei UI</FontFamily>
    <FontFamily x:Key="MonoFont">Cascadia Code, Cascadia Mono, Consolas</FontFamily>

    <x:Double x:Key="TypeDisplaySize">{px("t-display-size")}</x:Double>
    <x:Double x:Key="TypeTitleSize">{px("t-title-size")}</x:Double>
    <x:Double x:Key="TypeHeadingSize">{px("t-heading-size")}</x:Double>
    <x:Double x:Key="TypeBodySize">{px("t-body-size")}</x:Double>
    <x:Double x:Key="TypeCalloutSize">{px("t-callout-size")}</x:Double>
    <x:Double x:Key="TypeCaptionSize">{px("t-caption-size")}</x:Double>
    <x:Double x:Key="TypeMicroSize">{px("t-micro-size")}</x:Double>
    <x:Double x:Key="TypeCodeSize">13</x:Double>

    <!-- Spacing (8pt grid) -->
    <x:Double x:Key="Space1">{px("s-1")}</x:Double>
    <x:Double x:Key="Space2">{px("s-2")}</x:Double>
    <x:Double x:Key="Space3">{px("s-3")}</x:Double>
    <x:Double x:Key="Space4">{px("s-4")}</x:Double>
    <x:Double x:Key="Space5">{px("s-5")}</x:Double>
    <x:Double x:Key="Space6">{px("s-6")}</x:Double>
    <x:Double x:Key="Space8">{px("s-8")}</x:Double>
    <x:Double x:Key="Space10">{px("s-10")}</x:Double>
    <x:Double x:Key="Space12">{px("s-12")}</x:Double>
    <x:Double x:Key="Space16">{px("s-16")}</x:Double>

    <!-- Radii: only two steps are used for containers and pills -->
    <CornerRadius x:Key="RadiusXs">{px("r-xs")}</CornerRadius>
    <CornerRadius x:Key="RadiusSm">{px("r-sm")}</CornerRadius>
    <CornerRadius x:Key="RadiusMd">{px("r-md")}</CornerRadius>
    <CornerRadius x:Key="RadiusLg">{px("r-lg")}</CornerRadius>
    <CornerRadius x:Key="RadiusXl">{px("r-xl")}</CornerRadius>
    <CornerRadius x:Key="RadiusPill">{px("r-full")}</CornerRadius>

    <!-- Layout constants from the design system -->
    <x:Double x:Key="SidebarWidth">240</x:Double>
    <x:Double x:Key="ContentLeftInset">96</x:Double>
    <x:Double x:Key="ContentLeftBaseline">336</x:Double>
    <Thickness x:Key="ContentPadding">96,32,96,32</Thickness>

    <Style x:Key="TypeDisplay" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource DisplayFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeDisplaySize}}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{{ThemeResource TextPrimaryBrush}}" />
    </Style>
    <Style x:Key="TypeTitle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource DisplayFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeTitleSize}}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{{ThemeResource TextPrimaryBrush}}" />
    </Style>
    <Style x:Key="TypeHeading" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource UiFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeHeadingSize}}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{{ThemeResource TextPrimaryBrush}}" />
    </Style>
    <Style x:Key="TypeBody" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource UiFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeBodySize}}" />
        <Setter Property="Foreground" Value="{{ThemeResource TextPrimaryBrush}}" />
    </Style>
    <Style x:Key="TypeCallout" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource UiFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeCalloutSize}}" />
        <Setter Property="Foreground" Value="{{ThemeResource TextSecondaryBrush}}" />
    </Style>
    <Style x:Key="TypeCaption" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource UiFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeCaptionSize}}" />
        <Setter Property="Foreground" Value="{{ThemeResource TextTertiaryBrush}}" />
    </Style>
    <Style x:Key="TypeMicro" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource UiFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeMicroSize}}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Foreground" Value="{{ThemeResource TextTertiaryBrush}}" />
    </Style>
    <Style x:Key="TypeCode" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{{StaticResource MonoFont}}" />
        <Setter Property="FontSize" Value="{{StaticResource TypeCodeSize}}" />
        <Setter Property="Foreground" Value="{{ThemeResource TextSecondaryBrush}}" />
    </Style>

</ResourceDictionary>
"""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--palette", default="graphite", choices=["graphite", "paper", "midnight"])
    args = parser.parse_args()

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(emit(args.palette), encoding="utf-8", newline="\n")
    print(f"wrote {OUTPUT.relative_to(ROOT)} (palette={args.palette})")
    return 0


if __name__ == "__main__":
    sys.exit(main())

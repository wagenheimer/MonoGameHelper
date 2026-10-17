using System;
using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Gum;

/// <summary>
/// Defines visual material properties for text rendered via KernSmith in Gum:
/// vertical color gradients, soft blurred drop shadows, and atlas padding.
/// </summary>
public record FontMaterial(
    Color GradientTop,
    Color GradientBottom,
    float GradientAngle = 90f,
    float ShadowOpacity = 0.55f,
    float ShadowBlur = 3f,
    int ShadowOffsetX = 0,
    int ShadowOffsetY = 2,
    Color? ShadowColor = null,
    int Padding = 4)
{
    /// <summary>Vibrant casual gold (top yellow -> bottom amber orange).</summary>
    public static FontMaterial Gold { get; } = new(
        GradientTop: new Color(255, 216, 0),
        GradientBottom: new Color(255, 142, 0),
        ShadowOpacity: 0.60f,
        ShadowBlur: 3.5f,
        ShadowOffsetY: 2);

    /// <summary>Metallic silver/white gradient.</summary>
    public static FontMaterial Silver { get; } = new(
        GradientTop: Color.White,
        GradientBottom: new Color(176, 176, 176),
        ShadowOpacity: 0.55f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Warm medieval bronze.</summary>
    public static FontMaterial Bronze { get; } = new(
        GradientTop: new Color(230, 161, 92),
        GradientBottom: new Color(140, 78, 26),
        ShadowOpacity: 0.65f,
        ShadowBlur: 3.5f,
        ShadowOffsetY: 2);

    /// <summary>Fresh casual emerald green (bright lime -> forest green).</summary>
    public static FontMaterial Emerald { get; } = new(
        GradientTop: new Color(144, 255, 0),
        GradientBottom: new Color(58, 143, 36),
        ShadowOpacity: 0.55f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Fiery ruby / crimson red.</summary>
    public static FontMaterial Ruby { get; } = new(
        GradientTop: new Color(255, 75, 75),
        GradientBottom: new Color(158, 0, 0),
        ShadowOpacity: 0.60f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Bright casual amber.</summary>
    public static FontMaterial Amber { get; } = new(
        GradientTop: new Color(255, 185, 0),
        GradientBottom: new Color(247, 106, 7),
        ShadowOpacity: 0.55f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Vibrant ocean cyan/blue.</summary>
    public static FontMaterial Sapphire { get; } = new(
        GradientTop: new Color(0, 210, 255),
        GradientBottom: new Color(0, 85, 212),
        ShadowOpacity: 0.60f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Magic royal purple/amethyst.</summary>
    public static FontMaterial Amethyst { get; } = new(
        GradientTop: new Color(224, 132, 255),
        GradientBottom: new Color(104, 24, 165),
        ShadowOpacity: 0.60f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);

    /// <summary>Pure white with soft drop shadow.</summary>
    public static FontMaterial WhiteShadow { get; } = new(
        GradientTop: Color.White,
        GradientBottom: Color.White,
        ShadowOpacity: 0.55f,
        ShadowBlur: 3f,
        ShadowOffsetY: 2);
}

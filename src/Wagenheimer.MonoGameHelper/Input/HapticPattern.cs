namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Predefined haptic and vibration feedback patterns covering UI, Match-3 gameplay,
/// and isometric farm building/management.
/// </summary>
public enum HapticPattern
{
    // --- UI & GENERAL NAVIGATION ---
    /// <summary>Subtle tactile tick when hovering over an interactive button.</summary>
    ButtonHover,
    /// <summary>Crisp click feedback on confirming an action.</summary>
    ButtonClick,
    /// <summary>Light toggle switch vibration.</summary>
    ToggleSwitch,
    /// <summary>Short double-buzz warning for invalid moves, insufficient resources, or errors.</summary>
    ErrorOrDenied,

    // --- MATCH-3 GAMEPLAY ---
    /// <summary>Subtle tap when a token is picked/selected on the grid.</summary>
    TileSelect,
    /// <summary>Quick soft tick when swapping two adjacent tokens.</summary>
    TileSwap,
    /// <summary>Very light, crisp mechanical click for standard 3-tile matches (non-fatiguing).</summary>
    Match3,
    /// <summary>Deeper pulse for 4 or 5-tile matches.</summary>
    Match4Or5,
    /// <summary>Light pop on sequential cascade drops.</summary>
    CascadeStep,

    // --- MATCH-3 POWER-UPS & EXPLOSIONS ---
    /// <summary>Ascending chime pulse when a power-up is synthesized on the board.</summary>
    PowerUpSpawn,
    /// <summary>Quick snap/pop for firecracker blast.</summary>
    FirecrackerPop,
    /// <summary>Propulsive burst for horizontal/vertical rockets.</summary>
    RocketLaunch,
    /// <summary>Heavy low-frequency explosion thud for bombs.</summary>
    BombExplosion,
    /// <summary>Deep shockwave vibration for mega bomb/power-up combos.</summary>
    MegaComboExplosion,
    /// <summary>High-frequency electric pulse per lightning beam emitted by LightBall.</summary>
    LightBallBeamPulse,
    /// <summary>Concentrated impact burst when LightBall targets detonate.</summary>
    LightBallImpact,

    // --- ISOMETRIC FARM & CONSTRUCTION ---
    /// <summary>Tactile thud when picking up a building or decoration from the farm grid.</summary>
    BuildingPickup,
    /// <summary>Micro-click when dragging a building across valid placement grid cells.</summary>
    BuildingHoverValid,
    /// <summary>Short friction rumble when dragging over blocked/invalid tiles.</summary>
    BuildingHoverInvalid,
    /// <summary>Firm solid thud when snapping/placing a structure onto the ground.</summary>
    BuildingPlace,
    /// <summary>Heavy rubble crumble vibration when demolishing or stashing a structure.</summary>
    BuildingDemolish,
    /// <summary>Soft earthy pop when planting crops.</summary>
    CropPlant,
    /// <summary>Satisfying tactile pop when harvesting ripe crops or animal produce.</summary>
    CropHarvest,
    /// <summary>Light metallic twinkle vibration when collecting coins or stars.</summary>
    CoinOrStarCollect,
    /// <summary>Celebratory rhythmic double-burst on level up or quest completion.</summary>
    LevelUp
}

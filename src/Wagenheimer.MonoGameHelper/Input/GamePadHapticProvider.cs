using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Default implementation of <see cref="IHapticFeedbackProvider"/> utilizing MonoGame's <see cref="GamePad.SetVibration"/>.
/// Features smooth decay envelopes, priority-based overriding, and anti-fatigue throttling.
/// </summary>
public class GamePadHapticProvider : IHapticFeedbackProvider
{
    private struct VibrationSlot
    {
        public float LeftMotor;
        public float RightMotor;
        public float Duration;
        public float Elapsed;
        public bool Active;
    }

    private readonly VibrationSlot[] _slots = new VibrationSlot[4];
    private float _lastMatchHapticTime = -10f;
    private float _currentTime;

    public void Play(HapticPattern pattern, PlayerIndex player = PlayerIndex.One)
    {
        switch (pattern)
        {
            // --- UI ---
            case HapticPattern.ButtonHover:
                Vibrate(0.0f, 0.08f, 0.035f, player);
                break;
            case HapticPattern.ButtonClick:
                Vibrate(0.0f, 0.22f, 0.06f, player);
                break;
            case HapticPattern.ToggleSwitch:
                Vibrate(0.05f, 0.15f, 0.05f, player);
                break;
            case HapticPattern.ErrorOrDenied:
                Vibrate(0.35f, 0.10f, 0.16f, player);
                break;

            // --- MATCH-3 ---
            case HapticPattern.TileSelect:
                Vibrate(0.0f, 0.10f, 0.04f, player);
                break;
            case HapticPattern.TileSwap:
                Vibrate(0.0f, 0.15f, 0.05f, player);
                break;
            case HapticPattern.Match3:
                // Anti-fatigue throttle: prevent rapid-fire cascades from continuous rumbling
                if (_currentTime - _lastMatchHapticTime >= 0.09f)
                {
                    _lastMatchHapticTime = _currentTime;
                    Vibrate(0.0f, 0.12f, 0.055f, player);
                }
                break;
            case HapticPattern.Match4Or5:
                Vibrate(0.12f, 0.28f, 0.09f, player);
                break;
            case HapticPattern.CascadeStep:
                Vibrate(0.05f, 0.14f, 0.06f, player);
                break;

            // --- POWER-UPS ---
            case HapticPattern.PowerUpSpawn:
                Vibrate(0.08f, 0.25f, 0.12f, player);
                break;
            case HapticPattern.FirecrackerPop:
                Vibrate(0.22f, 0.45f, 0.14f, player);
                break;
            case HapticPattern.RocketLaunch:
                Vibrate(0.32f, 0.42f, 0.18f, player);
                break;
            case HapticPattern.BombExplosion:
                Vibrate(0.68f, 0.55f, 0.32f, player);
                break;
            case HapticPattern.MegaComboExplosion:
                Vibrate(0.92f, 0.82f, 0.45f, player);
                break;
            case HapticPattern.LightBallBeamPulse:
                Vibrate(0.0f, 0.18f, 0.045f, player);
                break;
            case HapticPattern.LightBallImpact:
                Vibrate(0.40f, 0.60f, 0.22f, player);
                break;

            // --- ISOMETRIC FARM & CONSTRUCTION ---
            case HapticPattern.BuildingPickup:
                Vibrate(0.18f, 0.20f, 0.08f, player);
                break;
            case HapticPattern.BuildingHoverValid:
                Vibrate(0.0f, 0.06f, 0.03f, player);
                break;
            case HapticPattern.BuildingHoverInvalid:
                Vibrate(0.25f, 0.05f, 0.10f, player);
                break;
            case HapticPattern.BuildingPlace:
                Vibrate(0.45f, 0.30f, 0.16f, player);
                break;
            case HapticPattern.BuildingDemolish:
                Vibrate(0.55f, 0.35f, 0.25f, player);
                break;
            case HapticPattern.CropPlant:
                Vibrate(0.0f, 0.14f, 0.05f, player);
                break;
            case HapticPattern.CropHarvest:
                Vibrate(0.12f, 0.24f, 0.07f, player);
                break;
            case HapticPattern.CoinOrStarCollect:
                Vibrate(0.0f, 0.18f, 0.04f, player);
                break;
            case HapticPattern.LevelUp:
                Vibrate(0.50f, 0.65f, 0.38f, player);
                break;
        }
    }

    public void Vibrate(float leftMotor, float rightMotor, float durationSeconds, PlayerIndex player = PlayerIndex.One)
    {
        int index = (int)player;
        if (index < 0 || index >= _slots.Length) return;

        leftMotor = Math.Clamp(leftMotor * HapticFeedback.GlobalIntensity, 0f, 1f);
        rightMotor = Math.Clamp(rightMotor * HapticFeedback.GlobalIntensity, 0f, 1f);

        if (!HapticFeedback.Enabled || (leftMotor <= 0.001f && rightMotor <= 0.001f) || durationSeconds <= 0f)
        {
            return;
        }

        ref var slot = ref _slots[index];

        // Priority rule: if current active vibration has stronger motors, only overwrite if new one is reasonably strong or longer
        if (slot.Active)
        {
            float currentPower = Math.Max(slot.LeftMotor, slot.RightMotor);
            float newPower = Math.Max(leftMotor, rightMotor);
            if (newPower < currentPower * 0.5f && (slot.Duration - slot.Elapsed) > 0.05f)
            {
                return; // Maintain stronger vibration
            }
        }

        slot.LeftMotor = leftMotor;
        slot.RightMotor = rightMotor;
        slot.Duration = durationSeconds;
        slot.Elapsed = 0f;
        slot.Active = true;

        GamePad.SetVibration(player, leftMotor, rightMotor);
    }

    public void Stop(PlayerIndex player = PlayerIndex.One)
    {
        int index = (int)player;
        if (index >= 0 && index < _slots.Length)
        {
            _slots[index].Active = false;
            _slots[index].LeftMotor = 0f;
            _slots[index].RightMotor = 0f;
            GamePad.SetVibration(player, 0f, 0f);
        }
    }

    public void Update(float deltaTime)
    {
        _currentTime += deltaTime;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Active) continue;

            _slots[i].Elapsed += deltaTime;
            if (_slots[i].Elapsed >= _slots[i].Duration)
            {
                _slots[i].Active = false;
                _slots[i].LeftMotor = 0f;
                _slots[i].RightMotor = 0f;
                GamePad.SetVibration((PlayerIndex)i, 0f, 0f);
            }
            else
            {
                // Smooth envelope decay in the final 30% of duration
                float remaining = 1f - (_slots[i].Elapsed / _slots[i].Duration);
                float decayFactor = remaining < 0.35f ? (remaining / 0.35f) : 1f;

                GamePad.SetVibration((PlayerIndex)i,
                    _slots[i].LeftMotor * decayFactor,
                    _slots[i].RightMotor * decayFactor);
            }
        }
    }
}

using System.Windows;
using System.Windows.Media.Imaging;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services.Sprites;

namespace ComputerBuddy.App.Services.Buddy;

public sealed class BuddyRuntime
{
    private readonly Random _rng = new();

    private double _stateTimeLeft;
    private BuddyState _state = BuddyState.Idle;
    private int _direction = 1;

    private SpriteAnimation _currentAnim;
    private int _frameIndex;
    private double _frameTimeLeft;

    public BuddyRuntime(UserSettings settings, SpriteSet spriteSet)
    {
        Settings = settings;
        SpriteSet = spriteSet;
        _currentAnim = spriteSet.Animations.TryGetValue("idle_right", out var anim) ? anim : spriteSet.Animations.Values.First();
        ResetState();
    }

    public UserSettings Settings { get; }
    public SpriteSet SpriteSet { get; private set; }

    public double X { get; private set; }
    public double Y { get; private set; }

    public double Width => SpriteSet.BaseWidth * Settings.Scale;
    public double Height => SpriteSet.BaseHeight * Settings.Scale;

    public BitmapSource CurrentFrame => _currentAnim.Frames[_frameIndex];

    public void SetInitialPosition(Rect workAreaDip)
    {
        var x = workAreaDip.Left + Math.Max(0, (workAreaDip.Width - Width) / 2.0);
        X = Clamp(x, workAreaDip.Left, workAreaDip.Right - Width);
        Y = Clamp(workAreaDip.Bottom - Height, workAreaDip.Top, workAreaDip.Bottom - Height);
    }

    public void SetPosition(double xDip, Rect workAreaDip)
    {
        X = Clamp(xDip, workAreaDip.Left, workAreaDip.Right - Width);
        Y = Clamp(workAreaDip.Bottom - Height, workAreaDip.Top, workAreaDip.Bottom - Height);
    }

    public void Update(double dtSeconds, Rect workAreaDip)
    {
        Y = Clamp(workAreaDip.Bottom - Height, workAreaDip.Top, workAreaDip.Bottom - Height);

        _stateTimeLeft -= dtSeconds;
        if (_stateTimeLeft <= 0)
        {
            TransitionState();
        }

        if (_state == BuddyState.Walk)
        {
            X += _direction * Settings.SpeedDipPerSecond * dtSeconds;
            var minX = workAreaDip.Left;
            var maxX = workAreaDip.Right - Width;
            if (X <= minX)
            {
                X = minX;
                _direction = 1;
            }
            else if (X >= maxX)
            {
                X = Math.Max(minX, maxX);
                _direction = -1;
            }
        }

        _frameTimeLeft -= dtSeconds;
        if (_frameTimeLeft <= 0)
        {
            _frameIndex = (_frameIndex + 1) % _currentAnim.Frames.Count;
            _frameTimeLeft = 1.0 / Math.Max(1, _currentAnim.Fps);
        }
    }

    public void ApplySpriteSet(SpriteSet spriteSet)
    {
        SpriteSet = spriteSet;
        _currentAnim = spriteSet.Animations.TryGetValue(GetAnimName(), out var anim) ? anim : spriteSet.Animations.Values.First();
        _frameIndex = 0;
        _frameTimeLeft = 1.0 / Math.Max(1, _currentAnim.Fps);
    }

    private void TransitionState()
    {
        _state = _state == BuddyState.Idle ? BuddyState.Walk : BuddyState.Idle;
        if (_state == BuddyState.Walk)
        {
            if (_rng.NextDouble() < 0.5)
            {
                _direction *= -1;
            }
        }

        ResetState();
    }

    private void ResetState()
    {
        _stateTimeLeft = _state switch
        {
            BuddyState.Idle => 0.8 + _rng.NextDouble() * 1.8,
            BuddyState.Walk => 1.2 + _rng.NextDouble() * 2.8,
            _ => 1.0
        };

        _currentAnim = SpriteSet.Animations.TryGetValue(GetAnimName(), out var anim) ? anim : SpriteSet.Animations.Values.First();
        _frameIndex = 0;
        _frameTimeLeft = 1.0 / Math.Max(1, _currentAnim.Fps);
    }

    private string GetAnimName()
    {
        var facing = _direction >= 0 ? "right" : "left";
        return _state == BuddyState.Walk ? $"walk_{facing}" : $"idle_{facing}";
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Max(min, Math.Min(value, max));
    }

    private enum BuddyState
    {
        Idle,
        Walk
    }
}


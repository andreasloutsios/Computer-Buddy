﻿﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services;
using ComputerBuddy.App.Services.Buddy;
using ComputerBuddy.App.Services.Minifigure;
using ComputerBuddy.App.Services.Sprites;

namespace ComputerBuddy.App;

public partial class MainWindow : Window
{
    private readonly UserSettings _settings;
    private readonly SpriteSetFactory _spriteSetFactory = new();

    private BuddyRuntime _buddyRuntime;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _reminderCloseTimer;
    private readonly DispatcherTimer _petCloseTimer;
    private readonly DispatcherTimer _actionCloseTimer;
    private readonly DropShadowEffect _glowEffect = new()
    {
        Color = Colors.White,
        BlurRadius = 18,
        ShadowDepth = 0,
        Opacity = 0
    };

    private bool _isDragging;
    private System.Windows.Point _dragOffsetDip;
    private bool _mouseDownWithoutDrag;
    private ModifierKeys _mouseDownModifiers;
    private readonly ScaleTransform _scaleTransform = new(1, 1);
    private readonly RotateTransform _rotateTransform = new(0);
    private readonly TranslateTransform _translateTransform = new(0, 0);
    private ContextMenu? _contextMenu;
    private MenuItem? _followMouseMenuItem;
    private bool _isHovering;
    private int _emoteIndex;
    private bool _followMouse;
    private double? _followMouseSavedSpeed;
    private double? _tempSpeedSaved;
    private DateTimeOffset? _tempSpeedUntil;
    private Storyboard? _activeStoryboard;

    public MainWindow(SettingsService settingsService, UserSettings settings)
    {
        _settings = settings;

        InitializeComponent();

        var transforms = new TransformGroup();
        transforms.Children.Add(_scaleTransform);
        transforms.Children.Add(_rotateTransform);
        transforms.Children.Add(_translateTransform);
        BuddyImage.RenderTransform = transforms;
        BuddyImage.Effect = _glowEffect;

        _buddyRuntime = new BuddyRuntime(_settings, LoadSpriteSet());

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += (_, _) => Tick();

        _reminderCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _reminderCloseTimer.Tick += (_, _) =>
        {
            _reminderCloseTimer.Stop();
            ReminderPopup.IsOpen = false;
        };

        _petCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _petCloseTimer.Tick += (_, _) =>
        {
            _petCloseTimer.Stop();
            PetPopup.IsOpen = false;
        };

        _actionCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1100)
        };
        _actionCloseTimer.Tick += (_, _) =>
        {
            _actionCloseTimer.Stop();
            ActionPopup.IsOpen = false;
        };

        Loaded += (_, _) =>
        {
            ApplySizing();
            SetInitialPosition();
            _stopwatch.Restart();
            _timer.Start();
        };

        PreviewKeyDown += (_, _) => UpdateCursorForState();
        PreviewKeyUp += (_, _) => UpdateCursorForState();
    }

    public void ReloadSprites()
    {
        _buddyRuntime.ApplySpriteSet(LoadSpriteSet());
        ApplySizing();
    }

    public void ShowReminder(string text)
    {
        ReminderText.Text = text;
        ReminderPopup.IsOpen = true;
        _reminderCloseTimer.Stop();
        _reminderCloseTimer.Start();
    }

    public void ApplySizing()
    {
        Width = _buddyRuntime.Width;
        Height = _buddyRuntime.Height;
        BuddyImage.Width = Width;
        BuddyImage.Height = Height;
    }

    private SpriteSet LoadSpriteSet()
    {
        if (string.Equals(_settings.CharacterMode, "sprite_sheet", StringComparison.OrdinalIgnoreCase))
        {
            if (_spriteSetFactory.TryCreateFromFiles(_settings.SpriteSheetPngPath, _settings.SpriteSheetManifestPath, out var set) && set is not null)
            {
                return set;
            }

            return _spriteSetFactory.CreateDefault();
        }

        var minifigure = new MinifigureSpriteSetFactory();
        return minifigure.Create(_settings);
    }

    private void SetInitialPosition()
    {
        var workArea = SystemParameters.WorkArea;
        _buddyRuntime.SetInitialPosition(workArea);
        Left = _buddyRuntime.X;
        Top = _buddyRuntime.Y;
    }

    private void Tick()
    {
        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();

        var workArea = SystemParameters.WorkArea;

        UpdateTemporarySpeed();

        if (!_isDragging)
        {
            _buddyRuntime.Update(elapsed, workArea);

            if (_followMouse)
            {
                var targetX = GetMouseXDip() - (Width / 2.0);
                var maxStep = 750 * elapsed;
                var dx = targetX - Left;
                var step = Math.Max(-maxStep, Math.Min(maxStep, dx));
                var x = Left + step;
                x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - Width));

                Left = x;
                _buddyRuntime.SetPosition(Left, workArea);
            }
            else
            {
                Left = _buddyRuntime.X;
            }

            Top = _buddyRuntime.Y;
        }
        else
        {
            Top = Math.Max(workArea.Top, workArea.Bottom - Height);
        }

        BuddyImage.Source = _buddyRuntime.CurrentFrame;
    }

    private void BuddyImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            if (System.Windows.Application.Current is App app)
            {
                app.ToggleBuddyVisibility();
            }
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            _isDragging = true;
            _mouseDownWithoutDrag = false;
            _dragOffsetDip = e.GetPosition(this);
            BuddyImage.CaptureMouse();
            SetGlow(true, Colors.DeepSkyBlue, 0.65);
            UpdateCursorForState();
            return;
        }

        _isDragging = false;
        _mouseDownWithoutDrag = true;
        _mouseDownModifiers = Keyboard.Modifiers;
    }

    private void BuddyImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            BuddyImage.ReleaseMouseCapture();
            SetGlow(_isHovering, Colors.White, 0.55);
            UpdateCursorForState();
            _buddyRuntime.SetPosition(Left, SystemParameters.WorkArea);
            return;
        }

        if (_mouseDownWithoutDrag)
        {
            _mouseDownWithoutDrag = false;
            if ((_mouseDownModifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                (System.Windows.Application.Current as App)?.ShowChat();
                ShowActionPopup("Chat", Colors.White);
                return;
            }

            if ((_mouseDownModifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                ShowNextEmote();
                return;
            }

            PlayPetAnimation();
        }
    }

    private void BuddyImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;

        var mouseScreen = PointToScreen(e.GetPosition(this));
        var xDip = mouseScreen.X - _dragOffsetDip.X;

        xDip = Math.Max(workArea.Left, Math.Min(xDip, workArea.Right - Width));

        Left = xDip;
        Top = Math.Max(workArea.Top, workArea.Bottom - Height);
        _buddyRuntime.SetPosition(Left, workArea);
    }

    private void BuddyImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        PlayClickPulse();
        EnsureContextMenu();
        if (_contextMenu is null)
        {
            return;
        }

        _contextMenu.PlacementTarget = BuddyImage;
        _contextMenu.IsOpen = true;
    }

    private void EnsureContextMenu()
    {
        if (_contextMenu is not null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Opened += (_, _) => UpdateFollowMouseMenuItem();

        var talk = new MenuItem { Header = "Talk" };
        talk.Click += (_, _) => (System.Windows.Application.Current as App)?.ShowChat();
        menu.Items.Add(talk);

        var actions = new MenuItem { Header = "Actions" };
        var wave = new MenuItem { Header = "Wave" };
        wave.Click += (_, _) => ShowActionPopup("👋", Colors.White);
        actions.Items.Add(wave);

        var dance = new MenuItem { Header = "Dance" };
        dance.Click += (_, _) => PlayDanceAnimation();
        actions.Items.Add(dance);

        var sleep = new MenuItem { Header = "Sleep (10s)" };
        sleep.Click += (_, _) =>
        {
            ApplyTemporarySpeed(0, TimeSpan.FromSeconds(10));
            ShowActionPopup("Zzz", Colors.White);
        };
        actions.Items.Add(sleep);

        var emote = new MenuItem { Header = "Next Emote" };
        emote.Click += (_, _) => ShowNextEmote();
        actions.Items.Add(emote);

        var randomAction = new MenuItem { Header = "Random Action" };
        randomAction.Click += (_, _) => PlayRandomAction();
        actions.Items.Add(randomAction);

        var stopAction = new MenuItem { Header = "Stop Action" };
        stopAction.Click += (_, _) => StopCurrentAction();
        actions.Items.Add(stopAction);

        var follow = new MenuItem { IsCheckable = true };
        _followMouseMenuItem = follow;
        UpdateFollowMouseMenuItem();
        follow.Click += (_, _) => ToggleFollowMouse();
        actions.Items.Add(follow);
        menu.Items.Add(actions);

        var feed = new MenuItem { Header = "Feed" };
        var cookie = new MenuItem { Header = "Cookie (2s)" };
        cookie.Click += (_, _) => PlayCookieAnimation();
        feed.Items.Add(cookie);

        var coffee = new MenuItem { Header = "Coffee (10s)" };
        coffee.Click += (_, _) => PlayCoffeeAnimation();
        feed.Items.Add(coffee);

        var pizza = new MenuItem { Header = "Pizza (4s)" };
        pizza.Click += (_, _) => PlayPizzaAnimation();
        feed.Items.Add(pizza);

        var randomSnack = new MenuItem { Header = "Random Snack" };
        randomSnack.Click += (_, _) => PlayRandomSnack();
        feed.Items.Add(randomSnack);
        menu.Items.Add(feed);

        var control = new MenuItem { Header = "Control Center" };
        control.Click += (_, _) => (System.Windows.Application.Current as App)?.OpenControlCenter();
        menu.Items.Add(control);

        var settings = new MenuItem { Header = "Settings" };
        settings.Click += (_, _) => (System.Windows.Application.Current as App)?.ShowSettings();
        menu.Items.Add(settings);

        var sys = new MenuItem { Header = "System Info" };
        sys.Click += (_, _) => (System.Windows.Application.Current as App)?.ShowSystemInfo();
        menu.Items.Add(sys);

        menu.Items.Add(new Separator());

        var hide = new MenuItem { Header = "Hide" };
        hide.Click += (_, _) => (System.Windows.Application.Current as App)?.ToggleBuddyVisibility();
        menu.Items.Add(hide);

        var exit = new MenuItem { Header = "Exit (Quit App)" };
        exit.Click += (_, _) => System.Windows.Application.Current?.Shutdown();
        menu.Items.Add(exit);

        _contextMenu = menu;
    }

    private void UpdateFollowMouseMenuItem()
    {
        if (_followMouseMenuItem is null)
        {
            return;
        }

        _followMouseMenuItem.IsChecked = _followMouse;
        _followMouseMenuItem.Header = _followMouse ? "Stop Following Mouse" : "Follow Mouse";
    }

    private void PlayPetAnimation()
    {
        var squash = new DoubleAnimation(1, 0.88, TimeSpan.FromMilliseconds(70))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, squash);
        ShowPetPopup();
    }

    private void ShowNextEmote()
    {
        var emotes = new[] { "♥", "!", "?", "✨", "👍" };
        _emoteIndex = (_emoteIndex + 1) % emotes.Length;
        ShowActionPopup(emotes[_emoteIndex], Colors.White);
    }

    private void PlayRandomAction()
    {
        var pick = Random.Shared.Next(4);
        switch (pick)
        {
            case 0:
                ShowActionPopup("👋", Colors.White);
                break;
            case 1:
                PlayDanceAnimation();
                break;
            case 2:
                ApplyTemporarySpeed(0, TimeSpan.FromSeconds(10));
                ShowActionPopup("Zzz", Colors.White);
                break;
            default:
                ShowNextEmote();
                break;
        }
    }

    private void PlayRandomSnack()
    {
        var pick = Random.Shared.Next(3);
        switch (pick)
        {
            case 0:
                PlayCookieAnimation();
                break;
            case 1:
                PlayCoffeeAnimation();
                break;
            default:
                PlayPizzaAnimation();
                break;
        }
    }

    private void StopCurrentAction()
    {
        ClearBuddyAnimations();
        if (_tempSpeedSaved is not null)
        {
            _settings.SpeedDipPerSecond = _tempSpeedSaved.Value;
            _tempSpeedSaved = null;
            _tempSpeedUntil = null;
        }

        ShowActionPopup("OK", Colors.White);
    }

    private void ShowActionPopup(string text, System.Windows.Media.Color color)
    {
        ActionText.Text = text;
        ActionText.Foreground = new SolidColorBrush(color);
        ActionText.Opacity = 1;
        ActionText.Margin = new Thickness(0, 0, 0, 6);
        ActionPopup.IsOpen = true;
        _actionCloseTimer.Stop();
        _actionCloseTimer.Start();

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1100));
        ActionText.BeginAnimation(OpacityProperty, fade);
        ActionText.BeginAnimation(TextBlock.MarginProperty, new ThicknessAnimation(ActionText.Margin, new Thickness(0, -10, 0, 6), TimeSpan.FromMilliseconds(280)));
    }

    private void PlayDanceAnimation()
    {
        ClearBuddyAnimations();
        var pulse = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(80))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(8),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        ShowActionPopup("✨", Colors.White);
    }

    private void PlayCookieAnimation()
    {
        if (_isDragging)
        {
            return;
        }

        ApplyTemporarySpeed(0, TimeSpan.FromSeconds(2));
        ClearBuddyAnimations();
        ShowActionPopup("🍪", Colors.White);

        var sb = new Storyboard();

        var chew = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(240),
            RepeatBehavior = new RepeatBehavior(4),
            FillBehavior = FillBehavior.Stop
        };
        chew.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        chew.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        chew.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        Storyboard.SetTarget(chew, _scaleTransform);
        Storyboard.SetTargetProperty(chew, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(chew);

        var wobble = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(240),
            RepeatBehavior = new RepeatBehavior(4),
            FillBehavior = FillBehavior.Stop
        };
        wobble.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        wobble.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        wobble.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        wobble.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        wobble.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        Storyboard.SetTarget(wobble, _rotateTransform);
        Storyboard.SetTargetProperty(wobble, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(wobble);

        StartCleanStoryboard(sb);
    }

    private void PlayCoffeeAnimation()
    {
        if (_isDragging)
        {
            return;
        }

        ApplyTemporarySpeed(_settings.SpeedDipPerSecond * 1.6, TimeSpan.FromSeconds(10));
        ClearBuddyAnimations();
        ShowActionPopup("☕", Colors.White);

        var sb = new Storyboard();

        var bounce = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(220),
            RepeatBehavior = new RepeatBehavior(10),
            FillBehavior = FillBehavior.Stop
        };
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        Storyboard.SetTarget(bounce, _translateTransform);
        Storyboard.SetTargetProperty(bounce, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(bounce);

        var tilt = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(220),
            RepeatBehavior = new RepeatBehavior(10),
            FillBehavior = FillBehavior.Stop
        };
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(55))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(165))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        Storyboard.SetTarget(tilt, _rotateTransform);
        Storyboard.SetTargetProperty(tilt, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(tilt);

        var pop = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(220),
            RepeatBehavior = new RepeatBehavior(6),
            FillBehavior = FillBehavior.Stop
        };
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        Storyboard.SetTarget(pop, _scaleTransform);
        Storyboard.SetTargetProperty(pop, new PropertyPath(ScaleTransform.ScaleXProperty));
        sb.Children.Add(pop);

        StartCleanStoryboard(sb);
    }

    private void PlayPizzaAnimation()
    {
        if (_isDragging)
        {
            return;
        }

        ApplyTemporarySpeed(0, TimeSpan.FromSeconds(4));
        ClearBuddyAnimations();
        ShowActionPopup("🍕", Colors.White);

        var sb = new Storyboard();

        var swellX = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(320),
            RepeatBehavior = new RepeatBehavior(6),
            FillBehavior = FillBehavior.Stop
        };
        swellX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        swellX.KeyFrames.Add(new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        swellX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        Storyboard.SetTarget(swellX, _scaleTransform);
        Storyboard.SetTargetProperty(swellX, new PropertyPath(ScaleTransform.ScaleXProperty));
        sb.Children.Add(swellX);

        var swellY = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(320),
            RepeatBehavior = new RepeatBehavior(6),
            FillBehavior = FillBehavior.Stop
        };
        swellY.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        swellY.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        swellY.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        Storyboard.SetTarget(swellY, _scaleTransform);
        Storyboard.SetTargetProperty(swellY, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(swellY);

        var sway = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(320),
            RepeatBehavior = new RepeatBehavior(6),
            FillBehavior = FillBehavior.Stop
        };
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        Storyboard.SetTarget(sway, _rotateTransform);
        Storyboard.SetTargetProperty(sway, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(sway);

        StartCleanStoryboard(sb);
    }

    private void BuddyImage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovering = true;
        SetGlow(true, (_isDragging ? Colors.DeepSkyBlue : Colors.White), 0.55);
        UpdateCursorForState();
    }

    private void BuddyImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovering = false;
        if (!_isDragging)
        {
            SetGlow(false, Colors.White, 0);
        }
        UpdateCursorForState();
    }

    private void SetGlow(bool enabled, System.Windows.Media.Color color, double opacity)
    {
        _glowEffect.Color = color;
        var to = enabled ? opacity : 0.0;
        var anim = new DoubleAnimation(_glowEffect.Opacity, to, TimeSpan.FromMilliseconds(120));
        _glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, anim);
    }

    private void UpdateCursorForState()
    {
        if (_isDragging)
        {
            BuddyImage.Cursor = System.Windows.Input.Cursors.SizeAll;
            return;
        }

        if (_isHovering && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            BuddyImage.Cursor = System.Windows.Input.Cursors.SizeAll;
            return;
        }

        BuddyImage.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private void ShowPetPopup()
    {
        PetText.Opacity = 1;
        PetText.Margin = new Thickness(0, 0, 0, 6);
        PetPopup.IsOpen = true;
        _petCloseTimer.Stop();
        _petCloseTimer.Start();

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900));

        PetText.BeginAnimation(OpacityProperty, fade);
        PetText.BeginAnimation(TextBlock.MarginProperty, new ThicknessAnimation(PetText.Margin, new Thickness(0, -10, 0, 6), TimeSpan.FromMilliseconds(300)));
    }

    private void PlayClickPulse()
    {
        var pulse = new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(60))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
    }

    private void ToggleFollowMouse()
    {
        if (_followMouse)
        {
            _followMouse = false;
            if (_followMouseSavedSpeed is not null)
            {
                _settings.SpeedDipPerSecond = _followMouseSavedSpeed.Value;
            }
            _followMouseSavedSpeed = null;
            ShowActionPopup("OK", Colors.White);
            UpdateFollowMouseMenuItem();
            return;
        }

        _followMouse = true;
        _followMouseSavedSpeed = _settings.SpeedDipPerSecond;
        _settings.SpeedDipPerSecond = 0;
        ShowActionPopup("👀", Colors.White);
        UpdateFollowMouseMenuItem();
    }

    private void ApplyTemporarySpeed(double newSpeed, TimeSpan duration)
    {
        if (_followMouse)
        {
            ToggleFollowMouse();
        }

        if (_tempSpeedSaved is null)
        {
            _tempSpeedSaved = _settings.SpeedDipPerSecond;
        }

        _settings.SpeedDipPerSecond = Math.Max(0, newSpeed);
        _tempSpeedUntil = DateTimeOffset.UtcNow.Add(duration);
    }

    private void ClearBuddyAnimations()
    {
        _activeStoryboard?.Stop(this);
        _activeStoryboard = null;

        _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        _translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _translateTransform.BeginAnimation(TranslateTransform.YProperty, null);

        _scaleTransform.ScaleX = 1;
        _scaleTransform.ScaleY = 1;
        _rotateTransform.Angle = 0;
        _translateTransform.X = 0;
        _translateTransform.Y = 0;
    }

    private void StartCleanStoryboard(Storyboard sb)
    {
        _activeStoryboard?.Stop(this);
        _activeStoryboard = sb;
        sb.FillBehavior = FillBehavior.Stop;
        sb.Completed += (_, _) =>
        {
            if (ReferenceEquals(_activeStoryboard, sb))
            {
                _activeStoryboard = null;
                ClearBuddyAnimations();
            }
        };
        sb.Begin(this, true);
    }

    private void UpdateTemporarySpeed()
    {
        if (_tempSpeedUntil is null || _tempSpeedSaved is null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow < _tempSpeedUntil.Value)
        {
            return;
        }

        _settings.SpeedDipPerSecond = _tempSpeedSaved.Value;
        _tempSpeedSaved = null;
        _tempSpeedUntil = null;
    }

    private double GetMouseXDip()
    {
        var p = System.Windows.Forms.Cursor.Position;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null)
        {
            return p.X;
        }

        var m = src.CompositionTarget.TransformFromDevice;
        return m.Transform(new System.Windows.Point(p.X, p.Y)).X;
    }
}

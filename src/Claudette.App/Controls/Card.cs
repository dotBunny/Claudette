using Avalonia;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Media;

namespace Claudette.App.Controls;

/// <summary>
/// One step of the conversation as a card (DESIGN.md §18, "Conversation cards"): a stripe down its left edge in its
/// kind's color over a faint tint of it, a header line with an icon, a label in that color, a short detail and the time
/// at the right, and the step under it. Its kind is a class (<c>you</c>, <c>claude</c>, <c>tools</c> and so on) that
/// sets the brushes, in ConversationStyles.axaml; <c>inline</c> puts the content on the header line, for a one-line
/// card, and <c>quiet</c> draws a step on the way softer until it's pointed at.
/// </summary>
public sealed class Card : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Card, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DetailProperty = AvaloniaProperty.Register<Card, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> TimeTextProperty = AvaloniaProperty.Register<Card, string?>(nameof(TimeText));

    public static readonly StyledProperty<string?> TimeTipProperty = AvaloniaProperty.Register<Card, string?>(nameof(TimeTip));

    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<Card, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<Card, object?>(nameof(Actions));

    public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> ActionsTemplateProperty =
        AvaloniaProperty.Register<Card, Avalonia.Controls.Templates.IDataTemplate?>(nameof(ActionsTemplate));

    public static readonly StyledProperty<IBrush?> StripeBrushProperty = AvaloniaProperty.Register<Card, IBrush?>(nameof(StripeBrush));

    public static readonly StyledProperty<IBrush?> TintBrushProperty = AvaloniaProperty.Register<Card, IBrush?>(nameof(TintBrush));

    public static readonly StyledProperty<IBrush?> OutlineBrushProperty = AvaloniaProperty.Register<Card, IBrush?>(nameof(OutlineBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty = AvaloniaProperty.Register<Card, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<bool> IsCollapsibleProperty = AvaloniaProperty.Register<Card, bool>(nameof(IsCollapsible));

    public static readonly StyledProperty<bool> IsCollapsedProperty = AvaloniaProperty.Register<Card, bool>(nameof(IsCollapsed));

    public static readonly StyledProperty<ICommand?> ToggleCommandProperty = AvaloniaProperty.Register<Card, ICommand?>(nameof(ToggleCommand));

    static Card()
    {
        IsCollapsedProperty.Changed.AddClassHandler<Card>((card, e) => card.PseudoClasses.Set(":collapsed", e.GetNewValue<bool>()));
        LabelProperty.Changed.AddClassHandler<Card>((card, _) => card.UpdateHeaderless());
        IconProperty.Changed.AddClassHandler<Card>((card, _) => card.UpdateHeaderless());
        TimeTextProperty.Changed.AddClassHandler<Card>((card, _) => card.UpdateHeaderless());
        DetailProperty.Changed.AddClassHandler<Card>((card, _) => card.UpdateHeaderless());
        ActionsProperty.Changed.AddClassHandler<Card>((card, _) => card.UpdateHeaderless());
    }

    public Card() => UpdateHeaderless();

    /// <summary>What the step is: "You", "Claude", "Tool calls".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>A few muted words after the label: "4 calls · 1 running", a subagent's task.</summary>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>When it happened, short, at the header's right.</summary>
    public string? TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    /// <summary>The full date and time, as the time's tooltip.</summary>
    public string? TimeTip
    {
        get => GetValue(TimeTipProperty);
        set => SetValue(TimeTipProperty, value);
    }

    /// <summary>A 12×12 stroked icon before the label, from App.axaml.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Buttons before the time, such as Copy, which show while the card is pointed at or has the focus.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>How <see cref="Actions"/> shows, when it's an item rather than a control.</summary>
    public Avalonia.Controls.Templates.IDataTemplate? ActionsTemplate
    {
        get => GetValue(ActionsTemplateProperty);
        set => SetValue(ActionsTemplateProperty, value);
    }

    /// <summary>The stripe down the left edge, and the label's color unless <see cref="LabelBrush"/> says otherwise.</summary>
    public IBrush? StripeBrush
    {
        get => GetValue(StripeBrushProperty);
        set => SetValue(StripeBrushProperty, value);
    }

    /// <summary>The faint tint behind the card.</summary>
    public IBrush? TintBrush
    {
        get => GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    /// <summary>The thin line around the card.</summary>
    public IBrush? OutlineBrush
    {
        get => GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    /// <summary>The label's and icon's color: the kind's color, darker or lighter so it reads as text.</summary>
    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>A chevron before the header's icon opens and closes the card's content (a Tool calls card's tiles).</summary>
    public bool IsCollapsible
    {
        get => GetValue(IsCollapsibleProperty);
        set => SetValue(IsCollapsibleProperty, value);
    }

    /// <summary>The content is folded away, leaving the header line; the chevron points right instead of down.</summary>
    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>What the chevron does.</summary>
    public ICommand? ToggleCommand
    {
        get => GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    /// <summary>Nothing to put on a header line: the content starts at the top.</summary>
    private void UpdateHeaderless() =>
        PseudoClasses.Set(":headerless", Label is null && Icon is null && TimeText is null && Detail is null && Actions is null);
}

using System.ComponentModel;
using Sectorbound.Engine;

namespace Sectorbound.Client;

public sealed class StartMenuForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 16, 24);
    private static readonly Color PanelDark = Color.FromArgb(23, 29, 42);
    private static readonly Color PanelDarker = Color.FromArgb(18, 23, 34);
    private static readonly Color Separator = Color.FromArgb(40, 52, 76);
    private static readonly Color TextCream = Color.FromArgb(245, 238, 219);
    private static readonly Color TextMuted = Color.FromArgb(174, 186, 205);
    private static readonly Color TextLight = Color.FromArgb(212, 221, 236);
    private static readonly Color BtnNeutral = Color.FromArgb(32, 42, 62);
    private static readonly Color BtnNeutralHv = Color.FromArgb(48, 62, 92);
    private static readonly Color BtnGreen = Color.FromArgb(30, 82, 56);
    private static readonly Color BtnGreenHv = Color.FromArgb(42, 112, 76);
    private static readonly Color BtnRed = Color.FromArgb(80, 30, 30);
    private static readonly Color BtnRedHv = Color.FromArgb(110, 42, 42);
    private static readonly Color BtnDisabled = Color.FromArgb(26, 32, 46);
    private static readonly Color TextDisabled = Color.FromArgb(80, 92, 112);

    private const int FormWidth = 1040;
    private const int FormHeight = 680;
    private const int LeftPanelWidth = 356;
    private const int SettingsPanelHeight = 220;

    private readonly IReadOnlyList<DifficultyDef> _difficulties;
    private readonly ClientSettings _settings;
    private readonly string _settingsPath;
    private readonly string _savePath;
    private readonly bool _hasSave;
    private readonly Dictionary<string, Button> _bodyButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _hairButtons = new(StringComparer.OrdinalIgnoreCase);

    private CharacterPreviewControl _previewControl = null!;
    private Label _characterSummaryLabel = null!;
    private ComboBox _difficultyCombo = null!;
    private Label _difficultyDetailLabel = null!;
    private Panel _settingsPanel = null!;
    private bool _settingsVisible;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShouldStartGame { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool StartNewGame { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedDifficultyPresetId { get; private set; } = ClientSettings.DefaultDifficultyPresetId;

    public StartMenuForm()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
        var content = ContentLoader.LoadFromDirectory(contentRoot);
        _difficulties = content.Difficulties.Values
            .OrderBy(x => x.InventoryDropPercent)
            .ThenBy(x => x.SkillPenaltyPercent)
            .ThenBy(x => x.CorpseRun)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _settingsPath = ClientSettings.GetDefaultPath();
        _settings = ClientSettings.Load(_settingsPath);
        SelectedDifficultyPresetId = ResolveDifficultyPresetId(_settings.GetPreferredDifficultyPresetId());
        _settings.SetPreferredDifficultyPreset(SelectedDifficultyPresetId);

        var saveRoot = SectorboundUserData.GetSaveRoot();
        _savePath = GameSession.GetSavePath(saveRoot, "sectorbound");
        _hasSave = File.Exists(_savePath);

        Text = "Sectorbound";
        ClientSize = new Size(FormWidth, FormHeight);
        MinimumSize = new Size(FormWidth, FormHeight);
        MaximumSize = new Size(FormWidth, FormHeight);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;

        BuildUi();
        UpdateCharacterUi();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Background,
            Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftPanelWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        Controls.Add(layout);

        layout.Controls.Add(BuildMenuPanel(), 0, 0);
        layout.Controls.Add(BuildCharacterPanel(), 1, 0);
    }

    private Control BuildMenuPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelDark,
            Padding = new Padding(24, 26, 24, 24)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.Controls.Add(stack);

        stack.Controls.Add(new Label
        {
            Height = 56,
            Width = LeftPanelWidth - 48,
            Margin = Padding.Empty,
            Text = "SECTORBOUND",
            Font = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = TextCream,
            TextAlign = ContentAlignment.MiddleLeft
        });

        stack.Controls.Add(new Label
        {
            Height = 26,
            Width = LeftPanelWidth - 48,
            Margin = Padding.Empty,
            Text = "a voxel survival adventure",
            Font = new Font("Segoe UI", 11f, FontStyle.Italic, GraphicsUnit.Point),
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        });

        stack.Controls.Add(new Panel
        {
            Height = 1,
            Width = LeftPanelWidth - 48,
            Margin = new Padding(0, 14, 0, 18),
            BackColor = Separator
        });

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 18, 0, 0),
            Width = LeftPanelWidth - 48
        };

        var continueButton = MakeButton("Continue", BtnGreen, BtnGreenHv);
        continueButton.Size = new Size(280, 44);
        if (_hasSave)
        {
            continueButton.Click += (_, _) => StartGame(newGame: false);
        }
        else
        {
            continueButton.Enabled = false;
            continueButton.BackColor = BtnDisabled;
            continueButton.FlatAppearance.MouseOverBackColor = BtnDisabled;
            continueButton.ForeColor = TextDisabled;
        }
        buttonsPanel.Controls.Add(continueButton);

        var newGameButton = MakeButton("New Game", BtnNeutral, BtnNeutralHv);
        newGameButton.Size = new Size(280, 44);
        newGameButton.Click += (_, _) => StartGame(newGame: true);
        buttonsPanel.Controls.Add(newGameButton);

        var settingsButton = MakeButton("Settings", BtnNeutral, BtnNeutralHv);
        settingsButton.Size = new Size(280, 44);
        settingsButton.Click += (_, _) => ToggleSettings();
        buttonsPanel.Controls.Add(settingsButton);

        var quitButton = MakeButton("Quit", BtnRed, BtnRedHv);
        quitButton.Size = new Size(280, 44);
        quitButton.Click += (_, _) => Close();
        buttonsPanel.Controls.Add(quitButton);
        stack.Controls.Add(buttonsPanel);

        stack.Controls.Add(new Label
        {
            Height = 56,
            Width = LeftPanelWidth - 48,
            Padding = new Padding(0, 18, 0, 0),
            Text = _hasSave
                ? "Save file found. Continue resumes the world as-is. New Game uses the selected difficulty, makes a timestamped backup, and then replaces the current save slot."
                : "No save file found. Pick a difficulty, then start a new run. Character changes save immediately.",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(108, 122, 146),
            TextAlign = ContentAlignment.MiddleLeft
        });

        stack.Controls.Add(BuildDifficultyPanel());

        _settingsPanel = BuildSettingsPanel();
        _settingsPanel.Dock = DockStyle.Bottom;
        _settingsPanel.Visible = false;
        panel.Controls.Add(_settingsPanel);

        return panel;
    }

    private Control BuildCharacterPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelDarker,
            Padding = new Padding(28)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Character Creator",
            Font = new Font("Segoe UI Semibold", 22f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = TextCream,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "A first pass using the imported base character pack. Pick a body frame and hair style; the runtime player model updates from this selection.",
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 18, 0, 0)
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380f));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.Controls.Add(contentLayout, 0, 2);

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelDark,
            Padding = new Padding(18)
        };
        _previewControl = new CharacterPreviewControl
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        previewPanel.Controls.Add(_previewControl);
        contentLayout.Controls.Add(previewPanel, 0, 0);

        var optionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 8, 0, 0),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 158f));
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 158f));
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
        contentLayout.Controls.Add(optionsPanel, 1, 0);

        _characterSummaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextLight,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };
        optionsPanel.Controls.Add(_characterSummaryLabel, 0, 0);

        var hairSection = BuildOptionSection(
            "Hairstyle",
            CharacterCatalog.Hairstyles,
            _hairButtons,
            selection => SaveCharacterSelection(new CharacterSelection(_settings.GetCharacterSelection().BodyId, selection.Id)));
        hairSection.Dock = DockStyle.Fill;
        optionsPanel.Controls.Add(hairSection, 0, 2);

        var bodySection = BuildOptionSection(
            "Body Frame",
            CharacterCatalog.Bodies,
            _bodyButtons,
            selection => SaveCharacterSelection(new CharacterSelection(selection.Id, _settings.GetCharacterSelection().HairId)));
        bodySection.Dock = DockStyle.Fill;
        optionsPanel.Controls.Add(bodySection, 0, 1);

        optionsPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "This is the start of the system: menu-side preview, saved selection, and runtime player swapping. More sliders and color controls can sit on top of this.",
            ForeColor = Color.FromArgb(140, 156, 181),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 4);

        return panel;
    }

    private Panel BuildOptionSection(
        string title,
        IReadOnlyList<CharacterOption> options,
        Dictionary<string, Button> buttonMap,
        Action<CharacterOption> onSelected)
    {
        var section = new Panel
        {
            Height = 158,
            Padding = new Padding(0, 10, 0, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        section.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = TextCream,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };

        foreach (var option in options)
        {
            var button = MakeButton(option.DisplayName, BtnNeutral, BtnNeutralHv);
            button.Size = new Size(138, 40);
            button.Click += (_, _) => onSelected(option);
            buttons.Controls.Add(button);
            buttonMap[option.Id] = button;
        }

        layout.Controls.Add(buttons, 0, 1);
        return section;
    }

    private Control BuildDifficultyPanel()
    {
        var panel = new Panel
        {
            Width = LeftPanelWidth - 48,
            Height = 126,
            Margin = new Padding(0, 6, 0, 0),
            Padding = new Padding(14, 12, 14, 12),
            BackColor = Color.FromArgb(20, 26, 38)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "New Game Difficulty",
            ForeColor = TextCream,
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _difficultyCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelDark,
            ForeColor = TextLight,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point)
        };
        _difficultyCombo.DisplayMember = nameof(DifficultyDef.DisplayName);
        foreach (var difficulty in _difficulties)
        {
            _difficultyCombo.Items.Add(difficulty);
        }

        var selectedDifficulty = _difficulties.FirstOrDefault(difficulty =>
            string.Equals(difficulty.Id, SelectedDifficultyPresetId, StringComparison.OrdinalIgnoreCase));
        if (selectedDifficulty is not null)
        {
            _difficultyCombo.SelectedItem = selectedDifficulty;
        }

        _difficultyCombo.SelectedIndexChanged += (_, _) => OnDifficultySelectionChanged();
        layout.Controls.Add(_difficultyCombo, 0, 1);

        _difficultyDetailLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.TopLeft
        };
        layout.Controls.Add(_difficultyDetailLabel, 0, 2);

        UpdateDifficultyDetail();
        return panel;
    }

    private Panel BuildSettingsPanel()
    {
        var panel = new Panel
        {
            Height = SettingsPanelHeight,
            BackColor = Color.FromArgb(20, 26, 38),
            Padding = new Padding(14, 12, 14, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        panel.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Settings",
            ForeColor = TextCream,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var invertCheck = new CheckBox
        {
            Dock = DockStyle.Fill,
            Text = "Invert Mouse Y",
            Checked = _settings.InvertMouseY,
            ForeColor = TextLight,
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.Transparent
        };
        invertCheck.CheckedChanged += (_, _) =>
        {
            _settings.InvertMouseY = invertCheck.Checked;
            _settings.TrySave(_settingsPath);
        };
        layout.Controls.Add(invertCheck, 0, 1);

        var sensitivityLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = SensitivityText(_settings.MouseSensitivity),
            ForeColor = TextLight,
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(sensitivityLabel, 0, 2);

        var sensitivityBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = SensitivityToTick(ClientSettings.MaxMouseSensitivity),
            Value = SensitivityToTick(_settings.MouseSensitivity),
            TickFrequency = 1,
            BackColor = panel.BackColor
        };
        sensitivityBar.ValueChanged += (_, _) =>
        {
            _settings.MouseSensitivity = TickToSensitivity(sensitivityBar.Value);
            sensitivityLabel.Text = SensitivityText(_settings.MouseSensitivity);
            _settings.TrySave(_settingsPath);
        };
        layout.Controls.Add(sensitivityBar, 0, 3);

        var viewLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = ViewDistanceText(_settings.ViewDistanceBlocks),
            ForeColor = TextLight,
            Font = new Font("Segoe UI", 10f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(viewLabel, 0, 4);

        var viewBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = ClientSettings.MinViewDistanceBlocks,
            Maximum = ClientSettings.MaxViewDistanceBlocks,
            Value = _settings.ViewDistanceBlocks,
            TickFrequency = 8,
            BackColor = panel.BackColor
        };
        viewBar.ValueChanged += (_, _) =>
        {
            _settings.ViewDistanceBlocks = viewBar.Value;
            viewLabel.Text = ViewDistanceText(_settings.ViewDistanceBlocks);
            _settings.TrySave(_settingsPath);
        };
        layout.Controls.Add(viewBar, 0, 5);

        return panel;
    }

    private void StartGame(bool newGame)
    {
        if (newGame && _hasSave)
        {
            var confirmation = MessageBox.Show(
                this,
                "Starting a new game will replace the current save slot.\n\nA timestamped backup of the existing save will be created first.\n\nContinue?",
                "Confirm New Game",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            if (!TryBackupExistingSave())
            {
                return;
            }
        }

        _settings.TrySave(_settingsPath);
        ShouldStartGame = true;
        StartNewGame = newGame;
        Close();
    }

    private void ToggleSettings()
    {
        _settingsVisible = !_settingsVisible;
        _settingsPanel.Visible = _settingsVisible;
    }

    private void SaveCharacterSelection(CharacterSelection selection)
    {
        _settings.SetCharacterSelection(selection);
        _settings.TrySave(_settingsPath);
        UpdateCharacterUi();
    }

    private void OnDifficultySelectionChanged()
    {
        if (_difficultyCombo.SelectedItem is not DifficultyDef difficulty)
        {
            return;
        }

        SelectedDifficultyPresetId = difficulty.Id;
        _settings.SetPreferredDifficultyPreset(difficulty.Id);
        _settings.TrySave(_settingsPath);
        UpdateDifficultyDetail();
    }

    private void UpdateCharacterUi()
    {
        var selection = _settings.GetCharacterSelection();
        _characterSummaryLabel.Text = $"{CharacterCatalog.GetDisplayName(selection)}{Environment.NewLine}Selection saves immediately and becomes the runtime player model.";
        _previewControl.Asset = ImportedVoxelAssets.GetCharacter(selection);

        UpdateOptionButtons(_bodyButtons, selection.BodyId);
        UpdateOptionButtons(_hairButtons, selection.HairId);
    }

    private static void UpdateOptionButtons(Dictionary<string, Button> buttons, string selectedId)
    {
        foreach (var (id, button) in buttons)
        {
            var isSelected = string.Equals(id, selectedId, StringComparison.OrdinalIgnoreCase);
            ApplyButtonPalette(button, isSelected ? BtnGreen : BtnNeutral, isSelected ? BtnGreenHv : BtnNeutralHv);
        }
    }

    private static Button MakeButton(string text, Color background, Color hover)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = background,
            ForeColor = TextCream,
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, 10)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(
            Math.Max(0, background.R - 18),
            Math.Max(0, background.G - 18),
            Math.Max(0, background.B - 18));
        return button;
    }

    private static void ApplyButtonPalette(Button button, Color background, Color hover)
    {
        button.BackColor = background;
        button.ForeColor = TextCream;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(
            Math.Max(0, background.R - 18),
            Math.Max(0, background.G - 18),
            Math.Max(0, background.B - 18));
    }

    private static int SensitivityToTick(float value) =>
        (int)Math.Round((value - ClientSettings.MinMouseSensitivity) / ClientSettings.MouseSensitivityStep);

    private static float TickToSensitivity(int tick) =>
        ClientSettings.MinMouseSensitivity + (tick * ClientSettings.MouseSensitivityStep);

    private static string SensitivityText(float value) => $"Mouse Sensitivity: {value:F3}";

    private static string ViewDistanceText(int value) => $"View Distance: {value} blocks";

    private bool TryBackupExistingSave()
    {
        if (!File.Exists(_savePath))
        {
            return true;
        }

        try
        {
            var saveDirectory = Path.GetDirectoryName(_savePath)
                ?? throw new InvalidOperationException("Save directory is unavailable.");
            var backupDirectory = Path.Combine(saveDirectory, "Backups");
            Directory.CreateDirectory(backupDirectory);

            var backupName = $"{Path.GetFileNameWithoutExtension(_savePath)}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
            var backupPath = Path.Combine(backupDirectory, backupName);
            File.Copy(_savePath, backupPath, overwrite: false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(
                this,
                $"The current save could not be backed up, so New Game was canceled.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Backup Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    private string ResolveDifficultyPresetId(string? requestedId)
    {
        var normalized = ClientSettings.NormalizeDifficultyPresetId(requestedId);
        if (_difficulties.Any(difficulty => string.Equals(difficulty.Id, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        if (_difficulties.Any(difficulty => string.Equals(difficulty.Id, ClientSettings.DefaultDifficultyPresetId, StringComparison.OrdinalIgnoreCase)))
        {
            return ClientSettings.DefaultDifficultyPresetId;
        }

        return _difficulties.FirstOrDefault()?.Id ?? ClientSettings.DefaultDifficultyPresetId;
    }

    private void UpdateDifficultyDetail()
    {
        if (_difficultyDetailLabel is null)
        {
            return;
        }

        if (_difficultyCombo.SelectedItem is DifficultyDef difficulty)
        {
            _difficultyDetailLabel.Text = DescribeDifficulty(difficulty);
            return;
        }

        _difficultyDetailLabel.Text = "Difficulty presets are unavailable right now.";
    }

    private static string DescribeDifficulty(DifficultyDef difficulty)
    {
        var skillPenalty = difficulty.SkillPenaltyPercent <= 0
            ? "no skill XP loss"
            : $"{difficulty.SkillPenaltyPercent:P0} skill XP loss";
        var corpseRun = difficulty.CorpseRun ? "corpse run on" : "corpse run off";
        return $"Death cost: {difficulty.InventoryDropPercent:P0} inventory loss, {skillPenalty}, {corpseRun}.";
    }
}

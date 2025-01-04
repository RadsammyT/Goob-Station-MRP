using System.Linq;
using System.Numerics;
using Content.Shared.Atmos;
using Content.Client.UserInterface.Controls;
using Content.Shared._Shitmed.Targeting; // Shitmed
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.HealthAnalyzer.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class HealthAnalyzerWindow : FancyWindow
    {
        private readonly IEntityManager _entityManager;
        private readonly SpriteSystem _spriteSystem;
        private readonly IPrototypeManager _prototypes;
        private readonly IResourceCache _cache;

        // Shitmed Change Start
        public event Action<TargetBodyPart?, EntityUid>? OnBodyPartSelected;
        private EntityUid _spriteViewEntity;

        [ValidatePrototypeId<EntityPrototype>]
        private readonly EntProtoId _bodyView = "AlertSpriteView";

        private readonly Dictionary<TargetBodyPart, TextureButton> _bodyPartControls;
        private EntityUid? _target;
        // Shitmed Change End

        public HealthAnalyzerWindow()
        {
            RobustXamlLoader.Load(this);

            var dependencies = IoCManager.Instance!;
            _entityManager = dependencies.Resolve<IEntityManager>();
            _spriteSystem = _entityManager.System<SpriteSystem>();
            _prototypes = dependencies.Resolve<IPrototypeManager>();
            _cache = dependencies.Resolve<IResourceCache>();
            // Shitmed Change Start
            _bodyPartControls = new Dictionary<TargetBodyPart, TextureButton>
            {
                { TargetBodyPart.Head, HeadButton },
                { TargetBodyPart.Torso, ChestButton },
                { TargetBodyPart.Groin, GroinButton },
                { TargetBodyPart.LeftArm, LeftArmButton },
                { TargetBodyPart.LeftHand, LeftHandButton },
                { TargetBodyPart.RightArm, RightArmButton },
                { TargetBodyPart.RightHand, RightHandButton },
                { TargetBodyPart.LeftLeg, LeftLegButton },
                { TargetBodyPart.LeftFoot, LeftFootButton },
                { TargetBodyPart.RightLeg, RightLegButton },
                { TargetBodyPart.RightFoot, RightFootButton },
            };

            foreach (var bodyPartButton in _bodyPartControls)
            {
                bodyPartButton.Value.MouseFilter = MouseFilterMode.Stop;
                bodyPartButton.Value.OnPressed += _ => SetActiveBodyPart(bodyPartButton.Key, bodyPartButton.Value);
            }
            ReturnButton.OnPressed += _ => ResetBodyPart();
            // Shitmed Change End
        }

        // Shitmed Change Start
        public void SetActiveBodyPart(TargetBodyPart part, TextureButton button)
        {
            if (_target == null)
                return;

            // Bit of the ole shitcode until we have Groins in the prototypes.
            OnBodyPartSelected?.Invoke(part == TargetBodyPart.Groin ? TargetBodyPart.Torso : part, _target.Value);
        }

        public void ResetBodyPart()
        {
            if (_target == null)
                return;

            OnBodyPartSelected?.Invoke(null, _target.Value);
        }

        public void SetActiveButtons(bool isHumanoid)
        {
            foreach (var button in _bodyPartControls)
                button.Value.Visible = isHumanoid;
        }

        // Not all of this function got messed with, but it was spread enough to warrant being covered entirely by a Shitmed Change
        public void Populate(HealthAnalyzerScannedUserMessage msg)
        {
            // Start-Shitmed
            _target = _entityManager.GetEntity(msg.TargetEntity);
            EntityUid? part = msg.Part != null ? _entityManager.GetEntity(msg.Part.Value) : null;
            var isPart = part != null;

            if (_target == null
                || !_entityManager.TryGetComponent<DamageableComponent>(isPart ? part : _target, out var damageable))
            {
                NoPatientDataText.Visible = true;
                return;
            }

            SetActiveButtons(_entityManager.HasComponent<TargetingComponent>(_target.Value));

            ReturnButton.Visible = isPart;
            PartNameLabel.Visible = isPart;

            if (part != null)
                PartNameLabel.Text = _entityManager.HasComponent<MetaDataComponent>(part)
                    ? Identity.Name(part.Value, _entityManager)
                    : Loc.GetString("health-analyzer-window-entity-unknown-value-text");

            NoPatientDataText.Visible = false;

            // Scan Mode

            ScanModeLabel.Text = msg.ScanMode.HasValue
                ? msg.ScanMode.Value
                    ? Loc.GetString("health-analyzer-window-scan-mode-active")
                    : Loc.GetString("health-analyzer-window-scan-mode-inactive")
                : Loc.GetString("health-analyzer-window-entity-unknown-text");

            ScanModeLabel.FontColorOverride = msg.ScanMode.HasValue && msg.ScanMode.Value ? Color.Green : Color.Red;

            // Patient Information

            SpriteView.SetEntity(SetupIcon(msg.Body) ?? _target.Value);
            SpriteView.Visible = msg.ScanMode.HasValue && msg.ScanMode.Value;
            PartView.Visible = SpriteView.Visible;
            NoDataTex.Visible = !SpriteView.Visible;

            var name = new FormattedMessage();
            name.PushColor(Color.White);
            name.AddText(_entityManager.HasComponent<MetaDataComponent>(_target.Value)
                ? Identity.Name(_target.Value, _entityManager)
                : Loc.GetString("health-analyzer-window-entity-unknown-text"));
            NameLabel.SetMessage(name);

            SpeciesLabel.Text =
                _entityManager.TryGetComponent<HumanoidAppearanceComponent>(_target.Value,
                    out var humanoidAppearanceComponent)
                    ? Loc.GetString(_prototypes.Index<SpeciesPrototype>(humanoidAppearanceComponent.Species).Name)
                    : Loc.GetString("health-analyzer-window-entity-unknown-species-text");

            // Basic Diagnostic

            TemperatureLabel.Text = !float.IsNaN(msg.Temperature)
                ? $"{msg.Temperature - Atmospherics.T0C:F1} °C ({msg.Temperature:F1} K)"
                : Loc.GetString("health-analyzer-window-entity-unknown-value-text");

            BloodLabel.Text = !float.IsNaN(msg.BloodLevel)
                ? $"{msg.BloodLevel * 100:F1} %"
                : Loc.GetString("health-analyzer-window-entity-unknown-value-text");

            StatusLabel.Text =
                _entityManager.TryGetComponent<MobStateComponent>(_target.Value, out var mobStateComponent)
                    ? GetStatus(mobStateComponent.CurrentState)
                    : Loc.GetString("health-analyzer-window-entity-unknown-text");

            // Total Damage

            DamageLabel.Text = damageable.TotalDamage.ToString();

            // Alerts

            var showAlerts = msg.Unrevivable == true || msg.Bleeding == true;

            AlertsDivider.Visible = showAlerts;
            AlertsContainer.Visible = showAlerts;

            if (showAlerts)
                AlertsContainer.DisposeAllChildren();

            if (msg.Unrevivable == true)
                AlertsContainer.AddChild(new RichTextLabel
                {
                    Text = Loc.GetString("health-analyzer-window-entity-unrevivable-text"),
                    Margin = new Thickness(0, 4),
                    MaxWidth = 300
                });

            if (msg.Bleeding == true)
                AlertsContainer.AddChild(new RichTextLabel
                {
                    Text = Loc.GetString("health-analyzer-window-entity-bleeding-text"),
                    Margin = new Thickness(0, 4),
                    MaxWidth = 300
                });

            // Damage Groups

            var damageSortedGroups =
                damageable.DamagePerGroup.OrderByDescending(damage => damage.Value)
                    .ToDictionary(x => x.Key, x => x.Value);

            IReadOnlyDictionary<string, FixedPoint2> damagePerType = damageable.Damage.DamageDict;

            DrawDiagnosticGroups(damageSortedGroups, damagePerType);
        }
        // Shitmed Change End
        private static string GetStatus(MobState mobState)
        {
            return mobState switch
            {
                MobState.Alive => Loc.GetString("health-analyzer-window-entity-alive-text"),
                MobState.Critical => Loc.GetString("health-analyzer-window-entity-critical-text"),
                MobState.Dead => Loc.GetString("health-analyzer-window-entity-dead-text"),
                _ => Loc.GetString("health-analyzer-window-entity-unknown-text"),
            };
        }

        private void DrawDiagnosticGroups(
            Dictionary<string, FixedPoint2> groups,
            IReadOnlyDictionary<string, FixedPoint2> damageDict)
        {
            GroupsContainer.RemoveAllChildren();

            foreach (var (damageGroupId, damageAmount) in groups)
            {
                if (damageAmount == 0)
                    continue;

                var groupTitleText = $"{Loc.GetString(
                    "health-analyzer-window-damage-group-text",
                    ("damageGroup", _prototypes.Index<DamageGroupPrototype>(damageGroupId).LocalizedName),
                    ("amount", damageAmount)
                )}";

                var groupContainer = new BoxContainer
                {
                    Align = BoxContainer.AlignMode.Begin,
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                };

                groupContainer.AddChild(CreateDiagnosticGroupTitle(groupTitleText, damageGroupId));

                GroupsContainer.AddChild(groupContainer);

                // Show the damage for each type in that group.
                var group = _prototypes.Index<DamageGroupPrototype>(damageGroupId);

                foreach (var type in group.DamageTypes)
                {
                    if (!damageDict.TryGetValue(type, out var typeAmount) || typeAmount <= 0)
                        continue;

                    var damageString = Loc.GetString(
                        "health-analyzer-window-damage-type-text",
                        ("damageType", _prototypes.Index<DamageTypePrototype>(type).LocalizedName),
                        ("amount", typeAmount)
                    );

                    groupContainer.AddChild(CreateDiagnosticItemLabel(damageString.Insert(0, " · ")));
                }
            }
        }

        private Texture GetTexture(string texture)
        {
            var rsiPath = new ResPath("/Textures/Objects/Devices/health_analyzer.rsi");
            var rsiSprite = new SpriteSpecifier.Rsi(rsiPath, texture);

            var rsi = _cache.GetResource<RSIResource>(rsiSprite.RsiPath).RSI;
            if (!rsi.TryGetState(rsiSprite.RsiState, out var state))
            {
                rsiSprite = new SpriteSpecifier.Rsi(rsiPath, "unknown");
            }

            return _spriteSystem.Frame0(rsiSprite);
        }

        private static Label CreateDiagnosticItemLabel(string text)
        {
            return new Label
            {
                Text = text,
            };
        }

        private BoxContainer CreateDiagnosticGroupTitle(string text, string id)
        {
            var rootContainer = new BoxContainer
            {
                Margin = new Thickness(0, 6, 0, 0),
                VerticalAlignment = VAlignment.Bottom,
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
            };

            rootContainer.AddChild(new TextureRect
            {
                SetSize = new Vector2(30, 30),
                Texture = GetTexture(id.ToLower())
            });

            rootContainer.AddChild(CreateDiagnosticItemLabel(text));

            return rootContainer;
        }

        // Shitmed Change Start
        /// <summary>
        /// Sets up the Body Doll using Alert Entity to use in Health Analyzer.
        /// </summary>
        private EntityUid? SetupIcon(Dictionary<TargetBodyPart, TargetIntegrity>? body)
        {
            if (body is null)
                return null;

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);

            _spriteViewEntity = _entityManager.Spawn(_bodyView);

            if (!_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
                return null;

            int layer = 0;
            foreach (var (bodyPart, integrity) in body)
            {
                // TODO: PartStatusUIController and make it use layers instead of TextureRects when EE refactors alerts.
                string enumName = Enum.GetName(typeof(TargetBodyPart), bodyPart) ?? "Unknown";
                int enumValue = (int) integrity;
                var rsi = new SpriteSpecifier.Rsi(new ResPath($"/Textures/_Shitmed/Interface/Targeting/Status/{enumName.ToLowerInvariant()}.rsi"), $"{enumName.ToLowerInvariant()}_{enumValue}");
                // Shitcode with love from Russia :)
                if (!sprite.TryGetLayer(layer, out _))
                    sprite.AddLayer(_spriteSystem.Frame0(rsi));
                else
                    sprite.LayerSetTexture(layer, _spriteSystem.Frame0(rsi));
                sprite.LayerSetScale(layer, new Vector2(3f, 3f));
                layer++;
            }
            return _spriteViewEntity;
        }
        // Shitmed Change End
    }
}

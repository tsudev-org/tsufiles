using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TsuFiles.App.Controls;
using TsuFiles.App.Services;
using TsuFiles.App.ViewModels;
using TsuFiles.App.ViewModels.Pages;
using TsuFiles.App.Views;
using TsuFiles.Core.Formats;
using TsuFiles.Core.Presets;
using TsuFiles.Core.Profiles;
using TsuFiles.Core.Settings;

namespace TsuFiles.App.Tests;

[Collection("Motion")]
public class FieldSwitchTests
{
    private sealed class Fixture : IDisposable
    {
        public UserDataStore Store { get; }
        public LocalizationService Loc { get; }
        public RadialViewModel Vm { get; }
        public RadialWindow Window { get; }
        private readonly List<string> _off;
        private readonly List<Preset> _presets;
        private readonly List<(ToolbarProfile Profile, List<string> Front, List<string>? Back)> _faces;

        public Fixture()
        {
            TestServices.EnsureInitialized("en");
            Store = AppHost.Get<UserDataStore>();
            Loc = AppHost.Get<LocalizationService>();
            _off = [.. Store.Settings.TurnedOffFields];
            _presets = [.. Store.Presets];
            _faces = Store.Profiles.Select(p => (p, p.PresetIds.ToList(), p.BackPresetIds?.ToList())).ToList();
            Store.Settings.TurnedOffFields = [];
            Vm = new RadialViewModel(Store, Loc, AppHost.Get<PresetAvailability>());
            Window = new RadialWindow { DataContext = Vm };
            Window.Show();
        }

        public ToolbarProfile Profile(params string[] files) => Assert.IsType<ToolbarProfile>(ProfileSelector.Select(Store.Profiles, files));

        public Preset Add(string format)
        {
            var preset = new Preset { Id = $"test-field-{format}", Name = format, TargetFormat = format };
            Store.Presets.Add(preset);
            return preset;
        }

        public Preset Group(string name, params string[] children)
        {
            var preset = new Preset { Id = $"test-field-{name}", Name = name, Kind = PresetKind.Group, SubPresetIds = [.. children] };
            Store.Presets.Add(preset);
            return preset;
        }

        public void Load(params string[] files)
        {
            Vm.Load(files, clickMode: true);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(Vm.Items, Window.DonutControl.Items);
        }

        public void Face(IEnumerable<string> ids)
        {
            Dispatcher.UIThread.RunJobs();
            var expected = ids.ToArray();
            Assert.Equal(expected, Vm.Presets.Select(p => p.Id));
            Assert.Equal(expected, Vm.Items.Select(p => p.Id));
            Assert.Equal(expected, Window.DonutControl.Items!.Select(p => p.Id));
        }

        public void Dispose()
        {
            Vm.ResetForDrag();
            Dispatcher.UIThread.RunJobs();
            Window.Close();
            Store.Settings.TurnedOffFields = _off;
            Store.Presets.Clear();
            Store.Presets.AddRange(_presets);
            foreach (var (profile, front, back) in _faces)
                (profile.PresetIds, profile.BackPresetIds) = (front, back);
        }
    }

    [AvaloniaFact]
    public void A_default_slot_of_a_turned_off_field_leaves_the_donut_and_comes_back()
    {
        using var f = new Fixture();
        var candidates =
            from profile in f.Store.Profiles
            where !profile.IsFallback && profile.Extensions.Count > 0
            let file = $"/d/a.{profile.Extensions[0]}"
            where ReferenceEquals(profile, ProfileSelector.Select(f.Store.Profiles, [file]))
            let formats = ProfileSelector.DetectFormats([file])
            from field in FormatFields.All
            where field.CanTurnOff && !formats.SelectMany(FormatFields.Of).Any(x => x.Id == field.Id)
            let off = new HashSet<string>([field.Id], StringComparer.OrdinalIgnoreCase)
            let presets = profile.PresetIds.Select(f.Store.FindPreset).OfType<Preset>().ToList()
            where presets.Any(p => !p.IsGroup && !FormatFields.IsShown(p.TargetFormat, off))
            where !presets.Where(p => p.IsGroup).SelectMany(p => p.SubPresetIds)
                .Select(f.Store.FindPreset).OfType<Preset>().Any(p => !FormatFields.IsShown(p.TargetFormat, off))
            select (Profile: profile, File: file, Off: off);
        var chosen = candidates.FirstOrDefault();
        Assert.NotNull(chosen.Profile);
        var saved = chosen.Profile.PresetIds.ToArray();
        f.Load(chosen.File);
        var baseline = f.Vm.Presets.ToArray();
        f.Store.Settings.TurnedOffFields = [.. chosen.Off];
        f.Load(chosen.File);
        f.Face(baseline.Where(p => FormatFields.IsShown(p.TargetFormat, chosen.Off)).Select(p => p.Id));
        Assert.Equal(saved, chosen.Profile.PresetIds);
        f.Store.Settings.TurnedOffFields.Clear();
        f.Load(chosen.File);
        f.Face(baseline.Select(p => p.Id));
    }

    [AvaloniaFact]
    public void Every_slot_of_a_turned_off_field_leaves_and_the_rest_keep_their_order()
    {
        using var f = new Fixture();
        string[] order = ["png", "yaml", "jpg", "toml", "webp"];
        var ids = order.Select(format => f.Add(format).Id).ToArray();
        var profile = f.Profile("/p/a.png");
        profile.PresetIds = [.. ids];
        f.Store.Settings.TurnedOffFields = ["developer"];
        f.Load("/p/a.png");
        f.Face([ids[0], ids[2], ids[4]]);
        RenderTests.Save(f.Window, "donut-fields-developer-off.png");
        Assert.Equal(ids, profile.PresetIds);
        f.Store.Settings.TurnedOffFields.Clear();
        f.Load("/p/a.png");
        f.Face(ids);
        RenderTests.Save(f.Window, "donut-fields-developer-on.png");
    }

    [AvaloniaFact]
    public void User_added_slots_follow_the_switch_on_both_faces()
    {
        using var f = new Fixture();
        var custom = f.Add("glb");
        var profile = f.Profile("/p/a.jpg");
        profile.PresetIds = ["to-png", "to-glb", custom.Id];
        // PNG keeps the back usable even when 3D model outputs are hidden or their engine is missing.
        profile.BackPresetIds = ["to-png", "to-gltf"];
        f.Store.Settings.TurnedOffFields = ["model3d"];
        f.Load("/p/a.jpg");
        f.Face(["to-png"]);
        Assert.True(f.Vm.ToggleFace());
        f.Face(["to-png"]);
        f.Store.Settings.TurnedOffFields.Clear();
        f.Load("/p/a.jpg");
        f.Face(["to-png", "to-glb", custom.Id]);
        Assert.True(f.Vm.ToggleFace());
        f.Face(["to-png", "to-gltf"]);
    }

    [AvaloniaFact]
    public void A_format_that_also_belongs_to_a_field_that_is_on_stays()
    {
        using var f = new Fixture();
        f.Profile("/p/a.png").PresetIds = ["to-dxf", "to-dwg"];
        f.Store.Settings.TurnedOffFields = ["cad"];
        f.Load("/p/a.png");
        f.Face(["to-dxf"]);
    }

    [AvaloniaFact]
    public void Group_children_of_a_turned_off_field_leave_the_ring()
    {
        using var f = new Fixture();
        var png = f.Add("png");
        var jpg = f.Add("jpg");
        var webp = f.Add("webp");
        var glb = f.Add("glb");
        var gltf = f.Add("gltf");
        var b = f.Group("B", glb.Id, gltf.Id);
        var a = f.Group("A", png.Id, glb.Id);
        var c = f.Group("C", "test-missing");
        var d = f.Group("D", jpg.Id, webp.Id);
        Preset[] groups = [b, a, c, d];
        var saved = groups.Select(p => p.SubPresetIds.ToArray()).ToArray();
        f.Profile("/p/a.jpg").PresetIds = groups.Select(p => p.Id).ToList();
        f.Store.Settings.TurnedOffFields = ["model3d"];
        f.Load("/p/a.jpg");
        f.Face([a.Id, c.Id, d.Id]);
        Assert.Single(f.Window.DonutControl.Items![0].Children!);
        Assert.Same(png, f.Vm.PresetShownAt(0, 0));
        Assert.Null(f.Vm.PresetShownAt(0, 1));
        Assert.Empty(f.Window.DonutControl.Items[1].Children!);
        Assert.Equal(f.Loc["donut.group_empty"], f.Window.DonutControl.Items[1].DisabledReason);
        Assert.Equal(2, f.Window.DonutControl.Items[2].Children!.Count);
        Assert.Same(jpg, f.Vm.PresetShownAt(2, 0));
        Assert.Same(webp, f.Vm.PresetShownAt(2, 1));
        Assert.Same(jpg, f.Vm.PresetAt(2, 0));
        f.Vm.FitToThisDonut();
        var donut = f.Window.DonutControl;
        donut.SetExternalHit(new DonutHit(DonutHitKind.Segment, 0));
        donut.ExpandPendingGroup(); // as after resting on ring A
        RenderTests.Pump();
        RenderTests.Save(f.Window, "donut-fields-groups.png");
        f.Store.Settings.TurnedOffFields.Clear();
        f.Load("/p/a.jpg");
        f.Face(groups.Select(p => p.Id));
        Assert.Same(gltf, f.Vm.PresetShownAt(0, 1));
        Assert.Same(glb, f.Vm.PresetShownAt(1, 1));
        for (var i = 0; i < groups.Length; i++)
            Assert.Equal(saved[i], groups[i].SubPresetIds);
    }

    [AvaloniaFact]
    public void Outputs_in_the_dropped_files_fields_stay_in_all_three_cases()
    {
        using var f = new Fixture();
        var glb = f.Add("glb");
        var stl = f.Add("stl");
        var png = f.Add("png");
        var group = f.Group("group", glb.Id, png.Id);
        var profile = f.Profile("/f/a.glb");
        var defaultModels = profile.PresetIds.Select(f.Store.FindPreset).OfType<Preset>()
            .Where(p => FormatRegistry.FindById(p.TargetFormat) is { } format && FormatFields.Of(format).Any(x => x.Id == "model3d"))
            .ToArray();
        Assert.NotEmpty(defaultModels);
        profile.PresetIds.AddRange([glb.Id, stl.Id, group.Id]);
        var saved = profile.PresetIds.ToArray();
        f.Load("/f/a.glb");
        var baseline = f.Vm.Presets.ToArray();
        f.Store.Settings.TurnedOffFields = ["model3d", "cad"];
        Assert.False(FormatFields.IsShown("glb", f.Store.Settings.TurnedOffFieldSet()));
        Assert.False(FormatFields.IsShown("stl", f.Store.Settings.TurnedOffFieldSet()));
        f.Load("/f/a.glb");
        var effectiveOff = new HashSet<string>(["cad"], StringComparer.OrdinalIgnoreCase);
        f.Face(baseline.Where(p => FormatFields.IsShown(p.TargetFormat, effectiveOff)).Select(p => p.Id));
        foreach (var preset in defaultModels.Concat([glb, stl, group]))
            Assert.Contains(f.Vm.Items, item => item.Id == preset.Id);
        var shown = Assert.Single(f.Window.DonutControl.Items!, item => item.Id == group.Id);
        Assert.Equal(new[] { glb.Id, png.Id }, shown.Children!.Select(p => p.Id));
        Assert.Equal(saved, profile.PresetIds);
        Assert.Equal(new[] { glb.Id, png.Id }, group.SubPresetIds);
    }

    [AvaloniaFact]
    public void A_new_install_opens_the_3d_model_donut_with_model_conversions()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = FormatFields.All.Where(x => x.CanTurnOff && !x.OnByDefault).Select(x => x.Id).ToList();
        var off = f.Store.Settings.TurnedOffFieldSet();
        var profile = f.Profile("/f/a.glb");
        Assert.False(profile.IsFallback);
        Assert.Contains("glb", profile.Extensions);
        var models = profile.PresetIds.Select(f.Store.FindPreset).OfType<Preset>()
            .Where(p => FormatRegistry.FindById(p.TargetFormat) is { } format && FormatFields.Of(format).Any(x => x.Id == "model3d"))
            .ToArray();
        Assert.NotEmpty(models);
        Assert.Contains(models, p => !FormatFields.IsShown(p.TargetFormat, off));
        f.Load("/f/a.glb");
        foreach (var preset in models)
            Assert.Contains(f.Window.DonutControl.Items!, item => item.Id == preset.Id);
        RenderTests.Save(f.Window, "donut-fields-model3d-new-install.png");
        Assert.Equal(f.Loc.Format("donut.files_same", 1, FormatRegistry.Get("glb").DisplayName), f.Window.DonutControl.CenterTitle);
    }

    [AvaloniaFact]
    public void Several_dropped_files_keep_the_fields_of_every_file()
    {
        using var f = new Fixture();
        var dds = f.Add("dds");
        var profile = f.Profile("/p/a.png", "/p/b.tga");
        Assert.False(profile.IsFallback);
        Assert.Same(f.Profile("/p/a.png"), profile);
        profile.PresetIds.Add(dds.Id);
        f.Store.Settings.TurnedOffFields = ["game"];
        f.Load("/p/a.png", "/p/b.tga");
        Assert.Contains(f.Window.DonutControl.Items!, item => item.Id == dds.Id);
        f.Load("/p/a.png");
        Assert.DoesNotContain(f.Window.DonutControl.Items!, item => item.Id == dds.Id);
    }

    [AvaloniaFact]
    public void The_mixed_donut_keeps_the_fields_of_every_dropped_file()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = FormatFields.All.Where(x => x.CanTurnOff && !x.OnByDefault).Select(x => x.Id).ToList();
        string[] files = ["/m/a.jpg", "/m/b.dwg", "/m/c.glb"];
        var profile = f.Profile(files);
        Assert.True(profile.IsFallback);
        profile.PresetIds = ["to-dwg", "to-glb", "to-kml"];
        f.Load(files);
        f.Face(["to-dwg", "to-glb"]);
        RenderTests.Save(f.Window, "donut-fields-mixed.png");
    }

    [AvaloniaFact]
    public void A_file_of_a_turned_off_field_is_still_recognized()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = ["video-pro"];
        Assert.Equal("ass", FormatRegistry.Detect("/s/a.ass")?.Id);
        Assert.Equal("subtitles", f.Profile("/s/a.ass").Id);
        f.Load("/s/a.ass");
        Assert.Equal(f.Loc.Format("donut.files_same", 1, "ASS"), f.Vm.CenterTitle);
        Assert.Equal(f.Vm.CenterTitle, f.Window.DonutControl.CenterTitle);
    }

    [AvaloniaFact]
    public void The_preset_library_leaves_out_turned_off_fields()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = ["model3d"];
        var off = f.Store.Settings.TurnedOffFieldSet();
        var page = new PresetsPageViewModel(f.Store, f.Loc, AppHost.Get<WindowService>());
        Assert.All(page.Presets, p => Assert.True(FormatFields.IsShown(p.Preset.TargetFormat, off), p.Preset.Id));
        Assert.Contains(page.Presets, p => p.Preset.Id == "to-stl");
        page.Search = "glb";
        Assert.Empty(page.VisiblePresets);
        Assert.All(page.PlacePresets, p => Assert.True(FormatFields.IsShown(p.Preset.TargetFormat, off)));
        f.Store.Settings.TurnedOffFields.Clear();
        var enabled = new PresetsPageViewModel(f.Store, f.Loc, AppHost.Get<WindowService>());
        Assert.Contains(enabled.Presets, p => p.Preset.Id == "to-glb");
    }

    [AvaloniaFact]
    public void The_editor_target_box_leaves_out_turned_off_fields()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = FormatFields.All.Where(x => x.CanTurnOff && !x.OnByDefault).Select(x => x.Id).ToList();
        var off = f.Store.Settings.TurnedOffFieldSet();
        var page = new PresetsPageViewModel(f.Store, f.Loc, AppHost.Get<WindowService>());
        page.SelectedPreset = page.Presets.Single(p => p.Preset.Id == "to-png");
        var window = new PresetEditorWindow { DataContext = page.Editor, Height = 900 };
        window.Show();
        try
        {
            RenderTests.Pump();
            var box = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TargetBox");
            var targets = box.Items.OfType<Choice<string>>().Select(c => c.Value).ToList();
            Assert.NotEmpty(targets);
            Assert.All(targets, t => Assert.True(FormatFields.IsShown(t, off), t));
            Assert.Contains("png", targets);
            Assert.Contains("*", targets);
            RenderTests.Save(window, "field-switch-targets.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_preset_whose_target_is_hidden_keeps_it_through_an_edit()
    {
        using var f = new Fixture();
        f.Store.Settings.TurnedOffFields = ["model3d"];
        var off = f.Store.Settings.TurnedOffFieldSet();
        var preset = f.Add("gltf");
        var editor = new PresetEditorViewModel(preset, f.Loc, off);
        Assert.All(editor.Targets, t => Assert.True(FormatFields.IsShown(t.Value, off), t.Value));
        Assert.DoesNotContain(editor.Targets, t => t.Value is "gltf" or "glb" or "ply");
        var name = FormatRegistry.Get("gltf").DisplayName;
        Assert.Equal("gltf", editor.Target.Value);
        Assert.Null(editor.TargetSelection);
        Assert.Equal(name, editor.HiddenTarget);
        Assert.Equal(f.Loc.Format("presets.target_hidden", name), editor.HiddenTargetNote);
        editor.Name = "Renamed model";
        Assert.True(editor.Apply());
        Assert.Equal("gltf", preset.TargetFormat);

        var window = new PresetEditorWindow { DataContext = editor, Width = 440, Height = 900 };
        window.Show();
        try
        {
            RenderTests.Pump();
            var box = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TargetBox");
            Assert.DoesNotContain(box.Items.OfType<Choice<string>>(), t => t.Value == "gltf");
            Assert.Null(box.SelectedItem);
            Assert.Equal(name, box.PlaceholderText);
            var note = Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == editor.HiddenTargetNote);
            Assert.True(note.IsVisible);
            Assert.Equal("gltf", editor.Target.Value);
            Assert.True(editor.Apply());
            Assert.Equal("gltf", preset.TargetFormat);
            RenderTests.Save(window, "preset-editor-hidden-target.png");
        }
        finally
        {
            window.Close();
        }
        var editing = AppHost.Get<WindowService>().EditPresetAsync(preset, f.Window);
        var popup = Assert.Single(f.Window.OwnedWindows.OfType<PresetEditorWindow>());
        try
        {
            RenderTests.Pump();
            var popupEditor = Assert.IsType<PresetEditorViewModel>(popup.DataContext);
            var box = popup.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TargetBox");
            Assert.DoesNotContain(box.Items.OfType<Choice<string>>(), t => t.Value == "gltf");
            Assert.Null(box.SelectedItem);
            Assert.Equal(name, box.PlaceholderText);
            Assert.Equal("gltf", popupEditor.Target.Value);
            RenderTests.Save(popup, "field-switch-hidden-target-popup.png");
        }
        finally
        {
            popup.Close(false);
        }
        Assert.False(await editing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var changed = new PresetEditorViewModel(preset, f.Loc, off);
        changed.TargetSelection = changed.Targets.Single(t => t.Value == "png");
        Assert.True(changed.Apply());
        Assert.Equal("png", preset.TargetFormat);
        Assert.Null(changed.HiddenTarget);
        Assert.Null(changed.HiddenTargetNote);
    }
}

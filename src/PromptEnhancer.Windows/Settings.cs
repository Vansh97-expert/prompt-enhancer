using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace PromptEnhancer.Windows;

public sealed record Preferences(string Shortcut = "Ctrl+Alt+E", bool UseLocalAi = false, string LocalModel = "")
{
    public static readonly string[] Shortcuts = ["Ctrl+Alt+E","Ctrl+Alt+H","Ctrl+Shift+F8"];
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PromptEnhancer","settings.json");

    public static Preferences Read()
    {
        try
        {
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath));
            return p != null && Shortcuts.Contains(p.Shortcut) && p.LocalModel is {Length: <=128} ? p : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) {return new();}
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(this));
            File.Move(temp,FilePath,true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class SettingsDialog : Window
{
    private readonly ComboBox shortcut = new();
    private readonly CheckBox engine = new() {Content="Use an installed local AI model",Margin=new Thickness(0,12,0,4)};
    private readonly TextBox model = new() {MaxLength=128};
    public Preferences Value => new((string)shortcut.SelectedItem,engine.IsChecked == true,model.Text.Trim());

    public SettingsDialog(Preferences prefs)
    {
        Title="Prompt Enhancer settings"; Width=440; SizeToContent=SizeToContent.Height;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var body = new StackPanel {Margin=new Thickness(20)};
        Content=body;
        shortcut.ItemsSource=Preferences.Shortcuts; shortcut.SelectedItem=prefs.Shortcut;
        body.Children.Add(new Label {Content="_Shortcut",Target=shortcut});
        body.Children.Add(shortcut);
        engine.IsChecked=prefs.UseLocalAi; body.Children.Add(engine);
        body.Children.Add(new Label {Content="Installed Ollama _model and tag",Target=model});
        model.Text=prefs.LocalModel; body.Children.Add(model);
        body.Children.Add(new TextBlock {Text="Use the exact name from 'ollama list'. Rules work immediately without setup.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)});
        var save=new Button {Content="_Save",IsDefault=true};
        save.Click+=(_,_)=>{
            if (engine.IsChecked == true && string.IsNullOrWhiteSpace(model.Text)) {model.Focus();return;}
            DialogResult=true;
        };
        var cancel=new Button {Content="Cancel",IsCancel=true};
        var actions=new WrapPanel(); actions.Children.Add(save); actions.Children.Add(cancel); body.Children.Add(actions);
    }
}


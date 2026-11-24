using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PromptEnhancer.Core;

namespace PromptEnhancer.Windows;

public partial class MainWindow : Window
{
    private Preferences preferences = Preferences.Read();
    private DesktopDraft? destination;
    private Draft? draft;
    private readonly Dictionary<string,TextBox> inputs = new();
    private CancellationTokenSource? generation;
    private IntPtr handle;
    private bool registered;
    private bool capturing;
    private int step;
    private TaskRoute Route => TaskBox.SelectedItem as TaskRoute ?? Catalog.General;

    public MainWindow()
    {
        InitializeComponent();
        TaskBox.ItemsSource = Catalog.Choices;
        SourceInitialized += (_,_) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
            SetShortcut(preferences.Shortcut);
            Say(registered ? $"Paste a draft here, or press {preferences.Shortcut} in the ChatGPT draft field."
                : "Shortcut in use. Choose another in Settings, or paste a draft here.");
        };
        ContentRendered += (_,_)=>Raw.Focus();
        Deactivated += (_,_)=>destination?.Invalidate();
        Closed += (_,_)=>{ generation?.Cancel(); UnregisterHotKey(handle,1); };
        PreviewKeyDown += (_,e)=>{ if(e.Key == Key.Escape) {e.Handled=true; ClearAndHide();} };
    }

    private void Say(string text)
    {
        Status.Text=text;
        var peer=UIElementAutomationPeer.FromElement(Status) ?? UIElementAutomationPeer.CreatePeerForElement(Status);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr id,IntPtr data,ref bool handled)
    {
        if(message == 0x0312 && id.ToInt32() == 1)
        {
            handled=true;
            if (!capturing && generation == null) _=OpenFromShortcut();
        }
        return IntPtr.Zero;
    }

    private async Task OpenFromShortcut()
    {
        capturing=true;
        try
        {
            if (IsActive) { Say("The enhancer is open. Return to the ChatGPT draft before using the shortcut."); return; }
            var captured = await Task.Run(DesktopDraft.Capture);
            // Preserve an unfinished session if the shortcut is pressed again.
            if (!string.IsNullOrEmpty(Raw.Text))
            {
                Show(); Activate(); Say("Your current draft is still open. Hide the enhancer to finish this session."); return;
            }
            Reset();
            destination=captured.Destination;
            if (!string.IsNullOrWhiteSpace(captured.Draft))
            {
                draft=Enhancer.Inspect(captured.Draft);
                Raw.Text=draft.Text;
                Configure();
            }
            Show(); Activate();
            if (draft == null) {Raw.Focus(); Say("Paste your draft here. The focused field could not supply a draft.");}
            else Say("Draft captured. Review the suggested task and enter the model name.");
        }
        catch (Exception e) when (Recoverable(e))
        {
            Show(); Activate(); Raw.Focus(); Say("The draft field could not be read reliably. Paste your draft here.");
        }
        finally {capturing=false;}
    }

    private static bool Recoverable(Exception e) => e is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    private bool SetShortcut(string shortcut)
    {
        if (registered) UnregisterHotKey(handle,1);
        (uint mods,uint key)=shortcut switch {
            "Ctrl+Alt+H"=>(3u,0x48u), "Ctrl+Shift+F8"=>(6u,0x77u), _=>(3u,0x45u) };
        return registered=RegisterHotKey(handle,1,mods|0x4000,key);
    }

    private void OpenSettings(object sender,RoutedEventArgs e)
    {
        if(generation != null) {Say("Finish or cancel the local rewrite before changing settings.");return;}
        var dialog=new SettingsDialog(preferences) {Owner=this};
        if(dialog.ShowDialog()!=true) return;
        var next=dialog.Value;
        if(!SetShortcut(next.Shortcut))
        {
            SetShortcut(preferences.Shortcut);
            Say("That shortcut is already in use. Choose a different shortcut."); return;
        }
        try {next.Save(); preferences=next; Say($"Settings saved. Use {next.Shortcut} while ChatGPT is focused.");}
        catch(Exception ex) when(Recoverable(ex))
        {SetShortcut(preferences.Shortcut); Say("Could not save settings in your Windows profile.");}
    }

    private void Page(int value)
    {
        step=value;
        Entry.Visibility=VisibleIf(value==0); Options.Visibility=VisibleIf(value==1); Review.Visibility=VisibleIf(value==2);
        Back.Visibility=VisibleIf(value>0); Next.Visibility=VisibleIf(value<2);
        CopyButton.Visibility=InsertButton.Visibility=VisibleIf(value==2);
        InsertButton.IsEnabled=destination!=null;
        Next.Content=value==0?"_Detect task":"_Enhance";
        RulesButton.Visibility=Visibility.Collapsed;
        Scroll.ScrollToTop();
    }

    private static Visibility VisibleIf(bool condition)=>condition?Visibility.Visible:Visibility.Collapsed;

    private void Configure()
    {
        if(draft==null) return;
        TaskBox.SelectedItem=draft.Routes[0];
        UpdateTechniques();
        RefreshQuestions(false);
        Page(1);
        Target.Focus();
        Say(draft.Redactions.Count>0 ? "Redacted: "+string.Join(", ",draft.Redactions)+". Review the task and model."
            : "Review the task and enter your target model.");
    }

    private void TaskChanged(object sender,SelectionChangedEventArgs e)
    {
        if(draft==null) return;
        UpdateTechniques(); RefreshQuestions(false);
    }

    private void UpdateTechniques()
    {
        TechniqueBox.ItemsSource=Route.Techniques;
        TechniqueBox.SelectedItem=Catalog.Recommend(Route,draft?.Instructions ?? "");
    }

    private void TechniqueChanged(object sender,SelectionChangedEventArgs e)
    {
        if(TechniqueBox.SelectedItem is Technique t) Rationale.Text=t.Instruction;
    }

    private void TargetChanged(object sender,TextChangedEventArgs e)
    {
        if(draft!=null) RefreshQuestions(true);
    }

    private void RefreshQuestions(bool keep)
    {
        if(draft==null) return;
        var model=Privacy.Clean(Target.Text).Text;
        Compatibility.Text=string.IsNullOrWhiteSpace(model)?"":Enhancer.Compatibility(model);
        var answers=keep?Answers():new Dictionary<string,string>();
        Questions.Children.Clear(); inputs.Clear();
        foreach(var field in Enhancer.Fields(draft,Route,model))
        {
            var box=new TextBox {MaxLength=1000,Text=answers.GetValueOrDefault(field.Id)??""};
            Questions.Children.Add(new Label {Content=field.Label+(field.Required?" (required)":" (optional)"),Target=box});
            AutomationProperties.SetName(box,field.Label);
            Questions.Children.Add(box); inputs[field.Id]=box;
        }
    }

    private Dictionary<string,string> Answers()=>inputs.ToDictionary(x=>x.Key,x=>Privacy.Clean(x.Value.Text).Text);
    private async void Advance(object sender,RoutedEventArgs e)
    {
        try
        {
            if(step==0)
            {
                var inspected=Enhancer.Inspect(Raw.Text);
                if(draft!=null) inspected=inspected with {Redactions=draft.Redactions.Concat(inspected.Redactions).Distinct().ToArray()};
                draft=inspected; Raw.Text=draft.Text; Configure();
            }
            else if(step==1) await Generate(preferences.UseLocalAi);
        }
        catch(Exception ex) when(Recoverable(ex)) {Say(ex is ArgumentException ? ex.Message : "Could not process this draft. Review its size and contents.");}
    }

    private async Task Generate(bool useAi)
    {
        if(draft==null || generation!=null || TechniqueBox.SelectedItem is not Technique technique) return;
        var model=Privacy.Clean(Target.Text.Trim()).Text;
        Target.Text=model;
        if(model.Length==0) {Say("Enter the model name you use in ChatGPT."); Target.Focus(); return;}
        foreach(var field in Enhancer.Fields(draft,Route,model))
            if(field.Required && string.IsNullOrWhiteSpace(inputs[field.Id].Text))
            { Say(field.Label); inputs[field.Id].Focus();return; }
        foreach(var box in inputs.Values) box.Text=Privacy.Clean(box.Text).Text;
        var answers=Answers();
        var result=Enhancer.Compile(draft,Route,technique,model,answers);
        if(useAi)
        {
            using var operation=new CancellationTokenSource();
            generation=operation; Busy(true); Say("The local model is reviewing this draft…");
            try
            {
                var extra=await LocalAi.SuggestAsync(preferences.LocalModel,draft,Route,technique,model,operation.Token);
                if(operation.IsCancellationRequested || draft==null) return;
                result=Enhancer.Compile(draft,Route,technique,model,answers,extra);
            }
            catch(Exception ex) when(Recoverable(ex))
            {
                if(step==1 && IsVisible)
                {
                    Say(ex is InvalidOperationException ? ex.Message : ex is OperationCanceledException ?
                        "Local generation stopped. You can retry or use the rules engine." :
                        "The local result could not be verified. Retry or use the rules engine.");
                    RulesButton.Visibility=Visibility.Visible;
                }
                return;
            }
            finally {generation=null; Busy(false);}
        }
        Preview.Text=result.Prompt;
        var notes=result.Additions.Select(x=>"Added · "+x)
            .Concat(result.Missing.Select(x=>"Missing · "+x+" ______"))
            .Concat(draft.Redactions.Select(x=>"Redacted · "+x)).ToArray();
        Notes.Text=notes.Length==0?"No assumptions added.":string.Join("\n\n",notes);
        Page(2); Preview.Focus();
        Say(result.Compatibility);
    }

    private void Busy(bool busy)
    {
        Options.IsEnabled=Next.IsEnabled=Back.IsEnabled=!busy;
        RulesButton.IsEnabled=!busy;
        StopButton.Visibility=VisibleIf(busy);
    }
    private async void UseRules(object sender,RoutedEventArgs e) { await Generate(false); }
    private void Stop(object sender,RoutedEventArgs e)=>generation?.Cancel();
    private void GoBack(object sender,RoutedEventArgs e) {Page(Math.Max(0,step-1));Say(step==0?"Edit your draft.":"Review the task, model, and details.");}
    private bool OutgoingReady()
    {
        if(string.IsNullOrWhiteSpace(Preview.Text)) {Say("The edited prompt is empty.");return false;}
        var cleaned=Privacy.Clean(Preview.Text);
        if(cleaned.Text==Preview.Text) return true;
        Preview.Text=cleaned.Text;
        Notes.Text+="\nRedacted · "+string.Join(", ",cleaned.Categories);
        Say("A sensitive value in your edit was redacted. Review the prompt and try again.");return false;
    }

    private void Copy(object sender,RoutedEventArgs e)
    {
        if(!OutgoingReady()) return;
        try
        {
            var data=new DataObject(); data.SetText(Preview.Text);
            data.SetData("ExcludeClipboardContentFromMonitorProcessing",new byte[]{1});
            Clipboard.SetDataObject(data,true); Say("Prompt copied.");
        }
        catch(ExternalException) {Say("Clipboard unavailable. Select the edited prompt to copy it manually.");}
    }

    private void Insert(object sender,RoutedEventArgs e)
    {
        if(!OutgoingReady()) return;
        try { if(destination?.TryInsert(Preview.Text,handle)==true) {ClearAndHide();return;} }
        catch(Exception ex) when(Recoverable(ex)) { /* The destination may disappear between UI Automation calls. */ }
        destination=null; InsertButton.IsEnabled=false;
        Say("Could not verify the original draft or the insertion. Review ChatGPT before pasting a copy.");
    }

    private void Reset()
    {
        generation?.Cancel(); destination=null; draft=null; Raw.Clear(); Target.Clear(); Preview.Clear();
        Notes.Text=Compatibility.Text=""; Questions.Children.Clear(); inputs.Clear(); Page(0);
    }
    private void ClearAndHide() {Reset(); Hide();}
    private void HidePopup(object sender,RoutedEventArgs e)=>ClearAndHide();
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h,int id);
}


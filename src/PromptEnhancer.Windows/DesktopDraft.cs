using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using PromptEnhancer.Core;

namespace PromptEnhancer.Windows;

public sealed class DesktopDraft
{
    private readonly IntPtr window;
    private readonly int[] fieldId;
    private readonly DraftGuard guard;
    private readonly AutomationElement element;
    private readonly string windowTitle;

    private DesktopDraft(IntPtr handle, AutomationElement field, string text)
    {
        window = handle;
        element = field;
        fieldId = field.GetRuntimeId();
        guard = new DraftGuard(text);
        windowTitle = Title(handle);
    }

    private static string Title(IntPtr handle)
    {
        var title = new StringBuilder(512);
        GetWindowText(handle,title,title.Capacity);
        return title.ToString();
    }

    public void Invalidate() => guard.Invalidate();

    public static (DesktopDraft? Destination, string Draft) Capture()
    {
        var handle = GetForegroundWindow();
        if (handle == 0 || !IsChatGpt(handle)) return (null, "");
        var focused = AutomationElement.FocusedElement;
        if (focused == null || !IsComposer(focused) || !BelongsTo(focused,handle)) return (null, "");
        string text;
        if (focused.TryGetCurrentPattern(ValuePattern.Pattern,out var value))
            text = ((ValuePattern)value).Current.Value;
        else if (focused.TryGetCurrentPattern(TextPattern.Pattern,out var pattern))
            text = ((TextPattern)pattern).DocumentRange.GetText(Privacy.MaxDraft + 1);
        else return (null,"");
        if (text.Length > Privacy.MaxDraft || GetForegroundWindow() != handle) return (null,"");
        return (new DesktopDraft(handle,focused,text),text);
    }

    public bool TryInsert(string text, IntPtr reviewWindow)
    {
        if (GetForegroundWindow() != reviewWindow || !IsWindow(window) || !IsChatGpt(window)
            || Title(window) != windowTitle || !IsComposer(element) || !BelongsTo(element,window)
            || !element.GetRuntimeId().SequenceEqual(fieldId)) return false;
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern)) return false;
        var field = (ValuePattern)pattern;
        if (field.Current.IsReadOnly) return false;
        return guard.TryWrite(GetForegroundWindow() == reviewWindow,field.Current.Value,()=>field.SetValue(text))
            && field.Current.Value == text;
    }

    private static bool IsComposer(AutomationElement field)
    {
        if (field.Current.IsPassword || !field.Current.IsEnabled) return false;
        var type = field.Current.ControlType;
        if (type != ControlType.Edit && type != ControlType.Document) return false;
        var id = field.Current.AutomationId;
        var name = field.Current.Name.Trim();
        return id.Equals("prompt-textarea",StringComparison.OrdinalIgnoreCase) ||
            new[]{"Message ChatGPT","Ask anything","Send a message","Prompt","Chat input"}.Contains(name,StringComparer.OrdinalIgnoreCase);
    }

    private static bool BelongsTo(AutomationElement field, IntPtr handle)
    {
        var root = AutomationElement.FromHandle(handle);
        AutomationElement? ancestor = field;
        for (var depth = 0; ancestor != null && depth < 48; depth++)
        {
            if (Automation.Compare(ancestor,root)) return true;
            ancestor = TreeWalker.RawViewWalker.GetParent(ancestor);
        }
        return false;
    }

    private static bool IsChatGpt(IntPtr handle)
    {
        GetWindowThreadProcessId(handle,out var pid);
        if (pid == 0) return false;
        using var process = Process.GetProcessById((int)pid);
        var length = 256u;
        var family = new StringBuilder((int)length);
        return GetPackageFamilyName(process.Handle, ref length, family) == 0 &&
            family.ToString().Equals("OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0", StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder name);
}


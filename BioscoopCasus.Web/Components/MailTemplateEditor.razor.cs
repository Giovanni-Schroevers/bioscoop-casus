using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BioscoopCasus.Web.Components;

/// <summary>
/// Email template editor with:
/// <list type="bullet">
///   <item>
///     <term>Visual editing</term>
///   </item>
///   <item>
///     <term>HTML/source editing</term>
///   </item>
///   <item>
///     <term>Preview rendering with mail variables</term>
///   </item>
///   <item>
///     <term>Formatting toolbar state syncing</term>
///   </item>
///   <item>
///     <term>Template saving</term>
///   </item>
/// </list>
/// </summary>
public partial class MailTemplateEditor : IAsyncDisposable
{
    #region Element references / JS interop

    private ElementReference _editorElementReference;
    private ElementReference _variableSearchInputReference;
    private DotNetObjectReference<MailTemplateEditor>? _dotNetObjectReference;

    #endregion

    #region Editor state

    /// <summary>
    /// Indicates whether the JS editor has already been initialized.
    /// </summary>
    private bool _hasInitializedEditor;

    /// <summary>
    /// Last HTML value that this component accepted as the current source of truth.
    /// Used to detect external changes coming in through parameters.
    /// </summary>
    private string _lastKnownEditorHtml = string.Empty;

    /// <summary>
    /// Last HTML value received back from the JS editor through callbacks.
    /// Prevents unnecessary setHtml calls and feedback loops.
    /// </summary>
    private string _lastHtmlReceivedFromEditor = string.Empty;

    /// <summary>
    /// Last HTML value explicitly pushed into the JS editor from .NET.
    /// Prevents reapplying the same content over and over.
    /// </summary>
    private string _lastHtmlAppliedToEditor = string.Empty;

    /// <summary>
    /// Rendered preview output after variables are resolved.
    /// </summary>
    private string _renderedPreviewHtml = string.Empty;

    /// <summary>
    /// Raw HTML shown/edited in source mode.
    /// </summary>
    private string _sourceHtml = string.Empty;

    /// <summary>
    /// True when the editor is in HTML/source mode instead of visual mode.
    /// </summary>
    private bool _isSourceView;

    #endregion

    #region Floating panels / menus

    private bool _showBlockStyleMenu;
    private bool _showVariablePicker;
    private bool _showLinkPanel;
    private bool _showImagePanel;
    private bool _showTextColorPanel;

    #endregion

    #region Toolbar state

    private bool _isBoldActive;
    private bool _isItalicActive;
    private bool _isUnderlineActive;
    private bool _isUnorderedListActive;
    private bool _isOrderedListActive;
    private bool _isLinkActive;

    private string _currentBlockStyleLabel = string.Empty;
    private string _selectedTextColor = "#e11d48";

    #endregion

    #region Link / image UI state

    private string _linkUrl = string.Empty;
    private string _linkText = string.Empty;
    private string _imageUrl = string.Empty;
    private string _imageAltText = string.Empty;

    #endregion

    #region Variable picker / preview / save state

    private string _variableSearchTerm = string.Empty;
    private string _previewMode = "desktop";

    private string _editorSaveStatusLabel = string.Empty;
    private int _editorSaveStatusVersion;

    #endregion

    #region Injections

    [Inject]
    public IJSRuntime JavaScriptRuntime { get; set; } = null!;
    
    #endregion

    #region Parameters

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public List<MailHelper.MailVariableDefinition> AvailableVariables { get; set; } = [];

    [Parameter]
    public IReadOnlyDictionary<string, string> PreviewValues { get; set; } =
        new Dictionary<string, string>();

    #endregion

    #region Computed properties

    /// <summary>
    /// Filters and sorts the available mail variables for the variable picker.
    /// Search matches both label and key.
    /// </summary>
    private List<MailHelper.MailVariableDefinition> FilteredVariables =>
        AvailableVariables
            .Where(variable =>
                string.IsNullOrWhiteSpace(_variableSearchTerm) ||
                variable.Label.Contains(_variableSearchTerm, StringComparison.OrdinalIgnoreCase) ||
                variable.Key.Contains(_variableSearchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(variable => variable.Label)
            .ToList();

    #endregion

    #region Lifecycle

    /// <summary>
    /// Keeps the preview and internal editor state in sync with incoming parameter values.
    /// Before JS initialization, only local state is prepared.
    /// After JS initialization, this tracks external Value changes so they can later be applied to the editor.
    /// </summary>
    protected override void OnParametersSet()
    {
        UpdateRenderedPreview();

        if (!_hasInitializedEditor)
        {
            _lastKnownEditorHtml = Value;
            _sourceHtml = Value;
            return;
        }

        if (Value == _lastKnownEditorHtml) return;
        
        _lastKnownEditorHtml = Value;
        _sourceHtml = Value;
    }

    /// <summary>
    /// Initializes the JS editor on first render and pushes external content into the editor
    /// when the parent component changes the bound value.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _dotNetObjectReference ??= DotNetObjectReference.Create(this);

        if (_isSourceView)
            return;

        if (firstRender || !_hasInitializedEditor)
        {
            _currentBlockStyleLabel = L["Paragraph"];
            _editorSaveStatusLabel = L["Saved"];

            await JavaScriptRuntime.InvokeVoidAsync(
                "mailEditor.initialize",
                _editorElementReference,
                Value,
                _dotNetObjectReference);

            _hasInitializedEditor = true;
            _lastHtmlAppliedToEditor = Value;
            _lastHtmlReceivedFromEditor = Value;
            _sourceHtml = Value;

            return;
        }

        var externalValue = Value;

        // Only push content into the JS editor when it truly came from outside.
        // This avoids endless update loops between JS and Blazor.
        if (externalValue != _lastHtmlReceivedFromEditor &&
            externalValue != _lastHtmlAppliedToEditor)
        {
            await JavaScriptRuntime.InvokeVoidAsync(
                "mailEditor.setHtml",
                _editorElementReference,
                externalValue);

            _lastHtmlAppliedToEditor = externalValue;
        }
    }

    #endregion

    #region JS callbacks

    /// <summary>
    /// Called by JS whenever the editor HTML changes.
    /// Updates the component state, preview, parent binding, toolbar state, and save indicator.
    /// </summary>
    [JSInvokable]
    public async Task HandleEditorChangedAsync(string html)
    {
        Value = html;
        _lastKnownEditorHtml = Value;
        _lastHtmlReceivedFromEditor = Value;
        _sourceHtml = Value;

        UpdateRenderedPreview();

        await ValueChanged.InvokeAsync(Value);
        await RefreshActiveFormatsAsync();
        await UpdateSaveStatusAsync();

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called by JS whenever the current selection/caret changes.
    /// Refreshes toolbar active state like bold/list/link/block type.
    /// </summary>
    [JSInvokable]
    public async Task HandleSelectionChangedAsync()
    {
        await RefreshActiveFormatsAsync();
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Synchronization helpers

    /// <summary>
    /// Re-renders the preview HTML by replacing template variables with preview values.
    /// </summary>
    private void UpdateRenderedPreview()
    {
        _renderedPreviewHtml = MailHelper.Render(Value, PreviewValues);
    }

    /// <summary>
    /// Pulls the current HTML out of the JS editor and updates the component state.
    /// Use this after editor commands that mutate content from JS.
    /// </summary>
    private async Task SyncValueFromEditorAsync()
    {
        Value = await JavaScriptRuntime.InvokeAsync<string>("mailEditor.getHtml", _editorElementReference);
        _lastKnownEditorHtml = Value;
        _lastHtmlReceivedFromEditor = Value;
        _sourceHtml = Value;

        UpdateRenderedPreview();

        await ValueChanged.InvokeAsync(Value);
        await RefreshActiveFormatsAsync();
        await UpdateSaveStatusAsync();
    }

    /// <summary>
    /// Shows a temporary "Saving..." status and resolves to "Saved" if no newer change happens meanwhile.
    /// The version counter prevents older delayed calls from overwriting newer state.
    /// </summary>
    private async Task UpdateSaveStatusAsync()
    {
        var currentVersion = Interlocked.Increment(ref _editorSaveStatusVersion);
        
        _editorSaveStatusLabel = $"{L["Saving"]}...";
        await InvokeAsync(StateHasChanged);

        await Task.Delay(650);

        if (currentVersion != _editorSaveStatusVersion)
            return;
        

        _editorSaveStatusLabel = L["Saved"];
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Reads the current formatting state from JS and updates the toolbar UI.
    /// This includes inline formatting, list state, active link, current block tag, and text color.
    /// </summary>
    private async Task RefreshActiveFormatsAsync()
    {
        if (!_hasInitializedEditor || _isSourceView)
            return;
        

        var activeFormats = await JavaScriptRuntime.InvokeAsync<ActiveFormatState>(
            "mailEditor.getActiveFormats",
            _editorElementReference);

        _isBoldActive = activeFormats.Bold;
        _isItalicActive = activeFormats.Italic;
        _isUnderlineActive = activeFormats.Underline;
        _isUnorderedListActive = activeFormats.UnorderedList;
        _isOrderedListActive = activeFormats.OrderedList;
        _isLinkActive = activeFormats.Link;

        _currentBlockStyleLabel = activeFormats.CurrentBlockTag switch
        {
            "h1" => L["Heading1"],
            "h2" => L["Heading2"],
            "h3" => L["Heading3"],
            "blockquote" => L["Quote"],
            _ => L["Paragraph"]
        };

        if (!string.IsNullOrWhiteSpace(activeFormats.CurrentTextColor))
            _selectedTextColor = activeFormats.CurrentTextColor;
        

        if (_isLinkActive)
        {
            _linkUrl = activeFormats.CurrentLinkUrl;
            _linkText = activeFormats.CurrentLinkText;
        }
    }

    #endregion

    #region Editor commands

    private async Task UndoAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.undo", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task RedoAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.redo", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task WrapSelectionStrongAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.toggleInlineTag", _editorElementReference, "strong");
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task WrapSelectionEmphasisAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.toggleInlineTag", _editorElementReference, "em");
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task WrapSelectionUnderlineAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.toggleInlineTag", _editorElementReference, "u");
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearFormattingAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.clearFormatting", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleUnorderedListAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.toggleUnorderedList", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleOrderedListAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.toggleOrderedList", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task RemoveLinkAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.removeLink", _editorElementReference);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyBlockStyleAsync(string tagName, string label)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return;
        

        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.applyBlockTag", _editorElementReference, tagName);
        await SyncValueFromEditorAsync();

        _currentBlockStyleLabel = label;
        _showBlockStyleMenu = false;

        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyTextColorAsync()
    {
        CloseFloatingUi();

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.applyTextColor", _editorElementReference, _selectedTextColor);
        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task InsertOrUpdateLinkAsync()
    {
        var normalizedUrl = MailHelper.NormalizeUrl(_linkUrl);

        if (string.IsNullOrWhiteSpace(normalizedUrl))
            return;
        

        await JavaScriptRuntime.InvokeVoidAsync(
            "mailEditor.insertOrUpdateLink",
            _editorElementReference,
            normalizedUrl,
            _linkText);

        _showLinkPanel = false;

        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task InsertImageAsync()
    {
        var normalizedUrl = MailHelper.NormalizeUrl(_imageUrl);

        if (string.IsNullOrWhiteSpace(normalizedUrl))
            return;
        
        var imageHtml = MailHelper.BuildImageHtml(normalizedUrl, _imageAltText);

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.insertHtmlAtCursor", _editorElementReference, imageHtml);

        _imageUrl = string.Empty;
        _imageAltText = string.Empty;
        _showImagePanel = false;

        await SyncValueFromEditorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SelectVariableAsync(string selectedKey)
    {
        if (string.IsNullOrWhiteSpace(selectedKey))
            return;

        var html = MailHelper.BuildVariableTokenHtml(selectedKey);

        await JavaScriptRuntime.InvokeVoidAsync("mailEditor.insertHtmlAtCursor", _editorElementReference, html);
        await SyncValueFromEditorAsync();

        _showVariablePicker = false;
        _variableSearchTerm = string.Empty;

        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Source view

    /// <summary>
    /// Switches between visual mode and source mode.
    /// When leaving source mode, the HTML from the textarea becomes the new editor value
    /// and the visual editor is forced to reinitialize with that content.
    /// </summary>
    private async Task ToggleSourceViewAsync()
    {
        CloseFloatingUi();

        if (_isSourceView)
        {
            Value = _sourceHtml;
            _lastKnownEditorHtml = Value;
            _lastHtmlReceivedFromEditor = Value;
            _lastHtmlAppliedToEditor = Value;

            UpdateRenderedPreview();

            _isSourceView = false;
            _hasInitializedEditor = false;

            await ValueChanged.InvokeAsync(Value);
            await InvokeAsync(StateHasChanged);
            return;
        }

        _sourceHtml = await JavaScriptRuntime.InvokeAsync<string>("mailEditor.getHtml", _editorElementReference);
        _isSourceView = true;

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Handles raw HTML edits in source mode and immediately updates preview + bound value.
    /// </summary>
    private async Task HandleSourceHtmlChanged(ChangeEventArgs changeEventArgs)
    {
        _sourceHtml = changeEventArgs.Value?.ToString() ?? string.Empty;
        Value = _sourceHtml;
        _lastKnownEditorHtml = Value;
        _lastHtmlReceivedFromEditor = Value;

        UpdateRenderedPreview();

        await ValueChanged.InvokeAsync(Value);
        await UpdateSaveStatusAsync();
    }

    #endregion

    #region Floating UI state control

    private void ToggleBlockStyleMenu()
    {
        _showBlockStyleMenu = !_showBlockStyleMenu;

        if (!_showBlockStyleMenu) return;
        _showVariablePicker = false;
        _showLinkPanel = false;
        _showImagePanel = false;
        _showTextColorPanel = false;
    }

    private async Task OpenVariablePicker()
    {
        _showVariablePicker = true;
        _showBlockStyleMenu = false;
        _showLinkPanel = false;
        _showImagePanel = false;
        _showTextColorPanel = false;
        _variableSearchTerm = string.Empty;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
        await _variableSearchInputReference.FocusAsync();
    }

    private void CloseVariablePicker()
    {
        _showVariablePicker = false;
        _variableSearchTerm = string.Empty;
    }

    private async Task ToggleLinkPanel()
    {
        _showLinkPanel = !_showLinkPanel;

        if (_showLinkPanel)
        {
            _showImagePanel = false;
            _showBlockStyleMenu = false;
            _showVariablePicker = false;
            _showTextColorPanel = false;

            await RefreshActiveFormatsAsync();

            if (!_isLinkActive)
            {
                _linkUrl = string.Empty;
                _linkText = string.Empty;
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private void ToggleImagePanel()
    {
        _showImagePanel = !_showImagePanel;

        if (!_showImagePanel) return;
        
        _showLinkPanel = false;
        _showBlockStyleMenu = false;
        _showVariablePicker = false;
        _showTextColorPanel = false;
    }

    private void ToggleTextColorPanel()
    {
        _showTextColorPanel = !_showTextColorPanel;

        if (!_showTextColorPanel)
            return;
        
        _showLinkPanel = false;
        _showImagePanel = false;
        _showBlockStyleMenu = false;
        _showVariablePicker = false;
    }

    /// <summary>
    /// Closes every popover/panel so toolbar actions do not leave stale UI open.
    /// </summary>
    private void CloseFloatingUi()
    {
        _showBlockStyleMenu = false;
        _showVariablePicker = false;
        _showLinkPanel = false;
        _showImagePanel = false;
        _showTextColorPanel = false;
    }

    #endregion

    #region Variable picker events

    private void HandleVariableSearchChanged(ChangeEventArgs changeEventArgs) => _variableSearchTerm = changeEventArgs.Value?.ToString() ?? string.Empty;

    private void HandleVariablePickerKeyDown(KeyboardEventArgs keyboardEventArgs)
    {
        if (keyboardEventArgs.Key == "Escape") CloseVariablePicker();
    }
    
    #endregion

    #region Preview helpers

    private void SetPreviewMode(string previewMode) => _previewMode = previewMode;

    private string GetPreviewModeClass()
        => _previewMode == "mobile"
            ? "mail-template-editor-preview-host-mobile"
            : "mail-template-editor-preview-host-desktop";
    
    private string GetPreviewModeButtonClass(string previewMode)
        => _previewMode == previewMode
            ? "mail-template-editor-toolbar-button-active"
            : string.Empty;
    
    #endregion
    
    #region Styling helpers

    private static string GetToolbarButtonClass(bool isActive)
        => isActive ? "mail-template-editor-toolbar-button-active" : string.Empty;

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        _dotNetObjectReference?.Dispose();
        await Task.CompletedTask;
        
        GC.SuppressFinalize(this);
    }

    #endregion

    #region JS state DTO

    /// <summary>
    /// Snapshot returned by JS describing the formatting state at the current selection.
    /// </summary>
    private sealed class ActiveFormatState
    {
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public bool Underline { get; init; }
        public bool UnorderedList { get; init; }
        public bool OrderedList { get; init; }
        public bool Link { get; init; }
        public string CurrentBlockTag { get; init; } = "p";
        public string CurrentTextColor { get; init; } = string.Empty;
        public string CurrentLinkUrl { get; init; } = string.Empty;
        public string CurrentLinkText { get; init; } = string.Empty;
    }

    #endregion
}
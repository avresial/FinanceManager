---
name: mudblazor
description: Reference for MudBlazor 9.x components used in FinanceManager. Consult this whenever building or modifying UI with MudBlazor — it lists every component family, links to the official docs, and shows the minimal idiomatic snippet for each. Use it to avoid guessing API surface or re-reading full docs pages.
---

# MudBlazor 9.x Component Reference

**Version in use**: `MudBlazor 9.4.0` (see `code/Directory.Packages.props`)
**Docs root**: `https://mudblazor.com/components/`

## Project-wide conventions (apply to every snippet)

- **Inject via `[Inject]` properties** in code-behind or `@code {}` — never `@inject` directives in markup.
- **Form inputs**: prefer `Variant="Variant.Outlined"` and `Margin="Margin.Dense"` for compact, consistent look.
- **Theme**: production is **dark**, sandbox is **light**. Use `color: var(--mud-palette-text-primary)` for theme-safe text, not hard-coded colours.
- **Icons**: `Icons.Material.Filled.*`, `Icons.Material.Outlined.*`, `Icons.Material.Rounded.*`.
- **Sizing enum values**: `Size.Small`, `Size.Medium`, `Size.Large`.
- **Color enum values**: `Color.Default`, `Color.Primary`, `Color.Secondary`, `Color.Tertiary`, `Color.Success`, `Color.Warning`, `Color.Error`, `Color.Info`, `Color.Surface`, `Color.Inherit`.

---

## Layout

### MudLayout / MudAppBar / MudDrawer / MudNavMenu
**Docs**: https://mudblazor.com/components/appbar | https://mudblazor.com/components/drawer | https://mudblazor.com/components/navmenu

```razor
<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="@(() => _drawerOpen = !_drawerOpen)" />
        <MudText Typo="Typo.h6">FinanceManager</MudText>
        <MudSpacer />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <MudNavMenu>
            <MudNavLink Href="/" Icon="@Icons.Material.Filled.Dashboard" Match="NavLinkMatch.All">Dashboard</MudNavLink>
            <MudNavGroup Title="Accounts" Icon="@Icons.Material.Filled.AccountBalance" Expanded="true">
                <MudNavLink Href="/AccountDetails/1">Cash 1</MudNavLink>
            </MudNavGroup>
        </MudNavMenu>
    </MudDrawer>

    <MudMainContent>
        @Body
    </MudMainContent>
</MudLayout>
```
Key params: `MudDrawer` — `Variant` (`DrawerVariant.Mini/Responsive/Temporary`), `@bind-Open`. `MudAppBar` — `Fixed`, `Dense`, `Color`.

---

### MudContainer
**Docs**: https://mudblazor.com/components/container

```razor
<MudContainer MaxWidth="MaxWidth.Large" Class="my-4 px-4">
    @* page content *@
</MudContainer>
```
`MaxWidth` values: `False`, `ExtraSmall`, `Small`, `Medium`, `Large`, `ExtraLarge`.

---

### MudGrid / MudItem
**Docs**: https://mudblazor.com/components/grid

```razor
<MudGrid Spacing="3">
    <MudItem xs="12" sm="6" md="4">
        <MudText>Column 1</MudText>
    </MudItem>
    <MudItem xs="12" sm="6" md="8">
        <MudText>Column 2</MudText>
    </MudItem>
</MudGrid>
```
Breakpoints: `xs`, `sm`, `md`, `lg`, `xl` (1–12 columns). `Spacing` is 0–16 (MUI spacing units).

---

### MudStack
**Docs**: https://mudblazor.com/components/stack

```razor
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2" Justify="Justify.SpaceBetween">
    <MudText>Left</MudText>
    <MudButton>Right</MudButton>
</MudStack>
```
`Row="false"` (default) = column. `Wrap="Wrap.Wrap"` for flex-wrap.

---

## Text & Typography

### MudText
**Docs**: https://mudblazor.com/components/text

```razor
<MudText Typo="Typo.h5" Color="Color.Primary">Heading</MudText>
<MudText Typo="Typo.body2" Align="Align.Center">Body copy</MudText>
```
`Typo` values: `h1`–`h6`, `subtitle1`, `subtitle2`, `body1`, `body2`, `button`, `caption`, `overline`.

---

### MudLink
**Docs**: https://mudblazor.com/components/link

```razor
<MudLink Href="/settings" Typo="Typo.body2" Color="Color.Primary">Settings</MudLink>
```

---

### MudHighlighter
**Docs**: https://mudblazor.com/components/highlighter

```razor
<MudHighlighter Text="@fullText" HighlightedText="@searchTerm" />
```

---

## Buttons

### MudButton
**Docs**: https://mudblazor.com/components/button

```razor
<MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="Save"
           StartIcon="@Icons.Material.Filled.Save" Size="Size.Medium">
    Save
</MudButton>
```
`Variant`: `Text`, `Outlined`, `Filled`. `Disabled`, `FullWidth`, `Style` all supported.

---

### MudIconButton
**Docs**: https://mudblazor.com/components/button

```razor
<MudIconButton Icon="@Icons.Material.Filled.Delete" Color="Color.Error"
               Size="Size.Small" OnClick="Delete" />
```

---

### MudFab (Floating Action Button)
**Docs**: https://mudblazor.com/components/fab

```razor
<MudFab Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add"
        Size="Size.Large" OnClick="AddItem" />
```

---

### MudButtonGroup
**Docs**: https://mudblazor.com/components/buttongroup

```razor
<MudButtonGroup Variant="Variant.Filled" Color="Color.Primary" Size="Size.Medium">
    <MudButton OnClick="Save">Save</MudButton>
    <MudMenu Icon="@Icons.Material.Filled.ArrowDropDown"
             AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight"
             Dense="true" Style="align-self: auto;">
        <MudMenuItem OnClick="SaveAs">Save as…</MudMenuItem>
        <MudMenuItem OnClick="Export">Export</MudMenuItem>
    </MudMenu>
</MudButtonGroup>
```
**Split-button pattern**: put `MudMenu` with `Icon` directly inside `MudButtonGroup` — do NOT use `ActivatorContent` + inner `MudButton` for this pattern.

---

### MudToggleGroup / MudToggleItem
**Docs**: https://mudblazor.com/components/togglegroup

```razor
<MudToggleGroup T="string?" Value="@_selected" ValueChanged="OnChanged"
                SelectionMode="SelectionMode.ToggleSelection"
                Dense="true" Size="Size.Small" Outlined="true" Delimiters="true">
    <MudToggleItem Value="@("income")">Income</MudToggleItem>
    <MudToggleItem Value="@("expense")">Expense</MudToggleItem>
</MudToggleGroup>
```
`SelectionMode`: `SingleSelection` (always one selected), `ToggleSelection` (nullable — clicking active item deselects), `MultiSelection` (T must be IList).

---

## Inputs

### MudTextField
**Docs**: https://mudblazor.com/components/textfield

```razor
<MudTextField T="string" @bind-Value="_name"
              Label="Name" Placeholder="Enter name"
              Variant="Variant.Outlined" Margin="Margin.Dense"
              Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Search"
              Clearable="true" Immediate="true" DebounceInterval="300"
              HelperText="Max 100 chars" MaxLength="100" />
```
Use `Value` + `ValueChanged` (uncontrolled) or `@bind-Value` (two-way). `Immediate="true"` fires on every keystroke.

---

### MudNumericField
**Docs**: https://mudblazor.com/components/numericfield

```razor
<MudNumericField T="decimal" @bind-Value="_amount"
                 Label="Amount" Variant="Variant.Outlined" Margin="Margin.Dense"
                 Min="0m" Max="1_000_000m" Step="0.01m"
                 Adornment="Adornment.Start" AdornmentText="$" />
```

---

### MudSelect
**Docs**: https://mudblazor.com/components/select

```razor
<MudSelect T="string" @bind-Value="_category"
           Label="Category" Variant="Variant.Outlined" Margin="Margin.Dense"
           AnchorOrigin="Origin.BottomCenter">
    @foreach (var cat in _categories)
    {
        <MudSelectItem Value="@cat">@cat</MudSelectItem>
    }
</MudSelect>

@* Multi-select *@
<MudSelect T="string" @bind-SelectedValues="_selected"
           MultiSelection="true" Label="Tags" Variant="Variant.Outlined">
    <MudSelectItem Value="@("tag1")">Tag 1</MudSelectItem>
</MudSelect>
```

---

### MudAutocomplete
**Docs**: https://mudblazor.com/components/autocomplete

```razor
<MudAutocomplete T="string" @bind-Value="_picked"
                 Label="Search" Variant="Variant.Outlined" Margin="Margin.Dense"
                 SearchFunc="SearchAsync" MinCharacters="1" />
```
`SearchFunc` is `Func<string, CancellationToken, Task<IEnumerable<T>>>`.

---

### MudCheckBox
**Docs**: https://mudblazor.com/components/checkbox

```razor
<MudCheckBox @bind-Value="_checked" Label="Accept terms" Color="Color.Primary" Dense="true" />
```
`T="bool"` (default). For nullable: `T="bool?"`.

---

### MudSwitch
**Docs**: https://mudblazor.com/components/switch

```razor
<MudSwitch @bind-Value="_enabled" Label="Enable notifications" Color="Color.Primary" />
```

---

### MudRadioGroup / MudRadio
**Docs**: https://mudblazor.com/components/radio

```razor
<MudRadioGroup T="string" @bind-Value="_mode">
    <MudRadio Value="@("auto")" Color="Color.Primary">Auto</MudRadio>
    <MudRadio Value="@("manual")" Color="Color.Primary">Manual</MudRadio>
</MudRadioGroup>
```

---

### MudDatePicker
**Docs**: https://mudblazor.com/components/datepicker

```razor
<MudDatePicker @bind-Date="_date" Label="Date"
               Variant="Variant.Outlined" Margin="Margin.Dense"
               DateFormat="yyyy-MM-dd" MinDate="DateTime.Today.AddYears(-10)" />
```
`_date` is `DateTime?`. `DateRangePicker` is a separate component (see below).

---

### MudDateRangePicker
**Docs**: https://mudblazor.com/components/daterangepicker

```razor
<MudDateRangePicker @bind-DateRange="_range" Label="Period"
                    Variant="Variant.Outlined" DateFormat="MMM d" />
@code {
    DateRange _range = new(DateTime.Today.AddMonths(-1), DateTime.Today);
}
```
`DateRange` is `MudBlazor.DateRange` with `Start` and `End` (`DateTime?`).

---

### MudTimePicker
**Docs**: https://mudblazor.com/components/timepicker

```razor
<MudTimePicker @bind-Time="_time" Label="Time"
               Variant="Variant.Outlined" Margin="Margin.Dense" AmPm="false" />
```
`_time` is `TimeSpan?`.

---

### MudSlider
**Docs**: https://mudblazor.com/components/slider

```razor
<MudSlider T="int" @bind-Value="_value" Min="0" Max="100" Step="5"
           Color="Color.Primary">@_value%</MudSlider>
```

---

### MudRating
**Docs**: https://mudblazor.com/components/rating

```razor
<MudRating @bind-SelectedValue="_stars" MaxValue="5" Color="Color.Warning" />
```

---

### MudFileUpload
**Docs**: https://mudblazor.com/components/fileupload

```razor
<MudFileUpload T="IBrowserFile" FilesChanged="OnFileChanged" Accept=".csv">
    <ActivatorContent>
        <MudButton Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Upload">
            Choose file
        </MudButton>
    </ActivatorContent>
</MudFileUpload>
```

---

## Data Display

### MudTable
**Docs**: https://mudblazor.com/components/table

```razor
<MudTable Items="_rows" Hover="true" Dense="true" Striped="false"
          Loading="_loading" LoadingProgressColor="Color.Primary"
          Filter="FilterFunc" SortLabel="Sort by">
    <ToolBarContent>
        <MudText Typo="Typo.h6">Transactions</MudText>
    </ToolBarContent>
    <HeaderContent>
        <MudTh><MudTableSortLabel SortBy="new Func<Row, object>(r => r.Date)">Date</MudTableSortLabel></MudTh>
        <MudTh>Amount</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd>@context.Date.ToShortDateString()</MudTd>
        <MudTd>@context.Amount.ToString("C")</MudTd>
    </RowTemplate>
    <PagerContent>
        <MudTablePager />
    </PagerContent>
</MudTable>
```

---

### MudDataGrid
**Docs**: https://mudblazor.com/components/datagrid

```razor
<MudDataGrid T="Row" Items="_rows" Filterable="true" Sortable="true"
             Dense="true" Hover="true" RowClick="OnRowClick">
    <Columns>
        <PropertyColumn Property="r => r.Date" Title="Date" />
        <PropertyColumn Property="r => r.Amount" Title="Amount" Format="C" />
        <TemplateColumn Title="Actions">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Delete" OnClick="@(() => Delete(context.Item))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

---

### MudList / MudListItem
**Docs**: https://mudblazor.com/components/list

```razor
<MudList T="string" Dense="true" ReadOnly="false" @bind-SelectedValue="_sel">
    @foreach (var item in _items)
    {
        <MudListItem Value="@item" Icon="@Icons.Material.Filled.Circle">@item</MudListItem>
    }
</MudList>
```

---

### MudExpansionPanels / MudExpansionPanel
**Docs**: https://mudblazor.com/components/expansion-panels

```razor
<MudExpansionPanels MultiExpansion="true">
    <MudExpansionPanel Text="Section 1" IsInitiallyExpanded="true">
        <MudText>Content 1</MudText>
    </MudExpansionPanel>
    <MudExpansionPanel Text="Section 2">
        <MudText>Content 2</MudText>
    </MudExpansionPanel>
</MudExpansionPanels>
```

---

### MudTreeView / MudTreeViewItem
**Docs**: https://mudblazor.com/components/treeview

```razor
<MudTreeView T="string" Dense="true" Hover="true">
    <MudTreeViewItem Value="@("root")" Text="Root">
        <MudTreeViewItem Value="@("child")" Text="Child" />
    </MudTreeViewItem>
</MudTreeView>
```

---

## Cards & Surfaces

### MudCard
**Docs**: https://mudblazor.com/components/card

```razor
<MudCard Elevation="2">
    <MudCardHeader>
        <CardHeaderAvatar>
            <MudAvatar Color="Color.Primary">A</MudAvatar>
        </CardHeaderAvatar>
        <CardHeaderContent>
            <MudText Typo="Typo.body1">Title</MudText>
            <MudText Typo="Typo.body2" Color="Color.Default">Subtitle</MudText>
        </CardHeaderContent>
        <CardHeaderActions>
            <MudIconButton Icon="@Icons.Material.Filled.MoreVert" />
        </CardHeaderActions>
    </MudCardHeader>
    <MudCardContent>
        <MudText>Body text here.</MudText>
    </MudCardContent>
    <MudCardActions>
        <MudButton Variant="Variant.Text" Color="Color.Primary">Action</MudButton>
    </MudCardActions>
</MudCard>
```

---

### MudPaper
**Docs**: https://mudblazor.com/components/paper

```razor
<MudPaper Elevation="4" Class="pa-4">
    @* content *@
</MudPaper>
```
`Elevation` 0–24. `Outlined="true"` instead of shadow. `Square="true"` removes border-radius.

---

### MudDivider
**Docs**: https://mudblazor.com/components/divider

```razor
<MudDivider Class="my-2" />
<MudDivider Vertical="true" FlexItem="true" Class="mx-2" />
```

---

## Chips & Badges

### MudChip / MudChipSet
**Docs**: https://mudblazor.com/components/chip

```razor
<MudChipSet T="string" @bind-SelectedValues="_selectedChips" SelectionMode="SelectionMode.MultiSelection">
    <MudChip T="string" Value="@("food")" Color="Color.Primary">Food</MudChip>
    <MudChip T="string" Value="@("travel")" Color="Color.Secondary">Travel</MudChip>
</MudChipSet>
```
Standalone chip with close button:
```razor
<MudChip T="string" OnClose="Remove" Color="Color.Primary">Label</MudChip>
```

---

### MudBadge
**Docs**: https://mudblazor.com/components/badge

```razor
<MudBadge Content="3" Color="Color.Error" Overlap="true" Bordered="true">
    <MudIconButton Icon="@Icons.Material.Filled.Notifications" />
</MudBadge>
```

---

### MudAvatar
**Docs**: https://mudblazor.com/components/avatar

```razor
<MudAvatar Color="Color.Primary" Size="Size.Medium">JD</MudAvatar>
<MudAvatar Image="/img/avatar.png" Size="Size.Large" />
```

---

### MudIcon
**Docs**: https://mudblazor.com/components/icon

```razor
<MudIcon Icon="@Icons.Material.Filled.Euro" Color="Color.Success" Size="Size.Small" />
```

---

## Navigation

### MudTabs / MudTab
**Docs**: https://mudblazor.com/components/tabs

```razor
<MudTabs Elevation="0" Rounded="false" ApplyEffectsToContainer="true"
         @bind-ActivePanelIndex="_tabIndex">
    <MudTabPanel Text="Overview" Icon="@Icons.Material.Filled.BarChart">
        @* content *@
    </MudTabPanel>
    <MudTabPanel Text="Transactions">
        @* content *@
    </MudTabPanel>
</MudTabs>
```

---

### MudBreadcrumbs
**Docs**: https://mudblazor.com/components/breadcrumbs

```razor
<MudBreadcrumbs Items="_breadcrumbs" Separator=">" />
@code {
    List<BreadcrumbItem> _breadcrumbs =
    [
        new("Home", "/"),
        new("Accounts", "/accounts"),
        new("Cash 1", null, disabled: true),
    ];
}
```

---

### MudPagination
**Docs**: https://mudblazor.com/components/pagination

```razor
<MudPagination Count="_pageCount" @bind-Selected="_page"
               Color="Color.Primary" BoundaryCount="1" MiddleCount="3" />
```

---

### MudStepper / MudStep
**Docs**: https://mudblazor.com/components/stepper

```razor
<MudStepper @bind-ActiveIndex="_step" Linear="true">
    <MudStep Title="Basic info">
        @* step content *@
    </MudStep>
    <MudStep Title="Confirm">
        @* step content *@
    </MudStep>
</MudStepper>
```

---

## Menus & Popovers

### MudMenu / MudMenuItem
**Docs**: https://mudblazor.com/components/menu

```razor
@* Icon-activated menu (no activator wrapper needed) *@
<MudMenu Icon="@Icons.Material.Filled.MoreVert" Dense="true"
         AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight">
    <MudMenuItem Icon="@Icons.Material.Filled.Edit" OnClick="Edit">Edit</MudMenuItem>
    <MudDivider />
    <MudMenuItem Icon="@Icons.Material.Filled.Delete" OnClick="Delete">Delete</MudMenuItem>
</MudMenu>

@* Custom activator *@
<MudMenu @ref="_menu" Dense="true" AnchorOrigin="Origin.BottomLeft">
    <ActivatorContent>
        <MudButton EndIcon="@Icons.Material.Filled.KeyboardArrowDown"
                   Variant="Variant.Outlined" OnClick="@(_menu!.OpenMenuAsync)">
            Options
        </MudButton>
    </ActivatorContent>
    <ChildContent>
        <MudMenuItem>Item 1</MudMenuItem>
    </ChildContent>
</MudMenu>
```
`AutoClose="false"` on `MudMenuItem` keeps the menu open after click (useful for multi-select menus).

---

### MudPopover
**Docs**: https://mudblazor.com/components/popover

```razor
<MudPopover Open="_open" AnchorOrigin="Origin.BottomLeft" TransformOrigin="Origin.TopLeft"
            Paper="true" Elevation="4" Class="pa-3">
    <MudText>Popover content</MudText>
</MudPopover>
```

---

## Feedback

### MudAlert
**Docs**: https://mudblazor.com/components/alert

```razor
<MudAlert Severity="Severity.Warning" Variant="Variant.Filled" Dense="true" ShowCloseIcon="true">
    This is a warning message.
</MudAlert>
```
`Severity`: `Normal`, `Info`, `Success`, `Warning`, `Error`.

---

### MudSnackbar (ISnackbar)
**Docs**: https://mudblazor.com/components/snackbar

```razor
@* In markup (required once, usually in MainLayout): *@
<MudSnackbarProvider />
```
```csharp
// In code-behind:
[Inject] public ISnackbar Snackbar { get; set; } = default!;

Snackbar.Add("Saved!", Severity.Success);
Snackbar.Add("Error saving", Severity.Error, cfg => cfg.ShowCloseIcon = true);
```
`ISnackbar` is registered by `builder.Services.AddMudServices()`.

---

### MudDialog / IDialogService
**Docs**: https://mudblazor.com/components/dialog

```razor
@* Dialog definition (separate .razor file) *@
<MudDialog>
    <TitleContent>Confirm delete</TitleContent>
    <DialogContent>
        <MudText>Are you sure?</MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Error" Variant="Variant.Filled" OnClick="Submit">Delete</MudButton>
    </DialogActions>
</MudDialog>
@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    void Cancel() => MudDialog.Cancel();
    void Submit() => MudDialog.Close(DialogResult.Ok(true));
}
```
```csharp
// Caller:
[Inject] public IDialogService DialogService { get; set; } = default!;

var result = await DialogService.ShowAsync<ConfirmDialog>("Confirm");
if (!result.Canceled) { /* proceed */ }
```
Always declare `<MudDialogProvider />` in `MainLayout`.

---

### MudSkeleton
**Docs**: https://mudblazor.com/components/skeleton

```razor
@if (_loading)
{
    <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="40px" Width="100%" Class="mb-2" />
    <MudSkeleton SkeletonType="SkeletonType.Text" Width="60%" />
}
```

---

### MudProgressLinear
**Docs**: https://mudblazor.com/components/progresslinear

```razor
<MudProgressLinear Color="Color.Primary" Indeterminate="_loading" Class="mb-4" />
<MudProgressLinear Color="Color.Success" Value="_progress" Max="100" />
```

---

### MudProgressCircular
**Docs**: https://mudblazor.com/components/progresscircular

```razor
<MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Medium" />
```

---

### MudOverlay
**Docs**: https://mudblazor.com/components/overlay

```razor
<MudOverlay Visible="_busy" DarkBackground="true" Absolute="true" ZIndex="9999">
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
</MudOverlay>
```

---

### MudTooltip
**Docs**: https://mudblazor.com/components/tooltip

```razor
<MudTooltip Text="Click to add" Placement="Placement.Top">
    <MudIconButton Icon="@Icons.Material.Filled.Add" />
</MudTooltip>
```

---

## Miscellaneous

### MudScrollToTop
**Docs**: https://mudblazor.com/components/scrolltotop

```razor
<MudScrollToTop>
    <MudFab Color="Color.Primary" StartIcon="@Icons.Material.Filled.ArrowUpward" Size="Size.Small" />
</MudScrollToTop>
```

---

### MudVirtualize
**Docs**: https://mudblazor.com/components/virtualize  
(Thin wrapper over Blazor's built-in `<Virtualize>` with MudBlazor styling.)

```razor
<MudList T="Row" Dense="true">
    <Virtualize Items="_allRows" Context="row">
        <MudListItem>@row.Description</MudListItem>
    </Virtualize>
</MudList>
```

---

### MudColorPicker
**Docs**: https://mudblazor.com/components/colorpicker

```razor
<MudColorPicker @bind-Text="_hexColor" Label="Colour"
                Variant="Variant.Outlined" ColorPickerMode="ColorPickerMode.HEX" />
```

---

### MudSpacer
**Docs**: — (inline layout helper)

```razor
<MudAppBar>
    <MudText>Left</MudText>
    <MudSpacer />
    <MudIconButton Icon="@Icons.Material.Filled.AccountCircle" />
</MudAppBar>
```
`MudSpacer` is simply `flex: 1 1 auto` — useful inside any flex container.

---

## Frequently needed enums / types (quick ref)

| Type | Values |
|---|---|
| `Variant` | `Text`, `Outlined`, `Filled` |
| `Size` | `Small`, `Medium`, `Large` |
| `Color` | `Default`, `Primary`, `Secondary`, `Tertiary`, `Success`, `Warning`, `Error`, `Info`, `Surface`, `Inherit` |
| `Typo` | `h1`–`h6`, `subtitle1`, `subtitle2`, `body1`, `body2`, `button`, `caption`, `overline` |
| `Adornment` | `None`, `Start`, `End` |
| `Margin` | `None`, `Dense`, `Normal` |
| `Origin` | `TopLeft`, `TopCenter`, `TopRight`, `CenterLeft`, `Center`, `CenterRight`, `BottomLeft`, `BottomCenter`, `BottomRight` |
| `Severity` | `Normal`, `Info`, `Success`, `Warning`, `Error` |
| `SelectionMode` | `SingleSelection`, `ToggleSelection`, `MultiSelection` |
| `Placement` | `Top`, `Bottom`, `Left`, `Right`, `Start`, `End` |
| `DrawerVariant` | `Temporary`, `Persistent`, `Mini`, `Responsive` |

---

## Common class utilities (MudBlazor CSS helpers)

```
pa-{0-16}   px-{n}   py-{n}   pt-{n}   pb-{n}   pl-{n}   pr-{n}  — padding
ma-{n}      mx-{n}   my-{n}   mt-{n}   mb-{n}   ml-{n}   mr-{n}  — margin
d-flex   d-block   d-none   d-inline-flex
flex-grow-1   flex-shrink-0   flex-column   flex-row   flex-wrap
align-center   align-start   align-end
justify-center   justify-space-between   justify-end
gap-{1-8}
```

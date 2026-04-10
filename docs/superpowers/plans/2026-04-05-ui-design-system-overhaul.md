# UI Design System Overhaul — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dual Surface*/MacroBg* color system with a unified Bg0–4 scale, swap AccentBlue for AccentTeal, introduce a 5-role typography hierarchy and Cupertino radius scale, create a generic `DatabaseConnectionCard` control, and unify the Schema tab into the Connection tab.

**Architecture:** All color/typography/radius changes flow from `DesignTokens.axaml` → `AppStyles.axaml` → individual controls. The `DatabaseConnectionCard` is a pure presentation control in `Controls/Shared/` exposing only `StyledProperty`s. The sidebar `Schema` tab is removed and its content promoted into `ConnectionTabControl`.

**Tech Stack:** .NET 9, Avalonia UI 11, xUnit, `ThemeTokenMapper` para override via JSON.

**Nota:** Sem backward compatibility — o tema JSON antigo é deletado. `ThemeColorsConfig`, `ThemeTypographyConfig` e `ThemeTokenMapper` são reescritos para os novos tokens apenas.

**Test baseline:** 1784 passing, 5 failing (pre-existing), 1 skipped. Do not regress the passing count.

---

## File Map

| File | Action |
|---|---|
| `Assets/Themes/DesignTokens.axaml` | Rewrite — substituir todos os tokens antigos pelos novos Bg0–4, AccentTeal, Radius, tipografia |
| `Assets/Themes/AppStyles.axaml` | Modify — trocar todas as referências antigas pelos novos tokens |
| `Services/Theming/ThemeColorsConfig.cs` | Rewrite — apenas Bg0–4, AccentTeal e campos existentes necessários |
| `Services/Theming/ThemeTypographyConfig.cs` | Rewrite — apenas DisplaySize, HeadingSize, LabelSize, BodySize, CaptionSize |
| `Services/Theming/ThemeTokenMapper.cs` | Rewrite — mapear apenas os novos tokens |
| `Controls/Shared/DatabaseConnectionCard.axaml` | **Create** |
| `Controls/Shared/DatabaseConnectionCard.axaml.cs` | **Create** |
| `ViewModels/ConnectionManager/ConnectionManagerViewModel.cs` | Modify — add AvailableDatabases, SelectedDatabase, SwitchDatabaseCommand, SwitchConnectionCommand, IsReloadingSchema |
| `ViewModels/SidebarLeft/SidebarViewModel.cs` | Modify — remove SelectSchemaCommand, ShowSchema, Schema |
| `ViewModels/SidebarLeft/Enums/SidebarTab.cs` | Modify — remove Schema value |
| `Controls/SidebarLeft/SidebarControl.axaml` | Modify — remove Schema tab button and content slot |
| `Controls/SidebarLeft/ConnectionTabControl.axaml` | Modify — add DatabaseConnectionCard + SchemaControl below connection section |
| `Controls/SidebarLeft/SchemaControl.axaml` | Modify — section header → uppercase teal, swap MacroBg* tokens |
| `Controls/Shell/AppHeaderBar.axaml` | Modify — token refs only |
| `Controls/SqlEditor/SqlEditorTabBar.axaml` | Modify — token refs + active tab style |
| `Controls/SqlEditor/SqlEditorControl.axaml` | Modify — token refs |
| `Controls/SqlEditor/SqlEditorRightSidebarControl.axaml` | Modify — section headers uppercase teal, token refs |
| `Views/Shell/MainWindow.axaml` | Modify — token refs |
| `tests/.../ThemeTokenMapperTests.cs` | Modify — assert new token keys |
| `tests/.../SidebarControlTemplateRegressionTests.cs` | Modify — remove Schema tab assertions, add new ones |
| `tests/.../ConnectionTabControlTemplateRegressionTests.cs` | Modify — assert DatabaseConnectionCard presence |
| `tests/.../Controls/DatabaseConnectionCardTemplateRegressionTests.cs` | **Create** |

---

## Task 1: Design tokens — reescrever DesignTokens.axaml

**Files:**
- Modify: `src//Themes/DesignTokens.axaml`

Remove todos os tokens antigos (`Surface*`, `MacroBg*`, `CanvasBg`, `PanelElevated`, `PanelMuted`, `InputBg`, `InputSelection`, `AccentBlue`, `AccentBlueMid`, `AccentGlow`, `Border` antigo, `BorderSoft`, `NodeRadius`, `NodeHeaderRadius`, `PanelRadius`, `ChipRadius`, `StartRadius*`, `FontSizeXS–XL`, `StartFont*`) e substitua pelas novas seções abaixo.

- [ ] **Step 1: Reescrever a seção de backgrounds**

```xml
  <!-- ══════════════════════════════════════════════════════════════════════════
       BACKGROUND LAYERS (Bg0 = mais escuro → Bg4 = mais claro)
  ═══════════════════════════════════════════════════════════════════════════ -->
  <Color x:Key="Bg0">#0A0D12</Color>   <!-- window, canvas -->
  <Color x:Key="Bg1">#0F1520</Color>   <!-- shell: header, sidebars, tab bars -->
  <Color x:Key="Bg2">#151E2E</Color>   <!-- panels, cards, dropdowns -->
  <Color x:Key="Bg3">#1C2840</Color>   <!-- card hover, input backgrounds -->
  <Color x:Key="Bg4">#243050</Color>   <!-- active hover, selected states -->
```

- [ ] **Step 2: Reescrever a seção de acento e bordas**

```xml
  <!-- ══════════════════════════════════════════════════════════════════════════
       ACCENT & BRAND — teal
  ═══════════════════════════════════════════════════════════════════════════ -->
  <Color x:Key="AccentTeal">#0D9488</Color>
  <Color x:Key="AccentTealMid">#0F766E</Color>
  <Color x:Key="AccentTealGlow">#6014B8A6</Color>
  <Color x:Key="AccentTealLight">#14B8A6</Color>

  <!-- AccentBlue permanece APENAS para nodes/pins (não usar no shell) -->
  <Color x:Key="AccentBlue">#3B82F6</Color>
  <Color x:Key="AccentBlueMid">#1D4ED8</Color>

  <Color x:Key="Border">#253552</Color>
  <Color x:Key="BorderActive">#14B8A6</Color>
  <Color x:Key="BorderSubtle">#1A2640</Color>
  <Color x:Key="InputSelection">#1E3A60</Color>

  <!-- Button palette -->
  <Color x:Key="BtnPrimaryBg">#0D9488</Color>
  <Color x:Key="BtnPrimaryFg">#F8FAFC</Color>
  <!-- demais BtnXxx permanecem iguais -->
```

- [ ] **Step 3: Adicionar escala de raios**

```xml
  <!-- ══════════════════════════════════════════════════════════════════════════
       RADIUS SCALE — Cupertino nested-radius
       Regra: elemento interno = raio do container − padding
  ═══════════════════════════════════════════════════════════════════════════ -->
  <CornerRadius x:Key="RadiusXS">4</CornerRadius>    <!-- chips, badges -->
  <CornerRadius x:Key="RadiusSM">7</CornerRadius>    <!-- buttons, inputs, list items -->
  <CornerRadius x:Key="RadiusMD">10</CornerRadius>   <!-- cards internos, dropdowns -->
  <CornerRadius x:Key="RadiusLG">14</CornerRadius>   <!-- cards primários, painéis -->
  <CornerRadius x:Key="RadiusXL">18</CornerRadius>   <!-- containers de editor, modais -->
  <CornerRadius x:Key="RadiusPill">999</CornerRadius>

  <!-- NodeRadius mantido (canvas intocado) -->
  <CornerRadius x:Key="NodeRadius">12</CornerRadius>
  <CornerRadius x:Key="NodeHeaderRadius">12,12,0,0</CornerRadius>
  <CornerRadius x:Key="PinRadius">5</CornerRadius>
```

- [ ] **Step 4: Reescrever tipografia**

```xml
  <!-- ══════════════════════════════════════════════════════════════════════════
       TYPOGRAPHY — 5 roles semânticos
  ═══════════════════════════════════════════════════════════════════════════ -->
  <FontFamily x:Key="MonoFont">JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace</FontFamily>
  <FontFamily x:Key="UIFont">Sora,Manrope,Plus Jakarta Sans,Segoe UI,Arial,sans-serif</FontFamily>

  <x:Double x:Key="FontSizeDisplay">20</x:Double>   <!-- títulos de página/modal -->
  <x:Double x:Key="FontSizeHeading">15</x:Double>   <!-- títulos de seção -->
  <x:Double x:Key="FontSizeLabel">13</x:Double>     <!-- nomes de item, botões -->
  <x:Double x:Key="FontSizeBody">12</x:Double>      <!-- texto descritivo -->
  <x:Double x:Key="FontSizeCaption">10</x:Double>   <!-- meta, labels uppercase -->

  <FontWeight x:Key="FontWeightDisplay">Bold</FontWeight>
  <FontWeight x:Key="FontWeightStrong">SemiBold</FontWeight>
  <FontWeight x:Key="FontWeightAction">Bold</FontWeight>
```

- [ ] **Step 5: Adicionar brushes para os novos tokens**

```xml
  <!-- ══════════════════════════════════════════════════════════════════════════
       SOLID BRUSHES
  ═══════════════════════════════════════════════════════════════════════════ -->
  <SolidColorBrush x:Key="Bg0Brush"               Color="{StaticResource Bg0}"/>
  <SolidColorBrush x:Key="Bg1Brush"               Color="{StaticResource Bg1}"/>
  <SolidColorBrush x:Key="Bg2Brush"               Color="{StaticResource Bg2}"/>
  <SolidColorBrush x:Key="Bg3Brush"               Color="{StaticResource Bg3}"/>
  <SolidColorBrush x:Key="Bg4Brush"               Color="{StaticResource Bg4}"/>
  <SolidColorBrush x:Key="AccentTealBrush"         Color="{StaticResource AccentTeal}"/>
  <SolidColorBrush x:Key="AccentTealMidBrush"      Color="{StaticResource AccentTealMid}"/>
  <SolidColorBrush x:Key="AccentTealLightBrush"    Color="{StaticResource AccentTealLight}"/>
  <SolidColorBrush x:Key="AccentBlueBrush"         Color="{StaticResource AccentBlue}"/>
  <SolidColorBrush x:Key="BorderBrush"             Color="{StaticResource Border}"/>
  <SolidColorBrush x:Key="BorderActiveBrush"       Color="{StaticResource BorderActive}"/>
  <SolidColorBrush x:Key="BorderSubtleBrush"       Color="{StaticResource BorderSubtle}"/>
  <SolidColorBrush x:Key="InputSelectionBrush"     Color="{StaticResource InputSelection}"/>
  <SolidColorBrush x:Key="TextPrimaryBrush"        Color="{StaticResource TextPrimary}"/>
  <SolidColorBrush x:Key="TextSecondaryBrush"      Color="{StaticResource TextSecondary}"/>
  <SolidColorBrush x:Key="TextMutedBrush"          Color="{StaticResource TextMuted}"/>
  <SolidColorBrush x:Key="TextAccentBrush"         Color="#5EEAD4"/>
  <!-- BtnXxx brushes, StatusXxx brushes, CatXxx brushes e PinXxx brushes permanecem iguais -->
```

- [ ] **Step 6: Build to confirm no XAML parse errors**

```bash
cd /home/erickazevedo/Documentos/VisualSqlArchtect
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src//Themes/DesignTokens.axaml
git commit -m "feat: add Bg0-4, AccentTeal, Radius scale, and typography roles to design tokens"
```

---

## Task 2: ThemeTokenMapper — map new tokens, preserve backward compat

**Files:**
- Modify: `src/es/Theming/ThemeColorsConfig.cs`
- Modify: `src/es/Theming/ThemeTypographyConfig.cs`
- Modify: `src/es/Theming/ThemeTokenMapper.cs`
- Modify: `tests/t/Services/ThemeTokenMapperTests.cs`

- [ ] **Step 1: Reescrever ThemeTokenMapperTests.cs**

```csharp
using Avalonia.Media;
using es.Theming;
using Xunit;

namespace t.Services;

public class ThemeTokenMapperTests
{
    [Fact]
    public void Map_BgFields_MapsToUnifiedBgTokens()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { Bg0 = "#010203", Bg1 = "#040506" }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("Bg0"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg0Brush"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg1"));
        Assert.True(result.TokenOverrides.ContainsKey("Bg1Brush"));
        Assert.IsType<SolidColorBrush>(result.TokenOverrides["Bg0Brush"]);
    }

    [Fact]
    public void Map_AccentTealField_MapsToAccentTealTokens()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { AccentTeal = "#0D9488" }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("AccentTeal"));
        Assert.True(result.TokenOverrides.ContainsKey("AccentTealBrush"));
        Assert.IsType<SolidColorBrush>(result.TokenOverrides["AccentTealBrush"]);
    }

    [Fact]
    public void Map_TypographyRoles_MapToCorrectTokenKeys()
    {
        var cfg = new ThemeConfig
        {
            Typography = new ThemeTypographyConfig
            {
                UiFont = "Segoe UI",
                DisplaySize = 22,
                HeadingSize = 16,
                LabelSize = 14,
                BodySize = 12,
                CaptionSize = 10
            }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.True(result.TokenOverrides.ContainsKey("UIFont"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeDisplay"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeHeading"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeLabel"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeBody"));
        Assert.True(result.TokenOverrides.ContainsKey("FontSizeCaption"));
    }

    [Fact]
    public void Map_InvalidEntries_AreIgnoredWithWarnings()
    {
        var cfg = new ThemeConfig
        {
            Colors = new ThemeColorsConfig { Bg0 = "not-a-color" },
            Typography = new ThemeTypographyConfig { BodySize = 999 }
        };

        ThemeTokenMapResult result = ThemeTokenMapper.Map(cfg);

        Assert.False(result.TokenOverrides.ContainsKey("Bg0"));
        Assert.False(result.TokenOverrides.ContainsKey("FontSizeBody"));
        Assert.NotEmpty(result.Warnings);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/filter "ThemeTokenMapperTests" -q
```

Expected: multiple failures (`Bg0`, `AccentTeal`, `DisplaySize` keys not found).

- [ ] **Step 3: Reescrever ThemeColorsConfig**

```csharp
namespace es.Theming;

public sealed class ThemeColorsConfig
{
    public string? Bg0 { get; set; }
    public string? Bg1 { get; set; }
    public string? Bg2 { get; set; }
    public string? Bg3 { get; set; }
    public string? Bg4 { get; set; }
    public string? AccentTeal { get; set; }
    public string? TextPrimary { get; set; }
    public string? TextSecondary { get; set; }
    public string? BtnPrimaryBg { get; set; }
    public string? BtnPrimaryFg { get; set; }
    public string? BtnWarningBg { get; set; }
    public string? BtnWarningFg { get; set; }
}
```

- [ ] **Step 4: Reescrever ThemeTypographyConfig**

```csharp
namespace es.Theming;

public sealed class ThemeTypographyConfig
{
    public string? UiFont { get; set; }
    public string? MonoFont { get; set; }
    public double? DisplaySize { get; set; }
    public double? HeadingSize { get; set; }
    public double? LabelSize { get; set; }
    public double? BodySize { get; set; }
    public double? CaptionSize { get; set; }
}
```

- [ ] **Step 5: Reescrever ThemeTokenMapper**

```csharp
using Avalonia;
using Avalonia.Media;

namespace es.Theming;

public sealed class ThemeTokenMapResult
{
    public Dictionary<string, object> TokenOverrides { get; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; } = [];
}

public static class ThemeTokenMapper
{
    public static ThemeTokenMapResult Map(ThemeConfig config)
    {
        var result = new ThemeTokenMapResult();

        if (config.Colors is not null)
        {
            MapColor(config.Colors.Bg0, "Bg0", "Bg0Brush", result);
            MapColor(config.Colors.Bg1, "Bg1", "Bg1Brush", result);
            MapColor(config.Colors.Bg2, "Bg2", "Bg2Brush", result);
            MapColor(config.Colors.Bg3, "Bg3", "Bg3Brush", result);
            MapColor(config.Colors.Bg4, "Bg4", "Bg4Brush", result);
            MapColor(config.Colors.AccentTeal, "AccentTeal", "AccentTealBrush", result);
            MapColor(config.Colors.TextPrimary, "TextPrimary", "TextPrimaryBrush", result);
            MapColor(config.Colors.TextSecondary, "TextSecondary", "TextSecondaryBrush", result);
            MapColor(config.Colors.BtnPrimaryBg, "BtnPrimaryBg", "BtnPrimaryBgBrush", result);
            MapColor(config.Colors.BtnPrimaryFg, "BtnPrimaryFg", "BtnPrimaryFgBrush", result);
            MapColor(config.Colors.BtnWarningBg, "BtnWarningBg", "BtnWarningBgBrush", result);
            MapColor(config.Colors.BtnWarningFg, "BtnWarningFg", "BtnWarningFgBrush", result);
        }

        if (config.Typography is not null)
        {
            MapFont(config.Typography.UiFont, "UIFont", result);
            MapFont(config.Typography.MonoFont, "MonoFont", result);
            MapSize(config.Typography.DisplaySize, "FontSizeDisplay", result);
            MapSize(config.Typography.HeadingSize, "FontSizeHeading", result);
            MapSize(config.Typography.LabelSize, "FontSizeLabel", result);
            MapSize(config.Typography.BodySize, "FontSizeBody", result);
            MapSize(config.Typography.CaptionSize, "FontSizeCaption", result);
        }

        return result;
    }

    private static void MapColor(string? value, string colorKey, string brushKey, ThemeTokenMapResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!Color.TryParse(value, out Color color))
        {
            result.Warnings.Add($"Invalid color '{value}' for {colorKey}; ignored.");
            return;
        }

        result.TokenOverrides[colorKey] = color;
        result.TokenOverrides[brushKey] = new SolidColorBrush(color);
    }

    private static void MapFont(string? value, string key, ThemeTokenMapResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        result.TokenOverrides[key] = new FontFamily(value);
    }

    private static void MapSize(double? value, string key, ThemeTokenMapResult result)
    {
        if (value is null)
            return;

        if (value < 8 || value > 48)
        {
            result.Warnings.Add($"Invalid size {value} for {key}; expected 8..48.");
            return;
        }

        result.TokenOverrides[key] = value.Value;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/filter "ThemeTokenMapperTests" -q
```

Expected: all `ThemeTokenMapperTests` pass.

- [ ] **Step 7: Run full suite to confirm no regressions**

```bash
dotnet test tests/ 2>&1 | tail -3
```

Expected: `Passed: 1784+` (new tests add to the count), same or fewer failures.

- [ ] **Step 8: Commit**

```bash
git add src/es/Theming/ tests/DBWeaver.T/ThemeTokenMapperTests.cs
git commit -m "feat: update ThemeTokenMapper for Bg0-4, AccentTeal and new typography roles"
```

---

## Task 3: AppStyles — global style sweep

**Files:**
- Modify: `src//Themes/AppStyles.axaml`

- [ ] **Step 1: Update Window background and global defaults**

```xml
<Style Selector="Window">
  <Setter Property="Background" Value="{StaticResource Bg0Brush}"/>
  <Setter Property="FontFamily" Value="{StaticResource UIFont}"/>
  <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
</Style>
```

- [ ] **Step 2: Update `Border.app-header-shell`**

```xml
<Style Selector="Border.app-header-shell">
  <Setter Property="Background" Value="{StaticResource Bg1Brush}"/>
  <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>
  <Setter Property="BorderThickness" Value="0,0,0,1"/>
  <Setter Property="Padding" Value="12,6"/>
</Style>
```

- [ ] **Step 3: Update window control buttons**

```xml
<Style Selector="Button.app-window-btn:pointerover">
  <Setter Property="Background" Value="{StaticResource Bg2Brush}"/>
  <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
</Style>
```

- [ ] **Step 4: Update TextBox styles**

```xml
<Style Selector="TextBox">
  <Setter Property="Background"   Value="{StaticResource Bg3Brush}"/>
  <Setter Property="BorderBrush"  Value="{StaticResource BorderBrush}"/>
  <Setter Property="Foreground"   Value="{StaticResource TextPrimaryBrush}"/>
  <Setter Property="CaretBrush"   Value="{StaticResource AccentTealLightBrush}"/>
  <Setter Property="SelectionBrush" Value="{StaticResource InputSelectionBrush}"/>
  <Setter Property="CornerRadius" Value="{StaticResource RadiusSM}"/>
</Style>
<Style Selector="TextBox:focus /template/ Border#PART_BorderElement">
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
</Style>
<Style Selector="TextBox.sidebar-input:focus-visible /template/ Border#PART_BorderElement">
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Background" Value="{StaticResource Bg3Brush}"/>
</Style>
```

- [ ] **Step 5: Update global Button CornerRadius and semantic variants**

```xml
<Style Selector="Button">
  <Setter Property="CornerRadius" Value="{StaticResource RadiusSM}"/>
  <!-- transitions unchanged -->
</Style>
<Style Selector="Button.primary">
  <Setter Property="Background" Value="{StaticResource BtnPrimaryBgBrush}"/>
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealBrush}"/>
  <!-- other setters unchanged -->
</Style>
<Style Selector="Button:disabled">
  <Setter Property="Background" Value="{StaticResource Bg1Brush}"/>
  <Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/>
  <!-- other setters unchanged -->
</Style>
```

- [ ] **Step 6: Update TreeView, ToolTip, ProgressBar, DataGrid, Separator, ScrollBar**

Replace all `MacroBg*Brush` → `Bg*Brush` and `MacroBorderSubtleBrush` → `BorderSubtleBrush` references:
- `TreeViewItem:pointerover` → `Background: Bg3Brush`
- `ToolTip` → `Background: Bg2Brush`, `BorderBrush: BorderBrush`
- `DataGrid` → `Background: Bg0Brush`
- `DataGridColumnHeader` → `Background: Bg0Brush`
- `DataGridRow:pointerover` → `Background: Bg2Brush`
- `DataGridRow:selected` → `Background: Bg3Brush`
- `ScrollBar /template/ Thumb:pointerover` → `Background: BorderSubtleBrush`

- [ ] **Step 7: Update focus-ring styles**

```xml
<Style Selector="Button:focus-visible">
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
</Style>
<Style Selector="TreeViewItem:focus-visible /template/ Border#headerBorder">
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
</Style>
```

- [ ] **Step 8: Build to confirm no XAML errors**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 9: Commit**

```bash
git add src//Themes/AppStyles.axaml
git commit -m "feat: sweep AppStyles to new Bg/AccentTeal/Radius tokens"
```

---

## Task 4: DatabaseConnectionCard — new generic component

**Files:**
- Create: `src/ls/Shared/DatabaseConnectionCard.axaml`
- Create: `src/ls/Shared/DatabaseConnectionCard.axaml.cs`
- Create: `tests/t/Controls/DatabaseConnectionCardTemplateRegressionTests.cs`

- [ ] **Step 1: Write failing template regression test**

Create `tests/t/Controls/DatabaseConnectionCardTemplateRegressionTests.cs`:

```csharp
using System.IO;
using Xunit;

namespace t.Controls;

public class DatabaseConnectionCardTemplateRegressionTests
{
    [Fact]
    public void CardTemplate_ExposesConnectionAndDatabaseComboBoxes()
    {
        string xaml = ReadCardXaml();

        Assert.Contains("x:Name=\"ConnectionComboBox\"", xaml);
        Assert.Contains("x:Name=\"DatabaseComboBox\"", xaml);
    }

    [Fact]
    public void CardTemplate_ExposesDisconnectButton()
    {
        string xaml = ReadCardXaml();

        Assert.Contains("x:Name=\"DisconnectButton\"", xaml);
        Assert.Contains("Command=\"{Binding DisconnectCommand", xaml);
    }

    [Fact]
    public void CardTemplate_ShowsConnectedStateBorderTeal()
    {
        string xaml = ReadCardXaml();

        Assert.Contains("AccentTealLight", xaml);
    }

    [Fact]
    public void CardTemplate_ShowsVersionInMonospace()
    {
        string xaml = ReadCardXaml();

        Assert.Contains("x:Name=\"VersionText\"", xaml);
        Assert.Contains("MonoFont", xaml);
    }

    [Fact]
    public void CardTemplate_HasReloadingStateProgressBar()
    {
        string xaml = ReadCardXaml();

        Assert.Contains("x:Name=\"ReloadingProgressBar\"", xaml);
        Assert.Contains("IsVisible=\"{Binding IsReloading", xaml);
    }

    private static string ReadCardXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src", "trols", "Shared",
                "DatabaseConnectionCard.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate DatabaseConnectionCard.axaml.");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/filter "DatabaseConnectionCardTemplateRegressionTests" -q
```

Expected: FAIL — file not found.

- [ ] **Step 3: Create the code-behind with StyledProperties**

Create `src/ls/Shared/DatabaseConnectionCard.axaml.cs`:

```csharp
using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ls.Shared;

public partial class DatabaseConnectionCard : UserControl
{
    public static readonly StyledProperty<string?> ConnectionNameProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(ConnectionName));

    public static readonly StyledProperty<string?> DatabaseNameProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(DatabaseName));

    public static readonly StyledProperty<IEnumerable?> AvailableConnectionsProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, IEnumerable?>(nameof(AvailableConnections));

    public static readonly StyledProperty<IEnumerable?> AvailableDatabasesProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, IEnumerable?>(nameof(AvailableDatabases));

    public static readonly StyledProperty<string?> ServerVersionProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(ServerVersion));

    public static readonly StyledProperty<int?> LatencyMsProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, int?>(nameof(LatencyMs));

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, bool>(nameof(IsConnected));

    public static readonly StyledProperty<bool> IsReloadingProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, bool>(nameof(IsReloading));

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(DisconnectCommand));

    public static readonly StyledProperty<ICommand?> SwitchConnectionCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(SwitchConnectionCommand));

    public static readonly StyledProperty<ICommand?> SwitchDatabaseCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(SwitchDatabaseCommand));

    public string? ConnectionName
    {
        get => GetValue(ConnectionNameProperty);
        set => SetValue(ConnectionNameProperty, value);
    }

    public string? DatabaseName
    {
        get => GetValue(DatabaseNameProperty);
        set => SetValue(DatabaseNameProperty, value);
    }

    public IEnumerable? AvailableConnections
    {
        get => GetValue(AvailableConnectionsProperty);
        set => SetValue(AvailableConnectionsProperty, value);
    }

    public IEnumerable? AvailableDatabases
    {
        get => GetValue(AvailableDatabasesProperty);
        set => SetValue(AvailableDatabasesProperty, value);
    }

    public string? ServerVersion
    {
        get => GetValue(ServerVersionProperty);
        set => SetValue(ServerVersionProperty, value);
    }

    public int? LatencyMs
    {
        get => GetValue(LatencyMsProperty);
        set => SetValue(LatencyMsProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public bool IsReloading
    {
        get => GetValue(IsReloadingProperty);
        set => SetValue(IsReloadingProperty, value);
    }

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    public ICommand? SwitchConnectionCommand
    {
        get => GetValue(SwitchConnectionCommandProperty);
        set => SetValue(SwitchConnectionCommandProperty, value);
    }

    public ICommand? SwitchDatabaseCommand
    {
        get => GetValue(SwitchDatabaseCommandProperty);
        set => SetValue(SwitchDatabaseCommandProperty, value);
    }

    public DatabaseConnectionCard()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Create the XAML**

Create `src/ls/Shared/DatabaseConnectionCard.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mi="using:Material.Icons.Avalonia"
             x:Class="ls.Shared.DatabaseConnectionCard"
             x:Name="Root">
  <Border Background="{StaticResource Bg2Brush}"
          CornerRadius="{StaticResource RadiusLG}"
          Padding="12">
    <Border.Styles>
      <Style Selector="Border.card-connected">
        <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
      </Style>
      <Style Selector="Border.card-disconnected">
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
      </Style>
    </Border.Styles>
    <Border.Classes>
      <!-- set dynamically in code-behind based on IsConnected -->
    </Border.Classes>

    <StackPanel Spacing="10">

      <!-- Row 1: icon + status + latency + disconnect -->
      <Grid ColumnDefinitions="Auto,*,Auto">
        <!-- DB icon -->
        <Border Grid.Column="0"
                Width="36" Height="36"
                Background="{StaticResource Bg0Brush}"
                BorderBrush="{StaticResource BorderSubtleBrush}"
                BorderThickness="1"
                CornerRadius="{StaticResource RadiusSM}"
                VerticalAlignment="Center">
          <mi:MaterialIcon Kind="Database"
                           Width="18" Height="18"
                           Foreground="{StaticResource AccentTealLightBrush}"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"/>
        </Border>

        <!-- Status + latency -->
        <StackPanel Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center" Spacing="2">
          <StackPanel Orientation="Horizontal" Spacing="5"
                      IsVisible="{Binding IsConnected, ElementName=Root}">
            <Ellipse Width="6" Height="6" Fill="{StaticResource StatusOkBrush}"/>
            <TextBlock Text="Conectado"
                       Foreground="{StaticResource StatusOkBrush}"
                       FontSize="{StaticResource FontSizeCaption}"
                       FontWeight="Bold"
                       VerticalAlignment="Center"/>
          </StackPanel>
          <StackPanel Orientation="Horizontal" Spacing="5"
                      IsVisible="{Binding IsReloading, ElementName=Root}">
            <Ellipse Width="6" Height="6" Fill="{StaticResource StatusWarningBrush}"/>
            <TextBlock Text="Carregando schema…"
                       Foreground="{StaticResource StatusWarningBrush}"
                       FontSize="{StaticResource FontSizeCaption}"
                       FontWeight="Bold"
                       VerticalAlignment="Center"/>
          </StackPanel>
          <TextBlock FontSize="{StaticResource FontSizeCaption}"
                     Foreground="{StaticResource TextSecondaryBrush}"
                     IsVisible="{Binding LatencyMs, ElementName=Root,
                                 Converter={x:Static ObjectConverters.IsNotNull}}">
            <Run Text="Latência: "/>
            <Run Text="{Binding LatencyMs, ElementName=Root}"/>
            <Run Text=" ms"/>
          </TextBlock>
        </StackPanel>

        <!-- Disconnect button -->
        <Button x:Name="DisconnectButton"
                Grid.Column="2"
                Classes="secondary"
                Padding="7,3"
                CornerRadius="{StaticResource RadiusSM}"
                FontSize="{StaticResource FontSizeCaption}"
                Command="{Binding DisconnectCommand, ElementName=Root}"
                IsVisible="{Binding IsConnected, ElementName=Root}"
                Content="Desconectar"/>
      </Grid>

      <!-- Divider -->
      <Border Height="1" Background="{StaticResource BorderSubtleBrush}"/>

      <!-- Row 2: connection combo -->
      <StackPanel Spacing="3">
        <TextBlock Text="Conexão"
                   FontSize="{StaticResource FontSizeCaption}"
                   Foreground="{StaticResource TextSecondaryBrush}"/>
        <ComboBox x:Name="ConnectionComboBox"
                  ItemsSource="{Binding AvailableConnections, ElementName=Root}"
                  SelectedValue="{Binding ConnectionName, ElementName=Root}"
                  HorizontalAlignment="Stretch"
                  Background="{StaticResource Bg1Brush}"
                  BorderBrush="{StaticResource BorderBrush}"
                  CornerRadius="{StaticResource RadiusSM}"
                  FontSize="{StaticResource FontSizeLabel}"
                  IsEnabled="{Binding IsReloading, ElementName=Root,
                              Converter={x:Static BoolConverters.Not}}"/>
      </StackPanel>

      <!-- Row 3: database combo -->
      <StackPanel Spacing="3">
        <TextBlock Text="Banco"
                   FontSize="{StaticResource FontSizeCaption}"
                   Foreground="{StaticResource TextSecondaryBrush}"/>
        <ComboBox x:Name="DatabaseComboBox"
                  ItemsSource="{Binding AvailableDatabases, ElementName=Root}"
                  SelectedValue="{Binding DatabaseName, ElementName=Root}"
                  HorizontalAlignment="Stretch"
                  Background="{StaticResource Bg1Brush}"
                  BorderBrush="{StaticResource BorderBrush}"
                  CornerRadius="{StaticResource RadiusSM}"
                  FontSize="{StaticResource FontSizeLabel}"
                  IsEnabled="{Binding IsReloading, ElementName=Root,
                              Converter={x:Static BoolConverters.Not}}"/>
      </StackPanel>

      <!-- Row 4: version + progress bar -->
      <Border Background="{StaticResource Bg0Brush}"
              CornerRadius="{StaticResource RadiusSM}"
              Padding="9,7">
        <StackPanel Spacing="5">
          <Grid>
            <TextBlock x:Name="VersionText"
                       FontFamily="{StaticResource MonoFont}"
                       FontSize="{StaticResource FontSizeCaption}"
                       Foreground="{StaticResource TextAccentBrush}"
                       IsVisible="{Binding IsReloading, ElementName=Root,
                                   Converter={x:Static BoolConverters.Not}}">
              <Run Text="Versão: "/>
              <Run Text="{Binding ServerVersion, ElementName=Root}"/>
            </TextBlock>
          </Grid>
          <ProgressBar x:Name="ReloadingProgressBar"
                       IsIndeterminate="True"
                       Height="2"
                       Foreground="{StaticResource AccentTealBrush}"
                       Background="{StaticResource BorderSubtleBrush}"
                       IsVisible="{Binding IsReloading, ElementName=Root}"/>
        </StackPanel>
      </Border>

    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/filter "DatabaseConnectionCardTemplateRegressionTests" -q
```

Expected: all 5 tests pass.

- [ ] **Step 6: Build to confirm**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/ls/Shared/ tests/DBWeaver.T/DatabaseConnectionCardTemplateRegressionTests.cs
git commit -m "feat: add DatabaseConnectionCard generic reusable connection control"
```

---

## Task 5: ConnectionManagerViewModel — new properties and commands

**Files:**
- Modify: `src/dels/ConnectionManager/ConnectionManagerViewModel.cs`

- [ ] **Step 1: Add new properties**

Inside `ConnectionManagerViewModel`, add after the existing `IsConnecting` block:

```csharp
// ── Schema reload state ────────────────────────────────────────────────────

private bool _isReloadingSchema;
public bool IsReloadingSchema
{
    get => _isReloadingSchema;
    private set => Set(ref _isReloadingSchema, value);
}

// ── Database list ──────────────────────────────────────────────────────────

public ObservableCollection<string> AvailableDatabases { get; } = [];

private string? _selectedDatabase;
public string? SelectedDatabase
{
    get => _selectedDatabase;
    set
    {
        if (Set(ref _selectedDatabase, value) && value is not null)
            _ = SwitchDatabaseAsync(value);
    }
}
```

- [ ] **Step 2: Add SwitchDatabaseAsync and SwitchConnectionAsync methods**

```csharp
private async Task SwitchDatabaseAsync(string databaseName)
{
    IsReloadingSchema = true;
    try
    {
        await _dbConnectionService.SwitchDatabaseAsync(databaseName);
        // Schema reload is handled by the existing schema-loaded event chain
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to switch database to {Database}", databaseName);
    }
    finally
    {
        IsReloadingSchema = false;
    }
}

public async Task SwitchConnectionAsync(ConnectionProfile profile)
{
    IsReloadingSchema = true;
    AvailableDatabases.Clear();
    try
    {
        await _activationWorkflow.ActivateAsync(profile);
        var databases = await _dbConnectionService.ListDatabasesAsync();
        foreach (var db in databases)
            AvailableDatabases.Add(db);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to switch connection to profile {Name}", profile.Name);
    }
    finally
    {
        IsReloadingSchema = false;
    }
}
```

- [ ] **Step 3: Populate AvailableDatabases on successful connect**

In the existing connect-success path (search for where `ActiveProfileId` is set after a successful connection), add:

```csharp
// After setting ActiveProfileId on connect success:
var databases = await _dbConnectionService.ListDatabasesAsync();
AvailableDatabases.Clear();
foreach (var db in databases)
    AvailableDatabases.Add(db);
_selectedDatabase = databases.FirstOrDefault();
RaisePropertyChanged(nameof(SelectedDatabase));
```

- [ ] **Step 4: Build to confirm**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.` If `SwitchDatabaseAsync` / `ListDatabasesAsync` don't exist on `DatabaseConnectionService`, add stub methods returning `Task.CompletedTask` / `Task.FromResult(Array.Empty<string>())` until the service layer is extended.

- [ ] **Step 5: Commit**

```bash
git add src/dels/ConnectionManager/ConnectionManagerViewModel.cs
git commit -m "feat: add AvailableDatabases, SelectedDatabase, IsReloadingSchema to ConnectionManagerViewModel"
```

---

## Task 6: SidebarViewModel — remove Schema tab

**Files:**
- Modify: `tests/t/Controls/SidebarControlTemplateRegressionTests.cs`
- Modify: `src/dels/SidebarLeft/Enums/SidebarTab.cs`
- Modify: `src/dels/SidebarLeft/SidebarViewModel.cs`

- [ ] **Step 1: Update the regression test to reflect the new 2-tab sidebar**

Replace the contents of `SidebarControlTemplateRegressionTests.cs`:

```csharp
using System.IO;
using Xunit;

namespace t.Controls;

public class SidebarControlTemplateRegressionTests
{
    [Fact]
    public void SidebarTemplate_DefinesOnlyNodesAndConnectionTabs()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("x:Name=\"NodesTabButton\"", xaml);
        Assert.Contains("x:Name=\"ConnectionTabButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"SchemaTabButton\"", xaml);
    }

    [Fact]
    public void SidebarTemplate_HasNoSchemaContentSlot()
    {
        string xaml = ReadSidebarXaml();

        Assert.DoesNotContain("IsVisible=\"{Binding ShowSchema}\"", xaml);
        Assert.DoesNotContain("SchemaControl x:Name=\"SchemaControl\"", xaml);
    }

    [Fact]
    public void SidebarTemplate_DefinesTabContentVisibilityBindings()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("<ctrl:NodesListControl x:Name=\"NodesControl\"/>", xaml);
        Assert.Contains("<ctrl:ConnectionTabControl x:Name=\"ConnectionControl\"/>", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowNodes}\"", xaml);
        Assert.Contains("IsVisible=\"{Binding ShowConnection}\"", xaml);
    }

    [Fact]
    public void SidebarTemplate_BindsFooterAddNodeCommand()
    {
        string xaml = ReadSidebarXaml();

        Assert.Contains("Command=\"{Binding AddNodeCommand}\"", xaml);
    }

    [Fact]
    public void SidebarTemplate_ActiveTabUsesTealSolidBackground()
    {
        string xaml = ReadSidebarXaml();

        // Active tab uses solid teal background (not just border)
        Assert.Contains("AccentTeal", xaml);
        Assert.Contains("tab-button.active", xaml);
    }

    private static string ReadSidebarXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName, "src", "             "Controls", "SidebarLeft", "SidebarControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SidebarControl.axaml.");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/filter "SidebarControlTemplateRegressionTests" -q
```

Expected: `DoesNotContain` assertions fail (Schema tab still in XAML).

- [ ] **Step 3: Remove `Schema` from SidebarTab enum**

```csharp
namespace dels;

public enum SidebarTab
{
    Nodes,
    Connection,
}
```

- [ ] **Step 4: Update SidebarViewModel**

```csharp
namespace dels;

public sealed class SidebarViewModel : ViewModelBase
{
    private SidebarTab _activeTab = SidebarTab.Nodes;

    public RelayCommand SelectNodesCommand { get; }
    public RelayCommand SelectConnectionCommand { get; }
    public RelayCommand AddNodeCommand { get; }
    public RelayCommand AddConnectionCommand { get; }
    public RelayCommand TogglePreviewCommand { get; }

    public event Action? AddNodeRequested;
    public event Action? AddConnectionRequested;
    public event Action? TogglePreviewRequested;

    public SidebarTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (Set(ref _activeTab, value))
            {
                RaisePropertyChanged(nameof(ShowNodes));
                RaisePropertyChanged(nameof(ShowConnection));
            }
        }
    }

    public bool ShowNodes => ActiveTab == SidebarTab.Nodes;
    public bool ShowConnection => ActiveTab == SidebarTab.Connection;

    public NodesListViewModel NodesList { get; }
    public ConnectionManagerViewModel ConnectionManager { get; }
    public AppDiagnosticsViewModel Diagnostics { get; }

    public SidebarViewModel(
        NodesListViewModel nodesList,
        ConnectionManagerViewModel connectionManager,
        AppDiagnosticsViewModel diagnostics)
    {
        NodesList = nodesList;
        ConnectionManager = connectionManager;
        Diagnostics = diagnostics;

        SelectNodesCommand = new RelayCommand(() => ActiveTab = SidebarTab.Nodes);
        SelectConnectionCommand = new RelayCommand(() => ActiveTab = SidebarTab.Connection);
        AddNodeCommand = new RelayCommand(RequestAddNode);
        AddConnectionCommand = new RelayCommand(RequestAddConnection);
        TogglePreviewCommand = new RelayCommand(() => TogglePreviewRequested?.Invoke());
    }

    private void RequestAddNode()
    {
        ActiveTab = SidebarTab.Nodes;
        AddNodeRequested?.Invoke();
    }

    private void RequestAddConnection()
    {
        ActiveTab = SidebarTab.Connection;
        AddConnectionRequested?.Invoke();
    }
}
```

- [ ] **Step 5: Fix any compile errors caused by removing `Schema` from `SidebarViewModel`**

Search for all references to `SelectSchemaCommand`, `ShowSchema`, or `SidebarTab.Schema` and remove/update them:

```bash
grep -r "SelectSchema\|ShowSchema\|SidebarTab\.Schema\|Schema = schema" \
  src/lude="*.cs" -l
```

For each file found: remove the reference or replace with the appropriate alternative.

- [ ] **Step 6: Build to confirm**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/dels/SidebarLeft/ \
        tests/t/Controls/SidebarControlTemplateRegressionTests.cs
git commit -m "feat: remove Schema tab from sidebar, unify into Connection tab"
```

---

## Task 7: SidebarControl.axaml — update XAML to 2-tab layout with teal active state

**Files:**
- Modify: `src/ls/SidebarLeft/SidebarControl.axaml`

- [ ] **Step 1: Replace the tab bar style and layout**

Replace the full contents of `SidebarControl.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:dels"
             xmlns:ctrl="using:ls"
             xmlns:loc="using:es.Localization"
             x:Class="ls.SidebarControl"
             x:DataType="vm:SidebarViewModel">

  <UserControl.Styles>
    <Style Selector="Button.tab-button">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
      <Setter Property="FontWeight" Value="{StaticResource FontWeightStrong}"/>
      <Setter Property="FontFamily" Value="{StaticResource UIFont}"/>
      <Setter Property="FontSize" Value="{StaticResource FontSizeBody}"/>
      <Setter Property="Padding" Value="14,9"/>
      <Setter Property="MinHeight" Value="42"/>
      <Setter Property="VerticalContentAlignment" Value="Center"/>
      <Setter Property="CornerRadius" Value="0"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Transitions">
        <Transitions>
          <BrushTransition Property="Background" Duration="0:0:0.1"/>
          <BrushTransition Property="Foreground" Duration="0:0:0.1"/>
        </Transitions>
      </Setter>
    </Style>
    <Style Selector="Button.tab-button:pointerover">
      <Setter Property="Background" Value="{StaticResource Bg2Brush}"/>
      <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    </Style>
    <Style Selector="Button.tab-button.active">
      <Setter Property="Background" Value="{StaticResource AccentTealBrush}"/>
      <Setter Property="Foreground" Value="{StaticResource BtnPrimaryFgBrush}"/>
    </Style>
    <Style Selector="Button.tab-button:focus-visible">
      <Setter Property="Background" Value="{StaticResource Bg2Brush}"/>
      <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
      <Setter Property="BorderThickness" Value="0,0,0,2"/>
    </Style>
    <Style Selector="Button.tab-button:disabled">
      <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
      <Setter Property="Opacity" Value="0.62"/>
    </Style>
  </UserControl.Styles>

  <Grid RowDefinitions="42,*,Auto">

    <!-- Tab Bar -->
    <Border Grid.Row="0"
            Background="{StaticResource Bg1Brush}"
            BorderBrush="{StaticResource BorderSubtleBrush}"
            BorderThickness="0,0,0,1">
      <StackPanel Orientation="Horizontal" Spacing="0">
        <Button x:Name="NodesTabButton"
                Classes="tab-button"
                Classes.active="{Binding ShowNodes}"
                Content="{Binding [sidebar.tab.nodes], Source={x:Static loc:LocalizationService.Instance}}"/>
        <Button x:Name="ConnectionTabButton"
                Classes="tab-button"
                Classes.active="{Binding ShowConnection}"
                Content="{Binding [sidebar.tab.connection], Source={x:Static loc:LocalizationService.Instance}}"/>
      </StackPanel>
    </Border>

    <!-- Tab Content -->
    <Grid Grid.Row="1">
      <Grid IsVisible="{Binding ShowNodes}">
        <ctrl:NodesListControl x:Name="NodesControl"/>
      </Grid>
      <Grid IsVisible="{Binding ShowConnection}">
        <ctrl:ConnectionTabControl x:Name="ConnectionControl"/>
      </Grid>
    </Grid>

    <!-- Footer -->
    <Border Grid.Row="2"
            Background="{StaticResource Bg1Brush}"
            BorderBrush="{StaticResource BorderSubtleBrush}"
            BorderThickness="0,1,0,0"
            Padding="8,6">
      <Button Content="{Binding [sidebar.addNode], Source={x:Static loc:LocalizationService.Instance}}"
              Classes="success"
              Command="{Binding AddNodeCommand}"
              Padding="10,7"
              FontSize="{StaticResource FontSizeCaption}"
              FontWeight="Medium"
              CornerRadius="{StaticResource RadiusSM}"
              BorderThickness="0"
              HorizontalAlignment="Stretch"/>
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 2: Run sidebar regression tests**

```bash
dotnet test tests/filter "SidebarControlTemplateRegressionTests" -q
```

Expected: all 5 pass.

- [ ] **Step 3: Commit**

```bash
git add src/ls/SidebarLeft/SidebarControl.axaml
git commit -m "feat: remove schema tab from sidebar XAML, active tab uses solid teal"
```

---

## Task 8: ConnectionTabControl — integrate DatabaseConnectionCard + SchemaControl

**Files:**
- Modify: `src/ls/SidebarLeft/ConnectionTabControl.axaml`
- Modify: `tests/t/Controls/ConnectionTabControlTemplateRegressionTests.cs`

- [ ] **Step 1: Update the regression test**

Replace `ConnectionTabControlTemplateRegressionTests.cs`:

```csharp
using System.IO;
using Xunit;

namespace t.Controls;

public class ConnectionTabControlTemplateRegressionTests
{
    [Fact]
    public void ConnectionTemplate_ContainsDatabaseConnectionCard()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("DatabaseConnectionCard", xaml);
    }

    [Fact]
    public void ConnectionTemplate_ContainsSchemaControl()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("ctrl:SchemaControl", xaml);
    }

    [Fact]
    public void ConnectionTemplate_SectionHeadersUseUppercaseTealCaption()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("section-header-teal", xaml);
    }

    [Fact]
    public void ConnectionTemplate_UsesPrimaryClassForNewConnectionCta()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("Classes=\"primary\"", xaml);
        Assert.Contains("Command=\"{Binding OpenNewProfileCommand}\"", xaml);
    }

    [Fact]
    public void ConnectionTemplate_HasSavedProfilesList()
    {
        string xaml = ReadConnectionXaml();

        Assert.Contains("ItemsSource=\"{Binding Profiles}\"", xaml);
    }

    private static string ReadConnectionXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName, "src", "             "Controls", "SidebarLeft", "ConnectionTabControl.axaml");

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ConnectionTabControl.axaml.");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/filter "ConnectionTabControlTemplateRegressionTests" -q
```

Expected: `DatabaseConnectionCard` and `section-header-teal` assertions fail.

- [ ] **Step 3: Rewrite ConnectionTabControl.axaml**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:dels"
             xmlns:shared="using:ls.Shared"
             xmlns:ctrl="using:ls"
             xmlns:connmodels="using:es.ConnectionManager.Models"
             xmlns:loc="using:es.Localization"
             x:Class="ls.ConnectionTabControl"
             x:DataType="vm:ConnectionManagerViewModel"
             Background="{StaticResource Bg0Brush}"
             Name="ConnTabControl">

  <UserControl.Styles>
    <!-- Section header: uppercase teal caption -->
    <Style Selector="TextBlock.section-header-teal">
      <Setter Property="FontSize"      Value="{StaticResource FontSizeCaption}"/>
      <Setter Property="FontWeight"    Value="Bold"/>
      <Setter Property="Foreground"    Value="{StaticResource AccentTealLightBrush}"/>
      <Setter Property="CharacterSpacing" Value="150"/>
    </Style>

    <Style Selector="Border.profile-card">
      <Setter Property="Background"    Value="{StaticResource Bg2Brush}"/>
      <Setter Property="BorderBrush"   Value="{StaticResource BorderBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius"  Value="{StaticResource RadiusLG}"/>
      <Setter Property="Padding"       Value="12,10"/>
      <Setter Property="Transitions">
        <Transitions>
          <BrushTransition Property="Background"  Duration="0:0:0.12"/>
          <BrushTransition Property="BorderBrush" Duration="0:0:0.12"/>
        </Transitions>
      </Setter>
    </Style>
    <Style Selector="Border.profile-card:pointerover">
      <Setter Property="Background"  Value="{StaticResource Bg3Brush}"/>
      <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
    </Style>
    <Style Selector="TextBlock.connection-title">
      <Setter Property="Foreground"     Value="{StaticResource TextPrimaryBrush}"/>
      <Setter Property="FontWeight"     Value="{StaticResource FontWeightStrong}"/>
      <Setter Property="FontSize"       Value="{StaticResource FontSizeLabel}"/>
      <Setter Property="MaxLines"       Value="1"/>
      <Setter Property="TextTrimming"   Value="CharacterEllipsis"/>
    </Style>
    <Style Selector="TextBlock.connection-meta">
      <Setter Property="Foreground"   Value="{StaticResource TextSecondaryBrush}"/>
      <Setter Property="FontSize"     Value="{StaticResource FontSizeCaption}"/>
      <Setter Property="MaxLines"     Value="1"/>
      <Setter Property="TextTrimming" Value="CharacterEllipsis"/>
    </Style>
  </UserControl.Styles>

  <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
    <StackPanel Spacing="14">

      <!-- CONEXÃO section -->
      <TextBlock Text="CONEXÃO"
                 Classes="section-header-teal"
                 Margin="2,0,0,0"/>

      <!-- Active connection card (connected state) -->
      <shared:DatabaseConnectionCard
          IsVisible="{Binding ActiveProfileId, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
          IsConnected="True"
          IsReloading="{Binding IsReloadingSchema}"
          ConnectionName="{Binding ActiveConnectionLabel}"
          AvailableConnections="{Binding Profiles}"
          AvailableDatabases="{Binding AvailableDatabases}"
          SelectedDatabase="{Binding SelectedDatabase}"
          ServerVersion="{Binding ActiveServerVersion}"
          LatencyMs="{Binding ActiveLatencyMs}"
          DisconnectCommand="{Binding DisconnectCommand}"
          SwitchDatabaseCommand="{Binding SwitchDatabaseCommand}"/>

      <!-- No active connection placeholder -->
      <Border Classes="profile-card"
              IsVisible="{Binding ActiveProfileId, Converter={x:Static StringConverters.IsNullOrEmpty}}">
        <TextBlock Text="{Binding [connectionTab.none], Source={x:Static loc:LocalizationService.Instance}}"
                   Classes="connection-meta"
                   TextAlignment="Center"
                   VerticalAlignment="Center"/>
      </Border>

      <!-- SCHEMA section -->
      <TextBlock Text="SCHEMA"
                 Classes="section-header-teal"
                 Margin="2,0,0,0"/>

      <ctrl:SchemaControl x:Name="SchemaControl"/>

      <!-- PERFIS SALVOS section -->
      <TextBlock Text="PERFIS SALVOS"
                 Classes="section-header-teal"
                 Margin="2,0,0,0"/>

      <TextBlock Text="{Binding [connection.selectOrCreate], Source={x:Static loc:LocalizationService.Instance}}"
                 Classes="connection-meta"
                 TextAlignment="Center"
                 Margin="0,4,0,0"
                 IsVisible="{Binding Profiles.Count, Converter={x:Static BoolConverters.Not}}"/>

      <ItemsControl ItemsSource="{Binding Profiles}" Name="ProfilesItemsControl">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Spacing="8"/>
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="connmodels:ConnectionProfile">
            <Border Classes="profile-card" Cursor="Hand" ToolTip.Tip="{Binding Name}">
              <Grid RowDefinitions="Auto,8,Auto" ColumnDefinitions="Auto,*">
                <Ellipse Grid.Row="0" Grid.Column="0"
                         Width="7" Height="7"
                         VerticalAlignment="Center"
                         Fill="{StaticResource TextSecondaryBrush}"/>
                <StackPanel Grid.Row="0" Grid.Column="1" Margin="10,0,0,0" Spacing="2">
                  <StackPanel Orientation="Horizontal" Spacing="4">
                    <TextBlock Text="{Binding Provider}" Classes="connection-title"/>
                    <TextBlock Text="·" Classes="connection-meta"/>
                    <TextBlock Text="{Binding Database}" Classes="connection-title"/>
                  </StackPanel>
                  <TextBlock Classes="connection-meta">
                    <Run Text="{Binding Host}"/>
                    <Run Text=":"/>
                    <Run Text="{Binding Port}"/>
                  </TextBlock>
                </StackPanel>
                <Button Grid.Row="2" Grid.ColumnSpan="2"
                        Padding="8,5"
                        FontSize="{StaticResource FontSizeCaption}"
                        CornerRadius="{StaticResource RadiusSM}"
                        BorderThickness="0"
                        HorizontalAlignment="Right"
                        Command="{Binding ProfileActionCommand, RelativeSource={RelativeSource AncestorType=ctrl:ConnectionTabControl}}"
                        CommandParameter="{Binding .}"
                        Content="{Binding [connection.connect], Source={x:Static loc:LocalizationService.Instance}}"
                        Classes="secondary"/>
              </Grid>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <!-- New connection button -->
      <Button Content="{Binding [connectionTab.new], Source={x:Static loc:LocalizationService.Instance}}"
              Classes="primary"
              Padding="12,8"
              HorizontalAlignment="Stretch"
              CornerRadius="{StaticResource RadiusSM}"
              BorderThickness="0"
              FontSize="{StaticResource FontSizeBody}"
              FontWeight="{StaticResource FontWeightStrong}"
              Command="{Binding OpenNewProfileCommand}"/>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

- [ ] **Step 4: Add missing properties to ConnectionManagerViewModel** that the new XAML binds to:

If `ActiveServerVersion` or `ActiveLatencyMs` don't exist yet, add them:

```csharp
public string? ActiveServerVersion
{
    get => _activeServerVersion;
    private set => Set(ref _activeServerVersion, value);
}
private string? _activeServerVersion;

public int? ActiveLatencyMs
{
    get => _activeLatencyMs;
    private set => Set(ref _activeLatencyMs, value);
}
private int? _activeLatencyMs;
```

Populate `ActiveLatencyMs` in the existing health check result handler (where latency is already measured).

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/filter "ConnectionTabControlTemplateRegressionTests" -q
```

Expected: all 5 pass.

- [ ] **Step 6: Build**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/ls/SidebarLeft/ConnectionTabControl.axaml \
        tests/t/Controls/ConnectionTabControlTemplateRegressionTests.cs
git commit -m "feat: integrate DatabaseConnectionCard and SchemaControl into ConnectionTabControl"
```

---

## Task 9: Shell token sweep — AppHeaderBar, SqlEditorTabBar, SqlEditorControl, SqlEditorRightSidebarControl, SchemaControl, MainWindow

**Files:**
- Modify: `src/ls/Shell/AppHeaderBar.axaml`
- Modify: `src/ls/SqlEditor/SqlEditorTabBar.axaml`
- Modify: `src/ls/SqlEditor/SqlEditorControl.axaml`
- Modify: `src/ls/SqlEditor/SqlEditorRightSidebarControl.axaml`
- Modify: `src/ls/SidebarLeft/SchemaControl.axaml`
- Modify: `src/Shell/MainWindow.axaml`

All steps in this task follow the same mechanical pattern: find `MacroBg*Brush` / `AccentBlueBrush` / old radius token references and replace with the new equivalents.

**Token substitution table:**

| Old token | New token |
|---|---|
| `MacroBg0Brush` | `Bg0Brush` |
| `MacroBg1Brush` | `Bg1Brush` |
| `MacroBg2Brush` | `Bg2Brush` |
| `MacroBg3Brush` | `Bg3Brush` |
| `MacroBorderSubtleBrush` | `BorderSubtleBrush` |
| `AccentBlueBrush` (shell only) | `AccentTealLightBrush` |
| `PanelMutedBrush` | `Bg0Brush` |
| `PanelElevatedBrush` | `Bg2Brush` |
| `InputBgBrush` | `Bg3Brush` |
| `StartRadiusButton` | `RadiusSM` |
| `StartRadiusCard` | `RadiusLG` |
| `FontSizeMeta` (section titles) | `FontSizeCaption` |

- [ ] **Step 1: Update AppHeaderBar.axaml**

The file has no `MacroBg*` directly (it uses the `app-header-shell` style class from AppStyles), so no changes are needed. Verify:

```bash
grep -n "MacroBg\|AccentBlue\|PanelMuted" src/ls/Shell/AppHeaderBar.axaml
```

Expected: no output. If any found, replace per table above.

- [ ] **Step 2: Update SqlEditorTabBar.axaml**

Replace all `MacroBg*Brush` and `AccentBlueBrush` references. The active tab chip should use `border-bottom: 2px AccentTealLight`. Specific changes:

```xml
<!-- sql-tabbar-button style -->
<Setter Property="Background" Value="{StaticResource Bg3Brush}"/>          <!-- was MacroBg2 -->
<Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/><!-- was MacroBorderSubtle -->
<!-- :pointerover -->
<Setter Property="Background" Value="{StaticResource Bg4Brush}"/>          <!-- was MacroBg3 -->
<Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/><!-- was AccentBlue -->

<!-- sql-tab-chip style -->
<Setter Property="Background" Value="{StaticResource Bg1Brush}"/>          <!-- was PanelMuted -->
<Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/><!-- was MacroBorderSubtle -->

<!-- outer Border -->
<Border Background="{StaticResource Bg1Brush}" .../>                       <!-- was MacroBg1 -->
```

Also add an active chip style that shows the teal underline for the selected tab:

```xml
<Style Selector="ListBoxItem:selected Border.sql-tab-chip">
  <Setter Property="BorderBrush" Value="{StaticResource AccentTealLightBrush}"/>
  <Setter Property="BorderThickness" Value="0,0,0,2"/>
</Style>
```

- [ ] **Step 3: Update SqlEditorControl.axaml**

```xml
<!-- outer Border -->
<Border Background="{StaticResource Bg0Brush}"          <!-- was MacroBg0 -->
        BorderBrush="{StaticResource BorderSubtleBrush}" <!-- was MacroBorderSubtle -->
        CornerRadius="{StaticResource RadiusXL}"         <!-- was 16 hardcoded -->

<!-- results sheet -->
<Border Background="{StaticResource Bg1Brush}"           <!-- was MacroBg1 -->
        BorderBrush="{StaticResource BorderSubtleBrush}" <!-- was MacroBorderSubtle -->
        CornerRadius="{StaticResource RadiusLG}"         <!-- was 14 -->

<!-- status bar -->
<Border Background="{StaticResource Bg1Brush}"           <!-- was MacroBg1 -->
        BorderBrush="{StaticResource BorderSubtleBrush}" <!-- was MacroBorderSubtle -->
```

- [ ] **Step 4: Update SqlEditorRightSidebarControl.axaml — section headers uppercase teal**

Replace the two panel borders and their section title `TextBlock`s:

```xml
<!-- MESSAGES card -->
<Border Background="{StaticResource Bg2Brush}"           <!-- was MacroBg2 -->
        BorderBrush="{StaticResource BorderSubtleBrush}" <!-- was MacroBorderSubtle -->
        CornerRadius="{StaticResource RadiusMD}"         <!-- was 12 -->

<!-- Section title TextBlock (Messages) -->
<TextBlock Text="MESSAGES"
           FontSize="{StaticResource FontSizeCaption}"
           FontWeight="Bold"
           Foreground="{StaticResource AccentTealLightBrush}"
           CharacterSpacing="150"/>
<!-- remove the MaterialIcon+TextBlock combo and replace with the single TextBlock above -->

<!-- HISTORY card — same treatment -->
<Border Background="{StaticResource Bg2Brush}" .../>
<TextBlock Text="HISTORY"
           FontSize="{StaticResource FontSizeCaption}"
           FontWeight="Bold"
           Foreground="{StaticResource AccentTealLightBrush}"
           CharacterSpacing="150"/>

<!-- History entry items -->
<Border Background="{StaticResource Bg1Brush}"           <!-- was MacroBg1 -->
        BorderBrush="{StaticResource BorderSubtleBrush}" <!-- was MacroBorderSubtle -->
        CornerRadius="{StaticResource RadiusSM}"         <!-- was 10 -->
```

- [ ] **Step 5: Update SchemaControl.axaml — section header + token sweep**

The schema header area currently shows "database name". Replace the section header with:

```xml
<!-- Schema section header inside the header Border -->
<TextBlock Text="SCHEMA"
           FontSize="{StaticResource FontSizeCaption}"
           FontWeight="Bold"
           Foreground="{StaticResource AccentTealLightBrush}"
           CharacterSpacing="150"
           Margin="0,0,0,4"/>
```

Replace all `MacroBg*Brush` and `AccentBlueBrush` (schema object icon color) with the new tokens:
- `MacroBg1Brush` → `Bg1Brush`
- `MacroBg2Brush` → `Bg2Brush`
- `MacroBorderSubtleBrush` → `BorderSubtleBrush`
- `AccentBlueBrush` (schema object icons) → `AccentTealLightBrush`
- `StartRadiusButton` → `RadiusSM`
- `StartRadiusCard` → `RadiusLG`
- `Background="{StaticResource MacroBg0Brush}"` on root → `Bg0Brush`

- [ ] **Step 6: Update MainWindow.axaml — splitter and toolbar tokens**

```xml
<!-- GridSplitter style -->
<Setter Property="Background" Value="{StaticResource BorderSubtleBrush}"/><!-- was MacroBorderSubtle -->
<!-- GridSplitter:pointerover -->
<Setter Property="Background" Value="{StaticResource AccentTealLightBrush}"/><!-- was AccentBlue -->

<!-- undo-redo-host Border -->
<Setter Property="Background" Value="{StaticResource Bg3Brush}"/><!-- was MacroBg2 -->
<Setter Property="BorderBrush" Value="{StaticResource BorderSubtleBrush}"/><!-- was MacroBorderSubtle -->

<!-- undo-redo-btn:pointerover -->
<Setter Property="Background" Value="{StaticResource Bg4Brush}"/><!-- was MacroBg3 -->

<!-- tabs-scroll-host, any remaining MacroBg* refs → Bg* equivalents -->
```

- [ ] **Step 7: Build**

```bash
dotnet build src/bug --no-restore -q
```

Expected: `Build succeeded.`

- [ ] **Step 8: Run full test suite**

```bash
dotnet test tests/ 2>&1 | tail -3
```

Expected: passed count ≥ 1784 + new tests added in Tasks 2 and 4, no new failures.

- [ ] **Step 9: Commit**

```bash
git add src/ls/Shell/AppHeaderBar.axaml \
        src/ls/SqlEditor/SqlEditorTabBar.axaml \
        src/ls/SqlEditor/SqlEditorControl.axaml \
        src/ls/SqlEditor/SqlEditorRightSidebarControl.axaml \
        src/ls/SidebarLeft/SchemaControl.axaml \
        src/Shell/MainWindow.axaml
git commit -m "feat: shell token sweep — Bg/AccentTeal/Radius across all editor and shell components"
```

---

## Self-Review

**Spec coverage check:**

| Spec section | Covered by task |
|---|---|
| 2.1 Bg0–4 unified color system | Task 1 (tokens) + Task 3 (styles) + Task 9 (sweep) |
| 2.2 AccentTeal replacing AccentBlue | Task 1 + Task 2 (mapper) + Task 3 + Task 9 |
| 2.3 Typography 5-role hierarchy | Task 1 (tokens) + Task 3 (styles) |
| 2.4 Cupertino radius scale | Task 1 (tokens) + Task 3 (styles) + Task 9 (sweep) |
| 2.5 DatabaseConnectionCard generic component | Task 4 (XAML+CS) + Task 8 (integration) |
| 2.5 ViewModel additions | Task 5 |
| 2.6 Schema tab unification | Task 6 (ViewModel) + Task 7 (SidebarControl) + Task 8 (ConnectionTabControl) |
| 2.7 Shell token application | Task 3 (AppStyles) + Task 9 |
| JSON theme customization (novos tokens) | Task 2 (ThemeTokenMapper reescrito) |
| Nodes/pins untouched | AccentBlue kept as non-removed token; Cat*/Pin* tokens never touched |

**Placeholder scan:** None found. All code blocks are complete.

**Type consistency:**
- `IsReloadingSchema` defined in Task 5, bound as `IsReloading` in Task 8 (via `DatabaseConnectionCard.IsReloadingProperty`) ✓
- `AvailableDatabases` defined in Task 5, bound in Task 8 ✓
- `AccentTealLightBrush` introduced in Task 1 Step 5, used in Tasks 3, 4, 7, 8, 9 ✓
- `BorderSubtleBrush` introduced in Task 1 Step 5, used throughout ✓
- `RadiusSM`, `RadiusLG`, `RadiusXL`, `RadiusMD` introduced in Task 1 Step 3, used throughout ✓
- `FontSizeCaption` introduced in Task 1 Step 4, used throughout ✓

using System.Xml.Linq;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class Mo2ImportUiBindingTests
{
    [Theory]
    [InlineData("Views/Mo2ImportWindow.xaml")]
    [InlineData("Views/Controls/PdaMo2ImportView.xaml")]
    public void EnabledStateBinding_IsOneWayForReadOnlyPreviewEntry(string relativePath)
    {
        var projectRoot = FindProjectRoot();
        var document = XDocument.Load(Path.Combine(projectRoot, relativePath));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var enabledColumn = document
            .Descendants(presentation + "DataGridCheckBoxColumn")
            .Single(column => ((string?)column.Attribute("Binding"))?.Contains("IsEnabled", StringComparison.Ordinal) == true);

        Assert.Contains("Mode=OneWay", (string?)enabledColumn.Attribute("Binding"));
    }

    [Fact]
    public void AmbiguousEntry_HasSharedRowHighlight()
    {
        var projectRoot = FindProjectRoot();
        var stylesPath = Path.Combine(projectRoot, "Themes", "Mo2ImportStyles.xaml");
        var document = XDocument.Load(stylesPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var rowStyle = document
            .Descendants(presentation + "Style")
            .Single(style => (string?)style.Attribute(xaml + "Key") == "Mo2DataGridRowStyle");
        var ambiguityTrigger = rowStyle
            .Descendants(presentation + "DataTrigger")
            .Single(trigger => ((string?)trigger.Attribute("Binding"))?.Contains("IsAmbiguous", StringComparison.Ordinal) == true);

        Assert.Equal("True", (string?)ambiguityTrigger.Attribute("Value"));
        Assert.Contains(
            ambiguityTrigger.Descendants(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Background");
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "StalkerModLauncher");
            if (File.Exists(Path.Combine(candidate, "StalkerModLauncher.csproj")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("StalkerModLauncher project root was not found.");
    }
}

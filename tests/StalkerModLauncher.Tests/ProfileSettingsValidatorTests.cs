using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileSettingsValidatorTests
{
    private readonly ProfileSettingsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidSettings()
    {
        var result = _validator.Validate("Zona", @"bin_x64\xrEngine.exe", _ => false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsEmptyName()
    {
        var result = _validator.Validate("   ", @"bin\xr_3da.exe", _ => false);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Contains("название", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDuplicateName()
    {
        var result = _validator.Validate("Zona", @"bin\xr_3da.exe", name => name == "Zona");

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Contains("уже существует", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsExecutableOutsideProfile()
    {
        var result = _validator.Validate("Zona", @"..\outside.exe", _ => false);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Contains("must not leave", StringComparison.OrdinalIgnoreCase));
    }
}

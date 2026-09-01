using Api.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace Architecture;

public sealed class IdentityOptionsValidationTests
{
    [Fact]
    public void MissingAuthority_ShouldFailValidation_WithNamedError()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Audience"] = "orokanban-api"
                // Authority intentionally missing — ni Identity ni Oidc
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<IdentityOptions>()
            .Bind(config.GetSection(IdentityOptions.SectionName))
            .Validate(o =>
            {
                var hasAuthority = !string.IsNullOrWhiteSpace(o.Authority)
                    || !string.IsNullOrWhiteSpace(config["Oidc:Authority"])
                    || !string.IsNullOrWhiteSpace(config["Oidc__Authority"]);
                var hasAudience = !string.IsNullOrWhiteSpace(o.Audience)
                    || !string.IsNullOrWhiteSpace(config["Oidc:Audience"])
                    || !string.IsNullOrWhiteSpace(config["Oidc__Audience"]);
                return hasAuthority && hasAudience;
            }, "Oidc:Authority/Audience o Identity:Authority/Audience es requerido.")
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<IdentityOptions>>().Value);
        Assert.Contains("Oidc:Authority", ex.Message);
    }

    [Fact]
    public void MissingAudience_ShouldFailValidation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://localhost:5080"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<IdentityOptions>()
            .Bind(config.GetSection(IdentityOptions.SectionName))
            .Validate(o =>
            {
                var hasAuthority = !string.IsNullOrWhiteSpace(o.Authority)
                    || !string.IsNullOrWhiteSpace(config["Oidc:Authority"]);
                var hasAudience = !string.IsNullOrWhiteSpace(o.Audience)
                    || !string.IsNullOrWhiteSpace(config["Oidc:Audience"]);
                return hasAuthority && hasAudience;
            }, "Oidc:Authority/Audience o Identity:Authority/Audience es requerido.")
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<IdentityOptions>>().Value);
        Assert.Contains("Oidc:Authority", ex.Message);
    }

    [Fact]
    public void ValidConfiguration_ShouldPass()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://localhost:5080",
                ["Identity:Audience"] = "orokanban-api",
                ["Identity:ClientId"] = "orokanban-api"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<IdentityOptions>()
            .Bind(config.GetSection(IdentityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal("http://localhost:5080", opts.Authority);
    }
}
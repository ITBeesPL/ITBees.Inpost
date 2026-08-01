using ITBees.Inpost.Entities;

namespace ITBees.Inpost.Models;

public class InpostIntegrationSettingsVm
{
    public InpostIntegrationSettingsVm()
    {
    }

    public InpostIntegrationSettingsVm(InpostIntegrationSettings x)
    {
        ApiToken = x.ApiToken;
        OrganizationId = x.OrganizationId;
        UseSandbox = x.UseSandbox;
        Modified = x.Modified;
    }

    public string ApiToken { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public bool UseSandbox { get; set; }
    public DateTime? Modified { get; set; }
}

public class InpostIntegrationSettingsIm
{
    public string ApiToken { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public bool UseSandbox { get; set; }
}

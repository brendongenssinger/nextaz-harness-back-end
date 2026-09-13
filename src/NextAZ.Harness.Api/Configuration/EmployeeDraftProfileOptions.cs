namespace NextAZ.Harness.Api.Configuration;
public sealed class EmployeeDraftProfileOptions
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool Enabled { get; set; }
    public int ConfigurationVersion { get; set; } = 1;
}

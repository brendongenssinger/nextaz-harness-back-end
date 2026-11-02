#Requires -Version 7.0
Set-StrictMode -Version Latest

function Get-HarnessProductionEnvIssues {
    param([Parameter(Mandatory)][AllowEmptyCollection()][AllowEmptyString()][string[]]$Lines)
    $issues = [Collections.Generic.List[string]]::new()
    $values = @{}
    $lineNumber = 0
    foreach ($line in $Lines) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
        if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_.]*)=(.*)$') {
            $issues.Add("Linha ${lineNumber}: esperado KEY=valor raw; nao executar como shell.")
            continue
        }
        $key, $value = $Matches[1], $Matches[2]
        if ($values.ContainsKey($key)) { $issues.Add("Linha ${lineNumber}: chave duplicada."); continue }
        $values[$key] = $value
        if ($value -match 'CHANGE_ME|<[^>]+>|example\.invalid') {
            $issues.Add("Linha ${lineNumber}: placeholder nao preenchido.")
        }
        if ($value.StartsWith('"') -or $value.StartsWith("'")) {
            $issues.Add("Linha ${lineNumber}: nao usar aspas externas no formato raw.")
        }
    }
    $required = @(
        'AllowedHosts', 'Authentication__Authority', 'ConnectionStrings__Harness',
        'Harness__Providers__OpenAI__Endpoint', 'Harness__ProviderEncryption__KeyVaultKeyId'
    )
    foreach ($key in $required) {
        if ([string]::IsNullOrWhiteSpace([string]$values[$key])) { $issues.Add("Obrigatoria: $key") }
    }
    $expected = @{
        DOTNET_ENVIRONMENT = 'Production'; ASPNETCORE_ENVIRONMENT = 'Production'
        ASPNETCORE_HTTP_PORTS = '8080'; Authentication__Audience = 'nextaz-harness-api'
        Harness__Persistence__Provider = 'PostgreSql'; Harness__Simulation__Enabled = 'false'
        Harness__Providers__OpenAI__Enabled = 'true'
    }
    foreach ($key in $expected.Keys) {
        if ($values[$key] -cne $expected[$key]) { $issues.Add("Valor exigido pelo perfil atual: $key=$($expected[$key])") }
    }
    $httpsKeys = @('Authentication__Authority', 'Harness__Providers__OpenAI__Endpoint',
        'Harness__ProviderEncryption__KeyVaultKeyId')
    $imobiliariaEnabled = $values['Harness__Imobiliaria__Enabled'] -eq 'true'
    if ($imobiliariaEnabled) {
        foreach ($key in @('Harness__Imobiliaria__Integration__ApiBaseUrl',
            'Harness__Imobiliaria__Integration__TokenEndpoint', 'Harness__Imobiliaria__Integration__ClientSecret')) {
            if ([string]::IsNullOrWhiteSpace([string]$values[$key])) { $issues.Add("Obrigatoria: $key") }
        }
        foreach ($key in @('Harness__Imobiliaria__Integration__Enabled',
            'Harness__Imobiliaria__Integration__ClientId', 'Harness__Imobiliaria__Integration__Scope')) {
            $expectedValue = switch ($key) {
                'Harness__Imobiliaria__Integration__Enabled' { 'true' }
                'Harness__Imobiliaria__Integration__ClientId' { 'nextaz-harness' }
                default { 'imobiliaria.tools' }
            }
            if ($values[$key] -cne $expectedValue) { $issues.Add("Valor exigido pelo perfil atual: $key=$expectedValue") }
        }
        $httpsKeys += @('Harness__Imobiliaria__Integration__ApiBaseUrl',
            'Harness__Imobiliaria__Integration__TokenEndpoint')
    }
    $enabledVerticals = @(@('Imobiliaria', 'Cartorio', 'Receipts', 'WhatsAppTemplates') |
        Where-Object { $values["Harness__${_}__Enabled"] -eq 'true' })
    if ($enabledVerticals.Count -eq 0) {
        $issues.Add('Habilite ao menos um agente operacional em producao.')
    }
    foreach ($key in $httpsKeys) {
        $uri = $null
        if (-not [Uri]::TryCreate([string]$values[$key], [UriKind]::Absolute, [ref]$uri) -or
            $uri.Scheme -ne 'https' -or $uri.IsLoopback -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
            $issues.Add("HTTPS sem credenciais/query e fora de loopback exigido: $key")
        }
    }
    $keyId = $null
    if ([Uri]::TryCreate([string]$values['Harness__ProviderEncryption__KeyVaultKeyId'],
        [UriKind]::Absolute, [ref]$keyId) -and $keyId.AbsolutePath -notmatch '^/keys/[^/]+/[^/]+$') {
        $issues.Add('Harness__ProviderEncryption__KeyVaultKeyId deve apontar para uma versao exata da chave do Key Vault.')
    }
    if ([string]$values['AllowedHosts'] -match '\*|://|CHANGE_ME' -or
        -not (([string]$values['AllowedHosts']).Split(';') | Where-Object { $_ -and $_ -notin @('localhost','127.0.0.1') })) {
        $issues.Add('AllowedHosts precisa do hostname real, sem wildcard ou esquema.')
    }
    foreach ($vertical in @('Imobiliaria', 'Cartorio', 'Receipts', 'WhatsAppTemplates')) {
        if ($values["Harness__${vertical}__Enabled"] -ne 'true') { continue }
        $tenantKeys = @($values.Keys | Where-Object { $_ -match "^Harness__${vertical}__TenantIds__\d+$" })
        $tenantIds = [Collections.Generic.HashSet[Guid]]::new()
        if ($tenantKeys.Count -eq 0) { $issues.Add("Allowlist obrigatoria: Harness__${vertical}__TenantIds__0") }
        foreach ($key in $tenantKeys) {
            $tenantId = [Guid]::Empty
            if (-not [Guid]::TryParse($values[$key], [ref]$tenantId) -or
                $tenantId -eq [Guid]::Empty -or -not $tenantIds.Add($tenantId)) {
                $issues.Add("Tenant invalido ou duplicado em $key")
            }
        }
    }
    if ($values['Harness__Receipts__Enabled'] -eq 'true' -and
        [string]::IsNullOrWhiteSpace([string]$values['Authentication__ReceiptClassifierClientIds__0'])) {
        $issues.Add('Obrigatoria para recibos: Authentication__ReceiptClassifierClientIds__0')
    }
    if (@($values.Keys | Where-Object { $_ -match '^Harness__EmployeeDraftProfiles__|^Authentication__EmployeeDraftClientIds__' }).Count) {
        $issues.Add('Employee drafts permanecem SimulationOnly; nao promover esse piloto por env.')
    }
    if ($values.ContainsKey('ASPNETCORE_URLS')) { $issues.Add('Remova ASPNETCORE_URLS: este pacote usa a porta 8080.') }
    if ($values.ContainsKey('HARNESS_POSTGRES_TEST')) { $issues.Add('HARNESS_POSTGRES_TEST nao pertence ao ambiente de producao.') }
    try {
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        $connection.psbase.ConnectionString = [string]$values['ConnectionStrings__Harness']
        foreach ($key in @('Host', 'Database', 'Username', 'Password', 'Maximum Pool Size')) {
            if (-not $connection.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$connection[$key])) {
                $issues.Add("ConnectionStrings__Harness exige $key no perfil de preparacao.")
            }
        }
        if (-not $connection.ContainsKey('SSL Mode') -or $connection['SSL Mode'] -ne 'VerifyFull') {
            $issues.Add('ConnectionStrings__Harness exige SSL Mode=VerifyFull e CA confiavel.')
        }
        if ($connection.ContainsKey('Username') -and $connection['Username'] -in @('postgres','supabase_admin')) {
            $issues.Add('Use usuario dedicado de aplicacao, nao superusuario.')
        }
        if ($connection.ContainsKey('Include Error Detail') -and $connection['Include Error Detail'] -ne 'false') {
            $issues.Add('Desative Include Error Detail em producao.')
        }
        $pool = 0
        if (-not $connection.ContainsKey('Maximum Pool Size') -or
            -not [int]::TryParse([string]$connection['Maximum Pool Size'], [ref]$pool) -or $pool -lt 1) {
            $issues.Add('Maximum Pool Size deve ser um inteiro positivo definido no plano de conexoes.')
        }
    }
    catch { $issues.Add('ConnectionStrings__Harness invalida; valor omitido por seguranca.') }
    return $issues.ToArray()
}

Export-ModuleMember -Function Get-HarnessProductionEnvIssues

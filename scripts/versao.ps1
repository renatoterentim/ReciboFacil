# Imprime a versao do programa (VersaoApp em Dtos.cs)
$t = Get-Content -Raw "$PSScriptRoot\..\ReciboFacil.Shared\Dtos.cs"
if ($t -match 'VersaoApp\s*=\s*"([^"]+)"') { $matches[1] } else { "0.0.0" }

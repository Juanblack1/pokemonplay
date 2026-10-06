param(
    [Parameter(Mandatory=$true)][string]$Directory,
    [ValidateSet('Runtime','Installer')][string]$Kind='Runtime',
    [switch]$ListTargets
)
$ErrorActionPreference='Stop'

$taskRoot=[IO.Path]::GetFullPath($Directory)
if(!(Test-Path -LiteralPath $taskRoot -PathType Container)){throw 'Pasta do pacote Windows ausente.'}
# Only files built from this project's source may receive its publisher identity.
# Do not sign PKHeX, .NET, emulators or their cores with the project's certificate.
$taskRelativeTargets=if($Kind -eq 'Installer'){
    @('pokemon-play-win-x64-setup.exe')
}else{
    @('PokemonPlayRuntime/Pokemons Play.exe','PokemonPlayRuntime/Pokemons Play.dll',
      'PokemonPlayRuntime/PokemonPlayUpdater.exe','launcher/Pokemons Play.exe')
}
$taskBinaries=@(foreach($taskRelative in $taskRelativeTargets){
    $taskPath=Join-Path $taskRoot $taskRelative
    if(!(Test-Path -LiteralPath $taskPath -PathType Leaf)){throw "Binário próprio obrigatório ausente: $taskRelative"}
    Get-Item -LiteralPath $taskPath
})
if($ListTargets){$taskBinaries | ForEach-Object {$_.FullName};return}

$taskCertificateBase64=$env:POKEMONPLAY_SIGNING_PFX_BASE64
$taskCertificatePassword=$env:POKEMONPLAY_SIGNING_PFX_PASSWORD
if([string]::IsNullOrWhiteSpace($taskCertificateBase64) -or [string]::IsNullOrWhiteSpace($taskCertificatePassword)){
    throw 'A release exige os secrets POKEMONPLAY_SIGNING_PFX_BASE64 e POKEMONPLAY_SIGNING_PFX_PASSWORD.'
}

$taskPfx=Join-Path $taskRoot 'pokemonplay-signing.pfx'
$taskImported=@()
try{
    [IO.File]::WriteAllBytes($taskPfx,[Convert]::FromBase64String($taskCertificateBase64))
    $taskSecurePassword=ConvertTo-SecureString -String $taskCertificatePassword -AsPlainText -Force
    $taskImported=@(Import-PfxCertificate -FilePath $taskPfx -CertStoreLocation 'Cert:\CurrentUser\My' -Password $taskSecurePassword)
    $taskCertificates=@($taskImported | Where-Object {$_.HasPrivateKey})
    if($taskCertificates.Count -ne 1){throw 'O PFX deve conter exatamente um certificado com chave privada.'}
    $taskCertificate=$taskCertificates[0]
    $taskCodeSigningEku=$taskCertificate.Extensions | Where-Object {$_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]} | ForEach-Object {$_.EnhancedKeyUsages} | Where-Object {$_.Value -eq '1.3.6.1.5.5.7.3.3'}
    if(!$taskCodeSigningEku){throw 'O certificado não possui o uso estendido Code Signing.'}
    if($taskCertificate.NotAfter -le (Get-Date)){throw 'O certificado de assinatura expirou.'}
    $taskPublicKeySize=$taskCertificate.PublicKey.Key.KeySize
    if($taskCertificate.PublicKey.Oid.Value -ne '1.2.840.113549.1.1.1' -or $taskPublicKeySize -lt 2048 -or $taskPublicKeySize -gt 4096){throw 'Use uma chave RSA de 2048 a 4096 bits para compatibilidade com App Control.'}

    $taskSdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $taskSignTool=Get-ChildItem -LiteralPath $taskSdkRoot -Filter signtool.exe -Recurse -File -ErrorAction Stop | Where-Object {$_.FullName -match '\\x64\\signtool\.exe$'} | Sort-Object FullName -Descending | Select-Object -First 1
    if(!$taskSignTool){throw 'SignTool x64 não foi encontrado no Windows SDK.'}

    foreach($taskBinary in $taskBinaries){
        $taskSignature=Get-AuthenticodeSignature -LiteralPath $taskBinary.FullName
        if($taskSignature.Status -eq 'NotSigned'){
            & $taskSignTool.FullName sign /s My /sha1 $taskCertificate.Thumbprint /fd SHA256 /tr 'http://timestamp.digicert.com' /td SHA256 $taskBinary.FullName
            if($LASTEXITCODE -ne 0){throw "Falha ao assinar um binário do pacote ($($taskBinary.Name))."}
        }elseif($taskSignature.Status -ne 'Valid'){
            throw "O pacote contém uma assinatura inválida ($($taskBinary.Name))."
        }
        & $taskSignTool.FullName verify /pa /all $taskBinary.FullName
        if($LASTEXITCODE -ne 0){throw "A assinatura Authenticode não foi verificada ($($taskBinary.Name))."}
    }
    Write-Output "Binários Windows assinados e verificados: $($taskBinaries.Count)."
}
finally{
    foreach($taskCertificate in $taskImported){
        if($taskCertificate.HasPrivateKey){Remove-Item -LiteralPath (Join-Path 'Cert:\CurrentUser\My' $taskCertificate.Thumbprint) -Force -ErrorAction SilentlyContinue}
    }
    if(Test-Path -LiteralPath $taskPfx){Remove-Item -LiteralPath $taskPfx -Force -ErrorAction SilentlyContinue}
}

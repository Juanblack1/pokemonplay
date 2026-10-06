$ErrorActionPreference='Stop'
$taskFixture=Join-Path ([IO.Path]::GetTempPath()) ('pokemonplay-signing-test-'+[guid]::NewGuid().ToString('N'))
$taskSigner=Join-Path $PSScriptRoot 'sign-windows-package.ps1'
$taskExpected=@('PokemonPlayRuntime/Pokemons Play.exe','PokemonPlayRuntime/Pokemons Play.dll',
    'PokemonPlayRuntime/PokemonPlayUpdater.exe','launcher/Pokemons Play.exe')
try{
    $taskDecoys=@('PokemonPlayRuntime/PKHeX.Core.dll','PokemonPlayRuntime/coreclr.dll',
        'PokemonPlayRuntime/Emulators/retroarch.exe','PokemonPlayRuntime/Emulators/cores/mgba_libretro.dll',
        'unrelated/Pokemons Play.exe','unrelated/Pokemons Play.dll','updater/PokemonPlayUpdater.exe')
    foreach($taskRelative in @($taskExpected+$taskDecoys+@('pokemon-play-win-x64-setup.exe'))){
        $taskPath=Join-Path $taskFixture $taskRelative
        New-Item -ItemType Directory -Path (Split-Path $taskPath) -Force | Out-Null
        [IO.File]::WriteAllText($taskPath,'fixture; not executable')
    }
    $taskActual=@(& $taskSigner -Directory $taskFixture -ListTargets)
    $taskExpectedPaths=@($taskExpected | ForEach-Object {[IO.Path]::GetFullPath((Join-Path $taskFixture $_))})
    if($taskActual.Count -ne 4 -or @(Compare-Object $taskExpectedPaths $taskActual).Count){throw 'Runtime signing scope includes unexpected files or excludes required files.'}
    Write-Output 'PASS: Only four project runtime/launcher files selected; upstream and duplicate-name decoys excluded.'
    $taskInstaller=@(& $taskSigner -Directory $taskFixture -Kind Installer -ListTargets)
    if($taskInstaller.Count -ne 1 -or $taskInstaller[0] -ne (Join-Path $taskFixture 'pokemon-play-win-x64-setup.exe')){throw 'Installer signing scope is incorrect.'}
    Write-Output 'PASS: Installer mode selects only the project installer.'
    Remove-Item -LiteralPath (Join-Path $taskFixture 'PokemonPlayRuntime/Pokemons Play.dll')
    $taskMissingRejected=$false
    try{& $taskSigner -Directory $taskFixture -ListTargets | Out-Null}catch{
        if($_.Exception.Message -notmatch 'obrigatório ausente'){throw}
        $taskMissingRejected=$true
    }
    if(!$taskMissingRejected){throw 'Missing required project assembly was accepted.'}
    Write-Output 'PASS: Missing project assembly fails before any certificate import or signing.'
}
finally{
    if(Test-Path -LiteralPath $taskFixture){
        $taskResolved=(Resolve-Path -LiteralPath $taskFixture).Path
        if($taskResolved -ne [IO.Path]::GetFullPath($taskFixture) -or !(Split-Path $taskResolved -Leaf).StartsWith('pokemonplay-signing-test-')){throw 'Unsafe fixture cleanup path.'}
        Remove-Item -LiteralPath $taskResolved -Recurse
    }
}

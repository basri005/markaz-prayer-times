param([string]$InnoCompiler)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force build,dependencies,dist,'build\x64','build\tessdata' | Out-Null
function Get-Verified($Url,$Path,$Sha256) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest -UseBasicParsing $Url -OutFile $Path }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw "Checksum mismatch: $Path" }
}
Get-Verified 'https://api.nuget.org/v3-flatcontainer/tesseract/5.2.0/tesseract.5.2.0.nupkg' 'dependencies\tesseract.zip' '202D82FC7C7D8384DF7DA57206D5E1F456CCDABD648C46E67CDFAA3A911D4795'
Expand-Archive -LiteralPath 'dependencies\tesseract.zip' -DestinationPath 'dependencies\tesseract' -Force
Copy-Item 'dependencies\tesseract\lib\net47\Tesseract.dll' build
Copy-Item 'dependencies\tesseract\x64\*.dll' 'build\x64'
Get-Verified 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/eng.traineddata' 'build\tessdata\eng.traineddata' '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2'
if (!(Test-Path 'dependencies\vc_redist.x64.exe')) { Invoke-WebRequest -UseBasicParsing 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile 'dependencies\vc_redist.x64.exe' }
$sig=Get-AuthenticodeSignature 'dependencies\vc_redist.x64.exe'
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Microsoft runtime signature validation failed.' }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /platform:x64 /target:winexe '/out:build\Prayer Times.exe' /win32icon:assets\prayer-times.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:build\Tesseract.dll src\*.cs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item assets\timetable.csv build\timetable.csv
$test=Start-Process -FilePath (Join-Path $PSScriptRoot 'build\Prayer Times.exe') -ArgumentList '--test' -Wait -PassThru -WindowStyle Hidden
if ($test.ExitCode -ne 0) { Get-Content build\verification.txt; throw 'Tests failed.' }
if (!$InnoCompiler) {
    $candidate=@("$env:ProgramFiles\Inno Setup 7\ISCC.exe","${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (!$candidate) { throw 'Install Inno Setup or pass -InnoCompiler with its ISCC.exe path.' }; $InnoCompiler=$candidate
}
& $InnoCompiler installer\PrayerTimes.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Get-ChildItem dist\*.exe | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name } | Set-Content dist\SHA256SUMS.txt
Write-Output 'Build and tests completed. Installer is in dist/.'

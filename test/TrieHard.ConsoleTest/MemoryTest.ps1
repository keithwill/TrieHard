dotnet build -c Release -v q --nologo | Out-Null
Clear-Host
echo "| Method    | Key Type   |  Managed MB |  Process MB | GC Pause |"
echo "|-----------|------------|-------------|-------------|---------:|"
$test = "bin/Release/net8.0/TrieHard.ConsoleTest.exe"

& $test baseline sequential
& $test naivelist sequential
& $test sqlite sequential
& $test radix sequential
& $test unsafe sequential
& $test flat sequential
& $test simple sequential
echo "|-----------|------------|-------------|-------------|---------:|"
& $test baseline paths
& $test naivelist paths
& $test sqlite paths
& $test radix paths
& $test unsafe paths
& $test flat paths
& $test simple paths

Output docs/vhs/install-run.gif

Set Shell "bash"
Set FontSize 28
Set Width 1200
Set Height 720
Set Padding 24

Type "dotnet build src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj -c Release"
Enter
Sleep 1s

Type "export RawSqlMcp__ConnectionStrings__Default='Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True'"
Enter
Sleep 500ms

Type "echo 'Recommended package command: dnx RawSqlMcp'"
Enter
Sleep 500ms

Type "src/RawSqlMcp.Cli/bin/Release/net10.0/RawSqlMcp.Cli & pid=$!; sleep 3; kill $pid 2>/dev/null || true; wait $pid 2>/dev/null || true"
Enter
Sleep 3s

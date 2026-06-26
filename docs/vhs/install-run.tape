Output docs/vhs/install-run.gif

Set Shell "bash"
Set FontSize 28
Set Width 1200
Set Height 720
Set Padding 24

Type "dnx RawSqlMcp@0.0.2 -y --help"
Enter
Sleep 2s

Type "export RawSqlMcp__ConnectionStrings__Default='Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True'"
Enter
Sleep 500ms

Type "timeout 5s dnx RawSqlMcp@0.0.2 -y || true"
Enter
Sleep 6s

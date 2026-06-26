Output docs/vhs/end-to-end.gif

Set Shell "bash"
Set FontSize 24
Set Width 1200
Set Height 720
Set Padding 24

Type "npx --yes @modelcontextprotocol/inspector --cli --transport stdio -e RawSqlMcp__ConnectionStrings__Default='Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True' --method tools/call --tool-name available_databases dnx RawSqlMcp@0.0.2 -y"
Enter
Sleep 5s

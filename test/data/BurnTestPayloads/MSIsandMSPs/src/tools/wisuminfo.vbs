Dim msi : Set msi = CreateObject("WindowsInstaller.Installer")
Dim db : Set db = msi.OpenDatabase(WScript.Arguments(0), 33)
Dim info : Set info = db.SummaryInformation(1)
info.Property(CInt(WScript.Arguments(1))) = WScript.Arguments(2)
info.Persist()
db.Commit()

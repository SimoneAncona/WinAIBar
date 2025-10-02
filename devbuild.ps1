dotnet publish AIBar\AIbar.csproj -c Release -o .\publish\App
dotnet publish Installer\Installer.csproj -c Release -o .\publish\
rm .\publish\*.pdb
rm .\publish\App\*.pdb
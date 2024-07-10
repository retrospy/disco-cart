
IF "%~1"=="" GOTO release

IF NOT "%~1"=="" set sub=1

:release
rmdir /S /Q bin\Release\net8.0
"C:\Program Files\dotnet\dotnet.exe" build DiscoCartUtil\DiscoCartUtil.csproj /p:RuntimeIdentifier=win-x86 /p:Configuration=Release /p:Platform="Any CPU" /p:OutputPath=..\bin\Release\net8.0
if %ERRORLEVEL% NEQ 0 goto :fail

del DiscoCartUtil-Windows.zip
del DiscoCartUtil-Windows.zip.*
rmdir /S /Q DiscoCartUtil-Setup
del DiscoCartUtil-Setup.exe
rmdir /S /Q DiscoCartUtil-Upload

mkdir DiscoCartUtil-Setup
if %ERRORLEVEL% NEQ 0 goto :fail

if exist "..\..\..\..\certs\codesignpasswd.txt" ( 
    set /p codesignpasswd=<"..\..\..\..\certs\codesignpasswd.txt"
)

cd "bin\Release\net8.0\"
if %ERRORLEVEL% NEQ 0 goto :fail

if exist "..\..\..\..\..\..\..\certs\codesign.cer" (
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x86\SignTool" sign /f "..\..\..\..\..\..\..\certs\codesign.cer" /csp "eToken Base Cryptographic Provider" /k "%codesignpasswd%" /tr http://timestamp.comodoca.com  /td sha256 /fd sha256 /a DiscoCartUtil.exe
if %ERRORLEVEL% NEQ 0 goto :fail
)

xcopy /y /e /s * ..\..\..\DiscoCartUtil-Setup
if %ERRORLEVEL% NEQ 0 goto :fail

cd ..\..\..
if %ERRORLEVEL% NEQ 0 goto :fail

cd DiscoCartUtil-Setup
if %ERRORLEVEL% NEQ 0 goto :fail
"C:\Program Files\7-Zip\7z.exe" -r a ..\DiscoCartUtil-Windows.zip *.*
if %ERRORLEVEL% NEQ 0 goto :fail
cd ..
if %ERRORLEVEL% NEQ 0 goto :fail

copy ..\LICENSE DiscoCartUtil-Setup
if %ERRORLEVEL% NEQ 0 goto :fail
"C:\Program Files\7-Zip\7z.exe" a DiscoCartUtil-Windows.zip ..\LICENSE
if %ERRORLEVEL% NEQ 0 goto :fail

if exist "C:\Program Files (x86)\Actual Installer\actinst.exe" (
"C:\Program Files (x86)\Actual Installer\actinst.exe" /S ".\DiscoCartUtil-32.aip"
if %ERRORLEVEL% NEQ 0 goto :fail
  if exist "..\..\..\..\certs\codesign.cer" (
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x86\SignTool" sign /f "..\..\..\..\certs\codesign.cer" /csp "eToken Base Cryptographic Provider" /k "%codesignpasswd%" /tr http://timestamp.comodoca.com  /td sha256 /fd sha256 /a DiscoCartUtil-Setup.exe
    if %ERRORLEVEL% NEQ 0 goto :fail
  )
)

if exist "..\..\..\..\upload\" (
  copy DiscoCartUtil-Setup.exe ..\..\..\..\upload\DiscoCartUtil-Setup-x86.exe
  if %ERRORLEVEL% NEQ 0 goto :fail
  copy DiscoCartUtil-Windows.zip ..\..\..\..\upload\DiscoCartUtil-Windows-x86.zip
  if %ERRORLEVEL% NEQ 0 goto :fail
)

:end
EXIT /b 0

:fail
EXIT /b 1
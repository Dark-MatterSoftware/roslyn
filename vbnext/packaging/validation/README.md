# Validating a Vb.Next package in Windows Sandbox

`Test-VbNextPackage.ps1` checks an extracted Vb.Next ZIP with a stock .NET SDK and writes a PASS/FAIL report. `expected-output.txt` is the verified output of HelloVbNext, 67 lines; one line ends in a space, so keep the file byte for byte.

## On the host

Windows Sandbox needs Windows Pro or Enterprise, with the "Windows Sandbox" optional feature turned on.

Put these in one folder, for example `C:\VbNextSandbox\input`:

- `VbNext-1.0.0-preview.1-win-net472.zip` and `VbNext-1.0.0-preview.1-SHA256SUMS.txt`
- the stock SDK installer from Microsoft, `dotnet-sdk-10.0.403-win-x64.exe`
- this `validation` folder

Create an empty `C:\VbNextSandbox\results`, and save this as `C:\VbNextSandbox\VbNext.wsb`:

```xml
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>C:\VbNextSandbox\input</HostFolder>
      <SandboxFolder>C:\Input</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>C:\VbNextSandbox\results</HostFolder>
      <SandboxFolder>C:\Results</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
</Configuration>
```

Open `VbNext.wsb` to start the sandbox. Everything inside it is discarded when it closes.

## In the sandbox

In Windows PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
Get-FileHash C:\Input\VbNext-1.0.0-preview.1-win-net472.zip -Algorithm SHA256
Get-Content C:\Input\VbNext-1.0.0-preview.1-SHA256SUMS.txt
Start-Process C:\Input\dotnet-sdk-10.0.403-win-x64.exe -ArgumentList '/install', '/quiet', '/norestart' -Wait
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
Expand-Archive C:\Input\VbNext-1.0.0-preview.1-win-net472.zip 'C:\Vb Next Test'
& C:\Input\validation\Test-VbNextPackage.ps1 -PackagePath 'C:\Vb Next Test\VbNext-1.0.0-preview.1' `
    -Dotnet 'C:\Program Files\dotnet\dotnet.exe' -WorkDir 'C:\Vb Next Work'
Copy-Item 'C:\Vb Next Work' C:\Results -Recurse
```

The two hashes must be equal. The script exits with 0 when every check passes; `C:\Results\Vb Next Work\report.txt` holds the report, and `logs` holds the build logs and program outputs.

On a machine with more than one SDK, add `-SdkVersion 10.0.403` to pin the SDK for the test projects.

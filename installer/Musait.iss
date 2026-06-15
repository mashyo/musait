#define AppName "Musait – Renderings and Family Builder for Revit"
#define AppPublisher "Mashyo"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#ifndef StageRoot
#define StageRoot "..\dist\inno-stage"
#endif
#ifndef SetupIcon
#define SetupIcon "..\assets\Musait.ico"
#endif

[Setup]
AppId={{7D8FBA10-7F77-4F3D-A07D-35DBA7838F4D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 Mashyo
AppPublisherURL=https://github.com/mashyo/musait
AppSupportURL=https://github.com/mashyo/musait/issues
AppUpdatesURL=https://github.com/mashyo/musait/releases
DefaultDirName={autopf}\Mashyo\Musait
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Musait-Setup
SetupIconFile={#SetupIcon}
UninstallDisplayIcon={app}\Musait.ico
LicenseFile=..\LICENSE
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern dynamic
Compression=lzma2/max
SolidCompression=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright (c) 2026 Mashyo
VersionInfoDescription=Musait Revit Add-In Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductTextVersion={#AppVersion}

[Types]
Name: "full"; Description: "Install for all supported Revit versions"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "revit2022"; Description: "Revit 2022 Add-in"; Types: full custom
Name: "revit2023"; Description: "Revit 2023 Add-in"; Types: full custom
Name: "revit2024"; Description: "Revit 2024 Add-in"; Types: full custom
Name: "revit2025"; Description: "Revit 2025 Add-in"; Types: full custom
Name: "revit2026"; Description: "Revit 2026 Add-in"; Types: full custom
Name: "revit2027"; Description: "Revit 2027 Add-in"; Types: full custom

[Files]
Source: "..\assets\Patreon-64X32.png"; Flags: dontcopy noencryption
Source: "..\assets\Subscribe-64X32.png"; Flags: dontcopy noencryption
Source: "{#SetupIcon}"; DestDir: "{app}"; DestName: "Musait.ico"; Flags: ignoreversion

Source: "{#StageRoot}\2022\Musait\*"; DestDir: "{app}\2022"; Components: revit2022; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#StageRoot}\2023\Musait\*"; DestDir: "{app}\2023"; Components: revit2023; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#StageRoot}\2024\Musait\*"; DestDir: "{app}\2024"; Components: revit2024; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#StageRoot}\2025\Musait\*"; DestDir: "{app}\2025"; Components: revit2025; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#StageRoot}\2026\Musait\*"; DestDir: "{app}\2026"; Components: revit2026; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#StageRoot}\2027\Musait\*"; DestDir: "{app}\2027"; Components: revit2027; Flags: ignoreversion recursesubdirs createallsubdirs


[UninstallDelete]
Type: files; Name: "{autoappdata}\Autodesk\Revit\Addins\2022\Musait.addin"
Type: files; Name: "{autoappdata}\Autodesk\Revit\Addins\2023\Musait.addin"
Type: files; Name: "{autoappdata}\Autodesk\Revit\Addins\2024\Musait.addin"
Type: files; Name: "{autoappdata}\Autodesk\Revit\Addins\2025\Musait.addin"
Type: files; Name: "{autoappdata}\Autodesk\Revit\Addins\2026\Musait.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2027\Musait.addin"
Type: files; Name: "{commonpf}\Autodesk\Revit\Addins\2027\Musait.addin"

[CustomMessages]
SupportButtonCaption=Support
SupportButtonHint=Support Musait on Patreon
SubscribeButtonCaption=Subscribe
SubscribeButtonHint=Visit mashyo.com

[Code]
var
  SupportButton: TBitmapButton;
  SubscribeButton: TBitmapButton;

procedure OpenUrl(Url: String);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', Url, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure SupportButtonClick(Sender: TObject);
begin
  OpenUrl('https://patreon.com/mashyo');
end;

procedure SubscribeButtonClick(Sender: TObject);
begin
  OpenUrl('https://mashyo.com/');
end;

procedure CreateInstallerLinkButtons();
var
  BevelTop: Integer;
  SupportImageFileName: String;
  SubscribeImageFileName: String;
begin
  if WizardSilent then
    Exit;

  SupportImageFileName := ExpandConstant('{tmp}\Patreon-64X32.png');
  SubscribeImageFileName := ExpandConstant('{tmp}\Subscribe-64X32.png');
  ExtractTemporaryFile(ExtractFileName(SupportImageFileName));
  ExtractTemporaryFile(ExtractFileName(SubscribeImageFileName));

  SupportButton := TBitmapButton.Create(WizardForm);
  SupportButton.AutoSize := True;
  SupportButton.PngImage.LoadFromFile(SupportImageFileName);
  SupportButton.Caption := CustomMessage('SupportButtonCaption');
  SupportButton.Hint := CustomMessage('SupportButtonHint');
  SupportButton.ShowHint := True;
  BevelTop := WizardForm.Bevel.Top;
  SupportButton.Top := BevelTop + (WizardForm.ClientHeight - BevelTop - SupportButton.Height) div 2;
  SupportButton.Left := SupportButton.Top - BevelTop;
  SupportButton.Cursor := crHand;
  SupportButton.OnClick := @SupportButtonClick;
  SupportButton.Parent := WizardForm;

  SubscribeButton := TBitmapButton.Create(WizardForm);
  SubscribeButton.AutoSize := True;
  SubscribeButton.PngImage.LoadFromFile(SubscribeImageFileName);
  SubscribeButton.Caption := CustomMessage('SubscribeButtonCaption');
  SubscribeButton.Hint := CustomMessage('SubscribeButtonHint');
  SubscribeButton.ShowHint := True;
  SubscribeButton.Top := SupportButton.Top;
  SubscribeButton.Left := SupportButton.Left + SupportButton.Width + ScaleX(4);
  SubscribeButton.Cursor := crHand;
  SubscribeButton.OnClick := @SubscribeButtonClick;
  SubscribeButton.Parent := WizardForm;
end;

function IsRevitInstalled(Version: String): Boolean;
var
  InstallLocation: String;
begin
  Result :=
    RegQueryStringValue(HKLM64, 'SOFTWARE\Autodesk\Revit\' + Version, 'InstallationLocation', InstallLocation) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Autodesk\Revit\' + Version, 'InstallationLocation', InstallLocation);
end;

function AddinXml(AssemblyPath: String): String;
begin
  Result :=
    '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>Musait</Name>' + #13#10 +
    '    <Assembly>' + AssemblyPath + '</Assembly>' + #13#10 +
    '    <ClientId>D0A1C8E4-9F8A-4B6B-B3A2-5D6E1F4C9B0E</ClientId>' + #13#10 +
    '    <FullClassName>Musait.App</FullClassName>' + #13#10 +
    '    <VendorId>MASHYO</VendorId>' + #13#10 +
    '    <VendorDescription>Mashyo Tools</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>' + #13#10;
end;

function DetectedComponents(): String;
begin
  Result := '';
  if IsRevitInstalled('2022') then Result := Result + 'revit2022,';
  if IsRevitInstalled('2023') then Result := Result + 'revit2023,';
  if IsRevitInstalled('2024') then Result := Result + 'revit2024,';
  if IsRevitInstalled('2025') then Result := Result + 'revit2025,';
  if IsRevitInstalled('2026') then Result := Result + 'revit2026,';
  if IsRevitInstalled('2027') then Result := Result + 'revit2027,';

  if Result <> '' then
    Delete(Result, Length(Result), 1);
end;

procedure InitializeWizard();
var
  Components: String;
begin
  CreateInstallerLinkButtons();

  Components := DetectedComponents();
  if Components <> '' then
    WizardSelectComponents(Components);
end;

function RevitAddinsDir(Version: String): String;
begin
  if (Version = '2027') and IsAdminInstallMode then
    Result := ExpandConstant('{commonpf}\Autodesk\Revit\Addins\2027')
  else
    Result := ExpandConstant('{autoappdata}\Autodesk\Revit\Addins\' + Version);
end;

procedure InstallRevitManifest(Version: String);
var
  AddinsDir: String;
  ManifestPath: String;
  AssemblyPath: String;
begin
  AddinsDir := RevitAddinsDir(Version);
  ManifestPath := AddinsDir + '\Musait.addin';
  AssemblyPath := ExpandConstant('{app}\' + Version + '\Musait.dll');

  ForceDirectories(AddinsDir);
  SaveStringToFile(ManifestPath, AddinXml(AssemblyPath), False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsComponentSelected('revit2022') then InstallRevitManifest('2022');
    if WizardIsComponentSelected('revit2023') then InstallRevitManifest('2023');
    if WizardIsComponentSelected('revit2024') then InstallRevitManifest('2024');
    if WizardIsComponentSelected('revit2025') then InstallRevitManifest('2025');
    if WizardIsComponentSelected('revit2026') then InstallRevitManifest('2026');
    if WizardIsComponentSelected('revit2027') then InstallRevitManifest('2027');
  end;
end;

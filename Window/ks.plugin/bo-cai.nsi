Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "nsDialogs.nsh"
!include "x64.nsh"

!define TEN_HIEN "Plugin ký số"
!define TEN_EXE "Ký số plugin.exe"
!define TEN_EXE_GO "go-cai-dat.exe"
!define TEN_THU_MUC "KySoPlugin"
!define NHA_PHAT_HANH "Ynap"

!define KHOA_CAU_HINH "Software\KySoPlugin"
!define GIA_TRI_MOI_TRUONG "MoiTruong"
!define GIA_TRI_THU_MUC_CAI "ThuMucCai"
!define KHOA_AUTOSTART "Software\Microsoft\Windows\CurrentVersion\Run"
!define TEN_AUTOSTART "KySoPlugin"
!define KHOA_GO_CAI_DAT "Software\Microsoft\Windows\CurrentVersion\Uninstall\KySoPlugin"

!define KHOA_PROVIDER "SOFTWARE\Microsoft\Cryptography\Defaults\Provider"
!define CO_NGAM_MIDDLEWARE "/S"

!ifndef PHIEN_BAN
  !define PHIEN_BAN "2.0.0"
!endif

!ifndef PHIEN_BAN_DAY
  !define PHIEN_BAN_DAY "2.0.0.0"
!endif

!ifndef DUONG_EXE
  !define DUONG_EXE "ks.plugin.api\bin\Release\net9.0-windows\${TEN_EXE}"
!endif

!ifndef DUONG_RA
  !define DUONG_RA "${TEN_EXE}"
!endif

!ifndef BST_CHECKED
  !define BST_CHECKED 1
!endif

Name "${TEN_HIEN} ${PHIEN_BAN}"
OutFile "${DUONG_RA}"
InstallDir "$LOCALAPPDATA\${TEN_THU_MUC}"
InstallDirRegKey HKCU "${KHOA_CAU_HINH}" "${GIA_TRI_THU_MUC_CAI}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
BrandingText "${TEN_HIEN} ${PHIEN_BAN}"

VIProductVersion "${PHIEN_BAN_DAY}"
VIAddVersionKey "ProductName" "${TEN_HIEN}"
VIAddVersionKey "FileDescription" "Bộ cài ${TEN_HIEN}"
VIAddVersionKey "FileVersion" "${PHIEN_BAN}"
VIAddVersionKey "ProductVersion" "${PHIEN_BAN}"
VIAddVersionKey "CompanyName" "${NHA_PHAT_HANH}"
VIAddVersionKey "LegalCopyright" "${NHA_PHAT_HANH}"

Var MoiTruong
Var OProduction
Var OStaging
Var ODevelopment

!define MUI_ICON "ks.plugin.api\Assets\ky-so.ico"
!define MUI_UNICON "ks.plugin.api\Assets\ky-so.ico"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "Cài ${TEN_HIEN}"
!define MUI_WELCOMEPAGE_TEXT "Plugin làm cầu nối giữa trang web ký số và USB token.$\r$\n$\r$\nTrình cài đặt sẽ cài trình đọc token nếu máy chưa có, chép plugin vào thư mục bạn chọn và bật tự khởi động cùng Windows.$\r$\n$\r$\nBấm Next để bắt đầu."

!define MUI_DIRECTORYPAGE_TEXT_TOP "Chọn thư mục lưu plugin và dữ liệu của nó. Mặc định nằm trong hồ sơ người dùng nên không cần quyền quản trị."

!define MUI_FINISHPAGE_TITLE "Đã cài xong"
!define MUI_FINISHPAGE_TEXT "${TEN_HIEN} đã được cài và sẽ tự khởi động cùng Windows.$\r$\n$\r$\nQuay lại trang ký số và bấm Kiểm tra lại."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${TEN_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Chạy ${TEN_HIEN} ngay"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
Page custom TrangMoiTruong RoiTrangMoiTruong
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Vietnamese"

LangString ^BackBtn ${LANG_VIETNAMESE} "< &Back"
LangString ^NextBtn ${LANG_VIETNAMESE} "&Next >"
LangString ^CancelBtn ${LANG_VIETNAMESE} "Cancel"
LangString ^InstallBtn ${LANG_VIETNAMESE} "&Install"
LangString ^UninstallBtn ${LANG_VIETNAMESE} "&Uninstall"
LangString ^CloseBtn ${LANG_VIETNAMESE} "&Close"
LangString ^BrowseBtn ${LANG_VIETNAMESE} "B&rowse..."
LangString ^ShowDetailsBtn ${LANG_VIETNAMESE} "Show &details"

Function .onInit
  SetRegView 64
  SetShellVarContext current
  StrCpy $MoiTruong "Production"
FunctionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext current
FunctionEnd

Function TrangMoiTruong
  !insertmacro MUI_HEADER_TEXT "Môi trường chạy" "Chọn môi trường plugin dùng khi khởi động."

  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 28u "Lựa chọn này được ghi vào registry và plugin đọc lại làm ASPNETCORE_ENVIRONMENT mỗi lần khởi động. Máy người dùng cuối chọn Production."
  Pop $0

  ${NSD_CreateRadioButton} 8u 34u 95% 12u "Production — máy người dùng cuối"
  Pop $OProduction

  ${NSD_CreateRadioButton} 8u 50u 95% 12u "Staging — môi trường kiểm thử"
  Pop $OStaging

  ${NSD_CreateRadioButton} 8u 66u 95% 12u "Development — máy lập trình viên"
  Pop $ODevelopment

  ${If} $MoiTruong == "Staging"
    ${NSD_SetState} $OStaging ${BST_CHECKED}
  ${ElseIf} $MoiTruong == "Development"
    ${NSD_SetState} $ODevelopment ${BST_CHECKED}
  ${Else}
    ${NSD_SetState} $OProduction ${BST_CHECKED}
  ${EndIf}

  nsDialogs::Show
FunctionEnd

Function RoiTrangMoiTruong
  ${NSD_GetState} $OStaging $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $MoiTruong "Staging"
    Return
  ${EndIf}

  ${NSD_GetState} $ODevelopment $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $MoiTruong "Development"
    Return
  ${EndIf}

  StrCpy $MoiTruong "Production"
FunctionEnd

Function DoMiddleware
  StrCpy $R0 "0"
  StrCpy $R1 0

  ${Do}
    EnumRegKey $R2 HKLM "${KHOA_PROVIDER}" $R1
    ${If} $R2 == ""
      ${ExitDo}
    ${EndIf}
    IntOp $R1 $R1 + 1

    StrCpy $R3 $R2 6
    ${If} $R3 == "bit4id"
      StrCpy $R0 "1"
      ${ExitDo}
    ${EndIf}

    StrCpy $R3 $R2 8
    ${If} $R3 == "bit4xpki"
      StrCpy $R0 "1"
      ${ExitDo}
    ${EndIf}
  ${Loop}

  ${If} $R0 == "0"
    ${If} ${RunningX64}
      ${DisableX64FSRedirection}
    ${EndIf}

    FindFirst $R4 $R5 "$SYSDIR\bit4*.dll"
    ${If} $R5 != ""
      StrCpy $R0 "1"
    ${EndIf}
    FindClose $R4

    ${If} ${RunningX64}
      ${EnableX64FSRedirection}
    ${EndIf}
  ${EndIf}
FunctionEnd

Function LoMiddleware
  Call DoMiddleware
  ${If} $R0 == "1"
    DetailPrint "Trình đọc token: máy đã có sẵn, bỏ qua."
    Return
  ${EndIf}

!ifdef BO_CAI_MIDDLEWARE
  DetailPrint "Trình đọc token: chưa có, đang cài. Xin chấp nhận hộp thoại nâng quyền."
  InitPluginsDir
  File "/oname=$PLUGINSDIR\bit4id-setup.exe" "${BO_CAI_MIDDLEWARE}"
  ExecShellWait "runas" "$PLUGINSDIR\bit4id-setup.exe" "${CO_NGAM_MIDDLEWARE}"

  Call DoMiddleware
  ${If} $R0 == "1"
    DetailPrint "Trình đọc token: cài xong."
  ${Else}
    DetailPrint "Trình đọc token: CHƯA cài được. Plugin vẫn chạy nhưng sẽ không thấy chứng thư trên token."
  ${EndIf}
!else
  DetailPrint "Trình đọc token: bản cài này không kèm sẵn. Lấy bộ cài bit4id từ đơn vị cấp chứng thư số rồi cài trước."
!endif
FunctionEnd

Section "Plugin ký số"
  DetailPrint "Đang dừng bản plugin đang chạy..."
  System::Call 'kernel32::GetCurrentProcessId()i.r0'
  nsExec::ExecToLog 'taskkill /F /IM "${TEN_EXE}" /FI "PID ne $0"'
  Pop $0
  Sleep 500

  Call LoMiddleware

  DetailPrint "Đang chép plugin vào $INSTDIR..."
  SetOutPath "$INSTDIR"
  File "/oname=$INSTDIR\${TEN_EXE}" "${DUONG_EXE}"

  WriteRegStr HKCU "${KHOA_CAU_HINH}" "${GIA_TRI_THU_MUC_CAI}" "$INSTDIR"
  WriteRegStr HKCU "${KHOA_CAU_HINH}" "${GIA_TRI_MOI_TRUONG}" "$MoiTruong"
  DetailPrint "Môi trường: $MoiTruong"

  WriteRegStr HKCU "${KHOA_AUTOSTART}" "${TEN_AUTOSTART}" '"$INSTDIR\${TEN_EXE}"'

  WriteUninstaller "$INSTDIR\${TEN_EXE_GO}"

  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "DisplayName" "${TEN_HIEN}"
  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "DisplayVersion" "${PHIEN_BAN}"
  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "Publisher" "${NHA_PHAT_HANH}"
  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "DisplayIcon" "$INSTDIR\${TEN_EXE}"
  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${KHOA_GO_CAI_DAT}" "UninstallString" '"$INSTDIR\${TEN_EXE_GO}"'
  WriteRegDWORD HKCU "${KHOA_GO_CAI_DAT}" "NoModify" 1
  WriteRegDWORD HKCU "${KHOA_GO_CAI_DAT}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  DetailPrint "Đang dừng plugin..."
  nsExec::ExecToLog 'taskkill /F /IM "${TEN_EXE}"'
  Pop $0
  Sleep 500

  DeleteRegValue HKCU "${KHOA_AUTOSTART}" "${TEN_AUTOSTART}"
  DeleteRegKey HKCU "${KHOA_GO_CAI_DAT}"
  DeleteRegKey HKCU "${KHOA_CAU_HINH}"

  Delete "$INSTDIR\${TEN_EXE}"
  Delete "$INSTDIR\${TEN_EXE_GO}"
  RMDir "$INSTDIR"

  DetailPrint "Đã gỡ plugin. Trình đọc token bit4id được giữ nguyên vì phần mềm khác còn dùng."
SectionEnd

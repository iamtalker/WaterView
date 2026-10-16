# 물뷰 (WaterView) 작업 메모

NeeView(MIT, © Ito Mitsuhiro) 를 포크한 Windows 만화 뷰어. 원본의 저작권 표시/`LICENSE.md` 는 유지한다.
다른 상용 뷰어의 코드·아이콘·이미지는 쓰지 않는다 (화면 구성/조작 방식 참고만).

## 구조
- `NeeView/` : 앱 본체 (WPF, .NET 10). 어셈블리 이름은 `WaterView`, 설정 포맷 이름은 호환 때문에 `NeeView` 로 둠.
- `WaterViewLauncher/` : 겉의 `WaterView.exe` (.NET Framework 4.8, 약 25KB). .NET 10 이 없으면 설치 안내, `--update` 모드로 업데이트 적용.
- `docs/waterview/` : 소개 이미지와 소개글. `tools/` : 보조 스크립트.
- 서브모듈 4개(AnimatedImage, NeeLaboratory.IO.Search, SevenZipSharp, Vlc.DotNet) → 받을 때 `git clone --recurse-submodules`.
- 배포 폴더: `WaterView.exe`(실행기) + `app\`(본체, `app\Profile` 에 사용자 설정/기록).

## 빌드 / 배포
```
dotnet build NeeView\NeeView.csproj -c Release
dotnet publish NeeView\NeeView.csproj -c Release -r win-x64 --self-contained false -p:DebugType=none -o <출력>   # ReadyToRun, 시작이 빠름
dotnet build WaterViewLauncher\WaterViewLauncher.csproj -c Release -o <출력>
```
- 릴리스 zip = 실행기 `WaterView.exe` + `app\`(publish 결과에서 `Profile*`, `*.pdb` 제외, `LICENSE.md`/`THIRDPARTY_LICENSES.md` 포함).
- **작업 순서: 로컬 먼저, 업로드는 나중.** 이 폴더 맨 위의 `WaterView.exe` + `app` 이 곧 릴리스 모습이다(git 에는 안 올라감, `.git/info/exclude`). `toolsmake-package.ps1` 로 소스를 빌드해 여기를 갱신하고 zip 을 만든 뒤, 그 zip 을 릴리스에 올린다. 실행 중이면 덮어쓸 수 없으니 먼저 닫을 것.
- **릴리스 자산 이름은 반드시 `WaterView.zip`** (버전 없음). 자동 업데이트가 이 이름으로 찾는다.
- 버전은 `NeeView/NeeView.csproj` 와 `WaterViewLauncher/WaterViewLauncher.csproj` 의 `<Version>` 둘 다 올리고, 태그 `vX.Y.Z` 로 릴리스.
- 릴리스 노트의 **앞 8줄이 업데이트 안내창에 그대로 보인다** → 앞부분을 짧은 요약으로 쓰고 마크다운 기호는 줄일 것.

## 주요 코드 위치
- 키/마우스 기본값: `NeeView/Command/CommandTable.cs` 의 `CreateDefaultMemento` (프리셋은 TypeB/왼→오로 고정. 설정 복원 중 프리셋이 TypeA 로 바뀌기 때문).
- 우클릭 메뉴/상단 메뉴 기본 구성: `NeeView/Menu/MenuTree.cs`. 상단 아이콘 도구 줄·테마/슬라이드쇼 메뉴: `NeeView/MenuBar/MainMenu.cs`.
- F5 간단 설정: `NeeView/Setting/QuickSettingsDialog.cs` (전체 설정은 "상세 설정..." = 원래 NeeView 설정 창).
- 테마/배경색 편집: `NeeView/Styles/ThemeEditing.cs`, `ColorEditing.cs`. 슬라이드쇼 간격 목록: `NeeView/SlideShow/SlideShowIntervalList.cs`.
- 자동 업데이트: `NeeView/Update/UpdateService.cs` + `WaterViewLauncher/Program.cs`(`--update`). 탐색기 우클릭 등록: `NeeView/System/ExplorerContextMenu.cs`.
- 시작 지연 수정: `NeeView/System/MultiBootService.cs`. 환영 화면: `MainView.xaml` + `Resources/Welcome.png`.

## 겪은 함정
- `BackgroundConfig.CustomBackground` 는 객체를 **교체하지 말고 속성을 직접 수정**해야 화면에 반영된다 (감시 대상이 처음 객체).
- 설정은 `app\Profile\UserSetting.json` 이고, 없으면 `.bak` 가 복원된다. 시험할 때 기본값을 보려면 둘 다 치울 것.
- `NeeView.settings.json` 은 실행에 필요한 앱 파일(사용자 설정 아님). `PackageType` 은 `Zip`, `Watermark` 는 `false`.
- 한글이 든 파일을 `perl`/`sed` 로 고치면 깨질 수 있다 → UTF-8 을 다루는 도구로. PowerShell 5.1 스크립트에 한글이 있으면 UTF-8 **BOM** 으로 저장.
- 이 PC 에는 Python 이 없다(스토어 스텁). 스크립트는 PowerShell/Bash/Node 로.
- 언어 파일(`Languages/ko.restext`)에서 "NeeView"를 바꿀 때 `@#...OpenInNeeView` 같은 **키 참조**는 건드리지 말 것.

## 시험 요령
- WPF 창에 `PostMessage` 로 키를 보내려면 창이 **활성화**되어 있어야 한다 (`SwitchToThisWindow`).
- UI 멈춤은 `SendMessageTimeout(WM_NULL)` 지연으로, 쪽 넘김은 키 입력 후 창 제목이 바뀔 때까지의 시간으로 쟀다 (정상: 5~15ms).
- 업데이트 시험: 환경변수 `WATERVIEW_UPDATE_API` 로 가짜 릴리스 서버를 가리키면 실제 GitHub 없이 전체 과정을 시험할 수 있다. `WATERVIEW_FORCE_NO_RUNTIME=1` 은 실행기의 .NET 설치 안내 시험용.
- 첫 실행 질문(탐색기 우클릭 등록)은 모달이라 시험 중 다른 안내가 가려질 수 있다. 시험이 레지스트리에 시험 경로를 등록하지 않게 주의.

## 작업 방식
- 파일 삭제/레지스트리 변경/공개 게시처럼 되돌리기 어려운 일은 먼저 확인받는다.
- 커밋 메시지는 한국어, 끝에 `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- 측정하지 못한 것은 "확인하지 못했다"고 분명히 적는다.

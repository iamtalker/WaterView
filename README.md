# 물뷰 (WaterView)

만화 보기에 맞춘 이미지 뷰어입니다. 오픈소스 [NeeView](https://github.com/neelabo/NeeView) (MIT, © Ito Mitsuhiro) 를 기반으로, 조작과 화면 구성을 단순한 만화 뷰어 스타일로 바꾼 포크입니다.

## 바뀐 점
- 기본 읽기 방향: 왼쪽 → 오른쪽, 휠/방향키/Space/PageDown 으로 쪽 넘김
- 우클릭 메뉴와 단축키 구성 변경 (열기 F2, 닫기 F4, 파일 정보 Tab, 설정 F5 등)
- Enter: "이미지 선택" 창 (미리보기 + 파일 목록 + 정렬)
- 어두운 회색 배경, 패널/슬라이더/주소창 기본 숨김
- 상단 아이콘 도구 줄 (EXIF, 보기 모드, 슬라이드쇼, 책갈피, 편집, 항상 위에 표시, 메뉴)
- 앱 이름 "물뷰", 새 아이콘

## 폴더 구성
배포 zip 을 풀면 겉에는 `WaterView.exe` 하나만 보이고, 프로그램 본체와 라이브러리는 `app` 폴더 안에 있습니다. `WaterView.exe` 는 `WaterViewLauncher` 프로젝트로 만든 작은 실행기이며 `appWaterView.exe` 를 시작합니다.

## 설정
`F5` 는 자주 쓰는 항목(테마, 배경색, 읽는 방향, 슬라이드쇼 간격, 우클릭 메뉴 등록)만 모은 간단한 설정 창이고, 나머지 모든 설정은 `상세 설정...` 에서 바꿉니다.

## 실행 환경
Windows 10/11 x64. `.NET 10 데스크톱 런타임`이 필요하며, 없으면 `WaterView.exe` 가 처음 실행할 때 내려받아 설치할지 물어봅니다. ([직접 설치](https://dotnet.microsoft.com/download/dotnet/10.0))

## 빌드
```
git clone --recurse-submodules https://github.com/iamtalker/WaterView.git
cd WaterView
dotnet build NeeView\NeeView.csproj -c Release
```

## 라이선스
MIT License (NeeView 의 `LICENSE.md` 를 그대로 유지합니다). 서드파티 라이선스는 `THIRDPARTY_LICENSES.md` 를 참고하세요.
이 프로젝트는 특정 상용 뷰어와 무관하며, 다른 프로그램의 코드·아이콘·이미지를 사용하지 않았습니다. 화면 구성은 일반적인 만화 뷰어의 사용 방식을 참고했습니다.

---

# <img src="docs/assets/images/AppIcon.png" align="absmiddle"/>  NeeView

**[NeeView Portal Site (English/日本語)](https://neelabo.github.io/NeeView)**

> [!WARNING]
> The only official NeeView website is `neelabo.github.io/NeeView`.  
> Any other sites featuring NeeView as their main content may be imposters and are dangerous.

### Screenshot
![Screenshot](docs/assets/images/NeeView42.en-us.jpg)

## Notice

> [!IMPORTANT]
> Installer version: The installation type has been changed to Per-User.

Starting with v45.0, the installation type changes to Per-User, eliminating the need for administrator privileges.
Per-Machine installations from v44.1 or earlier cannot coexist and must be uninstalled first. If the new installer detects a v44.1 or earlier installation, it will display a dialog prompting uninstallation and halt the installation process. User data will be carried over.

## Description

An image viewer that allows you to browse images in folders and compressed files like a book.
Powerful customization is available.

- Supported image format (bmp, jpg, gif, tiff, png, webp, ico, WIC images)
- Compressed file support (zip, rar, 7z, lzh, cbr, cbz, cb7, ...)
- Recursive compressed file support
- PDF support
- Video play support
- Touch operation supported
- Mouse gesture supported
- Key customization, gesture setting
- Movement, rotation, scaling by dragging
- Loupe mode
- Facing mode
- Full screen mode
- Slide show
- Susie plugin support
- Image drop from web browser
- Command extension by scripts

### System Requirements

* Windows 10 64bit or higher

## Download

- [GitHub Releases](https://github.com/neelabo/NeeView/releases)
- [Microsoft Store](https://www.microsoft.com/store/apps/9p24z53hc1jr)
- [Vector](https://www.vector.co.jp/soft/winnt/art/se512262.html)

## Development Environment

* VisualStudio 2026
    -  .NET Desktop Development
    - Desktop development in C++

## Development of Language Files

- [Development of Language Files](NeeView/Languages)

## Branching Strategy

### master

This is the main development branch. Basically, pull requests are accepted here.

### version-XX

Branches of each major version.
When development of the next major version begins on the master branch, a branch is created to maintain the current major version.

### feature-XX

Branches for feature development.
It will be removed when merged into the master branch.
These branches are working branches and are not guaranteed in any way.

## Versioning Strategy

> MAJOR  . MINOR . BUILD

Update the MAJOR version with any feature additions or changes.

If there are no changes in functionality, such as bug fixes, the MINOR version will be updated.

The BUILD version is automatically assigned the number of commits up to that revision. It is internal and not public.

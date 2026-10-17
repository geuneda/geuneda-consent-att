# Geuneda.Consent.Att - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.consent.att`
- **Unity**: 2021.3+
- **의존성**: `com.geuneda.consent` 0.1.0(`ITrackingAuthorization`), `com.geuneda.core` 0.4.0(결과 코드, 로거, 메인 스레드 디스패처), UniTask 2.5.10
- **역할**: iOS ATT 네이티브 브리지와 추적 설명 문구 빌드 후처리. D3 결정(네이티브 코드는 별도 패키지)에 따라 consent 에서 떼어 냈습니다
- **레퍼런스**: Percent `AppTrackingTransparencyService`(Android 는 스텁), SpellDefense `AttBootstrapper`(500ms 폴링, 60초 상한)·`AttUsageDescriptionPostBuild`(언어별 InfoPlist.strings)

## 2. 구조

| 파일 | 역할 |
|------|------|
| `Runtime/AttTrackingAuthorization.cs` | 공급자(묻기 조건, 진행 중, 창이 안 뜬 경우 경고) |
| `Runtime/Internal/IAttPlatform.cs` | 네이티브 경계(테스트 교체) |
| `Runtime/Internal/AttIosPlatform.cs`, `Runtime/Plugins/iOS/GeunedaAtt.mm` | iOS 브리지(GCHandle, 앱 활성 대기, 창 전 취소) |
| `Runtime/Internal/AttUnsupportedPlatform.cs` | 그 밖의 플랫폼 |
| `Runtime/Internal/AttStatuses.cs` | 네이티브 값 → `TrackingStatus` |
| `Editor/AttUsageDescription.cs` | 문구 설정 에셋(검사, 찾기) |
| `Editor/AttPostprocessBuild.cs` | iOS 후처리(Info.plist, InfoPlist.strings, 약한 링크). `#if UNITY_IOS` |
| `Editor/InfoPlistStrings.cs` | `.strings` 텍스트 합치기·이스케이프(iOS 모듈 없이 테스트) |

## 3. 규칙

- **폴링하지 않습니다.** completion 콜백만 씁니다. SpellDefense 는 500ms 마다 상태를 읽고 60초에 거절로 봤는데, 창이 뜨지 않는 환경에서 60초를 기다렸습니다.
- **앱이 활성일 때 묻습니다**(`UIApplicationDidBecomeActiveNotification`). 비활성 상태에서 물으면 시스템이 창 없이 답합니다. 기다리는 동안의 취소는 관찰자를 지우고 콜백(-2)으로 끝냅니다. 창이 뜬 뒤에는 시스템 창을 닫을 수 없어 C# 이 기다리기만 그만둡니다.
- **콜백은 요청마다 정확히 한 번**, 메인 스레드에서 오고 C# 은 그 콜백에서만 GCHandle 을 풉니다(account.apple 과 같은 방식). 네이티브 콜백 안에서는 예외를 막고 이어지는 코드는 `MainThreadDispatcher.Post` 로 넘깁니다.
- **답은 다시 읽습니다**: 요청이 끝나면 completion 값이 아니라 `trackingAuthorizationStatus` 를 다시 읽어 `Status` 로 씁니다.
- **모르는 네이티브 값은 `Denied`**(추적하지 않는 쪽).
- **빌드 후처리**: 순서 1000. 설정 에셋이 둘 이상이거나 잘못되었거나, 에셋도 다른 도구의 문구도 없으면 `BuildFailedException`. 이미 있는 `InfoPlist.strings` 는 이 키만 바꾸고 파일 참조가 있으면 다시 넣지 않습니다(덧붙이는 빌드). `CFBundleLocalizations` 는 문자열 배열을 유지하고 없는 언어만 더합니다.
- **`.strings` 합치기는 파서로**(`InfoPlistStrings`): 주석, 따옴표·이스케이프, 한 줄의 여러 항목, CRLF 를 구분해 대상 키의 항목만 바꾸고 중복은 하나로 줄입니다. 정규식으로 줄 단위 교체를 하면 같은 줄의 다른 항목이 지워지고 CRLF 파일에서 키가 겹칩니다(2026-10-17 단위 2 리뷰 1회차). 읽을 수 없으면 `FormatException` → `BuildFailedException`.
- **덧붙이는 빌드의 한계**: 설정에서 뺀 언어의 예전 문구와 `CFBundleLocalizations` 언어는 지우지 않습니다(README 에 Replace 빌드 안내). 이 후처리가 쓴 기록을 따로 두지 않기로 했습니다.
- **약한 링크**는 후처리가 UnityFramework 대상에 넣습니다(.mm 의 meta 에 FrameworkDependencies 를 두지 않음). 플러그인이 UnityFramework 에 들어가기 때문입니다.

## 4. 테스트

- EditMode(`Geuneda.Consent.Att.Tests.EditMode`): 가짜 `IAttPlatform` 으로 상태 변환, 묻기 조건, 창 전 취소, 진행 중, 창이 안 뜬 경우. `AttUsageDescription.Validate`, `InfoPlistStrings`.
- iOS 후처리와 네이티브는 임시 프로젝트의 시뮬레이터 빌드로 확인합니다(Info.plist, `.lproj`, 약한 링크, 실행).

## 5. 문서

공개 API 를 바꾸면 `README.md`, `CHANGELOG.md`, 호스트 `claude-skills/geuneda-consent/` 스킬(consent·consent.ump 와 공유), 호스트 `claude-skills/geuneda-frameworks/` 를 함께 고칩니다.

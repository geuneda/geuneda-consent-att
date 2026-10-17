# Geuneda Consent ATT

`com.geuneda.consent` 의 iOS 앱 추적 투명성(ATT) 공급자입니다. 아직 묻지 않았으면 추적 허용을 묻고 답을 기다립니다.

- **콜백으로 답을 받습니다**: 네이티브 브리지(`GeunedaAtt.mm`)가 `requestTrackingAuthorization` 의 completion 으로 답을 받습니다(상태를 주기적으로 읽지 않음).
- **앱이 활성일 때 묻습니다**: 앱이 활성 상태가 아니면 시스템이 창 없이 답하므로, 활성이 될 때까지 기다렸다 묻습니다.
- **추적 설명 문구**: iOS 빌드 후처리가 `NSUserTrackingUsageDescription`(기본 문구와 언어별 `InfoPlist.strings`)을 넣습니다. 문구가 없으면 빌드를 실패시킵니다(문구 없이 물으면 앱이 종료됨).
- **약한 링크**: AppTrackingTransparency 를 UnityFramework 에 약하게 링크해 iOS 13 기기에서도 실행됩니다.
- Android, 에디터, iOS 14 미만은 ATT 없음(`TrackingStatus.NotSupported`)이고 묻지 않습니다.

## 설치

```json
{
  "dependencies": {
    "com.geuneda.core": "https://github.com/geuneda/geuneda-core.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.consent": "https://github.com/geuneda/geuneda-consent.git",
    "com.geuneda.consent.att": "https://github.com/geuneda/geuneda-consent-att.git"
  }
}
```

- 의존성: `com.geuneda.consent` 0.1.0, `com.geuneda.core` 0.4.0, UniTask 2.5.10. 최소 Unity 2021.3.
- 이 패키지를 설치한 게임에만 iOS 플러그인과 프레임워크 링크가 들어갑니다.

## 사용 예

```csharp
using Geuneda.Consent;
using Geuneda.Consent.Att;
using Geuneda.Consent.Ump;   // com.geuneda.consent.ump 를 함께 설치했을 때

var consent = new ConsentService(new ConsentServiceOptions
{
	AdConsent = new UmpAdConsent(),              // 있으면 광고 동의 폼 뒤에 묻습니다
	Tracking = new AttTrackingAuthorization()
});

await consent.RunAsync(cancellationToken);
var tracking = consent.Snapshot.Tracking;          // Authorized, Denied, Restricted, NotDetermined, NotSupported
var allowed = consent.Snapshot.TrackingAllowed;    // Authorized 또는 ATT 없는 플랫폼
```

`ConsentService` 없이 직접 써도 됩니다.

```csharp
var att = new AttTrackingAuthorization();
var result = await att.RequestAsync(cancellationToken);   // 답이 무엇이든 성공(취소·진행 중만 실패)
Debug.Log(att.Status);
```

## 추적 설명 문구

Assets > Create > Geuneda > Consent > ATT Usage Description 으로 설정 에셋(`AttUsageDescription`)을 만들고 문구를 씁니다. 프로젝트에 하나만 둡니다.

| 값 | 들어가는 곳 |
|---|---|
| Description | `Info.plist` 의 `NSUserTrackingUsageDescription`(앱의 개발 언어로) |
| Localized(언어, 문구) | `<언어>.lproj/InfoPlist.strings`. 언어는 `.lproj` 이름(`ko`, `ja`, `zh-Hans`, `zh-Hant` 등). `CFBundleLocalizations` 에 없는 언어는 더합니다 |

- 후처리 순서는 1000 입니다(MAX 등 다른 SDK 의 후처리 뒤). 같은 키를 다른 도구가 넣었으면 이 에셋의 값이 남습니다.
- 이미 있는 `InfoPlist.strings` 는 이 키의 항목만 바꾸고 다른 항목과 주석은 그대로 둡니다(같은 키가 여러 번 있으면 하나로). 읽을 수 없는 파일이면 빌드가 실패합니다.
- 같은 폴더에 덧붙이는(Append) 빌드는 설정에서 뺀 언어의 예전 문구와 `CFBundleLocalizations` 의 언어를 지우지 않습니다. 언어를 빼거나 설정 에셋을 지웠으면 바꾸는(Replace) 빌드를 합니다.
- 설정 에셋이 없으면 다른 도구(MAX 통합 관리자의 동의 흐름 등)가 넣은 문구를 씁니다. 그것도 없으면 iOS 빌드가 `BuildFailedException` 으로 실패합니다.
- 기본 문구가 비었거나, 언어 이름이 잘못되었거나, 같은 언어가 두 번 있으면 빌드가 실패합니다.

## 동작

| 상황 | `RequestAsync` |
|---|---|
| ATT 없는 플랫폼, 이미 답함 | 묻지 않고 바로 성공 |
| 아직 묻지 않음 | 묻고 답이 오면 성공(답은 `Status`) |
| 앱이 비활성 | 활성이 되면 묻습니다 |
| 물었는데 창이 뜨지 않음(시스템 사정) | 성공, `Status` 는 `NotDetermined`, Warning 로그. 다음 실행에서 다시 묻습니다 |
| 창을 띄우기 전 취소 | `Cancelled`(묻지 않음) |
| 창이 뜬 뒤 취소 | `Cancelled`. 시스템 창은 닫을 수 없어 사용자의 답은 기기에 저장됩니다 |
| 겹친 호출 | `InProgress` |

설정의 '앱이 추적을 요청하도록 허용'이 꺼져 있으면 창 없이 `Denied` 입니다.

## 한계

- 확인한 범위(2026-10-17): Xcode 27.1 로 시뮬레이터 빌드·링크(번들의 `ko.lproj`·`ja.lproj` InfoPlist.strings, UnityFramework 의 AppTrackingTransparency 약한 링크 `LC_LOAD_WEAK_DYLIB`), iOS 18.6 시뮬레이터에서 상태 읽기(`NotDetermined`), UMP 동의 폼 뒤 ATT 창 표시와 허용 답의 콜백(`Authorized`). 실제 기기와 거부·제한 답은 확인하지 않았습니다.
- 추적 허용 전에 보여줄 안내 화면(사전 안내)은 게임이 만듭니다.
- 설정 앱에서 답을 바꾸면 다음에 `Status` 를 읽을 때 바뀐 값이 보입니다(변경 이벤트는 없음).

## 테스트

- `Tests/EditMode`(`Geuneda.Consent.Att.Tests.EditMode`): 가짜 네이티브로 상태 변환, 묻기 조건, 취소, 진행 중. 설정 에셋 검사와 `InfoPlist.strings` 합치기.

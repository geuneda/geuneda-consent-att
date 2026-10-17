# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.1.0] - 2026-10-17

**New**:
- `AttTrackingAuthorization`(`ITrackingAuthorization`): iOS 14 이상에서 아직 묻지 않았으면 ATT 허용을 묻고 completion 콜백으로 답을 받음. 앱이 활성이 될 때까지 기다렸다 물음. 창을 띄우기 전 취소는 묻지 않고 `Cancelled`, 창이 뜬 뒤 취소는 기다리기만 그만둠. 겹친 호출은 `InProgress`. 창이 뜨지 않았으면 Warning
- 그 밖의 플랫폼(Android, 에디터, iOS 14 미만)은 `TrackingStatus.NotSupported`
- iOS 빌드 후처리: 설정 에셋 `AttUsageDescription` 의 문구를 `NSUserTrackingUsageDescription`(Info.plist)과 언어별 `InfoPlist.strings` 에 넣고 `CFBundleLocalizations` 에 언어를 더함. 문구가 없으면 빌드 실패. AppTrackingTransparency 를 UnityFramework 에 약하게 링크

// Geuneda.Consent.Att 의 ATT 플러그인(AttIosPlatform.cs)
// 상태 값: -1 ATT 없음(iOS 14 미만), 0~3 ATTrackingManagerAuthorizationStatus, 콜백의 -2 는 창을 띄우기 전에 취소
// ARC 로 컴파일한다(meta 의 CompileFlags -fobjc-arc). AppTrackingTransparency 는 빌드 후처리가 약하게 링크한다(iOS 13 기기에서 실행되게)
// 콜백은 요청마다 정확히 한 번, 메인 스레드에서 부른다
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

typedef void (*GeunedaAttCallback)(intptr_t context, int status);

static const int GeunedaAttNotSupported = -1;
static const int GeunedaAttCancelledBeforePrompt = -2;

@interface GeunedaAttRequest : NSObject
@property (nonatomic, assign) intptr_t context;
@property (nonatomic, assign) GeunedaAttCallback callback;
@property (nonatomic, strong) id activeObserver;
@property (nonatomic, assign) BOOL requested;
@property (nonatomic, assign) BOOL finished;
@end

// 앱이 활성이 될 때까지 기다리는 요청을 붙잡아 둔다. 키는 C# 의 GCHandle 값이고 다음 요청이 같은 값을 다시 쓸 수 있다
static NSMutableDictionary<NSNumber*, GeunedaAttRequest*>* GeunedaAttPending()
{
    static NSMutableDictionary<NSNumber*, GeunedaAttRequest*>* pending;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ pending = [[NSMutableDictionary alloc] init]; });
    return pending;
}

@implementation GeunedaAttRequest

// C# 은 이 콜백에서 GCHandle 을 푼다. 메인 스레드에서만 부른다
- (void)finishWithStatus:(int)status
{
    if (self.finished)
    {
        return;
    }

    self.finished = YES;

    if (self.activeObserver != nil)
    {
        [[NSNotificationCenter defaultCenter] removeObserver:self.activeObserver];
        self.activeObserver = nil;
    }

    intptr_t context = self.context;

    // 대기 목록에서 이 요청만 뺀다(같은 키를 다음 요청이 쓸 수 있다)
    if (GeunedaAttPending()[@(context)] == self)
    {
        [GeunedaAttPending() removeObjectForKey:@(context)];
    }

    self.callback(context, status);
}

- (void)start API_AVAILABLE(ios(14.0))
{
    if (self.finished || self.requested)
    {
        return;
    }

    self.requested = YES;

    if (self.activeObserver != nil)
    {
        [[NSNotificationCenter defaultCenter] removeObserver:self.activeObserver];
        self.activeObserver = nil;
    }

    // completion 은 아무 큐에서나 온다
    [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
        dispatch_async(dispatch_get_main_queue(), ^{
            [self finishWithStatus:(int)status];
        });
    }];
}

@end

extern "C" int GeunedaAtt_Status()
{
    if (@available(iOS 14.0, *))
    {
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    }

    return GeunedaAttNotSupported;
}

// 앱이 활성 상태가 아니면 시스템이 창을 띄우지 않고 바로 답하므로, 활성이 될 때 묻는다
extern "C" void GeunedaAtt_Request(intptr_t context, GeunedaAttCallback callback)
{
    if (@available(iOS 14.0, *))
    {
        GeunedaAttRequest* request = [[GeunedaAttRequest alloc] init];
        request.context = context;
        request.callback = callback;
        GeunedaAttPending()[@(context)] = request;

        if ([UIApplication sharedApplication].applicationState == UIApplicationStateActive)
        {
            [request start];
            return;
        }

        __weak GeunedaAttRequest* weakRequest = request;
        request.activeObserver = [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidBecomeActiveNotification
                                                                                   object:nil
                                                                                    queue:[NSOperationQueue mainQueue]
                                                                               usingBlock:^(NSNotification* notification) {
            [weakRequest start];
        }];
        return;
    }

    callback(context, GeunedaAttNotSupported);
}

// 창을 띄우기 전이면 취소로 끝낸다(콜백으로 C# 핸들 해제). 이미 물었으면 시스템 창은 닫을 수 없어 답이 올 때 끝난다
extern "C" void GeunedaAtt_Cancel(intptr_t context)
{
    GeunedaAttRequest* request = GeunedaAttPending()[@(context)];

    if (request != nil && !request.requested)
    {
        [request finishWithStatus:GeunedaAttCancelledBeforePrompt];
    }
}

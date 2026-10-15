// iPhone taptic feedback for Out of Office. Called from Haptics.cs.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *haptic_impact[5];
static UINotificationFeedbackGenerator *haptic_notify;

static UIImpactFeedbackGenerator *haptic_generator(int style)
{
    if (haptic_impact[style] == nil)
    {
        UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
        if (style == 1) s = UIImpactFeedbackStyleMedium;
        if (style == 2) s = UIImpactFeedbackStyleHeavy;
        if (style == 3) s = UIImpactFeedbackStyleSoft;
        if (style == 4) s = UIImpactFeedbackStyleRigid;
        haptic_impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    }
    return haptic_impact[style];
}

// Creates every generator and wakes the Taptic Engine, so the first real tap has no delay.
extern "C" void _OutOfOfficeHapticPrepare()
{
    for (int i = 0; i <= 4; i++) [haptic_generator(i) prepare];
    if (haptic_notify == nil) haptic_notify = [[UINotificationFeedbackGenerator alloc] init];
    [haptic_notify prepare];
}

extern "C" void _OutOfOfficeHaptic(int style, float intensity)
{
    if (style >= 0 && style <= 4)
    {
        [haptic_generator(style) impactOccurredWithIntensity:intensity];
        [haptic_impact[style] prepare];
        return;
    }
    if (haptic_notify == nil) haptic_notify = [[UINotificationFeedbackGenerator alloc] init];
    [haptic_notify notificationOccurred:(style == 5 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError)];
}

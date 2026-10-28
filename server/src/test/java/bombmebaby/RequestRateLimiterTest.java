package bombmebaby;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;

class RequestRateLimiterTest {
    @Test void acceptsLimitAndRejectsNextFrameUntilExactExpiry() {
        var limiter = new RequestRateLimiter();
        for (int i = 0; i < 120; i++) assertTrue(limiter.allow(0));
        assertFalse(limiter.allow(0));
        assertFalse(limiter.allow(999_999_999L));
        for (int i = 0; i < 120; i++) assertTrue(limiter.allow(1_000_000_000L));
        assertFalse(limiter.allow(1_000_000_000L));
    }

    @Test void slidingWindowDoesNotResetAtWallClockSecondBoundary() {
        var limiter = new RequestRateLimiter();
        for (int i = 0; i < 60; i++) assertTrue(limiter.allow(900_000_000L));
        for (int i = 0; i < 60; i++) assertTrue(limiter.allow(1_100_000_000L));
        assertFalse(limiter.allow(1_800_000_000L));
        for (int i = 0; i < 60; i++) assertTrue(limiter.allow(1_900_000_000L));
        assertFalse(limiter.allow(1_900_000_000L));
        assertTrue(limiter.allow(2_100_000_000L));
    }

    @Test void sustainedUnityCadenceSurvivesClockWrapAndRingReuse() {
        var limiter = new RequestRateLimiter();
        long start = Long.MAX_VALUE - 500_000_000L;
        for (int i = 0; i < 1000; i++)
            assertTrue(limiter.allow(start + i * 30_000_000L));
    }
}
